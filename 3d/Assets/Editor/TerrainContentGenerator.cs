using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-click build of the stylized terrain content: materials, procedural textures,
/// and the Sand GroundType. Dual entry point per project convention.
/// </summary>
public static class TerrainContentGenerator
{
    const string ShaderDir = "Assets/Shaders";
    const string TerrainDir = "Assets/Terrain";
    const string DetailDir = "Assets/Terrain/Details";

    const string TerrainShaderName = "LionQuest/StylizedTerrain";
    const string WaterShaderName = "LionQuest/StylizedWater";

    [MenuItem("LionQuest/Generate/Terrain Content")]
    public static void Generate()
    {
        EnsureFolder(ShaderDir);
        EnsureFolder(TerrainDir);
        EnsureFolder(DetailDir);

        CreateMaterial(TerrainShaderName, $"{ShaderDir}/StylizedTerrain.mat");
        Material water = CreateMaterial(WaterShaderName, $"{ShaderDir}/StylizedWater.mat");
        if (water != null)
        {
            water.SetFloat("_Surface", 1f);
            water.renderQueue = 3000;
        }

        Texture2D sandTex = WriteTexture(TileableGround(
            new Color(0.86f, 0.78f, 0.58f), new Color(0.74f, 0.64f, 0.45f), 12f),
            $"{TerrainDir}/Sand_Generated.png");

        CreateSandGroundType(sandTex);

        WriteTexture(GrassTuft(new Color(0.36f, 0.62f, 0.24f), new Color(0.52f, 0.76f, 0.30f)),
            $"{DetailDir}/GrassTuftA.png");
        WriteTexture(GrassTuft(new Color(0.44f, 0.58f, 0.22f), new Color(0.66f, 0.78f, 0.34f)),
            $"{DetailDir}/GrassTuftB.png");
        WriteTexture(GrassTuft(new Color(0.30f, 0.50f, 0.22f), new Color(0.46f, 0.68f, 0.28f)),
            $"{DetailDir}/GrassTuftC.png");
        WriteTexture(Flower(new Color(0.42f, 0.62f, 0.26f), new Color(0.98f, 0.94f, 0.62f)),
            $"{DetailDir}/FlowerYellow.png");
        WriteTexture(Flower(new Color(0.42f, 0.62f, 0.26f), new Color(0.96f, 0.96f, 0.98f)),
            $"{DetailDir}/FlowerWhite.png");

        ApplyColorGrade();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireSceneMapGenerator();

        Debug.Log("TerrainContentGenerator: done.");
    }

    /// <summary>
    /// Assigns the generated assets to the MapGenerator in SampleScene via the Unity API.
    /// The scene is binary-serialized, so it must never be text-edited.
    /// </summary>
    static void WireSceneMapGenerator()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        MapGenerator generator = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            generator = root.GetComponentInChildren<MapGenerator>(true);
            if (generator != null) break;
        }

        if (generator == null)
        {
            Debug.LogError($"TerrainContentGenerator: no MapGenerator found in {scenePath}.");
            return;
        }

        var so = new SerializedObject(generator);

        so.FindProperty("terrainMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>($"{ShaderDir}/StylizedTerrain.mat");
        so.FindProperty("waterMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>($"{ShaderDir}/StylizedWater.mat");

        // Correct known-bad Inspector values from earlier iterations (flatTerrain=true and
        // terrainHeight=1 made every height clamp to max; mapSize=50 left no room for the
        // island layout below). Force them so the island reads correctly without a manual
        // Inspector fixup.
        so.FindProperty("mapSize").vector2IntValue = new Vector2Int(160, 160);
        so.FindProperty("terrainHeight").floatValue = 20f;
        so.FindProperty("flatTerrain").boolValue = false;
        so.FindProperty("islandRadius").floatValue = 40f;
        so.FindProperty("oceanDepth").floatValue = 6f;
        so.FindProperty("beachWidth").floatValue = 8f;

        GroundType sand = AssetDatabase.LoadAssetAtPath<GroundType>($"{TerrainDir}/Sand.asset");
        so.FindProperty("sandGroundType").objectReferenceValue = sand;

        // Sand joins Grass/Dirt/Hole for exactly 4 terrain layers — the per-pass limit.
        var groundTypes = so.FindProperty("availableGroundTypes");
        bool hasSand = false;
        for (int i = 0; i < groundTypes.arraySize; i++)
        {
            if (groundTypes.GetArrayElementAtIndex(i).objectReferenceValue == sand) hasSand = true;
        }
        if (!hasSand && sand != null)
        {
            groundTypes.arraySize++;
            groundTypes.GetArrayElementAtIndex(groundTypes.arraySize - 1).objectReferenceValue = sand;
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

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("TerrainContentGenerator: MapGenerator wired in SampleScene.");
    }

    /// <summary>
    /// Adds a warm, saturated grade to the scene volume profile. Existing Bloom/Vignette/
    /// Tonemapping overrides are left alone.
    /// </summary>
    static void ApplyColorGrade()
    {
        const string path = "Assets/Settings/SampleSceneProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            Debug.LogError($"TerrainContentGenerator: volume profile not found at {path}.");
            return;
        }

        if (!profile.TryGet(out ColorAdjustments color))
        {
            color = profile.Add<ColorAdjustments>(true);
        }

        color.active = true;
        color.saturation.overrideState = true;
        color.saturation.value = 12f;
        color.contrast.overrideState = true;
        color.contrast.value = 6f;
        color.colorFilter.overrideState = true;
        color.colorFilter.value = new Color(1.0f, 0.98f, 0.92f);

        if (!profile.TryGet(out WhiteBalance balance))
        {
            balance = profile.Add<WhiteBalance>(true);
        }

        balance.active = true;
        balance.temperature.overrideState = true;
        balance.temperature.value = 12f;

        EditorUtility.SetDirty(profile);
        Debug.Log("TerrainContentGenerator: color grade applied to volume profile.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static Material CreateMaterial(string shaderName, string path)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"TerrainContentGenerator: shader '{shaderName}' not found — cannot create {path}.");
            return null;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        EditorUtility.SetDirty(mat);
        Debug.Log($"TerrainContentGenerator: material ready at {path}");
        return mat;
    }

    static void CreateSandGroundType(Texture2D texture)
    {
        const string path = TerrainDir + "/Sand.asset";
        GroundType sand = AssetDatabase.LoadAssetAtPath<GroundType>(path);
        if (sand == null)
        {
            sand = ScriptableObject.CreateInstance<GroundType>();
            AssetDatabase.CreateAsset(sand, path);
        }

        sand.groundName = "Sand";
        sand.biomeColor = new Color(0.90f, 0.82f, 0.60f);
        sand.movementSpeedMultiplier = 0.85f;
        sand.isWalkable = true;
        sand.isHole = false;
        sand.terrainTexture = texture;
        sand.textureTiling = new Vector2(10, 10);

        EditorUtility.SetDirty(sand);
        Debug.Log($"TerrainContentGenerator: GroundType ready at {path}");
    }

    static Texture2D WriteTexture(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Value noise that wraps on `period` so the result tiles seamlessly.
    static float TilingNoise(float x, float y, int period)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3f - 2f * xf);
        float v = yf * yf * (3f - 2f * yf);

        float H(int a, int b)
        {
            a = ((a % period) + period) % period;
            b = ((b % period) + period) % period;
            return Mathf.Abs(Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f) % 1f;
        }

        float a0 = H(xi, yi), b0 = H(xi + 1, yi), c0 = H(xi, yi + 1), d0 = H(xi + 1, yi + 1);
        return Mathf.Lerp(Mathf.Lerp(a0, b0, u), Mathf.Lerp(c0, d0, u), v);
    }

    static Texture2D TileableGround(Color light, Color dark, float frequency)
    {
        const int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        int period = Mathf.RoundToInt(frequency);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = (float)x / size * frequency;
                float fy = (float)y / size * frequency;
                float n = TilingNoise(fx, fy, period) * 0.6f
                        + TilingNoise(fx * 2f, fy * 2f, period * 2) * 0.3f
                        + TilingNoise(fx * 4f, fy * 4f, period * 4) * 0.1f;
                tex.SetPixel(x, y, Color.Lerp(dark, light, n));
            }
        }

        tex.Apply();
        return tex;
    }

    static Texture2D GrassTuft(Color baseColor, Color tipColor)
    {
        const int size = 128;
        var tex = NewTransparent(size);

        int blades = 7;
        for (int b = 0; b < blades; b++)
        {
            float t = (b + 0.5f) / blades;
            float rootX = Mathf.Lerp(size * 0.18f, size * 0.82f, t);
            float lean = Mathf.Lerp(-size * 0.16f, size * 0.16f, (b * 0.37f) % 1f);
            float height = size * Mathf.Lerp(0.55f, 0.95f, (b * 0.61f) % 1f);

            for (int y = 0; y < height; y++)
            {
                float ny = y / height;
                float cx = rootX + lean * ny * ny;
                float halfWidth = Mathf.Lerp(size * 0.035f, 0.6f, ny);

                for (int dx = -Mathf.CeilToInt(halfWidth); dx <= Mathf.CeilToInt(halfWidth); dx++)
                {
                    int px = Mathf.RoundToInt(cx) + dx;
                    if (px < 0 || px >= size) continue;
                    tex.SetPixel(px, y, Color.Lerp(baseColor, tipColor, ny));
                }
            }
        }

        tex.Apply();
        return tex;
    }

    static Texture2D Flower(Color stemColor, Color petalColor)
    {
        const int size = 128;
        var tex = NewTransparent(size);

        float cx = size * 0.5f;
        float stemTop = size * 0.72f;

        for (int y = 0; y < stemTop; y++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int px = Mathf.RoundToInt(cx) + dx;
                if (px >= 0 && px < size) tex.SetPixel(px, y, stemColor);
            }
        }

        float headRadius = size * 0.15f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, stemTop));
                if (d <= headRadius)
                {
                    float k = 1f - d / headRadius;
                    tex.SetPixel(x, y, Color.Lerp(petalColor, Color.white, k * 0.35f));
                }
            }
        }

        tex.Apply();
        return tex;
    }

    static Texture2D NewTransparent(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var clear = new Color[size * size];
        tex.SetPixels(clear);
        return tex;
    }
}
