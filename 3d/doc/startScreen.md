# Start Screen — Implementation Plan

## Goal

Add a Start Screen as the game's entry point. Player sees two buttons: **Play** and **Exit**.
- **Exit** → quit the game
- **Play** → loads Character Selection screen (separate feature, see `characterSelection.md`)

---

## Environment

- Unity **6000.3.2f1** (Unity 6)
- uGUI **2.0.0**, new **Input System 1.17.0**
- Use **TextMeshPro** for all text (import TMP Essentials on first prompt)

---

## Scenes

| Scene | Path | Build index | Notes |
|---|---|---|---|
| `StartScreen` | `Assets/Scenes/StartScreen.unity` | 0 | New scene, entry point |
| `SampleScene` | `Assets/Scenes/SampleScene.unity` | 1 | Existing gameplay |

Character Selection gets its own scene later. For now **Play** loads `SampleScene` as placeholder.

---

## New Scripts

### `StartScreenController.cs`
`Assets/Scripts/UI/StartScreenController.cs`

Code-based button wiring (refactor-safe — renaming a method breaks compile instead of silently breaking Inspector wiring). Editor-guarded quit so Exit is testable in-editor.

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartScreenController : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button exitButton;

    private void Awake()
    {
        playButton.onClick.AddListener(OnPlayPressed);
        exitButton.onClick.AddListener(OnExitPressed);
    }

    private void OnDestroy()
    {
        playButton.onClick.RemoveListener(OnPlayPressed);
        exitButton.onClick.RemoveListener(OnExitPressed);
    }

    private void OnPlayPressed()
    {
        // TODO: swap to "CharacterSelection" scene when that feature lands
        SceneManager.LoadScene("SampleScene");
    }

    private void OnExitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
```

---

## Scene Setup (Unity Editor steps)

1. Create scene `Assets/Scenes/StartScreen.unity`.
2. **Canvas** (right-click Hierarchy → UI → Canvas):
   - Render Mode: `Screen Space - Overlay`
   - **CanvasScaler** → UI Scale Mode: `Scale With Screen Size`, Reference Resolution `1920×1080`, Match `0.5`.
3. **EventSystem** — auto-added with Canvas. Verify it uses **`InputSystemUIInputModule`** (new Input System), not legacy `StandaloneInputModule`. If wrong, click "Replace with InputSystemUIInputModule".
4. Background — full-screen `Image` anchored stretch-all, or a Camera with solid color / skybox.
5. **Panel** (menu container), anchored center, ~400×300:
   - Add **`VerticalLayoutGroup`** (spacing ~20, child alignment Middle Center, control child size width/height on).
   - Add **`ContentSizeFitter`** (Vertical Fit: Preferred Size) so panel auto-sizes to buttons.
6. Inside panel, add two **`Button - TextMeshPro`**:
   - Label **Play**
   - Label **Exit**
7. Add empty GameObject `StartScreenController`, attach `StartScreenController.cs`.
8. Drag Play button → `playButton` field, Exit button → `exitButton` field in Inspector.
9. **Build Settings** (File → Build Settings) — add scenes in order:
   - Index 0: `StartScreen`
   - Index 1: `SampleScene`

> Buttons wired in code (step 8 assigns refs). No manual OnClick entries needed.

---

## UI Layout

```
┌─────────────────────────────────┐
│                                 │
│         LionQuest               │
│                                 │
│         ┌─────────┐            │
│         │  Play   │            │
│         └─────────┘            │
│         ┌─────────┐            │
│         │  Exit   │            │
│         └─────────┘            │
│                                 │
└─────────────────────────────────┘
```

Buttons stacked by `VerticalLayoutGroup` — spacing/sizing automatic, survives resolution changes.

---

## Best-Practice Checklist

- [x] Dedicated scene, build index 0
- [x] `CanvasScaler` = Scale With Screen Size @ 1920×1080
- [x] TextMeshPro buttons (not legacy)
- [x] `VerticalLayoutGroup` + `ContentSizeFitter` for auto-layout
- [x] Everything anchored (no absolute pixel positions)
- [x] EventSystem uses `InputSystemUIInputModule`
- [x] Code-based button wiring (refactor-safe)
- [x] Editor-guarded `Application.Quit()`

---

## Implementation Order

1. Create `StartScreenController.cs`
2. Create `StartScreen.unity` scene — Canvas + CanvasScaler + EventSystem check
3. Add Panel with VerticalLayoutGroup + ContentSizeFitter
4. Add TMP Play/Exit buttons
5. Attach controller, assign button refs
6. Add scenes to Build Settings
7. Test: Play opens gameplay, Exit stops editor play (or quits in build)

---

## Out of Scope (this plan)

- Character selection screen → `characterSelection.md`
- Animated background / main menu music
- Settings / Options button
- Async scene load + fade transition
- Save slot selection
