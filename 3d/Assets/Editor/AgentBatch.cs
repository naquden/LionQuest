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
    /// Supported targets: StartScreen, CharacterSelection, UpgradeScreen, PlayerHud, SkillIcons
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
                case "Character67":
                    InvokeGeneratorMenu("LionQuest/Generate/Character 67");
                    break;
                case "PlayerHud":
                    InvokeGeneratorMenu("LionQuest/Generate/Player HUD");
                    break;
                case "PauseMenu":
                    InvokeGeneratorMenu("LionQuest/Generate/Pause Menu");
                    break;
                case "SkillIcons":
                    InvokeGeneratorMenu("LionQuest/Generate/Skill Icons");
                    break;
                case "Level1":
                    InvokeGeneratorMenu("LionQuest/Generate/Level 1");
                    break;
                case "EnemyCombat":
                    InvokeGeneratorMenu("LionQuest/Generate/Enemy Combat");
                    break;
                case "GenerateMap":
                    RunMapGenerator();
                    break;
                case "CameraRigFraming":
                    ApplyCameraRigFraming();
                    break;
                case "AgentGym":
                    InvokeGeneratorMenu("LionQuest/Generate/Agent Gym");
                    break;
                case "AgentGymCoop":
                    InvokeGeneratorMenu("LionQuest/Generate/Agent Gym Coop");
                    break;
                case "CaptureSelection":
                    CaptureCharacterSelection();
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
    /// Builds a real player binary. This is the ONLY gate that proves the game can ship:
    /// AgentBatch.Compile passes even when a player build is impossible, because it compiles the
    /// Editor assembly too (an unguarded `using UnityEditor` in Assembly-CSharp compiles fine in
    /// the editor and fails only for the player). Test scenes are excluded here rather than in
    /// EditorBuildSettings so the PlayMode suite can still load them.
    /// Usage: -executeMethod AgentBatch.BuildPlayerBinary
    /// </summary>
    public static void BuildPlayerBinary()
    {
        try
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                // AgentGym* are PlayMode-test rigs and SampleScene is the dead binary predecessor
                // of Level1 — none of them belong in a build handed to a player.
                if (scene.path.Contains("AgentGym") || scene.path.Contains("SampleScene")) continue;
                scenes.Add(scene.path);
            }

            if (scenes.Count == 0)
            {
                Report(FAIL, "no shippable scenes in EditorBuildSettings");
                EditorApplication.Exit(1);
                return;
            }

            // Build/ is gitignored; this is a throwaway artifact whose only purpose is proving the
            // player assemblies link.
            string outDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath), "Build", "AgentBuildCheck");
            System.IO.Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = System.IO.Path.Combine(outDir, "LionQuest.app"),
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development,
            };

            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            UnityEditor.Build.Reporting.BuildSummary summary = report.summary;
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Report(FAIL, $"player build {summary.result} with {summary.totalErrors} error(s)");
                EditorApplication.Exit(1);
                return;
            }

            Report(OK, $"player build succeeded, {scenes.Count} scene(s), {summary.totalSize} bytes");
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

    /// <summary>
    /// Dumps every scene's TopDownCameraController yaw/tilt. Movement in TopDownPlayerController is
    /// world-axis (new Vector3(input.x, 0, input.y)) with no camera-relative transform, so any
    /// non-zero rotationAngle shears input against what's on screen. SampleScene is binary, so its
    /// values are only readable this way — this is the reference the other scenes must match.
    /// Usage: -executeMethod AgentBatch.DiagnoseCameraRig
    /// </summary>
    public static void DiagnoseCameraRig()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/Scenes/")) continue;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                path, UnityEditor.SceneManagement.OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                {
                    var rig = cam.GetComponent<TopDownCameraController>();
                    string kind = rig != null ? "TopDownCameraController" : "plain Camera";
                    string angles = "n/a";
                    if (rig != null)
                    {
                        var so = new SerializedObject(rig);
                        angles = $"tilt={PropVal(so.FindProperty("tiltAngle"))} " +
                                 $"yaw={PropVal(so.FindProperty("rotationAngle"))} " +
                                 $"dist={PropVal(so.FindProperty("distance"))} " +
                                 $"height={PropVal(so.FindProperty("height"))}";
                    }
                    Debug.Log($"DIAG|scene={path} cam={cam.name} rig={kind} {angles} " +
                              $"euler={cam.transform.rotation.eulerAngles} tag={cam.tag}");
                }
            }
        }
        Report(OK, "diagnose camera rig done");
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

    /// <summary>
    /// Dumps every CharacterDefinition the roster discovers plus, for each, the components a
    /// player prefab must carry to be playable. A character can look correct in the carousel and
    /// still be unplayable (no PlayerInput -> no input; no CharacterStats -> no saves/skill
    /// gating), and neither shows up as a compile error — hence the per-component print.
    /// </summary>
    public static void DiagnoseRoster()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterDefinition");
        Debug.Log($"DIAG|definitionCount={guids.Length}");

        bool allPlayable = true;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (def == null) continue;

            int treeCount = def.skillTrees != null ? def.skillTrees.Length : 0;
            Debug.Log($"DIAG|definition={def.characterID} display='{def.displayName}' " +
                      $"unlockedByDefault={def.unlockedByDefault} trees={treeCount} path={path}");

            GameObject prefab = def.playerPrefab;
            if (prefab == null)
            {
                Debug.Log($"DIAG|{def.characterID}.playerPrefab=MISSING");
                allPlayable = false;
                continue;
            }

            bool hasController = prefab.GetComponentInChildren<TopDownPlayerController>(true) != null;
            bool hasCombat = prefab.GetComponentInChildren<CombatController>(true) != null;
            bool hasStats = prefab.GetComponentInChildren<CharacterStats>(true) != null;
            bool hasAnimator = prefab.GetComponentInChildren<CharacterAnimator>(true) != null;
            bool hasInput = prefab.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>(true) != null;
            bool hasBody = prefab.GetComponentInChildren<Rigidbody>(true) != null
                           && prefab.GetComponentInChildren<CapsuleCollider>(true) != null;

            Debug.Log($"DIAG|{def.characterID}.prefab={prefab.name} controller={hasController} " +
                      $"combat={hasCombat} stats={hasStats} animator={hasAnimator} " +
                      $"playerInput={hasInput} rigidbody+capsule={hasBody} active={prefab.activeSelf} " +
                      $"tag={prefab.tag}");

            if (treeCount == 0) Debug.Log($"DIAG|{def.characterID}.trees=NONE (no upgrades purchasable, all skills stay locked)");

            // Skill slots 1..3 must each resolve to a tree, or TopDownPlayerController treats the
            // slot as permanently unlocked and the HUD lock mark disagrees with the buttons.
            if (def.skillTrees != null)
            {
                for (int slot = 1; slot <= 3; slot++)
                {
                    bool bound = false;
                    foreach (SkillTreeDefinition tree in def.skillTrees)
                    {
                        if (tree.skillSlot == slot) { bound = true; break; }
                    }
                    if (!bound) Debug.Log($"DIAG|{def.characterID}.slot{slot}=UNBOUND");
                }
            }

            if (!hasController || !hasCombat || !hasStats || !hasAnimator || !hasInput || !hasBody
                || treeCount == 0)
            {
                allPlayable = false;
            }
        }

        if (!allPlayable)
        {
            Report(FAIL, "one or more CharacterDefinitions are not playable (see DIAG lines above)");
            EditorApplication.Exit(1);
            return;
        }

        Report(OK, "diagnose roster done");
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

    /// <summary>
    /// Lists every TopDownPlayerController baked into the gameplay scene together with the
    /// CharacterStats identity/definition it carries. A scene-baked player next to
    /// PlayerSpawner's runtime clone means two players read input at once: one of them can be
    /// missing its CharacterDefinition, in which case SkillIdForSlot returns null and
    /// HandleSkills' unlock gate is skipped entirely — skills fire while the HUD (bound to the
    /// other player) shows every slot locked.
    /// Usage: -executeMethod AgentBatch.DiagnoseScenePlayers
    /// </summary>
    public static void DiagnoseScenePlayers()
    {
        string scenePath = Environment.GetEnvironmentVariable("AGENT_GEN");
        if (string.IsNullOrEmpty(scenePath)) scenePath = "Assets/Scenes/SampleScene.unity";

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        int playerCount = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var controller in root.GetComponentsInChildren<TopDownPlayerController>(true))
            {
                playerCount++;
                var stats = controller.GetComponent<CharacterStats>();
                string characterId = stats != null ? stats.characterID : "NO-CharacterStats";
                CharacterDefinition def = stats != null ? stats.definition : null;
                Debug.Log($"DIAG|scenePlayer[{playerCount - 1}] name={controller.gameObject.name} " +
                          $"path={PathOf(controller.transform)} active={controller.gameObject.activeInHierarchy} " +
                          $"characterID={characterId} definition={(def != null ? def.name : "NULL")}");

                if (def == null) continue;
                int treeCount = def.skillTrees != null ? def.skillTrees.Length : 0;
                Debug.Log($"DIAG|scenePlayer[{playerCount - 1}].skillTrees={treeCount}");
                if (def.skillTrees == null) continue;
                foreach (var tree in def.skillTrees)
                {
                    Debug.Log($"DIAG|scenePlayer[{playerCount - 1}].tree id={tree.skillId} slot={tree.skillSlot}");
                }
            }
        }

        Debug.Log($"DIAG|scene={scenePath} playerCount={playerCount}");
        Debug.Log($"DIAG|persistentDataPath={Application.persistentDataPath}");
        string savePath = System.IO.Path.Combine(Application.persistentDataPath, "gamesave.json");
        Debug.Log($"DIAG|saveExists={System.IO.File.Exists(savePath)} savePath={savePath}");
        if (System.IO.File.Exists(savePath))
        {
            Debug.Log($"DIAG|saveContents={System.IO.File.ReadAllText(savePath).Replace("\n", " ")}");
        }

        Report(OK, "diagnose scene players done");
    }

    /// <summary>
    /// Removes scene-baked player objects from SampleScene, leaving PlayerSpawner as the only
    /// thing that creates players. A baked player has no CharacterDefinition (only PlayerSpawner
    /// assigns one), so its skill slots resolve no skill tree and fire regardless of the unlock
    /// gate, while the HUD stays bound to the spawner's clone and shows those slots locked.
    ///
    /// Deliberately NOT called by any generator: SampleScene is binary-serialized, so this edit
    /// produces no reviewable diff. Run it on purpose:
    ///   sh tools/unity-batch.sh StripBakedPlayersFromSampleScene
    /// AgentGym.unity needs no such cleanup — it is generated with zero baked players.
    /// </summary>
    public static void StripBakedPlayersFromSampleScene()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        var doomed = new System.Collections.Generic.List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var controller in root.GetComponentsInChildren<TopDownPlayerController>(true))
            {
                doomed.Add(controller.gameObject);
                Debug.Log($"DIAG|removing bakedPlayer={PathOf(controller.transform)}");
            }
        }

        foreach (GameObject go in doomed)
        {
            UnityEngine.Object.DestroyImmediate(go);
        }

        if (doomed.Count > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        Report(OK, $"stripped {doomed.Count} baked player(s) from SampleScene");
    }

    static string PathOf(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    /// <summary>
    /// Dumps spawn-point world position, MapGenerator terrain params, and the terrain
    /// surface height sampled under each spawn point, so a buried-spawn theory can be
    /// confirmed or refuted before touching spawn code.
    /// Usage: -executeMethod AgentBatch.DiagnoseSkillSpawnGeometry
    /// </summary>
    public static void DiagnoseSkillSpawnGeometry()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        PlayerSpawner spawner = null;
        MapGenerator generator = null;
        Terrain terrain = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (spawner == null) spawner = root.GetComponentInChildren<PlayerSpawner>(true);
            if (generator == null) generator = root.GetComponentInChildren<MapGenerator>(true);
            if (terrain == null) terrain = root.GetComponentInChildren<Terrain>(true);
        }

        if (generator != null)
        {
            var genSo = new SerializedObject(generator);
            string[] fields = { "mapSize", "terrainHeight", "flatTerrain", "heightAmplitude",
                "islandRadius", "oceanDepth", "beachWidth" };
            foreach (var f in fields)
            {
                var p = genSo.FindProperty(f);
                Debug.Log($"DIAG|mapGenerator.{f}={(p != null ? PropVal(p) : "MISSING")}");
            }
        }
        else
        {
            Debug.Log("DIAG|mapGenerator=MISSING");
        }

        if (terrain != null)
        {
            Debug.Log($"DIAG|terrain.transform.position={terrain.transform.position}");
        }
        else
        {
            Debug.Log("DIAG|terrain=MISSING");
        }

        if (spawner != null)
        {
            var spawnerSo = new SerializedObject(spawner);
            var spawnPointsProp = spawnerSo.FindProperty("spawnPoints");
            int count = spawnPointsProp.arraySize;
            Debug.Log($"DIAG|spawnPointCount={count}");

            for (int i = 0; i < count; i++)
            {
                Transform point = spawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (point == null)
                {
                    Debug.Log($"DIAG|spawnPoint[{i}]=MISSING");
                    continue;
                }

                Vector3 pos = point.position;
                float surfaceY = terrain != null ? terrain.SampleHeight(pos) + terrain.transform.position.y : float.NaN;
                Debug.Log($"DIAG|spawnPoint[{i}].position={pos} terrainSurfaceY={surfaceY:F2}");
            }

            // SpawnPlayer falls back to the spawner's own transform when spawnPoints is short.
            Vector3 fallbackPos = spawner.transform.position;
            float fallbackSurfaceY = terrain != null ? terrain.SampleHeight(fallbackPos) + terrain.transform.position.y : float.NaN;
            Debug.Log($"DIAG|spawnerTransform.position={fallbackPos} terrainSurfaceY={fallbackSurfaceY:F2}");
        }
        else
        {
            Debug.Log("DIAG|spawnPointCount=0 (no PlayerSpawner found)");
        }

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Characters/AssassinoCapuchino/AssasinoCappuchino.prefab");
        if (playerPrefab != null)
        {
            var controller = playerPrefab.GetComponentInChildren<TopDownPlayerController>(true);
            if (controller != null)
            {
                var ctrlSo = new SerializedObject(controller);
                Debug.Log($"DIAG|player.groundLevel={PropVal(ctrlSo.FindProperty("groundLevel"))}");
            }
        }

        ProjectileSkillData shuriken = AssetDatabase.LoadAssetAtPath<ProjectileSkillData>(
            "Assets/Characters/AssassinoCapuchino/ShurikenProjectile.asset");
        Debug.Log(shuriken != null
            ? $"DIAG|shuriken.spawnOffset={shuriken.spawnOffset}"
            : "DIAG|shuriken=MISSING");

        Report(OK, "diagnose skill spawn geometry done");
    }

    public static void DiagnoseSkillTree()
    {
        const string scenePath = "Assets/Scenes/CharacterSelection.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        SkillTreeScreenController panel = null;
        CharacterSelectionManager manager = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (panel == null) panel = root.GetComponentInChildren<SkillTreeScreenController>(true);
            if (manager == null) manager = root.GetComponentInChildren<CharacterSelectionManager>(true);
        }

        Debug.Log($"DIAG|panel={(panel != null ? panel.gameObject.name : "MISSING")}");

        if (manager != null)
        {
            var so = new SerializedObject(manager);
            var screenRef = so.FindProperty("skillTreeScreen").objectReferenceValue;
            Debug.Log($"DIAG|managerSkillTreeScreen={(screenRef != null ? screenRef.name : "MISSING")}");
        }
        else
        {
            Debug.Log("DIAG|managerSkillTreeScreen=MISSING (no CharacterSelectionManager found)");
        }

        const string assCapDefinitionPath = "Assets/Characters/AssassinoCapuchino/AssCap_Definition.asset";
        var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(assCapDefinitionPath);
        if (def == null)
        {
            Debug.Log("DIAG|assCapSkillTrees=MISSING (no CharacterDefinition asset)");
        }
        else
        {
            Debug.Log($"DIAG|assCapSkillTreeCount={(def.skillTrees != null ? def.skillTrees.Length : 0)}");
            if (def.skillTrees != null)
            {
                foreach (var tree in def.skillTrees)
                {
                    Debug.Log($"DIAG|tree={tree.skillId} slot={tree.skillSlot} nodes={(tree.nodes != null ? tree.nodes.Length : 0)}");
                }
            }
        }

        if (panel != null && def != null && !DiagnoseSkillTreeLayout(panel, def))
        {
            Report(FAIL, "one or more skill tree nodes fall outside the node container bounds");
            EditorApplication.Exit(1);
            return;
        }

        Report(OK, "diagnose skill tree done");
    }

    public static void DiagnosePauseMenu()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        PauseMenuController pauseMenu = null;
        PlayerSpawner spawner = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (pauseMenu == null) pauseMenu = root.GetComponentInChildren<PauseMenuController>(true);
            if (spawner == null) spawner = root.GetComponentInChildren<PlayerSpawner>(true);
        }

        Debug.Log($"DIAG|pauseMenu={(pauseMenu != null ? pauseMenu.gameObject.name : "MISSING")}");

        if (pauseMenu != null)
        {
            var so = new SerializedObject(pauseMenu);
            string[] props = { "panel", "resumeButton", "characterSelectionButton", "exitButton", "inputActions" };
            foreach (string propName in props)
            {
                var refValue = so.FindProperty(propName).objectReferenceValue;
                Debug.Log($"DIAG|pauseMenu.{propName}={(refValue != null ? refValue.name : "MISSING")}");
            }
            Debug.Log($"DIAG|pauseMenu.characterSelectionSceneName={so.FindProperty("characterSelectionSceneName").stringValue}");
        }

        if (spawner != null)
        {
            var so = new SerializedObject(spawner);
            var refValue = so.FindProperty("pauseMenu").objectReferenceValue;
            Debug.Log($"DIAG|spawner.pauseMenu={(refValue != null ? refValue.name : "MISSING")}");
        }
        else
        {
            Debug.Log("DIAG|spawner=MISSING (no PlayerSpawner found)");
        }

        Report(OK, "diagnose pause menu done");
    }

    /// <summary>
    /// Opens the panel against a real CharacterDefinition and asserts the upgrade rows for the
    /// first category actually spawned and each carries visible text — a machine check that the
    /// list is populated, replacing the old graph-era out-of-bounds node check (rows are now
    /// positioned by a VerticalLayoutGroup, so they cannot land outside the viewport at all).
    /// </summary>
    static bool DiagnoseSkillTreeLayout(SkillTreeScreenController panel, CharacterDefinition def)
    {
        panel.gameObject.SetActive(true);
        panel.Open(def, null);

        var so = new SerializedObject(panel);
        var rowContainer = so.FindProperty("rowContainer").objectReferenceValue as RectTransform;
        var categoryContainer = so.FindProperty("categoryContainer").objectReferenceValue as RectTransform;
        if (rowContainer == null || categoryContainer == null)
        {
            Debug.Log($"DIAG|rowContainer={(rowContainer != null ? "ok" : "MISSING")} categoryContainer={(categoryContainer != null ? "ok" : "MISSING")}");
            return false;
        }

        int tabCount = 0;
        foreach (RectTransform child in categoryContainer)
        {
            if (child.GetComponent<SkillTreeLinkUI>() != null) tabCount++;
        }

        int rowCount = 0;
        int blankRows = 0;
        foreach (RectTransform child in rowContainer)
        {
            var rowUi = child.GetComponent<SkillTreeNodeUI>();
            if (rowUi == null) continue;
            rowCount++;

            bool hasText = false;
            foreach (var text in child.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text)) { hasText = true; break; }
            }
            if (!hasText)
            {
                blankRows++;
                Debug.Log($"DIAG|rowHasNoVisibleText id={rowUi.NodeId}");
            }
        }

        int expectedTabs = def.skillTrees != null ? def.skillTrees.Length : 0;
        int expectedRows = def.skillTrees != null && def.skillTrees.Length > 0 && def.skillTrees[0].nodes != null
            ? def.skillTrees[0].nodes.Length
            : 0;
        Debug.Log($"DIAG|upgradeTabs={tabCount}/{expectedTabs} upgradeRows={rowCount}/{expectedRows} blankRows={blankRows}");

        panel.Close();
        return tabCount == expectedTabs && rowCount == expectedRows && rowCount > 0 && blankRows == 0;
    }

    /// <summary>
    /// Renders CharacterSelection.unity's overlay canvas into 1920x1080 PNGs for each
    /// PlayerSlotUI state (Empty/Selecting/Ready) so an agent can inspect the actual layout
    /// without a display. Needs a real graphics context — run via
    /// "sh tools/unity-batch.sh RunGenerator CaptureSelection graphics" (drops -nographics).
    /// </summary>
    static void CaptureCharacterSelection()
    {
        const string scenePath = "Assets/Scenes/CharacterSelection.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

        Canvas canvas = null;
        CharacterSelectionManager manager = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (canvas == null) canvas = root.GetComponentInChildren<Canvas>(true);
            if (manager == null) manager = root.GetComponentInChildren<CharacterSelectionManager>(true);
        }

        if (canvas == null || manager == null)
            throw new Exception("CaptureCharacterSelection: missing Canvas or CharacterSelectionManager in CharacterSelection.unity");

        var managerSo = new SerializedObject(manager);
        var slotsProp = managerSo.FindProperty("slots");
        var slots = new PlayerSlotUI[slotsProp.arraySize];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = slotsProp.GetArrayElementAtIndex(i).objectReferenceValue as PlayerSlotUI;

        var rosterProp = managerSo.FindProperty("allCharacters");
        var roster = new CharacterDefinition[rosterProp.arraySize];
        for (int i = 0; i < roster.Length; i++)
            roster[i] = rosterProp.GetArrayElementAtIndex(i).objectReferenceValue as CharacterDefinition;

        if (slots.Length == 0 || slots[0] == null || roster.Length == 0)
            throw new Exception("CaptureCharacterSelection: no slots or no roster wired.");

        var captureCameraGo = new GameObject("CaptureCamera", typeof(Camera));
        Camera captureCamera = captureCameraGo.GetComponent<Camera>();
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = Color.black;
        // ScreenSpaceCamera canvas rendering requires the canvas's own layer in cullingMask —
        // restrict to just that layer so no world-space object (e.g. a per-slot preview rig
        // sitting at its world offset) composites into the shot alongside the UI.
        captureCamera.cullingMask = 1 << canvas.gameObject.layer;

        RenderMode originalMode = canvas.renderMode;
        Camera originalWorldCamera = canvas.worldCamera;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = captureCamera;

        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        captureCamera.targetTexture = rt;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) slots[i].SetPlayerIndex(i);
        }

        CharacterPreviewStage previewStage = slots[0].GetComponentInChildren<CharacterPreviewStage>(true);

        CharacterSaveData save0 = SaveManager.LoadCharacter(roster[0].characterID);

        slots[0].SetEmpty();
        CaptureToPng(captureCamera, rt, "shot-empty.png");

        slots[0].SetSelecting(roster, 0, d => d.unlockedByDefault, save0);
        // Outside Play mode nothing drives previewCamera's render loop — force one frame so
        // the RenderTexture the RawImage reads from isn't still blank white.
        if (previewStage != null) previewStage.RenderNow();
        CaptureToPng(captureCamera, rt, "shot-selecting.png");

        slots[0].SetReady(roster[0]);
        if (previewStage != null) previewStage.RenderNow();
        CaptureToPng(captureCamera, rt, "shot-ready.png");

        slots[0].SetEmpty();
        canvas.renderMode = originalMode;
        canvas.worldCamera = originalWorldCamera;
        captureCamera.targetTexture = null;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(captureCameraGo);

        Report(OK, "capture character selection done");
    }

    static void CaptureToPng(Camera camera, RenderTexture rt, string fileName)
    {
        camera.Render();

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = previousActive;

        // Same destination as HarnessFixture.Shot() — <project>/screenshots/, not Logs/ or
        // Temp/ (Unity deletes Temp/ on editor quit, taking the proof with it).
        string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "screenshots");
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, fileName);
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        Debug.Log($"CAPTURE|{path}");
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

    /// <summary>
    /// Force distance/height/tilt/yaw on every TopDownCameraController in every scene.
    ///
    /// Level1 and AgentGym are code-generated, so their rigs come from their generators. SampleScene
    /// has no generator and is binary-serialized, so its values are unreachable by text edit and this
    /// is the only way to change them. Written as a sweep rather than a SampleScene special case so a
    /// scene added later cannot quietly keep its own framing.
    /// </summary>
    static void ApplyCameraRigFraming()
    {
        const float distance = 11f;
        const float height = 0f;
        const float tilt = 55f;
        // Yaw stays 0: movement input is mapped onto world axes with no camera-relative transform,
        // so a non-zero yaw makes "left" travel diagonally on screen.
        const float yaw = 0f;

        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/Scenes/")) continue;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                path, UnityEditor.SceneManagement.OpenSceneMode.Single);

            bool dirty = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var rig in root.GetComponentsInChildren<TopDownCameraController>(true))
                {
                    var so = new SerializedObject(rig);
                    so.FindProperty("distance").floatValue = distance;
                    so.FindProperty("height").floatValue = height;
                    so.FindProperty("tiltAngle").floatValue = tilt;
                    so.FindProperty("rotationAngle").floatValue = yaw;
                    so.ApplyModifiedProperties();

                    // Pre-place at the position LateUpdate solves for a target at the origin, so the
                    // first frame is already settled instead of visibly swinging into place.
                    float tiltRad = tilt * Mathf.Deg2Rad;
                    rig.transform.SetPositionAndRotation(
                        new Vector3(0f, height + distance * Mathf.Sin(tiltRad), -distance * Mathf.Cos(tiltRad)),
                        Quaternion.Euler(tilt, yaw, 0f));

                    changed++;
                    dirty = true;
                    Debug.Log($"DIAG|framed scene={path} cam={rig.gameObject.name}");
                }
            }

            if (!dirty) continue;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        if (changed == 0)
            throw new Exception("No TopDownCameraController found in any scene — nothing was framed.");
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
