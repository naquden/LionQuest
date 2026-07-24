# Start Screen — Implementation Plan

## Goal

Add a Start Screen as the game's entry point. Player sees two buttons: **Play** and **Exit**.
- **Exit** → quit the game
- **Play** → loads Character Selection screen (separate feature, see `characterSelection.md`)

---

## Environment

- Unity **6000.3.2f1** (Unity 6)
- uGUI **2.0.0**, new **Input System 1.17.0**, **TextMeshPro** (bundled with uGUI 2.0)

---

## UI Layer Choice — uGUI (Canvas), not UI Toolkit

Unity 6 ships two UI systems. This project uses **uGUI (Canvas + RectTransform)** for all game UI:

| Reason | Detail |
|---|---|
| **Prefab-first workflow** | uGUI UI lives in prefabs → matches our generator + downloaded-asset flow. UI Toolkit uses UXML/USS documents, no prefab composition. |
| **Downloaded assets** | Nearly all Asset Store UI packs ship uGUI prefabs. |
| **Gamepad/controller** | uGUI + `InputSystemUIInputModule` has mature controller navigation. UI Toolkit runtime input support is still maturing. |
| **Consistency** | Character Selection and in-game HUD are all uGUI → one system across the project. |

**Modern uGUI best practices applied here:** `CanvasScaler` (Scale With Screen Size), TMP text, layout groups for auto-layout, everything anchored, code-based button wiring, prefab-based composition.

---

## Workflow — Code-Generated, No Manual Editor Steps

Per project convention: **runtime scripts + one editor generator script per feature.** No dragging/clicking in the editor. The generator builds the prefab, scene, and Build Settings registration; you run it once from a menu item.

- **Runtime code** → `Assets/Scripts/UI/`
- **Editor generator** → `Assets/Editor/StartScreenGenerator.cs`, menu `LionQuest/Generate/Start Screen`
- Generated assets are normal editable prefabs/scenes afterward (tweak visually, swap in downloaded prefabs).

**One-time manual step:** if TMP Essentials not yet imported, `Window → TextMeshPro → Import TMP Essential Resources`. Generator warns if missing.

---

## Scenes

| Scene | Path | Build index | Notes |
|---|---|---|---|
| `StartScreen` | `Assets/Scenes/StartScreen.unity` | 0 | New scene, entry point |
| `SampleScene` | `Assets/Scenes/SampleScene.unity` | 1 | Existing gameplay (Play target placeholder) |

Character Selection gets its own scene later. For now **Play** loads `SampleScene` as placeholder (`playSceneName` field).

---

## New Runtime Script

### `StartScreenController.cs`
`Assets/Scripts/UI/StartScreenController.cs`

Code-based button wiring (refactor-safe — renaming a method breaks compile instead of silently breaking Inspector wiring). Editor-guarded quit so Exit is testable in-editor. Button refs assigned by the generator, not by hand.

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartScreenController : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private string playSceneName = "SampleScene"; // TODO: "CharacterSelection" later

    private void Awake()
    {
        if (playButton != null) playButton.onClick.AddListener(OnPlayPressed);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitPressed);
    }

    private void OnDestroy()
    {
        if (playButton != null) playButton.onClick.RemoveListener(OnPlayPressed);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExitPressed);
    }

    private void OnPlayPressed() => SceneManager.LoadScene(playSceneName);

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

## New Editor Script — Generator

### `StartScreenGenerator.cs`
`Assets/Editor/StartScreenGenerator.cs` — `[MenuItem("LionQuest/Generate/Start Screen")]`

Builds everything in code so no manual editor work is needed. Steps the generator performs:

1. **Warn if TMP Essentials missing** (check for `TMP Settings` asset).
2. **Build the Canvas prefab** `Assets/Prefabs/UI/StartScreenCanvas.prefab`:
   - `Canvas` (Screen Space - Overlay) + `CanvasScaler` (Scale With Screen Size, ref `1920×1080`, match `0.5`) + `GraphicRaycaster`.
   - Background `Image` anchored stretch-all.
   - Title `TMP_Text` "LionQuest".
   - Menu panel with `VerticalLayoutGroup` (spacing 20, middle-center, control child size) + `ContentSizeFitter` (vertical preferred).
   - Two `Button` + child `TMP_Text` ("Play", "Exit"), created via `DefaultControls` / `ObjectFactory`.
   - `StartScreenController` component; assign `playButton`/`exitButton` refs **in code** (`SerializedObject` or direct field set before saving prefab).
3. **Create `EventSystem`** with `InputSystemUIInputModule` (new Input System) — added to scene, not prefab.
4. **Create the scene** `Assets/Scenes/StartScreen.unity`: new empty scene, instantiate the Canvas prefab + EventSystem, save via `EditorSceneManager`.
5. **Register Build Settings**: ensure `StartScreen` at index 0 and `SampleScene` present, via `EditorBuildSettings.scenes`.
6. `AssetDatabase.SaveAssets()` + `Refresh()`.

Key APIs: `PrefabUtility.SaveAsPrefabAsset`, `ObjectFactory.CreateGameObject`, `EditorSceneManager.NewScene/SaveScene`, `EditorBuildSettings.scenes`, `TMP_DefaultControls` / `DefaultControls` for buttons.

> Re-runnable: regenerates prefab/scene in place. Safe to run after edits (or guard with "asset exists?" checks).

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

## New Assets Needed

| Asset | Path | Created by |
|---|---|---|
| `StartScreenCanvas.prefab` | `Assets/Prefabs/UI/StartScreenCanvas.prefab` | Generator |
| `StartScreen.unity` scene | `Assets/Scenes/StartScreen.unity` | Generator |
| Background sprite (optional) | `Assets/UI/StartBackground.png` | Manual / downloaded |

---

## Best-Practice Checklist

- [x] uGUI (Canvas) — correct layer for prefab + controller flow
- [x] Dedicated scene, build index 0
- [x] `CanvasScaler` = Scale With Screen Size @ 1920×1080
- [x] TextMeshPro buttons (not legacy)
- [x] `VerticalLayoutGroup` + `ContentSizeFitter` for auto-layout
- [x] Everything anchored (no absolute pixel positions)
- [x] EventSystem uses `InputSystemUIInputModule`
- [x] Code-based button wiring (refactor-safe)
- [x] Editor-guarded `Application.Quit()`
- [x] Prefab-based composition (generator-built, editable after)

---

## Implementation Order

1. Write `StartScreenController.cs` (runtime).
2. Write `StartScreenGenerator.cs` (editor).
3. Run menu `LionQuest/Generate/Start Screen`.
4. (If prompted) import TMP Essentials, re-run.
5. Test: Play opens gameplay, Exit stops editor play (or quits in build).

---

## Out of Scope (this plan)

- Character selection screen → `characterSelection.md`
- Animated background / main menu music
- Settings / Options button
- Async scene load + fade transition
- Save slot selection
