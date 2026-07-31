using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The whole game as a new player actually meets it, driven end to end through the real screens and
/// real input: CharacterSelection -> join -> pick AssCap -> Level1 -> kill enemies -> pause ->
/// back to CharacterSelection -> spend the earned souls in the upgrade screen -> start the next
/// round and verify the thing that was just bought behaves differently in game.
///
/// Every other suite here pins one screen. This one pins the LOOP, which is where the interesting
/// failures live: souls earned in round N must be spendable in the menu between rounds, a node
/// bought in the menu must change combat in round N+1, and the HUD must agree with all of it
/// (lock mark gone, cooldown indicator counting the NEW skill's own cooldown).
///
/// Facts this test asserts against, read off the authored assets rather than assumed:
///   Enemy.maxHealth                 100
///   BasicAttack.damage               60   (AssCap)
///   attributes/strength_1            +1 strength level per rank, cost 10 -> damage x1.2
///   whirlwind/unlock                 cost 20, skill slot 1, cooldown 10s, damage x2, radius 3m
///   shuriken/unlock                  cost 20, skill slot 2, cooldown 2s, 25 dmg x 2 blades
///   battlecry/unlock                 cost 20, skill slot 3, cooldown 10s, speed x2 for 5s
///   soul reward                      1 per enemy killed (GameSaveController.OnEnemyKilled)
/// </summary>
public class NewPlayerJourneyTests : HarnessFixture
{
    const int SkillSlotCount = 3;
    const string LevelScene = "Level1";
    const string SelectionScene = "CharacterSelection";

    const float EnemyMaxHealth = 100f;
    const float BasicAttackDamage = 60f;

    /// <summary>
    /// Round 1 — the new player's first ever run. Nothing is bought yet, so all three skill slots
    /// must be visibly locked AND refuse their buttons, the basic attack must be the unmodified
    /// authored damage, and killing 10 enemies must bank exactly 10 souls that survive the trip
    /// back to character selection.
    /// </summary>
    [UnityTest]
    public IEnumerator Round1_FreshPlayerKillsTenEnemiesAndBanksSouls()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();
        yield return JoinAndStartRun(pad, "r1");

        PlayerHUDPanel panel = GetBoundPanel();
        TopDownPlayerController player = GetPanelPlayer(panel);
        CharacterStats stats = GetPlayerStats(player);

        Assert.AreEqual(0, stats.souls,
            "[r1] A brand new player starts with souls it never earned — the save was not reset.");

        // --- All three skills locked, and the HUD says so ---
        for (int slot = 1; slot <= SkillSlotCount; slot++)
        {
            AssertHudSlotState(panel, player, slot, expectUnlocked: false, "r1");
        }
        Shot("journey-r1-all-locked");

        // --- Locked means the button does nothing, not merely that it looks locked ---
        GamepadButton[] skillButtons = { GamepadButton.North, GamepadButton.East, GamepadButton.RightShoulder };
        for (int slot = 1; slot <= SkillSlotCount; slot++)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                yield return Press(pad, skillButtons[slot - 1]);
                yield return null;
            }
            Assert.AreEqual(0f, player.SkillCooldownNormalized, 0.001f,
                $"[r1] Slot {slot} is drawn LOCKED but pressing its button fired the skill (a cooldown " +
                "started). The HUD and the input gate disagree.");
        }

        // --- Basic attack is never gated by the skill tree: it must work, at base damage ---
        var combat = player.GetComponent<CombatController>();
        Assert.AreEqual(1f, combat.GetDamageMultiplier(), 0.001f,
            "[r1] Damage multiplier is not 1.0x on a fresh save — nothing has been bought yet.");

        Enemy probe = FirstLivingEnemy("r1");
        float healthBefore = probe.CurrentHealth;
        probe.TakeDamage(BasicAttackDamage * combat.GetDamageMultiplier(), Vector3.forward, 0f);
        yield return null;
        Assert.AreEqual(healthBefore - BasicAttackDamage, probe.CurrentHealth, 0.001f,
            $"[r1] One basic attack should take {BasicAttackDamage} health off a fresh enemy at 1.0x, " +
            $"but it went {healthBefore} -> {probe.CurrentHealth}.");
        Assert.AreEqual(EnemyMaxHealth, probe.MaxHealth, 0.001f,
            "[r1] Enemy max health is not the authored 100 — the damage arithmetic below is calibrated to it.");

        // --- Ten kills, ten souls ---
        yield return KillEnemies(player, 10, "r1");
        Assert.AreEqual(10, stats.souls, "[r1] Ten kills did not bank ten souls.");
        Shot("journey-r1-after-ten-kills");

        // --- Souls must survive the exit back to the menu (that is what pays for the upgrade) ---
        yield return ExitToCharacterSelection(pad, "r1");

        var manager = Object.FindAnyObjectByType<CharacterSelectionManager>();
        CharacterDefinition character = GetRosterCharacter(manager, 0);
        CharacterSaveData saved = SaveManager.LoadCharacter(character.characterID);
        Assert.AreEqual(10, saved.souls,
            $"[r1] Ten souls were earned in the run but the save on disk holds {saved.souls} after " +
            "leaving it — progress is lost between rounds, so nothing can ever be bought.");
        Shot("journey-r1-back-at-selection");
    }

    /// <summary>
    /// Rounds 2 and 3 — the first skill unlock and its verification in the round AFTER buying it.
    ///
    /// A skill unlock costs 20 souls and a kill pays 1, so one 10-kill round cannot fund one: the
    /// player plays a second round, banks 20, and only then can buy. That is the loop this test
    /// walks. Round 3 then proves the purchase reached the game: HUD lock mark gone, the button
    /// accepted, the cooldown indicator running Whirlwind's own 10s, and real AoE damage dealt to
    /// enemies standing inside its 3m radius.
    /// </summary>
    [UnityTest]
    public IEnumerator Round2And3_UnlockWhirlwindThenVerifyItInTheNextRound()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();

        // --- Round 1: 10 kills ---
        yield return JoinAndStartRun(pad, "r1");
        TopDownPlayerController player = GetPanelPlayer(GetBoundPanel());
        yield return KillEnemies(player, 10, "r1");
        yield return ExitToCharacterSelection(pad, "r1");

        // --- Round 2: 10 more kills, banking the 20 souls an unlock costs ---
        yield return JoinAndStartRun(pad, "r2");
        PlayerHUDPanel panel = GetBoundPanel();
        player = GetPanelPlayer(panel);
        CharacterStats stats = GetPlayerStats(player);

        Assert.AreEqual(10, stats.souls,
            "[r2] The 10 souls earned in round 1 did not carry into round 2 — the run does not reload " +
            "the saved balance, so nothing can ever be banked toward a purchase.");
        // Still locked: no purchase has happened yet, only earning.
        AssertHudSlotState(panel, player, 1, expectUnlocked: false, "r2");

        yield return KillEnemies(player, 10, "r2");
        Assert.AreEqual(20, stats.souls, "[r2] Two rounds of 10 kills did not bank 20 souls.");
        yield return ExitToCharacterSelection(pad, "r2");

        // --- Between rounds: buy the Whirlwind unlock (category 1, row 0) ---
        yield return BuyUpgrade(pad, categoryIndex: 1, rowIndex: 0, "whirlwind", "unlock", "r2");

        var manager = Object.FindAnyObjectByType<CharacterSelectionManager>();
        CharacterDefinition character = GetRosterCharacter(manager, 0);
        CharacterSaveData afterBuy = SaveManager.LoadCharacter(character.characterID);
        Assert.IsTrue(SkillTreeService.IsSkillUnlocked(character, afterBuy, "whirlwind"),
            "[r2] The Whirlwind unlock row was bought but IsSkillUnlocked is still false.");
        Assert.AreEqual(0, afterBuy.souls, "[r2] The 20-soul unlock did not consume the whole balance.");

        // --- Round 3: the newly unlocked skill must now work in game ---
        yield return JoinAndStartRun(pad, "r3");
        panel = GetBoundPanel();
        player = GetPanelPlayer(panel);

        AssertHudSlotState(panel, player, 1, expectUnlocked: true, "r3");
        // The other two are untouched by this purchase and must stay locked — buying one unlock must
        // not open the rest.
        AssertHudSlotState(panel, player, 2, expectUnlocked: false, "r3");
        AssertHudSlotState(panel, player, 3, expectUnlocked: false, "r3");
        Shot("journey-r3-whirlwind-unlocked");

        // --- The skill's real effect and its cooldown, from one real Y press ---
        // Park an enemy inside Whirlwind's 3m radius first, so the same press that starts the
        // cooldown also has something to hit. Walking the player over instead would make this flaky
        // for terrain/physics reasons that have nothing to do with the skill.
        Enemy target = ParkEnemyNextToPlayer(player, "r3");
        // Round 3 spawned a new player, so re-read the stats component the HUD is now bound to.
        stats = GetPlayerStats(player);
        int soulsBeforeSkill = stats.souls;
        yield return null;

        float measured = -1f;
        yield return FireSkillAndMeasureCooldown(pad, panel, player, 1, "r3", d => measured = d);

        Assert.AreEqual(10f, measured, 0.001f,
            $"[r3] Whirlwind's cooldown ran for {measured}s but its authored cooldown is 10s. The HUD's " +
            "countdown is only meaningful if the duration behind it is the skill's own.");

        // Whirlwind is basic damage x2 = 120 against a 100 HP enemy, so a single cast must kill —
        // something the basic attack (60, or 72 upgraded) provably cannot do in one hit. That
        // asymmetry is what proves the press ran the SKILL's damage path, not a plain swing.
        yield return WaitForEnemyDeath(target);
        Assert.IsFalse(target.IsAlive,
            $"[r3] Whirlwind did not kill a full-health ({EnemyMaxHealth} HP) enemy standing 1m away, " +
            $"but it deals {BasicAttackDamage} x2 = {BasicAttackDamage * 2f} damage. Either the AoE " +
            "collider missed or the skill's damage multiplier was not applied.");
        Assert.AreEqual(soulsBeforeSkill + 1, stats.souls,
            "[r3] The Whirlwind kill did not pay a soul — a skill kill must reward like any other.");

        Shot("journey-r3-after-whirlwind-kill");
    }

    /// <summary>
    /// Round 4 — the damage upgrade, checked by the number an enemy actually loses.
    ///
    /// attributes/strength_1 costs 10 and is the cheapest thing in the shop, so it is what a new
    /// player buys first. It advertises "+20% damage": this round proves that claim reaches combat.
    /// A basic hit must go from 60 to 72, and the multiplier must apply on top of a skill's own
    /// multiplier too (Whirlwind 120 -> 144), not only to plain swings.
    /// </summary>
    [UnityTest]
    public IEnumerator Round4_BuyingTheDamageUpgradeRaisesDamageDealt()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();

        // Earn the 10 souls the upgrade costs, then leave and spend them.
        yield return JoinAndStartRun(pad, "r4-earn");
        TopDownPlayerController player = GetPanelPlayer(GetBoundPanel());
        yield return KillEnemies(player, 10, "r4-earn");
        yield return ExitToCharacterSelection(pad, "r4-earn");

        yield return BuyUpgrade(pad, categoryIndex: 0, rowIndex: 0, "attributes", "strength_1", "r4");

        // Next round: the purchase must be live in combat.
        yield return JoinAndStartRun(pad, "r4");
        player = GetPanelPlayer(GetBoundPanel());
        var combat = player.GetComponent<CombatController>();

        const float UpgradedMultiplier = 1.2f; // 1 + (strengthLevel - 1) * 0.2, one rank bought
        Assert.AreEqual(UpgradedMultiplier, combat.GetDamageMultiplier(), 0.001f,
            $"[r4] One rank of strength_1 should set the damage multiplier to {UpgradedMultiplier}x, but " +
            $"CombatController still runs at {combat.GetDamageMultiplier()}x — the purchase never reached " +
            "the spawned player.");

        // The number a player would actually see come off an enemy's health bar.
        float expectedBasic = BasicAttackDamage * UpgradedMultiplier; // 72
        Enemy probe = FirstLivingEnemy("r4");
        float healthBefore = probe.CurrentHealth;
        probe.TakeDamage(BasicAttackDamage * combat.GetDamageMultiplier(), Vector3.forward, 0f);
        yield return null;

        float dealt = healthBefore - probe.CurrentHealth;
        Assert.AreEqual(expectedBasic, dealt, 0.001f,
            $"[r4] After buying +20% damage, one basic attack took {dealt} health instead of {expectedBasic} " +
            $"(base {BasicAttackDamage} x {UpgradedMultiplier}).");
        Assert.Greater(dealt, BasicAttackDamage,
            $"[r4] The damage upgrade was bought but a hit still deals the un-upgraded {BasicAttackDamage}.");
        Shot("journey-r4-upgraded-basic-hit");

        // The character multiplier must compose with a skill's own multiplier, not replace it.
        // CombatController applies it inside PerformAttack, so read it back through the same maths
        // HandleSkills uses for slot 1.
        CharacterStats stats = GetPlayerStats(player);
        float whirlwindMult = stats.GetSkillValue("whirlwind", SkillEffect.Damage, 2f);
        Assert.AreEqual(BasicAttackDamage * 2f * UpgradedMultiplier,
            BasicAttackDamage * whirlwindMult * combat.GetDamageMultiplier(), 0.001f,
            "[r4] The +20% character damage does not stack onto Whirlwind's own x2 — a skill would " +
            "ignore the upgrade the player paid for.");
    }

    /// <summary>
    /// Round 5 — the second and third skills, proving the loop keeps working past the first unlock
    /// and that each slot runs its OWN cooldown rather than a shared hardcoded one.
    ///
    /// Shuriken (slot 2) and Battle Cry (slot 3) cost 20 souls each and behave nothing like
    /// Whirlwind: one spawns flying projectiles on a 2s cooldown, the other is a self speed buff on
    /// 10s. Each is verified in the round after its own purchase — GUI presence, cooldown indicator,
    /// and the effect itself (projectiles exist / move speed actually rises).
    ///
    /// Four rounds, because an unlock costs 20 and a kill pays 1: earn 20, buy Shuriken, play a round
    /// that both uses Shuriken and earns the next 20, buy Battle Cry, play a round that uses it.
    /// </summary>
    [UnityTest]
    public IEnumerator Round5_UnlockShurikenAndBattleCryThenVerifyBothNextRound()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();

        // --- Earn round: 20 kills pays for exactly one 20-soul unlock ---
        yield return JoinAndStartRun(pad, "r5a-earn");
        TopDownPlayerController player = GetPanelPlayer(GetBoundPanel());
        yield return KillEnemies(player, 20, "r5a-earn");
        yield return ExitToCharacterSelection(pad, "r5a-earn");

        yield return BuyUpgrade(pad, categoryIndex: 2, rowIndex: 0, "shuriken", "unlock", "r5a");

        // --- Round after the purchase: Shuriken must be present and working, and this same round
        // earns the 20 souls that pay for Battle Cry. That is the loop as a player runs it: the new
        // skill gets used while grinding for the next one. ---
        yield return JoinAndStartRun(pad, "r5a");
        PlayerHUDPanel panel = GetBoundPanel();
        player = GetPanelPlayer(panel);

        AssertHudSlotState(panel, player, 2, expectUnlocked: true, "r5a");
        // Slots 1 and 3 were not bought, so buying slot 2 must not open them as a side effect.
        AssertHudSlotState(panel, player, 1, expectUnlocked: false, "r5a");
        AssertHudSlotState(panel, player, 3, expectUnlocked: false, "r5a");
        Shot("journey-r5a-shuriken-unlocked");

        // Shuriken: real projectiles, on its own 2s cooldown (not Whirlwind's 10s).
        int projectilesBefore = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;

        float shurikenCooldown = -1f;
        yield return FireSkillAndMeasureCooldown(pad, panel, player, 2, "r5a", d => shurikenCooldown = d);
        Assert.AreEqual(2f, shurikenCooldown, 0.001f,
            $"[r5a] Shuriken ran a {shurikenCooldown}s cooldown but its authored cooldown is 2s. Each slot " +
            "must count its own skill's cooldown, not a shared default.");

        // Blades spawn on a timed coroutine across the throw animation, not on the press frame, and
        // each is destroyed on hit — so poll for a rise rather than sampling one frame.
        int projectilesSeen = projectilesBefore;
        for (int frame = 0; frame < 120 && projectilesSeen <= projectilesBefore; frame++)
        {
            projectilesSeen = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
            yield return null;
        }
        Assert.Greater(projectilesSeen, projectilesBefore,
            "[r5a] Shuriken is unlocked and started its cooldown, but no Projectile ever spawned — the " +
            "skill fires nothing.");
        Shot("journey-r5a-shuriken-projectiles");

        yield return KillEnemies(player, 20, "r5b-earn");
        yield return ExitToCharacterSelection(pad, "r5b-earn");

        yield return BuyUpgrade(pad, categoryIndex: 3, rowIndex: 0, "battlecry", "unlock", "r5b");

        // --- Round after that purchase: Battle Cry, a skill with no attack at all ---
        yield return JoinAndStartRun(pad, "r5b");
        panel = GetBoundPanel();
        player = GetPanelPlayer(panel);

        AssertHudSlotState(panel, player, 3, expectUnlocked: true, "r5b");
        // Shuriken stays unlocked across rounds — a purchase must not expire.
        AssertHudSlotState(panel, player, 2, expectUnlocked: true, "r5b");
        AssertHudSlotState(panel, player, 1, expectUnlocked: false, "r5b");
        Shot("journey-r5b-battlecry-unlocked");

        Assert.AreEqual(1f, PlayerFloat(player, "battleCrySpeedMultiplier"), 0.001f,
            "[r5b] The Battle Cry speed buff is already applied before the skill was ever used.");
        float speedBefore = PlayerFloat(player, "currentSpeed");

        float battleCryCooldown = -1f;
        yield return FireSkillAndMeasureCooldown(pad, panel, player, 3, "r5b", d => battleCryCooldown = d);
        Assert.AreEqual(10f, battleCryCooldown, 0.001f,
            $"[r5b] Battle Cry ran a {battleCryCooldown}s cooldown but its authored cooldown is 10s.");

        // The authored buff is x2 for 5s. FireSkillAndMeasureCooldown returns ~1.2s in, well inside it.
        Assert.AreEqual(2f, PlayerFloat(player, "battleCrySpeedMultiplier"), 0.001f,
            "[r5b] Battle Cry started its cooldown but applied no speed buff — the shout plays with no effect.");
        // The buff has to reach the speed the controller actually moves at, not just sit in a field.
        // Comparing "greater" rather than exactly x2 because currentSpeed also carries the smoothed
        // ground-type multiplier, which drifts frame to frame under the player.
        Assert.Greater(PlayerFloat(player, "currentSpeed"), speedBefore,
            $"[r5b] Battle Cry's x2 buff never reached move speed: it stayed at {speedBefore}.");
        Shot("journey-r5b-battlecry-active");
    }

    /// <summary>
    /// Teleports the nearest living enemy to 1m from the player and stops it dead, so a skill cast on
    /// the next frame is guaranteed to have a target inside its radius.
    /// </summary>
    static Enemy ParkEnemyNextToPlayer(TopDownPlayerController player, string step)
    {
        Enemy target = FirstLivingEnemy(step);

        var body = target.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        target.transform.position = player.transform.position + new Vector3(1f, 0f, 0f);
        return target;
    }

    /// <summary>Waits out the AttackEffect's trigger-overlap hit window (physics reports it a frame or
    /// two after the effect spawns), rather than asserting on the same frame as the press.</summary>
    static IEnumerator WaitForEnemyDeath(Enemy target)
    {
        for (int frame = 0; frame < 60 && target.IsAlive; frame++)
        {
            yield return new WaitForFixedUpdate();
        }
    }

    /// <summary>Reads a private float off the controller (move speed and the Battle Cry multiplier are
    /// internal state with no accessor — exposing them just for a test would widen the API).</summary>
    static float PlayerFloat(TopDownPlayerController player, string fieldName)
    {
        FieldInfo field = typeof(TopDownPlayerController)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"TopDownPlayerController has no field '{fieldName}' — the test is stale.");
        return (float)field.GetValue(player);
    }

    static Enemy FirstLivingEnemy(string step)
    {
        foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsAlive) return enemy;
        }
        Assert.Fail($"[{step}] No living enemy in {LevelScene}.");
        return null;
    }

    // ------------------------------------------------------------------
    // Scene / object access
    // ------------------------------------------------------------------

    static PlayerHUDPanel GetBoundPanel()
    {
        var hud = Object.FindAnyObjectByType<PlayerHUD>();
        Assert.IsNotNull(hud, "PlayerHUDCanvas (PlayerHUD) is missing from the gameplay scene.");

        FieldInfo field = typeof(PlayerHUD).GetField("panels", BindingFlags.NonPublic | BindingFlags.Instance);
        var panels = field?.GetValue(hud) as PlayerHUDPanel[];
        Assert.IsNotNull(panels, "PlayerHUD.panels is not assigned.");
        Assert.Greater(panels.Length, 0, "PlayerHUD.panels is empty.");
        Assert.IsNotNull(panels[0], "PlayerHUD panel 0 is not assigned.");
        return panels[0];
    }

    /// <summary>The controller the HUD is bound to — never FindAnyObjectByType, so a stray second
    /// player in the scene can't make this suite assert about the wrong object.</summary>
    static TopDownPlayerController GetPanelPlayer(PlayerHUDPanel panel)
    {
        FieldInfo field = typeof(PlayerHUDPanel).GetField("boundPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
        var controller = field?.GetValue(panel) as TopDownPlayerController;
        Assert.IsNotNull(controller, "HUD panel 0 has no bound player — PlayerSpawner did not spawn into the level.");
        return controller;
    }

    static T[] PanelArray<T>(PlayerHUDPanel panel, string fieldName)
    {
        FieldInfo field = typeof(PlayerHUDPanel).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"PlayerHUDPanel has no field '{fieldName}'.");
        return field.GetValue(panel) as T[] ?? System.Array.Empty<T>();
    }

    static CharacterDefinition GetRosterCharacter(CharacterSelectionManager manager, int index)
    {
        FieldInfo field = typeof(CharacterSelectionManager).GetField("allCharacters", BindingFlags.NonPublic | BindingFlags.Instance);
        var roster = field?.GetValue(manager) as CharacterDefinition[];
        return roster != null && index < roster.Length ? roster[index] : null;
    }

    static CharacterStats GetPlayerStats(TopDownPlayerController player)
    {
        var stats = player.GetComponent<CharacterStats>();
        Assert.IsNotNull(stats, "The spawned player has no CharacterStats.");
        return stats;
    }

    // ------------------------------------------------------------------
    // Journey steps — each is what a player physically does, in order
    // ------------------------------------------------------------------

    /// <summary>
    /// Character selection as a new player sees it: join with the gamepad, confirm the first
    /// character, press Start. Returns with Level1 active. This is the real screen and the real
    /// input path — not SceneManager.LoadScene("Level1"), because the round-trip through selection
    /// is exactly what the loop under test does between rounds.
    /// </summary>
    IEnumerator JoinAndStartRun(Gamepad pad, string step)
    {
        yield return LoadScene(SelectionScene);
        yield return null;

        var manager = Object.FindAnyObjectByType<CharacterSelectionManager>();
        Assert.IsNotNull(manager, $"[{step}] CharacterSelectionManager missing from {SelectionScene}.");

        yield return Press(pad, GamepadButton.South); // join -> slot 0 Selecting
        yield return null;
        yield return Press(pad, GamepadButton.South); // confirm the highlighted character
        yield return null;
        yield return Press(pad, GamepadButton.Start); // start the run
        yield return null;
        yield return null;

        Assert.AreEqual(LevelScene, SceneManager.GetActiveScene().name,
            $"[{step}] Start did not transition into {LevelScene}.");

        // The HUD binding is the contract every later assertion reads through, so fail here if the
        // spawner never bound it rather than in a confusing place downstream.
        PlayerHUDPanel panel = GetBoundPanel();
        TopDownPlayerController player = GetPanelPlayer(panel);
        Assert.IsNotNull(GetPlayerStats(player).definition,
            $"[{step}] CharacterStats.definition is null on the spawned player — no skill slot can " +
            "resolve its tree, so every lock check is meaningless.");
        yield return null;
    }

    /// <summary>
    /// Kills <paramref name="count"/> enemies the way the game does it — Enemy.TakeDamage with the
    /// player's real resolved basic-attack damage — and returns the souls actually credited.
    ///
    /// Deliberately NOT "walk the player into an enemy and mash attack": enemy approach is physics-
    /// and terrain-dependent and would make a progression test flaky for reasons that have nothing
    /// to do with progression. The damage number applied here is the same number
    /// CombatController would apply (base damage x the player's damage multiplier), and the kill
    /// path (Enemy.Die -> GameSaveController.OnEnemyKilled -> CharacterStats.AddSouls) is the real
    /// one. Combat wiring itself is covered by the separate whirlwind/shuriken assertions below,
    /// which fire the actual skills.
    /// </summary>
    IEnumerator KillEnemies(TopDownPlayerController player, int count, string step)
    {
        CharacterStats stats = GetPlayerStats(player);
        int soulsBefore = stats.souls;

        Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        int alive = 0;
        foreach (Enemy enemy in enemies) if (enemy.IsAlive) alive++;
        Assert.GreaterOrEqual(alive, count,
            $"[{step}] Only {alive} living enemies in {LevelScene} but the round needs to kill {count}. " +
            "Level1Generator must place at least that many.");

        var combat = player.GetComponent<CombatController>();
        Assert.IsNotNull(combat, $"[{step}] The spawned player has no CombatController.");
        float hitDamage = BasicAttackDamage * combat.GetDamageMultiplier();

        int killed = 0;
        foreach (Enemy enemy in enemies)
        {
            if (killed >= count) break;
            if (!enemy.IsAlive) continue;

            // Enough swings to drop it, however much health it has and however hard we hit.
            for (int swing = 0; swing < 20 && enemy.IsAlive; swing++)
            {
                enemy.TakeDamage(hitDamage, Vector3.forward, 0f);
                yield return null;
            }
            Assert.IsFalse(enemy.IsAlive,
                $"[{step}] {enemy.gameObject.name} survived 20 hits of {hitDamage} damage.");
            killed++;
        }

        yield return null;

        int earned = stats.souls - soulsBefore;
        Assert.AreEqual(count, earned,
            $"[{step}] Killed {count} enemies but the player was credited {earned} souls. " +
            "GameSaveController.OnEnemyKilled awards 1 soul per kill to every registered player — " +
            "a mismatch means the player is not registered, or is registered twice.");
    }

    /// <summary>
    /// Leaves the run through the pause menu's own "Character Selection" button — the exit path a
    /// player uses. Start opens the menu; the button click flushes progress to disk and loads the
    /// selection scene.
    /// </summary>
    IEnumerator ExitToCharacterSelection(Gamepad pad, string step)
    {
        Assert.IsNotNull(PauseMenuController.Instance,
            $"[{step}] PauseMenuController.Instance is missing — {LevelScene} has no pause menu, so " +
            "there is no in-game way back to character selection.");

        for (int attempt = 0; attempt < 5 && !PauseMenuController.Instance.IsOpen; attempt++)
        {
            yield return Press(pad, GamepadButton.Start);
            yield return null;
        }
        Assert.IsTrue(PauseMenuController.Instance.IsOpen,
            $"[{step}] Start did not open the pause menu.");

        Shot($"journey-{step}-pause-menu");

        FieldInfo field = typeof(PauseMenuController)
            .GetField("characterSelectionButton", BindingFlags.NonPublic | BindingFlags.Instance);
        var button = field?.GetValue(PauseMenuController.Instance) as Button;
        Assert.IsNotNull(button,
            $"[{step}] PauseMenuController.characterSelectionButton is not assigned — the menu shows " +
            "no way out of the run.");

        button.onClick.Invoke();
        yield return null;
        yield return null;

        Assert.AreEqual(SelectionScene, SceneManager.GetActiveScene().name,
            $"[{step}] The pause menu's Character Selection button did not return to {SelectionScene}.");
        // Time.timeScale is set to 0 while paused; leaving the run must restore it or the next round
        // is frozen and every later WaitForSeconds hangs.
        Assert.AreEqual(1f, Time.timeScale, 0.001f,
            $"[{step}] Time.timeScale is still {Time.timeScale} after leaving the run — the next round " +
            "would be frozen.");
        yield return null;
    }

    /// <summary>
    /// Buys one row in the upgrade screen using only gamepad presses: join, open the panel (Y),
    /// walk the cursor down to <paramref name="categoryIndex"/>, drill in (A), walk down to
    /// <paramref name="rowIndex"/>, buy (A), then close (B, B).
    ///
    /// Returns the save as it stands on disk afterwards. Asserts the purchase actually landed —
    /// rank up AND souls down — because SkillTreeScreenController.BuySelected silently no-ops when
    /// the row is unaffordable or gated, and a silent no-op here would make the "does it work in
    /// game" assertion in the next round fail for the wrong reason.
    /// </summary>
    IEnumerator BuyUpgrade(Gamepad pad, int categoryIndex, int rowIndex, string skillId, string nodeId, string step)
    {
        Assert.AreEqual(SelectionScene, SceneManager.GetActiveScene().name,
            $"[{step}] BuyUpgrade must run in {SelectionScene}.");

        var manager = Object.FindAnyObjectByType<CharacterSelectionManager>();
        Assert.IsNotNull(manager, $"[{step}] CharacterSelectionManager missing.");

        yield return Press(pad, GamepadButton.South); // join -> slot 0 Selecting
        yield return null;

        CharacterDefinition character = GetRosterCharacter(manager, 0);
        Assert.IsNotNull(character, $"[{step}] Roster (allCharacters) is empty in {SelectionScene}.");

        yield return Press(pad, GamepadButton.North); // Upgrade -> open the skill tree panel
        yield return null;
        Assert.IsTrue(manager.IsSlotUpgrading(0),
            $"[{step}] Upgrade (Y) did not open the skill tree panel.");

        Shot($"journey-{step}-upgrade-opened");

        // The panel opens with the cursor in the CATEGORY list.
        for (int i = 0; i < categoryIndex; i++)
        {
            yield return PulseStick(pad, Vector2.down);
        }

        CharacterSaveData before = SaveManager.LoadCharacter(character.characterID);
        int rankBefore = SkillTreeService.GetRank(before, skillId, nodeId);
        int soulsBefore = before.souls;
        int price = SkillTreeService.GetNextCost(before, skillId,
            SkillTreeService.FindNode(SkillTreeService.FindTree(character, skillId), nodeId));
        Assert.GreaterOrEqual(soulsBefore, price,
            $"[{step}] Player has {soulsBefore} souls but {skillId}/{nodeId} costs {price} — the round " +
            "before this did not earn enough to afford the purchase this step is about.");

        yield return Press(pad, GamepadButton.South); // drill into the category's rows
        yield return null;

        for (int i = 0; i < rowIndex; i++)
        {
            yield return PulseStick(pad, Vector2.down);
        }

        Shot($"journey-{step}-row-selected");

        yield return Press(pad, GamepadButton.South); // buy the highlighted row
        yield return null;

        CharacterSaveData after = SaveManager.LoadCharacter(character.characterID);
        Assert.AreEqual(rankBefore + 1, SkillTreeService.GetRank(after, skillId, nodeId),
            $"[{step}] Buying {skillId}/{nodeId} did not advance its rank. Either the cursor was on a " +
            "different row than expected (categoryIndex/rowIndex wrong) or the purchase was refused.");
        Assert.AreEqual(soulsBefore - price, after.souls,
            $"[{step}] {skillId}/{nodeId} cost {price} souls but the balance went {soulsBefore} -> {after.souls}.");

        Shot($"journey-{step}-after-buy");

        yield return Press(pad, GamepadButton.East); // rows -> categories
        yield return null;
        yield return Press(pad, GamepadButton.East); // categories -> close
        yield return null;
        Assert.IsFalse(manager.IsSlotUpgrading(0),
            $"[{step}] Cancel did not close the upgrade panel.");

        yield return null;
    }

    // ------------------------------------------------------------------
    // Verification of a newly unlocked skill, in the round after buying it
    // ------------------------------------------------------------------

    /// <summary>
    /// What the HUD must show for slot <paramref name="slot"/> given its unlock state: the box and
    /// its button prompt are always visible, the lock mark appears only while locked, and an
    /// unlocked slot's icon is drawn at full colour. This is the pairing the reported bug broke —
    /// the HUD claiming locked while the button fired.
    /// </summary>
    void AssertHudSlotState(PlayerHUDPanel panel, TopDownPlayerController player, int slot, bool expectUnlocked, string step)
    {
        Image[] backgrounds = PanelArray<Image>(panel, "skillSlotBackgrounds");
        TMP_Text[] labels = PanelArray<TMP_Text>(panel, "skillSlotLabels");
        TMP_Text[] lockedMarks = PanelArray<TMP_Text>(panel, "skillLockedMarks");
        Image[] icons = PanelArray<Image>(panel, "skillIconImages");

        Assert.AreEqual(SkillSlotCount, backgrounds.Length, $"[{step}] Expected 3 skill slot backgrounds.");

        int index = slot - 1;
        Assert.AreEqual(expectUnlocked, player.IsSkillSlotUnlocked(slot),
            $"[{step}] Slot {slot} unlock state is not {expectUnlocked} — the save and the controller disagree.");

        Assert.IsTrue(backgrounds[index].gameObject.activeInHierarchy,
            $"[{step}] Slot {slot} box is hidden, so the player cannot see the skill exists.");
        Assert.IsTrue(labels[index].gameObject.activeInHierarchy,
            $"[{step}] Slot {slot} has no visible button prompt.");
        Assert.AreEqual("Y", labels[0].text, $"[{step}] Slot 1 prompt should be the gamepad glyph Y.");

        Assert.AreEqual(!expectUnlocked, lockedMarks[index].gameObject.activeInHierarchy,
            expectUnlocked
                ? $"[{step}] Slot {slot} is unlocked but the HUD still draws the lock mark."
                : $"[{step}] Slot {slot} is locked but the HUD shows no lock mark.");

        if (expectUnlocked)
        {
            Assert.IsTrue(icons[index].enabled,
                $"[{step}] Slot {slot} is unlocked but its icon is not drawn — the slot reads as empty.");
            Assert.AreEqual(Color.white, icons[index].color,
                $"[{step}] Slot {slot} icon is still dimmed after unlocking.");
        }
    }

    /// <summary>
    /// Presses the slot's real button and verifies the cooldown indicator does the three things a
    /// player reads it for: fills, shows a whole-second countdown over its backing disc, and
    /// drains. Returns the cooldown duration the game actually used, so the caller can check it
    /// against the skill's authored cooldown (and against a purchased cooldown reduction).
    /// </summary>
    IEnumerator FireSkillAndMeasureCooldown(Gamepad pad, PlayerHUDPanel panel, TopDownPlayerController player,
        int slot, string step, System.Action<float> onDuration)
    {
        GamepadButton[] skillButtons =
        {
            GamepadButton.North,         // Skill1 <Gamepad>/buttonNorth
            GamepadButton.East,          // Skill2 <Gamepad>/buttonEast
            GamepadButton.RightShoulder, // Skill3 <Gamepad>/rightShoulder
        };

        // The skill lock is shared across all three slots, so wait out anything still running.
        while (player.SkillCooldownRemaining > 0f) yield return null;
        yield return null;

        for (int attempt = 0; attempt < 6 && player.SkillCooldownNormalized <= 0f; attempt++)
        {
            yield return Press(pad, skillButtons[slot - 1]);
            yield return null;
        }

        Assert.Greater(player.SkillCooldownNormalized, 0f,
            $"[{step}] Slot {slot} is unlocked and the HUD says so, but pressing its button started no " +
            "cooldown — the skill did not fire.");

        float duration = player.SkillCooldownDuration;
        onDuration?.Invoke(duration);

        yield return null; // let PlayerHUDPanel.Update push the cooldown into the UI

        Image[] overlays = PanelArray<Image>(panel, "skillCooldownOverlays");
        TMP_Text[] cooldownTexts = PanelArray<TMP_Text>(panel, "skillCooldownTexts");
        Image[] discs = PanelArray<Image>(panel, "skillCooldownDiscs");
        int index = slot - 1;

        Assert.AreEqual(Image.Type.Filled, overlays[index].type,
            $"[{step}] Slot {slot} cooldown overlay is not a Filled image, so it cannot show progress.");
        Assert.Greater(overlays[index].fillAmount, 0f,
            $"[{step}] Slot {slot} cooldown overlay stayed empty while the skill is on cooldown.");
        Assert.IsNotEmpty(cooldownTexts[index].text ?? string.Empty,
            $"[{step}] Slot {slot} shows no remaining-seconds text while on cooldown.");
        Assert.IsTrue(discs[index].enabled,
            $"[{step}] Slot {slot} seconds text has no backing disc, so it can be illegible over the icon.");

        Shot($"journey-{step}-skill{slot}-cooldown");

        // A static badge would satisfy everything above. Prove it is a live countdown.
        float fillBefore = overlays[index].fillAmount;
        string secondsBefore = cooldownTexts[index].text;
        yield return new WaitForSeconds(1.2f);

        Assert.Less(overlays[index].fillAmount, fillBefore,
            $"[{step}] Slot {slot} cooldown overlay did not drain over 1.2s — the indicator is static.");
        Assert.AreNotEqual(secondsBefore, cooldownTexts[index].text,
            $"[{step}] Slot {slot} cooldown seconds text did not tick down over 1.2s.");
    }

}
