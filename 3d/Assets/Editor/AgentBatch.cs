using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Headless batch entry points for agent automation.
/// Invoke via: Unity -batchmode -nographics -quit -executeMethod AgentBatch.{Method}
/// Pass args via environment variable AGENT_GEN.
/// Exit code 0 = success, 1 = failure.
/// Log sentinel lines: AGENT_BATCH_OK|msg or AGENT_BATCH_FAIL|msg
/// </summary>
public static class AgentBatch
{
    const string OK = "AGENT_BATCH_OK";
    const string FAIL = "AGENT_BATCH_FAIL";

    /// <summary>
    /// Compile gate. If Unity reaches this method, compilation succeeded.
    /// Usage: -executeMethod AgentBatch.Compile
    /// </summary>
    public static void Compile()
    {
        Report(OK, "compile clean");
    }

    /// <summary>
    /// Run a named generator by class method.
    /// Usage: AGENT_GEN=StartScreen -executeMethod AgentBatch.RunGenerator
    /// Supported targets: StartScreen, CharacterSelection, UpgradeScreen
    /// </summary>
    public static void RunGenerator()
    {
        string target = Environment.GetEnvironmentVariable("AGENT_GEN");
        if (string.IsNullOrEmpty(target))
        {
            Report(FAIL, "AGENT_GEN env var not set");
            EditorApplication.Exit(1);
            return;
        }

        try
        {
            switch (target)
            {
                case "StartScreen":
                    InvokeGeneratorMenu("LionQuest/Generate/Start Screen");
                    break;
                case "CharacterSelection":
                    InvokeGeneratorMenu("LionQuest/Generate/Character Selection");
                    break;
                case "UpgradeScreen":
                    InvokeGeneratorMenu("LionQuest/Generate/Upgrade Screen");
                    break;
                case "TerrainContent":
                    InvokeGeneratorMenu("LionQuest/Generate/Terrain Content");
                    break;
                case "GenerateMap":
                    RunMapGenerator();
                    break;
                default:
                    // Try as a full menu path
                    InvokeGeneratorMenu(target);
                    break;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Report(OK, $"generator {target} done");
        }
        catch (Exception e)
        {
            Report(FAIL, e.ToString());
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Run any generator by passing its full menu path.
    /// Usage: AGENT_GEN="LionQuest/Generate/Start Screen" -executeMethod AgentBatch.RunGeneratorByMenuPath
    /// </summary>
    public static void RunGeneratorByMenuPath()
    {
        string menuPath = Environment.GetEnvironmentVariable("AGENT_GEN");
        if (string.IsNullOrEmpty(menuPath))
        {
            Report(FAIL, "AGENT_GEN env var not set");
            EditorApplication.Exit(1);
            return;
        }

        try
        {
            InvokeGeneratorMenu(menuPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Report(OK, $"menu '{menuPath}' done");
        }
        catch (Exception e)
        {
            Report(FAIL, e.ToString());
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Prints environment facts an agent cannot get from disk.
    /// Usage: -executeMethod AgentBatch.Diagnose
    /// </summary>
    public static void Diagnose()
    {
        Debug.Log($"DIAG|serializationMode={EditorSettings.serializationMode}");
        Debug.Log($"DIAG|unityVersion={Application.unityVersion}");
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var main = AssetDatabase.LoadMainAssetAtPath(path);
            Debug.Log($"DIAG|scene={path} loadable={(main != null)}");
        }
        Report(OK, "diagnose done");
    }

    public static void DiagnoseMapGenerator()
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
            Report(FAIL, "no MapGenerator found");
            EditorApplication.Exit(1);
            return;
        }

        var so = new SerializedObject(generator);
        string[] fields = { "mapSize", "terrainHeight", "flatTerrain", "heightAmplitude", "heightFrequency",
            "islandRadius", "oceanDepth", "beachWidth", "pathCount", "pathWidth",
            "holeCount", "minHoleSize", "maxHoleSize" };
        foreach (var f in fields)
        {
            var p = so.FindProperty(f);
            Debug.Log($"DIAG|{f}={(p != null ? PropVal(p) : "MISSING")}");
        }

        Report(OK, "diagnose mapgen done");
    }

    static string PropVal(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Vector2: return p.vector2Value.ToString();
            case SerializedPropertyType.Vector2Int: return p.vector2IntValue.ToString();
            case SerializedPropertyType.Float: return p.floatValue.ToString("F2");
            case SerializedPropertyType.Integer: return p.intValue.ToString();
            case SerializedPropertyType.Boolean: return p.boolValue.ToString();
            default: return p.propertyType.ToString();
        }
    }

    static void RunMapGenerator()
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
            throw new Exception($"No MapGenerator found in {scenePath}");

        generator.GenerateMap();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    static void InvokeGeneratorMenu(string menuPath)
    {
        bool found = EditorApplication.ExecuteMenuItem(menuPath);
        if (!found)
            throw new Exception($"Menu item not found: '{menuPath}' — generator script may not be compiled yet");
    }

    static void Report(string tag, string msg) =>
        Debug.Log($"{tag}|{msg}");
}
