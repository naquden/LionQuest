using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Shared base for PlayMode verification tests. Provides synthetic gamepad input
/// (InputSystem.AddDevice/QueueStateEvent — public API, not the broken
/// Unity.InputSystem.TestFramework package) and PNG screenshot capture that an
/// agent reads back with the Read tool.
/// </summary>
public abstract class HarnessFixture
{
    const float NavigateRepeatDelay = 0.35f; // must exceed CharacterSelectionInputHandler.NavigateRepeatDelay

    readonly List<Gamepad> pads = new List<Gamepad>();
    readonly List<Keyboard> keyboards = new List<Keyboard>();

    InputSettings.BackgroundBehavior savedBackgroundBehavior;
    InputSettings.EditorInputBehaviorInPlayMode savedEditorInputBehavior;

    [SetUp]
    public virtual void SetUp()
    {
        // Redirect the save file into Temp/ BEFORE any Delete/Save call. These tests seed and wipe
        // save state constantly, and until this override existed they did it against the real
        // Application.persistentDataPath/gamesave.json — i.e. running the suite deleted the
        // player's own progress, which then showed up in-game as souls 0 and every skill slot
        // locked. Tests must never touch the live save.
        SaveManager.SetSaveFilePathOverride(
            Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "Temp"), "harness-gamesave.json"));
        SaveManager.DeleteSave();

        // Headless batchmode has no focused Game View, so the Input System keeps every device
        // disabled (Keyboard.enabled == false) and QueueStateEvent is swallowed — actions never
        // fire. Gamepads happen to survive this; keyboards do not. Force focus-independent input
        // for the duration of the test and restore afterwards.
        savedBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        savedEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    }

    /// <summary>
    /// Call at the top of every [UnityTest] body (LogAssert's scope is per-command — setting this
    /// from [SetUp] does not carry into the test method's own scope). UTF fails a test on any
    /// unhandled Debug.LogError/LogException by default. The character preview stage
    /// (CharacterPreviewStage.Show) instantiates the full playerPrefab and strips gameplay
    /// components via Destroy(), which only takes effect at end of frame — so the clone's
    /// Awake()/OnEnable() run once before removal and log real (pre-existing, harmless in this
    /// context) errors/exceptions. Assertions on scene/save state are the source of truth here,
    /// not this log noise.
    /// </summary>
    protected void IgnoreFailingLogs() => LogAssert.ignoreFailingMessages = true;

    [TearDown]
    public virtual void TearDown()
    {
        // The pause menu and the game-over screen both set timeScale to 0, and a test that ends
        // while one is open would leave the next test frozen — every WaitForSeconds in it would
        // hang until the suite times out.
        Time.timeScale = 1f;

        foreach (var pad in pads)
        {
            if (pad != null) InputSystem.RemoveDevice(pad);
        }
        pads.Clear();
        foreach (var keyboard in keyboards)
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        }
        keyboards.Clear();
        InputSystem.settings.backgroundBehavior = savedBackgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInputBehavior;
        DestroyPersistentSingletons();

        // Still pointed at Temp/harness-gamesave.json here — deliberately delete the throwaway
        // file, then release the override so anything running after the suite (e.g. the editor
        // entering Play mode) is back on the real save.
        SaveManager.DeleteSave();
        SaveManager.SetSaveFilePathOverride(null);
    }

    /// <summary>
    /// Destroys the DontDestroyOnLoad singletons, which a scene load does NOT clean up.
    ///
    /// Without this, tests leak into each other and the leak depends on execution order — so the
    /// suite passes one test at a time and fails when run together, or vice versa. Concretely:
    /// CharacterSelectFlowTests leaves a CharacterSelectionData holding one confirmed selection
    /// alive, and PlayerSpawner.Start prefers selection data over its fallback path, so the
    /// two-player coop gym afterwards spawned exactly ONE player and every coop assertion failed
    /// with "Expected: 2 But was: 1" — while passing under -testFilter CoopCamera.
    ///
    /// DestroyImmediate, not Destroy: Destroy defers to end of frame, and [TearDown] runs after the
    /// last frame of the test, so the object would still be alive when the next test's Start runs.
    /// </summary>
    static void DestroyPersistentSingletons()
    {
        if (CharacterSelectionData.Instance != null)
        {
            Object.DestroyImmediate(CharacterSelectionData.Instance.gameObject);
        }
        if (GameSaveController.Instance != null)
        {
            Object.DestroyImmediate(GameSaveController.Instance.gameObject);
        }
    }

    protected Gamepad AddPad()
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        pads.Add(pad);
        return pad;
    }

    /// <summary>
    /// Press+release a gamepad button. Deliberately does NOT call InputSystem.Update() — a test
    /// coroutine runs *after* every MonoBehaviour.Update in the frame, so a manually-pumped event
    /// gets stamped with the current frame and `WasPressedThisFrame()` is already false by the time
    /// the next frame's Update runs: the press is invisible to game code. Queuing and yielding
    /// instead lets Unity's own ProcessEventsInDynamicUpdate deliver it at the top of the next
    /// frame, before Update — which is what a real button press looks like.
    /// </summary>
    protected IEnumerator Press(Gamepad pad, GamepadButton button)
    {
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button, true));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button, false));
        yield return null;
    }

    protected Keyboard AddKeyboard()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        keyboards.Add(keyboard);
        return keyboard;
    }

    /// <summary>Press+release a key on a synthetic Keyboard (same frame rules as Press(Gamepad, ...)).</summary>
    protected IEnumerator PressKey(Keyboard keyboard, Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }

    /// <summary>
    /// Holds the left stick for the full navigate repeat delay. This deliberately spans the
    /// repeat gate, so a menu driven by ReadValue + a repeat timer steps TWICE — use it to model
    /// a player leaning on the stick, not to move one entry. For a single step use PulseStick.
    /// </summary>
    protected IEnumerator Stick(Gamepad pad, Vector2 direction)
    {
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = direction });
        InputSystem.Update();
        yield return null;
        yield return new WaitForSeconds(NavigateRepeatDelay);
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.zero });
        InputSystem.Update();
        yield return null;
    }

    /// <summary>
    /// One-frame stick deflection, then a wait for the repeat gate to expire: moves a menu cursor
    /// exactly one entry and leaves the next press unswallowed. Menu navigation reads the stick's
    /// current value behind a repeat timer rather than an edge, so a direction held across that
    /// timer double-fires — which silently wraps a short carousel back to where it started.
    /// </summary>
    protected IEnumerator PulseStick(Gamepad pad, Vector2 direction)
    {
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = direction });
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.zero });
        InputSystem.Update();
        yield return null;
        yield return new WaitForSeconds(NavigateRepeatDelay + 0.05f);
    }

    /// <summary>
    /// Holds the left stick at a fixed direction for a real duration, then releases and lets physics
    /// settle. Unlike Stick(), this re-queues the state every frame: a single QueueStateEvent is a
    /// state *change*, and gameplay movement integrates a held value over many frames, so a one-shot
    /// event followed by a long yield does not model a player holding a direction.
    /// </summary>
    protected IEnumerator HoldStick(Gamepad pad, Vector2 direction, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = direction });
            yield return null;
            elapsed += Time.deltaTime;
        }
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.zero });
        yield return null;
    }

    protected IEnumerator LoadScene(string sceneName)
    {
        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        yield return null;
    }

    /// <summary>
    /// Renders a dedicated capture camera to a PNG at Logs/test-&lt;name&gt;.png for the agent to
    /// Read back. Spawns its own camera (matches AgentBatch.CaptureCharacterSelection) rather than
    /// reusing the scene's own camera — e.g. CharacterSelection.unity's "Main Camera" has
    /// cullingMask=0 (it only exists for its background clear color; per-slot preview cameras
    /// render the actual 3D content), so trusting a scene camera's config is not safe in general.
    /// Uses a manual RenderTexture capture rather than ScreenCapture.CaptureScreenshotAsTexture —
    /// the latter needs a real presented backbuffer via WaitForEndOfFrame, which batchmode
    /// PlayMode tests never produce, and the coroutine hangs forever.
    /// ScreenSpaceOverlay canvases (CharacterSelection, StartScreen, PauseMenu, ...) don't render
    /// through any Camera at all — temporarily forced to ScreenSpaceCamera against the capture
    /// camera, same trick as AgentBatch.CaptureCharacterSelection.
    /// </summary>
    protected void Shot(string name)
    {
        var captureGo = new GameObject("HarnessCaptureCamera", typeof(Camera));
        Camera camera = captureGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = ~0;
        camera.orthographic = false;
        camera.fieldOfView = 60f;

        Camera reference = Camera.main;
        if (reference != null)
        {
            camera.transform.SetPositionAndRotation(reference.transform.position, reference.transform.rotation);
        }

        List<Canvas> overlayCanvases = FindOverlayCanvases();
        var savedModes = new RenderMode[overlayCanvases.Count];
        var savedCameras = new Camera[overlayCanvases.Count];
        var savedPlaneDistances = new float[overlayCanvases.Count];
        for (int i = 0; i < overlayCanvases.Count; i++)
        {
            savedModes[i] = overlayCanvases[i].renderMode;
            savedCameras[i] = overlayCanvases[i].worldCamera;
            savedPlaneDistances[i] = overlayCanvases[i].planeDistance;
            overlayCanvases[i].renderMode = RenderMode.ScreenSpaceCamera;
            overlayCanvases[i].worldCamera = camera;
            // A ScreenSpaceCamera canvas is real geometry at planeDistance (default 100), so in a
            // gameplay scene the terrain sits in front of it and the whole UI is occluded — the
            // assertions pass while the PNG shows nothing. Pull it inside the near geometry.
            overlayCanvases[i].planeDistance = 0.5f;
        }
        camera.nearClipPlane = 0.05f;

        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = rt;
        camera.Render();

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = previousActive;

        for (int i = 0; i < overlayCanvases.Count; i++)
        {
            overlayCanvases[i].renderMode = savedModes[i];
            overlayCanvases[i].worldCamera = savedCameras[i];
            overlayCanvases[i].planeDistance = savedPlaneDistances[i];
        }

        // Verification screenshots live in <project>/screenshots/, never Logs/ or Temp/ —
        // Unity wipes Temp/ when the editor quits, which would delete the proof before an
        // agent could Read it back (and ./temp is the same dir as ./Temp on a
        // case-insensitive filesystem).
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"test-{name}.png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.Destroy(tex);
        camera.targetTexture = null;
        rt.Release();
        Object.Destroy(rt);
        Object.Destroy(captureGo);

        Debug.Log($"CAPTURE|{path}");
    }

    static List<Canvas> FindOverlayCanvases()
    {
        var result = new List<Canvas>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            foreach (GameObject root in SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) result.Add(canvas);
                }
            }
        }
        return result;
    }
}
