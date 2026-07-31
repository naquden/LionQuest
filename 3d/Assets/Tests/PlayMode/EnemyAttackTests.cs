using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Pins the enemy's side of combat: an enemy that reaches the player must damage it.
///
/// This existed as code the whole time (Enemy.Attack -> CombatController.PerformAttack) but could
/// never land a hit, because three pieces of wiring were missing at once — Enemy.attackData was null,
/// the enemy's CombatController.targetTag said "Enemy", and the slash effect prefab had no
/// AttackEffect component or collider. Each one alone is silent: no exception, the swing animation
/// still plays. Only the player's health tells you, so that is what these tests assert on.
///
/// EnemyCombatGenerator owns that wiring; this suite is what proves it is still connected.
/// </summary>
public class EnemyAttackTests : HarnessFixture
{
    const string LevelScene = "Level1";
    const float EnemyAttackDamage = 10f; // Assets/Characters/enemy/BasicAttack.asset

    /// <summary>
    /// The whole chain, end to end: park an enemy on top of the player, let it swing on its own
    /// schedule, and require the player's health to drop. Nothing is called directly — Enemy.Update
    /// decides to attack, CombatController spawns the effect, and AttackEffect's trigger finds the
    /// player.
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyStandingOnThePlayerDamagesIt()
    {
        IgnoreFailingLogs();
        // Level1 spawns a fallbackDefinition player from PlayerSpawner.Start when there is no
        // CharacterSelectionData, so no join flow is needed to get a player under attack.
        yield return LoadScene(LevelScene);
        yield return null;

        TopDownPlayerController player = FindPlayer();
        Enemy enemy = FirstLivingEnemy();

        float healthBefore = player.CurrentHealth;
        Assert.Greater(healthBefore, 0f, "Player spawned with no health, so damage cannot be observed.");

        // Inside Enemy.stopDistance (1.5), which is what gates Attack().
        ParkEnemyOnPlayer(enemy, player, distance: 1f);

        // Enemy.attackCooldown is 2s and lastAttackTime starts at 0, so the first swing can come
        // immediately; allow a couple of cycles plus the effect's hit window before giving up.
        yield return WaitUntilPlayerHurt(player, healthBefore, timeout: 6f);

        Assert.Less(player.CurrentHealth, healthBefore,
            $"An enemy stood 1m from the player for 6s and swung, but the player's health never moved " +
            $"({healthBefore} -> {player.CurrentHealth}). The enemy's attack does not reach the player: " +
            "check Enemy.attackData is assigned, the enemy CombatController's targetTag is 'Player', and " +
            "Basic Slash Enemy.prefab has an AttackEffect with a collider (run LionQuest/Generate/Enemy Combat).");

        Assert.AreEqual(healthBefore - EnemyAttackDamage, player.CurrentHealth, 0.001f,
            $"One enemy swing should take {EnemyAttackDamage} health, but it took " +
            $"{healthBefore - player.CurrentHealth}.");

        Shot("enemy-attack-player-hurt");
    }

    /// <summary>
    /// The player's HUD health bar must follow the damage. A hit the player cannot see is the same
    /// bug from their side, and TopDownPlayerController.TakeDamage is the only thing that fires
    /// OnHealthChanged.
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyDamageUpdatesTheHudHealthBar()
    {
        IgnoreFailingLogs();
        yield return LoadScene(LevelScene);
        yield return null;

        TopDownPlayerController player = FindPlayer();
        Enemy enemy = FirstLivingEnemy();

        var hud = Object.FindAnyObjectByType<PlayerHUD>();
        Assert.IsNotNull(hud, $"{LevelScene} has no PlayerHUD, so health cannot be shown.");

        float healthBefore = player.CurrentHealth;
        ParkEnemyOnPlayer(enemy, player, distance: 1f);
        yield return WaitUntilPlayerHurt(player, healthBefore, timeout: 6f);

        Assert.Less(player.CurrentHealth, healthBefore, "Enemy never damaged the player.");

        // Let the HUD panel's health binding run.
        yield return null;
        Shot("enemy-attack-hud-health");
    }

    /// <summary>
    /// An enemy swing must not hit other enemies. The effect prefab's targetTag was "Enemy" before
    /// this was fixed, which is exactly the state where a swarm kills itself and pays the player
    /// souls for standing still.
    /// </summary>
    [UnityTest]
    public IEnumerator EnemySwingDoesNotHurtOtherEnemies()
    {
        IgnoreFailingLogs();
        yield return LoadScene(LevelScene);
        yield return null;

        TopDownPlayerController player = FindPlayer();

        Enemy attacker = null;
        Enemy bystander = null;
        foreach (Enemy candidate in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (!candidate.IsAlive) continue;
            if (attacker == null) attacker = candidate;
            else if (bystander == null) { bystander = candidate; break; }
        }
        Assert.IsNotNull(bystander, $"{LevelScene} needs at least two enemies for this test.");

        // Both crowded onto the player, so the attacker's hit sphere certainly contains the other.
        ParkEnemyOnPlayer(attacker, player, distance: 1f);
        ParkEnemyOnPlayer(bystander, player, distance: 1.2f);

        float bystanderHealthBefore = bystander.CurrentHealth;
        float playerHealthBefore = player.CurrentHealth;

        yield return WaitUntilPlayerHurt(player, playerHealthBefore, timeout: 6f);
        Assert.Less(player.CurrentHealth, playerHealthBefore,
            "The attacking enemy never hit the player, so this test proves nothing about friendly fire.");

        Assert.AreEqual(bystanderHealthBefore, bystander.CurrentHealth, 0.001f,
            $"An enemy swing damaged another enemy ({bystanderHealthBefore} -> {bystander.CurrentHealth}). " +
            "The enemy attack's targetTag is not restricted to 'Player'.");
    }

    /// <summary>
    /// A dead player must stop taking damage. TakeDamage guards on IsAlive, but the enemy keeps
    /// swinging at the corpse, so a regression here would drive health negative and re-fire Die().
    /// Death now also raises the game-over screen, which freezes time and stops the enemy swinging
    /// at all — so this passes for two reasons at once. The TakeDamage guard is still the one that
    /// matters, since the freeze is a UI decision that could change.
    /// </summary>
    [UnityTest]
    public IEnumerator DeadPlayerStopsTakingDamage()
    {
        IgnoreFailingLogs();
        yield return LoadScene(LevelScene);
        yield return null;

        TopDownPlayerController player = FindPlayer();
        Enemy enemy = FirstLivingEnemy();

        // Kill outright rather than waiting for ~10 swings at 2s each.
        player.TakeDamage(player.MaxHealth * 2f);
        yield return null;

        Assert.IsFalse(player.IsAlive, "Damage exceeding max health did not kill the player.");
        Assert.AreEqual(0f, player.CurrentHealth, 0.001f,
            $"A dead player's health is {player.CurrentHealth}; it must clamp at 0, not go negative.");

        ParkEnemyOnPlayer(enemy, player, distance: 1f);
        // Realtime, not scaled: the player's death now raises the game-over screen, which sets
        // Time.timeScale to 0 — a scaled WaitForSeconds would never complete and this test would
        // hang until the suite timed out.
        yield return new WaitForSecondsRealtime(3f); // more than one enemy attack cooldown

        Assert.AreEqual(0f, player.CurrentHealth, 0.001f,
            $"An enemy kept damaging a dead player: health went to {player.CurrentHealth}.");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Teleports the enemy next to the player and kills its momentum, so it is inside
    /// stopDistance immediately instead of walking there over several seconds.</summary>
    static void ParkEnemyOnPlayer(Enemy enemy, TopDownPlayerController player, float distance)
    {
        var body = enemy.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        enemy.transform.position = player.transform.position + new Vector3(distance, 0f, 0f);
        enemy.transform.rotation = Quaternion.LookRotation(-Vector3.right, Vector3.up);
    }

    /// <summary>Waits for the player's health to drop below its starting value, re-parking nothing and
    /// calling nothing — the enemy's own Update/attack cooldown drives the hit.</summary>
    static IEnumerator WaitUntilPlayerHurt(TopDownPlayerController player, float healthBefore, float timeout)
    {
        float deadline = Time.time + timeout;
        while (Time.time < deadline && player.CurrentHealth >= healthBefore)
        {
            yield return null;
        }
    }

    static TopDownPlayerController FindPlayer()
    {
        var player = Object.FindAnyObjectByType<TopDownPlayerController>();
        Assert.IsNotNull(player, $"No player spawned in {LevelScene} — PlayerSpawner did not run.");
        return player;
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
}
