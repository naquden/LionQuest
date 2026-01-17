using UnityEngine;

/// <summary>
/// Handles dropping items when attached object (enemy) dies.
/// </summary>
public class LootDropper : MonoBehaviour
{
    [SerializeField] private LootTable lootTable;
    
    // Can be called by Enemy script on death
    public void DropLoot()
    {
        if (lootTable == null) return;
        
        GameObject drop = lootTable.GetDrop();
        if (drop != null)
        {
            // Calculate drop position (center of collider or default offset)
            Vector3 spawnPosition = transform.position;
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                spawnPosition = col.bounds.center;
            }
            else
            {
                spawnPosition += Vector3.up * 1f; // Default fallback
            }

            Instantiate(drop, spawnPosition, Quaternion.identity);
            Debug.Log($"[Loot] Dropped {drop.name} from {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Assigns the loot table (used by setup helpers)
    /// </summary>
    public void SetLootTable(LootTable table)
    {
        lootTable = table;
    }
}
