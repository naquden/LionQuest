using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the in-game pause menu: a screen-space Canvas prefab (Resume / Character Selection /
/// Exit), wired into SampleScene's PlayerSpawner. Dual entry point per project convention.
/// </summary>
public static class PauseMenuGenerator
{
    const string UIDir = "Assets/Prefabs/UI";
    const string PrefabPath = UIDir + "/PauseMenuCanvas.prefab";
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    [MenuItem("LionQuest/Generate/Pause Menu")]
    public static void Generate()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogWarning("PauseMenuGenerator: TMP Essentials not imported " +
                "(Window > TextMeshPro > Import TMP Essential Resources). Continuing anyway.");
        }

        EnsureFolder(UIDir);

        GameObject prefab = BuildCanvasPrefab();
        WireIntoSampleScene(prefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("PauseMenuGenerator: done.");
    }

    // ------------------------------------------------------------------
    // PauseMenuCanvas.prefab
    // ------------------------------------------------------------------

    static GameObject BuildCanvasPrefab()
    {
        var root = new GameObject("PauseMenuCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above PlayerHUDCanvas

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = BuildPanel(root.transform);

        TMP_Text title = UiTheme.Text(panel.transform, "Title", "Paused", 56, TextAlignmentOptions.Center, UiTheme.Gold, FontStyles.Bold);
        title.rectTransform.anchoredPosition = new Vector2(0f, 220f);
        FixTitleRect(panel.transform);

        Transform menuPanel = BuildMenuPanel(panel.transform);

        Button resumeButton = CreateButton(menuPanel, "ResumeButton", "Resume");
        // Same slot as Resume, shown only on game over (PauseMenuController.Awake hides it).
        Button retryButton = CreateButton(menuPanel, "RetryButton", "Retry");
        Button characterSelectionButton = CreateButton(menuPanel, "CharacterSelectionButton", "Character Selection");
        Button exitButton = CreateButton(menuPanel, "ExitButton", "Exit");

        BuildVolumeSliders(menuPanel);

        WireNavigation(resumeButton, retryButton, characterSelectionButton, exitButton);

        InputActionAssetWireInto(root, panel, title, resumeButton, retryButton, characterSelectionButton, exitButton);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"PauseMenuGenerator: prefab ready at {PrefabPath}");
        return prefab;
    }

    static GameObject BuildPanel(Transform parent)
    {
        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(parent, false);

        var rect = panelGo.GetComponent<RectTransform>();
        UiTheme.StretchFull(rect);

        Image image = panelGo.GetComponent<Image>();
        image.color = new Color(UiTheme.BgBottom.r, UiTheme.BgBottom.g, UiTheme.BgBottom.b, 0.75f);

        return panelGo;
    }

    static void FixTitleRect(Transform panelTransform)
    {
        var titleTransform = panelTransform.Find("Title");
        var rect = (RectTransform)titleTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(800, 100);
        rect.anchoredPosition = new Vector2(0f, 220f);
    }

    static Transform BuildMenuPanel(Transform parent)
    {
        var panel = new GameObject("MenuPanel", typeof(RectTransform),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(parent, false);

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(400, 0);
        rect.anchoredPosition = Vector2.zero;

        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 24;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return panel.transform;
    }

    static Button CreateButton(Transform parent, string name, string label)
    {
        Button button = UiTheme.Button(parent, name, label, new Vector2(360, 90));

        var layoutElement = button.gameObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = 90;
        layoutElement.preferredHeight = 90;

        return button;
    }

    /// <summary>Master/Music/SFX rows below the buttons — this menu is the one screen guaranteed
    /// present in every gameplay scene, so it is where volume lives until a dedicated settings
    /// screen exists.</summary>
    static void BuildVolumeSliders(Transform parent)
    {
        Slider master = UiTheme.LabelledSlider(parent, "MasterVolumeRow", "Master", new Vector2(360, 44));
        Slider music = UiTheme.LabelledSlider(parent, "MusicVolumeRow", "Music", new Vector2(360, 44));
        Slider sfx = UiTheme.LabelledSlider(parent, "SfxVolumeRow", "SFX", new Vector2(360, 44));

        foreach (Slider slider in new[] { master, music, sfx })
        {
            var layoutElement = slider.gameObject.transform.parent.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 44;
            layoutElement.preferredHeight = 44;
        }

        var panelGo = parent.gameObject; // MenuPanel itself — VolumeSlidersPanel just needs the 3 slider refs, no new hierarchy.
        var panel = panelGo.AddComponent<VolumeSlidersPanel>();
        var so = new SerializedObject(panel);
        so.FindProperty("masterSlider").objectReferenceValue = master;
        so.FindProperty("musicSlider").objectReferenceValue = music;
        so.FindProperty("sfxSlider").objectReferenceValue = sfx;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Explicit navigation with Resume and Retry on the same rung: exactly one of the two is ever
    /// active. Character Selection's "up" is authored to Resume (the pause state) and repointed at
    /// Retry by PauseMenuController.ShowGameOver — Unity's explicit navigation does not skip an
    /// inactive target, it just refuses to move, so the up-link has to follow whichever is shown.
    /// </summary>
    static void WireNavigation(Button resume, Button retry, Button characterSelection, Button exit)
    {
        SetExplicitNavigation(resume, null, characterSelection);
        SetExplicitNavigation(retry, null, characterSelection);
        SetExplicitNavigation(characterSelection, resume, exit);
        SetExplicitNavigation(exit, characterSelection, null);
    }

    static void SetExplicitNavigation(Button button, Button up, Button down)
    {
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnUp = up;
        nav.selectOnDown = down;
        button.navigation = nav;
    }

    static void InputActionAssetWireInto(GameObject root, GameObject panel, TMP_Text title,
        Button resumeButton, Button retryButton, Button characterSelectionButton, Button exitButton)
    {
        var controller = root.AddComponent<PauseMenuController>();
        var so = new SerializedObject(controller);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("retryButton").objectReferenceValue = retryButton;
        so.FindProperty("resumeButton").objectReferenceValue = resumeButton;
        so.FindProperty("characterSelectionButton").objectReferenceValue = characterSelectionButton;
        so.FindProperty("exitButton").objectReferenceValue = exitButton;
        so.FindProperty("characterSelectionSceneName").stringValue = "CharacterSelection";

        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (actions == null)
        {
            Debug.LogError($"PauseMenuGenerator: could not load InputActionAsset at {InputActionsPath}.");
        }
        so.FindProperty("inputActions").objectReferenceValue = actions;

        so.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------------
    // SampleScene wiring — binary scene, generator-only edits
    // ------------------------------------------------------------------

    static void WireIntoSampleScene(GameObject pauseMenuPrefab)
    {
        var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        PauseMenuController existing = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            existing = rootGo.GetComponentInChildren<PauseMenuController>(true);
            if (existing != null) break;
        }
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(pauseMenuPrefab, scene);
        PauseMenuController pauseMenu = instance.GetComponent<PauseMenuController>();

        PlayerSpawner spawner = null;
        EventSystem eventSystem = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            if (spawner == null) spawner = rootGo.GetComponentInChildren<PlayerSpawner>(true);
            if (eventSystem == null) eventSystem = rootGo.GetComponentInChildren<EventSystem>(true);
        }

        if (spawner == null)
        {
            Debug.LogError("PauseMenuGenerator: no PlayerSpawner found in SampleScene — run 'LionQuest/Generate/Character Selection' first.");
        }
        else
        {
            var so = new SerializedObject(spawner);
            so.FindProperty("pauseMenu").objectReferenceValue = pauseMenu;
            so.ApplyModifiedProperties();
        }

        if (eventSystem == null)
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
