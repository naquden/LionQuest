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
/// Verifies the in-game HUD actually shows what the player needs to use skills:
/// a visible slot box per skill, a keybind prompt matching the real binding, and a
/// cooldown indicator that fills after a skill fires. The reported bug is that a
/// freshly-joined player's panel shows only name/health/souls — no slot boxes at all —
/// because every slot was hidden until its skill tree's Unlock node was bought.
/// </summary>
public class PlayerHudSkillSlotTests : HarnessFixture
{
    const int SkillSlotCount = 3;

    /// <summary>Fresh save: all 3 skills locked. Boxes, prompts and lock marks must still be visible.</summary>
    [UnityTest]
    public IEnumerator FreshPlayer_ShowsAllSkillSlotBoxesWithGamepadPrompts()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();
        yield return JoinAndStart(pad, unlockWhirlwind: false);

        PlayerHUDPanel panel = GetBoundPanel();
        yield return null;
        Shot("hud-fresh-player");

        // --- Claim 1: every skill slot box is visible even while the skill is locked ---
        Image[] backgrounds = GetArray<Image>(panel, "skillSlotBackgrounds");
        Assert.AreEqual(SkillSlotCount, backgrounds.Length, "Expected 3 skill slot backgrounds.");
        for (int i = 0; i < backgrounds.Length; i++)
        {
            Assert.IsNotNull(backgrounds[i], $"Slot {i + 1} background reference is null.");
            Assert.IsTrue(backgrounds[i].gameObject.activeInHierarchy,
                $"Skill slot {i + 1} box is hidden on the HUD — the player has no visible skill box to read.");
        }

        // --- Claim 2: each slot carries a prompt matching its real gamepad binding ---
        // Player action map binds Skill1/2/3 to buttonNorth / buttonEast / rightShoulder.
        TMP_Text[] labels = GetArray<TMP_Text>(panel, "skillSlotLabels");
        Assert.AreEqual(SkillSlotCount, labels.Length, "Expected 3 skill slot labels.");
        string[] expectedPrompts = { "Y", "B", "RB" };
        for (int i = 0; i < labels.Length; i++)
        {
            Assert.IsNotNull(labels[i], $"Slot {i + 1} label reference is null.");
            Assert.IsTrue(labels[i].gameObject.activeInHierarchy,
                $"Skill slot {i + 1} keybind label is not visible.");
            Assert.AreEqual(expectedPrompts[i], labels[i].text,
                $"Skill slot {i + 1} prompt does not match its actual gamepad binding.");
        }

        // --- Claim 3: a locked slot is marked as locked rather than silently missing ---
        TMP_Text[] lockedMarks = GetArray<TMP_Text>(panel, "skillLockedMarks");
        Assert.AreEqual(SkillSlotCount, lockedMarks.Length, "Expected 3 locked marks.");
        for (int i = 0; i < lockedMarks.Length; i++)
        {
            Assert.IsNotNull(lockedMarks[i], $"Slot {i + 1} locked mark reference is null.");
            Assert.IsTrue(lockedMarks[i].gameObject.activeInHierarchy,
                $"Skill slot {i + 1} is locked but shows no locked indicator.");
            // A glyph the font can't render would draw nothing while still being "active".
            Assert.IsNotEmpty(lockedMarks[i].text ?? string.Empty,
                $"Skill slot {i + 1} locked indicator has no text.");
            Assert.IsTrue(lockedMarks[i].font.HasCharacter(lockedMarks[i].text[0]),
                $"Skill slot {i + 1} locked indicator uses a glyph missing from the HUD font — it renders blank.");
        }
    }

    /// <summary>Whirlwind unlocked: pressing its button must start a cooldown the HUD displays.</summary>
    [UnityTest]
    public IEnumerator UnlockedSkill_CooldownIndicatorFillsAndCountsDown()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();
        yield return JoinAndStart(pad, unlockWhirlwind: true);

        PlayerHUDPanel panel = GetBoundPanel();
        TopDownPlayerController controller = GetPanelPlayer(panel);
        Assert.IsTrue(controller.IsSkillSlotUnlocked(1),
            "Seeded save did not unlock Whirlwind — cooldown cannot be exercised.");

        Image[] overlays = GetArray<Image>(panel, "skillCooldownOverlays");
        TMP_Text[] cooldownTexts = GetArray<TMP_Text>(panel, "skillCooldownTexts");
        Assert.AreEqual(SkillSlotCount, overlays.Length, "Expected 3 cooldown overlays.");
        Assert.AreEqual(SkillSlotCount, cooldownTexts.Length, "Expected 3 cooldown texts.");
        foreach (Image overlay in overlays)
        {
            Assert.IsNotNull(overlay, "A cooldown overlay reference is null.");
            Assert.AreEqual(Image.Type.Filled, overlay.type,
                "Cooldown overlay must be a Filled image to show progress.");
        }

        Assert.AreEqual(0f, controller.SkillCooldownNormalized, 0.001f,
            "Skill cooldown should start ready (0) before any skill is used.");
        Assert.IsEmpty(cooldownTexts[0].text ?? string.Empty,
            "Cooldown text should be blank while the skill is ready.");

        Shot("hud-skill-ready");

        // Skill1 (Whirlwind) is bound to <Gamepad>/buttonNorth. A single Press can land on a frame
        // where the controller's Update has already run (WasPressedThisFrame is frame-scoped), so
        // retry a few times rather than asserting on one frame's alignment.
        for (int attempt = 0; attempt < 5 && controller.SkillCooldownNormalized <= 0f; attempt++)
        {
            yield return Press(pad, GamepadButton.North);
            yield return null;
        }

        Assert.Greater(controller.SkillCooldownNormalized, 0f,
            "Using Skill 1 did not start a cooldown — nothing for the HUD to display.");
        yield return null; // let PlayerHUDPanel.Update push the new cooldown into the UI

        for (int i = 0; i < overlays.Length; i++)
        {
            if (!controller.IsSkillSlotUnlocked(i + 1)) continue;
            Assert.IsTrue(overlays[i].gameObject.activeInHierarchy,
                $"Slot {i + 1} cooldown overlay is not active, so no cooldown can be seen.");
            Assert.Greater(overlays[i].fillAmount, 0f,
                $"Slot {i + 1} cooldown overlay fillAmount stayed 0 while the skill is on cooldown.");
            Assert.IsNotEmpty(cooldownTexts[i].text ?? string.Empty,
                $"Slot {i + 1} shows no remaining-seconds text while on cooldown.");
        }

        Shot("hud-cooldown-active");

        // The indicator must drain over time, not just latch on.
        float fillBefore = overlays[0].fillAmount;
        yield return new WaitForSeconds(1.5f);
        Assert.Less(overlays[0].fillAmount, fillBefore,
            "Cooldown overlay did not drain over 1.5s — the indicator is static.");

        Shot("hud-cooldown-draining");
    }

    /// <summary>
    /// Keyboard path — the configuration in the reported screenshot (prompts read 1/2/3).
    /// All three skills unlocked, then each skill key is pressed in turn: every press must show a
    /// countdown on its own slot, and the shared lock must show on the other unlocked slots too.
    /// </summary>
    [UnityTest]
    public IEnumerator KeyboardPlayer_EachSkillKeyShowsCooldownOnItsSlot()
    {
        IgnoreFailingLogs();

        Keyboard keyboard = AddKeyboard();
        yield return JoinAndStartKeyboard(keyboard,
            "whirlwind/unlock:1", "shuriken/unlock:1", "battlecry/unlock:1");

        PlayerHUDPanel panel = GetBoundPanel();
        TopDownPlayerController controller = GetPanelPlayer(panel);

        TMP_Text[] labels = GetArray<TMP_Text>(panel, "skillSlotLabels");
        TMP_Text[] lockedMarks = GetArray<TMP_Text>(panel, "skillLockedMarks");
        Image[] overlays = GetArray<Image>(panel, "skillCooldownOverlays");
        TMP_Text[] cooldownTexts = GetArray<TMP_Text>(panel, "skillCooldownTexts");
        Image[] cooldownDiscs = GetArray<Image>(panel, "skillCooldownDiscs");

        for (int i = 0; i < SkillSlotCount; i++)
        {
            Assert.IsFalse(cooldownDiscs[i].enabled,
                $"Slot {i + 1} shows the seconds backing disc while the skill is ready.");
            Assert.IsTrue(controller.IsSkillSlotUnlocked(i + 1),
                $"Seeded save did not unlock skill slot {i + 1}.");
            Assert.IsFalse(lockedMarks[i].gameObject.activeInHierarchy,
                $"Slot {i + 1} still shows the locked mark after its Unlock node was bought.");
            Assert.AreEqual((i + 1).ToString(), labels[i].text,
                $"Slot {i + 1} prompt should be the keyboard key for this control scheme.");
        }

        yield return null;
        Shot("hud-keyboard-all-unlocked-ready");

        Key[] skillKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };
        for (int slot = 0; slot < SkillSlotCount; slot++)
        {
            // Wait out the previous skill's shared lock so this press isn't swallowed by it.
            while (controller.SkillCooldownRemaining > 0f) yield return null;
            yield return null;

            for (int attempt = 0; attempt < 5 && controller.SkillCooldownNormalized <= 0f; attempt++)
            {
                yield return PressKey(keyboard, skillKeys[slot]);
                yield return null;
            }

            Assert.Greater(controller.SkillCooldownNormalized, 0f,
                $"Pressing '{slot + 1}' did not start a cooldown for skill {slot + 1}.");
            yield return null; // let PlayerHUDPanel.Update push it into the UI

            Assert.Greater(overlays[slot].fillAmount, 0f,
                $"Slot {slot + 1} cooldown overlay did not fill after its own key was pressed.");
            Assert.IsNotEmpty(cooldownTexts[slot].text ?? string.Empty,
                $"Slot {slot + 1} shows no remaining-seconds text after its own key was pressed.");
            // Without the disc the light digits vanish over a pale icon.
            Assert.IsTrue(cooldownDiscs[slot].enabled,
                $"Slot {slot + 1} seconds text has no backing disc, so it can be illegible over the icon.");

            Shot($"hud-keyboard-skill{slot + 1}-cooldown");

            // Prove the number is a live countdown, not a static badge: it must have ticked down
            // by the time the next shot is taken.
            if (slot == 0)
            {
                string secondsBefore = cooldownTexts[0].text;
                float fillBefore = overlays[0].fillAmount;
                yield return new WaitForSeconds(3f);
                Assert.Less(overlays[0].fillAmount, fillBefore,
                    "Keyboard cooldown overlay did not drain over 3s — the indicator is static.");
                Assert.AreNotEqual(secondsBefore, cooldownTexts[0].text,
                    "Keyboard cooldown seconds text did not change over 3s.");
                Shot("hud-keyboard-skill1-cooldown-draining");
            }
        }
    }

    /// <summary>
    /// Joins a gamepad in CharacterSelection, optionally seeds the roster character's save with
    /// the Whirlwind unlock node, confirms and starts the run. PlayerSpawner calls
    /// CharacterStats.ReloadFromDisk before binding the HUD, so the seed is visible to the panel.
    /// </summary>
    IEnumerator JoinAndStart(Gamepad pad, bool unlockWhirlwind)
    {
        yield return LoadScene("CharacterSelection");
        yield return null;

        var manager = Object.FindAnyObjectByType<CharacterSelectionManager>();
        Assert.IsNotNull(manager, "CharacterSelectionManager missing from CharacterSelection.unity");

        yield return Press(pad, GamepadButton.South); // join
        yield return null;

        if (unlockWhirlwind) SeedSave(manager, "whirlwind/unlock:1");

        yield return Press(pad, GamepadButton.South); // confirm character
        yield return null;
        yield return Press(pad, GamepadButton.Start); // start run
        yield return null;
        yield return null;

        Assert.AreEqual("Level1", SceneManager.GetActiveScene().name,
            "Start did not transition into the gameplay scene.");
    }

    /// <summary>
    /// Same flow on a keyboard. Joining is done through PlayerInputManager.JoinPlayer with an
    /// explicit device instead of a synthetic Enter: PlayerInputManager's join-on-button-press path
    /// never fires for a device added by a test (the join action's device filtering ignores it), so
    /// a keyboard player could never be spawned that way. Pairing explicitly is what matters here —
    /// the point of the test is the HUD's keyboard prompts and cooldown, not the join listener.
    /// UI/Submit and UI/Start are both bound to &lt;Keyboard&gt;/enter, so a single Enter can confirm
    /// AND start in the same frame; press until the scene actually changes rather than assuming.
    /// </summary>
    IEnumerator JoinAndStartKeyboard(Keyboard keyboard, params string[] unlockNodes)
    {
        yield return LoadScene("CharacterSelection");
        yield return null;

        var manager = Object.FindAnyObjectByType<CharacterSelectionManager>();
        Assert.IsNotNull(manager, "CharacterSelectionManager missing from CharacterSelection.unity");

        PlayerInputManager joinManager = PlayerInputManager.instance;
        Assert.IsNotNull(joinManager, "PlayerInputManager missing from CharacterSelection.unity");
        joinManager.JoinPlayer(pairWithDevice: keyboard);
        // Join-on-button-press also listens on <Keyboard>/enter, so the confirm press below would
        // spawn a SECOND player that re-pairs this keyboard away from slot 0 — slot 0's actions then
        // go Disabled and Submit/Start never fire. One keyboard player is all this test needs.
        joinManager.DisableJoining();
        yield return null;
        yield return null;
        Assert.AreEqual(SlotPhase.Selecting, GetSlotPhase(manager, 0),
            "Keyboard player did not take a selection slot.");

        if (unlockNodes != null && unlockNodes.Length > 0) SeedSave(manager, unlockNodes);

        for (int attempt = 0; attempt < 4 && SceneManager.GetActiveScene().name != "Level1"; attempt++)
        {
            yield return PressKey(keyboard, Key.Enter); // confirm, then start
            yield return null;
        }
        yield return null;

        Assert.AreEqual("Level1", SceneManager.GetActiveScene().name,
            "Enter did not confirm + start the run from the keyboard player's slot.");
    }

    /// <summary>Reads a slot's phase out of the manager's private states[] so a failure names the step.</summary>
    static SlotPhase GetSlotPhase(CharacterSelectionManager manager, int slotIndex)
    {
        FieldInfo field = typeof(CharacterSelectionManager).GetField("states", BindingFlags.NonPublic | BindingFlags.Instance);
        var states = field?.GetValue(manager) as PlayerSlotState[];
        Assert.IsNotNull(states, "CharacterSelectionManager.states is not initialised.");
        return states[slotIndex].phase;
    }

    static void SeedSave(CharacterSelectionManager manager, params string[] nodes)
    {
        CharacterDefinition character = GetRosterCharacter(manager, 0);
        Assert.IsNotNull(character, "Roster (allCharacters) is empty in the CharacterSelection scene.");
        var seeded = new CharacterSaveData(character.characterID) { souls = 500 };
        seeded.purchasedSkillNodes.AddRange(nodes);
        SaveManager.SaveCharacter(seeded);
    }

    static PlayerHUDPanel GetBoundPanel()
    {
        var hud = Object.FindAnyObjectByType<PlayerHUD>();
        Assert.IsNotNull(hud, "PlayerHUDCanvas (PlayerHUD) is missing from Level1.");

        FieldInfo field = typeof(PlayerHUD).GetField("panels", BindingFlags.NonPublic | BindingFlags.Instance);
        var panels = field?.GetValue(hud) as PlayerHUDPanel[];
        Assert.IsNotNull(panels, "PlayerHUD.panels is not assigned.");
        Assert.Greater(panels.Length, 0, "PlayerHUD.panels is empty.");
        Assert.IsNotNull(panels[0], "PlayerHUD panel 0 is not assigned.");
        return panels[0];
    }

    /// <summary>
    /// The controller the HUD panel is actually bound to. Deliberately NOT FindAnyObjectByType:
    /// Level1 has only one player today, but the bug this suite guards was SampleScene holding a
    /// scene-baked AssasinoCappuchino next to PlayerSpawner's clone, where which one Find returns
    /// is not deterministic — asserting on the wrong one reads a cooldown of 0 while the real
    /// player is mid-cooldown. Reading the HUD's own binding cannot drift that way.
    /// </summary>
    static TopDownPlayerController GetPanelPlayer(PlayerHUDPanel panel)
    {
        FieldInfo field = typeof(PlayerHUDPanel).GetField("boundPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
        var controller = field?.GetValue(panel) as TopDownPlayerController;
        Assert.IsNotNull(controller, "HUD panel 0 has no bound player — PlayerSpawner did not spawn into Level1.");
        return controller;
    }

    static CharacterDefinition GetRosterCharacter(CharacterSelectionManager manager, int index)
    {
        FieldInfo field = typeof(CharacterSelectionManager).GetField("allCharacters", BindingFlags.NonPublic | BindingFlags.Instance);
        var roster = field?.GetValue(manager) as CharacterDefinition[];
        return roster != null && index < roster.Length ? roster[index] : null;
    }

    static T[] GetArray<T>(PlayerHUDPanel panel, string fieldName)
    {
        FieldInfo field = typeof(PlayerHUDPanel).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"PlayerHUDPanel has no field '{fieldName}'.");
        return field.GetValue(panel) as T[] ?? System.Array.Empty<T>();
    }
}
