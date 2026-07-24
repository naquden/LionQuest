# Character Selection Screen — Implementation Plan

## Goal

4-player local character selection screen. Each player joins by pressing a button on their controller. Each slot has a vertical carousel to pick a character. Locked characters visible but not selectable. Displays character stats and souls. After all joined players confirm → load gameplay.

---

## Environment

- Unity 6000.3.2f1, new Input System 1.17.0, uGUI 2.0.0, TMP (bundled)
- Existing save: **static** `SaveManager.LoadCharacter(id)` → `CharacterSaveData` (characterID, souls, strengthLevel, healthLevel, speedLevel). Always returns data (new default if none).
- Called from `StartScreen.unity` via `SceneManager.LoadScene("CharacterSelection")`

---

## UI Layer Choice — uGUI (Canvas), not UI Toolkit

Same rationale as `startScreen.md`: **uGUI (Canvas + RectTransform)** for prefab-first workflow, downloaded Asset Store UI packs, mature controller navigation via `InputSystemUIInputModule`, and project-wide consistency. Carousel items, slot panels, and stat displays are all uGUI prefabs.

---

## Workflow — Code-Generated, No Manual Editor Steps

Runtime scripts + one editor generator per feature. No dragging/clicking.

- **Runtime code** → `Assets/Scripts/CharacterSelection/`
- **Editor generator** → `Assets/Editor/CharacterSelectionGenerator.cs`, menu `LionQuest/Generate/Character Selection`
- Generator builds: `CharacterDefinition` assets, `CarouselItem.prefab`, slot panel prefabs, the `CharacterSelection.unity` scene (Canvas + 4 slots + EventSystem with `InputSystemUIInputModule`), and Build Settings registration. Assigns all component refs in code.
- **One-time manual:** import TMP Essentials if missing; supply portrait sprites (or generator uses placeholder).

---

## Key Decisions (locked)

- **Local input:** Unity **`PlayerInputManager`** (join-based local co-op). Each joined device → one `PlayerInput` instance. `TopDownPlayerController` refactored from shared-asset `.Enable()` to per-instance actions. See [Input Architecture](#input-architecture--playerinputmanager).
- **Start trigger:** explicit **Start prompt** — when ≥1 joined slot is Ready, show "Press Start"; a Ready player confirms to launch. Keep a start control (not auto-start).
- **Load by scene NAME**, not build index (index-independent, matches `StartScreenController`).

---

## Scenes

Load by **name** via `SceneManager.LoadScene("...")`. Build indices below are just registration order (generator ensures all three registered; exact index irrelevant to code).

| Scene | Path | Notes |
|---|---|---|
| `StartScreen` | `Assets/Scenes/StartScreen.unity` | Entry point (index 0) |
| `CharacterSelection` | `Assets/Scenes/CharacterSelection.unity` | This feature |
| `SampleScene` | `Assets/Scenes/SampleScene.unity` | Gameplay target |

> **Existing `characterID` values matter.** AssCap prefab (`AssasinoCappuchino.prefab`) uses `characterID: AssCap`. `CharacterDefinition.characterID` MUST match exactly (`"AssCap"`, not `"AssassinoCapuchino"`).

---

## New ScriptableObject: `CharacterDefinition`

`Assets/Scripts/CharacterSelection/CharacterDefinition.cs`

One asset per character. Defines display info and lock state. Source of truth for roster.

```csharp
[CreateAssetMenu(menuName = "LionQuest/CharacterDefinition")]
public class CharacterDefinition : ScriptableObject
{
    public string characterID;        // MUST match CharacterStats.characterID (e.g. "AssCap")
    public string displayName;        // e.g. "Assassino Capuchino"
    public Sprite portrait;           // shown in carousel
    public Sprite portraitLocked;     // greyed/locked version (optional; tint at runtime if null)
    public bool unlockedByDefault;    // true = always available (e.g. AssCap)
    public GameObject playerPrefab;   // instantiated in gameplay when this character is picked
}
```

Create one asset per character. The generator creates the AssCap definition:
- `Assets/Characters/AssassinoCapuchino/AssCap_Definition.asset`
  - `characterID = "AssCap"` (matches the prefab)
  - `displayName = "Assassino Capuchino"`
  - `unlockedByDefault = true`
  - `playerPrefab = AssasinoCappuchino.prefab` (`Assets/Characters/AssassinoCapuchino/AssasinoCappuchino.prefab`)
  - `portrait` = placeholder unless a sprite is supplied

The generator discovers definitions via `AssetDatabase.FindAssets("t:CharacterDefinition")` so new characters need no code change — just a new `.asset`.

Lock check at runtime: `unlockedByDefault` for MVP (see [Lock Check Logic](#lock-check-logic)).

---

## Input Architecture — PlayerInputManager

The existing `InputSystem_Actions.inputactions` has only a **`Player`** map (Move/Sprint/Attack/Skill1-3) and **no control schemes**. Both selection-screen joining and 4-player gameplay need additions. **The generator (or a manual note) must extend the input asset:**

### Required input-asset additions
1. **Add a `UI` action map** with at least:
   - `Join` — button, bound to gamepad South (`<Gamepad>/buttonSouth`) + keyboard (e.g. `<Keyboard>/enter`, `<Keyboard>/space`)
   - `Navigate` — Vector2 (D-Pad + left stick + arrow keys)
   - `Submit` — gamepad South / Enter
   - `Cancel` — gamepad East (`<Gamepad>/buttonEast`) / Escape
   - `Start` — gamepad Start (`<Gamepad>/start`) / Enter
2. **Add control schemes** `Gamepad` and `Keyboard&Mouse` so `PlayerInputManager` can device-pair. (Currently `controlSchemes: []` — join-based pairing needs them.)

> If editing `.inputactions` programmatically is impractical, the generator logs the exact maps/bindings to add and the agent creates a **second** asset `Assets/Input/JoinControls.inputactions` dedicated to the selection screen. Either path is acceptable — document which one is used.

### Join flow (selection screen)
- A single **`PlayerInputManager`** in the CharacterSelection scene, `joinBehavior = JoinPlayersWhenButtonIsPressed`, `joinAction` = `UI/Join`, `playerPrefab` = a lightweight **`SelectionPlayer.prefab`** (holds a `PlayerInput` + `CharacterSelectionInputHandler`, NO gameplay components).
- On join, `PlayerInputManager` spawns `SelectionPlayer` and pairs the pressing device. `CharacterSelectionInputHandler.OnPlayerJoined`-equivalent maps that `PlayerInput` to the next free slot (0–3) via `CharacterSelectionManager.AssignSlot(playerInput)`.
- `maxPlayerCount = 4` → manager blocks joins beyond 4.
- Per-slot input (`Navigate`/`Submit`/`Cancel`) is delivered by that slot's own `PlayerInput` (no device polling), so each controller drives only its slot.

### Gameplay hand-off (PlayerInputManager in gameplay)
- Gameplay refactor: **`TopDownPlayerController` must stop calling `inputActions.Enable()` on a shared asset.** Instead it reads actions from its own `PlayerInput` component (per-instance). This is a required code change — see [Gameplay Integration](#gameplay-integration--required-code-changes).
- Devices assigned during selection are carried in `CharacterSelectionData` and re-paired to gameplay `PlayerInput` instances by the spawner.

---

## New Scripts

### `CharacterSelectionManager.cs`
`Assets/Scripts/CharacterSelection/CharacterSelectionManager.cs`

Singleton. Manages global state: which devices joined, which characters confirmed. Listens for any-button presses from unjoined devices.

Responsibilities:
- Hold array `PlayerSlotState[4]`
- On "any button" from unjoined device → assign to next empty slot
- Track per-slot: Empty / Selecting / Ready
- When all joined players are Ready → countdown → `SceneManager.LoadScene("SampleScene")`
- Pass selected characterIDs to next scene via `DontDestroyOnLoad` data carrier or static

```csharp
public class CharacterSelectionManager : MonoBehaviour
{
    public static CharacterSelectionManager Instance { get; private set; }

    [SerializeField] private PlayerSlotUI[] slots; // 4 slots
    [SerializeField] private CharacterDefinition[] allCharacters;

    private PlayerSlotState[] states = new PlayerSlotState[4];

    // Called by input when unassigned device presses join button
    public void TryJoin(InputDevice device) { ... }
    public void TryLeave(int slotIndex) { ... }
    public void TryConfirm(int slotIndex) { ... }
    public void NavigateCarousel(int slotIndex, int direction) { ... } // +1 down, -1 up
    private bool CanStartGame() => states.Any(s => s.phase == SlotPhase.Ready);
    private void StartGame() { ... }
}

public enum SlotPhase { Empty, Selecting, Ready }

public class PlayerSlotState
{
    public SlotPhase phase;
    public InputDevice device;
    public int carouselIndex;
    public CharacterDefinition selectedCharacter;
}
```

### `PlayerSlotUI.cs`
`Assets/Scripts/CharacterSelection/PlayerSlotUI.cs`

Controls one slot panel's visuals. Updated by `CharacterSelectionManager`.

States:
- **Empty** → show "Press A to Join", dim background
- **Selecting** → show carousel, portrait, stats panel, "Press A to Confirm / B to Leave"
- **Ready** → show portrait + "READY", lock carousel

```csharp
public class PlayerSlotUI : MonoBehaviour
{
    [SerializeField] private GameObject emptyPanel;
    [SerializeField] private GameObject selectingPanel;
    [SerializeField] private GameObject readyOverlay;

    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text soulsText;
    [SerializeField] private TMP_Text strengthText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text speedText;

    [SerializeField] private Transform carouselContainer; // parent for carousel items
    [SerializeField] private GameObject carouselItemPrefab;

    public void SetEmpty() { ... }
    public void SetSelecting(CharacterDefinition[] roster, int selectedIndex, CharacterSaveData saveData) { ... }
    public void SetReady(CharacterDefinition character) { ... }
    public void UpdateCarousel(int selectedIndex) { ... }
}
```

### `CharacterCarouselItem.cs`
`Assets/Scripts/CharacterSelection/CharacterCarouselItem.cs`

One entry in carousel (portrait thumbnail + name + lock icon). Instantiated per character.

```csharp
public class CharacterCarouselItem : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private TMP_Text nameText;

    public void Setup(CharacterDefinition def, bool isUnlocked, bool isSelected) { ... }
}
```

### `CharacterSelectionInputHandler.cs`
`Assets/Scripts/CharacterSelection/CharacterSelectionInputHandler.cs`

Bridges new Input System → `CharacterSelectionManager`. Uses `PlayerInput` or direct `InputDevice` polling.

- Any unjoined device presses South (A/Cross) → `TryJoin(device)`
- Joined device presses South → `TryConfirm(slotIndex)`
- Joined device presses East (B/Circle) → `TryLeave(slotIndex)`
- Joined device Left Stick Y / D-Pad Up/Down → `NavigateCarousel(slotIndex, dir)`

### `CharacterSelectionData.cs`
`Assets/Scripts/CharacterSelection/CharacterSelectionData.cs`

DontDestroyOnLoad carrier — passes selections into gameplay scene.

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterSelectionData : MonoBehaviour
{
    public static CharacterSelectionData Instance { get; private set; }

    public struct Selection
    {
        public string characterID;
        public int[] deviceIds;   // InputDevice.deviceId values paired to this player
    }

    // Only joined players present; order = player index 0..N-1
    public readonly List<Selection> selections = new List<Selection>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Clear() => selections.Clear();
}
```

- Store **device IDs** (`InputDevice.deviceId`, an int), not `InputDevice` references — devices survive scene load but storing IDs is robust and serialization-friendly. Re-resolve in gameplay via `InputSystem.GetDeviceById(id)`.
- Created lazily by `CharacterSelectionManager` on `StartGame()` if not already present (`new GameObject("CharacterSelectionData").AddComponent<...>()`).
- Gameplay `PlayerSpawner` reads `Instance.selections` to spawn correct prefabs and re-pair devices.

---

## UI Layout

```
┌──────────┬──────────┬──────────┬──────────┐
│ PLAYER 1 │ PLAYER 2 │ PLAYER 3 │ PLAYER 4 │
├──────────┼──────────┼──────────┼──────────┤
│          │          │          │          │
│ carousel │  Press A │  Press A │  Press A │
│ portrait │  to Join │  to Join │  to Join │
│          │          │          │          │
├──────────┼──────────┼──────────┼──────────┤
│ souls: X │          │          │          │
│ str:   X │          │          │          │
│ hp:    X │          │          │          │
│ spd:   X │          │          │          │
└──────────┴──────────┴──────────┴──────────┘
       [Start Game] (appears when ≥1 ready)
```

**Carousel** — vertical list, center item = selected. Items above/below visible but smaller (CSS-like scale). Locked items show padlock overlay + greyed portrait.

---

## Carousel Scroll Behaviour

- Items rendered in `carouselContainer` (vertical layout group)
- Selected item: full size, full opacity
- ±1 items: 80% scale, 60% opacity
- ±2 items: 60% scale, 30% opacity
- Items beyond ±2: hidden
- Navigation wraps (bottom → top)
- Locked items: selectable for viewing, but Confirm blocked — show "LOCKED" flash

---

## Lock Check Logic

```csharp
private bool IsUnlocked(CharacterDefinition def)
{
    return def.unlockedByDefault;
    // Future: add explicit `bool isUnlocked` to CharacterSaveData and OR it in:
    //   || SaveManager.LoadCharacter(def.characterID).isUnlocked;
}
```

Note: `SaveManager.LoadCharacter()` always returns non-null (creates default), so presence-in-save is NOT a valid unlock signal. Use the explicit `unlockedByDefault` flag for MVP; add a real unlock field later.

For MVP: all characters with `unlockedByDefault = true` are selectable. Others show lock. Unlock progression = future feature.

---

## Data Flow

```
CharacterSelection scene loads
  → CharacterSelectionManager reads SaveManager for all CharacterDefinitions
  → 4 PlayerSlotUI set to Empty
  
Player presses A on controller
  → InputHandler detects device, calls TryJoin()
  → Manager assigns slot, sets state = Selecting
  → PlayerSlotUI.SetSelecting() — builds carousel, loads save data for current carousel char

Player navigates carousel
  → NavigateCarousel() → updates carouselIndex
  → PlayerSlotUI.UpdateCarousel() → rescales items, loads stats for new selected char

Player presses A to confirm
  → if locked: flash "LOCKED", do nothing
  → if unlocked: state = Ready, PlayerSlotUI.SetReady()
  → CharacterSelectionManager checks CanStartGame()

All joined players ready → StartGame()
  → Write selections to CharacterSelectionData (DontDestroyOnLoad)
  → SceneManager.LoadScene("SampleScene")
```

---

## Integration with Existing Systems

- **`SaveManager`** — call static `SaveManager.LoadCharacter(characterID)` to read souls/stats per character for display. No new save fields needed for MVP.
- **`CharacterStats`** on player prefabs still loads from save on `Start()` using `characterID` — unchanged.
- Gameplay scene: read `CharacterSelectionData.Instance.selectedCharacterIDs[i]` to set `CharacterStats.characterID` before spawning, or pass via PlayerInput's `PlayerIndex`.

---

## Gameplay Integration — Required Code Changes

This feature does **not** work end-to-end without these gameplay-side changes. An autonomous agent must implement them, not just the selection screen.

### 1. `PlayerSpawner.cs` (new) — `Assets/Scripts/CharacterSelection/PlayerSpawner.cs`
Lives in the gameplay scene (SampleScene). On `Start`:
1. If `CharacterSelectionData.Instance == null` (scene launched directly for testing) → spawn a single default player (`fallbackDefinition.playerPrefab`) so the scene is runnable standalone.
2. Else, for each `Selection`:
   - `Instantiate(definition.playerPrefab)` at spawn point `i`.
   - Set `CharacterStats.characterID` **before** the object activates (instantiate inactive, set field, then activate) so `LoadStats()` in `Start()` loads the right save.
   - Ensure a `PlayerInput` on the prefab; pair the stored devices via `PlayerInput.Instantiate(prefab, pairWithDevices: resolvedDevices)` or `InputUser.PerformPairingWithDevice`.
   - Assign `viewportCamera` (currently each `TopDownPlayerController` falls back to `Camera.main`).
- Lookup `characterID → CharacterDefinition` via the same `FindAssets("t:CharacterDefinition")` list (or a serialized array on the spawner).
- Spawn points: serialized `Transform[] spawnPoints` (generator can drop 4 empty GameObjects into the scene).

### 2. `TopDownPlayerController` refactor — shared asset → per-instance `PlayerInput`
**Current blocker:** `Awake()` calls `inputActions.Enable()` on a single shared `InputActionAsset` and `FindAction("Player/Move")`. With 4 players this makes every player read the **same** device. Must change to per-instance input:
- Add `[RequireComponent(typeof(PlayerInput))]` (or fetch in `Awake`).
- Read actions from the instance: `var pi = GetComponent<PlayerInput>(); moveAction = pi.actions["Move"];` etc. Remove the global `inputActions.Enable()/Disable()`.
- `PlayerInput` (with per-player device pairing from `PlayerInputManager`) then isolates each controller to its own player.
- Keep a **fallback**: if no `PlayerInput` is present (e.g. legacy scene), fall back to the old shared-asset path so existing SampleScene setups still run. Guard with a null check.
- The AssCap prefab (`AssasinoCappuchino.prefab`) currently has **no** `PlayerInput` component — the generator/agent must add one and assign the actions asset + default map `Player`.

### 3. `GameSaveController` must exist before players spawn
`CharacterStats.OnEnable()` does `GameSaveController.Instance.RegisterPlayer(this)` with **no null guard → NullReferenceException** if absent. `GameSaveController` lives on `GameController.prefab` (marked `DontDestroyOnLoad`) which is **not currently in SampleScene**.
- The `PlayerSpawner` (or a scene bootstrap) must **ensure `GameSaveController.Instance` exists before spawning players** — e.g. `if (GameSaveController.Instance == null) Instantiate(gameControllerPrefab);`.
- Because it is `DontDestroyOnLoad`, one instance persists across StartScreen → CharacterSelection → gameplay; safest to instantiate it once at StartScreen or guard-instantiate in the spawner.

---

## New Assets Needed

| Asset | Path | Created by |
|---|---|---|
| `CharacterDefinition` SO per character | `Assets/Characters/<Name>/<Name>_Definition.asset` | Generator (AssCap) |
| `CharacterSelection.unity` scene | `Assets/Scenes/CharacterSelection.unity` | Generator |
| `SelectionPlayer.prefab` (PlayerInput + handler, no gameplay comps) | `Assets/Prefabs/UI/SelectionPlayer.prefab` | Generator |
| `CarouselItem.prefab` | `Assets/Prefabs/UI/CarouselItem.prefab` | Generator |
| `PlayerSlot.prefab` (one of 4 slot panels) | `Assets/Prefabs/UI/PlayerSlot.prefab` | Generator |
| UI action map / `JoinControls.inputactions` | `Assets/InputSystem_Actions.inputactions` (add `UI` map) or new asset | Generator / manual |
| `PlayerInput` added to `AssasinoCappuchino.prefab` | existing prefab | Generator |
| Spawn points + `PlayerSpawner` + `GameController` in SampleScene | `Assets/Scenes/SampleScene.unity` | Generator |
| Lock icon sprite | `Assets/UI/LockIcon.png` | Manual / placeholder |
| Portrait per character | `Assets/Characters/<Name>/Portrait.png` | Manual / placeholder |

---

## Implementation Order

**Runtime scripts**
1. `CharacterDefinition.cs` SO (with `playerPrefab`).
2. `CharacterSelectionData.cs` (DontDestroyOnLoad carrier, device IDs).
3. `CharacterSelectionManager.cs` + `SlotPhase` enum + `PlayerSlotState`.
4. `PlayerSlotUI.cs`, `CharacterCarouselItem.cs`.
5. `CharacterSelectionInputHandler.cs` (per-`PlayerInput`, reads `UI` map).
6. `PlayerSpawner.cs` (gameplay scene).
7. Refactor `TopDownPlayerController` → per-instance `PlayerInput` with shared-asset fallback.

**Editor generator** (`CharacterSelectionGenerator.cs`, menu `LionQuest/Generate/Character Selection`)
8. Extend input asset: add `UI` map (Join/Navigate/Submit/Cancel/Start) + `Gamepad`/`Keyboard&Mouse` schemes (or emit `JoinControls.inputactions`).
9. Build prefabs: `SelectionPlayer`, `PlayerSlot` (×4 in scene), `CarouselItem`. Assign all refs in code.
10. Create `AssCap_Definition.asset` (characterID `"AssCap"`, playerPrefab = AssCap prefab, unlockedByDefault).
11. Build `CharacterSelection.unity`: Canvas + 4 slots + `PlayerInputManager` (join=`UI/Join`, playerPrefab=`SelectionPlayer`, max 4) + EventSystem (`InputSystemUIInputModule`) + `CharacterSelectionManager`.
12. Add `PlayerInput` to `AssasinoCappuchino.prefab` (actions asset, default map `Player`).
13. Prep SampleScene: add `PlayerSpawner`, 4 spawn points, ensure `GameController` present.
14. Wire nav: `StartScreenController.playSceneName = "CharacterSelection"`; register all scenes in Build Settings.

**Manual (you)**
15. Import TMP Essentials if prompted. Run the generator menu item. Supply real portraits/lock icon later (placeholders until then).

---

## Acceptance Criteria (definition of done)

- Launch `StartScreen` → Play → `CharacterSelection` loads.
- Press gamepad South on controller 1 → slot 0 becomes Selecting; a 2nd controller → slot 1. Each controller drives only its own slot.
- Carousel navigates with that slot's stick/D-pad; locked characters show a lock and block Confirm.
- Stats (souls/str/hp/spd) update per selected character via `SaveManager.LoadCharacter(id)`.
- Confirm → slot Ready. When ≥1 Ready, "Press Start" prompt appears; Start → gameplay loads.
- In gameplay, one player per joined slot spawns with the chosen prefab + correct `characterID`, each controlled by its own paired device, no NullRef from `GameSaveController`.
- Launching SampleScene directly (no selection data) still spawns one default player.

---

## Out of Scope (this plan)

- Character unlock progression / unlock UI
- Animated portrait / 3D character preview
- Keyboard support (controller-first MVP)
- Online/network multiplayer
- "Open to Public" / invite system (Castle Crashers feature)
- Character stats upgrade screen

> Note: keyboard bindings are included in the `UI` map for editor testing, but the design is controller-first (multiple keyboard players are not separated).
