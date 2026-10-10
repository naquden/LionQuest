using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds an on-screen wave banner: a small Canvas prefab at the top-center of the screen,
/// showing "Wave 3 — 12 enemies" or "Wave 3 cleared!" driven by WaveSpawner events.
/// Wired into Level1.unity's WaveSpawner. Dual entry point per project convention.
/// </summary>
public static class WaveBannerGenerator
{
    const string UIDir = "Assets/Prefabs/UI";
    const string PrefabPath = UIDir + "/WaveBannerCanvas.prefab";
    const string Level1ScenePath = "Assets/Scenes/Level1.unity";

    [MenuItem("LionQuest/Generate/Wave Banner")]
    public static void Generate()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogWarning("WaveBannerGenerator: TMP Essentials not imported " +
                "(Window > TextMeshPro > Import TMP Essential Resources). Continuing anyway.");
        }

        EnsureFolder(UIDir);

        GameObject prefab = BuildCanvasPrefab();
        WireIntoLevel1(prefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("WaveBannerGenerator: done.");
    }

    static GameObject BuildCanvasPrefab()
    {
        var root = new GameObject("WaveBannerCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // above HUD, below pause menu

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Banner is display-only — never intercept gameplay input.
        root.GetComponent<GraphicRaycaster>().enabled = false;

        WaveBannerUI banner = BuildBanner(root.transform);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"WaveBannerGenerator: prefab ready at {PrefabPath}");
        return prefab;
    }

    static WaveBannerUI BuildBanner(Transform canvasTransform)
    {
        var panelGo = new GameObject("BannerPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelGo.transform.SetParent(canvasTransform, false);

        var rect = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(400, 80);
        rect.anchoredPosition = new Vector2(0f, -24f); // top-center with small margin

        Image panelImage = panelGo.GetComponent<Image>();
        panelImage.sprite = UiTheme.RoundedPanelSprite;
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(UiTheme.Surface.r, UiTheme.Surface.g, UiTheme.Surface.b, 0.9f);
        panelImage.raycastTarget = false;

        CanvasGroup canvasGroup = panelGo.GetComponent<CanvasGroup>();

        TMP_Text waveText = new GameObject("WaveText", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        waveText.transform.SetParent(panelGo.transform, false);
        UiTheme.StretchFull((RectTransform)waveText.transform);
        waveText.text = "Wave 1 — 10 enemies";
        waveText.fontSize = 36;
        waveText.alignment = TextAlignmentOptions.Center;
        waveText.color = UiTheme.Gold;
        waveText.fontStyle = FontStyles.Bold;
        waveText.font = UiTheme.DefaultFont();
        waveText.raycastTarget = false;

        var bannerUI = panelGo.AddComponent<WaveBannerUI>();
        var so = new SerializedObject(bannerUI);
        so.FindProperty("waveText").objectReferenceValue = waveText;
        so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("fadeOutDelay").floatValue = 1.5f;
        so.FindProperty("fadeDuration").floatValue = 0.5f;
        so.ApplyModifiedProperties();

        return bannerUI;
    }

    static void WireIntoLevel1(GameObject bannerPrefab)
    {
        if (!System.IO.File.Exists(Level1ScenePath))
        {
            Debug.LogError($"WaveBannerGenerator: Level1 scene not found at {Level1ScenePath} — " +
                "run 'LionQuest/Generate/Level 1' first.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Single);

        WaveBannerUI existingBanner = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            existingBanner = rootGo.GetComponentInChildren<WaveBannerUI>(true);
            if (existingBanner != null) break;
        }
        if (existingBanner != null)
        {
            Object.DestroyImmediate(existingBanner.gameObject);
        }

        var bannerInstance = (GameObject)PrefabUtility.InstantiatePrefab(bannerPrefab, scene);
        if (bannerInstance == null)
        {
            Debug.LogError("WaveBannerGenerator: failed to instantiate banner prefab into Level1.");
            return;
        }

        WaveBannerUI bannerUI = bannerInstance.GetComponentInChildren<WaveBannerUI>(true);
        if (bannerUI == null)
        {
            Debug.LogError("WaveBannerGenerator: banner prefab instance has no WaveBannerUI component.");
            return;
        }

        WaveSpawner waveSpawner = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            waveSpawner = rootGo.GetComponentInChildren<WaveSpawner>(true);
            if (waveSpawner != null) break;
        }

        if (waveSpawner == null)
        {
            Debug.LogError("WaveBannerGenerator: no WaveSpawner found in Level1 — " +
                "run 'LionQuest/Generate/Level 1' first.");
        }
        else
        {
            var so = new SerializedObject(bannerUI);
            so.FindProperty("spawner").objectReferenceValue = waveSpawner;
            so.ApplyModifiedProperties();
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
