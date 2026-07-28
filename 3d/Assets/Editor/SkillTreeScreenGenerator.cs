using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the skill-tree upgrade screen (node + edge + tab-button prefabs, panel prefab), wires it
/// into the CharacterSelection scene, and authors AssCap's starting skill trees onto its
/// CharacterDefinition. Dual entry point per project convention. AgentBatch.cs already dispatches
/// "UpgradeScreen" here.
///
/// Layout is a full-screen, fully opaque panel (NOT a translucent overlay) split into:
///   header (character name + souls) -> tab row (one big button per skill) ->
///   left: scrollable node tree, right: description + cost + Buy -> bottom: Back.
/// Text and node sizes are large on purpose — this should read at a glance.
/// </summary>
public static class SkillTreeScreenGenerator
{
    const string UIDir = "Assets/Prefabs/UI";
    const string NodePrefabPath = UIDir + "/SkillTreeNode.prefab";
    const string EdgePrefabPath = UIDir + "/SkillTreeEdge.prefab";
    const string TabButtonPrefabPath = UIDir + "/SkillTreeTabButton.prefab";
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
        GameObject edgePrefab = BuildEdgePrefab();
        GameObject tabButtonPrefab = BuildTabButtonPrefab();
        GameObject panelPrefab = BuildPanelPrefab(nodePrefab, edgePrefab, tabButtonPrefab);

        WireIntoSelectionScene(panelPrefab);
        AuthorAssCapTrees();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("SkillTreeScreenGenerator: done.");
    }

    // ------------------------------------------------------------------
    // SkillTreeNode.prefab — big, high-contrast, readable at a glance
    // ------------------------------------------------------------------

    static GameObject BuildNodePrefab()
    {
        var root = new GameObject("SkillTreeNode", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 130);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image background = root.GetComponent<Image>();
        background.sprite = UiTheme.RoundedPanelSprite;
        background.type = Image.Type.Sliced;
        background.color = UiTheme.Surface;

        Button button = root.GetComponent<Button>();
        button.targetGraphic = background;

        var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(root.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.4f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(8, 0);
        nameRect.offsetMax = new Vector2(-8, -8);
        var nameText = nameGo.GetComponent<TextMeshProUGUI>();
        nameText.text = "Node";
        nameText.fontSize = 24;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 16;
        nameText.fontSizeMax = 24;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = UiTheme.TextLight;
        nameText.font = UiTheme.DefaultFont();
        nameText.raycastTarget = false;

        var costGo = new GameObject("CostText", typeof(RectTransform), typeof(TextMeshProUGUI));
        costGo.transform.SetParent(root.transform, false);
        var costRect = costGo.GetComponent<RectTransform>();
        costRect.anchorMin = new Vector2(0f, 0f);
        costRect.anchorMax = new Vector2(1f, 0.4f);
        costRect.offsetMin = new Vector2(8, 8);
        costRect.offsetMax = new Vector2(-8, 0);
        var costText = costGo.GetComponent<TextMeshProUGUI>();
        costText.text = "0 souls";
        costText.fontSize = 20;
        costText.alignment = TextAlignmentOptions.Center;
        costText.color = UiTheme.Gold;
        costText.font = UiTheme.DefaultFont();
        costText.raycastTarget = false;

        var nodeUi = root.AddComponent<SkillTreeNodeUI>();
        var so = new SerializedObject(nodeUi);
        so.FindProperty("background").objectReferenceValue = background;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("costText").objectReferenceValue = costText;
        so.FindProperty("button").objectReferenceValue = button;
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, NodePrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------
    // SkillTreeEdge.prefab — thick line, resized/rotated at spawn time
    // ------------------------------------------------------------------

    static GameObject BuildEdgePrefab()
    {
        var root = new GameObject("SkillTreeEdge", typeof(RectTransform), typeof(Image));
        var rect = root.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(100, 5);

        Image image = root.GetComponent<Image>();
        image.color = UiTheme.GoldDim;
        image.raycastTarget = false;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EdgePrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------
    // SkillTreeTabButton.prefab — one big button per skill
    // ------------------------------------------------------------------

    static GameObject BuildTabButtonPrefab()
    {
        GameObject go = CreateButton(null, "SkillTreeTabButton", "Skill",
            Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(320, 80), Vector2.zero);

        var text = go.GetComponentInChildren<TextMeshProUGUI>();
        text.fontSize = 28;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, TabButtonPrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------
    // Generic button helper — used by tab buttons, Buy, Back
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

    static GameObject BuildPanelPrefab(GameObject nodePrefab, GameObject edgePrefab, GameObject tabButtonPrefab)
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

        // --- Header: character name (left) + souls (right), big and unmissable. ---
        var headerGo = new GameObject("Header", typeof(RectTransform));
        headerGo.transform.SetParent(bgGo.transform, false);
        var headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 100f);
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

        // --- Tab row: one big labeled button per skill, always visible. ---
        var tabRowGo = new GameObject("TabRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabRowGo.transform.SetParent(bgGo.transform, false);
        var tabRowRect = tabRowGo.GetComponent<RectTransform>();
        tabRowRect.anchorMin = new Vector2(0f, 1f);
        tabRowRect.anchorMax = new Vector2(1f, 1f);
        tabRowRect.pivot = new Vector2(0.5f, 1f);
        tabRowRect.sizeDelta = new Vector2(0f, 100f);
        tabRowRect.anchoredPosition = new Vector2(0f, -100f);
        var tabRowLayout = tabRowGo.GetComponent<HorizontalLayoutGroup>();
        tabRowLayout.childAlignment = TextAnchor.MiddleCenter;
        tabRowLayout.spacing = 20;
        tabRowLayout.childForceExpandWidth = false;
        tabRowLayout.childForceExpandHeight = false;

        // --- Body: node tree (left, scrollable) + description/Buy panel (right). ---
        var bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(bgGo.transform, false);
        var bodyRect = bodyGo.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0.1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(40, 0);
        bodyRect.offsetMax = new Vector2(-40, -210);

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

        var contentGo = new GameObject("NodeContainer", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.sizeDelta = new Vector2(2200, 1400);
        contentRect.anchoredPosition = new Vector2(60, -60);

        var edgeContainerGo = new GameObject("EdgeContainer", typeof(RectTransform));
        edgeContainerGo.transform.SetParent(contentGo.transform, false);
        StretchFull((RectTransform)edgeContainerGo.transform);
        edgeContainerGo.transform.SetAsFirstSibling(); // edges render behind nodes

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
        descText.fontSize = 30;
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
        so.FindProperty("tabContainer").objectReferenceValue = tabRowGo.GetComponent<RectTransform>();
        so.FindProperty("tabButtonPrefab").objectReferenceValue = tabButtonPrefab;
        so.FindProperty("nodeContainer").objectReferenceValue = contentRect;
        so.FindProperty("nodePrefab").objectReferenceValue = nodePrefab;
        so.FindProperty("edgeContainer").objectReferenceValue = edgeContainerGo.GetComponent<RectTransform>();
        so.FindProperty("edgePrefab").objectReferenceValue = edgePrefab;
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

        def.skillTrees = new[] { BuildWhirlwindTree(), BuildShurikenTree(), BuildBattleCryTree() };

        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
    }

    static SkillNodeDefinition Node(string id, string name, string description, SkillEffect effect, SkillEffectOp op, float value, int cost, int gx, int gy, params string[] prereqs)
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
            prereqNodeIds = prereqs,
            gridPos = new Vector2Int(gx, gy),
        };
    }

    static SkillTreeDefinition BuildWhirlwindTree()
    {
        return new SkillTreeDefinition
        {
            skillId = "whirlwind",
            displayName = "Whirlwind",
            skillSlot = 1,
            nodes = new[]
            {
                Node("unlock", "Unlock", "Lets you use Whirlwind, a spinning attack that hits everything around you.",
                    SkillEffect.Unlock, SkillEffectOp.Add, 1, 20, 1, 0),
                Node("damage_1", "Damage +0.5x", "Whirlwind hits harder.",
                    SkillEffect.Damage, SkillEffectOp.Add, 0.5f, 15, 0, 1, "unlock"),
                Node("damage_2", "Damage +1.0x", "Whirlwind hits even harder.",
                    SkillEffect.Damage, SkillEffectOp.Add, 1.0f, 35, 0, 2, "damage_1"),
                Node("area_1", "Area +1", "Whirlwind hits enemies farther away from you.",
                    SkillEffect.Area, SkillEffectOp.Add, 1f, 20, 1, 1, "unlock"),
                Node("cooldown_2s", "Cooldown -2s", "Whirlwind is ready to use 2 seconds sooner.",
                    SkillEffect.Cooldown, SkillEffectOp.Add, -2f, 15, 2, 1, "unlock"),
                Node("cooldown_3s", "Cooldown -3s", "Whirlwind is ready to use 3 more seconds sooner.",
                    SkillEffect.Cooldown, SkillEffectOp.Add, -3f, 40, 2, 2, "cooldown_2s"),
            },
        };
    }

    static SkillTreeDefinition BuildShurikenTree()
    {
        return new SkillTreeDefinition
        {
            skillId = "shuriken",
            displayName = "Shuriken",
            skillSlot = 2,
            nodes = new[]
            {
                Node("unlock", "Unlock", "Lets you use Shuriken, a ranged throw that hits enemies from a distance.",
                    SkillEffect.Unlock, SkillEffectOp.Add, 1, 20, 1, 0),
                Node("damage_1", "Damage +5", "Each shuriken hits harder.",
                    SkillEffect.Damage, SkillEffectOp.Add, 5f, 15, 0, 1, "unlock"),
                Node("count_1", "Projectile +1", "Throw one extra shuriken per use.",
                    SkillEffect.ProjectileCount, SkillEffectOp.Add, 1f, 30, 1, 1, "unlock"),
                Node("cooldown_2s", "Cooldown -2s", "Shuriken is ready to use 2 seconds sooner.",
                    SkillEffect.Cooldown, SkillEffectOp.Add, -2f, 15, 2, 1, "unlock"),
            },
        };
    }

    static SkillTreeDefinition BuildBattleCryTree()
    {
        return new SkillTreeDefinition
        {
            skillId = "battlecry",
            displayName = "Battle Cry",
            skillSlot = 3,
            nodes = new[]
            {
                Node("unlock", "Unlock", "Lets you use Battle Cry, a shout that makes you move faster for a few seconds.",
                    SkillEffect.Unlock, SkillEffectOp.Add, 1, 20, 1, 0),
                Node("magnitude_1", "Magnitude +0.25x", "Battle Cry makes you move even faster.",
                    SkillEffect.Magnitude, SkillEffectOp.Add, 0.25f, 15, 0, 1, "unlock"),
                Node("magnitude_2", "Magnitude +0.5x", "Battle Cry makes you move a lot faster.",
                    SkillEffect.Magnitude, SkillEffectOp.Add, 0.5f, 35, 0, 2, "magnitude_1"),
                Node("duration_1", "Duration +1s", "Battle Cry's speed boost lasts 1 second longer.",
                    SkillEffect.Duration, SkillEffectOp.Add, 1f, 20, 1, 1, "unlock"),
                Node("duration_2", "Duration +2s", "Battle Cry's speed boost lasts 2 more seconds.",
                    SkillEffect.Duration, SkillEffectOp.Add, 2f, 40, 1, 2, "duration_1"),
                Node("cooldown_2s", "Cooldown -2s", "Battle Cry is ready to use 2 seconds sooner.",
                    SkillEffect.Cooldown, SkillEffectOp.Add, -2f, 15, 2, 1, "unlock"),
            },
        };
    }
}
