using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pins the stat/skill split between 67 and AssCap: 67 must have its own base health/speed
/// (CharacterDefinition.baseMaxHealth/baseMoveSpeed), a wider basic-attack hit radius, and a
/// hitscan laser for skill 2 instead of a thrown shuriken. Each test spawns the character it needs
/// through the real CharacterSelection flow — CharacterSelectFlowTests.SelectingSecondCharacter_
/// Spawns67_NotTheDefault already proved the roster/spawn wiring, so this suite is free to build
/// on top of it rather than re-proving it.
/// </summary>
public class Char67IdentityTests : HarnessFixture
{
    const string SelectionScene = "CharacterSelection";
    const string LevelScene = "Level1";

    /// <summary>
    /// 67's health/speed must come from its own CharacterDefinition, not AssCap's — and the two
    /// characters must actually differ, not just both read "some" definition value.
    /// </summary>
    [UnityTest]
    public IEnumerator Char67_HasItsOwnBaseStats_DistinctFromAssCap()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();
        yield return JoinAndStartAsRoster(pad, rosterIndex: 0, step: "stats-asscap"); // AssCap
        TopDownPlayerController assCap = GetPanelPlayer(GetBoundPanel());
        CharacterStats assCapStats = assCap.GetComponent<CharacterStats>();

        Assert.AreEqual(100f, assCap.MaxHealth, 0.01f,
            "[stats] AssCap's max health drifted from its authored baseMaxHealth (100).");
        Assert.AreEqual(100f, assCapStats.definition.baseMaxHealth, 0.01f,
            "[stats] AssCap_Definition.baseMaxHealth is not 100 — CharacterSelectionGenerator must " +
            "author it explicitly.");
        Assert.AreEqual(3f, assCapStats.definition.baseMoveSpeed, 0.01f,
            "[stats] AssCap_Definition.baseMoveSpeed is not 3.");

        yield return JoinAndStartAsRoster(pad, rosterIndex: 1, step: "stats-67"); // 67
        TopDownPlayerController char67 = GetPanelPlayer(GetBoundPanel());
        CharacterStats char67Stats = char67.GetComponent<CharacterStats>();

        Assert.AreEqual("67", char67Stats.characterID,
            "[stats] Roster index 1 did not spawn 67 — SortRoster ordering changed underneath this test.");
        Assert.AreEqual(70f, char67.MaxHealth, 0.01f,
            "[stats] 67's max health is not 70 — CharacterStats.GetMaxHealth is not reading " +
            "CharacterDefinition.baseMaxHealth, or 67_Definition.asset was not regenerated.");
        Assert.AreEqual(70f, char67Stats.definition.baseMaxHealth, 0.01f,
            "[stats] 67_Definition.baseMaxHealth is not 70.");
        Assert.AreEqual(6f, char67Stats.definition.baseMoveSpeed, 0.01f,
            "[stats] 67_Definition.baseMoveSpeed is not 6.");

        float char67MoveSpeed = PrivateFloat(char67, "moveSpeed");
        Assert.AreEqual(6f, char67MoveSpeed, 0.01f,
            "[stats] 67's runtime moveSpeed did not adopt CharacterDefinition.baseMoveSpeed in Awake.");

        Shot("char67-stats-distinct-from-asscap");
    }

    /// <summary>
    /// 67's basic attack must spawn a wider hit sphere than AssCap's — measured directly off the
    /// spawned AttackEffect's SphereCollider world radius, not inferred from whether a parked enemy
    /// happened to take damage. Distance-based inference is fragile here: the effect spawns at
    /// AttackPoint (0.8m ahead of the player) rather than the player's own position, and the
    /// overlap check measures from the collider's AABB extents, not a clean sphere-vs-point test —
    /// both add slop that makes "did it hit" a noisy proxy for "how wide is the radius". Reading the
    /// collider directly is what CombatController.SpawnAttackEffect + AttackEffect.SetRadius
    /// actually produce, so this asserts the real mechanism instead of a downstream side effect of it.
    /// </summary>
    [UnityTest]
    public IEnumerator Char67BasicAttack_HasAWiderHitRadiusThanAssCap()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();

        yield return JoinAndStartAsRoster(pad, rosterIndex: 0, step: "reach-asscap");
        yield return FireBasicAttack(pad);
        float assCapRadius = -1f;
        yield return WaitForEffectRadius(r => assCapRadius = r);
        Assert.GreaterOrEqual(assCapRadius, 0f, "[reach] AssCap's basic attack spawned no AttackEffect.");

        yield return JoinAndStartAsRoster(pad, rosterIndex: 1, step: "reach-67");
        yield return FireBasicAttack(pad);
        float char67Radius = -1f;
        yield return WaitForEffectRadius(r => char67Radius = r);
        Assert.GreaterOrEqual(char67Radius, 0f, "[reach] 67's basic attack spawned no AttackEffect.");

        Assert.AreEqual(0.714f, assCapRadius, 0.05f,
            $"[reach] AssCap's basic-attack hit radius was {assCapRadius}m, expected ~0.714m " +
            "(unchanged authored SphereCollider). If this fails, the 67 comparison below proves nothing.");
        Assert.AreEqual(1.3f, char67Radius, 0.05f,
            $"[reach] 67's basic-attack hit radius was {char67Radius}m, expected 1.3m — AttackData." +
            "hitRadius is not reaching CombatController.SpawnAttackEffect / AttackEffect.SetRadius.");
        Assert.Greater(char67Radius, assCapRadius,
            "[reach] 67's basic attack should have a wider hit radius than AssCap's.");
    }

    /// <summary>
    /// 67's skill 2 must be the hitscan laser, not the shuriken: it damages every enemy in a line up
    /// to its pierceCount, an enemy beyond that count is untouched (proving pierce is real and not
    /// "hits everything"), the HUD renders its own 3s cooldown, and the beam actually renders on
    /// screen for the screenshot to catch — a green assertion alone would also pass for a beam that
    /// never draws.
    /// </summary>
    [UnityTest]
    public IEnumerator Char67Laser_DamagesEnemiesInLineUpToPierceCountAndRendersOnScreen()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();
        SeedSave("67", "laser/unlock:1");

        yield return JoinAndStartAsRoster(pad, rosterIndex: 1, step: "laser");
        PlayerHUDPanel panel = GetBoundPanel();
        TopDownPlayerController player = GetPanelPlayer(panel);

        Assert.IsTrue(player.IsSkillSlotUnlocked(2),
            "[laser] Seeded 'laser/unlock:1' but slot 2 still reports locked — 67's skillId for slot " +
            "2 is not 'laser', or BuildLaserTree was not authored onto 67_Definition.");

        // Two enemies in a line: the first inside pierceCount (1), the second beyond it. Facing +z
        // (transform.forward default heading after spawn-snap) so both sit on the beam's ray.
        Vector3 forward = player.transform.forward;
        Enemy firstEnemy = ParkEnemyAt(player, forward * 3f, "laser-first");
        Enemy secondEnemy = ParkEnemyAt(player, forward * 6f, "laser-second");
        float firstHealthBefore = firstEnemy.CurrentHealth;
        float secondHealthBefore = secondEnemy.CurrentHealth;

        for (int attempt = 0; attempt < 6 && player.SkillCooldownNormalized <= 0f; attempt++)
        {
            yield return Press(pad, GamepadButton.East); // Skill2
            yield return null;
        }
        Assert.Greater(player.SkillCooldownNormalized, 0f,
            "[laser] Slot 2 is unlocked but pressing East started no cooldown — the skill did not fire.");
        Assert.AreEqual(3f, player.SkillCooldownDuration, 0.01f,
            $"[laser] Laser cooldown was {player.SkillCooldownDuration}s but LaserBeam.asset authors 3s.");

        // The beam fires after LaserBeamSkillData.fireDelay (0.35s), not on the press frame. Poll by
        // elapsed Time.time rather than a fixed frame count — batchmode frame steps are not a fixed
        // real-time interval, so a frame cap sized for one test's per-frame cost can under-cover the
        // delay in another.
        LaserBeam beam = null;
        float deadline = Time.time + 3f;
        while (beam == null && Time.time < deadline)
        {
            beam = Object.FindAnyObjectByType<LaserBeam>();
            yield return null;
        }
        Assert.IsNotNull(beam,
            "[laser] No LaserBeam instance ever appeared after firing — FireLaserAfterDelay did not " +
            "instantiate the beam prefab.");
        Shot("char67-laser-beam-visible");

        yield return WaitAFewFixedFrames();

        Assert.Less(firstEnemy.CurrentHealth, firstHealthBefore,
            "[laser] The enemy 3m in front (within pierceCount 1) took no damage from the beam.");
        Assert.AreEqual(secondHealthBefore, secondEnemy.CurrentHealth, 0.01f,
            "[laser] The enemy 6m in front (beyond pierceCount 1) took damage — pierce is not being " +
            "capped, so the beam hits everything on the line regardless of pierceCount.");
    }

    // ------------------------------------------------------------------
    // Journey steps
    // ------------------------------------------------------------------

    /// <summary>Joins, steps the carousel to rosterIndex, confirms, and starts — same shape as
    /// CharacterSelectFlowTests.SelectingSecondCharacter_Spawns67_NotTheDefault, generalized to any
    /// roster slot so both AssCap (0) and 67 (1) can be spawned from the same helper.</summary>
    IEnumerator JoinAndStartAsRoster(Gamepad pad, int rosterIndex, string step)
    {
        yield return LoadScene(SelectionScene);
        yield return null;

        var manager = Object.FindAnyObjectByType<CharacterSelectionManager>();
        Assert.IsNotNull(manager, $"[{step}] CharacterSelectionManager missing from {SelectionScene}.");

        yield return Press(pad, GamepadButton.South); // join -> slot 0 Selecting
        yield return null;

        for (int i = 0; i < rosterIndex; i++)
        {
            yield return PulseStick(pad, Vector2.down);
        }

        yield return Press(pad, GamepadButton.South); // confirm the highlighted character
        yield return null;
        yield return Press(pad, GamepadButton.Start); // start the run
        yield return null;
        yield return null;

        Assert.AreEqual(LevelScene, SceneManager.GetActiveScene().name,
            $"[{step}] Start did not transition into {LevelScene}.");

        PlayerHUDPanel panel = GetBoundPanel();
        TopDownPlayerController player = GetPanelPlayer(panel);
        Assert.IsNotNull(player.GetComponent<CharacterStats>().definition,
            $"[{step}] CharacterStats.definition is null on the spawned player.");
    }

    IEnumerator FireBasicAttack(Gamepad pad)
    {
        yield return Press(pad, GamepadButton.South); // Attack <Gamepad>/buttonSouth
        yield return null;
    }

    IEnumerator WaitAFewFixedFrames()
    {
        for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
    }

    /// <summary>Polls a few frames for the AttackEffect the basic attack just spawned and reports its
    /// SphereCollider's world-space radius (bounds.extents.x, matching how AttackEffect itself
    /// measures the overlap-check radius at :282). Invokes onRadius with -1 if none appears.</summary>
    static IEnumerator WaitForEffectRadius(System.Action<float> onRadius)
    {
        AttackEffect effect = null;
        for (int frame = 0; frame < 10 && effect == null; frame++)
        {
            effect = Object.FindAnyObjectByType<AttackEffect>();
            yield return null;
        }

        if (effect == null)
        {
            onRadius(-1f);
            yield break;
        }

        SphereCollider sphere = effect.GetComponent<SphereCollider>();
        onRadius(sphere != null ? sphere.bounds.extents.x : -1f);
    }

    static void SeedSave(string characterID, params string[] nodes)
    {
        var seeded = new CharacterSaveData(characterID) { souls = 500 };
        seeded.purchasedSkillNodes.AddRange(nodes);
        SaveManager.SaveCharacter(seeded);
    }

    /// <summary>Teleports the nearest unused living enemy to a world offset from the player and
    /// stops it dead, mirroring NewPlayerJourneyTests.ParkEnemyNextToPlayer but with a caller-chosen
    /// offset and per-test bookkeeping (parkedEnemies) so repeated calls don't reuse the same enemy.</summary>
    static Enemy ParkEnemyAt(TopDownPlayerController player, Vector3 offsetFromPlayer, string step)
    {
        Enemy target = FirstUnparkedLivingEnemy(step);

        var body = target.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        target.transform.position = player.transform.position + offsetFromPlayer;
        parkedEnemies.Add(target);
        return target;
    }

    static readonly System.Collections.Generic.HashSet<Enemy> parkedEnemies = new System.Collections.Generic.HashSet<Enemy>();

    static Enemy FirstUnparkedLivingEnemy(string step)
    {
        foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsAlive && !parkedEnemies.Contains(enemy)) return enemy;
        }
        Assert.Fail($"[{step}] No unused living enemy in {LevelScene}.");
        return null;
    }

    // ------------------------------------------------------------------
    // Scene / object access — same reflection pattern as NewPlayerJourneyTests/AgentGymSkillLockTests
    // ------------------------------------------------------------------

    static PlayerHUDPanel GetBoundPanel()
    {
        var hud = Object.FindAnyObjectByType<PlayerHUD>();
        Assert.IsNotNull(hud, "PlayerHUDCanvas (PlayerHUD) is missing from the gameplay scene.");

        FieldInfo field = typeof(PlayerHUD).GetField("panels", BindingFlags.NonPublic | BindingFlags.Instance);
        var panels = field?.GetValue(hud) as PlayerHUDPanel[];
        Assert.IsNotNull(panels, "PlayerHUD.panels is not assigned.");
        Assert.Greater(panels.Length, 0, "PlayerHUD.panels is empty.");
        return panels[0];
    }

    static TopDownPlayerController GetPanelPlayer(PlayerHUDPanel panel)
    {
        FieldInfo field = typeof(PlayerHUDPanel).GetField("boundPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
        var controller = field?.GetValue(panel) as TopDownPlayerController;
        Assert.IsNotNull(controller, "HUD panel 0 has no bound player.");
        return controller;
    }

    static float PrivateFloat(TopDownPlayerController player, string fieldName)
    {
        FieldInfo field = typeof(TopDownPlayerController).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"TopDownPlayerController has no field '{fieldName}' — the test is stale.");
        return (float)field.GetValue(player);
    }
}
