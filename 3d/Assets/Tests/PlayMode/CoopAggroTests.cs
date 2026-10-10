using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Pins enemy targeting in a real multi-player run.
///
/// The bug this exists for: Enemy resolved its target once, in Start, with
/// GameObject.FindGameObjectWithTag("Player"). A tag lookup returns whichever tagged object Unity
/// happens to find first, so in a four-player game EVERY enemy in the level converged on that one
/// player while the other three walked through the horde untouched — and because the lookup never
/// re-ran, killing the targeted player left the entire level frozen in place. Both halves are
/// invisible in single player, which is why the whole suite passed while co-op was unplayable.
///
/// Targeting now goes through GameSaveController's player registry (FindNearestLivingPlayer), which
/// is also what the project's no-lookup-fallbacks rule requires.
///
/// These tests need two real players, so they seed CharacterSelectionData before loading Level1
/// rather than using its single-player fallback path.
/// </summary>
public class CoopAggroTests : HarnessFixture
{
    const string LevelScene = "Level1";
    const string FallbackCharacterID = "AssCap";

    /// <summary>
    /// Two players, two enemies, one parked beside each player. Each enemy must chase the player it
    /// is standing next to. Under the old tag lookup both would target the same player and this
    /// fails on the second assertion.
    /// </summary>
    [UnityTest]
    public IEnumerator EnemiesTargetTheNearestPlayerNotAlwaysTheFirstOne()
    {
        IgnoreFailingLogs();

        yield return LoadCoopLevel();

        TopDownPlayerController[] players = LivingPlayers();
        Assert.AreEqual(2, players.Length, "Expected two players in the seeded co-op run.");

        // Move the players far apart so "nearest" is unambiguous — at spawn they are ~6u apart, which
        // is close enough that a small parking error could flip which one is nearer.
        Teleport(players[0], new Vector3(-14f, 0f, 0f));
        Teleport(players[1], new Vector3(14f, 0f, 0f));
        yield return null;

        Enemy[] enemies = LivingEnemies(2);
        Park(enemies[0], players[0].transform.position + new Vector3(2f, 0f, 0f));
        Park(enemies[1], players[1].transform.position + new Vector3(2f, 0f, 0f));

        // Longer than Enemy.retargetInterval (0.5s) plus its jitter, so both have retargeted at least
        // once from where they were parked.
        yield return new WaitForSeconds(1.5f);

        TopDownPlayerController targetA = TargetOf(enemies[0]);
        TopDownPlayerController targetB = TargetOf(enemies[1]);

        Assert.AreSame(players[0], targetA,
            $"The enemy parked beside player 0 is targeting {Describe(targetA)} instead. Enemies are not " +
            "picking the nearest player.");
        Assert.AreSame(players[1], targetB,
            $"The enemy parked beside player 1 is targeting {Describe(targetB)} instead. Every enemy is " +
            "converging on one player — the other player is unattackable, which is the tag-lookup bug.");

        Shot("coop-aggro-split");
    }

    /// <summary>
    /// The retarget half: kill the player an enemy is chasing and it must switch to the survivor.
    /// Under the old one-shot lookup the enemy kept its dead target forever and stopped moving, so a
    /// downed player froze the level.
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyRetargetsTheSurvivorWhenItsTargetDies()
    {
        IgnoreFailingLogs();

        yield return LoadCoopLevel();

        TopDownPlayerController[] players = LivingPlayers();
        Assert.AreEqual(2, players.Length, "Expected two players in the seeded co-op run.");

        Teleport(players[0], new Vector3(-14f, 0f, 0f));
        Teleport(players[1], new Vector3(14f, 0f, 0f));
        yield return null;

        Enemy enemy = LivingEnemies(1)[0];
        Park(enemy, players[0].transform.position + new Vector3(2f, 0f, 0f));
        yield return new WaitForSeconds(1.5f);

        Assert.AreSame(players[0], TargetOf(enemy),
            "The enemy did not target the player it was parked next to, so this test cannot prove " +
            "anything about retargeting.");

        // Overkill on purpose: TakeDamage clamps at 0 and Die() runs on the way through.
        players[0].TakeDamage(99999f);
        yield return null;
        Assert.IsFalse(players[0].IsAlive, "Player 0 survived 99999 damage — it is not actually dead.");

        yield return new WaitForSeconds(1.5f);

        TopDownPlayerController target = TargetOf(enemy);
        Assert.AreSame(players[1], target,
            $"After its target died the enemy is targeting {Describe(target)}. It must switch to the " +
            "surviving player — otherwise one death parks every enemy on a corpse and the run stalls.");

        Shot("coop-aggro-retarget");
    }

    /// <summary>
    /// Seeds two confirmed selections and loads Level1. Without this, Level1's PlayerSpawner takes its
    /// single-player fallback path and there is no second player for aggro to be split between.
    ///
    /// Both slots use the same character on purpose: that is the sharing model the game supports, and
    /// it keeps this test independent of which definitions happen to exist in the project.
    /// </summary>
    IEnumerator LoadCoopLevel()
    {
        CharacterSelectionData data = CharacterSelectionData.GetOrCreate();
        data.Clear();
        // deviceId -1 means "pair nothing" — these tests drive players by calling their public API,
        // not through input, so no synthetic devices are needed.
        data.selections.Add(new CharacterSelectionData.Selection { characterID = FallbackCharacterID, deviceId = -1 });
        data.selections.Add(new CharacterSelectionData.Selection { characterID = FallbackCharacterID, deviceId = -1 });

        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;
    }

    /// <summary>
    /// Enemy.targetPlayer, read by reflection. Deliberately checking the enemy's actual chosen target
    /// rather than inferring it from movement: an enemy inside stopDistance does not move at all, so a
    /// position-based check cannot tell "targeting this player" from "targeting nobody".
    /// </summary>
    static TopDownPlayerController TargetOf(Enemy enemy)
    {
        FieldInfo field = typeof(Enemy).GetField("targetPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "Enemy.targetPlayer no longer exists — this test needs updating.");
        return field.GetValue(enemy) as TopDownPlayerController;
    }

    static string Describe(TopDownPlayerController target)
    {
        return target == null ? "nobody" : $"'{target.gameObject.name}' at {target.transform.position}";
    }

    static void Teleport(TopDownPlayerController player, Vector3 position)
    {
        var body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        player.transform.position = position;
    }

    /// <summary>
    /// Puts an enemy at a spot with no momentum, and far enough from the island's other enemies not to
    /// be shoved. Enemies are moved rather than spawned so this exercises the prefab the level ships.
    /// </summary>
    static void Park(Enemy enemy, Vector3 position)
    {
        var body = enemy.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        enemy.transform.position = position;
    }

    static TopDownPlayerController[] LivingPlayers()
    {
        return Object.FindObjectsByType<TopDownPlayerController>(FindObjectsSortMode.None);
    }

    /// <summary>
    /// The requested number of living enemies, picked from the level's own population. Fails loudly on
    /// a short count rather than silently testing fewer enemies than intended.
    /// </summary>
    static Enemy[] LivingEnemies(int count)
    {
        var picked = new System.Collections.Generic.List<Enemy>(count);
        foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (!enemy.IsAlive) continue;
            picked.Add(enemy);
            if (picked.Count == count) break;
        }

        Assert.AreEqual(count, picked.Count, $"{LevelScene} has fewer than {count} living enemies.");
        return picked.ToArray();
    }
}
