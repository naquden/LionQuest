using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the skill-tree upgrade screen (node/link/branch-total prefabs, panel prefab), wires it
/// into the CharacterSelection scene, and authors AssCap's four trees (attributes + 3 skills) onto
/// its CharacterDefinition. Dual entry point per project convention. AgentBatch.cs dispatches
/// "UpgradeScreen" here.
///
/// Layout: one opaque full-screen panel, no tabs — every branch fans outward from a shared root
/// at the bottom of a single scrollable node canvas. Left ~65% is the node canvas, right ~35% is
/// the description/Buy info panel. Text and node sizes stay large — this should read at a glance.
/// </summary>
public static class SkillTreeScreenGenerator
{
    const string UIDir = "Assets/Prefabs/UI";
    const string NodePrefabPath = UIDir + "/SkillTreeNode.prefab";
    const string LinkPrefabPath = UIDir + "/SkillTreeLink.prefab";
    const string BranchTotalPrefabPath = UIDir + "/SkillTreeBranchTotal.prefab";
    const string PanelPrefabPath = UIDir + "/SkillTreePanel.prefab";
    const string SelectionScenePath = "Assets/Scenes/CharacterSelection.unity";

    const string AssCapDefinitionPath = "Assets/Characters/AssassinoCapuchino/AssCap_Definition.asset";

    [MenuItem("LionQuest/Generate/Upgrade Screen")]
    public static void Generate()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogWarning("SkillTreeScreenGenerator: TMP Essentials not imported " +
                "(Window > TextMeshPro > Import TMP Essential Resources). Continuing anyway.");
        }

        EnsureFolder(UIDir);

        GameObject nodePrefab = BuildNodePrefab();
        GameObject linkPrefab = BuildLinkPrefab();
        GameObject branchTotalPrefab = BuildBranchTotalPrefab();
        GameObject panelPrefab = BuildPanelPrefab(nodePrefab, linkPrefab, branchTotalPrefab);

        WireIntoSelectionScene(panelPrefab);
        AuthorAssCapTrees();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("SkillTreeScreenGenerator: done.");
    }

    // ------------------------------------------------------------------
    // SkillTreeNode.prefab — tier frame swaps by SkillNodeUI.Setup(), big and readable
    // ------------------------------------------------------------------

    static GameObject BuildNodePrefab()
    {
        var root = new GameObject("SkillTreeNode", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90, 90);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image background = root.GetComponent<Image>();
        background.sprite = UiTheme.CircleSprite;
        background.type = Image.Type.Simple;
        background.color = UiTheme.Surface;

        Button button = root.GetComponent<Button>();
        button.targetGraphic = background;

        var ringGo = new GameObject("SelectionRing", typeof(RectTransform), typeof(Image));
        ringGo.transform.SetParent(root.transform, false);
        var ringRect = ringGo.GetComponent<RectTransform>();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = new Vector2(-10, -10);
        ringRect.offsetMax = new Vector2(10, 10);
        Image ringImage = ringGo.GetComponent<Image>();
        ringImage.sprite = UiTheme.RingSprite;
        ringImage.type = Image.Type.Simple;
        ringImage.color = UiTheme.SelectedOutline;
        ringImage.raycastTarget = false;
        ringGo.SetActive(false);

        var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(root.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.sizeDelta = new Vector2(160, 40);
        nameRect.anchoredPosition = new Vector2(0, 6);
        var nameText = nameGo.GetComponent<TextMeshProUGUI>();
        nameText.text = "Node";
        nameText.fontSize = 20;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 12;
        nameText.fontSizeMax = 20;
        nameText.alignment = TextAlignmentOptions.Bottom;
        nameText.color = UiTheme.TextLight;
        nameText.font = UiTheme.DefaultFont();
        nameText.raycastTarget = false;

        var rankGo = new GameObject("RankText", typeof(RectTransform), typeof(TextMeshProUGUI));
        rankGo.transform.SetParent(root.transform, false);
        var rankRect = rankGo.GetComponent<RectTransform>();
        rankRect.anchorMin = Vector2.zero;
        rankRect.anchorMax = Vector2.one;
        rankRect.offsetMin = Vector2.zero;
        rankRect.offsetMax = Vector2.zero;
        var rankText = rankGo.GetComponent<TextMeshProUGUI>();
        rankText.text = "0/1";
        rankText.fontSize = 18;
        rankText.fontStyle = FontStyles.Bold;
        rankText.alignment = TextAlignmentOptions.Center;
        rankText.color = UiTheme.TextLight;
        rankText.font = UiTheme.DefaultFont();
        rankText.raycastTarget = false;

        var costGo = new GameObject("CostText", typeof(RectTransform), typeof(TextMeshProUGUI));
        costGo.transform.SetParent(root.transform, false);
        var costRect = costGo.GetComponent<RectTransform>();
        costRect.anchorMin = new Vector2(0f, 0f);
        costRect.anchorMax = new Vector2(1f, 0f);
        costRect.pivot = new Vector2(0.5f, 1f);
        costRect.sizeDelta = new Vector2(160, 30);
        costRect.anchoredPosition = new Vector2(0, -6);
        var costText = costGo.GetComponent<TextMeshProUGUI>();
        costText.text = "0 souls";
        costText.fontSize = 16;
        costText.alignment = TextAlignmentOptions.Top;
        costText.color = UiTheme.Gold;
        costText.font = UiTheme.DefaultFont();
        costText.raycastTarget = false;

        var nodeUi = root.AddComponent<SkillTreeNodeUI>();
        var so = new SerializedObject(nodeUi);
        so.FindProperty("background").objectReferenceValue = background;
        so.FindProperty("selectionRing").objectReferenceValue = ringImage;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("rankText").objectReferenceValue = rankText;
        so.FindProperty("costText").objectReferenceValue = costText;
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("keystoneSprite").objectReferenceValue = UiTheme.SquareSprite;
        so.FindProperty("majorSprite").objectReferenceValue = UiTheme.DiamondSprite;
        so.FindProperty("minorSprite").objectReferenceValue = UiTheme.CircleSprite;
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, NodePrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------
    // SkillTreeLink.prefab — elbow (horizontal + vertical) with a diamond junction
    // ------------------------------------------------------------------

    static GameObject BuildLinkPrefab()
    {
        var root = new GameObject("SkillTreeLink", typeof(RectTransform));
        root.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

        var hGo = new GameObject("Horizontal", typeof(RectTransform), typeof(Image));
        hGo.transform.SetParent(root.transform, false);
        var hRect = hGo.GetComponent<RectTransform>();
        hRect.pivot = new Vector2(0.5f, 0.5f);
        hRect.sizeDelta = new Vector2(10, 6);
        Image hImage = hGo.GetComponent<Image>();
        hImage.color = UiTheme.GoldDim;
        hImage.raycastTarget = false;

        var vGo = new GameObject("Vertical", typeof(RectTransform), typeof(Image));
        vGo.transform.SetParent(root.transform, false);
        var vRect = vGo.GetComponent<RectTransform>();
        vRect.pivot = new Vector2(0.5f, 0.5f);
        vRect.sizeDelta = new Vector2(6, 10);
        Image vImage = vGo.GetComponent<Image>();
        vImage.color = UiTheme.GoldDim;
        vImage.raycastTarget = false;

        var jGo = new GameObject("Junction", typeof(RectTransform), typeof(Image));
        jGo.transform.SetParent(root.transform, false);
        var jRect = jGo.GetComponent<RectTransform>();
        jRect.pivot = new Vector2(0.5f, 0.5f);
        jRect.sizeDelta = new Vector2(16, 16);
        Image jImage = jGo.GetComponent<Image>();
        jImage.sprite = UiTheme.DiamondSprite;
        jImage.type = Image.Type.Simple;
        jImage.color = UiTheme.GoldDim;
        jImage.raycastTarget = false;

        var link = root.AddComponent<SkillTreeLinkUI>();
        var so = new SerializedObject(link);
        so.FindProperty("horizontalSegment").objectReferenceValue = hRect;
        so.FindProperty("verticalSegment").objectReferenceValue = vRect;
        so.FindProperty("horizontalImage").objectReferenceValue = hImage;
        so.FindProperty("verticalImage").objectReferenceValue = vImage;
        so.FindProperty("junctionImage").objectReferenceValue = jImage;
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, LinkPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------
    // SkillTreeBranchTotal.prefab — one label per branch in the header row
    // ------------------------------------------------------------------

    static GameObject BuildBranchTotalPrefab()
    {
        var go = new GameObject("BranchTotal", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(260, 40);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = "BRANCH 0";
        text.fontSize = 24;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = UiTheme.TextLight;
        text.font = UiTheme.DefaultFont();
        text.raycastTarget = false;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, BranchTotalPrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------
    // Generic button helper — used by Buy, Back
    // ------------------------------------------------------------------

    static GameObject CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        if (parent != null) go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = go.GetComponent<Image>();
        image.sprite = UiTheme.RoundedPanelSprite;
        image.type = Image.Type.Sliced;
        image.color = UiTheme.Surface;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = UiTheme.Text(go.transform, "Text", label, 24, TextAlignmentOptions.Center, UiTheme.TextLight, FontStyles.Bold);
        StretchFull((RectTransform)text.transform);

        return go;
    }

    // ------------------------------------------------------------------
    // SkillTreePanel.prefab — full-screen, fully opaque, large text throughout
    // ------------------------------------------------------------------

    static GameObject BuildPanelPrefab(GameObject nodePrefab, GameObject linkPrefab, GameObject branchTotalPrefab)
    {
        var root = new GameObject("SkillTreePanel", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // above the character-select carousel canvas

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Fully opaque — this is a separate screen, not a see-through overlay.
        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(root.transform, false);
        StretchFull((RectTransform)bgGo.transform);
        Image bgImage = bgGo.GetComponent<Image>();
        bgImage.sprite = UiTheme.BgGradientSprite;
        bgImage.type = Image.Type.Simple;
        bgImage.color = Color.white;

        var vignetteGo = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        vignetteGo.transform.SetParent(bgGo.transform, false);
        StretchFull((RectTransform)vignetteGo.transform);
        Image vignetteImage = vignetteGo.GetComponent<Image>();
        vignetteImage.sprite = UiTheme.VignetteSprite;
        vignetteImage.type = Image.Type.Simple;
        vignetteImage.raycastTarget = false;

        // --- Header: character name (left) + souls (right). ---
        var headerGo = new GameObject("Header", typeof(RectTransform));
        headerGo.transform.SetParent(bgGo.transform, false);
        var headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 90f);
        headerRect.anchoredPosition = Vector2.zero;

        var nameGo = new GameObject("CharacterNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(headerGo.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.offsetMin = new Vector2(50, 0);
        nameRect.offsetMax = Vector2.zero;
        var nameText = nameGo.GetComponent<TextMeshProUGUI>();
        nameText.text = "Character";
        nameText.fontSize = 46;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.color = UiTheme.TextLight;
        nameText.fontStyle = FontStyles.Bold;
        nameText.font = UiTheme.DefaultFont();

        var soulsGo = new GameObject("SoulsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        soulsGo.transform.SetParent(headerGo.transform, false);
        var soulsRect = soulsGo.GetComponent<RectTransform>();
        soulsRect.anchorMin = new Vector2(0.5f, 0f);
        soulsRect.anchorMax = new Vector2(1f, 1f);
        soulsRect.offsetMin = Vector2.zero;
        soulsRect.offsetMax = new Vector2(-50, 0);
        var soulsText = soulsGo.GetComponent<TextMeshProUGUI>();
        soulsText.text = "Souls: 0";
        soulsText.fontSize = 40;
        soulsText.alignment = TextAlignmentOptions.MidlineRight;
        soulsText.color = UiTheme.Gold;
        soulsText.fontStyle = FontStyles.Bold;
        soulsText.font = UiTheme.DefaultFont();

        // --- Branch totals row: one label per branch, always visible. ---
        var totalsRowGo = new GameObject("BranchTotalsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        totalsRowGo.transform.SetParent(bgGo.transform, false);
        var totalsRowRect = totalsRowGo.GetComponent<RectTransform>();
        totalsRowRect.anchorMin = new Vector2(0f, 1f);
        totalsRowRect.anchorMax = new Vector2(1f, 1f);
        totalsRowRect.pivot = new Vector2(0.5f, 1f);
        totalsRowRect.sizeDelta = new Vector2(0f, 50f);
        totalsRowRect.anchoredPosition = new Vector2(0f, -90f);
        var totalsRowLayout = totalsRowGo.GetComponent<HorizontalLayoutGroup>();
        totalsRowLayout.childAlignment = TextAnchor.MiddleLeft;
        totalsRowLayout.spacing = 40;
        totalsRowLayout.padding = new RectOffset(50, 50, 0, 0);
        totalsRowLayout.childForceExpandWidth = false;
        totalsRowLayout.childForceExpandHeight = false;

        // --- Body: node canvas (left, scrollable) + description/Buy panel (right). ---
        var bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(bgGo.transform, false);
        var bodyRect = bodyGo.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0.1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(40, 0);
        bodyRect.offsetMax = new Vector2(-40, -150);

        // Node scroll area — left ~65% of the body.
        var scrollGo = new GameObject("ScrollRect", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
        scrollGo.transform.SetParent(bodyGo.transform, false);
        var scrollRectT = scrollGo.GetComponent<RectTransform>();
        scrollRectT.anchorMin = new Vector2(0f, 0f);
        scrollRectT.anchorMax = new Vector2(0.65f, 1f);
        scrollRectT.offsetMin = Vector2.zero;
        scrollRectT.offsetMax = new Vector2(-20, 0);
        Image scrollBg = scrollGo.GetComponent<Image>();
        scrollBg.sprite = UiTheme.RoundedPanelSprite;
        scrollBg.type = Image.Type.Sliced;
        scrollBg.color = UiTheme.SurfaceInset;

        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        StretchFull((RectTransform)viewportGo.transform);

        // NodeContainer is top-left anchored — SkillTreeScreenController fits its size to actual
        // content bounds and positions nodes/links in positive-x, negative-y space accordingly.
        // (Prior bug: node prefab anchor (0.5,0.5) vs a top-left-assumed layout put every node
        // far outside the viewport with no visible error — verify anchoring matches on any change.)
        var contentGo = new GameObject("NodeContainer", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.sizeDelta = new Vector2(1600, 900);
        contentRect.anchoredPosition = Vector2.zero;

        var linkContainerGo = new GameObject("LinkContainer", typeof(RectTransform));
        linkContainerGo.transform.SetParent(contentGo.transform, false);
        StretchFull((RectTransform)linkContainerGo.transform);
        linkContainerGo.transform.SetAsFirstSibling(); // links render behind nodes

        var scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.viewport = (RectTransform)viewportGo.transform;
        scrollRect.content = contentRect;
        scrollRect.horizontal = true;
        scrollRect.vertical = true;

        // Description + Buy panel — right ~35% of the body.
        var infoGo = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image));
        infoGo.transform.SetParent(bodyGo.transform, false);
        var infoRect = infoGo.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.67f, 0f);
        infoRect.anchorMax = new Vector2(1f, 1f);
        infoRect.offsetMin = Vector2.zero;
        infoRect.offsetMax = Vector2.zero;
        Image infoBg = infoGo.GetComponent<Image>();
        infoBg.sprite = UiTheme.RoundedPanelSprite;
        infoBg.type = Image.Type.Sliced;
        infoBg.color = UiTheme.Surface;

        var descGo = new GameObject("DescriptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        descGo.transform.SetParent(infoGo.transform, false);
        var descRect = descGo.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0.25f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.offsetMin = new Vector2(30, 0);
        descRect.offsetMax = new Vector2(-30, -30);
        var descText = descGo.GetComponent<TextMeshProUGUI>();
        descText.text = "Select a node to see what it does.";
        descText.fontSize = 28;
        descText.alignment = TextAlignmentOptions.TopLeft;
        descText.color = UiTheme.TextLight;
        descText.font = UiTheme.DefaultFont();
        descText.enableWordWrapping = true;

        GameObject buyGo = CreateButton(infoGo.transform, "BuyButton", "Buy",
            new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0f), new Vector2(360, 90), Vector2.zero);
        Button buyButton = buyGo.GetComponent<Button>();
        TMP_Text buyButtonText = buyGo.GetComponentInChildren<TMP_Text>();
        buyButtonText.fontSize = 30;

        // --- Back button, bottom-left, always reachable. ---
        GameObject backButton = CreateButton(bgGo.transform, "BackButton", "Back",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(240, 80), new Vector2(40, 40));

        var panel = root.AddComponent<SkillTreeScreenController>();
        var so = new SerializedObject(panel);
        so.FindProperty("panelRoot").objectReferenceValue = root;
        so.FindProperty("characterNameText").objectReferenceValue = nameText;
        so.FindProperty("soulsText").objectReferenceValue = soulsText;
        so.FindProperty("branchTotalsContainer").objectReferenceValue = totalsRowGo.GetComponent<RectTransform>();
        so.FindProperty("branchTotalPrefab").objectReferenceValue = branchTotalPrefab;
        so.FindProperty("nodeContainer").objectReferenceValue = contentRect;
        so.FindProperty("nodePrefab").objectReferenceValue = nodePrefab;
        so.FindProperty("linkContainer").objectReferenceValue = linkContainerGo.GetComponent<RectTransform>();
        so.FindProperty("linkPrefab").objectReferenceValue = linkPrefab;
        so.FindProperty("descriptionText").objectReferenceValue = descText;
        so.FindProperty("buyButton").objectReferenceValue = buyButton;
        so.FindProperty("buyButtonText").objectReferenceValue = buyButtonText;
        so.ApplyModifiedProperties();

        Button backBtn = backButton.GetComponent<Button>();
        // No persisted onClick — CharacterSelectionManager drives Close() via input, but the mouse
        // path needs a listener too. Wired here since it's a fixed, non-per-instance callback.
        UnityEditor.Events.UnityEventTools.AddPersistentListener(backBtn.onClick, panel.Close);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
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

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    // ------------------------------------------------------------------
    // CharacterSelection.unity wiring — idempotent
    // ------------------------------------------------------------------

    static void WireIntoSelectionScene(GameObject panelPrefab)
    {
        var scene = EditorSceneManager.OpenScene(SelectionScenePath, OpenSceneMode.Single);

        SkillTreeScreenController existing = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            existing = rootGo.GetComponentInChildren<SkillTreeScreenController>(true);
            if (existing != null) break;
        }
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        var panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, scene);
        SkillTreeScreenController controller = panelInstance.GetComponent<SkillTreeScreenController>();

        CharacterSelectionManager manager = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            manager = rootGo.GetComponentInChildren<CharacterSelectionManager>(true);
            if (manager != null) break;
        }

        if (manager == null)
        {
            Debug.LogError("SkillTreeScreenGenerator: no CharacterSelectionManager found in CharacterSelection scene — run 'LionQuest/Generate/Character Selection' first.");
        }
        else
        {
            var so = new SerializedObject(manager);
            so.FindProperty("skillTreeScreen").objectReferenceValue = controller;
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ------------------------------------------------------------------
    // AssCap starting skill trees — plain-language descriptions so the
    // in-panel DescriptionText explains each node without extra lookups.
    // ------------------------------------------------------------------

    static void AuthorAssCapTrees()
    {
        var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssCapDefinitionPath);
        if (def == null)
        {
            Debug.LogError($"SkillTreeScreenGenerator: no CharacterDefinition at {AssCapDefinitionPath} — run 'LionQuest/Generate/Character Selection' first.");
            return;
        }

        def.skillTrees = new[] { BuildAttributesTree(), BuildWhirlwindTree(), BuildShurikenTree(), BuildBattleCryTree() };

        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
    }

    static SkillNodeDefinition Node(string id, string name, string description, SkillEffect effect, SkillEffectOp op,
        float value, int cost, int maxRank, SkillNodeTier tier, int gx, int gy, params string[] prereqs)
    {
        return new SkillNodeDefinition
        {
            nodeId = id,
            displayName = name,
            description = description,
            effect = effect,
            op = op,
            value = value,
            cost = cost,
            maxRank = maxRank,
            tier = tier,
            prereqNodeIds = prereqs,
            gridPos = new Vector2Int(gx, gy),
        };
    }

    static SkillDisplayBase DisplayBase(SkillEffect effect, float baseValue, string unit) =>
        new SkillDisplayBase { effect = effect, baseValue = baseValue, unit = unit };

    static SkillTreeDefinition BuildAttributesTree()
    {
        return new SkillTreeDefinition
        {
            skillId = "attributes",
            displayName = "Attributes",
            skillSlot = 0,
            branchColor = new Color(0.4f, 0.8f, 0.35f),
            target = SkillEffectTarget.Attribute,
            nodes = new[]
            {
                Node("root", "Attributes", "Spend souls on Strength, Health, and Speed.",
                    SkillEffect.Unlock, SkillEffectOp.Add, 0f, 0, 1, SkillNodeTier.Keystone, 0, 0),

                Node("strength_1", "+1 Strength", "Increases damage dealt.",
                    SkillEffect.Strength, SkillEffectOp.Add, 1f, 10, 5, SkillNodeTier.Minor, -2, 1, "root"),
                Node("strength_2", "+1 Strength (Major)", "A bigger jump in damage dealt.",
                    SkillEffect.Strength, SkillEffectOp.Add, 1f, 25, 3, SkillNodeTier.Major, -2, 2, "strength_1"),

                Node("health_1", "+1 Health", "Increases max health.",
                    SkillEffect.Health, SkillEffectOp.Add, 1f, 10, 5, SkillNodeTier.Minor, 0, 1, "root"),
                Node("health_2", "+1 Health (Major)", "A bigger jump in max health.",
                    SkillEffect.Health, SkillEffectOp.Add, 1f, 25, 3, SkillNodeTier.Major, 0, 2, "health_1"),

                Node("speed_1", "+1 Speed", "Increases move speed.",
                    SkillEffect.Speed, SkillEffectOp.Add, 1f, 10, 5, SkillNodeTier.Minor, 2, 1, "root"),
                Node("speed_2", "+1 Speed (Major)", "A bigger jump in move speed.",
                    SkillEffect.Speed, SkillEffectOp.Add, 1f, 25, 3, SkillNodeTier.Major, 2, 2, "speed_1"),
            },
        };
    }

    static SkillTreeDefinition BuildWhirlwindTree()
    {
        var skill1 = AssetDatabase.LoadAssetAtPath<AttackData>("Assets/Characters/AssassinoCapuchino/Skill1Attack.asset");
        float baseCooldown = skill1 != null ? skill1.cooldown : 10f;

        return new SkillTreeDefinition
        {
            skillId = "whirlwind",
            displayName = "Whirlwind",
            skillSlot = 1,
            branchColor = new Color(0.35f, 0.55f, 0.9f),
            displayBases = new[] { DisplayBase(SkillEffect.Cooldown, baseCooldown, "s") },
            nodes = new[]
            {
                Node("unlock", "Unlock", "Lets you use Whirlwind, a spinning attack that hits everything around you.",
                    SkillEffect.Unlock, SkillEffectOp.Add, 1f, 20, 1, SkillNodeTier.Keystone, 0, 0),

                Node("damage_1", "Damage +0.5x", "Whirlwind hits harder.",
                    SkillEffect.Damage, SkillEffectOp.Add, 0.5f, 15, 1, SkillNodeTier.Minor, -1, 1, "unlock"),
                Node("damage_2", "Damage +1.0x", "Whirlwind hits even harder.",
                    SkillEffect.Damage, SkillEffectOp.Add, 1.0f, 35, 1, SkillNodeTier.Major, -1, 2, "damage_1"),

                Node("area_1", "Area +1", "Whirlwind hits enemies farther away from you.",
                    SkillEffect.Area, SkillEffectOp.Add, 1f, 20, 1, SkillNodeTier.Minor, 0, 1, "unlock"),

                Node("cooldown_2s", "Cooldown -2s", "Whirlwind is ready to use 2 seconds sooner.",
                    SkillEffect.Cooldown, SkillEffectOp.Add, -2f, 15, 1, SkillNodeTier.Minor, 1, 1, "unlock"),
                Node("cooldown_3s", "Cooldown -3s", "Whirlwind is ready to use 3 more seconds sooner.",
                    SkillEffect.Cooldown, SkillEffectOp.Add, -3f, 40, 1, SkillNodeTier.Major, 1, 2, "cooldown_2s"),

                // Converge: either damage or area path opens the outer keystone-adjacent node.
                Node("mastery", "Whirlwind Mastery", "A final push of damage and area, from mastering either path.",
                    SkillEffect.Damage, SkillEffectOp.Add, 0.5f, 50, 1, SkillNodeTier.Major, 0, 3, "damage_2", "area_1"),
            },
        };
    }

    static SkillTreeDefinition BuildShurikenTree()
    {
        var shuriken = AssetDatabase.LoadAssetAtPath<ProjectileSkillData>("Assets/Characters/AssassinoCapuchino/ShurikenProjectile.asset");
        float baseCooldown = shuriken != null ? shuriken.cooldown : 2f;
        float baseDamage = shuriken != null ? shuriken.damage : 25f;

        return new SkillTreeDefinition
        {
            skillId = "shuriken",
            displayName = "Shuriken",
            skillSlot = 2,
            branchColor = new Color(0.65f, 0.35f, 0.85f),
            displayBases = new[]
            {
                DisplayBase(SkillEffect.Cooldown, baseCooldown, "s"),
                DisplayBase(SkillEffect.Damage, baseDamage, ""),
            },
            nodes = new[]
            {
                Node("unlock", "Unlock", "Lets you use Shuriken, a ranged throw that hits enemies from a distance.",
                    SkillEffect.Unlock, SkillEffectOp.Add, 1f, 20, 1, SkillNodeTier.Keystone, 0, 0),

                Node("damage_1", "Damage +5", "Each shuriken hits harder.",
                    SkillEffect.Damage, SkillEffectOp.Add, 5f, 15, 1, SkillNodeTier.Minor, -1, 1, "unlock"),
                Node("damage_2", "Damage +5", "Each shuriken hits even harder.",
                    SkillEffect.Damage, SkillEffectOp.Add, 5f, 30, 1, SkillNodeTier.Major, -1, 2, "damage_1"),

                Node("count_1", "Projectile +1", "Throw one extra shuriken per use.",
                    SkillEffect.ProjectileCount, SkillEffectOp.Add, 1f, 30, 1, SkillNodeTier.Major, 1, 1, "unlock"),

                Node("cooldown_2s", "Cooldown -2s", "Shuriken is ready to use 2 seconds sooner.",
                    SkillEffect.Cooldown, SkillEffectOp.Add, -2f, 15, 1, SkillNodeTier.Minor, 0, 1, "unlock"),

                // Converge: either the damage chain or the count node opens this.
                Node("mastery", "Shuriken Mastery", "A final push of damage, from mastering either path.",
                    SkillEffect.Damage, SkillEffectOp.Add, 10f, 45, 1, SkillNodeTier.Major, 0, 2, "damage_2", "count_1"),
            },
        };
    }

    static SkillTreeDefinition BuildBattleCryTree()
    {
        var battleCry = AssetDatabase.LoadAssetAtPath<BattleCrySkillData>("Assets/Characters/AssassinoCapuchino/BattleCry.asset");
        float baseCooldown = battleCry != null ? battleCry.cooldown : 10f;
        float baseDuration = battleCry != null ? battleCry.duration : 5f;

        return new SkillTreeDefinition
        {
            skillId = "battlecry",
            displayName = "Battle Cry",
            skillSlot = 3,
            branchColor = new Color(0.85f, 0.3f, 0.25f),
            displayBases = new[]
            {
                DisplayBase(SkillEffect.Cooldown, baseCooldown, "s"),
                DisplayBase(SkillEffect.Duration, baseDuration, "s"),
            },
            nodes = new[]
            {
                Node("unlock", "Unlock", "Lets you use Battle Cry, a shout that makes you move faster for a few seconds.",
                    SkillEffect.Unlock, SkillEffectOp.Add, 1f, 20, 1, SkillNodeTier.Keystone, 0, 0),

                Node("magnitude_1", "Magnitude +0.25x", "Battle Cry makes you move even faster.",
                    SkillEffect.Magnitude, SkillEffectOp.Add, 0.25f, 15, 1, SkillNodeTier.Minor, -1, 1, "unlock"),
                Node("magnitude_2", "Magnitude +0.5x", "Battle Cry makes you move a lot faster.",
                    SkillEffect.Magnitude, SkillEffectOp.Add, 0.5f, 35, 1, SkillNodeTier.Major, -1, 2, "magnitude_1"),

                Node("duration_1", "Duration +1s", "Battle Cry's speed boost lasts 1 second longer.",
                    SkillEffect.Duration, SkillEffectOp.Add, 1f, 20, 1, SkillNodeTier.Minor, 1, 1, "unlock"),
                Node("duration_2", "Duration +2s", "Battle Cry's speed boost lasts 2 more seconds.",
                    SkillEffect.Duration, SkillEffectOp.Add, 2f, 40, 1, SkillNodeTier.Major, 1, 2, "duration_1"),

                Node("cooldown_2s", "Cooldown -2s", "Battle Cry is ready to use 2 seconds sooner.",
                    SkillEffect.Cooldown, SkillEffectOp.Add, -2f, 15, 1, SkillNodeTier.Minor, 0, 1, "unlock"),

                // Converge: either magnitude or duration chain opens this outer node.
                Node("mastery", "Battle Cry Mastery", "A final push of speed and duration, from mastering either path.",
                    SkillEffect.Magnitude, SkillEffectOp.Add, 0.25f, 50, 1, SkillNodeTier.Major, 0, 3, "magnitude_2", "duration_2"),
            },
        };
    }
}
