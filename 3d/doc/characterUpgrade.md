# Character Upgrade Screen — Implementation Plan

## Goal

A screen where a player spends collected **souls** to upgrade a character. Works **identically for every character** (data-driven — AssCap, 67, and any future character with zero code changes). Upgradable:

- **Health** (universal stat)
- **Strength** (universal stat — damage)
- **Speed** (universal stat — passive base move speed)
- **Skills** (per-character, per-skill, and a skill may expose **multiple independent upgrade tracks** — e.g. AssCap Battle Cry upgrades *magnitude* and *duration* separately)

---

## Environment

- Unity **6000.3.2f1** (Unity 6), URP
- uGUI (Canvas) — same UI layer as `startScreen.md` / `characterSelection.md`
- New Input System 1.17.0, TextMeshPro
- Global namespace, static `SaveManager` persistence

---

## Key Decisions (locked — from user)

1. **Speed is a universal passive stat.** Every character has a base move speed; `speedLevel` = permanent move-speed multiplier applied to all characters. Implements the currently-orphaned `GetMoveSpeedMultiplier`.
2. **Battle Cry (AssCap Skill 3) is a *character-unique skill*, NOT the speed stat.** It temporarily boosts AssCap's move speed for X seconds. It has **two independent, separately-purchasable upgrade tracks**: *magnitude* (how much faster) and *duration* (how many seconds).
3. **Skills upgrade per-skill, with per-skill multiple tracks.** Generic model: each character declares its skills; each skill declares one or more named upgrade tracks. This supersedes asscap.md's "speedLevel = Battle Cry magnitude" decision (see [Impact on asscap.md](#impact-on-asscapmd)).
4. **Entry point: in-game pause/menu overlay.** Opened from an in-game menu. *When it may be opened (anytime vs. only between fights) is a gameplay-gating toggle — default: anytime while paused; see [Entry Point](#entry-point).* 
5. **Data-driven & consistent:** the same screen and code render any character by reading its stats + skill roster. Adding a character = new prefab + definition asset, no upgrade-screen code change.

---

## Current State (verified against code)

### Stats
- `CharacterStats.cs`: `strengthLevel`, `healthLevel`, `speedLevel` (all saved). Has `TryUpgradeStrength()`, `TryUpgradeHealth()`, `GetStrengthCost()`, `GetHealthCost()`, `GetDamageMultiplier()`, `GetMaxHealth()`.
- **Missing:** any speed getter/cost/upgrade. `speedLevel` is saved/loaded but **never applied** (orphaned — `doc/project.md` Known Gaps). `GetMoveSpeedMultiplier` does not exist.
- `TopDownPlayerController` computes `currentSpeed = moveSpeed * (sprint?…) * groundSpeedMultiplier` (~line 431) — no stat multiplier hooked in.

### Skills
- `CharacterSaveData.unlockedSkills` = `List<string>` **stub** — saved/loaded, nothing reads or gates on it. No per-skill levels, no skill metadata.
- Skills today are just ScriptableObject references on `TopDownPlayerController` (`skill2ProjectileData`, `skill3AttackData`/battle-cry). No registry describing "what skills does this character have and how can each be upgraded."

### Roster
- Two characters: `Assets/Characters/AssassinoCapuchino/` (`characterID: AssCap`) and `Assets/Characters/67/` (`characterID` TBD — the `67.prefab` has a `CharacterAnimator` with Skill2/Skill3 params but its skill data/ID need confirming during impl).
- `CharacterDefinition` SO is **planned but not yet created** (spec in `characterSelection.md`). This plan **depends on / extends** it.

### UI
- No `Assets/Scripts/UI/` scripts exist yet. Start screen + selection are also still plans.

---

## Design Overview

Three upgrade categories on one screen:

```
┌──────────────────────────────────────────────┐
│  [Portrait]  Assassino Capuchino    Souls: 240 │
│                                                │
│  ── Stats ──                                   │
│  Health    Lv 3   +20 HP      [ Upgrade  15 ] │
│  Strength  Lv 2   +0.2x dmg   [ Upgrade  10 ] │
│  Speed     Lv 1   +0% move    [ Upgrade   5 ] │
│                                                │
│  ── Skills ──                                  │
│  Shuriken                                      │
│    Damage    Lv 1   25 dmg    [ Upgrade  10 ] │
│  Battle Cry                                    │
│    Magnitude Lv 1   2.0x      [ Upgrade  10 ] │
│    Duration  Lv 1   5.0s      [ Upgrade  10 ] │
│                                                │
│                                    [ Close ]   │
└──────────────────────────────────────────────┘
```

Rows are generated from data. Stat rows are the same 3 for every character. Skill rows come from the character's declared skill roster; each skill contributes one row per upgrade track.

---

## Data Model

### 1. Save format change — `CharacterSaveData` (`SaveDataTypes.cs`)

Replace the `unlockedSkills` stub with a **per-track level dictionary**. `List<string>` is not serializable-friendly for key/value in Unity's `JsonUtility`, so use a serializable list of entries.

```csharp
[System.Serializable]
public class SkillUpgradeEntry
{
    public string trackId;   // e.g. "shuriken.damage", "battlecry.magnitude", "battlecry.duration"
    public int level = 1;
}

[System.Serializable]
public class CharacterSaveData
{
    public string characterID;
    public int souls;

    public int strengthLevel = 1;
    public int healthLevel = 1;
    public int speedLevel = 1;

    // Per-skill-track levels (replaces the old unlockedSkills stub).
    public List<SkillUpgradeEntry> skillTracks = new List<SkillUpgradeEntry>();

    public CharacterSaveData(string id)
    {
        characterID = id;
        souls = 0;
        strengthLevel = 1;
        healthLevel = 1;
        speedLevel = 1;
        skillTracks = new List<SkillUpgradeEntry>();
    }
}
```

> **Migration:** `JsonUtility` tolerates the removed `unlockedSkills` field (old saves just drop it; missing `skillTracks` deserializes to empty → defaults applied). No manual migration code needed. Note the removal in `doc/project.md`.

### 2. Skill upgrade metadata — describes tracks generically

Each character's skills, and the upgrade tracks each skill exposes, are declared in data so the screen is fully generic. Add to the existing (planned) `CharacterDefinition` SO:

```csharp
[System.Serializable]
public class SkillUpgradeTrack
{
    public string trackId;         // stable save key, e.g. "battlecry.magnitude"
    public string displayName;     // "Magnitude"
    public int baseCost = 10;      // souls for level 1→2
    public float costGrowth = 1f;  // cost = baseCost * level * costGrowth (linear like stats)
    public int maxLevel = 0;       // 0 = uncapped
}

[System.Serializable]
public class SkillUpgradeInfo
{
    public string skillId;                 // "shuriken", "battlecry"
    public string displayName;             // "Shuriken", "Battle Cry"
    public SkillUpgradeTrack[] tracks;     // one or more upgrade tracks
}
```

`CharacterDefinition` gains:
```csharp
public SkillUpgradeInfo[] skills;   // this character's upgradable skills (empty for none)
```

- **Generic:** the upgrade screen iterates `CharacterDefinition.skills` → for each skill, each track → one row. Zero per-character UI code.
- AssCap's definition declares: `shuriken` (track `shuriken.damage`) and `battlecry` (tracks `battlecry.magnitude`, `battlecry.duration`).
- 67 declares its own; future characters just author their definition asset.

### 3. Reading/writing track levels

Add to `CharacterStats.cs`:
```csharp
public int GetSkillTrackLevel(string trackId)  // returns saved level or 1 if absent
public int GetSkillTrackCost(SkillUpgradeTrack track)  // track.baseCost * level (linear)
public bool TryUpgradeSkillTrack(SkillUpgradeTrack track)  // spend souls, ++level, save, fire events
```
Backed by the `skillTracks` list on save data (load into a `Dictionary<string,int>` at runtime for O(1); write back on save).

---

## Stat Upgrades — fill the Speed gap

`speedLevel` must actually affect movement (currently orphaned). Add to `CharacterStats.cs`:

```csharp
public int GetSpeedCost() => speedLevel * 5;

/// <summary>Passive move-speed multiplier. Lvl 1 = 1.0x, +10% per level.</summary>
public float GetMoveSpeedMultiplier() => 1f + ((speedLevel - 1) * 0.1f);

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

Apply in `TopDownPlayerController.HandleMovement()` where `currentSpeed` is built:
```csharp
float speedStatMult = characterStats != null ? characterStats.GetMoveSpeedMultiplier() : 1f;
currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f)
             * groundSpeedMultiplier * speedStatMult /* * battleCrySpeedMultiplier */;
```
(Battle Cry buff multiplier — from asscap.md — multiplies on top of this; the two are independent.)

`OnStatsUpgraded` already re-reads max health & damage in `UpdateStatsFromUpgrade()`. Move speed is read live each frame, so no extra wiring needed for it to take effect.

---

## Impact on asscap.md

asscap.md currently says **"Battle Cry magnitude uses `speedLevel`"**. That conflicts with this plan (speedLevel is now the universal passive move-speed stat). **Reconcile:**

- Battle Cry magnitude & duration are **skill tracks** (`battlecry.magnitude`, `battlecry.duration`), not `speedLevel`.
- `BattleCrySkillData` keeps `duration`/`cooldown` as **base** values; runtime reads track levels for the *actual* magnitude & duration:
  - magnitude = `2 + (battlecry.magnitude level - 1) * 0.25`
  - duration = `baseDuration + (battlecry.duration level - 1) * 1s`
- `CharacterStats.GetBattleCryMultiplier()` (proposed in asscap.md) becomes `GetSkillTrackLevel("battlecry.magnitude")`-driven; drop the `speedLevel` coupling and the `TryUpgradeSpeed`-as-battle-cry idea.
- `speedLevel` → passive move speed (this plan). asscap.md's "removes speedLevel from orphaned list" note still holds — just for a different reason.

> **Action:** when implementing, update asscap.md Parts 2b/2c and its Decisions #2–#4 to point at skill tracks. Flag both docs touch `CharacterStats` — implement in one pass.

---

## Entry Point

In-game pause overlay. Opened from an in-game menu (pause). Details:

- A `UI` action (e.g. `Menu`/`Start` button — same one the input-asset work in `characterSelection.md` adds) toggles a pause overlay Canvas in the gameplay scene.
- Overlay pauses gameplay (`Time.timeScale = 0`) while open. Upgrade screen is one panel of that overlay.
- **Which character?** In gameplay the active player(s) exist — the screen targets the local player's `CharacterStats`. For multiple local players, one upgrade panel per player (or the opener's). MVP: single active player's stats.
- **Gating toggle:** `bool allowUpgradeAnytime` on the menu controller. `true` (default) = open anytime paused. `false` = only when a "between fights" flag is set (future wave/level system). Left as a config bool so the decision can be deferred without a redesign.

> This overlay reuses the same `SaveManager`/`CharacterStats` already live on the player — no scene load, no data carrier needed.

---

## New Runtime Scripts

`Assets/Scripts/UI/`:

| Script | Role |
|---|---|
| `UpgradeScreenController.cs` | Builds/refreshes all rows for a target `CharacterStats` + `CharacterDefinition`. Handles Close. Subscribes to `OnSoulsChanged`/`OnStatsUpgraded` to live-refresh. |
| `UpgradeRowUI.cs` | One row: label, level, current-effect text, cost, Upgrade button. Bound to a stat kind OR a skill track via a small `UpgradeTarget` abstraction. |
| `PauseMenuController.cs` | Toggles pause overlay, `Time.timeScale`, holds `allowUpgradeAnytime`, opens `UpgradeScreenController` for the active player. |

`UpgradeTarget` abstraction (keeps rows generic over stats vs. skill tracks):
```csharp
public abstract class UpgradeTarget
{
    public abstract string Label { get; }
    public abstract int Level { get; }
    public abstract int Cost { get; }
    public abstract string EffectText { get; }
    public abstract bool TryUpgrade();
    public virtual bool IsMaxed => false;
}
// StatUpgradeTarget (Health/Strength/Speed) + SkillTrackUpgradeTarget wrap CharacterStats calls.
```

Row build order: 3 stat targets, then for each `CharacterDefinition.skills` → for each track → a `SkillTrackUpgradeTarget`.

---

## Editor Generator

`Assets/Editor/UpgradeScreenGenerator.cs` — `[MenuItem("LionQuest/Generate/Upgrade Screen")]`. Per project convention (code-generated, no manual editor steps). Builds:

1. `UpgradeScreenCanvas.prefab` (`Assets/Prefabs/UI/`): Canvas (Screen Space-Overlay) + CanvasScaler (1920×1080, match 0.5) + GraphicRaycaster; header (portrait Image, name TMP, souls TMP); a `VerticalLayoutGroup` "Stats" section; a `VerticalLayoutGroup` "Skills" section; a reusable `UpgradeRow.prefab`; Close button. Wire `UpgradeScreenController` refs in code (`SerializedObject`).
2. `UpgradeRow.prefab`: label TMP, level TMP, effect TMP, cost TMP, Button + TMP; `UpgradeRowUI` component.
3. Pause overlay wiring into the gameplay scene: the generator instantiates the `PauseMenuController` + overlay Canvas into the gameplay scene (`SampleScene`) and saves it via `EditorSceneManager`. Scene edits are now allowed for agents (see CLAUDE.md / `.cursorrules`), so this is done in code — no manual drag step.

Key APIs: `PrefabUtility.SaveAsPrefabAsset`, `ObjectFactory`, `DefaultControls`/TMP, `AssetDatabase`, `SerializedObject`, `EditorSceneManager`.

---

## New Assets

| Asset | Path | Created by |
|---|---|---|
| `UpgradeScreenCanvas.prefab` | `Assets/Prefabs/UI/` | Generator |
| `UpgradeRow.prefab` | `Assets/Prefabs/UI/` | Generator |
| `AssCap` skill metadata | on `AssCap_Definition.asset` (`skills[]`) | Generator / edit |
| `67` skill metadata | on `67_Definition.asset` | Generator / edit |

Depends on `CharacterDefinition` existing (`characterSelection.md`). If selection feature not yet implemented, this plan's generator must also create the `CharacterDefinition.cs` script (shared dependency) — implement selection first, or share the SO.

---

## Implementation Order

1. **Save format:** add `SkillUpgradeEntry` + `skillTracks`, remove `unlockedSkills` in `SaveDataTypes.cs`.
2. **CharacterStats:** speed (`GetSpeedCost`/`GetMoveSpeedMultiplier`/`TryUpgradeSpeed`) + skill-track methods (`GetSkillTrackLevel`/`GetSkillTrackCost`/`TryUpgradeSkillTrack`).
3. **TopDownPlayerController:** apply `GetMoveSpeedMultiplier()` in `currentSpeed`.
4. **CharacterDefinition:** add `SkillUpgradeInfo[] skills` (+ `SkillUpgradeTrack`, `SkillUpgradeInfo` types). (Create the SO if selection feature not done yet.)
5. **Reconcile asscap.md:** Battle Cry reads skill tracks, not `speedLevel`.
6. **UI runtime:** `UpgradeTarget` + subclasses, `UpgradeRowUI`, `UpgradeScreenController`, `PauseMenuController`.
7. **Generator:** `UpgradeScreenGenerator.cs`, build prefabs.
8. **Author skill metadata** on AssCap (+ 67) definitions.
9. Hook pause overlay into gameplay scene (generator writes it into the scene via `EditorSceneManager`).
10. Playtest each upgrade path; update `doc/project.md` Known Gaps.

---

## Acceptance Criteria

- [ ] Screen renders Health/Strength/Speed rows for **any** character with no per-character code.
- [ ] Skill rows are generated from `CharacterDefinition.skills`; AssCap shows Shuriken(Damage), Battle Cry(Magnitude), Battle Cry(Duration).
- [ ] Each Upgrade button spends the shown souls, increments the level, updates effect text + souls display live, and persists (survives restart).
- [ ] Speed upgrade increases passive move speed for every character (visible in play).
- [ ] Battle Cry magnitude & duration upgrade **independently** (buy one without the other).
- [ ] Buttons disable/grey when souls insufficient or track maxed.
- [ ] Opening the screen pauses gameplay; closing resumes.
- [ ] Old saves (with `unlockedSkills`) load without error.

---

## Out of Scope

- Souls economy balancing (costs are linear placeholders).
- Refund / respec.
- Skill *unlock* gating (all declared tracks upgradable from lvl 1; locking behind unlock is future).
- Wave/level "between fights" gating logic (only the `allowUpgradeAnytime` toggle is stubbed).
- Controller navigation polish beyond default `InputSystemUIInputModule` focus.
