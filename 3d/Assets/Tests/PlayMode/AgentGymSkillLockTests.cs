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
/// Runs in AgentGym.unity — the generated, text-YAML, single-player gameplay scene.
///
/// The reported bug: the HUD showed all three slots locked (red X) while the player could still
/// use the skills. That is only possible when the object the HUD reads and the object the buttons
/// drive are not the same, or when the input gate and the HUD's lock check derive their answer
/// differently. SampleScene made the first case reachable (a scene-baked AssasinoCappuchino with
/// CharacterStats.definition == null living next to PlayerSpawner's clone: null definition means
/// SkillIdForSlot returns null, so the old gate `skillId != null && !IsUnlocked` fell through and
/// every skill fired for free).
///
/// These tests pin the invariant that actually matters, in a scene where exactly one player
/// exists: the HUD's lock mark and the button's real effect always agree.
/// </summary>
public class AgentGymSkillLockTests : HarnessFixture
{
    const int SkillSlotCount = 3;
    const string GymScene = "AgentGym";

    /// <summary>The gym must contain exactly one player, or every assertion below is ambiguous.</summary>
    [UnityTest]
    public IEnumerator Gym_ContainsExactlyOnePlayerBoundToTheHud()
    {
        IgnoreFailingLogs();

        yield return LoadScene(GymScene);
        yield return null;
        yield return null;

        TopDownPlayerController[] players = Object.FindObjectsByType<TopDownPlayerController>(FindObjectsSortMode.None);
        Assert.AreEqual(1, players.Length,
            "AgentGym must contain exactly one player. More than one means input drives a different " +
            "object than the HUD reads — the exact condition behind 'HUD says locked but my skills work'.");

        PlayerHUDPanel panel = GetBoundPanel();
        TopDownPlayerController bound = GetPanelPlayer(panel);
        Assert.AreSame(players[0], bound, "The HUD is bound to a different player than the one in the scene.");

        CharacterStats stats = bound.GetComponent<CharacterStats>();
        Assert.IsNotNull(stats, "The spawned player has no CharacterStats.");
        Assert.IsNotNull(stats.definition,
            "CharacterStats.definition is null on the spawned player. With no definition no slot can " +
            "resolve a skill tree, so every skill behaves as unlocked while the HUD shows it locked.");

        Shot("gym-single-player");
    }

    /// <summary>
    /// Fresh save: HUD marks all three slots locked. Each button press must then be REFUSED —
    /// no cooldown starts. This is the assertion that would have caught the reported bug.
    /// </summary>
    [UnityTest]
    public IEnumerator LockedSlots_ShowXAndRefuseTheButton()
    {
        IgnoreFailingLogs();

        Keyboard keyboard = AddKeyboard();
        yield return LoadScene(GymScene);
        yield return null;
        yield return null;

        PlayerHUDPanel panel = GetBoundPanel();
        TopDownPlayerController controller = GetPanelPlayer(panel);
        TMP_Text[] lockedMarks = GetArray<TMP_Text>(panel, "skillLockedMarks");
        TMP_Text[] labels = GetArray<TMP_Text>(panel, "skillSlotLabels");

        Key[] skillKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };
        for (int slot = 0; slot < SkillSlotCount; slot++)
        {
            Assert.IsFalse(controller.IsSkillSlotUnlocked(slot + 1),
                $"Slot {slot + 1} reports unlocked on a fresh save — no Unlock node has been bought.");
            Assert.IsTrue(lockedMarks[slot].gameObject.activeInHierarchy,
                $"Slot {slot + 1} is locked but the HUD shows no lock mark.");
            Assert.AreEqual((slot + 1).ToString(), labels[slot].text,
                $"Slot {slot + 1} prompt should be the keyboard key.");
        }

        Shot("gym-all-locked");

        // The claim under test: what the HUD shows and what the button does must agree.
        for (int slot = 0; slot < SkillSlotCount; slot++)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                yield return PressKey(keyboard, skillKeys[slot]);
                yield return null;
            }

            Assert.AreEqual(0f, controller.SkillCooldownNormalized, 0.001f,
                $"Slot {slot + 1} is drawn as LOCKED on the HUD but pressing '{slot + 1}' still fired the skill " +
                "(a cooldown started). The HUD and the input gate disagree.");
        }
    }

    /// <summary>
    /// Same scene, save seeded with all three Unlock nodes: no lock marks, and every button press
    /// DOES start a cooldown the HUD renders. The mirror image of the test above.
    /// </summary>
    [UnityTest]
    public IEnumerator UnlockedSlots_HideXAndAcceptTheButton()
    {
        IgnoreFailingLogs();

        Keyboard keyboard = AddKeyboard();

        // Seed before the scene loads: PlayerSpawner calls CharacterStats.ReloadFromDisk during
        // Start, so a save written afterwards would not be visible to the HUD binding.
        SeedSave("AssCap", "whirlwind/unlock:1", "shuriken/unlock:1", "battlecry/unlock:1");

        yield return LoadScene(GymScene);
        yield return null;
        yield return null;

        PlayerHUDPanel panel = GetBoundPanel();
        TopDownPlayerController controller = GetPanelPlayer(panel);
        TMP_Text[] lockedMarks = GetArray<TMP_Text>(panel, "skillLockedMarks");
        TMP_Text[] cooldownTexts = GetArray<TMP_Text>(panel, "skillCooldownTexts");
        Image[] overlays = GetArray<Image>(panel, "skillCooldownOverlays");
        Image[] cooldownDiscs = GetArray<Image>(panel, "skillCooldownDiscs");

        for (int slot = 0; slot < SkillSlotCount; slot++)
        {
            Assert.IsTrue(controller.IsSkillSlotUnlocked(slot + 1),
                $"Slot {slot + 1} still reports locked after its Unlock node was purchased.");
            Assert.IsFalse(lockedMarks[slot].gameObject.activeInHierarchy,
                $"Slot {slot + 1} is unlocked but the HUD still draws the lock mark — the user's exact complaint.");
        }

        Shot("gym-all-unlocked-ready");

        Key[] skillKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };
        for (int slot = 0; slot < SkillSlotCount; slot++)
        {
            // The cooldown lock is shared across all three skills, so wait it out first.
            while (controller.SkillCooldownRemaining > 0f) yield return null;
            yield return null;

            for (int attempt = 0; attempt < 5 && controller.SkillCooldownNormalized <= 0f; attempt++)
            {
                yield return PressKey(keyboard, skillKeys[slot]);
                yield return null;
            }

            Assert.Greater(controller.SkillCooldownNormalized, 0f,
                $"Slot {slot + 1} is drawn as UNLOCKED but pressing '{slot + 1}' did nothing.");
            yield return null; // let PlayerHUDPanel.Update push it into the UI

            Assert.Greater(overlays[slot].fillAmount, 0f,
                $"Slot {slot + 1} cooldown overlay did not fill.");
            Assert.IsNotEmpty(cooldownTexts[slot].text ?? string.Empty,
                $"Slot {slot + 1} shows no remaining-seconds text while on cooldown.");
            Assert.IsTrue(cooldownDiscs[slot].enabled,
                $"Slot {slot + 1} seconds text has no backing disc.");

            Shot($"gym-skill{slot + 1}-cooldown");
        }
    }

    /// <summary>
    /// Regression guard for the harness itself: the test save must never be the real one. Running
    /// the suite used to delete Application.persistentDataPath/gamesave.json — the player's own
    /// progress — which presented in-game as souls 0 and every skill re-locked.
    /// </summary>
    [Test]
    public void Harness_NeverTouchesTheRealSaveFile()
    {
        string path = SaveManager.GetSavePath();
        Assert.IsNotNull(SaveManager.SaveFilePathOverride,
            "SaveManager.SaveFilePathOverride is not set during tests — the suite would wipe the live save.");
        Assert.IsFalse(path.StartsWith(Application.persistentDataPath),
            $"Test save path '{path}' is inside persistentDataPath — the suite would destroy real progress.");
    }

    static void SeedSave(string characterID, params string[] nodes)
    {
        var seeded = new CharacterSaveData(characterID) { souls = 500 };
        seeded.purchasedSkillNodes.AddRange(nodes);
        SaveManager.SaveCharacter(seeded);
    }

    static PlayerHUDPanel GetBoundPanel()
    {
        var hud = Object.FindAnyObjectByType<PlayerHUD>();
        Assert.IsNotNull(hud, "PlayerHUDCanvas (PlayerHUD) is missing from AgentGym.");

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
        Assert.IsNotNull(controller, "HUD panel 0 has no bound player — PlayerSpawner did not spawn into AgentGym.");
        return controller;
    }

    static T[] GetArray<T>(PlayerHUDPanel panel, string fieldName)
    {
        FieldInfo field = typeof(PlayerHUDPanel).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"PlayerHUDPanel has no field '{fieldName}'.");
        return field.GetValue(panel) as T[] ?? System.Array.Empty<T>();
    }
}
