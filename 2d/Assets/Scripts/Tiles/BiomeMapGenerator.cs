using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace LionQuest.Character.Tiles
{
    /// <summary>
    /// Generates game-like maps with biomes (clustered tile types) and trails.
    /// Creates natural-feeling maps with different terrain types grouped together.
    /// </summary>
    [AddComponentMenu("LionQuest/Biome Map Generator")]
    public class BiomeMapGenerator : MonoBehaviour
    {
        [Header("Map Settings")]
        [SerializeField] private int mapWidth = 40;
        [SerializeField] private int mapHeight = 40;
        
        [Header("Biome Tiles")]
        [SerializeField] private TileBase grassTile;
        [SerializeField] private TileBase mudTile;
        [SerializeField] private TileBase stoneTile;
        [SerializeField] private TileBase sandTile;
        [SerializeField] private TileBase waterTile; // Optional: for water biomes
        
        [Header("Biome Settings")]
        [SerializeField] private int numBiomes = 3; // Fewer biomes = larger each biome
        [SerializeField] private float biomeSize = 0.02f; // Lower = larger biomes (default: 0.02 for very large biomes)
        [SerializeField] private float biomeBlend = 0.01f; // How much biomes blend at edges (lower = less mixing, default: 0.01 = almost no mixing)
        [SerializeField] private bool useStrictBoundaries = true; // If true, no blending at all
        
        [Header("Trail Settings")]
        [SerializeField] private bool generateTrails = true;
        [SerializeField] private int numTrails = 3;
        [SerializeField] private float trailWidth = 2f;
        [SerializeField] private float trailCurviness = 0.3f; // How curvy trails are (0-1)
        
        [Header("Path Settings")]
        [SerializeField] private bool generatePaths = true;
        [SerializeField] private int numPaths = 2;
        [SerializeField] private float pathWidth = 3f;
        
        private Tilemap tilemap;
        private Dictionary<Vector2Int, BiomeType> biomeMap;
        
        private enum BiomeType
        {
            Grass,
            Mud,
            Stone,
            Sand,
            Water
        }
        
        private void Awake()
        {
            FindOrCreateTilemap();
        }
        
        private void FindOrCreateTilemap()
        {
            if (tilemap == null)
            {
                tilemap = GetComponentInChildren<Tilemap>();
                if (tilemap == null)
                {
                    tilemap = FindObjectOfType<Tilemap>();
                }
                
                if (tilemap == null)
                {
                    CreateTilemap();
                }
            }
        }
        
        private void CreateTilemap()
        {
            GameObject gridObject = new GameObject("Grid");
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);
            
            GameObject tilemapObject = new GameObject("Tilemap");
            tilemapObject.transform.SetParent(gridObject.transform);
            tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>();
        }
        
        [ContextMenu("Generate Biome Map")]
        public void GenerateBiomeMap()
        {
            FindOrCreateTilemap();
            
            if (tilemap == null)
            {
                Debug.LogError("BiomeMapGenerator: Failed to find or create Tilemap!");
                return;
            }
            
            if (grassTile == null)
            {
                Debug.LogError("BiomeMapGenerator: No grass tile assigned! Please assign at least a grass tile.");
                return;
            }
            
            tilemap.ClearAllTiles();
            biomeMap = new Dictionary<Vector2Int, BiomeType>();
            
            // Step 1: Generate biome centers
            List<Vector2Int> biomeCenters = GenerateBiomeCenters();
            
            // Step 2: Assign biomes to each cell based on distance to centers
            AssignBiomes(biomeCenters);
            
            // Step 3: Place tiles based on biome type
            PlaceBiomeTiles();
            
            // Step 4: Add trails (mud trails through grass, etc.)
            if (generateTrails)
            {
                GenerateTrails();
            }
            
            // Step 5: Add paths connecting biomes
            if (generatePaths)
            {
                GeneratePaths();
            }
            
            Debug.Log($"BiomeMapGenerator: Generated biome map ({mapWidth}x{mapHeight}) with {biomeCenters.Count} biomes");
        }
        
        private List<Vector2Int> GenerateBiomeCenters()
        {
            List<Vector2Int> centers = new List<Vector2Int>();
            List<BiomeType> availableBiomes = GetAvailableBiomes();
            
            // Ensure minimum distance between biome centers for larger, distinct biomes
            float minDistanceBetweenCenters = Mathf.Min(mapWidth, mapHeight) / (numBiomes + 1);
            
            for (int i = 0; i < numBiomes && i < availableBiomes.Count; i++)
            {
                Vector2Int center;
                int attempts = 0;
                bool validPosition = false;
                
                do
                {
                    center = new Vector2Int(
                        Random.Range(mapWidth / 5, mapWidth * 4 / 5),
                        Random.Range(mapHeight / 5, mapHeight * 4 / 5)
                    );
                    
                    // Check if this position is far enough from existing centers
                    validPosition = true;
                    foreach (var existingCenter in centers)
                    {
                        float distance = Vector2Int.Distance(center, existingCenter);
                        if (distance < minDistanceBetweenCenters)
                        {
                            validPosition = false;
                            break;
                        }
                    }
                    
                    attempts++;
                } while (!validPosition && attempts < 100);
                
                if (validPosition || centers.Count == 0)
                {
                    centers.Add(center);
                }
            }
            
            return centers;
        }
        
        private List<BiomeType> GetAvailableBiomes()
        {
            List<BiomeType> biomes = new List<BiomeType> { BiomeType.Grass };
            
            if (mudTile != null) biomes.Add(BiomeType.Mud);
            if (stoneTile != null) biomes.Add(BiomeType.Stone);
            if (sandTile != null) biomes.Add(BiomeType.Sand);
            if (waterTile != null) biomes.Add(BiomeType.Water);
            
            return biomes;
        }
        
        private void AssignBiomes(List<Vector2Int> biomeCenters)
        {
            List<BiomeType> availableBiomes = GetAvailableBiomes();
            
            // First pass: Assign biomes based on distance (Voronoi-like)
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    float minDistance = float.MaxValue;
                    int closestBiomeIndex = 0;
                    
                    // Find closest biome center
                    for (int i = 0; i < biomeCenters.Count; i++)
                    {
                        float distance = Vector2Int.Distance(pos, biomeCenters[i]);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestBiomeIndex = i;
                        }
                    }
                    
                    // Assign biome type based on closest center
                    BiomeType biomeType = availableBiomes[closestBiomeIndex % availableBiomes.Count];
                    biomeMap[pos] = biomeType;
                }
            }
            
            // Second pass: Smooth biome edges (very minimal blending, or none if strict boundaries)
            if (!useStrictBoundaries)
            {
                Dictionary<Vector2Int, BiomeType> smoothedMap = new Dictionary<Vector2Int, BiomeType>(biomeMap);
                
                for (int x = 1; x < mapWidth - 1; x++)
                {
                    for (int y = 1; y < mapHeight - 1; y++)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        BiomeType currentBiome = biomeMap[pos];
                        
                        // Count neighboring biomes
                        Dictionary<BiomeType, int> neighborCounts = new Dictionary<BiomeType, int>();
                        neighborCounts[currentBiome] = 1; // Count current tile
                        
                        // Check 8 neighbors
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                
                                Vector2Int neighborPos = new Vector2Int(x + dx, y + dy);
                                if (biomeMap.ContainsKey(neighborPos))
                                {
                                    BiomeType neighborBiome = biomeMap[neighborPos];
                                    if (!neighborCounts.ContainsKey(neighborBiome))
                                    {
                                        neighborCounts[neighborBiome] = 0;
                                    }
                                    neighborCounts[neighborBiome]++;
                                }
                            }
                        }
                        
                        // Only blend if there's a strong majority of a different biome (very rare)
                        BiomeType mostCommon = currentBiome;
                        int maxCount = 0;
                        foreach (var kvp in neighborCounts)
                        {
                            if (kvp.Value > maxCount)
                            {
                                maxCount = kvp.Value;
                                mostCommon = kvp.Key;
                            }
                        }
                        
                        // Only change if different biome has 7+ neighbors AND very low random chance (extremely rare mixing)
                        if (mostCommon != currentBiome && maxCount >= 7 && Random.value < biomeBlend * 0.1f)
                        {
                            smoothedMap[pos] = mostCommon;
                        }
                    }
                }
                
                biomeMap = smoothedMap;
            }
            // If useStrictBoundaries is true, skip smoothing entirely - pure Voronoi with no mixing
        }
        
        private void PlaceBiomeTiles()
        {
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    BiomeType biome = biomeMap.ContainsKey(pos) ? biomeMap[pos] : BiomeType.Grass;
                    
                    TileBase tile = GetTileForBiome(biome);
                    if (tile != null)
                    {
                        tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                    }
                }
            }
        }
        
        private TileBase GetTileForBiome(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Grass: return grassTile;
                case BiomeType.Mud: return mudTile != null ? mudTile : grassTile;
                case BiomeType.Stone: return stoneTile != null ? stoneTile : grassTile;
                case BiomeType.Sand: return sandTile != null ? sandTile : grassTile;
                case BiomeType.Water: return waterTile != null ? waterTile : grassTile;
                default: return grassTile;
            }
        }
        
        private void GenerateTrails()
        {
            if (mudTile == null) return; // Need mud tile for trails
            
            for (int i = 0; i < numTrails; i++)
            {
                // Start trail from random edge
                Vector2Int start = GetRandomEdgePosition();
                Vector2Int end = GetRandomEdgePosition();
                
                // Create curvy trail
                CreateCurvyTrail(start, end, mudTile, trailWidth, trailCurviness);
            }
        }
        
        private void GeneratePaths()
        {
            if (mudTile == null && stoneTile == null) return;
            
            // Find biome centers (approximate)
            List<Vector2Int> biomeCenters = new List<Vector2Int>();
            for (int x = mapWidth / 4; x < mapWidth * 3 / 4; x += mapWidth / 4)
            {
                for (int y = mapHeight / 4; y < mapHeight * 3 / 4; y += mapHeight / 4)
                {
                    biomeCenters.Add(new Vector2Int(x, y));
                }
            }
            
            // Connect some biome centers with paths
            TileBase pathTile = stoneTile != null ? stoneTile : mudTile;
            
            for (int i = 0; i < Mathf.Min(numPaths, biomeCenters.Count - 1); i++)
            {
                Vector2Int start = biomeCenters[Random.Range(0, biomeCenters.Count)];
                Vector2Int end = biomeCenters[Random.Range(0, biomeCenters.Count)];
                
                if (start != end)
                {
                    CreatePath(start, end, pathTile, pathWidth);
                }
            }
        }
        
        private Vector2Int GetRandomEdgePosition()
        {
            int side = Random.Range(0, 4);
            switch (side)
            {
                case 0: return new Vector2Int(0, Random.Range(0, mapHeight)); // Left
                case 1: return new Vector2Int(mapWidth - 1, Random.Range(0, mapHeight)); // Right
                case 2: return new Vector2Int(Random.Range(0, mapWidth), 0); // Bottom
                default: return new Vector2Int(Random.Range(0, mapWidth), mapHeight - 1); // Top
            }
        }
        
        private void CreateCurvyTrail(Vector2Int start, Vector2Int end, TileBase trailTile, float width, float curviness)
        {
            // Create a curvy path using Bezier-like interpolation
            int steps = Mathf.Max(Mathf.Abs(end.x - start.x), Mathf.Abs(end.y - start.y));
            
            // Add control point for curvature (convert Vector2Int to Vector2)
            Vector2 startVec2 = new Vector2(start.x, start.y);
            Vector2 endVec2 = new Vector2(end.x, end.y);
            Vector2 controlPoint = Vector2.Lerp(startVec2, endVec2, 0.5f);
            controlPoint += new Vector2(
                Random.Range(-mapWidth * curviness, mapWidth * curviness),
                Random.Range(-mapHeight * curviness, mapHeight * curviness)
            );
            
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                
                // Quadratic Bezier curve
                Vector2 point = (1 - t) * (1 - t) * startVec2 + 2 * (1 - t) * t * controlPoint + t * t * endVec2;
                Vector2Int tilePos = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));
                
                // Place trail with width
                PlaceTrailTile(tilePos, trailTile, width);
            }
        }
        
        private void CreatePath(Vector2Int start, Vector2Int end, TileBase pathTile, float width)
        {
            // Simple line path
            int dx = Mathf.Abs(end.x - start.x);
            int dy = Mathf.Abs(end.y - start.y);
            int sx = start.x < end.x ? 1 : -1;
            int sy = start.y < end.y ? 1 : -1;
            int err = dx - dy;
            
            int x = start.x;
            int y = start.y;
            
            while (true)
            {
                PlaceTrailTile(new Vector2Int(x, y), pathTile, width);
                
                if (x == end.x && y == end.y) break;
                
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }
        
        private void PlaceTrailTile(Vector2Int center, TileBase tile, float width)
        {
            int radius = Mathf.CeilToInt(width / 2f);
            
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance <= width / 2f)
                    {
                        Vector2Int pos = new Vector2Int(center.x + dx, center.y + dy);
                        if (pos.x >= 0 && pos.x < mapWidth && pos.y >= 0 && pos.y < mapHeight)
                        {
                            tilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), tile);
                        }
                    }
                }
            }
        }
    }
}

