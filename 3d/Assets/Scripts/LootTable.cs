using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Loot Table", menuName = "LionQuest/Loot Table")]
public class LootTable : ScriptableObject
{
    [System.Serializable]
    public class DropItem
    {
        public GameObject prefab;
        [Range(0, 100)] public float dropChance = 10f; // Percentage
    }

    public List<DropItem> items;

    /// <summary>
    /// Returns a random item based on chance, or null if nothing drops.
    /// </summary>
    public GameObject GetDrop()
    {
        float random = Random.Range(0f, 100f);
        
        foreach (var item in items)
        {
            if (random <= item.dropChance)
            {
                return item.prefab;
            }
            random -= item.dropChance; // Cumulative probability
        }
        
        return null;
    }
}
