# LionQuest 3D — Project Knowledge

## Game Overview

Top-down 3D action game. Players earn **souls** by killing enemies and spend them to upgrade character stats. Each character class has a unique ID and persists stats across sessions via JSON save.

---

## Soul & Progression System

### Earning souls
- `GameSaveController.OnEnemyKilled()` awards **+1 soul** to every registered active player on each enemy kill.
- Souls persist to disk immediately via `SaveManager.SaveCharacter()`.

### Spending souls — stat upgrades
Upgrades are purchased directly with souls. No level gate, no skill point pool.

| Upgrade | Cost formula | Effect |
|---|---|---|
| Strength | `strengthLevel * 5` souls | +0.2× damage multiplier per level |
| Health | `healthLevel * 5` souls | +20 max HP per level |
| Speed | *(not implemented)* | `speedLevel` saved/loaded but no getter or effect |

Cost scales linearly: level 1→2 costs 5, 2→3 costs 10, 3→4 costs 15, etc.

### Save format
`gamesave.json` at `Application.persistentDataPath`. Top-level: `GameSaveData { List<CharacterSaveData> }`.

Per character: `characterID`, `souls`, `strengthLevel`, `healthLevel`, `speedLevel`, `List<string> unlockedSkills`.

`unlockedSkills` is a stub — saved/loaded but nothing reads or gates on it.

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

---

## Known Gaps / Orphaned Code

- **`speedLevel`** — saved and loaded, no getter (`GetMoveSpeedMultiplier`), never applied to `moveSpeed`.
- **`unlockedSkills`** — serialized list, nothing reads or gates on it. No unlock UI or logic.
- **Cooldown stat** — no `cooldownLevel`, no reduction formula, cooldown is flat `attackData.cooldown`.
- **Upgrade UI** — `TryUpgradeStrength()` / `TryUpgradeHealth()` exist on `CharacterStats` but no UI script found that calls them.

---

## Character: AssassinoCapuchino (`AssCap`)

- Basic attack damage: **60** (`Assets/Characters/AssassinoCapuchino/BasicAttack.asset`)
- Skill 2 (shuriken): **15** base damage (`ShurikenProjectile.asset`)
- Enemy base health: **100** → 2 basic-attack hits to kill at strength lvl 1 (60+60=120)
