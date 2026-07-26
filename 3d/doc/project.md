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
| Speed (Battle Cry) | `speedLevel * 5` souls | Battle Cry speed multiplier `2 + (speedLevel-1)*0.25` |
| Skill 1 damage | `skill1DamageLevel * 5` souls | Whirlwind damage multiplier `2 + (skill1DamageLevel-1)*0.25` (applied to basic-attack damage) |
| Skill 1 radius | `skill1RadiusLevel * 5` souls | Whirlwind hit-circle radius `3 + (skill1RadiusLevel-1)*0.5` world units |
| Skill cooldown (per skill 1/2/3) | `level * 5` souls | Cooldown multiplier `max(0.4, 1 - (level-1)*0.1)` |

Cost scales linearly: level 1→2 costs 5, 2→3 costs 10, 3→4 costs 15, etc.

### Save format
`gamesave.json` at `Application.persistentDataPath`. Top-level: `GameSaveData { List<CharacterSaveData> }`.

Per character: `characterID`, `souls`, `strengthLevel`, `healthLevel`, `speedLevel`, `skill1DamageLevel`, `skill1RadiusLevel`, `skill1CooldownLevel`, `skill2CooldownLevel`, `skill3CooldownLevel`, `List<string> unlockedSkills`.

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

- **`unlockedSkills`** — serialized list, nothing reads or gates on it. No unlock UI or logic.
- **Upgrade UI** — `TryUpgradeStrength()` / `TryUpgradeHealth()` / `TryUpgradeSpeed()` / `TryUpgradeSkill1Damage()` / `TryUpgradeSkill1Radius()` / `TryUpgradeSkillCooldown()` exist on `CharacterStats` but no UI script found that calls them.

---

## Character: AssassinoCapuchino (`AssCap`)

- Basic attack damage: **60** (`Assets/Characters/AssassinoCapuchino/BasicAttack.asset`)
- Skill 1 (Whirlwind): player-centered AoE, damage = `basicAttackData.damage × GetSkill1DamageMultiplier()` (2.0× at lvl 1 = 120, before strength multiplier), radius = `GetSkill1Radius()` (3.0 world units at lvl 1). Effect prefab `Whirlwind Slash AssCap.prefab`, data asset `Skill1Attack.asset`. 10s base cooldown, scaled by `GetSkillCooldownMultiplier(1)`.
- Skill 2 (shuriken): **25** base damage (`ShurikenProjectile.asset`), 2 projectiles/throw = 50/throw at strength lvl 1.
- Skill 3 (Battle Cry): self speed buff, `GetBattleCryMultiplier()` = 2.0× at speedLevel 1, 5s duration, 10s base cooldown (`BattleCry.asset`, `BattleCrySkillData`).
- All three skills share one **global skill lock**: activating any skill blocks all skills (not basic attack) for that skill's cooldown.
- Enemy base health: **100** → 2 basic-attack hits to kill at strength lvl 1 (60+60=120), or one Whirlwind hit (120) at skill1DamageLevel 1.
