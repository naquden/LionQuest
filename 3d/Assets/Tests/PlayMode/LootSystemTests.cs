using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Pins the Phase 8 loot wiring: every enemy archetype must actually be able to drop loot on death.
///
/// Before this, LootDropper/LootTable/HealthPickup all worked in isolation but no enemy prefab had a
/// LootDropper attached and no LootTable asset existed, so Enemy.Die's GetComponent&lt;LootDropper&gt;()
/// always found nothing.
/// </summary>
public class LootSystemTests : HarnessFixture
{
    /// <summary>The grunt (a plain prefab, not a variant) got its LootDropper directly from
    /// LootSystemGenerator.WireEnemyPrefabs.</summary>
    [UnityTest]
    public IEnumerator GruntEnemy_HasALootDropperWithATable()
    {
        IgnoreFailingLogs();
        yield return LoadScene("Level1");
        yield return null;

        Enemy enemy = Object.FindAnyObjectByType<Enemy>();
        Assert.IsNotNull(enemy, "Level1 has no enemies.");

        LootDropper dropper = enemy.GetComponent<LootDropper>();
        Assert.IsNotNull(dropper, $"{enemy.gameObject.name} has no LootDropper — run 'LionQuest/Generate/Loot System'.");
    }

    /// <summary>
    /// Ranged/Elite/Boss are Unity prefab VARIANTS of the grunt (EnemyArchetypeGenerator forks them
    /// via InstantiatePrefab + SaveAsPrefabAsset on the instance). A variant with no override removing
    /// or replacing a component inherits it from the base at runtime — this checks that inheritance
    /// alone is enough to give every archetype a working LootDropper, without each variant needing its
    /// own separately-authored component.
    /// </summary>
    [UnityTest]
    public IEnumerator EveryArchetypePrefab_InheritsAWorkingLootDropper()
    {
        IgnoreFailingLogs();
        yield return LoadScene("Level1");
        yield return null;

        foreach (string prefabPath in new[]
                 {
                     "Assets/Characters/enemy/EnemyHero Variant.prefab",
                     "Assets/Characters/enemy/Enemy Ranged.prefab",
                     "Assets/Characters/enemy/Enemy Elite.prefab",
                     "Assets/Characters/enemy/Enemy Boss.prefab",
                 })
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, $"{prefabPath} does not exist.");

            GameObject instance = Object.Instantiate(prefab);
            LootDropper dropper = instance.GetComponent<LootDropper>();
            Assert.IsNotNull(dropper, $"{prefabPath}'s runtime instance has no LootDropper.");

            System.Reflection.FieldInfo field = typeof(LootDropper)
                .GetField("lootTable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var table = field?.GetValue(dropper) as LootTable;
            Assert.IsNotNull(table, $"{prefabPath}'s LootDropper has no lootTable assigned — it inherited the " +
                                     "component but not a working reference to it, or the grunt's own wiring is missing.");

            Object.Destroy(instance);
            yield return null;
#endif
        }
    }

    /// <summary>End-to-end: killing an enemy with a 100%-chance table must actually spawn the pickup
    /// in the scene, not merely have the component present.</summary>
    [UnityTest]
    public IEnumerator KillingAnEnemy_CanActuallySpawnAPickup()
    {
        IgnoreFailingLogs();
        yield return LoadScene("Level1");
        yield return null;

        Enemy enemy = Object.FindAnyObjectByType<Enemy>();
        Assert.IsNotNull(enemy, "Level1 has no enemies.");

        LootDropper dropper = enemy.GetComponent<LootDropper>();
        Assert.IsNotNull(dropper, "Enemy has no LootDropper.");

        int pickupsBefore = Object.FindObjectsByType<HealthPickup>(FindObjectsSortMode.None).Length;

        // Force a guaranteed drop rather than relying on the table's real (15%) chance, which would
        // make this test flaky. Temporarily swaps in a 100%-chance table, restores after.
        System.Reflection.FieldInfo field = typeof(LootDropper)
            .GetField("lootTable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var originalTable = field?.GetValue(dropper) as LootTable;
        Assert.IsNotNull(originalTable, "Enemy's LootDropper has no lootTable to test against.");
        Assert.IsTrue(originalTable.items != null && originalTable.items.Count > 0,
            "The loot table has no items — nothing could ever drop.");

        var guaranteedTable = ScriptableObject.CreateInstance<LootTable>();
        guaranteedTable.items = new System.Collections.Generic.List<LootTable.DropItem>
        {
            new LootTable.DropItem { prefab = originalTable.items[0].prefab, dropChance = 100f },
        };
        dropper.SetLootTable(guaranteedTable);

        enemy.TakeDamage(999999f);
        yield return null;

        int pickupsAfter = Object.FindObjectsByType<HealthPickup>(FindObjectsSortMode.None).Length;
        Assert.Greater(pickupsAfter, pickupsBefore,
            "Killing an enemy with a guaranteed-drop loot table did not spawn a HealthPickup — " +
            "LootDropper.DropLoot is not being called from Enemy.Die, or LootTable.GetDrop is not returning the item.");

        Shot("loot-pickup-dropped");
    }
}
