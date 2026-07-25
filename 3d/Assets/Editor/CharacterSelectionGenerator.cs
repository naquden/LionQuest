using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the 4-player local Character Selection screen: CharacterDefinition assets,
/// SelectionPlayer/PlayerSlot/CarouselItem prefabs, the CharacterSelection.unity scene,
/// the gameplay-side PlayerSpawner wiring in SampleScene, and Build Settings registration.
/// </summary>
public static class CharacterSelectionGenerator
{
    const string UIDir = "Assets/Prefabs/UI";
    const string SelectionPlayerPath = UIDir + "/SelectionPlayer.prefab";
    const string CarouselItemPath = UIDir + "/CarouselItem.prefab";
    const string PlayerSlotPath = UIDir + "/PlayerSlot.prefab";
    const string ScenePath = "Assets/Scenes/CharacterSelection.unity";
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    const string StartScreenScenePath = "Assets/Scenes/StartScreen.unity";

    const string CharactersDir = "Assets/Characters";
    const string AssCapPrefabPath = CharactersDir + "/AssassinoCapuchino/AssasinoCappuchino.prefab";
    const string AssCapDefinitionPath = CharactersDir + "/AssassinoCapuchino/AssCap_Definition.asset";

    const string GameControllerPrefabPath = "Assets/Prefabs/GameController.prefab";
    const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    [MenuItem("LionQuest/Generate/Character Selection")]
    public static void Generate()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogWarning("CharacterSelectionGenerator: TMP Essentials not imported " +
                "(Window > TextMeshPro > Import TMP Essential Resources). Continuing anyway.");
        }

        EnsureFolder(UIDir);

        EnsureAssCapDefinition();
        AssetDatabase.SaveAssets();
        EnsurePlayerInputOnPrefab(AssCapPrefabPath);

        GameObject carouselItemPrefab = BuildCarouselItemPrefab();
        GameObject playerSlotPrefab = BuildPlayerSlotPrefab(carouselItemPrefab);
        GameObject selectionPlayerPrefab = BuildSelectionPlayerPrefab();

        BuildCharacterSelectionScene(playerSlotPrefab, selectionPlayerPrefab);
        PrepSampleScene();

        RegisterBuildScenes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("CharacterSelectionGenerator: done.");
    }

    // ------------------------------------------------------------------
    // CharacterDefinition assets
    // ------------------------------------------------------------------

    static CharacterDefinition EnsureAssCapDefinition()
    {
        var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssCapDefinitionPath);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<CharacterDefinition>();
            AssetDatabase.CreateAsset(def, AssCapDefinitionPath);
        }

        def.characterID = "AssCap";
        def.displayName = "Assassino Capuchino";
        def.unlockedByDefault = true;
        def.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssCapPrefabPath);

        EditorUtility.SetDirty(def);
        return def;
    }

    static CharacterDefinition[] DiscoverDefinitions()
    {
        var list = new List<CharacterDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterDefinition"))
        {
            var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def != null) list.Add(def);
        }
        return list.ToArray();
    }

    // ------------------------------------------------------------------
    // AssCap prefab: add PlayerInput
    // ------------------------------------------------------------------

    static void EnsurePlayerInputOnPrefab(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"CharacterSelectionGenerator: prefab not found at {prefabPath}");
            return;
        }

        using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            GameObject root = editScope.prefabContentsRoot;

            PlayerInput pi = root.GetComponent<PlayerInput>();
            if (pi == null) pi = root.AddComponent<PlayerInput>();

            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            var so = new SerializedObject(pi);
            so.FindProperty("m_Actions").objectReferenceValue = actions;
            so.FindProperty("m_DefaultActionMap").stringValue = "Player";
            so.FindProperty("m_NotificationBehavior").enumValueIndex = (int)PlayerNotifications.InvokeUnityEvents;
            so.ApplyModifiedProperties();
        }
    }

    // ------------------------------------------------------------------
    // CarouselItem.prefab
    // ------------------------------------------------------------------

    static GameObject BuildCarouselItemPrefab()
    {
        var root = new GameObject("CarouselItem", typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 60);

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        StretchFull(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 0.8f);

        var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitGo.transform.SetParent(root.transform, false);
        var portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 0.5f);
        portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.sizeDelta = new Vector2(50, 50);
        portraitRect.anchoredPosition = new Vector2(5, 0);

        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(root.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(60, 0);
        nameRect.offsetMax = new Vector2(-30, 0);
        var nameText = nameGo.GetComponent<TextMeshProUGUI>();
        nameText.text = "Character";
        nameText.fontSize = 22;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.color = Color.white;

        var lockGo = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
        lockGo.transform.SetParent(root.transform, false);
        var lockRect = lockGo.GetComponent<RectTransform>();
        lockRect.anchorMin = new Vector2(1f, 0.5f);
        lockRect.anchorMax = new Vector2(1f, 0.5f);
        lockRect.pivot = new Vector2(1f, 0.5f);
        lockRect.sizeDelta = new Vector2(24, 24);
        lockRect.anchoredPosition = new Vector2(-5, 0);
        lockGo.GetComponent<Image>().color = Color.yellow;

        var item = root.AddComponent<CharacterCarouselItem>();
        var so = new SerializedObject(item);
        so.FindProperty("portrait").objectReferenceValue = portraitGo.GetComponent<Image>();
        so.FindProperty("lockIcon").objectReferenceValue = lockGo;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CarouselItemPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------------
    // PlayerSlot.prefab
    // ------------------------------------------------------------------

    static GameObject BuildPlayerSlotPrefab(GameObject carouselItemPrefab)
    {
        var root = new GameObject("PlayerSlot", typeof(RectTransform), typeof(Image));
        root.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.6f);

        GameObject emptyPanel = BuildEmptyPanel(root.transform);
        (GameObject selectingPanel, Transform carouselContainer, TMP_Text nameText, TMP_Text soulsText,
            TMP_Text strengthText, TMP_Text healthText, TMP_Text speedText, TMP_Text lockedFlashText) =
            BuildSelectingPanel(root.transform);
        (GameObject readyOverlay, Image readyPortrait, TMP_Text readyName) = BuildReadyOverlay(root.transform);

        var slotUI = root.AddComponent<PlayerSlotUI>();
        var so = new SerializedObject(slotUI);
        so.FindProperty("emptyPanel").objectReferenceValue = emptyPanel;
        so.FindProperty("selectingPanel").objectReferenceValue = selectingPanel;
        so.FindProperty("readyOverlay").objectReferenceValue = readyOverlay;
        so.FindProperty("characterNameText").objectReferenceValue = nameText;
        so.FindProperty("soulsText").objectReferenceValue = soulsText;
        so.FindProperty("strengthText").objectReferenceValue = strengthText;
        so.FindProperty("healthText").objectReferenceValue = healthText;
        so.FindProperty("speedText").objectReferenceValue = speedText;
        so.FindProperty("lockedFlashText").objectReferenceValue = lockedFlashText;
        so.FindProperty("readyPortraitImage").objectReferenceValue = readyPortrait;
        so.FindProperty("readyNameText").objectReferenceValue = readyName;
        so.FindProperty("carouselContainer").objectReferenceValue = carouselContainer;
        so.FindProperty("carouselItemPrefab").objectReferenceValue = carouselItemPrefab;
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerSlotPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildEmptyPanel(Transform parent)
    {
        var panel = new GameObject("EmptyPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        StretchFull((RectTransform)panel.transform);

        var textGo = new GameObject("PromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(panel.transform, false);
        StretchFull((RectTransform)textGo.transform);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = "Press A to Join";
        text.fontSize = 28;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 1f, 1f, 0.7f);

        return panel;
    }

    static (GameObject, Transform, TMP_Text, TMP_Text, TMP_Text, TMP_Text, TMP_Text, TMP_Text) BuildSelectingPanel(Transform parent)
    {
        var panel = new GameObject("SelectingPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        StretchFull((RectTransform)panel.transform);
        panel.SetActive(false);

        // Carousel: vertical list in the upper 60% of the slot.
        var carouselGo = new GameObject("Carousel", typeof(RectTransform), typeof(VerticalLayoutGroup));
        carouselGo.transform.SetParent(panel.transform, false);
        var carouselRect = carouselGo.GetComponent<RectTransform>();
        carouselRect.anchorMin = new Vector2(0f, 0.4f);
        carouselRect.anchorMax = new Vector2(1f, 1f);
        carouselRect.offsetMin = Vector2.zero;
        carouselRect.offsetMax = Vector2.zero;
        var layout = carouselGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text nameText = CreateStatText(panel.transform, "NameText", new Vector2(0f, 0.34f), new Vector2(1f, 0.4f), 24, TextAlignmentOptions.Center);
        TMP_Text soulsText = CreateStatText(panel.transform, "SoulsText", new Vector2(0f, 0.26f), new Vector2(1f, 0.34f), 18, TextAlignmentOptions.Center);
        TMP_Text strengthText = CreateStatText(panel.transform, "StrengthText", new Vector2(0f, 0.18f), new Vector2(1f, 0.26f), 18, TextAlignmentOptions.Center);
        TMP_Text healthText = CreateStatText(panel.transform, "HealthText", new Vector2(0f, 0.10f), new Vector2(1f, 0.18f), 18, TextAlignmentOptions.Center);
        TMP_Text speedText = CreateStatText(panel.transform, "SpeedText", new Vector2(0f, 0.02f), new Vector2(1f, 0.10f), 18, TextAlignmentOptions.Center);

        var lockedGo = new GameObject("LockedFlashText", typeof(RectTransform), typeof(TextMeshProUGUI));
        lockedGo.transform.SetParent(panel.transform, false);
        var lockedRect = lockedGo.GetComponent<RectTransform>();
        lockedRect.anchorMin = new Vector2(0f, 0.4f);
        lockedRect.anchorMax = new Vector2(1f, 0.6f);
        lockedRect.offsetMin = Vector2.zero;
        lockedRect.offsetMax = Vector2.zero;
        var lockedText = lockedGo.GetComponent<TextMeshProUGUI>();
        lockedText.text = "LOCKED";
        lockedText.fontSize = 32;
        lockedText.color = Color.red;
        lockedText.alignment = TextAlignmentOptions.Center;
        lockedGo.SetActive(false);

        return (panel, carouselGo.transform, nameText, soulsText, strengthText, healthText, speedText, lockedText);
    }

    static TMP_Text CreateStatText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    static (GameObject, Image, TMP_Text) BuildReadyOverlay(Transform parent)
    {
        var overlay = new GameObject("ReadyOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(parent, false);
        StretchFull((RectTransform)overlay.transform);
        overlay.GetComponent<Image>().color = new Color(0.1f, 0.4f, 0.1f, 0.5f);
        overlay.SetActive(false);

        var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitGo.transform.SetParent(overlay.transform, false);
        var portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
        portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
        portraitRect.pivot = new Vector2(0.5f, 0.5f);
        portraitRect.sizeDelta = new Vector2(120, 120);
        portraitRect.anchoredPosition = new Vector2(0, 30);

        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(overlay.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0.3f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        var nameText = nameGo.GetComponent<TextMeshProUGUI>();
        nameText.text = "READY";
        nameText.fontSize = 26;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.green;

        return (overlay, portraitGo.GetComponent<Image>(), nameText);
    }

    // ------------------------------------------------------------------
    // SelectionPlayer.prefab — PlayerInput + handler only, no gameplay components
    // ------------------------------------------------------------------

    static GameObject BuildSelectionPlayerPrefab()
    {
        var root = new GameObject("SelectionPlayer", typeof(PlayerInput), typeof(CharacterSelectionInputHandler));

        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        PlayerInput pi = root.GetComponent<PlayerInput>();

        var so = new SerializedObject(pi);
        so.FindProperty("m_Actions").objectReferenceValue = actions;
        so.FindProperty("m_DefaultActionMap").stringValue = "UI";
        so.FindProperty("m_NotificationBehavior").enumValueIndex = (int)PlayerNotifications.InvokeUnityEvents;
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SelectionPlayerPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------
    // CharacterSelection.unity
    // ------------------------------------------------------------------

    static void BuildCharacterSelectionScene(GameObject playerSlotPrefab, GameObject selectionPlayerPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // NewScene/OpenScene can invalidate asset references acquired in a prior scene context —
        // always re-resolve the roster right after switching scenes, never trust a passed-in array.
        CharacterDefinition[] roster = DiscoverDefinitions();

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var rowGo = new GameObject("SlotRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(canvasGo.transform, false);
        StretchFull((RectTransform)rowGo.transform);
        var rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;
        rowLayout.spacing = 4;

        var slotUIs = new PlayerSlotUI[4];
        for (int i = 0; i < 4; i++)
        {
            var slotInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerSlotPrefab, rowGo.transform);
            slotInstance.name = $"PlayerSlot_{i}";
            slotUIs[i] = slotInstance.GetComponent<PlayerSlotUI>();
        }

        GameObject startPrompt = BuildStartPrompt(canvasGo.transform);

        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        var managerGo = new GameObject("CharacterSelectionManager", typeof(CharacterSelectionManager));
        var manager = managerGo.GetComponent<CharacterSelectionManager>();
        var managerSo = new SerializedObject(manager);
        var slotsProp = managerSo.FindProperty("slots");
        slotsProp.arraySize = slotUIs.Length;
        for (int i = 0; i < slotUIs.Length; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotUIs[i];
        }
        var rosterProp = managerSo.FindProperty("allCharacters");
        rosterProp.arraySize = roster.Length;
        for (int i = 0; i < roster.Length; i++)
        {
            rosterProp.GetArrayElementAtIndex(i).objectReferenceValue = roster[i];
        }
        managerSo.FindProperty("startPrompt").objectReferenceValue = startPrompt;
        managerSo.FindProperty("gameplaySceneName").stringValue = "SampleScene";
        managerSo.ApplyModifiedProperties();

        var inputManagerGo = new GameObject("PlayerInputManager", typeof(PlayerInputManager));
        var inputManager = inputManagerGo.GetComponent<PlayerInputManager>();
        var inputManagerSo = new SerializedObject(inputManager);
        inputManagerSo.FindProperty("m_JoinBehavior").enumValueIndex = (int)PlayerJoinBehavior.JoinPlayersWhenJoinActionIsTriggered;
        inputManagerSo.FindProperty("m_MaxPlayerCount").intValue = 4;
        inputManagerSo.FindProperty("m_PlayerPrefab").objectReferenceValue = selectionPlayerPrefab;

        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        InputAction joinAction = actions.FindAction("UI/Join");
        InputActionReference joinReference = FindActionReference(InputActionsPath, joinAction);
        if (joinReference != null)
        {
            var joinActionProp = inputManagerSo.FindProperty("m_JoinAction");
            joinActionProp.FindPropertyRelative("m_UseReference").boolValue = true;
            joinActionProp.FindPropertyRelative("m_Reference").objectReferenceValue = joinReference;
        }
        else
        {
            Debug.LogError("CharacterSelectionGenerator: could not resolve InputActionReference for UI/Join.");
        }
        inputManagerSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static InputActionReference FindActionReference(string assetPath, InputAction action)
    {
        if (action == null) return null;
        foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (obj is InputActionReference actionRef && actionRef.action != null && actionRef.action.id == action.id)
            {
                return actionRef;
            }
        }
        return null;
    }

    static GameObject BuildStartPrompt(Transform parent)
    {
        var go = new GameObject("StartPrompt", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(500, 80);
        rect.anchoredPosition = new Vector2(0, 20);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = "Press Start";
        text.fontSize = 40;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.yellow;

        go.SetActive(false);
        return go;
    }

    // ------------------------------------------------------------------
    // SampleScene prep: PlayerSpawner + spawn points + GameController
    // ------------------------------------------------------------------

    static void PrepSampleScene()
    {
        var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        // Re-resolve after the scene switch — see BuildCharacterSelectionScene for why.
        CharacterDefinition[] roster = DiscoverDefinitions();
        CharacterDefinition fallbackDefinition = roster.FirstOrDefault(d => d.characterID == "AssCap");

        GameObject gameControllerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameControllerPrefabPath);

        bool hasGameController = false;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            if (rootGo.GetComponentInChildren<GameSaveController>(true) != null)
            {
                hasGameController = true;
                break;
            }
        }
        if (!hasGameController && gameControllerPrefab != null)
        {
            PrefabUtility.InstantiatePrefab(gameControllerPrefab, scene);
        }

        PlayerSpawner spawner = null;
        Transform[] spawnPoints = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            spawner = rootGo.GetComponentInChildren<PlayerSpawner>(true);
            if (spawner != null) break;
        }

        if (spawner == null)
        {
            var spawnerGo = new GameObject("PlayerSpawner", typeof(PlayerSpawner));
            spawner = spawnerGo.GetComponent<PlayerSpawner>();

            var pointsParent = new GameObject("SpawnPoints").transform;
            spawnPoints = new Transform[4];
            Vector3[] offsets =
            {
                new Vector3(-3f, 0f, -3f), new Vector3(3f, 0f, -3f),
                new Vector3(-3f, 0f, 3f), new Vector3(3f, 0f, 3f)
            };
            for (int i = 0; i < 4; i++)
            {
                var point = new GameObject($"SpawnPoint_{i}").transform;
                point.SetParent(pointsParent, false);
                point.position = offsets[i];
                spawnPoints[i] = point;
            }
        }
        else
        {
            var so = new SerializedObject(spawner);
            var existing = so.FindProperty("spawnPoints");
            spawnPoints = new Transform[existing.arraySize];
            for (int i = 0; i < existing.arraySize; i++)
            {
                spawnPoints[i] = existing.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            }
        }

        var spawnerSo = new SerializedObject(spawner);
        var spawnPointsProp = spawnerSo.FindProperty("spawnPoints");
        spawnPointsProp.arraySize = spawnPoints.Length;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            spawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        }
        spawnerSo.FindProperty("fallbackDefinition").objectReferenceValue = fallbackDefinition;
        spawnerSo.FindProperty("gameControllerPrefab").objectReferenceValue = gameControllerPrefab;
        var allCharProp = spawnerSo.FindProperty("allCharacters");
        allCharProp.arraySize = roster.Length;
        for (int i = 0; i < roster.Length; i++)
        {
            allCharProp.GetArrayElementAtIndex(i).objectReferenceValue = roster[i];
        }
        spawnerSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ------------------------------------------------------------------
    // Build Settings
    // ------------------------------------------------------------------

    static void RegisterBuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        var seen = new HashSet<string>();

        void AddIfMissing(string path)
        {
            if (seen.Add(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        AddIfMissing(StartScreenScenePath);
        AddIfMissing(ScenePath);
        AddIfMissing(SampleScenePath);

        foreach (var existing in EditorBuildSettings.scenes)
        {
            if (seen.Add(existing.path)) scenes.Add(new EditorBuildSettingsScene(existing.path, existing.enabled));
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
