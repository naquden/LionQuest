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
    [SerializeField] private Vector2Int mapSize = new Vector2Int(200, 200);
    
    /// <summary>
    /// Gets the map size (public for boundary checking)
    /// </summary>
    public Vector2 GetMapSize()
    {
        return new Vector2(mapSize.x, mapSize.y);
    }
    
    [Tooltip("Height of the terrain")]
    [SerializeField] private float terrainHeight = 20f;
    
    [Header("Terrain Settings")]
    [Tooltip("Generate flat terrain (no height variation)")]
    [SerializeField] private bool flatTerrain = true;
    
    [Header("Biome Generation")]
    [Tooltip("Ground types available for generation")]
    [SerializeField] private List<GroundType> availableGroundTypes = new List<GroundType>();
    
    [Tooltip("Number of biome regions to generate")]
    [SerializeField] private int biomeCount = 5;
    
    [Tooltip("Size variation of biomes")]
    [SerializeField] private float biomeSizeVariation = 0.3f;
    
    [Tooltip("Blend distance between biomes")]
    [SerializeField] private float biomeBlendDistance = 10f;
    
    [Header("Hole Generation")]
    [Tooltip("Number of holes to generate")]
    [SerializeField] private int holeCount = 10;
    
    [Tooltip("Minimum hole size")]
    [SerializeField] private float minHoleSize = 2f;
    
    [Tooltip("Maximum hole size")]
    [SerializeField] private float maxHoleSize = 5f;
    
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

        layerIndexToSpeed.Clear();
        layerIndexToIsHole.Clear();

        if (terrainData.terrainLayers == null || terrainData.terrainLayers.Length == 0)
        {
            Debug.LogWarning("[MapGenerator] No terrain layers found on existing terrain!");
            return;
        }

        Debug.Log($"[MapGenerator] Found {terrainData.terrainLayers.Length} terrain layers. Matching with {availableGroundTypes.Count} available GroundTypes...");

        for (int i = 0; i < terrainData.terrainLayers.Length; i++)
        {
            TerrainLayer layer = terrainData.terrainLayers[i];
            bool foundMatch = false;

            foreach (var groundType in availableGroundTypes)
            {
                // Match by texture (Diffuse Texture is usually unique enough)
                if (groundType.terrainTexture == layer.diffuseTexture)
                {
                    layerIndexToSpeed[i] = groundType.movementSpeedMultiplier;
                    layerIndexToIsHole[i] = groundType.isHole;
                    Debug.Log($"[MapGenerator] Matched Layer {i} ({layer.name}) to GroundType '{groundType.groundName}'. Speed: {groundType.movementSpeedMultiplier}, IsHole: {groundType.isHole}");
                    foundMatch = true;
                    break;
                }
            }

            if (!foundMatch)
            {
                Debug.LogWarning($"[MapGenerator] Layer {i} ({layer.name}) has no matching GroundType in 'availableGroundTypes'. Defaulting to Speed=1, IsHole=False.");
                layerIndexToSpeed[i] = 1f;
                layerIndexToIsHole[i] = false;
            }
        }
        
        // Important: IsMapGenerated() checks terrain != null. 
        // We consider the map "generated" for gameplay purposes if we have data, even if groundTypeMap is null.
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
    
    /// <summary>
    /// Generates the entire map (terrain + biomes)
    /// </summary>
    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
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
        
        Debug.Log("MapGenerator: Starting map generation...");
        
        // Set terrain size
        terrainData.size = new Vector3(mapSize.x, terrainHeight, mapSize.y);
        terrain.transform.position = Vector3.zero;
        
        // Get terrain resolutions
        terrainResolution = terrainData.heightmapResolution;
        alphamapResolution = terrainData.alphamapResolution;
        
        // Generate terrain heightmap (flat if enabled)
        if (flatTerrain)
        {
            GenerateFlatTerrain();
        }
        else
        {
            GenerateTerrainHeight();
        }
        
        // Generate biome map
        GenerateBiomes();
        
        // Apply textures to terrain
        ApplyTerrainTextures();
        
        // Generate holes
        GenerateHoles();
        
        Debug.Log("MapGenerator: Map generation complete!");
    }
    
    /// <summary>
    /// Generates flat terrain (no height variation)
    /// </summary>
    private void GenerateFlatTerrain()
    {
        float[,] heights = new float[terrainResolution, terrainResolution];
        
        // Set all heights to 0 (flat ground at base level)
        for (int x = 0; x < terrainResolution; x++)
        {
            for (int y = 0; y < terrainResolution; y++)
            {
                heights[x, y] = 0f;
            }
        }
        
        terrainData.SetHeights(0, 0, heights);
    }
    
    /// <summary>
    /// Generates terrain height using Perlin noise (not used if flatTerrain is true)
    /// </summary>
    private void GenerateTerrainHeight()
    {
        float[,] heights = new float[terrainResolution, terrainResolution];
        
        for (int x = 0; x < terrainResolution; x++)
        {
            for (int y = 0; y < terrainResolution; y++)
            {
                float xCoord = (float)x / terrainResolution * mapSize.x * 0.1f;
                float yCoord = (float)y / terrainResolution * mapSize.y * 0.1f;
                
                float height = Mathf.PerlinNoise(xCoord, yCoord);
                height = height * 10f / terrainHeight; // Normalize to terrain height
                
                heights[x, y] = height;
            }
        }
        
        terrainData.SetHeights(0, 0, heights);
    }
    
    /// <summary>
    /// Generates biome regions with logical placement
    /// </summary>
    private void GenerateBiomes()
    {
        groundTypeMap = new GroundType[terrainResolution, terrainResolution];
        
        // Create biome centers
        List<BiomeCenter> biomeCenters = new List<BiomeCenter>();
        
        for (int i = 0; i < biomeCount; i++)
        {
            // Select a random ground type (excluding holes - those are generated separately)
            GroundType groundType = GetRandomWalkableGroundType();
            if (groundType == null) continue;
            
            // Random position
            Vector2 center = new Vector2(
                Random.Range(mapSize.x * 0.2f, mapSize.x * 0.8f),
                Random.Range(mapSize.y * 0.2f, mapSize.y * 0.8f)
            );
            
            // Random size
            float size = Random.Range(
                Mathf.Min(mapSize.x, mapSize.y) * 0.1f,
                Mathf.Min(mapSize.x, mapSize.y) * 0.3f
            );
            size *= (1f + Random.Range(-biomeSizeVariation, biomeSizeVariation));
            
            biomeCenters.Add(new BiomeCenter
            {
                center = center,
                size = size,
                groundType = groundType
            });
            
            Debug.Log($"Biome {i}: Type={groundType.groundName}, Center=({center.x:F1}, {center.y:F1}), Size={size:F1}");
        }
        
        // Fill ground type map based on biome centers
        for (int x = 0; x < terrainResolution; x++)
        {
            for (int y = 0; y < terrainResolution; y++)
            {
                // Convert array coordinates to world position
                // x and y are array indices, convert to world coordinates
                Vector2 worldPos = new Vector2(
                    ((float)x / terrainResolution) * mapSize.x,
                    ((float)y / terrainResolution) * mapSize.y
                );
                
                GroundType closestType = GetClosestBiomeType(worldPos, biomeCenters);
                groundTypeMap[x, y] = closestType;
            }
        }
        
        Debug.Log($"MapGenerator: Generated {biomeCenters.Count} biomes. Ground type map size: {terrainResolution}x{terrainResolution}");
    }
    
    /// <summary>
    /// Gets the ground type at the closest biome center, with blending
    /// </summary>
    private GroundType GetClosestBiomeType(Vector2 position, List<BiomeCenter> biomeCenters)
    {
        if (biomeCenters.Count == 0)
        {
            return availableGroundTypes.Count > 0 ? availableGroundTypes[0] : null; // Default to first ground type
        }
        
        float minDistance = float.MaxValue;
        GroundType closestType = biomeCenters[0].groundType;
        
        // Find closest biome based on distance from center
        foreach (var biome in biomeCenters)
        {
            float distance = Vector2.Distance(position, biome.center);
            
            // Check if position is within this biome's radius
            if (distance <= biome.size)
            {
                // Position is within this biome, use it
                return biome.groundType;
            }
            
            // Track closest biome for positions outside all biomes
            if (distance < minDistance)
            {
                minDistance = distance;
                closestType = biome.groundType;
            }
        }
        
        // If we're outside all biomes, use the closest one
        // Check if we're in blend zone between biomes
        if (biomeBlendDistance > 0)
        {
            List<BiomeCenter> nearbyBiomes = new List<BiomeCenter>();
            foreach (var biome in biomeCenters)
            {
                float distance = Vector2.Distance(position, biome.center);
                if (distance <= biome.size + biomeBlendDistance)
                {
                    nearbyBiomes.Add(biome);
                }
            }
            
            if (nearbyBiomes.Count > 1)
            {
                // Blend between nearby biomes (simple: use closest)
                // Could be enhanced with weighted blending
                float closestDist = float.MaxValue;
                foreach (var biome in nearbyBiomes)
                {
                    float dist = Vector2.Distance(position, biome.center);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestType = biome.groundType;
                    }
                }
            }
        }
        
        return closestType;
    }
    
    /// <summary>
    /// Generates holes in the terrain
    /// </summary>
    private void GenerateHoles()
    {
        GroundType holeType = GetHoleGroundType();
        if (holeType == null)
        {
            Debug.LogWarning("MapGenerator: No hole ground type found. Skipping hole generation.");
            return;
        }
        
        for (int i = 0; i < holeCount; i++)
        {
            // Random position
            int x = Random.Range(terrainResolution / 4, terrainResolution * 3 / 4);
            int y = Random.Range(terrainResolution / 4, terrainResolution * 3 / 4);
            
            // Random size
            float holeSize = Random.Range(minHoleSize, maxHoleSize);
            int holeRadius = Mathf.RoundToInt(holeSize * terrainResolution / mapSize.x);
            
            // Create circular hole
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
                            groundTypeMap[nx, ny] = holeType;
                            
                            // Also lower the terrain height at hole location
                            float[,] heights = terrainData.GetHeights(nx, ny, 1, 1);
                            heights[0, 0] = 0f; // Set to ground level (or below for holes)
                            terrainData.SetHeights(nx, ny, heights);
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Applies terrain textures based on ground type map
    /// </summary>
    private void ApplyTerrainTextures()
    {
        layerIndexToSpeed.Clear();
        layerIndexToIsHole.Clear();

        // Get unique ground types (only those with textures)
        List<GroundType> typesWithTextures = new List<GroundType>();
        HashSet<GroundType> uniqueTypes = new HashSet<GroundType>();
        
        for (int x = 0; x < terrainResolution; x++)
        {
            for (int y = 0; y < terrainResolution; y++)
            {
                if (groundTypeMap[x, y] != null && groundTypeMap[x, y].terrainTexture != null)
                {
                    uniqueTypes.Add(groundTypeMap[x, y]);
                }
            }
        }
        
        if (uniqueTypes.Count == 0)
        {
            Debug.LogWarning("MapGenerator: No ground types with textures found. Skipping texture application.");
            return;
        }
        
        // Create terrain layers
        TerrainLayer[] terrainLayers = new TerrainLayer[uniqueTypes.Count];
        int layerIndex = 0;
        Dictionary<GroundType, int> groundTypeToLayerIndex = new Dictionary<GroundType, int>();
        
        foreach (var groundType in uniqueTypes)
        {
            if (groundType.terrainTexture != null)
            {
                TerrainLayer layer = new TerrainLayer();
                layer.diffuseTexture = groundType.terrainTexture;
                layer.normalMapTexture = groundType.normalMap;
                layer.tileSize = groundType.textureTiling;
                terrainLayers[layerIndex] = layer;
                groundTypeToLayerIndex[groundType] = layerIndex;
                
                // Cache properties for runtime lookup
                layerIndexToSpeed[layerIndex] = groundType.movementSpeedMultiplier;
                layerIndexToIsHole[layerIndex] = groundType.isHole;

                layerIndex++;
            }
        }
        
        // Ensure we have at least one layer
        if (terrainLayers.Length == 0)
        {
            Debug.LogWarning("MapGenerator: No valid terrain layers created. Skipping texture application.");
            return;
        }
        
        terrainData.terrainLayers = terrainLayers;
        
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
        
        // Map ground types to alpha map
        for (int x = 0; x < alphamapResolution; x++)
        {
            for (int y = 0; y < alphamapResolution; y++)
            {
                // Convert alphamap coordinates to heightmap coordinates
                int heightmapX = Mathf.RoundToInt((float)x / alphamapResolution * terrainResolution);
                int heightmapY = Mathf.RoundToInt((float)y / alphamapResolution * terrainResolution);
                
                heightmapX = Mathf.Clamp(heightmapX, 0, terrainResolution - 1);
                heightmapY = Mathf.Clamp(heightmapY, 0, terrainResolution - 1);
                
                GroundType type = groundTypeMap[heightmapX, heightmapY];
                if (type != null && groundTypeToLayerIndex.ContainsKey(type))
                {
                    int targetLayerIndex = groundTypeToLayerIndex[type];
                    alphaMap[x, y, targetLayerIndex] = 1f;
                }
                else if (terrainLayers.Length > 0)
                {
                    // Default to first layer if no type found
                    alphaMap[x, y, 0] = 1f;
                }
            }
        }
        
        // Smooth the boundaries
        alphaMap = SmoothAlphaMap(alphaMap);

        terrainData.SetAlphamaps(0, 0, alphaMap);
    }

    private float[,,] SmoothAlphaMap(float[,,] originalMap)
    {
        int width = originalMap.GetLength(0);
        int height = originalMap.GetLength(1);
        int layers = originalMap.GetLength(2);
        
        float[,,] smoothMap = new float[width, height, layers];
        int radius = 2; // Blur radius (increase for smoother transitions)

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int l = 0; l < layers; l++)
                {
                    float sum = 0;
                    int count = 0;
                    
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                sum += originalMap[nx, ny, l];
                                count++;
                            }
                        }
                    }
                    smoothMap[x, y, l] = sum / count;
                }
            }
        }
        return smoothMap;
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
                    Debug.Log($"[MapGen] Layer {i}: Weight={weight:F2}, Speed={speed:F2}");
                }
                else
                {
                    Debug.LogWarning($"[MapGen] Layer {i} has weight {weight:F2} but no speed in lookup! (Did you regenerate map?)");
                }
            }
        }
        
        float finalSpeed = totalWeight > 0.01f ? weightedSpeed / totalWeight : 1f;
        Debug.Log($"[MapGen] Final Smoothed Speed: {finalSpeed:F2}");
        return finalSpeed;
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
    
    private GroundType GetRandomWalkableGroundType()
    {
        List<GroundType> walkableTypes = availableGroundTypes.FindAll(gt => gt != null && gt.isWalkable && !gt.isHole);
        if (walkableTypes.Count == 0)
        {
            return availableGroundTypes.Count > 0 ? availableGroundTypes[0] : null;
        }
        return walkableTypes[Random.Range(0, walkableTypes.Count)];
    }
    
    private GroundType GetHoleGroundType()
    {
        return availableGroundTypes.Find(gt => gt != null && gt.isHole);
    }
    
    private struct BiomeCenter
    {
        public Vector2 center;
        public float size;
        public GroundType groundType;
    }
}

