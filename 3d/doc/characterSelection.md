# Character Selection Screen — Implementation Plan

## Goal

4-player local character selection screen. Each player joins by pressing a button on their controller. Each slot has a vertical carousel to pick a character. Locked characters visible but not selectable. Displays character stats and souls. After all joined players confirm → load gameplay.

---

## Environment

- Unity 6000.3.2f1, new Input System 1.17.0, uGUI 2.0.0, TMP
- Existing save: `SaveManager` + `CharacterSaveData` (characterID, souls, strengthLevel, healthLevel, speedLevel)
- Called from `StartScreen.unity` via `SceneManager.LoadScene("CharacterSelection")`

---

## Scenes

| Scene | Path | Build index |
|---|---|---|
| `CharacterSelection` | `Assets/Scenes/CharacterSelection.unity` | 1 |
| `SampleScene` | `Assets/Scenes/SampleScene.unity` | 2 |
| `StartScreen` | `Assets/Scenes/StartScreen.unity` | 0 |

---

## New ScriptableObject: `CharacterDefinition`

`Assets/Scripts/CharacterSelection/CharacterDefinition.cs`

One asset per character. Defines display info and lock state. Source of truth for roster.

```csharp
[CreateAssetMenu(menuName = "LionQuest/CharacterDefinition")]
public class CharacterDefinition : ScriptableObject
{
    public string characterID;        // matches CharacterSaveData.characterID
    public string displayName;
    public Sprite portrait;           // shown in carousel
    public Sprite portraitLocked;     // greyed/locked version
    public bool unlockedByDefault;    // true = always available (e.g. AssCap)
}
```

Create one asset per character, e.g.:
- `Assets/Characters/AssassinoCapuchino/AssCap_Definition.asset` — unlockedByDefault = true

Lock check at runtime: `unlockedByDefault || saveData.unlockedSkills.Contains("unlocked")` — or add explicit `bool isUnlocked` to `CharacterSaveData` later.

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
public class CharacterSelectionData : MonoBehaviour
{
    public static CharacterSelectionData Instance { get; private set; }

    // Index = player slot (0–3), null = not joined
    public string[] selectedCharacterIDs = new string[4];
    public InputDevice[] assignedDevices = new InputDevice[4];

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```

Gameplay scene reads `CharacterSelectionData.Instance` to spawn correct player prefabs with correct characterIDs.

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
    if (def.unlockedByDefault) return true;
    CharacterSaveData save = SaveManager.Instance.GetCharacterSave(def.characterID);
    return save != null; // has been played = unlocked, OR add explicit flag later
}
```

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

- **`SaveManager`** — call `SaveManager.Instance.GetCharacterSave(characterID)` to read souls/stats per character for display. No new save fields needed for MVP.
- **`CharacterStats`** on player prefabs still loads from save on `Start()` using `characterID` — unchanged.
- Gameplay scene: read `CharacterSelectionData.Instance.selectedCharacterIDs[i]` to set `CharacterStats.characterID` before spawning, or pass via PlayerInput's `PlayerIndex`.

---

## New Assets Needed

| Asset | Path |
|---|---|
| `CharacterDefinition` SO per character | `Assets/Characters/<Name>/<Name>_Definition.asset` |
| `CharacterSelection.unity` scene | `Assets/Scenes/CharacterSelection.unity` |
| `CarouselItem.prefab` | `Assets/Prefabs/UI/CarouselItem.prefab` |
| Lock icon sprite | `Assets/UI/LockIcon.png` |
| Portrait per character | `Assets/Characters/<Name>/Portrait.png` |

---

## Implementation Order

1. `CharacterDefinition.cs` ScriptableObject + create asset for AssCap
2. `CharacterSelectionData.cs` (DontDestroyOnLoad carrier)
3. `CharacterSelectionManager.cs` + `PlayerSlotState` enum/class
4. `PlayerSlotUI.cs` prefab + scene layout (4 slot panels, Canvas)
5. `CharacterCarouselItem.cs` + prefab
6. `CharacterSelectionInputHandler.cs` — wire Input System
7. Connect `StartScreenController.OnPlayPressed()` → load `CharacterSelection`
8. Connect `CharacterSelectionManager.StartGame()` → write `CharacterSelectionData` → load `SampleScene`
9. Update gameplay scene to read `CharacterSelectionData`

---

## Out of Scope (this plan)

- Character unlock progression / unlock UI
- Animated portrait / 3D character preview
- Keyboard support (controller-first MVP)
- Online/network multiplayer
- "Open to Public" / invite system (Castle Crashers feature)
- Character stats upgrade screen
