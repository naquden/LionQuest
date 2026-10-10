using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;

/// <summary>
/// Builds Assets/Scenes/Level1.unity — the first real playable level, and the gameplay scene
/// CharacterSelection now starts.
///
/// Why this exists instead of reusing SampleScene: SampleScene.unity is binary-serialized, so an
/// agent can neither read nor diff it, and it carries years of hand-made Inspector state (that is
/// exactly how the gamepad-pairing bug stayed hidden — a scene-baked player was accidentally
/// holding the keyboard). Level1 is regenerated from code, saves as text YAML, and contains no
/// baked player: PlayerSpawner creates the only one at runtime.
///
/// Unlike AgentGym (flat plane, fixed camera, no enemies — a HUD/skill test rig), Level1 is a real
/// level: procedural island terrain via MapGenerator, a TopDownCameraController following the
/// spawned players, and enemies placed on the island.
///
/// Dual entry point per project convention.
/// </summary>
public static class Level1Generator
{
    const string ScenePath = "Assets/Scenes/Level1.unity";
    const string TerrainDataPath = "Assets/Terrain/Level1Terrain.asset";
    const string HudPrefabPath = "Assets/Prefabs/UI/PlayerHUDCanvas.prefab";
    const string PauseMenuPrefabPath = "Assets/Prefabs/UI/PauseMenuCanvas.prefab";
    const string GameControllerPrefabPath = "Assets/Prefabs/GameController.prefab";
    const string EnemyPrefabPath = "Assets/Characters/enemy/EnemyHero Variant.prefab";
    const string VolumeProfilePath = "Assets/Settings/SampleSceneProfile.asset";
    const string ShaderDir = "Assets/Shaders";
    const string TerrainDir = "Assets/Terrain";
    const string DetailDir = "Assets/Terrain/Details";
    const string FallbackCharacterID = "AssCap";

    // Follow-camera framing. Shared with every other scene that follows a player (AgentGym,
    // SampleScene) and with TopDownCameraController's own field defaults — the rig must frame the
    // player identically everywhere, or a flow verified in one scene proves nothing about another.
    const float CameraDistance = 11f;
    const float CameraHeight = 0f;
    const float CameraTilt = 55f;

    // Terrain layout. The island is centred on world origin: MapGenerator always spans
    // terrainOrigin..terrainOrigin+mapSize, and its surface sits oceanDepth+heightAmplitude above
    // terrainOrigin.y, so terrainOrigin is offset by half the map on x/z and by the island height
    // on y. Without that, spawn points near the origin would land in the ocean corner, and
    // TopDownPlayerController.Awake (which force-clamps spawn y to groundLevel = 0) would drop the
    // player under the terrain.
    const int MapSize = 280;
    const float TerrainHeight = 24f;
    const float IslandRadius = 85f;
    const float OceanDepth = 8f;
    const float HeightAmplitude = 4f;
    const float BeachWidth = 10f;
    const int HeightmapResolution = 1025;

    const string RegionDir = "Assets/Data/Regions";
    const string ArchetypeDir = "Assets/Data/EnemyArchetypes";

    static Vector3 TerrainOrigin =>
        new Vector3(-MapSize * 0.5f, -(OceanDepth + HeightAmplitude), -MapSize * 0.5f);

    [MenuItem("LionQuest/Generate/Level 1")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildLighting();
        BuildVolume();
        RegionDefinition[] regions = BuildRegions();
        MapGenerator mapGenerator = BuildTerrain(regions);
        Transform[] spawnPoints = BuildSpawnPoints();
        TopDownCameraController followCamera = BuildCamera();
        Transform openingWaveParent = SpawnEnemies();
        BuildWaveSpawner(openingWaveParent, mapGenerator, regions);

        GameObject gameControllerPrefab = Load<GameObject>(GameControllerPrefabPath);
        if (gameControllerPrefab != null)
        {
            PrefabUtility.InstantiatePrefab(gameControllerPrefab, scene);
        }
        else
        {
            Debug.LogError($"Level1Generator: {GameControllerPrefabPath} missing — CharacterStats.OnEnable will throw.");
        }

        PlayerHUD hud = InstantiateComponentPrefab<PlayerHUD>(HudPrefabPath, scene,
            "run 'LionQuest/Generate/Player HUD' first");
        PauseMenuController pauseMenu = InstantiateComponentPrefab<PauseMenuController>(PauseMenuPrefabPath, scene,
            "run 'LionQuest/Generate/Pause Menu' first");

        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        BuildSpawner(spawnPoints, hud, pauseMenu, followCamera, mapGenerator);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        RegisterInBuildSettings();

        Debug.Log($"Level1Generator: wrote {ScenePath}");
    }

    static void BuildLighting()
    {
        var lightGo = new GameObject("Directional Light", typeof(Light));
        Light light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.97f, 0.9f);
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
    }

    static void BuildVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            Debug.LogError($"Level1Generator: volume profile not found at {VolumeProfilePath}.");
            return;
        }

        var go = new GameObject("Global Volume", typeof(Volume));
        Volume volume = go.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    /// <summary>
    /// Idempotent load-or-create of the 5 difficulty-band RegionDefinition assets, so re-running
    /// this generator always converges every field back to these locked values rather than drifting
    /// from whatever an earlier iteration or manual Inspector edit left behind. eligibleArchetypes
    /// entries are loaded from Assets/Data/EnemyArchetypes — if 'LionQuest/Generate/Enemy Archetypes'
    /// hasn't run yet, those slots stay empty and restrictArchetypes silently excludes everything
    /// for that region until it's re-run.
    /// </summary>
    static RegionDefinition[] BuildRegions()
    {
        UiTheme.EnsureFolder(RegionDir);

        var rangedArchetype = AssetDatabase.LoadAssetAtPath<EnemyArchetype>($"{ArchetypeDir}/Ranged.asset");
        var eliteArchetype = AssetDatabase.LoadAssetAtPath<EnemyArchetype>($"{ArchetypeDir}/Elite.asset");
        var bossArchetype = AssetDatabase.LoadAssetAtPath<EnemyArchetype>($"{ArchetypeDir}/Boss.asset");

        var specs = new[]
        {
            new RegionSpec("StartMeadow", "Start Meadow", 0.00f, 0.20f, 1.0f, 1.0f, false, null),
            new RegionSpec("Wildwood", "Wildwood", 0.20f, 0.42f, 1.3f, 1.0f, false, null),
            new RegionSpec("AshenFlats", "Ashen Flats", 0.42f, 0.65f, 1.6f, 1.3f, false, null),
            new RegionSpec("ShatteredReach", "Shattered Reach", 0.65f, 0.85f, 2.0f, 1.6f, true,
                new[] { rangedArchetype, eliteArchetype, bossArchetype }),
            new RegionSpec("TheMaw", "The Maw", 0.85f, 1.00f, 2.5f, 2.0f, true,
                new[] { eliteArchetype, bossArchetype }),
        };

        var result = new RegionDefinition[specs.Length];
        for (int i = 0; i < specs.Length; i++)
        {
            string path = $"{RegionDir}/{specs[i].fileName}.asset";
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(path);
            if (region == null)
            {
                region = ScriptableObject.CreateInstance<RegionDefinition>();
                AssetDatabase.CreateAsset(region, path);
            }

            region.regionName = specs[i].displayName;
            region.minFraction = specs[i].minFraction;
            region.maxFraction = specs[i].maxFraction;
            region.densityMultiplier = specs[i].density;
            region.holeDensityMultiplier = specs[i].holeDensity;
            region.restrictArchetypes = specs[i].restrict;
            region.eligibleArchetypes = specs[i].restrict
                ? System.Array.FindAll(specs[i].archetypes, a => a != null)
                : System.Array.Empty<EnemyArchetype>();

            EditorUtility.SetDirty(region);
            result[i] = region;
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    struct RegionSpec
    {
        public string fileName;
        public string displayName;
        public float minFraction;
        public float maxFraction;
        public float density;
        public float holeDensity;
        public bool restrict;
        public EnemyArchetype[] archetypes;

        public RegionSpec(string fileName, string displayName, float minFraction, float maxFraction,
            float density, float holeDensity, bool restrict, EnemyArchetype[] archetypes)
        {
            this.fileName = fileName;
            this.displayName = displayName;
            this.minFraction = minFraction;
            this.maxFraction = maxFraction;
            this.density = density;
            this.holeDensity = holeDensity;
            this.restrict = restrict;
            this.archetypes = archetypes;
        }
    }

    /// <summary>
    /// Procedural island terrain. Its TerrainData is a dedicated asset (not SampleScene's) so
    /// regenerating Level1 can never corrupt another scene's terrain, and generation runs here at
    /// edit time with generateOnStart left ON — MapGenerator.Awake regenerates at runtime, which
    /// keeps the scene file free of a baked 280x280 heightmap blob.
    /// </summary>
    static MapGenerator BuildTerrain(RegionDefinition[] regions)
    {
        TerrainData terrainData = Load<TerrainData>(TerrainDataPath);
        if (terrainData == null)
        {
            terrainData = new TerrainData();
            AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
        }

        // Must be set before terrainData.size (inside GenerateMap) — Unity resamples the heightmap
        // to whatever resolution is current at the time size is assigned.
        terrainData.heightmapResolution = HeightmapResolution;

        var terrainGo = new GameObject("Terrain", typeof(Terrain), typeof(TerrainCollider), typeof(MapGenerator));
        terrainGo.transform.position = TerrainOrigin;

        Terrain terrain = terrainGo.GetComponent<Terrain>();
        terrain.terrainData = terrainData;
        terrainGo.GetComponent<TerrainCollider>().terrainData = terrainData;

        MapGenerator generator = terrainGo.GetComponent<MapGenerator>();
        var so = new SerializedObject(generator);

        so.FindProperty("mapSize").vector2IntValue = new Vector2Int(MapSize, MapSize);
        so.FindProperty("terrainHeight").floatValue = TerrainHeight;
        so.FindProperty("terrainOrigin").vector3Value = TerrainOrigin;
        so.FindProperty("flatTerrain").boolValue = false;
        so.FindProperty("heightAmplitude").floatValue = HeightAmplitude;
        so.FindProperty("islandRadius").floatValue = IslandRadius;
        so.FindProperty("oceanDepth").floatValue = OceanDepth;
        so.FindProperty("beachWidth").floatValue = BeachWidth;
        so.FindProperty("pathCount").intValue = 3;
        so.FindProperty("pathWidth").floatValue = 6f;
        // Holes are fall-through pits. A few make the level readable; SampleScene's 10 on a smaller
        // island left almost no safe ground.
        so.FindProperty("holeCount").intValue = 4;
        so.FindProperty("minHoleSize").floatValue = 2f;
        so.FindProperty("maxHoleSize").floatValue = 4f;
        // Shallower than the waterline drop, or the water quad renders inside the pit.
        so.FindProperty("holeDepth").floatValue = 1.2f;
        so.FindProperty("generateOnStart").boolValue = true;

        var regionsProp = so.FindProperty("regions");
        regionsProp.arraySize = regions.Length;
        for (int i = 0; i < regions.Length; i++)
        {
            regionsProp.GetArrayElementAtIndex(i).objectReferenceValue = regions[i];
        }

        so.FindProperty("terrainMaterial").objectReferenceValue = Load<Material>($"{ShaderDir}/StylizedTerrain.mat");
        so.FindProperty("waterMaterial").objectReferenceValue = Load<Material>($"{ShaderDir}/StylizedWater.mat");
        so.FindProperty("sandGroundType").objectReferenceValue = Load<GroundType>($"{TerrainDir}/Sand.asset");

        // Exactly 4 ground types — the URP terrain shader renders at most 4 layers in one pass.
        string[] groundTypeNames = { "Grass", "Dirt", "Hole", "Sand" };
        var groundTypes = so.FindProperty("availableGroundTypes");
        groundTypes.arraySize = groundTypeNames.Length;
        for (int i = 0; i < groundTypeNames.Length; i++)
        {
            groundTypes.GetArrayElementAtIndex(i).objectReferenceValue =
                Load<GroundType>($"{TerrainDir}/{groundTypeNames[i]}.asset");
        }

        string[] detailNames = { "GrassTuftA", "GrassTuftB", "GrassTuftC", "FlowerYellow", "FlowerWhite" };
        var details = so.FindProperty("detailTextures");
        details.arraySize = detailNames.Length;
        for (int i = 0; i < detailNames.Length; i++)
        {
            details.GetArrayElementAtIndex(i).objectReferenceValue = Load<Texture2D>($"{DetailDir}/{detailNames[i]}.png");
        }

        so.ApplyModifiedProperties();

        // Generate now so this run fails loudly on a broken heightmap/alphamap rather than only at
        // play time, and so the saved TerrainData is viewable in the editor.
        generator.GenerateMap();

        return generator;
    }

    /// <summary>
    /// Four spawn points at y = 0 near world origin, i.e. the island's centre plateau. y must be 0:
    /// TopDownPlayerController.Awake overwrites the spawn y with its own groundLevel (0), so any
    /// other height here is silently discarded — the terrain is offset to meet it instead.
    /// </summary>
    static Transform[] BuildSpawnPoints()
    {
        var parent = new GameObject("SpawnPoints").transform;
        Vector3[] offsets =
        {
            new Vector3(-3f, 0f, -3f), new Vector3(3f, 0f, -3f),
            new Vector3(-3f, 0f, 3f), new Vector3(3f, 0f, 3f),
        };

        var points = new Transform[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            var point = new GameObject($"SpawnPoint_{i}").transform;
            point.SetParent(parent, false);
            point.position = offsets[i];
            points[i] = point;
        }
        return points;
    }

    /// <summary>
    /// Isometric follow camera. Its players array is left empty on purpose — players only exist at
    /// runtime, so PlayerSpawner registers them via AddPlayer (autoFindPlayers stays off: tag
    /// lookups are banned by the project's no-lookup-fallbacks rule). Tagged MainCamera because
    /// TopDownPlayerController falls back to Camera.main for its viewport bounds check.
    /// </summary>
    static TopDownCameraController BuildCamera()
    {
        // AudioListener belongs on the one shared camera, not on players: four listeners in a
        // local-coop scene makes Unity pick one arbitrarily and warn, and no listener at all
        // (the previous state) makes every sound silent no matter how it is played.
        var cameraGo = new GameObject("Main Camera",
            typeof(Camera), typeof(TopDownCameraController), typeof(AudioListener));
        cameraGo.tag = "MainCamera";

        Camera camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 400f;

        var controller = cameraGo.GetComponent<TopDownCameraController>();
        var so = new SerializedObject(controller);
        so.FindProperty("autoFindPlayers").boolValue = false;
        so.FindProperty("distance").floatValue = CameraDistance;
        so.FindProperty("height").floatValue = CameraHeight;
        so.FindProperty("followSpeed").floatValue = 8f;
        so.FindProperty("tiltAngle").floatValue = CameraTilt;
        // Yaw must stay 0: TopDownPlayerController maps stick input straight onto world axes
        // (new Vector3(input.x, 0, input.y)) with no camera-relative transform. Any non-zero yaw
        // shears the mapping — at 45 deg, "left" walked down-left diagonally on screen.
        so.FindProperty("rotationAngle").floatValue = 0f;
        so.FindProperty("useOrthographic").boolValue = false;
        so.FindProperty("fieldOfView").floatValue = 60f;
        so.FindProperty("minPlayerDistance").floatValue = 0f;
        so.FindProperty("maxPlayerDistance").floatValue = 24f;
        so.FindProperty("distanceMultiplier").floatValue = 0.5f;
        // Four players can end up on opposite sides of the 90-unit playable radius, far beyond the
        // 24-unit comfort spread. The ceiling is what actually decides whether the straggler is on
        // screen at all; sized to frame the island's full playable width (same ratio to playable
        // radius as before the region rescale: ~2.4x).
        so.FindProperty("maxCameraDistance").floatValue = 216f;
        so.ApplyModifiedProperties();

        // Pre-place it exactly where LateUpdate solves for a target at the origin, so the first
        // rendered frame (and any screenshot taken before a player exists) already frames the island.
        // Derived rather than hardcoded — a literal here silently goes stale when the constants move.
        float tiltRad = CameraTilt * Mathf.Deg2Rad;
        cameraGo.transform.SetPositionAndRotation(
            new Vector3(0f,
                CameraHeight + CameraDistance * Mathf.Sin(tiltRad),
                -CameraDistance * Mathf.Cos(tiltRad)),
            Quaternion.Euler(CameraTilt, 0f, 0f));

        return controller;
    }

    /// <summary>
    /// Enemies ringed around the island centre, outside the players' spawn cluster but inside
    /// Enemy.pullRange's reach once a player walks toward them. y = 0 for the same reason as spawn
    /// points: the island surface is at world y = 0 by construction.
    ///
    /// Three rings, 24 total. The inner ring alone (the original 6) was fewer enemies than a single
    /// round is expected to clear, so a run could exhaust the level and earn no further souls —
    /// which made the earn-then-spend progression loop untestable and, in game, made the level end
    /// with nothing left to fight. 24 is chosen against the shop: a soul per kill and 20 for a skill
    /// unlock, so clearing the level funds exactly one new skill with a little to spare. Each ring is
    /// offset half a step so they don't line up radially.
    /// </summary>
    /// <returns>The "Enemies" parent transform, so WaveSpawner can watch it for wave-1 clearance —
    /// see BuildWaveSpawner.</returns>
    static Transform SpawnEnemies()
    {
        GameObject enemyPrefab = Load<GameObject>(EnemyPrefabPath);
        if (enemyPrefab == null)
        {
            Debug.LogError($"Level1Generator: {EnemyPrefabPath} missing — Level1 will have no enemies.");
            return null;
        }

        var parent = new GameObject("Enemies").transform;

        // Ring radii are unchanged from the original 120-unit map: they only need to clear
        // Enemy.pullRange (15u) so the player isn't swarmed the instant they spawn, which has
        // nothing to do with IslandRadius or region bands — the opening wave is hand-placed and
        // does not consult MapGenerator.GetRegionAt at all. All three land inside Start Meadow's
        // 0.00-0.20 band anyway (< 18u of the new GetPlayableRadius()=90) except the outermost
        // ring, which spills slightly into Wildwood — harmless, since region membership only
        // matters to WaveSpawner's spawned waves, not this hand-placed one.
        SpawnEnemyRing(enemyPrefab, parent, count: 6, radius: 16f, angleOffset: 0f, nameOffset: 0);
        SpawnEnemyRing(enemyPrefab, parent, count: 8, radius: 20f, angleOffset: Mathf.PI / 8f, nameOffset: 6);
        SpawnEnemyRing(enemyPrefab, parent, count: 10, radius: 24f, angleOffset: Mathf.PI / 10f, nameOffset: 14);

        return parent;
    }

    static void SpawnEnemyRing(GameObject enemyPrefab, Transform parent, int count, float radius,
        float angleOffset, int nameOffset)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = angleOffset + i * Mathf.PI * 2f / count;
            var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            enemy.name = $"Enemy_{nameOffset + i}";
            enemy.transform.SetParent(parent, false);
            enemy.transform.position = position;
            // Face the island centre so the first frame doesn't show them all aligned to +z.
            enemy.transform.rotation = Quaternion.LookRotation(-position.normalized, Vector3.up);
        }
    }

    /// <summary>
    /// Creates the WaveSpawner that takes over once the hand-placed opening wave (openingWaveParent)
    /// is cleared. Its archetype tables (archetypes/bossArchetypes) are left empty here — run
    /// 'LionQuest/Generate/Enemy Archetypes' after this to fill them in, since that generator owns
    /// building the archetype prefabs/assets this one has no reason to know about. Re-running Level 1
    /// alone (e.g. to pick up a terrain change) does not need archetypes rebuilt, which is why the
    /// two stay separate generators rather than one doing both.
    /// </summary>
    static void BuildWaveSpawner(Transform openingWaveParent, MapGenerator mapGenerator, RegionDefinition[] regions)
    {
        var spawnerGo = new GameObject("WaveSpawner", typeof(WaveSpawner));
        var spawner = spawnerGo.GetComponent<WaveSpawner>();
        var so = new SerializedObject(spawner);
        so.FindProperty("openingWaveParent").objectReferenceValue = openingWaveParent;
        so.FindProperty("mapGenerator").objectReferenceValue = mapGenerator;

        var regionsProp = so.FindProperty("regions");
        regionsProp.arraySize = regions.Length;
        for (int i = 0; i < regions.Length; i++)
        {
            regionsProp.GetArrayElementAtIndex(i).objectReferenceValue = regions[i];
        }

        so.ApplyModifiedProperties();
    }

    static void BuildSpawner(Transform[] spawnPoints, PlayerHUD hud, PauseMenuController pauseMenu,
        TopDownCameraController followCamera, MapGenerator mapGenerator)
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
            Debug.LogError($"Level1Generator: no CharacterDefinition with characterID '{FallbackCharacterID}' found — " +
                           "Level1 cannot spawn a player when launched directly.");
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
        so.FindProperty("followCamera").objectReferenceValue = followCamera;
        // Each spawned player gets this via SetMapGenerator — the prefab cannot hold a scene ref and
        // the controller is not allowed to search for one.
        so.FindProperty("mapGenerator").objectReferenceValue = mapGenerator;
        so.ApplyModifiedProperties();

        if (mapGenerator == null)
        {
            Debug.LogError("Level1Generator: terrain build produced no MapGenerator — ground speed and hole " +
                           "detection will be dead in Level1.");
        }
    }

    static T InstantiateComponentPrefab<T>(string path, UnityEngine.SceneManagement.Scene scene, string hint)
        where T : Component
    {
        GameObject prefab = Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"Level1Generator: {path} missing — {hint}.");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var component = instance.GetComponent<T>();
        if (component == null)
        {
            Debug.LogError($"Level1Generator: {path} has no {typeof(T).Name} component.");
        }
        return component;
    }

    static CharacterDefinition[] DiscoverDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterDefinition");
        var list = new List<CharacterDefinition>(guids.Length);
        foreach (string guid in guids)
        {
            var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def != null) list.Add(def);
        }
        // Same order as the selection screen's roster — PlayerSpawner looks characters up by id,
        // but an order that differs between scenes makes any index-based debugging misleading.
        CharacterSelectionGenerator.SortRoster(list);
        return list.ToArray();
    }

    static void RegisterInBuildSettings()
    {
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (existing.path == ScenePath) return;
        }

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
        {
            new EditorBuildSettingsScene(ScenePath, true),
        };
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
