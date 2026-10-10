# LionQuest 3D — Project Knowledge

## Game Overview

Top-down 3D action game. Players earn **souls** by killing enemies and spend them to upgrade character stats. Each character class has a unique ID and persists stats across sessions via JSON save.

---

## Soul & Progression System

### Earning souls
- `GameSaveController.OnEnemyKilled()` awards **+1 soul** to every registered active player on each enemy kill.
- Souls persist to disk immediately via `SaveManager.SaveCharacter()`.

### Spending souls — stat upgrades
Universal stats (Strength/Health/Speed) are purchased as levels, same as before. Skills are purchased
as discrete **skill-tree nodes** (see `doc/characterUpgrade.md`) — each skill starts locked and its
tree's root `Unlock` node must be bought before the skill (and any of its other nodes) works.

| Upgrade | Cost formula | Effect |
|---|---|---|
| Strength | `strengthLevel * 5` souls | +0.2× damage multiplier per level |
| Health | `healthLevel * 5` souls | +20 max HP per level |
| Speed | `speedLevel * 5` souls | Passive move-speed multiplier `1 + (speedLevel-1)*0.1`, applied in `TopDownPlayerController.HandleMovement()` |
| Skill node (any skill, any node) | node's own authored `cost` | Node's own authored `(effect, op, value)` — stacked with every other purchased node on that skill via `CharacterStats.GetSkillValue` |

Universal stat cost still scales linearly (level 1→2 costs 5, 2→3 costs 10, …). Skill-tree node costs
are each authored individually on `CharacterDefinition.skillTrees[].nodes[].cost` — no formula, no level.

### Save format
`gamesave.json` at `Application.persistentDataPath`. Top-level: `GameSaveData { List<CharacterSaveData> }`.

Per character: `characterID`, `souls`, `strengthLevel`, `healthLevel`, `speedLevel`, `List<string> purchasedSkillNodes`.

`purchasedSkillNodes` holds `"skillId/nodeId"` keys (e.g. `"battlecry/magnitude_1"`). It is the **only**
persisted skill state — the tree itself (`CharacterDefinition.skillTrees`) is the source of truth for
what each node does; resolved values are rebuilt in memory (`CharacterStats.RebuildSkillCache`) on load
and on purchase, never serialized. This replaced the old per-skill level fields
(`skill1DamageLevel`/`skill1RadiusLevel`/`skill{1,2,3}CooldownLevel`) and the orphaned `unlockedSkills`
stub, none of which exist anymore.

---

## Map Generation & Region Difficulty

### Overview
Every run generates a fresh, randomly-seeded island terrain via `MapGenerator.GenerateMap(int seed)`.
The island is one continuous landmass — regions are difficulty *bands* by distance from the island
centre, not separate zones behind gates. A player can always walk from the start region to the
outermost region; nothing is geometrically locked. Difficulty is the only thing that decides whether
they can survive once they get there.

### Seeding
`MapGenerator.GenerateMap(int seed)` calls `Random.InitState(seed)` before any randomized pass (path
waypoints, hole placement, region ground-type biasing). Height noise itself is coordinate-deterministic
Perlin and does not depend on the seed. `MapGenerator.Awake()` picks a fresh seed automatically every
scene load via `GenerateRandomSeed()` (`System.Guid`-derived) — so retrying a run (which reloads the
scene) always lands on a new map layout. The seed is not persisted anywhere; it exists only for the
lifetime of one generated scene, queryable at runtime via `GetLastGeneratedSeed()` for logging/debugging.

### Regions
`RegionDefinition` (`Assets/Scripts/RegionDefinition.cs`) ScriptableObjects define distance bands from
`MapGenerator.GetIslandCenter()`, each covering a `minFraction..maxFraction` of `GetPlayableRadius()`.
`MapGenerator.GetRegionAt(Vector3 worldPosition)` resolves which region a point falls in at runtime —
a sharp radius-fraction cutoff, no boundary noise (the existing splatmap gather already softens visual
transitions; a region boundary is a gameplay/spawn concept, not a visual one). Level1 authors 5 bands
via `Level1Generator.BuildRegions()`, sized against a 280×280 map (`IslandRadius=85`, `BeachWidth=10`,
`GetPlayableRadius()=90`):

| Region | Distance band | Density multiplier | Hole density multiplier | Archetype restriction |
|---|---|---|---|---|
| Start Meadow | 0.00–0.20 | 1.0× | 1.0× | none (all tiers) |
| Wildwood | 0.20–0.42 | 1.3× | 1.0× | none |
| Ashen Flats | 0.42–0.65 | 1.6× | 1.3× | none |
| Shattered Reach | 0.65–0.85 | 2.0× | 1.6× | Ranged/Elite/Boss only |
| The Maw | 0.85–1.00 | 2.5× | 2.0× | Elite/Boss only |

`GenerateHoles()` reads each candidate hole's region and scales how many holes stamp there by
`holeDensityMultiplier` (integer part stamps that many, the fractional remainder is a coin-flip for
one more) — outer regions end up hazard-denser than the old flat inner-50%-uniform rule. `RegionDefinition`
also carries a `groundBias` weighted-`GroundType` list for biasing terrain painting per region, but
Level1's authored regions leave it empty today — see Known Gaps.

### Difficulty feeding WaveSpawner
`WaveSpawner` picks a spawn region *after* choosing an enemy's archetype (`PickSpawnRegion`), weighted
by `densityMultiplier` among the regions that archetype is eligible for. A restricted archetype (e.g.
Elite/Boss-only) can therefore never be assigned a region it isn't allowed in — no reject-and-retry
loop needed. Wave-number gating (`archetype.minWave`) and spatial gating stack independently: a Boss
archetype still needs `minWave` reached, but will never spawn in Start Meadow regardless of wave number.
The hand-placed opening wave (wave 1, `Level1Generator.SpawnEnemies`) is unaffected — it doesn't consult
regions at all, since its 3 fixed rings only need to clear `Enemy.pullRange` (15u), not express difficulty.

### Progression loop tie-in
Souls and skill-tree purchases never wipe on death or on map regeneration — no map/seed/region state is
persisted anywhere in `GameSaveData`/`CharacterSaveData`, and `PauseMenuController`'s retry path reloads
the scene, which both re-seeds the map (via `MapGenerator.Awake()`) and preserves every purchased
stat/skill node exactly as before. Practically: dying near the edge of Wildwood on one attempt, buying a
skill node with the souls earned, and retrying puts the player in a *different* Wildwood-equivalent
layout, but strictly stronger than before — the intended "upgrade lets you push further out" loop.

---

## Damage Calculation

### Melee / Skill 1
```
final damage = attackData.damage × CombatController.damageMultiplier
```
- `attackData.damage` — flat value in `AttackData` ScriptableObject (e.g. Cappuccino basic = 60)
- `damageMultiplier` — set from `CharacterStats.GetDamageMultiplier()` on init and on each upgrade

Formula: `GetDamageMultiplier() = 1 + (strengthLevel - 1) * 0.2`
- Strength lvl 1 = 1.0× (no bonus)
- Strength lvl 2 = 1.2×
- Strength lvl 3 = 1.4×

### Skill 2 projectile
```
final damage = projectileSkillData.damage × CombatController.damageMultiplier
```
- Previously broken (hardcoded `1f`). Fixed by adding `CombatController.GetDamageMultiplier()` getter and calling it in `TopDownPlayerController.SpawnProjectile()`.

### Enemy health formula
`GetMaxHealth() = 100 + (healthLevel - 1) * 20`

---

## Combat Flow

### Melee hit
```
Input → TopDownPlayerController → CombatController.PerformAttack(attackData)
  → Instantiate effectPrefab → AttackEffect.Initialize(damage, ...)
    → OnTriggerEnter → Enemy.TakeDamage(damage)
```
- `AttackEffect` tracks `hitTargets` (HashSet) to prevent hitting same enemy twice per swing.
- Collider active for `hitWindowDuration` (default 0.15s), then disabled. Visual (particle) lives longer.

### Skill 2 projectile
```
Input → TopDownPlayerController → StartCoroutine(SpawnProjectilesOverTime)
  → SpawnProjectile() → Instantiate projectilePrefab → Projectile.Initialize(damage, ...)
    → OnTriggerEnter → Enemy.TakeDamage(damage)
```
- Timing driven by `ProjectileSkillData.spawnTimesNormalized[]` — spawn times as % of animation length.

### Enemy death → soul reward
```
Enemy.Die() → OnDeath event → GameSaveController.OnEnemyKilled() → CharacterStats.AddSouls(1)
```

---

## Key Scripts

| Script | Role |
|---|---|
| `CharacterStats.cs` | Stat levels, soul currency, getters, save/load |
| `CombatController.cs` | Attack execution, damage/knockback multipliers, cooldown |
| `AttackData.cs` | ScriptableObject: base damage, knockback, cooldown, effect prefab |
| `ProjectileSkillData.cs` | ScriptableObject: projectile damage, speed, spawn timing |
| `AttackEffect.cs` | Collision-based hit detection for melee/area effects |
| `Projectile.cs` | Projectile movement + hit detection |
| `Enemy.cs` | Enemy health, TakeDamage, Die, soul drop trigger |
| `TopDownPlayerController.cs` | Input, movement, attack/skill dispatch, stat wiring |
| `GameSaveController.cs` | Singleton, enemy kill events, player registration |
| `SaveManager.cs` | JSON read/write, per-character save slots |
| `SaveDataTypes.cs` | `GameSaveData`, `CharacterSaveData` — serializable POCOs |
| `MapGenerator.cs` | Procedural terrain, ocean falloff, region-band lookup, seeded generation |
| `RegionDefinition.cs` | ScriptableObject: distance band, archetype eligibility, density/hole multipliers |
| `WaveSpawner.cs` | Runtime wave spawning after the opening wave, region-aware archetype/position selection |

---

## Known Gaps / Orphaned Code

- **Skill tree UI polish** — `SkillTreeScreenController` (character-select panel) covers buy/navigate/close;
  no respec, no exclusive-node builds (all nodes are eventually buyable by design, see `doc/characterUpgrade.md`).
- ~~`unlockedSkills`~~ and ~~per-skill level fields~~ (`skill1DamageLevel`/`skill1RadiusLevel`/
  `skill{1,2,3}CooldownLevel`) — **resolved**: replaced by `purchasedSkillNodes` + the skill-tree node
  graph (`doc/characterUpgrade.md`).
- ~~`speedLevel` orphaned~~ — **resolved**: now the universal passive move-speed stat
  (`CharacterStats.GetMoveSpeedMultiplier()`), applied in `TopDownPlayerController.HandleMovement()`.
- **Region visual identity is density/hazard-bias only** — Level1's 5 authored regions share the same 4
  GroundTypes (Grass/Dirt/Hole/Sand, the URP terrain shader's per-pass layer ceiling) and are told apart
  by hole density and enemy tier, not distinct textures; `RegionDefinition.groundBias` exists to bias
  ground-type painting per region but is left empty by `Level1Generator.BuildRegions()`. A future pass
  could populate it, or add per-region fog tint/lighting, to make the boundary more visually legible
  without spending a texture-layer slot.

---

## Character: AssassinoCapuchino (`AssCap`)

- Basic attack damage: **60** (`Assets/Characters/AssassinoCapuchino/BasicAttack.asset`)
- All three skills start **locked** — each skill's tree root `Unlock` node must be purchased (20 souls)
  before it can be used. Values below are the skill's authored base (SO) once unlocked, at zero
  additional nodes purchased.
- Skill 1 (Whirlwind): player-centered AoE, damage = `basicAttackData.damage × GetSkillValue("whirlwind", Damage, 2f)`, radius = `GetSkillValue("whirlwind", Area, 3f)`. Effect prefab `Whirlwind Slash AssCap.prefab`, data asset `Skill1Attack.asset`. 10s base cooldown, resolved via `GetSkillValue("whirlwind", Cooldown, 10f)`. Tree adds `damage_1/2`, `area_1`, `cooldown_2s/3s` nodes.
- Skill 2 (shuriken): **25** base damage (`ShurikenProjectile.asset`), 2 projectiles/throw = 50/throw at strength lvl 1. Tree adds `damage_1`, `count_1` (+1 projectile), `cooldown_2s` nodes.
- Skill 3 (Battle Cry): self speed buff, magnitude = `GetSkillValue("battlecry", Magnitude, 2f)`, duration = `GetSkillValue("battlecry", Duration, 5f)`, cooldown = `GetSkillValue("battlecry", Cooldown, 10f)` (`BattleCry.asset`, `BattleCrySkillData`). Tree adds `magnitude_1/2`, `duration_1/2`, `cooldown_2s` nodes.
- All three skills share one **global skill lock**: activating any skill blocks all skills (not basic attack) for that skill's cooldown.
- Enemy base health: **100** → 2 basic-attack hits to kill at strength lvl 1 (60+60=120), or one Whirlwind hit (120 at base) at zero extra damage nodes.
