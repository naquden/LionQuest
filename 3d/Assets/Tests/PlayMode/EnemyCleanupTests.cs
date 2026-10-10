using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Pins two Phase 3 fixes: dead enemies eventually leave the scene, and living enemies pushed onto
/// the same point separate instead of stacking.
///
/// Neither symptom was visible in a single-enemy test: a lone corpse sitting forever is invisible
/// until you count objects over a full run, and stacking only happens once two or more enemies chase
/// the same target point.
/// </summary>
public class EnemyCleanupTests : HarnessFixture
{
    const string LevelScene = "Level1";

    /// <summary>A killed enemy's GameObject must be destroyed a few seconds later, not linger forever.</summary>
    [UnityTest]
    public IEnumerator DeadEnemy_IsDestroyedAfterCorpseLifetime()
    {
        IgnoreFailingLogs();
        yield return LoadScene(LevelScene);
        yield return null;

        Enemy enemy = FirstLivingEnemy();
        GameObject corpse = enemy.gameObject;

        enemy.TakeDamage(99999f);
        Assert.IsFalse(enemy.IsAlive, "Enemy survived 99999 damage.");

        // corpseLifetime defaults to 5s; give a margin past that.
        yield return new WaitForSeconds(7f);

        Assert.IsTrue(corpse == null,
            "The dead enemy's GameObject is still alive 7s after death. Corpses must despawn, or a " +
            "full run leaves dozens of dead bodies in the scene forever.");
    }

    /// <summary>
    /// Two living enemies forced onto the exact same position must push apart rather than staying
    /// stacked. Both chase the same player, so without separation this is their natural resting state.
    /// </summary>
    [UnityTest]
    public IEnumerator StackedEnemies_PushApart()
    {
        IgnoreFailingLogs();
        yield return LoadScene(LevelScene);
        yield return null;

        Enemy[] enemies = FirstTwoLivingEnemies();
        Vector3 samePoint = enemies[0].transform.position;

        var bodyA = enemies[0].GetComponent<Rigidbody>();
        var bodyB = enemies[1].GetComponent<Rigidbody>();
        bodyA.linearVelocity = Vector3.zero;
        bodyB.linearVelocity = Vector3.zero;
        enemies[0].transform.position = samePoint;
        enemies[1].transform.position = samePoint + new Vector3(0.05f, 0f, 0f); // avoid exact NaN case

        yield return new WaitForSeconds(1.5f);

        float distance = Vector3.Distance(enemies[0].transform.position, enemies[1].transform.position);
        Assert.Greater(distance, 0.5f,
            $"Two enemies forced onto the same point are still {distance:F2}u apart after 1.5s — " +
            "separation is not pushing them apart.");
    }

    static Enemy FirstLivingEnemy()
    {
        foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsAlive) return enemy;
        }
        Assert.Fail($"No living enemy in {LevelScene}.");
        return null;
    }

    static Enemy[] FirstTwoLivingEnemies()
    {
        var picked = new System.Collections.Generic.List<Enemy>(2);
        foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (!enemy.IsAlive) continue;
            picked.Add(enemy);
            if (picked.Count == 2) break;
        }
        Assert.AreEqual(2, picked.Count, $"{LevelScene} needs at least two living enemies.");
        return picked.ToArray();
    }
}
