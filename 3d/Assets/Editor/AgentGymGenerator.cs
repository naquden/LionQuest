using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Builds the minimal, fully agent-observable gameplay scenes: Assets/Scenes/AgentGym.unity
/// (one player) and Assets/Scenes/AgentGymCoop.unity (two players). Both come from the same builder
/// so they differ only in player count.
///
/// Why this exists: SampleScene.unity is binary-serialized (agents cannot read or diff it) and
/// carries years of hand-made Inspector state, including a scene-baked player instance sitting
/// next to the one PlayerSpawner creates at runtime. Two players meant input drove one object
/// while the HUD was bound to the other, so "HUD says locked but my skills fire" was possible.
/// The gym is regenerated from code every run, saves as text YAML, and contains exactly one
/// player (spawned by PlayerSpawner, never baked), so every fact about it is greppable on disk.
///
/// Deliberately excludes MapGenerator/terrain: ground is a flat collider plane, so nothing in
/// the scene depends on procedural generation finishing before a test asserts. TopDownPlayerController
/// logs one "MapGenerator missing" error in this scene by design — ground-type speed scaling is
/// out of scope for a HUD/skill gym.
///
/// Everything else mirrors the real gameplay scenes on purpose, including the actual
/// TopDownCameraController rig at the same angles SampleScene uses. Substituting a simpler
/// stand-in for a real component is what lets bugs through: the gym previously used a plain fixed
/// Camera, which made it structurally incapable of catching Level1's yaw-45 rig skewing movement
/// input. Prefer the real prefab/component over a convenient approximation here.
///
/// Dual entry point per project convention.
/// </summary>
public static class AgentGymGenerator
{
    const string ScenePath = "Assets/Scenes/AgentGym.unity";
    const string CoopScenePath = "Assets/Scenes/AgentGymCoop.unity";
    const string HudPrefabPath = "Assets/Prefabs/UI/PlayerHUDCanvas.prefab";
    const string PauseMenuPrefabPath = "Assets/Prefabs/UI/PauseMenuCanvas.prefab";
    const string GameControllerPrefabPath = "Assets/Prefabs/GameController.prefab";
    const string FallbackCharacterID = "AssCap";

    [MenuItem("LionQuest/Generate/Agent Gym")]
    public static void Generate() => Build(ScenePath, playerCount: 1);

    /// <summary>
    /// Two-player variant, for verifying the parts of the follow camera that only run with 2+ players:
    /// CalculateCentralizedPosition's averaging and the spread-based zoom-out. A single-player scene
    /// cannot exercise either — with one player the average is that player and the spread is 0 — so
    /// those paths were previously untested in any scene.
    ///
    /// Identical to the single-player gym in every other respect (same ground, camera rig, HUD,
    /// prefabs) so a difference in behaviour between the two is attributable to player count alone.
    /// </summary>
    [MenuItem("LionQuest/Generate/Agent Gym Coop")]
    public static void GenerateCoop() => Build(CoopScenePath, playerCount: 2);

    static void Build(string scenePath, int playerCount)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildLighting();
        Transform[] spawnPoints = BuildGround();
        TopDownCameraController followCamera = BuildCamera();

        GameObject gameControllerPrefab = Load<GameObject>(GameControllerPrefabPath);
        if (gameControllerPrefab != null)
        {
            PrefabUtility.InstantiatePrefab(gameControllerPrefab, scene);
        }
        else
        {
            Debug.LogError($"AgentGymGenerator: {GameControllerPrefabPath} missing — CharacterStats.OnEnable will throw.");
        }

        PlayerHUD hud = InstantiateComponentPrefab<PlayerHUD>(HudPrefabPath, scene,
            "run 'LionQuest/Generate/Player HUD' first");
        PauseMenuController pauseMenu = InstantiateComponentPrefab<PauseMenuController>(PauseMenuPrefabPath, scene,
            "run 'LionQuest/Generate/Pause Menu' first");

        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        BuildSpawner(spawnPoints, hud, pauseMenu, followCamera, playerCount);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();

        RegisterInBuildSettings(scenePath);

        Debug.Log($"AgentGymGenerator: wrote {scenePath} ({playerCount} player(s))");
    }

    static void BuildLighting()
    {
        var lightGo = new GameObject("Directional Light", typeof(Light));
        Light light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    /// <summary>
    /// Flat collider ground plus the four spawn points PlayerSpawner uses.
    ///
    /// A Cube, not a Plane. A Plane's MeshCollider is a single zero-thickness quad, and a
    /// gravity-driven capsule resting exactly on it can occasionally be resolved to the wrong side and
    /// fall through — which showed up as a rare GroundStandingTests failure with the player at
    /// y = -2.3 instead of its usual 0.5. A box gives the solver real depth to push back out of. Scaled
    /// and sunk so its top face is at y = 0, matching the Plane it replaces.
    /// </summary>
    const float GroundThickness = 2f;

    static Transform[] BuildGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -GroundThickness * 0.5f, 0f);
        ground.transform.localScale = new Vector3(60f, GroundThickness, 60f); // 60x60 units, as before

        var pointsParent = new GameObject("SpawnPoints").transform;
        Vector3[] offsets =
        {
            new Vector3(-3f, 0f, -3f), new Vector3(3f, 0f, -3f),
            new Vector3(-3f, 0f, 3f), new Vector3(3f, 0f, 3f),
        };
        var spawnPoints = new Transform[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            var point = new GameObject($"SpawnPoint_{i}").transform;
            point.SetParent(pointsParent, false);
            point.position = offsets[i];
            spawnPoints[i] = point;
        }
        return spawnPoints;
    }

    /// <summary>
    /// The real TopDownCameraController rig, configured to match every other player-following scene
    /// (distance 11, height 0, tilt 55, yaw 0). Those numbers are deliberately identical across
    /// Level1/AgentGym/SampleScene: the gym only proves something about the game if it frames the
    /// player the same way the game does.
    ///
    /// This used to be a plain fixed Camera, on the theory that a follow camera drifts between
    /// frames and would make two screenshots of the same HUD state differ. That traded away more
    /// than it bought: input direction is only meaningful *relative to the camera*, so a gym with
    /// a different camera rig than the game cannot verify movement at all. It let a real bug ship —
    /// Level1 was built at yaw 45 while TopDownPlayerController maps stick input onto world axes
    /// with no camera-relative transform, so "left" walked down-left diagonally on screen, and no
    /// gym test could have caught it.
    ///
    /// Yaw MUST stay 0 here and in every gameplay scene until movement is made camera-relative;
    /// that coupling is the thing under test, so the gym has to share it.
    ///
    /// For screenshot stability: the rig is pre-placed at the exact position LateUpdate solves for
    /// at the origin, so with a stationary player SmoothDamp has nothing to correct and the camera
    /// holds still. Tests that move the player should settle a few frames before capturing.
    /// </summary>
    static TopDownCameraController BuildCamera()
    {
        // One AudioListener on the shared camera — see the note in Level1Generator.BuildCamera.
        var cameraGo = new GameObject("Main Camera",
            typeof(Camera), typeof(TopDownCameraController), typeof(AudioListener));
        cameraGo.tag = "MainCamera";

        Camera camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.05f;

        // Same framing constants as Level1/SampleScene — see the note on this method.
        const float tilt = 55f;
        const float distance = 11f;
        const float height = 0f;

        var controller = cameraGo.GetComponent<TopDownCameraController>();
        var so = new SerializedObject(controller);
        so.FindProperty("autoFindPlayers").boolValue = false;
        so.FindProperty("distance").floatValue = distance;
        so.FindProperty("height").floatValue = height;
        so.FindProperty("followSpeed").floatValue = 8f;
        so.FindProperty("tiltAngle").floatValue = tilt;
        so.FindProperty("rotationAngle").floatValue = 0f;
        so.FindProperty("useOrthographic").boolValue = false;
        so.FindProperty("fieldOfView").floatValue = 60f;
        so.FindProperty("minPlayerDistance").floatValue = 0f;
        so.FindProperty("maxPlayerDistance").floatValue = 24f;
        so.FindProperty("distanceMultiplier").floatValue = 0.5f;
        // Same ceiling as Level1 — see the note there. The gym's ground is only 60 units wide, so
        // this is never reached here; it matches the real scenes so the zoom behaves identically.
        so.FindProperty("maxCameraDistance").floatValue = 90f;
        so.ApplyModifiedProperties();

        // Same solve as UpdateCameraPosition for a target at the origin, so frame 1 is already settled.
        float tiltRad = tilt * Mathf.Deg2Rad;
        var offset = new Vector3(0f, height + distance * Mathf.Sin(tiltRad), -distance * Mathf.Cos(tiltRad));
        cameraGo.transform.SetPositionAndRotation(offset, Quaternion.Euler(tilt, 0f, 0f));

        return controller;
    }

    static void BuildSpawner(Transform[] spawnPoints, PlayerHUD hud, PauseMenuController pauseMenu,
        TopDownCameraController followCamera, int playerCount)
    {
        var spawnerGo = new GameObject("PlayerSpawner", typeof(PlayerSpawner));
        var spawner = spawnerGo.GetComponent<PlayerSpawner>();

        CharacterDefinition[] roster = DiscoverDefinitions();
        CharacterDefinition fallback = null;
        foreach (CharacterDefinition def in roster)
        {
            if (def != null && def.characterID == FallbackCharacterID) fallback = def;
        }
        if (fallback == null)
        {
            Debug.LogError($"AgentGymGenerator: no CharacterDefinition with characterID '{FallbackCharacterID}' found — " +
                           "the gym cannot spawn a player.");
        }

        var so = new SerializedObject(spawner);

        var spawnPointsProp = so.FindProperty("spawnPoints");
        spawnPointsProp.arraySize = spawnPoints.Length;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            spawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        }

        var rosterProp = so.FindProperty("allCharacters");
        rosterProp.arraySize = roster.Length;
        for (int i = 0; i < roster.Length; i++)
        {
            rosterProp.GetArrayElementAtIndex(i).objectReferenceValue = roster[i];
        }

        so.FindProperty("fallbackDefinition").objectReferenceValue = fallback;
        so.FindProperty("gameControllerPrefab").objectReferenceValue = Load<GameObject>(GameControllerPrefabPath);
        so.FindProperty("hud").objectReferenceValue = hud;
        so.FindProperty("pauseMenu").objectReferenceValue = pauseMenu;
        // Players only exist at runtime, so the rig's players array starts empty and PlayerSpawner
        // registers each spawn via AddPlayer — same wiring as Level1.
        so.FindProperty("followCamera").objectReferenceValue = followCamera;
        // No CharacterSelectionData exists when the scene is entered directly, so the spawner takes
        // its fallback path — this is what decides how many players the scene actually contains.
        so.FindProperty("fallbackPlayerCount").intValue = playerCount;
        so.ApplyModifiedProperties();
    }

    static T InstantiateComponentPrefab<T>(string path, UnityEngine.SceneManagement.Scene scene, string hint)
        where T : Component
    {
        GameObject prefab = Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"AgentGymGenerator: {path} missing — {hint}.");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var component = instance.GetComponent<T>();
        if (component == null)
        {
            Debug.LogError($"AgentGymGenerator: {path} has no {typeof(T).Name} component.");
        }
        return component;
    }

    static CharacterDefinition[] DiscoverDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterDefinition");
        var list = new System.Collections.Generic.List<CharacterDefinition>(guids.Length);
        foreach (string guid in guids)
        {
            var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def != null) list.Add(def);
        }
        CharacterSelectionGenerator.SortRoster(list);
        return list.ToArray();
    }

    static void RegisterInBuildSettings(string scenePath)
    {
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (existing.path == scenePath) return;
        }

        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
        {
            new EditorBuildSettingsScene(scenePath, true),
        };
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
