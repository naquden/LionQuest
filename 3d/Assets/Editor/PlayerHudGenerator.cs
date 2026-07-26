using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the in-game HUD: a screen-space Canvas prefab with 4 corner player panels
/// (portrait, name, health bar, souls, 3 skill slots with a shared-cooldown overlay),
/// wired into SampleScene's PlayerSpawner. Dual entry point per project convention.
/// </summary>
public static class PlayerHudGenerator
{
    const string UIDir = "Assets/Prefabs/UI";
    const string PrefabPath = UIDir + "/PlayerHUDCanvas.prefab";
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    const int SkillSlotCount = 3;
    static readonly Vector2[] PanelAnchors =
    {
        new Vector2(0f, 1f), // P1 top-left
        new Vector2(1f, 1f), // P2 top-right
        new Vector2(0f, 0f), // P3 bottom-left
        new Vector2(1f, 0f), // P4 bottom-right
    };

    [MenuItem("LionQuest/Generate/Player HUD")]
    public static void Generate()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogWarning("PlayerHudGenerator: TMP Essentials not imported " +
                "(Window > TextMeshPro > Import TMP Essential Resources). Continuing anyway.");
        }

        EnsureFolder(UIDir);

        GameObject prefab = BuildCanvasPrefab();
        WireIntoSampleScene(prefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("PlayerHudGenerator: done.");
    }

    // ------------------------------------------------------------------
    // PlayerHUDCanvas.prefab
    // ------------------------------------------------------------------

    static GameObject BuildCanvasPrefab()
    {
        var root = new GameObject("PlayerHUDCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // HUD is display-only — never intercept gameplay input.
        root.GetComponent<GraphicRaycaster>().enabled = false;

        var panels = new PlayerHUDPanel[PanelAnchors.Length];
        for (int i = 0; i < PanelAnchors.Length; i++)
        {
            panels[i] = BuildPanel(root.transform, i, PanelAnchors[i]);
        }

        var hud = root.AddComponent<PlayerHUD>();
        var so = new SerializedObject(hud);
        var panelsProp = so.FindProperty("panels");
        panelsProp.arraySize = panels.Length;
        for (int i = 0; i < panels.Length; i++)
        {
            panelsProp.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
        }
        so.ApplyModifiedProperties();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"PlayerHudGenerator: prefab ready at {PrefabPath}");
        return prefab;
    }

    static PlayerHUDPanel BuildPanel(Transform parent, int index, Vector2 corner)
    {
        var panelRoot = new GameObject($"Panel_P{index + 1}", typeof(RectTransform));
        panelRoot.transform.SetParent(parent, false);

        var rootRect = panelRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = corner;
        rootRect.anchorMax = corner;
        rootRect.pivot = corner;
        rootRect.sizeDelta = new Vector2(440, 176);
        Vector2 padding = new Vector2(corner.x > 0.5f ? -24 : 24, corner.y > 0.5f ? -24 : 24);
        rootRect.anchoredPosition = padding;

        var content = new GameObject("Content", typeof(RectTransform), typeof(Image));
        content.transform.SetParent(panelRoot.transform, false);
        StretchFull((RectTransform)content.transform);
        content.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.6f);
        SetNoRaycast(content.GetComponent<Image>());

        Image portraitImage = BuildPortrait(content.transform, corner);
        TMP_Text playerLabelText = BuildPlayerLabel(content.transform, corner);
        TMP_Text characterNameText = BuildCharacterName(content.transform, corner);
        (Image healthFillImage, TMP_Text healthText) = BuildHealthBar(content.transform, corner);
        TMP_Text soulsText = BuildSoulsText(content.transform, corner);

        Image[] skillIconImages = new Image[SkillSlotCount];
        Image[] skillCooldownOverlays = new Image[SkillSlotCount];
        TMP_Text[] skillCooldownTexts = new TMP_Text[SkillSlotCount];
        TMP_Text[] skillSlotLabels = new TMP_Text[SkillSlotCount];
        BuildSkillSlots(content.transform, corner, skillIconImages, skillCooldownOverlays, skillCooldownTexts, skillSlotLabels);

        var panel = panelRoot.AddComponent<PlayerHUDPanel>();
        var so = new SerializedObject(panel);
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("portraitImage").objectReferenceValue = portraitImage;
        so.FindProperty("playerLabelText").objectReferenceValue = playerLabelText;
        so.FindProperty("characterNameText").objectReferenceValue = characterNameText;
        so.FindProperty("healthFillImage").objectReferenceValue = healthFillImage;
        so.FindProperty("healthText").objectReferenceValue = healthText;
        so.FindProperty("soulsText").objectReferenceValue = soulsText;

        AssignArray(so, "skillIconImages", skillIconImages);
        AssignArray(so, "skillCooldownOverlays", skillCooldownOverlays);
        AssignArray(so, "skillCooldownTexts", skillCooldownTexts);
        AssignArray(so, "skillSlotLabels", skillSlotLabels);

        so.ApplyModifiedProperties();

        return panel;
    }

    static Image BuildPortrait(Transform parent, Vector2 corner)
    {
        var go = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(corner.x, 0.5f);
        rect.anchorMax = new Vector2(corner.x, 0.5f);
        rect.pivot = new Vector2(corner.x, 0.5f);
        rect.sizeDelta = new Vector2(96, 96);
        rect.anchoredPosition = new Vector2(corner.x > 0.5f ? -12 : 12, 0);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        SetNoRaycast(image);
        return image;
    }

    // Bands are stacked top-down in absolute pixels (not fractions) so fixed-size
    // elements (56px skill icons) never fight fractional siblings for the same space.
    const float BandPlayerLabelTop = 0f, BandPlayerLabelHeight = 26f;
    const float BandCharacterNameTop = 26f, BandCharacterNameHeight = 22f;
    const float BandHealthBarTop = 56f, BandHealthBarHeight = 22f;
    const float BandSoulsTextTop = 84f, BandSoulsTextHeight = 18f;
    const float BandSkillSlotsTop = 110f, BandSkillSlotsHeight = 56f;

    static RectTransform TopStretchX(GameObject go, Transform parent, float xMin, float xMax, float yOffsetFromTop, float height)
    {
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, 1f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, height);
        rect.anchoredPosition = new Vector2(0f, -yOffsetFromTop);
        return rect;
    }

    static TMP_Text BuildPlayerLabel(Transform parent, Vector2 corner)
    {
        var go = new GameObject("PlayerLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        TopStretchX(go, parent, corner.x > 0.5f ? 0f : 0.28f, corner.x > 0.5f ? 0.72f : 1f,
            BandPlayerLabelTop, BandPlayerLabelHeight);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = "P1";
        text.fontSize = 22;
        text.alignment = corner.x > 0.5f ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        SetNoRaycast(text);
        return text;
    }

    static TMP_Text BuildCharacterName(Transform parent, Vector2 corner)
    {
        var go = new GameObject("CharacterName", typeof(RectTransform), typeof(TextMeshProUGUI));
        TopStretchX(go, parent, corner.x > 0.5f ? 0f : 0.28f, corner.x > 0.5f ? 0.72f : 1f,
            BandCharacterNameTop, BandCharacterNameHeight);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = "Character";
        text.fontSize = 18;
        text.alignment = corner.x > 0.5f ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        text.color = new Color(1f, 1f, 1f, 0.85f);
        SetNoRaycast(text);
        return text;
    }

    static (Image, TMP_Text) BuildHealthBar(Transform parent, Vector2 corner)
    {
        var bgGo = new GameObject("HealthBarBg", typeof(RectTransform), typeof(Image));
        TopStretchX(bgGo, parent, corner.x > 0.5f ? 0f : 0.28f, corner.x > 0.5f ? 0.72f : 1f,
            BandHealthBarTop, BandHealthBarHeight);
        bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        SetNoRaycast(bgGo.GetComponent<Image>());

        var fillGo = new GameObject("HealthBarFill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(bgGo.transform, false);
        StretchFull((RectTransform)fillGo.transform);
        Image fillImage = fillGo.GetComponent<Image>();
        fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (fillImage.sprite == null)
        {
            Debug.LogError("PlayerHudGenerator: builtin sprite 'UI/Skin/UISprite.psd' could not be resolved — health bar will not fill correctly.");
        }
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
        fillImage.color = new Color(0.75f, 0.15f, 0.15f, 1f);
        SetNoRaycast(fillImage);

        var textGo = new GameObject("HealthText", typeof(RectTransform), typeof(TextMeshProUGUI));
        TopStretchX(textGo, parent, corner.x > 0.5f ? 0f : 0.28f, corner.x > 0.5f ? 0.72f : 1f,
            BandHealthBarTop, BandHealthBarHeight);
        var healthText = textGo.GetComponent<TextMeshProUGUI>();
        healthText.text = "100 / 100";
        healthText.fontSize = 14;
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.color = Color.white;
        SetNoRaycast(healthText);

        return (fillImage, healthText);
    }

    static TMP_Text BuildSoulsText(Transform parent, Vector2 corner)
    {
        var go = new GameObject("SoulsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        TopStretchX(go, parent, corner.x > 0.5f ? 0f : 0.28f, corner.x > 0.5f ? 0.72f : 1f,
            BandSoulsTextTop, BandSoulsTextHeight);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = "0";
        text.fontSize = 16;
        text.alignment = corner.x > 0.5f ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        text.color = new Color(1f, 0.85f, 0.2f, 1f);
        SetNoRaycast(text);
        return text;
    }

    static void BuildSkillSlots(Transform parent, Vector2 corner, Image[] icons, Image[] overlays, TMP_Text[] cooldownTexts, TMP_Text[] labels)
    {
        const float slotSize = 56f;
        const float spacing = 8f;

        var rowGo = new GameObject("SkillSlots", typeof(RectTransform));
        rowGo.transform.SetParent(parent, false);
        var rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(corner.x, 1f);
        rowRect.anchorMax = new Vector2(corner.x, 1f);
        rowRect.pivot = new Vector2(corner.x, 1f);
        rowRect.sizeDelta = new Vector2(SkillSlotCount * slotSize + (SkillSlotCount - 1) * spacing, BandSkillSlotsHeight);
        rowRect.anchoredPosition = new Vector2(corner.x > 0.5f ? -12 : 12, -BandSkillSlotsTop);

        for (int i = 0; i < SkillSlotCount; i++)
        {
            var slotGo = new GameObject($"Slot_{i + 1}", typeof(RectTransform), typeof(Image));
            slotGo.transform.SetParent(rowGo.transform, false);
            var slotRect = slotGo.GetComponent<RectTransform>();
            slotRect.anchorMin = slotRect.anchorMax = new Vector2(0f, 0f);
            slotRect.pivot = new Vector2(0f, 0f);
            slotRect.sizeDelta = new Vector2(slotSize, slotSize);
            slotRect.anchoredPosition = new Vector2(i * (slotSize + spacing), 0);
            slotGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 0.9f);
            SetNoRaycast(slotGo.GetComponent<Image>());

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(slotGo.transform, false);
            StretchFull((RectTransform)iconGo.transform);
            Image icon = iconGo.GetComponent<Image>();
            icon.enabled = false;
            SetNoRaycast(icon);
            icons[i] = icon;

            var labelGo = new GameObject("SlotLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(slotGo.transform, false);
            StretchFull((RectTransform)labelGo.transform);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = (i + 1).ToString();
            label.fontSize = 24;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 1f, 1f, 0.6f);
            SetNoRaycast(label);
            labels[i] = label;

            var overlayGo = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(slotGo.transform, false);
            StretchFull((RectTransform)overlayGo.transform);
            Image overlay = overlayGo.GetComponent<Image>();
            overlay.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (overlay.sprite == null)
            {
                Debug.LogError("PlayerHudGenerator: builtin sprite 'UI/Skin/UISprite.psd' could not be resolved — cooldown overlay will not fill correctly.");
            }
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)Image.Origin360.Top;
            overlay.fillClockwise = true;
            overlay.fillAmount = 0f;
            overlay.color = new Color(0f, 0f, 0f, 0.75f);
            SetNoRaycast(overlay);
            overlays[i] = overlay;

            var cooldownTextGo = new GameObject("CooldownText", typeof(RectTransform), typeof(TextMeshProUGUI));
            cooldownTextGo.transform.SetParent(slotGo.transform, false);
            StretchFull((RectTransform)cooldownTextGo.transform);
            var cooldownText = cooldownTextGo.GetComponent<TextMeshProUGUI>();
            cooldownText.fontSize = 18;
            cooldownText.alignment = TextAlignmentOptions.Center;
            cooldownText.color = Color.white;
            cooldownTextGo.SetActive(false);
            SetNoRaycast(cooldownText);
            cooldownTexts[i] = cooldownText;
        }
    }

    static void AssignArray(SerializedObject so, string propertyName, Object[] values)
    {
        var prop = so.FindProperty(propertyName);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    static void SetNoRaycast(Graphic graphic) => graphic.raycastTarget = false;

    static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------------
    // SampleScene wiring — binary scene, generator-only edits
    // ------------------------------------------------------------------

    static void WireIntoSampleScene(GameObject hudPrefab)
    {
        var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        PlayerHUD existingHud = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            existingHud = rootGo.GetComponentInChildren<PlayerHUD>(true);
            if (existingHud != null) break;
        }
        if (existingHud != null)
        {
            Object.DestroyImmediate(existingHud.gameObject);
        }

        var hudInstance = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene);
        PlayerHUD hud = hudInstance.GetComponent<PlayerHUD>();

        PlayerSpawner spawner = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            spawner = rootGo.GetComponentInChildren<PlayerSpawner>(true);
            if (spawner != null) break;
        }

        if (spawner == null)
        {
            Debug.LogError("PlayerHudGenerator: no PlayerSpawner found in SampleScene — run 'LionQuest/Generate/Character Selection' first.");
        }
        else
        {
            var so = new SerializedObject(spawner);
            so.FindProperty("hud").objectReferenceValue = hud;
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
