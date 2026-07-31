using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Two reported bugs, both about things that were silently *not* happening:
///
///   1. Health reaching 0 was not game over. TopDownPlayerController.Die() logged, greyed the HUD
///      portrait and fired OnDeath — but nothing listened, so the run carried on and the player
///      kept walking, swinging and casting with a body at 0 health. The HUD's "0 / 0" (a hardcoded
///      literal in HandleDeath, not the real max) was the only sign anything had happened.
///   2. The basic attack could be spammed. CombatController rate-limits the effect spawn by
///      AttackData.cooldown, so extra presses dealt no extra damage — but HandleAttack replayed the
///      swing animation on every single press, which looks exactly like a working machine gun.
///
/// Runs in Level1, which auto-spawns a fallbackDefinition player from PlayerSpawner.Start, so no
/// character-select join flow is needed to get a player who can die.
/// </summary>
public class GameOverAndAttackRateTests : HarnessFixture
{
    const string LevelScene = "Level1";

    /// <summary>Matches TopDownPlayerController.basicAttackMinInterval and
    /// AssassinoCapuchino/BasicAttack.asset's cooldown — the gate is the max of the two.</summary>
    const float BasicAttackInterval = 0.5f;

    /// <summary>Player/Attack's keyboard binding. Input here goes through the keyboard, not a
    /// gamepad: Level1's player comes from PlayerSpawner.SpawnFallbackPlayer with
    /// pairWithDevice == null, so PlayerInput auto-picks the first control scheme
    /// ("Keyboard&amp;Mouse") and a synthetic gamepad's events are never routed to it.</summary>
    const Key AttackKey = Key.Space;

    /// <summary>The log HandleAttack emits once per ACCEPTED press (it sits after the rate-limit
    /// early-out), which makes it the only observable that distinguishes "the press was accepted and
    /// the swing animation replayed" from "the press was refused".</summary>
    const string AcceptedAttackLog = "Attack button pressed";

    // ------------------------------------------------------------------
    // Game over
    // ------------------------------------------------------------------

    /// <summary>
    /// The headline assertion: health hits 0 -> the game-over panel is up, time is frozen, and the
    /// title says so. Before the fix every one of these was false.
    /// </summary>
    [UnityTest]
    public IEnumerator PlayerDeath_EndsTheRunWithAGameOverScreen()
    {
        IgnoreFailingLogs();
        yield return LoadScene(LevelScene);
        yield return null;

        TopDownPlayerController player = FindPlayer();
        PauseMenuController menu = PauseMenuController.Instance;
        Assert.IsNotNull(menu,
            $"{LevelScene} has no PauseMenuController — it doubles as the game-over screen, so death " +
            "cannot be shown at all. Run 'LionQuest/Generate/Level1'.");
        Assert.IsFalse(menu.IsGameOver, "The run reported game over before anyone died.");

        player.TakeDamage(player.MaxHealth);
        yield return null;

        Assert.IsFalse(player.IsAlive, "Damage equal to max health did not kill the player.");
        Assert.IsTrue(menu.IsGameOver,
            "The player's health reached 0 and the run did not end. TopDownPlayerController.OnDeath " +
            "fires, so the missing link is PlayerSpawner registering the controller with " +
            "PauseMenuController.RegisterPlayer(TopDownPlayerController).");
        Assert.IsTrue(menu.IsOpen, "Game over was reported but the menu is not open, so nothing is on screen.");
        Assert.IsTrue(GetPanel(menu).activeInHierarchy, "The game-over panel object is inactive — the screen is invisible.");
        Assert.AreEqual(0f, Time.timeScale, 0.0001f,
            "Time is still running on the game-over screen, so enemies keep moving behind it.");

        TMP_Text title = GetField<TMP_Text>(menu, "titleText");
        Assert.IsNotNull(title, "PauseMenuController.titleText is not assigned — the game-over panel still reads 'Paused'.");
        Assert.AreEqual("Game Over", title.text, $"The game-over panel's title reads '{title.text}'.");

        Shot("gameover-panel");
    }

    /// <summary>Retry replaces Resume, and only one of the two is ever on screen: offering "Resume"
    /// after death would imply a run that can be continued.</summary>
    [UnityTest]
    public IEnumerator GameOver_OffersRetryInsteadOfResume()
    {
        IgnoreFailingLogs();
        yield return LoadScene(LevelScene);
        yield return null;

        PauseMenuController menu = PauseMenuController.Instance;
        Button resume = GetField<Button>(menu, "resumeButton");
        Button retry = GetField<Button>(menu, "retryButton");
        Assert.IsNotNull(retry, "PauseMenuController.retryButton is not assigned — there is no way to restart the run. " +
                                "Run 'LionQuest/Generate/Pause Menu'.");
        Assert.IsNotNull(resume, "PauseMenuController.resumeButton is not assigned.");

        Assert.IsFalse(retry.gameObject.activeSelf, "Retry is visible before anyone died.");

        FindPlayer().TakeDamage(9999f);
        yield return null;

        Assert.IsTrue(retry.gameObject.activeInHierarchy, "Game over does not show the Retry button.");
        Assert.IsFalse(resume.gameObject.activeSelf,
            "Game over still shows Resume. There is nothing to resume — the player is dead.");

        // The keyboard/gamepad focus must land on something usable, or a pad-only player is stuck.
        Assert.IsTrue(retry.navigation.mode == Navigation.Mode.Explicit,
            "Retry's navigation is not explicit, so pad navigation on the game-over screen is undefined.");
        Button selection = GetField<Button>(menu, "characterSelectionButton");
        Assert.AreSame(retry, selection.navigation.selectOnUp,
            "Character Selection still navigates up to the hidden Resume button, which dead-ends pad input.");
    }

    /// <summary>
    /// A dead player must be inert. Update's early-out is what stops it: before the fix, Die() only
    /// disabled input on the shared-asset fallback path, so every PlayerSpawner-created player (they
    /// all have a PlayerInput) kept full control of its corpse.
    /// </summary>
    [UnityTest]
    public IEnumerator DeadPlayer_CannotAttackOrMove()
    {
        IgnoreFailingLogs();
        Keyboard keyboard = AddKeyboard();
        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;

        TopDownPlayerController player = FindPlayer();
        Enemy enemy = FirstLivingEnemy();

        // Park the enemy where a swing would certainly hit it, so a working attack is visible as
        // damage rather than having to be inferred from the absence of an effect object.
        ParkEnemyOnPlayer(enemy, player, distance: 1f);
        float enemyHealthBefore = enemy.CurrentHealth;
        Vector3 positionBefore = player.transform.position;

        player.TakeDamage(9999f);
        yield return null;
        Assert.IsFalse(player.IsAlive, "Player did not die.");

        // Hold attack + walk right. Time is frozen by the game-over screen, so drive frames rather
        // than wall-clock waits.
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(AttackKey, Key.D));
        for (int i = 0; i < 30; i++) yield return null;

        Assert.AreEqual(enemyHealthBefore, enemy.CurrentHealth, 0.001f,
            $"A dead player still damaged an enemy ({enemyHealthBefore} -> {enemy.CurrentHealth}): " +
            "the attack button works on a corpse.");
        // Weaker than the damage assertion above: the game-over screen also sets timeScale to 0,
        // which stops FixedUpdate (and therefore ApplyMovementForces) on its own. Kept because it
        // is the thing a player would notice, and it still catches a corpse coasting on residual
        // velocity if the freeze is ever removed.
        Assert.Less(Vector3.Distance(positionBefore, player.transform.position), 0.5f,
            "A dead player moved under movement input.");
    }

    /// <summary>The HUD must not claim max health is 0. "0 / 0" was a literal in HandleDeath, and it
    /// is what made the bug look like a broken health bar rather than a death.</summary>
    [UnityTest]
    public IEnumerator DeadPlayerHud_ShowsZeroOutOfRealMaxHealth()
    {
        IgnoreFailingLogs();
        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;

        TopDownPlayerController player = FindPlayer();
        var hud = Object.FindAnyObjectByType<PlayerHUD>();
        Assert.IsNotNull(hud, $"{LevelScene} has no PlayerHUD.");

        float maxHealth = player.MaxHealth;
        Assert.Greater(maxHealth, 0f, "The player spawned with 0 max health, so this test proves nothing.");

        player.TakeDamage(9999f);
        yield return null;

        TMP_Text healthText = FindHealthText(hud);
        Assert.AreEqual($"0 / {Mathf.CeilToInt(maxHealth)}", healthText.text,
            $"The dead player's HUD reads '{healthText.text}'. It must show 0 out of the real max, not '0 / 0' — " +
            "the latter reads as an unset/broken health bar instead of as a death.");

        Shot("gameover-hud-health");
    }

    /// <summary>Pause must not be able to undo game over: Toggle would retitle the panel "Paused"
    /// and re-offer Resume, handing control back to a corpse.</summary>
    [UnityTest]
    public IEnumerator GameOver_CannotBeDismissedByPausing()
    {
        IgnoreFailingLogs();
        yield return LoadScene(LevelScene);
        yield return null;

        PauseMenuController menu = PauseMenuController.Instance;
        FindPlayer().TakeDamage(9999f);
        yield return null;
        Assert.IsTrue(menu.IsGameOver, "Player death did not end the run.");

        menu.Toggle();
        yield return null;

        Assert.IsTrue(menu.IsGameOver, "Toggling the pause menu cleared the game-over state.");
        Assert.IsTrue(menu.IsOpen, "Toggling the pause menu closed the game-over screen.");
        Assert.AreEqual("Game Over", GetField<TMP_Text>(menu, "titleText").text,
            "Toggling the pause menu retitled the game-over screen back to 'Paused'.");
        Assert.AreEqual(0f, Time.timeScale, 0.0001f, "Toggling the pause menu un-froze a finished run.");
    }

    // ------------------------------------------------------------------
    // Basic attack rate limit
    // ------------------------------------------------------------------

    /// <summary>
    /// Mash the attack button every other frame for a second and count the swings that actually
    /// happened. At 0.5s apart, one second of mashing yields 2 or 3 (whether the very first frame
    /// counts and where the interval boundary lands are both timing-dependent) — never one per
    /// press, which is what an ungated HandleAttack allowed.
    ///
    /// Counts two independent things, because they were governed by two different gates:
    ///   - accepted presses, via the one log line HandleAttack emits per accepted swing. This is the
    ///     gate added for the spam bug, and the only thing that covers the animation: CombatController
    ///     already capped effect spawns, so a spammed button dealt no extra damage but did replay the
    ///     swing animation on every press, which is what made it look like a working machine gun.
    ///   - distinct spawned AttackEffect instances, i.e. the actual damage rate (enforced by both the
    ///     new gate and CombatController's per-AttackData cooldown). Effects outlive their spawn
    ///     frame (hitWindowDuration + 0.1s minimum), so none are missed between samples.
    /// </summary>
    [UnityTest]
    public IEnumerator BasicAttack_SpammingTheButtonIsRateLimited()
    {
        IgnoreFailingLogs();
        Keyboard keyboard = AddKeyboard();
        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;

        TopDownPlayerController player = FindPlayer();
        Assert.IsTrue(player.IsAlive, "Player must be alive to attack.");

        var seenEffects = new System.Collections.Generic.HashSet<int>();
        CountEffects(seenEffects); // ignore anything already in flight

        int acceptedPresses = 0;
        Application.LogCallback countPresses = (condition, stackTrace, type) =>
        {
            if (condition != null && condition.Contains(AcceptedAttackLog)) acceptedPresses++;
        };
        Application.logMessageReceived += countPresses;

        // 1s of alternating press/release, i.e. a WasPressedThisFrame on roughly every other frame.
        const float SpamSeconds = 1f;
        float deadline = Time.time + SpamSeconds;
        try
        {
            while (Time.time < deadline)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(AttackKey));
                yield return null;
                CountEffects(seenEffects);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                CountEffects(seenEffects);
            }
        }
        finally
        {
            Application.logMessageReceived -= countPresses;
        }

        int swings = seenEffects.Count;
        int maxAllowed = Mathf.CeilToInt(SpamSeconds / BasicAttackInterval) + 1; // +1 for the free first swing

        Assert.GreaterOrEqual(acceptedPresses, 1,
            "Spamming the attack button produced no attack at all — the rate limit is blocking everything.");
        Assert.LessOrEqual(acceptedPresses, maxAllowed,
            $"{acceptedPresses} attack presses were accepted in {SpamSeconds}s. At a {BasicAttackInterval}s " +
            $"minimum interval at most {maxAllowed} are allowed — the attack button can be spammed " +
            "(each accepted press also replays the swing animation).");
        Assert.LessOrEqual(swings, maxAllowed,
            $"{swings} attack effects spawned in {SpamSeconds}s, more than the {maxAllowed} a " +
            $"{BasicAttackInterval}s interval allows — the damage rate is not capped.");

        Debug.Log($"[test] {acceptedPresses} accepted presses / {swings} effects in {SpamSeconds}s (cap {maxAllowed}).");
    }

    /// <summary>Adds every AttackEffect currently alive to <paramref name="seen"/>, keyed by instance
    /// id so a re-observed effect is not double-counted.</summary>
    static void CountEffects(System.Collections.Generic.HashSet<int> seen)
    {
        foreach (AttackEffect effect in Object.FindObjectsByType<AttackEffect>(FindObjectsSortMode.None))
        {
            seen.Add(effect.GetInstanceID());
        }
    }

    /// <summary>
    /// The interval itself, measured: the moment after a swing the attack must be on cooldown, and
    /// the remaining time must be the full interval rather than some shorter accidental value.
    /// </summary>
    [UnityTest]
    public IEnumerator BasicAttack_CooldownIsTheConfiguredInterval()
    {
        IgnoreFailingLogs();
        Keyboard keyboard = AddKeyboard();
        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;

        TopDownPlayerController player = FindPlayer();
        Assert.AreEqual(0f, player.BasicAttackCooldownRemaining, 0.001f,
            "The basic attack starts on cooldown, so the very first swing of a run is delayed.");

        // Sampled by watching the gate rather than after PressKey returns: PressKey burns 3 frames,
        // and in batchmode a frame can be long enough to eat a measurable slice of a 0.5s interval,
        // which would make an exact-duration assertion flaky for reasons unrelated to the gate.
        float peakRemaining = 0f;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(AttackKey));
        for (int i = 0; i < 4 && peakRemaining <= 0f; i++)
        {
            yield return null;
            peakRemaining = Mathf.Max(peakRemaining, player.BasicAttackCooldownRemaining);
        }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());

        Assert.Greater(peakRemaining, 0f,
            "The basic attack is ready again immediately after swinging — it is not rate-limited at all.");
        Assert.LessOrEqual(peakRemaining, BasicAttackInterval + 0.001f,
            $"The basic attack cooldown is {peakRemaining}s, longer than the configured {BasicAttackInterval}s.");
        Assert.Greater(peakRemaining, BasicAttackInterval * 0.5f,
            $"The basic attack cooldown is only {peakRemaining}s — well under the configured {BasicAttackInterval}s.");

        // ...and it clears on its own.
        yield return new WaitForSeconds(BasicAttackInterval + 0.2f);
        Assert.AreEqual(0f, player.BasicAttackCooldownRemaining, 0.001f,
            "The basic attack never became ready again after its interval elapsed.");
    }

    /// <summary>
    /// The rate limit must not throttle skills: they run off the shared skill lock and pass
    /// bypassCooldown, and a player who unlocked a 2s shuriken should not inherit the swing's gate.
    /// </summary>
    [UnityTest]
    public IEnumerator BasicAttackRateLimit_DoesNotAffectTheSkillCooldown()
    {
        IgnoreFailingLogs();
        Keyboard keyboard = AddKeyboard();
        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;

        TopDownPlayerController player = FindPlayer();
        yield return PressKey(keyboard, AttackKey);

        Assert.Greater(player.BasicAttackCooldownRemaining, 0f, "The basic attack did not start its cooldown.");
        Assert.AreEqual(0f, player.SkillCooldownRemaining, 0.001f,
            "Swinging the basic attack put the skills on cooldown. The two gates are separate: skills " +
            "share the skill lock, the basic attack has its own interval.");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

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

    static void ParkEnemyOnPlayer(Enemy enemy, TopDownPlayerController player, float distance)
    {
        var body = enemy.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        enemy.transform.position = player.transform.position + new Vector3(distance, 0f, 0f);
    }

    static GameObject GetPanel(PauseMenuController menu) => GetField<GameObject>(menu, "panel");

    /// <summary>Reads a [SerializeField] the generator wired, so the test asserts on the same
    /// reference the runtime uses instead of re-finding it by name in the hierarchy.</summary>
    static T GetField<T>(object target, string fieldName) where T : class
    {
        FieldInfo field = target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{target.GetType().Name} has no field '{fieldName}'.");
        return field.GetValue(target) as T;
    }

    static TMP_Text FindHealthText(PlayerHUD hud)
    {
        var panels = GetField<PlayerHUDPanel[]>(hud, "panels");
        Assert.IsNotNull(panels, "PlayerHUD.panels is not assigned.");
        Assert.Greater(panels.Length, 0, "PlayerHUD has no panels.");
        TMP_Text healthText = GetField<TMP_Text>(panels[0], "healthText");
        Assert.IsNotNull(healthText, "PlayerHUDPanel.healthText is not assigned.");
        return healthText;
    }
}
