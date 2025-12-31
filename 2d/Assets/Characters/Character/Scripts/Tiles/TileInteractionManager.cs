using UnityEngine;
using UnityEngine.Tilemaps;
using LionQuest.Character.Tiles;

namespace LionQuest.Character
{
    /// <summary>
    /// Manages tile interactions (movement speed, damage, etc.) for the player.
    /// This system works with Unity's Tilemap for better performance than individual prefabs.
    /// </summary>
    public class TileInteractionManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Tilemap tilemap;
        [SerializeField] private SimpleCharacterController playerController;
        
        [Header("Settings")]
        [SerializeField] private bool showDebugInfo = false;
        
        private Vector3Int lastTilePosition;
        private GameTile currentTile;
        private float timeOnCurrentTile = 0f;
        private float lastDamageTime = 0f;
        
        private void Awake()
        {
            if (tilemap == null)
            {
                tilemap = FindObjectOfType<Tilemap>();
                if (tilemap == null)
                {
                    Debug.LogError("TileInteractionManager: No Tilemap found! Please assign one in the Inspector.");
                }
            }
            
            if (playerController == null)
            {
                playerController = GetComponent<SimpleCharacterController>();
                if (playerController == null)
                {
                    playerController = FindObjectOfType<SimpleCharacterController>();
                }
            }
        }
        
        private void Update()
        {
            if (tilemap == null || playerController == null) return;
            
            // Get current tile position
            Vector3Int tilePosition = tilemap.WorldToCell(transform.position);
            
            // Check if we've moved to a new tile
            if (tilePosition != lastTilePosition)
            {
                OnTileChanged(tilePosition);
                lastTilePosition = tilePosition;
                timeOnCurrentTile = 0f;
            }
            
            // Update time on current tile
            timeOnCurrentTile += Time.deltaTime;
            
            // Apply tile effects
            if (currentTile != null)
            {
                ApplyTileEffects();
            }
        }
        
        private void OnTileChanged(Vector3Int tilePosition)
        {
            // Get the tile at this position
            TileBase tile = tilemap.GetTile(tilePosition);
            
            // Check if it's a GameTile
            if (tile is GameTile gameTile)
            {
                currentTile = gameTile;
                
                // Apply one-time damage on step
                if (gameTile.GetDamageOnStep() > 0f)
                {
                    ApplyDamage(gameTile.GetDamageOnStep());
                }
                
                // Log tile change (movement speed is applied automatically via GetMovementSpeedMultiplier())
                if (showDebugInfo)
                {
                    Debug.Log($"Stepped on tile: {gameTile.tileType}, Movement Speed Multiplier: {gameTile.GetMovementSpeedMultiplier()}, Damage/sec: {gameTile.GetDamagePerSecond()}");
                }
            }
            else if (tile != null)
            {
                // Regular tile (not a GameTile) - treat as normal floor (no modifiers)
                currentTile = null;
                if (showDebugInfo)
                {
                    Debug.Log($"Stepped on regular tile (no GameTile properties)");
                }
            }
            else
            {
                // No tile at this position
                currentTile = null;
            }
        }
        
        private void ApplyTileEffects()
        {
            if (currentTile == null) return;
            
            // Apply damage over time
            if (currentTile.GetDamagePerSecond() > 0f)
            {
                float timeSinceLastDamage = Time.time - lastDamageTime;
                float damageInterval = 1f / currentTile.GetDamagePerSecond(); // Damage interval in seconds
                
                if (timeSinceLastDamage >= damageInterval)
                {
                    ApplyDamage(1f); // 1 damage per interval
                    lastDamageTime = Time.time;
                }
            }
            
            // Apply movement speed modifier
            // TODO: Integrate with SimpleCharacterController to modify movement speed
            // This would require adding a method like SetMovementSpeedMultiplier(float multiplier)
        }
        
        private void ApplyDamage(float damage)
        {
            // TODO: Integrate with player health system
            Debug.Log($"Player takes {damage} damage from tile: {currentTile.tileType}");
            
            // Example integration:
            // if (playerHealth != null)
            // {
            //     playerHealth.TakeDamage(damage);
            // }
        }
        
        /// <summary>
        /// Get the current tile the player is standing on
        /// </summary>
        public GameTile GetCurrentTile()
        {
            return currentTile;
        }
        
        /// <summary>
        /// Get movement speed multiplier from current tile
        /// </summary>
        public float GetMovementSpeedMultiplier()
        {
            return currentTile != null ? currentTile.GetMovementSpeedMultiplier() : 1.0f;
        }
    }
}

