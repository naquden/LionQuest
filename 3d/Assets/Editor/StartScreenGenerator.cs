using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Builds the Start Screen: a uGUI Canvas prefab (Play/Exit), a 3D showcase diorama
/// (terrain + characters + orbiting camera), the scene, and Build Settings registration.
/// Dual entry point per project convention.
/// </summary>
public static class StartScreenGenerator
{
    const string UIDir = "Assets/Prefabs/UI";
    const string PrefabPath = UIDir + "/StartScreenCanvas.prefab";
    const string ScenePath = "Assets/Scenes/StartScreen.unity";
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    const string ShaderDir = "Assets/Shaders";
    const string TerrainDir = "Assets/Terrain";
    const string DetailDir = "Assets/Terrain/Details";
    const string TerrainDataPath = TerrainDir + "/StartScreenTerrain.asset";
    const string VolumeProfilePath = "Assets/Settings/SampleSceneProfile.asset";
    const string CharactersDir = "Assets/Characters";

    [MenuItem("LionQuest/Generate/Start Screen")]
    public static void Generate()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogWarning("StartScreenGenerator: TMP Essentials not imported " +
                "(Window > TextMeshPro > Import TMP Essential Resources). Continuing anyway.");
        }

        EnsureFolder(UIDir);

        GameObject prefab = BuildCanvasPrefab();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        PrefabUtility.InstantiatePrefab(prefab, scene);
        BuildEventSystem();

        Camera camera = BuildCameraAndLight();
        BuildVolume();
        Terrain terrain = BuildTerrain();
        CharacterAnimator[] characters = PlaceCharacters(terrain);
        BuildDiorama(camera, terrain, characters);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        RegisterBuildScenes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("StartScreenGenerator: done.");
    }

    static GameObject BuildCanvasPrefab()
    {
        var root = new GameObject("StartScreenCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        BuildBackground(root.transform);
        BuildTitle(root.transform);
        Transform menuPanel = BuildMenuPanel(root.transform);

        Button playButton = CreateButton(menuPanel, "PlayButton", "Play");
        Button exitButton = CreateButton(menuPanel, "ExitButton", "Exit");

        var controller = root.AddComponent<StartScreenController>();
        var so = new SerializedObject(controller);
        so.FindProperty("playButton").objectReferenceValue = playButton;
        so.FindProperty("exitButton").objectReferenceValue = exitButton;
        so.FindProperty("playSceneName").stringValue = "CharacterSelection";
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"StartScreenGenerator: prefab ready at {PrefabPath}");
        return prefab;
    }

    static void BuildBackground(Transform parent)
    {
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(parent, false);

        var rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bgImage = background.GetComponent<Image>();
        bgImage.color = new Color(UiTheme.BgBottom.r, UiTheme.BgBottom.g, UiTheme.BgBottom.b, 0.35f);
    }

    static void BuildTitle(Transform parent)
    {
        var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(parent, false);

        var rect = title.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.75f);
        rect.anchorMax = new Vector2(0.5f, 0.75f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(800, 150);
        rect.anchoredPosition = Vector2.zero;

        var text = title.GetComponent<TextMeshProUGUI>();
        text.text = "LionQuest";
        text.fontSize = 72;
        text.alignment = TextAlignmentOptions.Center;
        text.color = UiTheme.Gold;
        text.fontStyle = FontStyles.Bold;
        text.font = UiTheme.DefaultFont();
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
        rect.sizeDelta = new Vector2(300, 0);
        rect.anchoredPosition = Vector2.zero;

        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 32;
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
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 90);

        var layoutElement = go.GetComponent<LayoutElement>();
        layoutElement.minHeight = 90;
        layoutElement.preferredHeight = 90;

        var image = go.GetComponent<Image>();
        image.sprite = UiTheme.RoundedPanelSprite;
        image.type = Image.Type.Sliced;
        image.color = UiTheme.Surface;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 42;
        text.alignment = TextAlignmentOptions.Center;
        text.color = UiTheme.TextLight;
        text.fontStyle = FontStyles.Bold;
        text.font = UiTheme.DefaultFont();

        return button;
    }

    static void BuildEventSystem()
    {
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    static Camera BuildCameraAndLight()
    {
        var cameraGo = new GameObject("Main Camera", typeof(Camera));
        cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 45f;
        camera.transform.position = new Vector3(0f, 14f, -30f);
        camera.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

        var lightGo = new GameObject("Directional Light", typeof(Light));
        Light light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        return camera;
    }

    static void BuildVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            Debug.LogError($"StartScreenGenerator: volume profile not found at {VolumeProfilePath}.");
            return;
        }

        var go = new GameObject("Global Volume", typeof(Volume));
        Volume volume = go.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    static Terrain BuildTerrain()
    {
        EnsureFolder(TerrainDir);

        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        if (terrainData == null)
        {
            terrainData = new TerrainData();
            AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
        }

        var terrainGo = new GameObject("Terrain", typeof(Terrain), typeof(TerrainCollider), typeof(MapGenerator));
        terrainGo.transform.position = new Vector3(-30f, 0f, -30f);

        Terrain terrain = terrainGo.GetComponent<Terrain>();
        terrain.terrainData = terrainData;
        terrainGo.GetComponent<TerrainCollider>().terrainData = terrainData;

        MapGenerator generator = terrainGo.GetComponent<MapGenerator>();
        var so = new SerializedObject(generator);

        so.FindProperty("mapSize").vector2IntValue = new Vector2Int(60, 60);
        so.FindProperty("terrainHeight").floatValue = 12f;
        so.FindProperty("flatTerrain").boolValue = false;
        so.FindProperty("islandRadius").floatValue = 16f;
        so.FindProperty("oceanDepth").floatValue = 3f;
        so.FindProperty("beachWidth").floatValue = 4f;
        so.FindProperty("holeCount").intValue = 0;
        so.FindProperty("pathCount").intValue = 1;
        so.FindProperty("generateOnStart").boolValue = false;

        so.FindProperty("terrainMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>($"{ShaderDir}/StylizedTerrain.mat");
        so.FindProperty("waterMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>($"{ShaderDir}/StylizedWater.mat");
        so.FindProperty("sandGroundType").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GroundType>($"{TerrainDir}/Sand.asset");

        var groundTypes = so.FindProperty("availableGroundTypes");
        string[] groundTypeNames = { "Grass", "Dirt", "Hole", "Sand" };
        groundTypes.arraySize = groundTypeNames.Length;
        for (int i = 0; i < groundTypeNames.Length; i++)
        {
            groundTypes.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GroundType>($"{TerrainDir}/{groundTypeNames[i]}.asset");
        }

        string[] detailNames = { "GrassTuftA", "GrassTuftB", "GrassTuftC", "FlowerYellow", "FlowerWhite" };
        var details = so.FindProperty("detailTextures");
        details.arraySize = detailNames.Length;
        for (int i = 0; i < detailNames.Length; i++)
        {
            details.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{DetailDir}/{detailNames[i]}.png");
        }

        so.ApplyModifiedProperties();

        generator.GenerateMap();

        return terrain;
    }

    /// <summary>
    /// Discovers every character prefab with a CharacterAnimator, places it on the island,
    /// and strips the gameplay components (input/combat/save-registration) that would crash
    /// or misbehave outside SampleScene.
    /// </summary>
    static CharacterAnimator[] PlaceCharacters(Terrain terrain)
    {
        var showcased = new List<CharacterAnimator>();

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { CharactersDir });
        var prefabs = new List<GameObject>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (candidate != null && candidate.GetComponentInChildren<CharacterAnimator>(true) != null)
            {
                prefabs.Add(candidate);
            }
        }

        if (prefabs.Count == 0)
        {
            Debug.LogWarning($"StartScreenGenerator: no character prefabs with CharacterAnimator found under {CharactersDir}.");
            return showcased.ToArray();
        }

        Vector3 terrainCenter = terrain.transform.position + new Vector3(terrain.terrainData.size.x * 0.5f, 0f, terrain.terrainData.size.z * 0.5f);
        float arcRadius = 8f;

        for (int i = 0; i < prefabs.Count; i++)
        {
            float t = prefabs.Count == 1 ? 0.5f : (float)i / (prefabs.Count - 1);
            float angle = Mathf.Lerp(-40f, 40f, t) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(angle) * arcRadius, 0f, Mathf.Cos(angle) * arcRadius * 0.4f);
            Vector3 worldPos = terrainCenter + offset;
            worldPos.y = terrain.SampleHeight(worldPos) + terrain.transform.position.y;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
            instance.transform.position = worldPos;
            instance.transform.rotation = Quaternion.LookRotation(terrainCenter - worldPos, Vector3.up);

            StripGameplayComponents(instance);

            CharacterAnimator animator = instance.GetComponentInChildren<CharacterAnimator>(true);
            if (animator != null) showcased.Add(animator);
        }

        return showcased.ToArray();
    }

    static void StripGameplayComponents(GameObject instance)
    {
        foreach (var controller in instance.GetComponentsInChildren<TopDownPlayerController>(true))
            Object.DestroyImmediate(controller);
        foreach (var stats in instance.GetComponentsInChildren<CharacterStats>(true))
            Object.DestroyImmediate(stats);
        foreach (var combat in instance.GetComponentsInChildren<CombatController>(true))
            Object.DestroyImmediate(combat);
        foreach (var rb in instance.GetComponentsInChildren<Rigidbody>(true))
            rb.isKinematic = true;
    }

    static void BuildDiorama(Camera camera, Terrain terrain, CharacterAnimator[] characters)
    {
        var pivotGo = new GameObject("DioramaPivot");
        pivotGo.transform.position = terrain.transform.position +
            new Vector3(terrain.terrainData.size.x * 0.5f, 0f, terrain.terrainData.size.z * 0.5f);

        var dioramaGo = new GameObject("Diorama", typeof(StartScreenDiorama));
        StartScreenDiorama diorama = dioramaGo.GetComponent<StartScreenDiorama>();

        var so = new SerializedObject(diorama);
        so.FindProperty("orbitPivot").objectReferenceValue = pivotGo.transform;
        so.FindProperty("orbitCamera").objectReferenceValue = camera.transform;

        var showcase = so.FindProperty("showcaseCharacters");
        showcase.arraySize = characters.Length;
        for (int i = 0; i < characters.Length; i++)
        {
            showcase.GetArrayElementAtIndex(i).objectReferenceValue = characters[i];
        }

        so.ApplyModifiedProperties();
    }

    static void RegisterBuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };

        bool hasSampleScene = false;
        foreach (var existing in EditorBuildSettings.scenes)
        {
            if (existing.path == ScenePath) continue;
            if (existing.path == SampleScenePath) hasSampleScene = true;
            scenes.Add(new EditorBuildSettingsScene(existing.path, existing.enabled));
        }

        if (!hasSampleScene)
        {
            scenes.Add(new EditorBuildSettingsScene(SampleScenePath, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
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
