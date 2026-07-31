using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the upgrade screen (row/tab prefabs, panel prefab), wires it into the CharacterSelection
/// scene, and authors each playable character's four upgrade groups (Body + 3 skills) onto its
/// CharacterDefinition.
/// Dual entry point per project convention. AgentBatch.cs dispatches "UpgradeScreen" here.
///
/// Layout is a plain shop, not a graph: a left column of category tabs and a right column of upgrade
/// rows, each row carrying its own name, plain sentence, rank pips, value change and price. Nothing
/// to pan, no links, no legend — a non-gamer should be able to read one row and know what it does.
/// </summary>
public static class SkillTreeScreenGenerator
{
    const string UIDir = "Assets/Prefabs/UI";
    const string RowPrefabPath = UIDir + "/SkillTreeNode.prefab";
    const string TabPrefabPath = UIDir + "/SkillTreeLink.prefab";
    const string PanelPrefabPath = UIDir + "/SkillTreePanel.prefab";
    const string SelectionScenePath = "Assets/Scenes/CharacterSelection.unity";

    const string AssCapDir = "Assets/Characters/AssassinoCapuchino";
    const string AssCapDefinitionPath = AssCapDir + "/AssCap_Definition.asset";
    const string Char67Dir = "Assets/Characters/67";
    const string Char67DefinitionPath = Char67Dir + "/67_Definition.asset";

    const float RowHeight = 118f;
    const int MaxRankPips = 6;

    [MenuItem("LionQuest/Generate/Upgrade Screen")]
    public static void Generate()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogWarning("SkillTreeScreenGenerator: TMP Essentials not imported " +
                "(Window > TextMeshPro > Import TMP Essential Resources). Continuing anyway.");
        }

        UiTheme.EnsureFolder(UIDir);

        // The old graph layout's per-branch total label prefab has no place in a tabbed shop.
        AssetDatabase.DeleteAsset(UIDir + "/SkillTreeBranchTotal.prefab");

        GameObject rowPrefab = BuildRowPrefab();
        GameObject tabPrefab = BuildTabPrefab();
        GameObject panelPrefab = BuildPanelPrefab(rowPrefab, tabPrefab);

        WireIntoSelectionScene(panelPrefab);
        AuthorTrees(AssCapDefinitionPath, AssCapDir, BuildShurikenTree);
        AuthorTrees(Char67DefinitionPath, Char67Dir, BuildLaserTree);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("SkillTreeScreenGenerator: done.");
    }

    // ------------------------------------------------------------------
    // SkillTreeNode.prefab — one full-width upgrade row
    // ------------------------------------------------------------------

    static GameObject BuildRowPrefab()
    {
        var root = new GameObject("SkillTreeRow", typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(LayoutElement));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, RowHeight);

        Image background = root.GetComponent<Image>();
        background.sprite = UiTheme.RoundedPanelSprite;
        background.type = Image.Type.Sliced;
        background.color = UiPalette.SurfaceInset;

        var layout = root.GetComponent<LayoutElement>();
        layout.minHeight = RowHeight;
        layout.preferredHeight = RowHeight;

        Button button = root.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        // Selection outline sits slightly outside the row so a highlighted row reads instantly.
        var ringGo = new GameObject("SelectionRing", typeof(RectTransform), typeof(Image));
        ringGo.transform.SetParent(root.transform, false);
        var ringRect = ringGo.GetComponent<RectTransform>();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = new Vector2(-5, -5);
        ringRect.offsetMax = new Vector2(5, 5);
        Image ringImage = ringGo.GetComponent<Image>();
        ringImage.sprite = UiTheme.RoundedOutlineSprite;
        ringImage.type = Image.Type.Sliced;
        ringImage.color = UiPalette.Gold;
        ringImage.raycastTarget = false;
        ringGo.SetActive(false);

        // Category colour stripe down the left edge — the only decorative colour on a row.
        var stripeGo = new GameObject("AccentStripe", typeof(RectTransform), typeof(Image));
        stripeGo.transform.SetParent(root.transform, false);
        var stripeRect = stripeGo.GetComponent<RectTransform>();
        stripeRect.anchorMin = new Vector2(0f, 0f);
        stripeRect.anchorMax = new Vector2(0f, 1f);
        stripeRect.pivot = new Vector2(0f, 0.5f);
        stripeRect.sizeDelta = new Vector2(10f, -24f);
        stripeRect.anchoredPosition = new Vector2(14f, 0f);
        Image stripeImage = stripeGo.GetComponent<Image>();
        stripeImage.sprite = UiTheme.PillFillSprite;
        stripeImage.type = Image.Type.Sliced;
        stripeImage.raycastTarget = false;

        TMP_Text nameText = UiTheme.Text(root.transform, "NameText", "Upgrade", 34,
            TextAlignmentOptions.BottomLeft, UiPalette.TextLight, FontStyles.Bold);
        var nameRect = (RectTransform)nameText.transform;
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(0.55f, 1f);
        nameRect.offsetMin = new Vector2(40, 0);
        nameRect.offsetMax = new Vector2(0, -12);

        TMP_Text descText = UiTheme.Text(root.transform, "DescriptionText", "What it does.", 24,
            TextAlignmentOptions.TopLeft, UiPalette.TextMuted);
        var descRect = (RectTransform)descText.transform;
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(0.55f, 0.5f);
        descRect.offsetMin = new Vector2(40, 10);
        descRect.offsetMax = Vector2.zero;
        descText.enableWordWrapping = true;

        TMP_Text changeText = UiTheme.Text(root.transform, "ChangeText", "10s → 8s", 26,
            TextAlignmentOptions.Right, UiPalette.TextLight, FontStyles.Bold);
        var changeRect = (RectTransform)changeText.transform;
        changeRect.anchorMin = new Vector2(0.75f, 0.48f);
        changeRect.anchorMax = new Vector2(1f, 1f);
        changeRect.offsetMin = Vector2.zero;
        changeRect.offsetMax = new Vector2(-32, -12);

        TMP_Text priceText = UiTheme.Text(root.transform, "PriceText", "10 souls", 26,
            TextAlignmentOptions.Right, UiPalette.Gold, FontStyles.Bold);
        var priceRect = (RectTransform)priceText.transform;
        priceRect.anchorMin = new Vector2(0.75f, 0f);
        priceRect.anchorMax = new Vector2(1f, 0.48f);
        priceRect.offsetMin = new Vector2(0, 10);
        priceRect.offsetMax = new Vector2(-32, 0);

        // Rank pips: a fixed strip the controller shows/hides per row, so "how many left to buy"
        // is countable at a glance instead of parsed from "3/5" text.
        var pipsGo = new GameObject("RankPips", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        pipsGo.transform.SetParent(root.transform, false);
        var pipsRect = pipsGo.GetComponent<RectTransform>();
        // Own column between the name/description block and the price block. Anchored positively
        // from 0.55 — an earlier version anchored at 0.62 and pulled itself back by -150px, which
        // dragged the pips on top of the description text.
        pipsRect.anchorMin = new Vector2(0.55f, 0.5f);
        pipsRect.anchorMax = new Vector2(0.55f, 0.5f);
        pipsRect.pivot = new Vector2(0f, 0.5f);
        pipsRect.sizeDelta = new Vector2(180f, 22f);
        pipsRect.anchoredPosition = new Vector2(12f, 0f);
        var pipsLayout = pipsGo.GetComponent<HorizontalLayoutGroup>();
        pipsLayout.spacing = 8f;
        pipsLayout.childAlignment = TextAnchor.MiddleLeft;
        pipsLayout.childForceExpandWidth = false;
        pipsLayout.childForceExpandHeight = false;

        var pips = new Image[MaxRankPips];
        for (int i = 0; i < MaxRankPips; i++)
        {
            var pipGo = new GameObject($"Pip{i}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            pipGo.transform.SetParent(pipsGo.transform, false);
            pipGo.GetComponent<RectTransform>().sizeDelta = new Vector2(20f, 20f);
            var pipLayout = pipGo.GetComponent<LayoutElement>();
            pipLayout.minWidth = 20f;
            pipLayout.minHeight = 20f;
            Image pipImage = pipGo.GetComponent<Image>();
            pipImage.sprite = UiTheme.CircleSprite;
            pipImage.color = UiPalette.PipEmpty;
            pipImage.raycastTarget = false;
            pips[i] = pipImage;
        }

        var rowUi = root.AddComponent<SkillTreeNodeUI>();
        var so = new SerializedObject(rowUi);
        so.FindProperty("background").objectReferenceValue = background;
        so.FindProperty("selectionRing").objectReferenceValue = ringImage;
        so.FindProperty("accentStripe").objectReferenceValue = stripeImage;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("descriptionText").objectReferenceValue = descText;
        so.FindProperty("changeText").objectReferenceValue = changeText;
        so.FindProperty("priceText").objectReferenceValue = priceText;
        so.FindProperty("button").objectReferenceValue = button;
        UiTheme.AssignArray(so, "rankPips", pips);
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------
    // SkillTreeLink.prefab — one category tab (type name kept; see SkillTreeLinkUI)
    // ------------------------------------------------------------------

    static GameObject BuildTabPrefab()
    {
        var root = new GameObject("SkillTreeCategoryTab", typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(LayoutElement));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 104f);

        Image background = root.GetComponent<Image>();
        background.sprite = UiTheme.RoundedPanelSprite;
        background.type = Image.Type.Sliced;
        background.color = UiPalette.SurfaceInset;

        var layout = root.GetComponent<LayoutElement>();
        layout.minHeight = 104f;
        layout.preferredHeight = 104f;

        Button button = root.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        // Focus outline, matching the row prefab's — shown only while the cursor is in the tab list.
        var ringGo = new GameObject("SelectionRing", typeof(RectTransform), typeof(Image));
        ringGo.transform.SetParent(root.transform, false);
        var ringRect = ringGo.GetComponent<RectTransform>();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = new Vector2(-5, -5);
        ringRect.offsetMax = new Vector2(5, 5);
        Image ringImage = ringGo.GetComponent<Image>();
        ringImage.sprite = UiTheme.RoundedOutlineSprite;
        ringImage.type = Image.Type.Sliced;
        ringImage.color = UiPalette.Gold;
        ringImage.raycastTarget = false;
        ringGo.SetActive(false);

        var barGo = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(root.transform, false);
        var barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.sizeDelta = new Vector2(12f, -20f);
        barRect.anchoredPosition = new Vector2(12f, 0f);
        Image barImage = barGo.GetComponent<Image>();
        barImage.sprite = UiTheme.PillFillSprite;
        barImage.type = Image.Type.Sliced;
        barImage.raycastTarget = false;

        TMP_Text titleText = UiTheme.Text(root.transform, "TitleText", "Group", 32,
            TextAlignmentOptions.BottomLeft, UiPalette.TextLight, FontStyles.Bold);
        var titleRect = (RectTransform)titleText.transform;
        titleRect.anchorMin = new Vector2(0f, 0.45f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(38, 0);
        titleRect.offsetMax = new Vector2(-18, -10);

        TMP_Text subtitleText = UiTheme.Text(root.transform, "SubtitleText", "0 / 0 bought", 22,
            TextAlignmentOptions.TopLeft, UiPalette.TextMuted);
        var subtitleRect = (RectTransform)subtitleText.transform;
        subtitleRect.anchorMin = new Vector2(0f, 0f);
        subtitleRect.anchorMax = new Vector2(1f, 0.45f);
        subtitleRect.offsetMin = new Vector2(38, 10);
        subtitleRect.offsetMax = new Vector2(-18, 0);

        var tabUi = root.AddComponent<SkillTreeLinkUI>();
        var so = new SerializedObject(tabUi);
        so.FindProperty("background").objectReferenceValue = background;
        so.FindProperty("selectionRing").objectReferenceValue = ringImage;
        so.FindProperty("accentBar").objectReferenceValue = barImage;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("subtitleText").objectReferenceValue = subtitleText;
        so.FindProperty("button").objectReferenceValue = button;
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TabPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------
    // SkillTreePanel.prefab — opaque full screen: title bar, tabs left, rows right, one Buy button
    // ------------------------------------------------------------------

    static GameObject BuildPanelPrefab(GameObject rowPrefab, GameObject tabPrefab)
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

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(root.transform, false);
        UiTheme.StretchFull((RectTransform)bgGo.transform);
        Image bgImage = bgGo.GetComponent<Image>();
        bgImage.sprite = UiTheme.BgGradientSprite;
        bgImage.type = Image.Type.Simple;
        bgImage.color = Color.white;

        // --- Title bar: what screen this is (left) + the one number that matters (right).
        // Anchored explicitly rather than via TopStretchX + offset patching: setting offsetMin
        // after a top-anchored TopStretchX pins the box's BOTTOM edge to the parent's top edge,
        // which pushed the character name entirely off the top of the screen. ---
        TMP_Text titleText = UiTheme.Text(bgGo.transform, "ScreenTitleText", "Spend Souls", 30,
            TextAlignmentOptions.Left, UiPalette.TextMuted, FontStyles.Bold);
        TopBand((RectTransform)titleText.transform, 0f, 0.5f, 30f, 40f, 60f, 0f);

        TMP_Text nameText = UiTheme.Text(bgGo.transform, "CharacterNameText", "Character", 56,
            TextAlignmentOptions.Left, UiPalette.TextLight, FontStyles.Bold);
        TopBand((RectTransform)nameText.transform, 0f, 0.55f, 72f, 70f, 60f, 0f);

        TMP_Text soulsText = UiTheme.Text(bgGo.transform, "SoulsText", "0 souls to spend", 44,
            TextAlignmentOptions.Right, UiPalette.Gold, FontStyles.Bold);
        TopBand((RectTransform)soulsText.transform, 0.5f, 1f, 62f, 62f, 0f, 60f);

        // Always-visible current stats — the effect of a purchase without reading any row.
        // Sits in its own band below the souls total so the two never overlap.
        TMP_Text statSummaryText = UiTheme.Text(bgGo.transform, "StatSummaryText",
            "Damage +0%     Health 100     Move speed +0%", 26,
            TextAlignmentOptions.Right, UiPalette.TextMuted);
        TopBand((RectTransform)statSummaryText.transform, 0.35f, 1f, 128f, 36f, 0f, 60f);

        // --- Body: tabs column + rows column. Absolute bands, no fractional fights. ---
        var bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(bgGo.transform, false);
        var bodyRect = bodyGo.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(60, 190);
        bodyRect.offsetMax = new Vector2(-60, -184);

        var tabsGo = new GameObject("CategoryColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
        tabsGo.transform.SetParent(bodyGo.transform, false);
        var tabsRect = tabsGo.GetComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0f, 0f);
        tabsRect.anchorMax = new Vector2(0f, 1f);
        tabsRect.pivot = new Vector2(0f, 1f);
        tabsRect.sizeDelta = new Vector2(420f, 0f);
        tabsRect.anchoredPosition = Vector2.zero;
        var tabsLayout = tabsGo.GetComponent<VerticalLayoutGroup>();
        tabsLayout.spacing = 14f;
        tabsLayout.childAlignment = TextAnchor.UpperCenter;
        tabsLayout.childForceExpandWidth = true;
        tabsLayout.childForceExpandHeight = false;
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = false;

        // Right side: category heading + scrollable row list.
        var listGo = new GameObject("UpgradeColumn", typeof(RectTransform));
        listGo.transform.SetParent(bodyGo.transform, false);
        var listRect = listGo.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.offsetMin = new Vector2(452, 0);
        listRect.offsetMax = Vector2.zero;

        TMP_Text categoryTitleText = UiTheme.Text(listGo.transform, "CategoryTitleText", "Body", 40,
            TextAlignmentOptions.BottomLeft, UiPalette.TextLight, FontStyles.Bold);
        UiTheme.TopStretchX(categoryTitleText.gameObject, listGo.transform, 0f, 1f, 0f, 48f);

        TMP_Text categorySummaryText = UiTheme.Text(listGo.transform, "CategorySummaryText",
            "What this group does.", 26, TextAlignmentOptions.TopLeft, UiPalette.TextMuted);
        UiTheme.TopStretchX(categorySummaryText.gameObject, listGo.transform, 0f, 1f, 50f, 38f);

        var scrollGo = new GameObject("RowScroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
        scrollGo.transform.SetParent(listGo.transform, false);
        var scrollRectT = scrollGo.GetComponent<RectTransform>();
        scrollRectT.anchorMin = new Vector2(0f, 0f);
        scrollRectT.anchorMax = new Vector2(1f, 1f);
        scrollRectT.offsetMin = Vector2.zero;
        scrollRectT.offsetMax = new Vector2(0, -100);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        UiTheme.StretchFull((RectTransform)viewportGo.transform);

        // Top-left anchored content driven by a VerticalLayoutGroup + ContentSizeFitter: rows can
        // never land outside the viewport the way hand-positioned graph nodes did.
        var contentGo = new GameObject("RowContainer", typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        contentRect.anchoredPosition = Vector2.zero;
        var contentLayout = contentGo.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 14f;
        contentLayout.padding = new RectOffset(0, 12, 0, 12);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        var contentFitter = contentGo.GetComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.viewport = (RectTransform)viewportGo.transform;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        // --- Footer: one Buy button, Back, and a single line of controls help. ---
        Button buyButton = UiTheme.Button(bgGo.transform, "BuyButton", "Buy", new Vector2(520, 96));
        var buyRect = (RectTransform)buyButton.transform;
        buyRect.anchorMin = new Vector2(1f, 0f);
        buyRect.anchorMax = new Vector2(1f, 0f);
        buyRect.pivot = new Vector2(1f, 0f);
        buyRect.anchoredPosition = new Vector2(-60, 56);
        buyButton.image.color = UiPalette.Purchased;
        TMP_Text buyButtonText = buyButton.GetComponentInChildren<TMP_Text>();
        buyButtonText.fontSize = 32;

        Button backButton = UiTheme.Button(bgGo.transform, "BackButton", "Back", new Vector2(280, 96));
        var backRect = (RectTransform)backButton.transform;
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 0f);
        backRect.pivot = new Vector2(0f, 0f);
        backRect.anchoredPosition = new Vector2(60, 56);

        TMP_Text helpText = UiTheme.Text(bgGo.transform, "HelpText",
            "Left / Right: choose a group     Up / Down: choose an upgrade     A: buy     B: back",
            24, TextAlignmentOptions.Center, UiPalette.TextMuted);
        var helpRect = (RectTransform)helpText.transform;
        helpRect.anchorMin = new Vector2(0f, 0f);
        helpRect.anchorMax = new Vector2(1f, 0f);
        helpRect.pivot = new Vector2(0.5f, 0f);
        helpRect.sizeDelta = new Vector2(-720, 34f);
        helpRect.anchoredPosition = new Vector2(0f, 14f);

        var panel = root.AddComponent<SkillTreeScreenController>();
        var so = new SerializedObject(panel);
        so.FindProperty("panelRoot").objectReferenceValue = root;
        so.FindProperty("characterNameText").objectReferenceValue = nameText;
        so.FindProperty("soulsText").objectReferenceValue = soulsText;
        so.FindProperty("categoryContainer").objectReferenceValue = tabsRect;
        so.FindProperty("categoryPrefab").objectReferenceValue = tabPrefab;
        so.FindProperty("rowContainer").objectReferenceValue = contentRect;
        so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
        so.FindProperty("rowScrollRect").objectReferenceValue = scrollRect;
        so.FindProperty("categoryTitleText").objectReferenceValue = categoryTitleText;
        so.FindProperty("categorySummaryText").objectReferenceValue = categorySummaryText;
        so.FindProperty("statSummaryText").objectReferenceValue = statSummaryText;
        so.FindProperty("helpText").objectReferenceValue = helpText;
        so.FindProperty("buyButton").objectReferenceValue = buyButton;
        so.FindProperty("buyButtonText").objectReferenceValue = buyButtonText;
        so.ApplyModifiedProperties();

        // CharacterSelectionManager drives Close() via input, but the mouse path needs a listener too.
        UnityEditor.Events.UnityEventTools.AddPersistentListener(backButton.onClick, panel.Close);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Places a header text band a fixed distance below the parent's top edge, with its own
    /// left/right padding. Both edges are anchored to the parent top (anchorMin.y == anchorMax.y == 1)
    /// and the box hangs downward from there, so writing offsetMin/offsetMax afterwards cannot move
    /// the band off-screen the way patching a stretched rect's offsets does.</summary>
    static void TopBand(RectTransform rect, float anchorMinX, float anchorMaxX,
        float yOffsetFromTop, float height, float padLeft, float padRight)
    {
        rect.anchorMin = new Vector2(anchorMinX, 1f);
        rect.anchorMax = new Vector2(anchorMaxX, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(padLeft, -(yOffsetFromTop + height));
        rect.offsetMax = new Vector2(-padRight, -yOffsetFromTop);
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
    // Upgrade groups — plain-language rows a non-gamer can read once and understand.
    // Every row says what it does in outcome terms ("hit harder", "ready sooner"), never in
    // jargon ("magnitude +0.25x"). Repeatable rows get rising prices so the ladder still has a
    // decision in it without needing separate "major" nodes.
    //
    // Every playable character gets the same four groups. The displayed "before" values are read
    // from that character's own skill assets (charDir), so a character tuned to a different
    // cooldown or damage shows its real numbers rather than AssCap's.
    // ------------------------------------------------------------------

    /// <summary>Authors the four upgrade groups onto a character's CharacterDefinition. Slot 2's
    /// tree differs per character (shuriken vs laser), so the caller picks the builder — everything
    /// else (Body, Whirlwind, Battle Cry) is shared.</summary>
    static void AuthorTrees(string definitionPath, string charDir, System.Func<string, SkillTreeDefinition> buildSlot2Tree)
    {
        var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(definitionPath);
        if (def == null)
        {
            Debug.LogError($"SkillTreeScreenGenerator: no CharacterDefinition at {definitionPath} — run the generator that creates it first ('Character Selection' for AssCap, 'Character 67' for 67).");
            return;
        }

        def.skillTrees = new[]
        {
            BuildBodyTree(),
            BuildWhirlwindTree(charDir),
            buildSlot2Tree(charDir),
            BuildBattleCryTree(charDir),
        };

        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
    }

    static SkillNodeDefinition Row(string id, string name, string description, SkillEffect effect,
        float value, int cost, int costIncrement, int maxRank, SkillEffectOp op = SkillEffectOp.Add)
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
            costIncrement = costIncrement,
            maxRank = maxRank,
        };
    }

    static SkillNodeDefinition UnlockRow(string description, int cost) =>
        Row("unlock", "Unlock this skill", description, SkillEffect.Unlock, 1f, cost, 0, 1);

    static SkillDisplayBase DisplayBase(SkillEffect effect, float baseValue, string unit) =>
        new SkillDisplayBase { effect = effect, baseValue = baseValue, unit = unit };

    static SkillTreeDefinition BuildBodyTree()
    {
        return new SkillTreeDefinition
        {
            skillId = "attributes",
            displayName = "Body",
            skillSlot = 0,
            branchColor = new Color(0.4f, 0.8f, 0.35f),
            target = SkillEffectTarget.Attribute,
            summary = "Always available. Makes your character stronger no matter which skills you use.",
            nodes = new[]
            {
                Row("strength_1", "Hit harder", "Every attack you make deals 20% more damage.",
                    SkillEffect.Strength, 1f, 10, 5, 5),
                Row("health_1", "Take more hits", "Adds 20 more health, so you survive longer.",
                    SkillEffect.Health, 1f, 10, 5, 5),
                Row("speed_1", "Move faster", "You walk and run 10% faster.",
                    SkillEffect.Speed, 1f, 10, 5, 5),
            },
        };
    }

    static SkillTreeDefinition BuildWhirlwindTree(string charDir)
    {
        var skill1 = AssetDatabase.LoadAssetAtPath<AttackData>(charDir + "/Skill1Attack.asset");
        float baseCooldown = skill1 != null ? skill1.cooldown : 10f;

        return new SkillTreeDefinition
        {
            skillId = "whirlwind",
            displayName = "Whirlwind",
            skillSlot = 1,
            branchColor = new Color(0.35f, 0.55f, 0.9f),
            summary = "Skill 1. A spin attack that hits every enemy standing around you.",
            displayBases = new[]
            {
                DisplayBase(SkillEffect.Cooldown, baseCooldown, "s"),
                DisplayBase(SkillEffect.Area, 3f, "m"),
            },
            nodes = new[]
            {
                UnlockRow("Lets you use Whirlwind: a spin that hits everything around you.", 20),
                Row("damage", "Hit harder", "Whirlwind does more damage each time you buy this.",
                    SkillEffect.Damage, 0.5f, 15, 10, 4),
                Row("area", "Reach further", "Whirlwind hits enemies standing further away.",
                    SkillEffect.Area, 1f, 20, 10, 3),
                Row("cooldown", "Ready sooner", "Cuts 2 seconds off the wait before you can use it again.",
                    SkillEffect.Cooldown, -2f, 15, 15, 3),
            },
        };
    }

    static SkillTreeDefinition BuildShurikenTree(string charDir)
    {
        var shuriken = AssetDatabase.LoadAssetAtPath<ProjectileSkillData>(charDir + "/ShurikenProjectile.asset");
        float baseCooldown = shuriken != null ? shuriken.cooldown : 2f;
        float baseDamage = shuriken != null ? shuriken.damage : 25f;
        int baseCount = shuriken != null && shuriken.spawnTimesNormalized != null ? shuriken.spawnTimesNormalized.Length : 2;

        return new SkillTreeDefinition
        {
            skillId = "shuriken",
            displayName = "Shuriken",
            skillSlot = 2,
            branchColor = new Color(0.65f, 0.35f, 0.85f),
            summary = "Skill 2. Throws blades that hit enemies from a distance.",
            displayBases = new[]
            {
                DisplayBase(SkillEffect.Cooldown, baseCooldown, "s"),
                DisplayBase(SkillEffect.Damage, baseDamage, ""),
                DisplayBase(SkillEffect.ProjectileCount, baseCount, ""),
            },
            nodes = new[]
            {
                UnlockRow("Lets you use Shuriken: thrown blades that hit from a distance.", 20),
                Row("damage", "Hit harder", "Each blade does 5 more damage.",
                    SkillEffect.Damage, 5f, 15, 10, 4),
                Row("count", "Throw one more", "Throws one extra blade every time you use the skill.",
                    SkillEffect.ProjectileCount, 1f, 30, 20, 3),
                Row("cooldown", "Ready sooner", "Cuts 0.5 seconds off the wait before you can throw again.",
                    SkillEffect.Cooldown, -0.5f, 15, 15, 2),
            },
        };
    }

    static SkillTreeDefinition BuildLaserTree(string charDir)
    {
        var laser = AssetDatabase.LoadAssetAtPath<LaserBeamSkillData>(charDir + "/LaserBeam.asset");
        float baseCooldown = laser != null ? laser.cooldown : 3f;
        float baseDamage = laser != null ? laser.damage : 45f;
        float baseRange = laser != null ? laser.range : 20f;
        float basePierce = laser != null ? laser.pierceCount : 1;

        return new SkillTreeDefinition
        {
            skillId = "laser",
            displayName = "Laser Beam",
            skillSlot = 2,
            branchColor = new Color(0.3f, 0.85f, 0.85f),
            summary = "Skill 2. Fires an instant beam from your finger that burns everything in a straight line.",
            displayBases = new[]
            {
                DisplayBase(SkillEffect.Cooldown, baseCooldown, "s"),
                DisplayBase(SkillEffect.Damage, baseDamage, ""),
                DisplayBase(SkillEffect.Range, baseRange, "m"),
                DisplayBase(SkillEffect.PierceCount, basePierce, ""),
            },
            nodes = new[]
            {
                UnlockRow("Lets you use Laser Beam: an instant beam that burns enemies in a line.", 20),
                Row("damage", "Hit harder", "The beam does 10 more damage.",
                    SkillEffect.Damage, 10f, 15, 10, 4),
                Row("range", "Reach further", "The beam reaches 5 meters further.",
                    SkillEffect.Range, 5f, 20, 10, 3),
                Row("pierce", "Burn through more", "The beam passes through one more enemy.",
                    SkillEffect.PierceCount, 1f, 30, 20, 3),
                Row("cooldown", "Ready sooner", "Cuts 0.5 seconds off the wait before you can fire again.",
                    SkillEffect.Cooldown, -0.5f, 15, 15, 2),
            },
        };
    }

    static SkillTreeDefinition BuildBattleCryTree(string charDir)
    {
        var battleCry = AssetDatabase.LoadAssetAtPath<BattleCrySkillData>(charDir + "/BattleCry.asset");
        float baseCooldown = battleCry != null ? battleCry.cooldown : 10f;
        float baseDuration = battleCry != null ? battleCry.duration : 5f;

        return new SkillTreeDefinition
        {
            skillId = "battlecry",
            displayName = "Battle Cry",
            skillSlot = 3,
            branchColor = new Color(0.85f, 0.3f, 0.25f),
            summary = "Skill 3. A shout that makes you run much faster for a few seconds.",
            displayBases = new[]
            {
                DisplayBase(SkillEffect.Cooldown, baseCooldown, "s"),
                DisplayBase(SkillEffect.Duration, baseDuration, "s"),
                DisplayBase(SkillEffect.Magnitude, 2f, "x"),
            },
            nodes = new[]
            {
                UnlockRow("Lets you use Battle Cry: a shout that makes you run faster.", 20),
                Row("magnitude", "Run even faster", "The speed boost gets stronger.",
                    SkillEffect.Magnitude, 0.25f, 15, 10, 4),
                Row("duration", "Lasts longer", "The speed boost lasts 1 second longer.",
                    SkillEffect.Duration, 1f, 20, 10, 4),
                Row("cooldown", "Ready sooner", "Cuts 2 seconds off the wait before you can shout again.",
                    SkillEffect.Cooldown, -2f, 15, 15, 3),
            },
        };
    }
}
