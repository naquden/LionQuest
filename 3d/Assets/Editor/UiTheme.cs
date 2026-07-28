using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared palette, procedural sprite authoring, and uGUI factory helpers for every
/// LionQuest editor generator (StartScreen, CharacterSelection, UpgradeScreen, PlayerHUD).
/// Warm fantasy / parchment art direction — see doc/characterSelection.md for the card layout.
/// </summary>
public static class UiTheme
{
    public const string GeneratedDir = "Assets/UI/Generated";

    // ------------------------------------------------------------------
    // Palette
    // ------------------------------------------------------------------

    // Values live in the runtime-safe UiPalette (Assets/Scripts/UI) so PlayerSlotUI etc. can
    // use them too — UiTheme itself is editor-only and can't be referenced from player code.
    public static Color BgTop => UiPalette.BgTop;
    public static Color BgBottom => UiPalette.BgBottom;
    public static Color Surface => UiPalette.Surface;
    public static Color SurfaceInset => UiPalette.SurfaceInset;
    public static Color Gold => UiPalette.Gold;
    public static Color GoldDim => UiPalette.GoldDim;
    public static Color TextLight => UiPalette.TextLight;
    public static Color TextMuted => UiPalette.TextMuted;
    public static Color Locked => UiPalette.Locked;

    public static Color Purchased => UiPalette.Purchased;
    public static Color Affordable => UiPalette.Affordable;
    public static Color Unaffordable => UiPalette.Unaffordable;
    public static Color SelectedOutline => UiPalette.SelectedOutline;

    public static Color PlayerAccent(int index) => UiPalette.PlayerAccent(index);

    // ------------------------------------------------------------------
    // Procedural sprites — written once to Assets/UI/Generated, reused thereafter
    // ------------------------------------------------------------------

    public static Sprite RoundedPanelSprite => LoadOrBuildSprite("RoundedPanel", 64, RoundedRect, new Vector4(20, 20, 20, 20));
    public static Sprite RoundedOutlineSprite => LoadOrBuildSprite("RoundedOutline", 64, RoundedRingTex, new Vector4(20, 20, 20, 20));
    public static Sprite SoftGlowSprite => LoadOrBuildSprite("SoftGlow", 128, RadialGlow, Vector4.zero);
    public static Sprite VignetteSprite => LoadOrBuildSprite("VignetteGradient", 128, VignetteTex, Vector4.zero);
    public static Sprite PillFillSprite => LoadOrBuildSprite("PillFill", 32, PillTex, new Vector4(8, 8, 8, 8));
    public static Sprite CircleSprite => LoadOrBuildSprite("Circle", 32, CircleTex, Vector4.zero);
    public static Sprite RingSprite => LoadOrBuildSprite("CircleRing", 32, CircleRingTex, Vector4.zero);
    public static Sprite BgGradientSprite => LoadOrBuildGradientSprite();

    static Sprite LoadOrBuildGradientSprite()
    {
        EnsureFolder(GeneratedDir);
        const string pngPath = GeneratedDir + "/BgGradient.png";

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath) == null)
        {
            const int w = 8, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(BgBottom, BgTop, (float)y / (h - 1));
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
    }

    static Texture2D CircleTex(int size)
    {
        var tex = NewTransparent(size);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(radius - d + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D CircleRingTex(int size)
    {
        var tex = NewTransparent(size);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.48f;
        float thickness = size * 0.12f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float outer = Mathf.Clamp01(radius - d + 0.5f);
                float inner = Mathf.Clamp01(radius - thickness - d + 0.5f);
                float a = Mathf.Clamp01(outer - inner);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    static Sprite LoadOrBuildSprite(string name, int size, System.Func<int, Texture2D> build, Vector4 border)
    {
        EnsureFolder(GeneratedDir);
        string pngPath = $"{GeneratedDir}/{name}.png";

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        if (tex == null)
        {
            Texture2D generated = build(size);
            File.WriteAllBytes(pngPath, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
    }

    static Texture2D RoundedRect(int size)
    {
        var tex = NewTransparent(size);
        float radius = size * 0.28f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float a = RoundedRectAlpha(x, y, size, radius);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D RoundedRingTex(int size)
    {
        var tex = NewTransparent(size);
        float radius = size * 0.28f;
        float thickness = size * 0.06f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float outer = RoundedRectAlpha(x, y, size, radius);
                float inner = RoundedRectAlpha(x, y, size, radius, thickness);
                float a = Mathf.Clamp01(outer - inner);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    static float RoundedRectAlpha(int x, int y, int size, float radius, float inset = 0f)
    {
        float min = inset, max = size - inset;
        float cx = Mathf.Clamp(x + 0.5f, min + radius, max - radius);
        float cy = Mathf.Clamp(y + 0.5f, min + radius, max - radius);
        float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - cx) - 0f);
        float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - cy) - 0f);

        bool insideCorner = (x + 0.5f < min + radius || x + 0.5f > max - radius) &&
                             (y + 0.5f < min + radius || y + 0.5f > max - radius);

        if (x + 0.5f < min || x + 0.5f > max || y + 0.5f < min || y + 0.5f > max) return 0f;

        if (insideCorner)
        {
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(radius - d + 0.5f);
        }
        return 1f;
    }

    static Texture2D RadialGlow(int size)
    {
        var tex = NewTransparent(size);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D VignetteTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.55f);
        float maxDist = size * 0.75f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float darken = Mathf.Clamp01(d - 0.35f) * 0.9f;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01(darken)));
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D PillTex(int size)
    {
        var tex = NewTransparent(size);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float a = RoundedRectAlpha(x, y, size, radius);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D NewTransparent(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var clear = new Color[size * size];
        tex.SetPixels(clear);
        return tex;
    }

    // ------------------------------------------------------------------
    // Typography
    // ------------------------------------------------------------------

    static TMP_FontAsset cachedFont;

    /// <summary>Default project font. No custom SDF font exists yet — falls back to the
    /// stock LiberationSans SDF shipped with TMP Essentials.</summary>
    public static TMP_FontAsset DefaultFont()
    {
        if (cachedFont != null) return cachedFont;
        string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("Fallback.asset")) continue;
            cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (cachedFont != null) break;
        }
        return cachedFont;
    }

    // ------------------------------------------------------------------
    // Layout / factory helpers
    // ------------------------------------------------------------------

    public static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    public static void AssignArray(SerializedObject so, string propertyName, Object[] values)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    /// <summary>Rounded, sliced panel image. Pass sprite=null for RoundedPanelSprite.</summary>
    public static Image Panel(Transform parent, string name, Color color, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite != null ? sprite : RoundedPanelSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    public static TMP_Text Text(Transform parent, string name, string label, int fontSize, TextAlignmentOptions alignment, Color color, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = style;
        text.font = DefaultFont();
        text.raycastTarget = false;
        return text;
    }

    public static Button Button(Transform parent, string name, string label, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.sprite = RoundedPanelSprite;
        image.type = Image.Type.Sliced;
        image.color = Surface;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = Text(go.transform, "Text", label, 28, TextAlignmentOptions.Center, TextLight, FontStyles.Bold);
        StretchFull((RectTransform)text.transform);

        return button;
    }

    /// <summary>Top-down absolute-pixel band stacking (ported from PlayerHudGenerator.TopStretchX)
    /// so fixed-size elements never fight fractional siblings for the same space.</summary>
    public static RectTransform TopStretchX(GameObject go, Transform parent, float xMin, float xMax, float yOffsetFromTop, float height)
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
}
