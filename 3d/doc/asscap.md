# AssassinoCapuchino (`AssCap`) — Finalization Plan

## Goal

Finalize the AssCap character's two skills:

1. **Skill 2 — Shuriken throw**: give it *proper damage* (verify the damage chain, set a sensible base value so it's a meaningful ranged option).
2. **Skill 3 — Battle Cry**: a self-buff that temporarily multiplies the character's **movement speed by 2×** (default). The multiplier is upgradable with **souls** (spend to increase the boost above 2×).

This closes out AssCap so both skills are functional and stat-integrated.

---

## Environment

- Unity **6000.3.2f1** (Unity 6), URP
- Global namespace (no namespaces), physics-based movement (`Rigidbody.AddForce`)
- Damage flows through `CombatController.damageMultiplier` (set from `CharacterStats`)

---

## Current State (verified against code)

### Skill 2 — Shuriken (already wired, damage works)

- Data: `Assets/Characters/AssassinoCapuchino/ShurikenProjectile.asset` (`ProjectileSkillData`) → `damage: 15`, `spawnTimesNormalized: [0.4, 0.7]` (2 projectiles per throw), `cooldown: 2`, `projectileSpeed: 15`.
- Prefab: `Assets/Characters/AssassinoCapuchino/Shuriken.prefab` (has `Projectile` script).
- Flow: `TopDownPlayerController.HandleSkills()` → `SpawnProjectilesAtTimes()` → `SpawnProjectile()` which already applies:
  ```csharp
  projectile.Initialize(skillData.damage * combatController.GetDamageMultiplier(), ...)
  ```
- **Conclusion:** damage IS applied and DOES scale with `strengthLevel`. The "missing damage" is a **tuning** problem, not a broken chain: 15 × 2 projectiles = **30 damage/throw** vs enemy **100 HP** (≈4 throws, 8s with cooldown). Too weak to feel like a skill.

### Skill 3 — Battle Cry (does NOT exist as a buff)

- `HandleSkills()` skill3 branch calls `combatController.PerformAttack(skill3AttackData)` — that path is for **melee/AttackData** attacks (requires an `effectPrefab`), NOT a self-buff. Wrong mechanism entirely.
- `speedLevel` exists in `CharacterStats` + `CharacterSaveData` but is **orphaned** — saved/loaded, no getter, never applied (see `doc/project.md` Known Gaps). We reuse it as the battle-cry magnitude stat.
- `moveSpeed` is a private serialized field in `TopDownPlayerController`; `currentSpeed` is computed each frame at line ~431:
  ```csharp
  currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f) * groundSpeedMultiplier;
  ```
  A battle-cry multiplier hooks in here cleanly.

---

## Design Decisions (locked — implement without asking)

1. **Shuriken base damage → `25`** (per projectile). 25 × 2 = 50/throw at strength lvl 1; scales with strength multiplier. Meaningful but still under a basic-attack combo (60+60). *(Value editable in the `.asset` afterward — this is the starting number.)*
2. **Battle Cry magnitude uses `speedLevel`** (repurpose the orphaned stat — no save-format change needed).
3. **Battle Cry multiplier formula:** `2 + (speedLevel - 1) × 0.25`.
   - lvl 1 = **2.0×** (default), lvl 2 = 2.25×, lvl 3 = 2.5×, …
4. **Battle Cry upgrade cost:** `speedLevel * 5` souls (matches strength/health cost pattern).
5. **Battle Cry duration:** `5s` (config field, see `BattleCrySkillData`). **Cooldown:** `10s`.
6. **Buff affects movement only** (not sprint stacking specifics beyond the multiply — it multiplies final `currentSpeed`, so it composes with sprint & ground). Buff does NOT stack with itself: re-press during active buff is blocked by cooldown.
7. New buff config is a **ScriptableObject** (`BattleCrySkillData`) to match the `AttackData` / `ProjectileSkillData` convention.

---

## Part 1 — Shuriken Proper Damage

### Change

`Assets/Characters/AssassinoCapuchino/ShurikenProjectile.asset`:
```yaml
damage: 25   # was 15
```

That's the only required change — the chain already multiplies by strength. Editing the `.asset` YAML directly is safe (single scalar).

### Optional (only if desired later)

- Adjust `spawnTimesNormalized` count to change projectiles/throw.
- Adjust `cooldown` for DPS tuning.

### Acceptance

- Throwing shuriken at strength lvl 1 deals 25/projectile (50/throw).
- After a strength upgrade, per-hit damage increases (e.g. lvl 2 = 25 × 1.2 = 30/projectile). Verify via `Projectile.debugProjectile` log.

---

## Part 2 — Battle Cry Skill (Skill 3)

### 2a. New ScriptableObject — `BattleCrySkillData.cs`

`Assets/Scripts/BattleCrySkillData.cs` — mirrors `ProjectileSkillData` style.

```csharp
using UnityEngine;

/// <summary>
/// ScriptableObject defining a temporary self-buff skill (Battle Cry).
/// The speed multiplier magnitude comes from CharacterStats (speedLevel);
/// this asset holds duration/cooldown and optional VFX/SFX hooks.
/// </summary>
[CreateAssetMenu(fileName = "New Battle Cry Skill", menuName = "LionQuest/Battle Cry Skill Data")]
public class BattleCrySkillData : ScriptableObject
{
    [Header("Skill Info")]
    public string skillName = "Battle Cry";

    [Header("Buff")]
    [Tooltip("How long the speed buff lasts (seconds)")]
    public float duration = 5f;

    [Tooltip("Cooldown before Battle Cry can be used again (seconds)")]
    public float cooldown = 10f;

    [Header("VFX/SFX (optional)")]
    [Tooltip("Optional effect spawned on the caster when Battle Cry activates")]
    public GameObject activateEffectPrefab;
}
```

> NOTE: the speed multiplier value is intentionally NOT stored here — it comes from `CharacterStats.GetBattleCryMultiplier()` so souls upgrades take effect.

### 2b. `CharacterStats.cs` — new getter, cost, upgrade method

Add alongside the existing strength/health methods (reuse `speedLevel`, already saved/loaded):

```csharp
// --- Battle Cry (speed buff) ---

public int GetSpeedCost() => speedLevel * 5;

/// <summary>Battle Cry temporary move-speed multiplier. Default 2× at lvl 1.</summary>
public float GetBattleCryMultiplier()
{
    return 2f + ((speedLevel - 1) * 0.25f);
}

public bool TryUpgradeSpeed()
{
    int cost = GetSpeedCost();
    if (souls >= cost)
    {
        souls -= cost;
        speedLevel++;
        SaveStats();
        OnStatsUpgraded?.Invoke();
        OnSoulsChanged?.Invoke(souls);
        return true;
    }
    return false;
}
```

- `speedLevel` is already serialized in `SaveStats()`/`LoadStats()` — **no `SaveDataTypes` change needed**.
- Removes `speedLevel` from the orphaned-code list in `doc/project.md` (update that doc's Known Gaps + AssCap section after implementing).

### 2c. `TopDownPlayerController.cs` — activate the buff

**New serialized field** (Combat header, next to `skill3AttackData`):
```csharp
[Tooltip("Battle Cry buff data for Skill 3 (self speed buff). If set, Skill 3 uses this instead of skill3AttackData.")]
[SerializeField] private BattleCrySkillData skill3BattleCryData;
```

**New runtime fields** (near `lastSkill2ProjectileTime`):
```csharp
private float battleCrySpeedMultiplier = 1f;
private float lastSkill3BattleCryTime = -999f;
private Coroutine battleCryRoutine;
```

**Apply multiplier** in `HandleMovement()` where `currentSpeed` is computed:
```csharp
currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f)
             * groundSpeedMultiplier * battleCrySpeedMultiplier;
```

**Rewrite the skill3 branch** in `HandleSkills()` to prefer the battle-cry path. **Delete the entire existing skill3 block** (the `if (skill3Action != null && skill3Action.WasPressedThisFrame())` block that calls `PerformAttack(skill3AttackData)`) and replace it with the code below — do not leave the old block in place:
```csharp
if (skill3Action != null && skill3Action.WasPressedThisFrame())
{
    if (skill3BattleCryData != null)
    {
        // Battle Cry self-buff (cooldown-gated, no stacking)
        if (Time.time - lastSkill3BattleCryTime >= skill3BattleCryData.cooldown)
        {
            lastSkill3BattleCryTime = Time.time;

            if (characterAnimator != null)
                characterAnimator.TriggerSkill3();

            if (battleCryRoutine != null) StopCoroutine(battleCryRoutine);
            battleCryRoutine = StartCoroutine(BattleCryRoutine(skill3BattleCryData));
        }
    }
    else
    {
        // Legacy fallback: AttackData-based skill 3
        if (characterAnimator != null)
            characterAnimator.TriggerSkill3();

        if (combatController != null && skill3AttackData != null)
            combatController.PerformAttack(skill3AttackData);
    }
}
```

**New coroutine:**
```csharp
private IEnumerator BattleCryRoutine(BattleCrySkillData data)
{
    float mult = characterStats != null ? characterStats.GetBattleCryMultiplier() : 2f;

    if (data.activateEffectPrefab != null)
        Instantiate(data.activateEffectPrefab, transform.position, transform.rotation, transform);

    battleCrySpeedMultiplier = mult;
    Debug.Log($"[Player] Battle Cry! Speed x{mult} for {data.duration}s");

    yield return new WaitForSeconds(data.duration);

    battleCrySpeedMultiplier = 1f;
    battleCryRoutine = null;
    Debug.Log($"[Player] Battle Cry ended.");
}
```

> Coroutine is safe to leave running on death (buff just expires). If desired, `Die()` can `StopCoroutine(battleCryRoutine)` and reset `battleCrySpeedMultiplier = 1f`.
>
> **Timing note:** `battleCrySpeedMultiplier` is a field read inside `HandleMovement()` (called from `Update()`), and `Update()` runs before `FixedUpdate()` applies forces. Setting it from the coroutine takes effect the same frame it activates — no lag. Reset to `1f` on expiry likewise applies next `HandleMovement()`.

### 2d. New asset — `BattleCry.asset`

Create `Assets/Characters/AssassinoCapuchino/BattleCry.asset` (a `BattleCrySkillData`) with defaults (`duration 5`, `cooldown 10`). Assign to the AssCap prefab's `TopDownPlayerController.skill3BattleCryData` field.

### 2e. Animator

`CharacterAnimator.TriggerSkill3()` already exists and checks for a `Skill3` trigger param. If `ass_capp_animator.controller` lacks a Skill3 state, the buff still works (a warning logs, no animation). Adding a Skill3 animation state is optional polish, not required for function.

---

## Files Touched

| File | Change | Type |
|---|---|---|
| `ShurikenProjectile.asset` | `damage: 15 → 25` | edit (data) |
| `BattleCrySkillData.cs` | **new** ScriptableObject | new script |
| `CharacterStats.cs` | `GetSpeedCost`, `GetBattleCryMultiplier`, `TryUpgradeSpeed` | edit |
| `TopDownPlayerController.cs` | buff field + multiplier in `currentSpeed` + skill3 branch + `BattleCryRoutine` | edit |
| `BattleCry.asset` | **new** `BattleCrySkillData` instance | new asset |
| `AssasinoCappuchino.prefab` | assign `skill3BattleCryData` | wiring |
| `doc/project.md` | remove `speedLevel` from Known Gaps; update AssCap section | doc |

> **`characterStats` / `combatController` are null in the prefab** (`{fileID: 0}`) but this is fine — `TopDownPlayerController.Awake()` auto-wires both via `GetComponent` (lines ~236, ~297). Both components exist on the AssCap prefab. No manual wiring needed for those; only `skill3BattleCryData` must be assigned.
>
> **Prefab/asset wiring caveat:** creating `BattleCry.asset` and assigning it to the prefab is easiest via a small editor helper or `[ContextMenu]` on `PlayerSetupHelper` (per SetupHelper convention) rather than by hand — an agent can generate the asset with `AssetDatabase.CreateAsset` and wire the prefab field via `SerializedObject`. Alternatively assign once in the Inspector (one manual drag).

---

## Implementation Order

1. Edit `ShurikenProjectile.asset` damage → 25. Playtest shuriken.
2. Add `BattleCrySkillData.cs`.
3. Add `CharacterStats` speed getter/cost/upgrade.
4. Edit `TopDownPlayerController` (field, `currentSpeed`, skill3 branch, coroutine).
5. Create `BattleCry.asset`, assign to AssCap prefab `skill3BattleCryData`.
6. Playtest: press Skill 3 → move speed 2× for 5s, 10s cooldown.
7. Update `doc/project.md` (speedLevel no longer orphaned).

---

## Acceptance Criteria

- [ ] Shuriken deals 25 base/projectile and scales with `strengthLevel`.
- [ ] Skill 3 doubles movement speed for 5s (visible via faster movement + debug log).
- [ ] Battle Cry respects 10s cooldown (re-press during active/cooldown does nothing).
- [ ] `TryUpgradeSpeed()` spends souls, raises `speedLevel`, and next Battle Cry is stronger (2.25× at lvl 2).
- [ ] Buff multiplier composes with sprint and ground speed (multiplicative).
- [ ] No errors when `skill3BattleCryData` is null (legacy AttackData path still works).

---

## Out of Scope

- Upgrade UI for the speed stat (souls-spend button) — spending is via `TryUpgradeSpeed()`; UI is a separate feature.
- Battle Cry as a team/party buff (self-only here).
- Skill 3 animation state authoring (optional polish).
- Particle/sound design for the cry.
