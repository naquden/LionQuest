using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// Pins the Phase 5 downed/revive mechanic.
///
/// Before this, TopDownPlayerController.Die() was permanent and immediate: the first player to hit 0
/// health ended their run for good, even with three teammates alive and fighting a few metres away.
/// That is a hard mode for a co-op game to ship with — one unlucky hit benches a player for the rest
/// of the level. Now a player who dies while at least one teammate is alive goes DOWNED instead of
/// dead: movement/attack/skills lock exactly like death, but a teammate standing close by for
/// reviveDuration brings them back at partial health. Only a player with no living teammate at the
/// moment of the lethal hit dies permanently (see PlayerDeath_EndsTheRunWithAGameOverScreen in
/// GameOverAndAttackRateTests, which covers the solo case and must keep passing unchanged).
///
/// Uses CharacterSelectionData to force a real two-player run, same technique as CoopAggroTests.
/// </summary>
public class ReviveTests : HarnessFixture
{
    const string LevelScene = "Level1";
    const string FallbackCharacterID = "AssCap";

    /// <summary>Dying with a living teammate must go DOWNED, not permanently dead — the run must not
    /// end and the downed player must not be IsAlive.</summary>
    [UnityTest]
    public IEnumerator DyingWithALivingTeammate_GoesDownedNotDead()
    {
        IgnoreFailingLogs();
        yield return LoadCoopLevel();

        TopDownPlayerController[] players = LivingPlayers();
        Assert.AreEqual(2, players.Length, "Expected two players in the seeded co-op run.");

        players[0].TakeDamage(999999f);
        yield return null;

        Assert.IsFalse(players[0].IsAlive, "Player 0 survived a lethal hit.");
        Assert.IsTrue(players[0].IsDowned,
            "Player 0 died permanently despite player 1 being alive — it should have gone downed instead.");
        Assert.IsFalse(PauseMenuController.Instance.IsGameOver,
            "The run ended even though a teammate is still alive and able to revive the downed player.");
    }

    /// <summary>A teammate standing within reviveRadius for reviveDuration must bring the downed
    /// player back at partial health, clear IsDowned, and restore movement/attack.</summary>
    [UnityTest]
    public IEnumerator TeammateStandingNearby_RevivesTheDownedPlayer()
    {
        IgnoreFailingLogs();
        yield return LoadCoopLevel();

        TopDownPlayerController[] players = LivingPlayers();
        Assert.AreEqual(2, players.Length, "Expected two players.");

        TopDownPlayerController downed = players[0];
        TopDownPlayerController reviver = players[1];

        downed.TakeDamage(999999f);
        yield return null;
        Assert.IsTrue(downed.IsDowned, "Player 0 did not go downed — this test cannot prove anything about revive.");

        Teleport(reviver, downed.transform.position);
        yield return null;

        // reviveDuration defaults to 2s; give a small margin.
        yield return new WaitForSeconds(2.5f);

        Assert.IsFalse(downed.IsDowned, "Standing next to the downed player for 2.5s did not revive them.");
        Assert.IsTrue(downed.IsAlive, "The revived player is still not IsAlive.");
        Assert.Greater(downed.CurrentHealth, 0f, "The revived player has 0 health.");
        Assert.Less(downed.CurrentHealth, downed.MaxHealth,
            "The revived player returned at full health — reviveHealthFraction should leave a cost to being downed.");

        Shot("revive-teammate-revived");
    }

    /// <summary>The reviver must actually be nearby — a teammate on the far side of the map must not
    /// revive anyone, and progress must not creep up from a teammate passing through briefly.</summary>
    [UnityTest]
    public IEnumerator TeammateOutOfRange_DoesNotRevive()
    {
        IgnoreFailingLogs();
        yield return LoadCoopLevel();

        TopDownPlayerController[] players = LivingPlayers();
        Assert.AreEqual(2, players.Length, "Expected two players.");

        TopDownPlayerController downed = players[0];
        TopDownPlayerController farAway = players[1];

        downed.TakeDamage(999999f);
        yield return null;
        Assert.IsTrue(downed.IsDowned, "Player 0 did not go downed.");

        Teleport(farAway, downed.transform.position + new Vector3(50f, 0f, 0f));
        yield return new WaitForSeconds(2.5f);

        Assert.IsTrue(downed.IsDowned, "The downed player was revived despite no teammate being nearby.");
        Assert.IsFalse(downed.IsAlive, "The downed player is somehow IsAlive without being revived.");
    }

    /// <summary>
    /// A downed player must not respond to movement input — same lock as permanent death
    /// (TopDownPlayerController.Update's `if (!IsAlive) return` early-out, which IsDowned also
    /// satisfies since IsAlive is false while downed).
    ///
    /// Drives movement the same way DeadPlayer_CannotAttackOrMove does (moveAction input), not via a
    /// raw Rigidbody.AddForce — an external physics impulse is expected to still move a downed OR
    /// dead body (nothing in this game makes either kinematic), so asserting against that would be
    /// pinning behaviour nobody intended to guarantee.
    /// </summary>
    [UnityTest]
    public IEnumerator DownedPlayer_CannotMoveUnderInput()
    {
        IgnoreFailingLogs();
        Gamepad pad = AddPad();
        yield return LoadCoopLevel();

        TopDownPlayerController[] players = LivingPlayers();
        Assert.AreEqual(2, players.Length, "Expected two players.");

        // Both selections use deviceId -1 (see LoadCoopLevel), so PlayerInput auto-picks whichever
        // scheme's devices are free — which of the two players actually ends up paired to the
        // synthetic pad is not guaranteed to be index 0. Identify it the same way CoopCameraTests
        // does, rather than assuming.
        TopDownPlayerController downed = null;
        foreach (TopDownPlayerController candidate in players)
        {
            var input = candidate.GetComponent<PlayerInput>();
            if (input == null) continue;
            foreach (InputDevice device in input.devices)
            {
                if (device == pad) { downed = candidate; break; }
            }
            if (downed != null) break;
        }
        Assert.IsNotNull(downed, "Neither player is paired to the synthetic gamepad — PlayerInput grabbed the keyboard scheme for both.");

        var body = downed.GetComponent<Rigidbody>();
        body.linearVelocity = Vector3.zero;

        downed.TakeDamage(999999f);
        yield return null;
        Assert.IsTrue(downed.IsDowned, "Player did not go downed.");

        Vector3 positionBefore = downed.transform.position;
        yield return HoldStick(pad, Vector2.up, 0.6f);

        Assert.Less(Vector3.Distance(positionBefore, downed.transform.position), 0.5f,
            "A downed player moved under movement input — HandleMovement/ApplyMovementForces should be gated by IsAlive the same as permanent death.");
    }

    IEnumerator LoadCoopLevel()
    {
        CharacterSelectionData data = CharacterSelectionData.GetOrCreate();
        data.Clear();
        data.selections.Add(new CharacterSelectionData.Selection { characterID = FallbackCharacterID, deviceId = -1 });
        data.selections.Add(new CharacterSelectionData.Selection { characterID = FallbackCharacterID, deviceId = -1 });

        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;
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

    static TopDownPlayerController[] LivingPlayers()
    {
        return Object.FindObjectsByType<TopDownPlayerController>(FindObjectsSortMode.None);
    }
}
