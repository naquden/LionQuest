using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Pins the Phase 4 win condition: once Level1's hand-placed opening wave (wave 1) is cleared,
/// WaveSpawner takes over and spawns further waves, and killing a boss wave's Enemy ends the run in
/// victory via PauseMenuController.
///
/// Before this, a full clear of Level1 left an empty island with nothing left to do — there was no
/// way to "win" the game at all, only to survive an already-placed set of enemies once.
/// </summary>
public class WaveSpawnerTests : HarnessFixture
{
    const string LevelScene = "Level1";

    /// <summary>Clearing every hand-placed opening-wave enemy must cause WaveSpawner to spawn a new
    /// wave of its own, parented separately from the opening wave.</summary>
    [UnityTest]
    public IEnumerator ClearingTheOpeningWave_SpawnsANewWave()
    {
        IgnoreFailingLogs();
        yield return LoadScene(LevelScene);
        yield return null;

        WaveSpawner spawner = Object.FindAnyObjectByType<WaveSpawner>();
        Assert.IsNotNull(spawner, "Level1 has no WaveSpawner — run 'LionQuest/Generate/Level 1'.");

        // No delay between waves for this test, and force the first spawned wave regardless of
        // player count, so the assertion below doesn't depend on tuning constants.
        SetPrivateField(spawner, "delayBetweenWaves", 0f);

        int openingWaveCount = 0;
        foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsAlive) { enemy.TakeDamage(999999f); openingWaveCount++; }
        }
        Assert.Greater(openingWaveCount, 0, "Level1 has no living opening-wave enemies to clear.");

        // WatchOpeningWave polls every 0.5s; give it a few cycles plus spawn time.
        int spawnedEnemies = 0;
        for (float waited = 0f; waited < 5f && spawnedEnemies == 0; waited += 0.5f)
        {
            yield return new WaitForSeconds(0.5f);
            spawnedEnemies = spawner.transform.childCount;
        }

        Assert.Greater(spawnedEnemies, 0,
            "No enemies appeared under WaveSpawner after the opening wave was cleared — wave 2 never spawned.");

        Shot("wavespawner-wave2-spawned");
    }

    /// <summary>Killing a boss wave's Enemy must end the run in victory, not game over, and must not
    /// spawn any further wave.</summary>
    [UnityTest]
    public IEnumerator KillingTheBoss_EndsTheRunInVictory()
    {
        IgnoreFailingLogs();
        yield return LoadScene(LevelScene);
        yield return null;

        WaveSpawner spawner = Object.FindAnyObjectByType<WaveSpawner>();
        Assert.IsNotNull(spawner, "Level1 has no WaveSpawner.");

        // Force every spawned wave to be a boss wave and remove the inter-wave delay, so the very
        // first wave after the opening one is the boss — independent of the tuned interval/delay.
        SetPrivateField(spawner, "bossWaveInterval", 1);
        SetPrivateField(spawner, "delayBetweenWaves", 0f);

        foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsAlive) enemy.TakeDamage(999999f);
        }

        Enemy boss = null;
        for (float waited = 0f; waited < 5f && boss == null; waited += 0.5f)
        {
            yield return new WaitForSeconds(0.5f);
            boss = spawner.GetComponentInChildren<Enemy>();
        }
        Assert.IsNotNull(boss, "No boss wave enemy appeared after the opening wave was cleared.");

        boss.TakeDamage(999999f);
        yield return null;

        Assert.IsTrue(PauseMenuController.Instance.IsVictory,
            "Killing the boss did not set PauseMenuController.IsVictory.");
        Assert.IsFalse(PauseMenuController.Instance.IsGameOver,
            "Victory must not also report as game over — they are mutually exclusive terminal states.");
        Assert.AreEqual(0f, Time.timeScale, "Victory must freeze time, same as game over.");

        // No further wave should spawn once the run has ended. WaitForSecondsRealtime, not
        // WaitForSeconds: ShowVictory just set Time.timeScale = 0, and WaitForSeconds scales with
        // it — at timeScale 0 it never elapses and this coroutine hangs forever.
        int enemiesAfterVictory = spawner.transform.childCount;
        yield return new WaitForSecondsRealtime(2f);
        Assert.AreEqual(enemiesAfterVictory, spawner.transform.childCount,
            "A new wave spawned after victory — WaveSpawner's loop did not stop.");

        Shot("wavespawner-victory");
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"WaveSpawner.{fieldName} no longer exists — this test needs updating.");
        field.SetValue(target, value);
    }
}
