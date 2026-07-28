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

    [SetUp]
    public virtual void SetUp()
    {
        SaveManager.DeleteSave();
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
        foreach (var pad in pads)
        {
            if (pad != null) InputSystem.RemoveDevice(pad);
        }
        pads.Clear();
        SaveManager.DeleteSave();
    }

    protected Gamepad AddPad()
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        pads.Add(pad);
        return pad;
    }

    protected IEnumerator Press(Gamepad pad, GamepadButton button)
    {
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button, true));
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button, false));
        InputSystem.Update();
        yield return null;
    }

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
        for (int i = 0; i < overlayCanvases.Count; i++)
        {
            savedModes[i] = overlayCanvases[i].renderMode;
            savedCameras[i] = overlayCanvases[i].worldCamera;
            overlayCanvases[i].renderMode = RenderMode.ScreenSpaceCamera;
            overlayCanvases[i].worldCamera = camera;
        }

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
        }

        string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
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
