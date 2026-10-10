using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Procedural map generator that creates terrain and biomes
/// Generates logical biome placement (grass, dirt, holes, etc.)
/// </summary>
[RequireComponent(typeof(Terrain))]
public class MapGenerator : MonoBehaviour
{
    [Header("Map Settings")]
    [Tooltip("Size of the map in Unity units")]
    [SerializeField] private Vector2Int mapSize = new Vector2Int(160, 160);
    
    /// <summary>
    /// Gets the map size (public for boundary checking)
    /// </summary>
    public Vector2 GetMapSize()
    {
        return new Vector2(mapSize.x, mapSize.y);
    }
    
    [Tooltip("Height of the terrain")]
    [SerializeField] private float terrainHeight = 20f;

    [Tooltip("World position of the terrain's (0,0) corner. Terrain always spans origin..origin+mapSize, " +
             "so to centre the island on world origin set this to (-mapSize.x/2, -islandTopHeight, -mapSize.y/2). " +
             "Leave at zero to keep the legacy layout used by SampleScene.")]
    [SerializeField] private Vector3 terrainOrigin = Vector3.zero;

    /// <summary>
    /// World-space centre of the island, on the terrain surface. Spawn points and cameras should be
    /// placed relative to this rather than world origin — the terrain spans terrainOrigin..+mapSize,
    /// so the island's middle is nowhere near (0,0,0) unless terrainOrigin says so.
    /// </summary>
    public Vector3 GetIslandCenter()
    {
        return new Vector3(terrainOrigin.x + mapSize.x * 0.5f,
                           terrainOrigin.y + oceanDepth + heightAmplitude,
                           terrainOrigin.z + mapSize.y * 0.5f);
    }

    /// <summary>
    /// Horizontal radius from <see cref="GetIslandCenter"/> that players are kept inside. Reaches the
    /// middle of the sand ring, so a player can walk onto the beach but not into the ocean.
    /// This replaced the per-player camera-viewport leash that used to bound movement: that leash
    /// froze any player who was carried out of frame by a teammate, because both movement axes were
    /// rejected at once and nothing let them walk back in. Containment is a property of the map, not
    /// of where the shared camera happens to be pointing.
    /// </summary>
    public float GetPlayableRadius()
    {
        return islandRadius + beachWidth * 0.5f;
    }

    [Header("Regions")]
    [Tooltip("Difficulty bands by distance from the island centre. Every band is always walkable — " +
             "regions only change how hard survival is, never whether an area is reachable. Leave " +
             "empty to skip all region-based biasing (legacy single-biome behaviour).")]
    [SerializeField] private RegionDefinition[] regions;

    /// <summary>
    /// Resolves which RegionDefinition a world position falls in, by horizontal distance from
    /// GetIslandCenter() as a fraction of GetPlayableRadius(). Returns null if no band matches
    /// (no regions assigned, or the fraction lands in a gap between authored bands).
    /// </summary>
    public RegionDefinition GetRegionAt(Vector3 worldPosition)
    {
        if (regions == null || regions.Length == 0) return null;

        Vector3 center = GetIslandCenter();
        float dx = worldPosition.x - center.x;
        float dz = worldPosition.z - center.z;
        float distance = Mathf.Sqrt(dx * dx + dz * dz);

        float radius = GetPlayableRadius();
        float t = radius > 0f ? Mathf.Clamp01(distance / radius) : 0f;

        foreach (var region in regions)
        {
            if (region == null) continue;
            if (t >= region.minFraction && t < region.maxFraction) return region;
        }

        return null;
    }

    [Header("Terrain Settings")]
    [Tooltip("Generate flat terrain (no height variation)")]
    [SerializeField] private bool flatTerrain = false;

    [Tooltip("Stylized terrain material (LionQuest/StylizedTerrain). Assigned to the terrain at startup.")]
    [SerializeField] private Material terrainMaterial;

    [Tooltip("Amplitude of the rolling height noise, in world units")]
    [SerializeField] private float heightAmplitude = 3.5f;

    [Tooltip("World-space wavelength of the rolling height noise")]
    [SerializeField] private float heightFrequency = 55f;

    [Header("Water — the map is a single island surrounded by ocean")]
    [Tooltip("Radius of the walkable island (grass/paths), centered on the map, in world units")]
    [SerializeField] private float islandRadius = 40f;

    [Tooltip("Depth of the ocean floor below island level, in world units. Must exceed heightAmplitude or the rolling terrain will poke through the water surface.")]
    [SerializeField] private float oceanDepth = 6f;

    [Tooltip("Water surface material (LionQuest/StylizedWater)")]
    [SerializeField] private Material waterMaterial;

    [Tooltip("Sand shoreline GroundType, painted in a ring around the island")]
    [SerializeField] private GroundType sandGroundType;

    [Tooltip("Width of the sandy shoreline ring, in world units")]
    [SerializeField] private float beachWidth = 8f;

    [Header("Details")]
    [Tooltip("Grass and flower detail textures scattered on grass")]
    [SerializeField] private List<Texture2D> detailTextures = new List<Texture2D>();

    [Tooltip("Detail instances per detail-map cell on grass")]
    [SerializeField] private int detailDensity = 6;

    [Header("Biome Generation")]
    [Tooltip("Ground types available for generation")]
    [SerializeField] private List<GroundType> availableGroundTypes = new List<GroundType>();
    
    [Tooltip("Number of winding dirt paths to paint")]
    [SerializeField] private int pathCount = 3;

    [Tooltip("Width of dirt paths, in world units")]
    [SerializeField] private float pathWidth = 6f;

    [Tooltip("Extra gather radius for splat blending — higher is softer biome transitions")]
    [SerializeField] private int blendRadius = 3;

    [Header("Hole Generation")]
    [Tooltip("Number of holes to generate")]
    [SerializeField] private int holeCount = 10;
    
    [Tooltip("Minimum hole size")]
    [SerializeField] private float minHoleSize = 2f;
    
    [Tooltip("Maximum hole size")]
    [SerializeField] private float maxHoleSize = 5f;

    [Tooltip("How deep a hole is dug below the surrounding ground, in world units. Must stay below " +
             "the waterline drop (oceanDepth * 0.25) or the map-wide water quad renders inside the " +
             "pit — that reads as a blue chasm ringed with shore foam, not a hole.")]
    [SerializeField] private float holeDepth = 1.2f;

    [Header("Auto Generation")]
    [Tooltip("Automatically generate map when game starts")]
    [SerializeField] private bool generateOnStart = true;
    
    private Terrain terrain;
    private TerrainData terrainData;
    private GroundType[,] groundTypeMap; // Map of ground types at each position
    private int terrainResolution;
    private int alphamapResolution;
    
    // Runtime lookup for speed blending
    private Dictionary<int, float> layerIndexToSpeed = new Dictionary<int, float>();
    private Dictionary<int, bool> layerIndexToIsHole = new Dictionary<int, bool>();
    private Dictionary<GroundType, int> groundTypeToLayerIndex = new Dictionary<GroundType, int>();

    private Vector2 mapCenter;
    private List<Transform> waterSurfaces = new List<Transform>();

    private const string WaterSurfacePrefix = "WaterSurface_";
    
    private void Awake()
    {
        Debug.Log($"[MapGenerator] Awake running. GenerateOnStart: {generateOnStart}");

        InitializeTerrain();
        
        // Auto-generate map on awake if enabled to ensure it's ready for other scripts
        if (generateOnStart && groundTypeMap == null)
        {
            GenerateMap();
        }
        else if (!generateOnStart)
        {
            Debug.LogWarning("[MapGenerator] Auto-generation is DISABLED. Trying to rebuild runtime data from existing terrain...");
            RebuildRuntimeData();
        }
    }

    /// <summary>
    /// Rebuilds runtime lookup tables (speed, hole checks) from existing terrain data without regenerating the map.
    /// This allows playing with pre-generated or hand-painted maps.
    /// </summary>
    private void RebuildRuntimeData()
    {
        if (terrain == null || terrainData == null)
        {
            Debug.LogError("[MapGenerator] Cannot rebuild runtime data: Terrain not initialized!");
            return;
        }

        if (terrainData.terrainLayers == null || terrainData.terrainLayers.Length == 0)
        {
            Debug.LogWarning("[MapGenerator] No terrain layers found on existing terrain!");
            return;
        }

        // Rebuild the layers from availableGroundTypes so indices match what a generated map
        // would produce. Matching existing layers by diffuse texture instead would mis-assign
        // any two ground types that share a texture.
        BuildTerrainLayers();
    }

    
    private void Start()
    {
    }
    
    /// <summary>
    /// Initializes the Terrain component and TerrainData
    /// </summary>
    private void InitializeTerrain()
    {
        terrain = GetComponent<Terrain>();
        if (terrain == null)
        {
            Debug.LogError("MapGenerator: Terrain component not found! Adding Terrain component...");
            terrain = gameObject.AddComponent<Terrain>();
        }
        
        terrainData = terrain.terrainData;
        if (terrainData == null)
        {
            Debug.Log("MapGenerator: Creating new TerrainData...");
            terrainData = new TerrainData();
            terrain.terrainData = terrainData;
        }

        if (terrainMaterial == null)
        {
            Debug.LogError("MapGenerator: terrainMaterial is not assigned! Run LionQuest/Generate/Terrain Content, then assign Assets/Shaders/StylizedTerrain.mat in the Inspector.");
        }
        else
        {
            terrain.materialTemplate = terrainMaterial;
        }

        // Ensure TerrainCollider exists for physics collision
        TerrainCollider terrainCollider = GetComponent<TerrainCollider>();
        if (terrainCollider == null)
        {
            Debug.Log("MapGenerator: Adding TerrainCollider for physics collision...");
            terrainCollider = gameObject.AddComponent<TerrainCollider>();
            terrainCollider.terrainData = terrainData;
        }
        else if (terrainCollider.terrainData != terrainData)
        {
            // Update collider if terrain data changed
            terrainCollider.terrainData = terrainData;
        }
    }
    
    private int lastGeneratedSeed;

    /// <summary>
    /// Seed of the most recently generated map. Only meaningful after GenerateMap has run.
    /// </summary>
    public int GetLastGeneratedSeed()
    {
        return lastGeneratedSeed;
    }

    /// <summary>
    /// Picks a fresh seed for a new run. Not derived from Unity's global RNG state, so it doesn't
    /// depend on how much randomness earlier code already consumed this session.
    /// </summary>
    private int GenerateRandomSeed()
    {
        return System.Guid.NewGuid().GetHashCode();
    }

    /// <summary>
    /// Generates the entire map (terrain + biomes) with a freshly picked seed. Every run gets a
    /// different layout — retrying after death re-enters this scene and calls this again.
    /// </summary>
    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        GenerateMap(GenerateRandomSeed());
    }

    /// <summary>
    /// Generates the entire map (terrain + biomes) deterministically from the given seed. Height
    /// noise is coordinate-deterministic Perlin and is unaffected by the seed; everything else
    /// that calls Random (path waypoints, hole placement, region ground-type bias) is seeded here.
    /// </summary>
    public void GenerateMap(int seed)
    {
        Random.InitState(seed);
        lastGeneratedSeed = seed;

        // Ensure terrain is initialized
        if (terrain == null || terrainData == null)
        {
            Debug.Log("MapGenerator: Terrain not initialized. Initializing now...");
            InitializeTerrain();

            if (terrain == null || terrainData == null)
            {
                Debug.LogError("MapGenerator: Failed to initialize Terrain!");
                return;
            }
        }

        if (availableGroundTypes == null || availableGroundTypes.Count == 0)
        {
            Debug.LogError("MapGenerator: No ground types assigned! Please assign ground types in the inspector.");
            return;
        }

        Debug.Log($"MapGenerator: Starting map generation with seed {seed}...");

        // Set terrain size
        terrainData.size = new Vector3(mapSize.x, terrainHeight, mapSize.y);
        terrain.transform.position = terrainOrigin;
        
        // Get terrain resolutions
        terrainResolution = terrainData.heightmapResolution;
        alphamapResolution = terrainData.alphamapResolution;

        mapCenter = new Vector2(mapSize.x * 0.5f, mapSize.y * 0.5f);

        // Generate terrain heightmap (flat if enabled) — the ocean basin is carved in here.
        if (flatTerrain)
        {
            GenerateFlatTerrain();
        }
        else
        {
            GenerateTerrainHeight();
        }

        // Generate biome map (island interior: grass + paths)
        GenerateBiomes();

        // Every pass that writes groundTypeMap must run BEFORE the alphamap is built,
        // otherwise the alphamap-based queries (IsHoleAtPosition) can never see it.
        PaintShorelineAndOcean();
        GenerateHoles();

        // Apply textures to terrain
        ApplyTerrainTextures();

        ApplyDetails();
        SpawnOceanSurface();

        Debug.Log("MapGenerator: Map generation complete!");
    }
    
    /// <summary>
    /// Flat island plateau, still dropping to the ocean floor past islandRadius+beachWidth.
    /// </summary>
    private void GenerateFlatTerrain()
    {
        float[,] heights = new float[terrainResolution, terrainResolution];
        float baseline = (oceanDepth + heightAmplitude) / terrainHeight;

        for (int x = 0; x < terrainResolution; x++)
        {
            for (int y = 0; y < terrainResolution; y++)
            {
                float wx = (float)x / terrainResolution * mapSize.x;
                float wy = (float)y / terrainResolution * mapSize.y;
                heights[y, x] = Mathf.Clamp01(baseline - OceanDepressionAt(wx, wy) / terrainHeight);
            }
        }

        terrainData.SetHeights(0, 0, heights);
    }

    /// <summary>
    /// Gentle rolling height from domain-warped multi-octave Perlin over a single central
    /// island, dropping to the ocean floor past islandRadius+beachWidth.
    /// Heights array is indexed [y, x]; groundTypeMap is [x, y].
    /// </summary>
    private void GenerateTerrainHeight()
    {
        float[,] heights = new float[terrainResolution, terrainResolution];

        // Baseline sits above 0 so the ocean basin has room to carve downward.
        float baseline = (oceanDepth + heightAmplitude) / terrainHeight;
        float freq = Mathf.Max(heightFrequency, 0.001f);

        for (int x = 0; x < terrainResolution; x++)
        {
            for (int y = 0; y < terrainResolution; y++)
            {
                float wx = (float)x / terrainResolution * mapSize.x;
                float wy = (float)y / terrainResolution * mapSize.y;

                // Domain warp: offset the sample point by noise so ridges meander
                // instead of forming the regular lattice plain Perlin produces.
                float warpX = Mathf.PerlinNoise(wx / freq * 2.3f + 11.7f, wy / freq * 2.3f + 3.1f) - 0.5f;
                float warpY = Mathf.PerlinNoise(wx / freq * 2.3f + 41.3f, wy / freq * 2.3f + 27.9f) - 0.5f;

                float sx = (wx + warpX * freq * 0.6f) / freq;
                float sy = (wy + warpY * freq * 0.6f) / freq;

                float n = Mathf.PerlinNoise(sx, sy) * 0.6f
                        + Mathf.PerlinNoise(sx * 2.1f, sy * 2.1f) * 0.3f
                        + Mathf.PerlinNoise(sx * 4.3f, sy * 4.3f) * 0.1f;

                float height = baseline + (n - 0.5f) * (heightAmplitude / terrainHeight);
                heights[y, x] = Mathf.Clamp01(height - OceanDepressionAt(wx, wy) / terrainHeight);
            }
        }

        terrainData.SetHeights(0, 0, heights);
    }

    /// <summary>
    /// GenerateMap is reachable from the Inspector context menu, where Destroy() throws.
    /// </summary>
    private static void DestroySafely(Object target)
    {
        if (target == null) return;

        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    /// <summary>
    /// World Y of the ocean surface: below the lowest dry land, above the ocean floor.
    /// </summary>
    private float WaterSurfaceHeight()
    {
        float baseline = oceanDepth + heightAmplitude;
        float lowestDryLand = baseline - heightAmplitude * 0.5f;
        float oceanFloor = baseline - oceanDepth;

        return Mathf.Lerp(oceanFloor, lowestDryLand, 0.75f);
    }

    /// <summary>
    /// How far below ground level the ocean basin pulls the terrain at a world position.
    /// Smoothstep falloff from the island edge out to the map border, so everything past
    /// islandRadius+beachWidth is fully submerged well before the terrain boundary.
    /// </summary>
    private float OceanDepressionAt(float worldX, float worldZ)
    {
        float distance = Vector2.Distance(new Vector2(worldX, worldZ), mapCenter);
        float shoreStart = islandRadius + beachWidth;

        // Widest map edge distance from center, so the transition always finishes inside bounds.
        float mapEdge = Mathf.Max(mapSize.x, mapSize.y) * 0.5f;
        float falloffWidth = Mathf.Max(1f, mapEdge - shoreStart);

        float t = Mathf.Clamp01((distance - shoreStart) / falloffWidth);
        return oceanDepth * Mathf.SmoothStep(0f, 1f, t);
    }

    /// <summary>
    /// Paints sand in a ring around the island edge, then ocean-floor GroundType (the hole
    /// type, non-walkable) everywhere past the beach. Must run before ApplyTerrainTextures
    /// so the alphamap picks it up.
    /// </summary>
    private void PaintShorelineAndOcean()
    {
        GroundType oceanFloorType = GetHoleGroundType();

        for (int x = 0; x < terrainResolution; x++)
        {
            for (int y = 0; y < terrainResolution; y++)
            {
                float wx = (float)x / terrainResolution * mapSize.x;
                float wy = (float)y / terrainResolution * mapSize.y;
                float distance = Vector2.Distance(new Vector2(wx, wy), mapCenter);

                if (distance > islandRadius + beachWidth)
                {
                    if (oceanFloorType != null) groundTypeMap[x, y] = oceanFloorType;
                }
                else if (distance > islandRadius && sandGroundType != null)
                {
                    groundTypeMap[x, y] = sandGroundType;
                }
            }
        }
    }

    /// <summary>
    /// Spawns a single ocean plane covering the whole map at runtime. Runtime spawning
    /// avoids editing the binary-serialized scene.
    /// </summary>
    private void SpawnOceanSurface()
    {
        if (waterMaterial == null)
        {
            Debug.LogError("MapGenerator: waterMaterial is not assigned! Run LionQuest/Generate/Terrain Content, then assign Assets/Shaders/StylizedWater.mat in the Inspector.");
            return;
        }

        // Sweep by name, not just the tracked list — the list is empty after a domain reload
        // but a surface from a previous generate may still be parented under the terrain.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith(WaterSurfacePrefix)) DestroySafely(child.gameObject);
        }
        waterSurfaces.Clear();

        // The rolling noise spans baseline +/- heightAmplitude/2, so the waterline must sit
        // below the lowest dry land or the ocean visibly floods the island.
        float waterY = WaterSurfaceHeight();

        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surface.name = $"{WaterSurfacePrefix}0";
        surface.transform.SetParent(transform, false);
        // localPosition, not position: the terrain sits at terrainOrigin, and mapCenter/waterY are
        // both terrain-local. Setting world position here would offset the water by terrainOrigin.
        surface.transform.localPosition = new Vector3(mapCenter.x, waterY, mapCenter.y);
        surface.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Oversize past the map bounds so there's no visible seam at the terrain edge.
        float span = Mathf.Max(mapSize.x, mapSize.y) * 1.4f;
        surface.transform.localScale = new Vector3(span, span, 1f);

        DestroySafely(surface.GetComponent<Collider>());
        surface.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;

        waterSurfaces.Add(surface.transform);
    }

    /// <summary>
    /// Scatters grass and flower details, dense on grass and absent on sand, holes and water.
    /// </summary>
    private void ApplyDetails()
    {
        if (detailTextures == null || detailTextures.Count == 0) return;

        List<DetailPrototype> prototypes = new List<DetailPrototype>();
        foreach (var texture in detailTextures)
        {
            if (texture == null) continue;

            prototypes.Add(new DetailPrototype
            {
                prototypeTexture = texture,
                renderMode = DetailRenderMode.GrassBillboard,
                usePrototypeMesh = false,
                healthyColor = Color.white,
                dryColor = new Color(0.85f, 0.82f, 0.55f),
                minWidth = 0.6f,
                maxWidth = 1.3f,
                minHeight = 0.5f,
                maxHeight = 1.1f,
                noiseSpread = 0.3f
            });
        }

        if (prototypes.Count == 0) return;

        terrainData.detailPrototypes = prototypes.ToArray();
        terrainData.SetDetailResolution(512, 16);
        terrainData.wavingGrassStrength = 0.35f;
        terrainData.wavingGrassSpeed = 0.4f;
        terrainData.wavingGrassAmount = 0.35f;
        terrainData.wavingGrassTint = new Color(0.8f, 0.85f, 0.7f);

        int detailRes = terrainData.detailResolution;

        for (int layer = 0; layer < prototypes.Count; layer++)
        {
            int[,] map = new int[detailRes, detailRes];

            for (int x = 0; x < detailRes; x++)
            {
                for (int y = 0; y < detailRes; y++)
                {
                    int hx = Mathf.Clamp(Mathf.RoundToInt((float)x / detailRes * terrainResolution), 0, terrainResolution - 1);
                    int hy = Mathf.Clamp(Mathf.RoundToInt((float)y / detailRes * terrainResolution), 0, terrainResolution - 1);

                    GroundType type = groundTypeMap[hx, hy];
                    if (type == null || type.isHole || type == sandGroundType) continue;

                    // Break the scatter into clumps rather than a uniform carpet.
                    float clump = Mathf.PerlinNoise(x * 0.05f + layer * 13.3f, y * 0.05f + layer * 7.1f);
                    if (clump < 0.45f) continue;

                    map[y, x] = Random.Range(0, Mathf.Max(1, detailDensity));
                }
            }

            terrainData.SetDetailLayer(0, 0, layer, map);
        }

        Debug.Log($"MapGenerator: Applied {prototypes.Count} detail prototypes.");
    }
    
    /// <summary>
    /// Generates biome regions with logical placement
    /// </summary>
    private void GenerateBiomes()
    {
        groundTypeMap = new GroundType[terrainResolution, terrainResolution];

        GroundType baseType = GetBaseGroundType();
        if (baseType == null)
        {
            Debug.LogError("MapGenerator: No walkable base ground type available!");
            return;
        }

        for (int x = 0; x < terrainResolution; x++)
        {
            for (int y = 0; y < terrainResolution; y++)
            {
                float wx = (float)x / terrainResolution * mapSize.x;
                float wy = (float)y / terrainResolution * mapSize.y;
                groundTypeMap[x, y] = PickBiasedGroundType(wx, wy, baseType);
            }
        }

        PaintPaths();

        Debug.Log($"MapGenerator: Base ground '{baseType.groundName}' + {pathCount} paths. Ground type map size: {terrainResolution}x{terrainResolution}");
    }

    /// <summary>
    /// Picks a ground type for a base-layer cell, nudged toward a region's groundBias entries if
    /// the cell falls in a region that defines any. Falls back to fallback when the region has no
    /// bias, has none assigned, or the weighted roll lands on nothing (zero total weight).
    /// </summary>
    private GroundType PickBiasedGroundType(float worldX, float worldY, GroundType fallback)
    {
        RegionDefinition region = GetRegionAt(new Vector3(terrainOrigin.x + worldX, 0f, terrainOrigin.z + worldY));
        if (region == null || region.groundBias == null || region.groundBias.Length == 0) return fallback;

        float total = 0f;
        foreach (var bias in region.groundBias)
        {
            if (bias.type != null) total += bias.weight;
        }
        if (total <= 0f) return fallback;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        foreach (var bias in region.groundBias)
        {
            if (bias.type == null) continue;
            cumulative += bias.weight;
            if (roll <= cumulative) return bias.type;
        }

        return fallback;
    }

    /// <summary>
    /// Paints winding dirt paths as Catmull-Rom splines through random waypoints.
    /// Reads as a deliberate route rather than the random blobs this replaced.
    /// </summary>
    private void PaintPaths()
    {
        GroundType pathType = GetPathGroundType();
        if (pathType == null) return;

        for (int p = 0; p < pathCount; p++)
        {
            List<Vector2> waypoints = new List<Vector2>();

            // Cross the map edge-to-edge so paths read as routes, not stubs.
            bool horizontal = Random.value < 0.5f;
            int segments = Random.Range(4, 7);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float along = Mathf.Lerp(-0.1f, 1.1f, t);
                float across = Random.Range(0.2f, 0.8f);

                waypoints.Add(horizontal
                    ? new Vector2(along * mapSize.x, across * mapSize.y)
                    : new Vector2(across * mapSize.x, along * mapSize.y));
            }

            // Duplicate the ends so Catmull-Rom has control points for the first/last span.
            waypoints.Insert(0, waypoints[0]);
            waypoints.Add(waypoints[waypoints.Count - 1]);

            float halfWidth = pathWidth * 0.5f;
            int steps = terrainResolution * 2;

            for (int s = 0; s <= steps; s++)
            {
                float u = (float)s / steps * (waypoints.Count - 3);
                int seg = Mathf.Clamp(Mathf.FloorToInt(u), 0, waypoints.Count - 4);
                float local = u - seg;

                Vector2 point = CatmullRom(waypoints[seg], waypoints[seg + 1], waypoints[seg + 2], waypoints[seg + 3], local);
                StampCircle(point, halfWidth, pathType);
            }
        }
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    /// <summary>
    /// Stamps a filled circle of a ground type into groundTypeMap at a world position.
    /// </summary>
    private void StampCircle(Vector2 worldCenter, float worldRadius, GroundType type)
    {
        float cellsPerUnitX = (float)terrainResolution / mapSize.x;
        float cellsPerUnitY = (float)terrainResolution / mapSize.y;

        int cx = Mathf.RoundToInt(worldCenter.x * cellsPerUnitX);
        int cy = Mathf.RoundToInt(worldCenter.y * cellsPerUnitY);
        int rx = Mathf.Max(1, Mathf.RoundToInt(worldRadius * cellsPerUnitX));
        int ry = Mathf.Max(1, Mathf.RoundToInt(worldRadius * cellsPerUnitY));

        for (int dx = -rx; dx <= rx; dx++)
        {
            for (int dy = -ry; dy <= ry; dy++)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx < 0 || nx >= terrainResolution || ny < 0 || ny >= terrainResolution) continue;

                float ellipse = (float)(dx * dx) / (rx * rx) + (float)(dy * dy) / (ry * ry);
                if (ellipse <= 1f) groundTypeMap[nx, ny] = type;
            }
        }
    }
    
    /// <summary>
    /// Digs the fall-through holes: paints the hole ground type and sinks the heightmap a fixed
    /// holeDepth below the ground it started at. Depth is RELATIVE — an absolute drop to 0 puts the
    /// hole floor below the map-wide water quad, so the pit fills with water and gets ringed by the
    /// shader's shore foam, reading as a blue chasm instead of a pit. Holes are also kept out of the
    /// ocean/shoreline: a hole below the waterline is invisible and unreachable.
    /// </summary>
    private void GenerateHoles()
    {
        GroundType holeType = GetHoleGroundType();
        if (holeType == null)
        {
            Debug.LogWarning("MapGenerator: No hole ground type found. Skipping hole generation.");
            return;
        }

        // Read the whole heightmap once, mutate locally, write once — a per-pixel
        // GetHeights/SetHeights round-trip inside the loop is orders of magnitude slower.
        float[,] heights = terrainData.GetHeights(0, 0, terrainResolution, terrainResolution);

        float depthNormalized = Mathf.Clamp01(holeDepth / terrainHeight);
        // Anything at or under the waterline is ocean/beach — no holes there.
        float minGroundNormalized = (WaterSurfaceHeight() + holeDepth) / terrainHeight;
        float pixelsPerUnit = terrainResolution / mapSize.x;

        for (int i = 0; i < holeCount; i++)
        {
            // Random position
            int x = Random.Range(terrainResolution / 4, terrainResolution * 3 / 4);
            int y = Random.Range(terrainResolution / 4, terrainResolution * 3 / 4);

            // A region's holeDensityMultiplier scales how many holes land here: >1 stamps extra
            // holes at this roll (fractional part resolved by a coin flip), <1 sometimes skips it
            // entirely, replacing the old flat inner-50%-uniform rule with a per-region density.
            float worldX = (float)x / terrainResolution * mapSize.x;
            float worldY = (float)y / terrainResolution * mapSize.y;
            RegionDefinition region = GetRegionAt(new Vector3(terrainOrigin.x + worldX, 0f, terrainOrigin.z + worldY));
            float multiplier = region != null ? region.holeDensityMultiplier : 1f;

            int stampCount = Mathf.FloorToInt(multiplier);
            if (Random.value < multiplier - stampCount) stampCount++;

            for (int s = 0; s < stampCount; s++)
            {
                float holeSize = Random.Range(minHoleSize, maxHoleSize);
                int holeRadius = Mathf.RoundToInt(holeSize * pixelsPerUnit);
                StampHole(x, y, holeRadius, holeType, heights, depthNormalized, minGroundNormalized);
            }
        }

        terrainData.SetHeights(0, 0, heights);
    }

    /// <summary>
    /// Carves one circular hole into groundTypeMap/heights at a given cell, skipping any cell
    /// already at or below the waterline.
    /// </summary>
    private void StampHole(int x, int y, int holeRadius, GroundType holeType, float[,] heights, float depthNormalized, float minGroundNormalized)
    {
        for (int dx = -holeRadius; dx <= holeRadius; dx++)
        {
            for (int dy = -holeRadius; dy <= holeRadius; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && nx < terrainResolution && ny >= 0 && ny < terrainResolution)
                {
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance <= holeRadius)
                    {
                        // Unity's heights array is indexed [y, x], groundTypeMap is [x, y].
                        if (heights[ny, nx] < minGroundNormalized) continue;

                        groundTypeMap[nx, ny] = holeType;
                        heights[ny, nx] = Mathf.Max(0f, heights[ny, nx] - depthNormalized);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Creates one terrain layer per configured GroundType, in inspector order.
    /// Layer indices must stay stable across runs — shader and detail-density code keys off them.
    /// </summary>
    private void BuildTerrainLayers()
    {
        layerIndexToSpeed.Clear();
        layerIndexToIsHole.Clear();
        groundTypeToLayerIndex.Clear();

        List<TerrainLayer> layers = new List<TerrainLayer>();

        foreach (var groundType in availableGroundTypes)
        {
            if (groundType == null || groundType.terrainTexture == null) continue;
            if (groundTypeToLayerIndex.ContainsKey(groundType)) continue;

            int index = layers.Count;

            TerrainLayer layer = new TerrainLayer();
            layer.name = groundType.groundName;
            layer.diffuseTexture = groundType.terrainTexture;
            layer.normalMapTexture = groundType.normalMap;
            layer.tileSize = groundType.textureTiling;
            layers.Add(layer);

            groundTypeToLayerIndex[groundType] = index;
            layerIndexToSpeed[index] = groundType.movementSpeedMultiplier;
            layerIndexToIsHole[index] = groundType.isHole;
        }

        terrainData.terrainLayers = layers.ToArray();

        Debug.Log($"MapGenerator: Built {layers.Count} terrain layers from {availableGroundTypes.Count} ground types.");
    }

    /// <summary>
    /// Applies terrain textures based on ground type map
    /// </summary>
    private void ApplyTerrainTextures()
    {
        BuildTerrainLayers();

        if (terrainData.terrainLayers.Length == 0)
        {
            Debug.LogWarning("MapGenerator: No valid terrain layers created. Skipping texture application.");
            return;
        }

        TerrainLayer[] terrainLayers = terrainData.terrainLayers;
        
        // Create alpha map (texture blending) - use alphamap resolution, not heightmap resolution
        float[,,] alphaMap = new float[alphamapResolution, alphamapResolution, terrainLayers.Length];
        
        // Initialize all to 0
        for (int x = 0; x < alphamapResolution; x++)
        {
            for (int y = 0; y < alphamapResolution; y++)
            {
                for (int layer = 0; layer < terrainLayers.Length; layer++)
                {
                    alphaMap[x, y, layer] = 0f;
                }
            }
        }
        
        // Weighted splat: each alphamap cell gathers a neighbourhood of ground-type cells with
        // a distance falloff, so transitions come out soft. Replaces the old one-hot write plus
        // box blur, which could only ever produce a hard edge smeared by a fixed 2px radius.
        int gather = Mathf.Max(1, Mathf.RoundToInt((float)terrainResolution / alphamapResolution) + blendRadius);

        for (int x = 0; x < alphamapResolution; x++)
        {
            for (int y = 0; y < alphamapResolution; y++)
            {
                int centerX = Mathf.Clamp(Mathf.RoundToInt((float)x / alphamapResolution * terrainResolution), 0, terrainResolution - 1);
                int centerY = Mathf.Clamp(Mathf.RoundToInt((float)y / alphamapResolution * terrainResolution), 0, terrainResolution - 1);

                float total = 0f;

                for (int dx = -gather; dx <= gather; dx++)
                {
                    for (int dy = -gather; dy <= gather; dy++)
                    {
                        int nx = centerX + dx;
                        int ny = centerY + dy;
                        if (nx < 0 || nx >= terrainResolution || ny < 0 || ny >= terrainResolution) continue;

                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        if (distance > gather) continue;

                        GroundType type = groundTypeMap[nx, ny];
                        if (type == null || !groundTypeToLayerIndex.TryGetValue(type, out int layerIndex)) continue;

                        float weight = 1f - distance / (gather + 1f);
                        weight *= weight;

                        alphaMap[x, y, layerIndex] += weight;
                        total += weight;
                    }
                }

                if (total > 0f)
                {
                    for (int layer = 0; layer < terrainLayers.Length; layer++)
                    {
                        alphaMap[x, y, layer] /= total;
                    }
                }
                else
                {
                    alphaMap[x, y, 0] = 1f;
                }
            }
        }

        terrainData.SetAlphamaps(0, 0, alphaMap);
    }

    /// <summary>
    /// Checks if the map has been generated or successfully loaded
    /// </summary>
    public bool IsMapGenerated()
    {
        // If we have lookup data, we consider the map usable
        if (layerIndexToSpeed.Count > 0 && terrain != null && terrainData != null)
        {
            return true;
        }
        return groundTypeMap != null && terrain != null && terrainData != null;
    }

    /// <summary>
    /// Gets the weighted average movement speed multiplier at a world position based on terrain blending.
    /// This provides smooth speed transitions between biomes.
    /// </summary>
    public float GetSmoothedSpeedMultiplierAtPosition(Vector3 worldPosition)
    {
        if (terrain == null || terrainData == null) return 1f;

        // Convert world pos to alphamap coordinates
        float mapX = ((worldPosition.x - terrain.transform.position.x) / terrainData.size.x) * terrainData.alphamapWidth;
        float mapZ = ((worldPosition.z - terrain.transform.position.z) / terrainData.size.z) * terrainData.alphamapHeight;

        int x = Mathf.FloorToInt(mapX);
        int y = Mathf.FloorToInt(mapZ);

        // Check bounds
        if (x < 0 || x >= terrainData.alphamapWidth || y < 0 || y >= terrainData.alphamapHeight)
        {
            return 1f;
        }

        // Sample alphamap (1x1 block)
        float[,,] splatmapData = terrainData.GetAlphamaps(x, y, 1, 1);
        int numLayers = splatmapData.GetLength(2);
        
        float weightedSpeed = 0f;
        float totalWeight = 0f;
        
        for (int i = 0; i < numLayers; i++)
        {
            float weight = splatmapData[0, 0, i];
            if (weight > 0.01f) // Ignore negligible weights
            {
                if (layerIndexToSpeed.TryGetValue(i, out float speed))
                {
                    weightedSpeed += speed * weight;
                    totalWeight += weight;
                }
            }
        }

        return totalWeight > 0.01f ? weightedSpeed / totalWeight : 1f;
    }

    /// <summary>
    /// Checks if the position is effectively a hole (dominant texture is hole)
    /// </summary>
    public bool IsHoleAtPosition(Vector3 worldPosition)
    {
        if (terrain == null || terrainData == null) return false;

        float mapX = ((worldPosition.x - terrain.transform.position.x) / terrainData.size.x) * terrainData.alphamapWidth;
        float mapZ = ((worldPosition.z - terrain.transform.position.z) / terrainData.size.z) * terrainData.alphamapHeight;
        
        int x = Mathf.FloorToInt(mapX);
        int y = Mathf.FloorToInt(mapZ);

        if (x < 0 || x >= terrainData.alphamapWidth || y < 0 || y >= terrainData.alphamapHeight) return false;

        float[,,] splatmapData = terrainData.GetAlphamaps(x, y, 1, 1);
        int numLayers = splatmapData.GetLength(2);
        
        float holeWeight = 0f;
        
        for (int i = 0; i < numLayers; i++)
        {
            if (layerIndexToIsHole.TryGetValue(i, out bool isHole) && isHole)
            {
                holeWeight += splatmapData[0, 0, i];
            }
        }
        
        // Consider it a hole if > 50% hole texture
        return holeWeight > 0.5f;
    }
    
    /// <summary>
    /// Gets the ground type at a world position
    /// </summary>
    public GroundType GetGroundTypeAtPosition(Vector3 worldPosition)
    {
        if (groundTypeMap == null)
        {
            // Don't spam warnings - this will be handled by the caller
            return null;
        }
        
        if (terrain == null)
        {
            Debug.LogWarning("MapGenerator: terrain is null.");
            return null;
        }
        
        // Get terrain bounds
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;
        
        // Convert world position to terrain local position
        Vector3 localPos = worldPosition - terrainPos;
        
        // Convert to normalized coordinates [0, 1] based on terrain size
        float normalizedX = localPos.x / terrainSize.x;
        float normalizedZ = localPos.z / terrainSize.z;
        
        // Clamp normalized coordinates to [0, 1]
        normalizedX = Mathf.Clamp01(normalizedX);
        normalizedZ = Mathf.Clamp01(normalizedZ);
        
        // Convert to array indices (groundTypeMap uses terrainResolution which matches heightmap resolution)
        int x = Mathf.FloorToInt(normalizedX * terrainResolution);
        int y = Mathf.FloorToInt(normalizedZ * terrainResolution);
        
        // Clamp to valid array range
        x = Mathf.Clamp(x, 0, terrainResolution - 1);
        y = Mathf.Clamp(y, 0, terrainResolution - 1);
        
        GroundType groundType = groundTypeMap[x, y];
        
        return groundType;
    }
    
    /// <summary>
    /// The dominant surface everything else is painted over — fastest walkable type.
    /// </summary>
    private GroundType GetBaseGroundType()
    {
        GroundType best = null;
        foreach (var type in availableGroundTypes)
        {
            if (type == null || type.isHole || !type.isWalkable) continue;
            if (type == sandGroundType) continue;
            if (best == null || type.movementSpeedMultiplier > best.movementSpeedMultiplier) best = type;
        }
        return best;
    }

    /// <summary>
    /// Path surface — slowest walkable type that isn't the base or the shoreline sand.
    /// </summary>
    private GroundType GetPathGroundType()
    {
        GroundType baseType = GetBaseGroundType();
        GroundType best = null;

        foreach (var type in availableGroundTypes)
        {
            if (type == null || type.isHole || !type.isWalkable) continue;
            if (type == baseType || type == sandGroundType) continue;
            if (best == null || type.movementSpeedMultiplier < best.movementSpeedMultiplier) best = type;
        }
        return best;
    }
    
    private GroundType GetHoleGroundType()
    {
        return availableGroundTypes.Find(gt => gt != null && gt.isHole);
    }
}

