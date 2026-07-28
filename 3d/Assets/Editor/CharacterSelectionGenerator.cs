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
/// Warm fantasy / parchment art direction via UiTheme — see doc/characterSelection.md.
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
    const string SkillTreePanelPath = UIDir + "/SkillTreePanel.prefab";

    const string CharactersDir = "Assets/Characters";
    const string AssCapPrefabPath = CharactersDir + "/AssassinoCapuchino/AssasinoCappuchino.prefab";
    const string AssCapDefinitionPath = CharactersDir + "/AssassinoCapuchino/AssCap_Definition.asset";

    const string GameControllerPrefabPath = "Assets/Prefabs/GameController.prefab";
    const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    const string PreviewLayerName = CharacterPreviewStage.PreviewLayerName;

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
    // CarouselItem.prefab — compact accent dot + lock ring
    // ------------------------------------------------------------------

    static GameObject BuildCarouselItemPrefab()
    {
        var root = new GameObject("CarouselItem", typeof(RectTransform), typeof(CanvasGroup));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(28, 28);

        Image dot = UiTheme.Panel(root.transform, "Dot", UiTheme.Gold, UiTheme.CircleSprite);
        dot.type = Image.Type.Simple;
        UiTheme.StretchFull((RectTransform)dot.transform);

        var lockRingGo = new GameObject("LockRing", typeof(RectTransform), typeof(Image));
        lockRingGo.transform.SetParent(root.transform, false);
        UiTheme.StretchFull((RectTransform)lockRingGo.transform);
        Image lockRingImage = lockRingGo.GetComponent<Image>();
        lockRingImage.sprite = UiTheme.RingSprite;
        lockRingImage.type = Image.Type.Simple;
        lockRingImage.color = UiTheme.TextMuted;
        lockRingGo.SetActive(false);

        var item = root.AddComponent<CharacterCarouselItem>();
        var so = new SerializedObject(item);
        so.FindProperty("dot").objectReferenceValue = dot;
        so.FindProperty("lockRing").objectReferenceValue = lockRingGo;
        so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CarouselItemPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------
    // PlayerSlot.prefab — card shell (banner, border, glow) + 3 state panels
    // ------------------------------------------------------------------

    static GameObject BuildPlayerSlotPrefab(GameObject carouselItemPrefab)
    {
        var root = new GameObject("PlayerSlot", typeof(RectTransform), typeof(Image));
        Image cardBg = root.GetComponent<Image>();
        cardBg.sprite = UiTheme.RoundedPanelSprite;
        cardBg.type = Image.Type.Sliced;
        cardBg.color = UiTheme.Surface;

        // Soft accent glow sits behind the card, slightly oversized.
        var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glowGo.transform.SetParent(root.transform, false);
        var glowRect = glowGo.GetComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-24, -24);
        glowRect.offsetMax = new Vector2(24, 24);
        glowGo.transform.SetAsFirstSibling();
        Image glow = glowGo.GetComponent<Image>();
        glow.sprite = UiTheme.SoftGlowSprite;
        glow.color = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.35f);
        glow.raycastTarget = false;

        // Gold-trim border outline, drawn above content.
        var borderGo = new GameObject("CardBorder", typeof(RectTransform), typeof(Image));
        borderGo.transform.SetParent(root.transform, false);
        UiTheme.StretchFull((RectTransform)borderGo.transform);
        Image cardBorder = borderGo.GetComponent<Image>();
        cardBorder.sprite = UiTheme.RoundedOutlineSprite;
        cardBorder.type = Image.Type.Sliced;
        cardBorder.color = UiTheme.Gold;
        cardBorder.raycastTarget = false;

        // Accent banner across the top: "PLAYER n" label.
        var bannerGo = new GameObject("AccentBanner", typeof(RectTransform), typeof(Image));
        UiTheme.TopStretchX(bannerGo, root.transform, 0f, 1f, 0f, 64f);
        Image accentBanner = bannerGo.GetComponent<Image>();
        accentBanner.sprite = UiTheme.RoundedPanelSprite;
        accentBanner.type = Image.Type.Sliced;
        accentBanner.color = UiTheme.Gold;

        TMP_Text playerLabelText = UiTheme.Text(bannerGo.transform, "PlayerLabel", "PLAYER 1", 26,
            TextAlignmentOptions.Center, UiTheme.Locked, FontStyles.Bold);
        UiTheme.StretchFull((RectTransform)playerLabelText.transform);

        GameObject emptyPanel = BuildEmptyPanel(root.transform);
        CharacterPreviewStage previewStage = BuildPreviewStagePlaceholder(root.transform);
        (GameObject selectingPanel, Transform carouselContainer, TMP_Text nameText, TMP_Text soulsValueText,
            PlayerSlotUI.StatBar strengthBar, PlayerSlotUI.StatBar healthBar, PlayerSlotUI.StatBar speedBar,
            TMP_Text lockedFlashText, TMP_Text hintText) = BuildSelectingPanel(root.transform);
        (GameObject readyOverlay, TMP_Text readyNameText, TMP_Text readySealText) = BuildReadyOverlay(root.transform);

        var slotUI = root.AddComponent<PlayerSlotUI>();
        var so = new SerializedObject(slotUI);
        so.FindProperty("playerLabelText").objectReferenceValue = playerLabelText;
        so.FindProperty("accentBanner").objectReferenceValue = accentBanner;
        so.FindProperty("cardBorder").objectReferenceValue = cardBorder;
        so.FindProperty("glow").objectReferenceValue = glow;
        so.FindProperty("emptyPanel").objectReferenceValue = emptyPanel;
        so.FindProperty("selectingPanel").objectReferenceValue = selectingPanel;
        so.FindProperty("readyOverlay").objectReferenceValue = readyOverlay;
        so.FindProperty("previewStage").objectReferenceValue = previewStage;
        so.FindProperty("characterNameText").objectReferenceValue = nameText;
        so.FindProperty("soulsValueText").objectReferenceValue = soulsValueText;

        var strengthBarProp = so.FindProperty("strengthBar");
        strengthBarProp.FindPropertyRelative("label").objectReferenceValue = strengthBar.label;
        UiTheme.AssignArray(so, "strengthBar.segments", strengthBar.segments);
        var healthBarProp = so.FindProperty("healthBar");
        healthBarProp.FindPropertyRelative("label").objectReferenceValue = healthBar.label;
        UiTheme.AssignArray(so, "healthBar.segments", healthBar.segments);
        var speedBarProp = so.FindProperty("speedBar");
        speedBarProp.FindPropertyRelative("label").objectReferenceValue = speedBar.label;
        UiTheme.AssignArray(so, "speedBar.segments", speedBar.segments);

        so.FindProperty("lockedFlashText").objectReferenceValue = lockedFlashText;
        so.FindProperty("hintText").objectReferenceValue = hintText;
        so.FindProperty("readyNameText").objectReferenceValue = readyNameText;
        so.FindProperty("readySealText").objectReferenceValue = readySealText;
        so.FindProperty("carouselContainer").objectReferenceValue = carouselContainer;
        so.FindProperty("carouselItemPrefab").objectReferenceValue = carouselItemPrefab;
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerSlotPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------
    // CharacterPreviewStage — RawImage target lives in the card; camera/anchor/light are
    // created as scene-space siblings by BuildCharacterSelectionScene (per-slot world offset)
    // and wired onto this component there. Here we only build the RawImage + component shell.
    // ------------------------------------------------------------------

    static CharacterPreviewStage BuildPreviewStagePlaceholder(Transform parent)
    {
        var areaGo = new GameObject("PreviewArea", typeof(RectTransform));
        areaGo.transform.SetParent(parent, false);
        var areaRect = areaGo.GetComponent<RectTransform>();
        areaRect.anchorMin = new Vector2(0.5f, 1f);
        areaRect.anchorMax = new Vector2(0.5f, 1f);
        areaRect.pivot = new Vector2(0.5f, 1f);
        areaRect.sizeDelta = new Vector2(260, 320);
        areaRect.anchoredPosition = new Vector2(0f, -76f);

        var rawGo = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage));
        rawGo.transform.SetParent(areaGo.transform, false);
        UiTheme.StretchFull((RectTransform)rawGo.transform);
        RawImage rawImage = rawGo.GetComponent<RawImage>();
        rawImage.raycastTarget = false;

        var stageGo = new GameObject("PreviewStage", typeof(CharacterPreviewStage));
        stageGo.transform.SetParent(areaGo.transform, false);
        CharacterPreviewStage stage = stageGo.GetComponent<CharacterPreviewStage>();

        var so = new SerializedObject(stage);
        so.FindProperty("target").objectReferenceValue = rawImage;
        so.ApplyModifiedProperties();

        return stage;
    }

    // ------------------------------------------------------------------
    // EmptyPanel — dimmed "Press A to Join" prompt, pulsing
    // ------------------------------------------------------------------

    static GameObject BuildEmptyPanel(Transform parent)
    {
        var panel = new GameObject("EmptyPanel", typeof(RectTransform), typeof(CanvasGroup));
        panel.transform.SetParent(parent, false);
        UiTheme.StretchFull((RectTransform)panel.transform);
        panel.AddComponent<UiPulse>();

        TMP_Text text = UiTheme.Text(panel.transform, "PromptText", "Press Ⓐ to Join", 30,
            TextAlignmentOptions.Center, UiTheme.TextMuted, FontStyles.Bold);
        UiTheme.StretchFull((RectTransform)text.transform);

        return panel;
    }

    // ------------------------------------------------------------------
    // SelectingPanel — carousel dots + name + souls + stat bars + hints
    // ------------------------------------------------------------------

    static (GameObject, Transform, TMP_Text, TMP_Text, PlayerSlotUI.StatBar, PlayerSlotUI.StatBar, PlayerSlotUI.StatBar, TMP_Text, TMP_Text)
        BuildSelectingPanel(Transform parent)
    {
        var panel = new GameObject("SelectingPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        UiTheme.StretchFull((RectTransform)panel.transform);
        panel.SetActive(false);

        // Preview area (built in BuildPreviewStagePlaceholder) occupies offset 76..396 from the
        // top — everything below must start past 396 or it overlaps the live 3D character.
        TMP_Text nameText = UiTheme.Text(panel.transform, "NameText", "Character", 26,
            TextAlignmentOptions.Center, UiTheme.TextLight, FontStyles.Bold);
        UiTheme.TopStretchX(nameText.gameObject, panel.transform, 0f, 1f, 404f, 40f);

        var carouselGo = new GameObject("Carousel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var carouselLayout = carouselGo.GetComponent<HorizontalLayoutGroup>();
        carouselLayout.spacing = 10;
        carouselLayout.childAlignment = TextAnchor.MiddleCenter;
        carouselLayout.childControlWidth = false;
        carouselLayout.childControlHeight = false;
        carouselLayout.childForceExpandWidth = false;
        carouselLayout.childForceExpandHeight = false;
        UiTheme.TopStretchX(carouselGo, panel.transform, 0f, 1f, 448f, 36f);

        TMP_Text soulsValueText = UiTheme.Text(panel.transform, "SoulsValueText", "0 souls", 22,
            TextAlignmentOptions.Center, UiTheme.Gold, FontStyles.Bold);
        UiTheme.TopStretchX(soulsValueText.gameObject, panel.transform, 0f, 1f, 492f, 32f);

        PlayerSlotUI.StatBar strengthBar = BuildStatBar(panel.transform, "STR", 532f);
        PlayerSlotUI.StatBar healthBar = BuildStatBar(panel.transform, "HP", 568f);
        PlayerSlotUI.StatBar speedBar = BuildStatBar(panel.transform, "SPD", 604f);

        TMP_Text hintText = UiTheme.Text(panel.transform, "HintText",
            "Ⓐ Confirm   Ⓑ Leave   Tab Upgrade", 16,
            TextAlignmentOptions.Center, UiTheme.TextMuted);
        var hintRect = hintText.GetComponent<RectTransform>();
        hintRect.SetParent(panel.transform, false);
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(0f, 28f);
        hintRect.anchoredPosition = new Vector2(0f, 14f);

        var lockedGo = new GameObject("LockedFlashText", typeof(RectTransform));
        lockedGo.transform.SetParent(panel.transform, false);
        UiTheme.StretchFull((RectTransform)lockedGo.transform);
        TMP_Text lockedText = UiTheme.Text(lockedGo.transform, "Text", "LOCKED", 32,
            TextAlignmentOptions.Center, UiTheme.SelectedOutline, FontStyles.Bold);
        UiTheme.StretchFull((RectTransform)lockedText.transform);
        lockedGo.SetActive(false);

        return (panel, carouselGo.transform, nameText, soulsValueText, strengthBar, healthBar, speedBar, lockedText, hintText);
    }

    static PlayerSlotUI.StatBar BuildStatBar(Transform parent, string label, float yOffsetFromTop)
    {
        var rowGo = new GameObject($"StatBar_{label}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        UiTheme.TopStretchX(rowGo, parent, 0.08f, 0.92f, yOffsetFromTop, 28f);
        var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(LayoutElement));
        labelGo.transform.SetParent(rowGo.transform, false);
        labelGo.GetComponent<LayoutElement>().preferredWidth = 46f;
        TMP_Text labelText = UiTheme.Text(labelGo.transform, "Text", label, 16,
            TextAlignmentOptions.MidlineLeft, UiTheme.TextMuted, FontStyles.Bold);
        UiTheme.StretchFull((RectTransform)labelText.transform);

        const int segmentCount = 10;
        var segments = new Image[segmentCount];
        for (int i = 0; i < segmentCount; i++)
        {
            var segGo = new GameObject($"Segment_{i}", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            segGo.transform.SetParent(rowGo.transform, false);
            segGo.GetComponent<LayoutElement>().preferredWidth = 12f;
            Image seg = segGo.GetComponent<Image>();
            seg.sprite = UiTheme.PillFillSprite;
            seg.type = Image.Type.Sliced;
            seg.color = UiTheme.SurfaceInset;
            segments[i] = seg;
        }

        return new PlayerSlotUI.StatBar { label = labelText, segments = segments };
    }

    // ------------------------------------------------------------------
    // ReadyOverlay — accent seal over the same 3D preview
    // ------------------------------------------------------------------

    static (GameObject, TMP_Text, TMP_Text) BuildReadyOverlay(Transform parent)
    {
        var overlay = new GameObject("ReadyOverlay", typeof(RectTransform));
        overlay.transform.SetParent(parent, false);
        UiTheme.StretchFull((RectTransform)overlay.transform);
        overlay.SetActive(false);

        // Preview area occupies offset 76..396 from the top — keep the seal below it.
        TMP_Text sealText = UiTheme.Text(overlay.transform, "SealText", "READY", 34,
            TextAlignmentOptions.Center, UiTheme.Purchased, FontStyles.Bold);
        UiTheme.TopStretchX(sealText.gameObject, overlay.transform, 0f, 1f, 404f, 44f);

        TMP_Text nameText = UiTheme.Text(overlay.transform, "NameText", "Character", 24,
            TextAlignmentOptions.Center, UiTheme.TextLight, FontStyles.Bold);
        UiTheme.TopStretchX(nameText.gameObject, overlay.transform, 0f, 1f, 452f, 36f);

        return (overlay, nameText, sealText);
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

        EnsurePreviewLayerExists();

        var cameraGo = new GameObject("Main Camera", typeof(Camera));
        cameraGo.tag = "MainCamera";
        Camera mainCamera = cameraGo.GetComponent<Camera>();
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = UiTheme.BgBottom;
        mainCamera.cullingMask = 0; // UI-only screen; overlay canvas renders independently
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 5f;

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Image bg = UiTheme.Panel(canvasGo.transform, "Background", Color.white, UiTheme.BgGradientSprite);
        bg.type = Image.Type.Simple;
        UiTheme.StretchFull((RectTransform)bg.transform);
        bg.raycastTarget = false;

        Image vignette = UiTheme.Panel(canvasGo.transform, "Vignette", Color.white, UiTheme.VignetteSprite);
        vignette.type = Image.Type.Simple;
        UiTheme.StretchFull((RectTransform)vignette.transform);
        vignette.raycastTarget = false;

        TMP_Text title = UiTheme.Text(canvasGo.transform, "Title", "CHOOSE YOUR CHAMPION", 40,
            TextAlignmentOptions.Center, UiTheme.Gold, FontStyles.Bold);
        UiTheme.TopStretchX(title.gameObject, canvasGo.transform, 0f, 1f, 28f, 56f);

        var rowGo = new GameObject("SlotRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(canvasGo.transform, false);
        var rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.offsetMin = new Vector2(48, 48);
        rowRect.offsetMax = new Vector2(-48, -108);
        var rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;
        rowLayout.spacing = 32;

        var slotUIs = new PlayerSlotUI[4];
        for (int i = 0; i < 4; i++)
        {
            var slotInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerSlotPrefab, rowGo.transform);
            slotInstance.name = $"PlayerSlot_{i}";
            slotUIs[i] = slotInstance.GetComponent<PlayerSlotUI>();
            WirePreviewStage(slotUIs[i], i);
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

        // Regeneration hazard: this method starts from NewScene(EmptyScene), which would drop
        // any previously-wired SkillTreePanel instance. Re-instantiate + re-wire it here so
        // running this generator after "Upgrade Screen" doesn't silently break the reference,
        // regardless of run order.
        WireSkillTreePanel(manager);

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

    static void EnsurePreviewLayerExists()
    {
        if (LayerMask.NameToLayer(PreviewLayerName) < 0)
        {
            Debug.LogError($"CharacterSelectionGenerator: layer '{PreviewLayerName}' not found in TagManager. " +
                "Add it via Edit > Project Settings > Tags and Layers before regenerating.");
        }
    }

    /// <summary>Creates this slot's dedicated preview camera/anchor/light at a world-space offset
    /// (i * 20 on X, so 4 slots never see each other's characters) and wires them onto the
    /// CharacterPreviewStage placeholder built into the prefab instance.</summary>
    static void WirePreviewStage(PlayerSlotUI slotUI, int slotIndex)
    {
        CharacterPreviewStage stage = slotUI.GetComponentInChildren<CharacterPreviewStage>(true);
        if (stage == null)
        {
            Debug.LogError("CharacterSelectionGenerator: PlayerSlot prefab instance has no CharacterPreviewStage.");
            return;
        }

        int previewLayer = LayerMask.NameToLayer(PreviewLayerName);
        float worldX = slotIndex * 20f;

        var anchorGo = new GameObject($"PreviewAnchor_{slotIndex}");
        anchorGo.transform.position = new Vector3(worldX, 0f, 0f);

        var cameraGo = new GameObject($"PreviewCamera_{slotIndex}", typeof(Camera));
        cameraGo.transform.position = new Vector3(worldX, 1.4f, -3.2f);
        cameraGo.transform.LookAt(anchorGo.transform.position + Vector3.up * 1f);
        Camera previewCamera = cameraGo.GetComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.cullingMask = 1 << previewLayer;
        previewCamera.depth = -10;
        previewCamera.fieldOfView = 30f;
        cameraGo.layer = previewLayer;

        var lightGo = new GameObject($"PreviewLight_{slotIndex}", typeof(Light));
        lightGo.transform.position = new Vector3(worldX, 3f, -1f);
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = UiTheme.TextLight;
        light.intensity = 1.1f;
        light.cullingMask = 1 << previewLayer;

        var so = new SerializedObject(stage);
        so.FindProperty("previewCamera").objectReferenceValue = previewCamera;
        so.FindProperty("characterAnchor").objectReferenceValue = anchorGo.transform;
        so.ApplyModifiedProperties();
    }

    static void WireSkillTreePanel(CharacterSelectionManager manager)
    {
        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillTreePanelPath);
        if (panelPrefab == null) return; // Upgrade Screen generator hasn't run yet — nothing to wire.

        var panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab);
        SkillTreeScreenController controller = panelInstance.GetComponent<SkillTreeScreenController>();

        var so = new SerializedObject(manager);
        so.FindProperty("skillTreeScreen").objectReferenceValue = controller;
        so.ApplyModifiedProperties();
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
        var go = new GameObject("StartPrompt", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(420, 64);
        rect.anchoredPosition = new Vector2(0, 24);

        Image pill = go.AddComponent<Image>();
        pill.sprite = UiTheme.PillFillSprite;
        pill.type = Image.Type.Sliced;
        pill.color = UiTheme.Surface;

        TMP_Text text = UiTheme.Text(go.transform, "Text", "Press Start", 30,
            TextAlignmentOptions.Center, UiTheme.Gold, FontStyles.Bold);
        UiTheme.StretchFull((RectTransform)text.transform);

        go.AddComponent<UiPulse>();
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
