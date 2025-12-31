using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using LionQuest.Character.Tiles;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace LionQuest.Character.Tiles
{
    /// <summary>
    /// Exports tile data to JSON format for AI map generation.
    /// This allows AI to understand available tiles and generate maps.
    /// </summary>
    public class TileDataExporter : MonoBehaviour
    {
        [Header("Export Settings")]
        [SerializeField] private string exportPath = "Assets/Characters/Character/Tiles/tile_data.json";
        
#if UNITY_EDITOR
        [ContextMenu("Export Tile Data for AI")]
        public void ExportTileData()
        {
            // Find all GameTile assets in the project
            string[] guids = AssetDatabase.FindAssets("t:GameTile");
            List<TileDataInfo> tileDataList = new List<TileDataInfo>();
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameTile tile = AssetDatabase.LoadAssetAtPath<GameTile>(path);
                
                if (tile != null)
                {
                    TileDataInfo info = new TileDataInfo
                    {
                        name = tile.name,
                        tileType = tile.tileType,
                        movementSpeedMultiplier = tile.movementSpeedMultiplier,
                        isWalkable = tile.isWalkable,
                        damagePerSecond = tile.damagePerSecond,
                        damageOnStep = tile.damageOnStep,
                        spriteName = tile.sprite != null ? tile.sprite.name : "none"
                    };
                    
                    tileDataList.Add(info);
                }
            }
            
            // Convert to JSON (using Unity's JsonUtility)
            string json = JsonUtility.ToJson(new TileDataWrapper { tiles = tileDataList }, true);
            
            // Ensure directory exists
            string directory = Path.GetDirectoryName(exportPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Write to file
            File.WriteAllText(exportPath, json);
            
            Debug.Log($"Exported {tileDataList.Count} tiles to {exportPath}");
            AssetDatabase.Refresh();
        }
        
        [ContextMenu("Export Current Map Layout")]
        public void ExportMapLayout()
        {
            Tilemap tilemap = FindObjectOfType<Tilemap>();
            if (tilemap == null)
            {
                Debug.LogError("No Tilemap found in scene!");
                return;
            }
            
            BoundsInt bounds = tilemap.cellBounds;
            List<MapTileInfo> mapData = new List<MapTileInfo>();
            
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    TileBase tile = tilemap.GetTile(pos);
                    
                    if (tile is GameTile gameTile)
                    {
                        mapData.Add(new MapTileInfo
                        {
                            x = x,
                            y = y,
                            tileName = gameTile.name,
                            tileType = gameTile.tileType
                        });
                    }
                }
            }
            
            string json = JsonUtility.ToJson(new MapDataWrapper { tiles = mapData }, true);
            string mapExportPath = exportPath.Replace("tile_data.json", "map_layout.json");
            
            File.WriteAllText(mapExportPath, json);
            Debug.Log($"Exported map layout with {mapData.Count} tiles to {mapExportPath}");
            AssetDatabase.Refresh();
        }
#endif
    }
    
    [System.Serializable]
    public class TileDataInfo
    {
        public string name;
        public string tileType;
        public float movementSpeedMultiplier;
        public bool isWalkable;
        public float damagePerSecond;
        public float damageOnStep;
        public string spriteName;
    }
    
    [System.Serializable]
    public class MapTileInfo
    {
        public int x;
        public int y;
        public string tileName;
        public string tileType;
    }
    
    [System.Serializable]
    public class TileDataWrapper
    {
        public List<TileDataInfo> tiles;
    }
    
    [System.Serializable]
    public class MapDataWrapper
    {
        public List<MapTileInfo> tiles;
    }
}

