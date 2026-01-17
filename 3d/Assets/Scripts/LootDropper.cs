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
            Instantiate(drop, transform.position, Quaternion.identity);
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
