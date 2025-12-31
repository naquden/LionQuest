using UnityEngine;
using UnityEngine.Tilemaps;

namespace LionQuest.Character.Tiles
{
    /// <summary>
    /// Custom tile that stores game logic data (movement speed, damage, etc.)
    /// This extends TileBase to work with Unity's Tilemap system for better performance.
    /// </summary>
    [CreateAssetMenu(fileName = "New Game Tile", menuName = "LionQuest/Tiles/Game Tile")]
    public class GameTile : TileBase
    {
        [Header("Visual")]
        public Sprite sprite;
        public Color color = Color.white;
        
        [Header("Movement Properties")]
        [Tooltip("Movement speed multiplier (1.0 = normal, 0.5 = half speed, 2.0 = double speed)")]
        [Range(0.1f, 3.0f)]
        public float movementSpeedMultiplier = 1.0f;
        
        [Tooltip("Can the player walk on this tile?")]
        public bool isWalkable = true;
        
        [Header("Damage Properties")]
        [Tooltip("Damage per second when standing on this tile")]
        public float damagePerSecond = 0f;
        
        [Tooltip("Damage on first step (one-time damage)")]
        public float damageOnStep = 0f;
        
        [Header("Other Properties")]
        [Tooltip("Custom data that can be used for AI generation or other systems")]
        public string tileType = "floor";
        
        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = sprite;
            tileData.color = color;
            tileData.colliderType = isWalkable ? Tile.ColliderType.None : Tile.ColliderType.Grid;
        }
        
        /// <summary>
        /// Get movement speed multiplier for this tile
        /// </summary>
        public float GetMovementSpeedMultiplier()
        {
            return movementSpeedMultiplier;
        }
        
        /// <summary>
        /// Check if tile is walkable
        /// </summary>
        public bool IsWalkable()
        {
            return isWalkable;
        }
        
        /// <summary>
        /// Get damage per second
        /// </summary>
        public float GetDamagePerSecond()
        {
            return damagePerSecond;
        }
        
        /// <summary>
        /// Get one-time damage on step
        /// </summary>
        public float GetDamageOnStep()
        {
            return damageOnStep;
        }
    }
}

