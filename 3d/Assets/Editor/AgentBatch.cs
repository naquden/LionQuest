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
    /// Supported targets: StartScreen, CharacterSelection, UpgradeScreen, PlayerHud
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
                case "AssCapSkills":
                    InvokeGeneratorMenu("LionQuest/Generate/AssCap Skills");
                    break;
                case "PlayerHud":
                    InvokeGeneratorMenu("LionQuest/Generate/Player HUD");
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

    public static void DiagnoseSampleScenePlayerSetup()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        bool hasGameSaveController = false;
        bool hasPlayerSpawner = false;
        int spawnPointCount = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<GameSaveController>(true) != null) hasGameSaveController = true;

            var spawner = root.GetComponentInChildren<PlayerSpawner>(true);
            if (spawner != null)
            {
                hasPlayerSpawner = true;
                var so = new SerializedObject(spawner);
                spawnPointCount = so.FindProperty("spawnPoints").arraySize;
                Debug.Log($"DIAG|fallbackDefinition={so.FindProperty("fallbackDefinition").objectReferenceValue}");
                Debug.Log($"DIAG|gameControllerPrefab={so.FindProperty("gameControllerPrefab").objectReferenceValue}");
                Debug.Log($"DIAG|allCharacters.arraySize={so.FindProperty("allCharacters").arraySize}");
            }
        }

        Debug.Log($"DIAG|hasGameSaveController={hasGameSaveController}");
        Debug.Log($"DIAG|hasPlayerSpawner={hasPlayerSpawner}");
        Debug.Log($"DIAG|spawnPointCount={spawnPointCount}");
        Report(OK, "diagnose sample scene player setup done");
    }

    public static void DiagnoseAssCapSkills()
    {
        const string prefabPath = "Assets/Characters/AssassinoCapuchino/AssasinoCappuchino.prefab";
        const string skill1DataPath = "Assets/Characters/AssassinoCapuchino/Skill1Attack.asset";
        const string battleCryPath = "Assets/Characters/AssassinoCapuchino/BattleCry.asset";

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Report(FAIL, $"prefab not found at {prefabPath}");
            EditorApplication.Exit(1);
            return;
        }

        var controller = prefab.GetComponentInChildren<TopDownPlayerController>(true);
        var so = new SerializedObject(controller);
        Debug.Log($"DIAG|skill1AttackData={so.FindProperty("skill1AttackData").objectReferenceValue}");
        Debug.Log($"DIAG|skill1BaseCooldown={so.FindProperty("skill1BaseCooldown").floatValue}");
        Debug.Log($"DIAG|skill3BattleCryData={so.FindProperty("skill3BattleCryData").objectReferenceValue}");

        var skill1Data = AssetDatabase.LoadAssetAtPath<AttackData>(skill1DataPath);
        if (skill1Data != null)
        {
            Debug.Log($"DIAG|skill1.damage={skill1Data.damage} radius(collider)={GetSphereRadius(skill1Data.effectPrefab)} spawnAtCenter={skill1Data.spawnAtCharacterCenter}");
        }
        else
        {
            Debug.Log("DIAG|skill1Data=MISSING");
        }

        var battleCry = AssetDatabase.LoadAssetAtPath<BattleCrySkillData>(battleCryPath);
        Debug.Log(battleCry != null
            ? $"DIAG|battleCry.duration={battleCry.duration} cooldown={battleCry.cooldown}"
            : "DIAG|battleCryData=MISSING");

        Report(OK, "diagnose asscap skills done");
    }

    static string GetSphereRadius(GameObject effectPrefab)
    {
        if (effectPrefab == null) return "no-effect-prefab";
        var sphere = effectPrefab.GetComponent<SphereCollider>();
        return sphere != null ? sphere.radius.ToString("F3") : "no-sphere-collider";
    }

    public static void DiagnosePlayerHud()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        PlayerHUD hud = null;
        PlayerSpawner spawner = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (hud == null) hud = root.GetComponentInChildren<PlayerHUD>(true);
            if (spawner == null) spawner = root.GetComponentInChildren<PlayerSpawner>(true);
        }

        Debug.Log($"DIAG|hudCanvas={(hud != null ? hud.gameObject.name : "MISSING")}");

        if (hud != null)
        {
            var so = new SerializedObject(hud);
            Debug.Log($"DIAG|panels={so.FindProperty("panels").arraySize}");
        }
        else
        {
            Debug.Log("DIAG|panels=0");
        }

        if (spawner != null)
        {
            var spawnerSo = new SerializedObject(spawner);
            var hudRef = spawnerSo.FindProperty("hud").objectReferenceValue;
            Debug.Log($"DIAG|spawnerHud={(hudRef != null ? hudRef.name : "MISSING")}");
        }
        else
        {
            Debug.Log("DIAG|spawnerHud=MISSING (no PlayerSpawner found)");
        }

        Report(OK, "diagnose player hud done");
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
