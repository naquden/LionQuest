using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Drives CharacterSelection.unity with a real synthetic gamepad end-to-end: join, confirm,
/// start, and verifies PlayerSpawner produced a player in SampleScene. This is the flow that
/// shipped with white/blank preview boxes for players 2-4 — Shot() catches the same regression.
/// </summary>
public class CharacterSelectFlowTests : HarnessFixture
{
    [UnityTest]
    public IEnumerator SinglePlayer_JoinConfirmStart_SpawnsInGameplayScene()
    {
        IgnoreFailingLogs();
        yield return LoadScene("CharacterSelection");
        yield return null;

        Gamepad pad = AddPad();

        // PlayerInputManager.JoinBehavior = JoinPlayersWhenButtonIsPressed, bound to Gamepad/buttonSouth.
        yield return Press(pad, GamepadButton.South);
        yield return null;

        var manager = Object.FindAnyObjectByType<CharacterSelectionManager>();
        Assert.IsNotNull(manager, "CharacterSelectionManager missing from CharacterSelection.unity");
        Shot("select-joined");

        // Confirm the carousel's default (index 0) character. UI/Submit is bound to Gamepad/buttonSouth.
        yield return Press(pad, GamepadButton.South);
        yield return null;

        Shot("select-after-submit");

        // Start the run once at least one slot is Ready.
        yield return Press(pad, GamepadButton.Start);
        yield return null;
        yield return null;

        Scene active = SceneManager.GetActiveScene();
        Assert.AreEqual("Level1", active.name, "Start did not transition into the gameplay scene.");

        var stats = Object.FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        Assert.IsTrue(stats.Length >= 1, "PlayerSpawner did not spawn any CharacterStats-bearing player.");

        Shot("select-gameplay-spawned");
    }

    /// <summary>
    /// Picking the second roster entry (67) must spawn 67, not the default. Guards the whole
    /// mirrored-presence chain at once: the definition is discovered into the roster, the carousel
    /// can move off index 0, and PlayerSpawner resolves the chosen characterID to 67's prefab.
    /// </summary>
    [UnityTest]
    public IEnumerator SelectingSecondCharacter_Spawns67_NotTheDefault()
    {
        IgnoreFailingLogs();
        yield return LoadScene("CharacterSelection");
        yield return null;

        Gamepad pad = AddPad();

        yield return Press(pad, GamepadButton.South); // join
        yield return null;

        var manager = Object.FindAnyObjectByType<CharacterSelectionManager>();
        Assert.IsNotNull(manager, "CharacterSelectionManager missing from CharacterSelection.unity");

        // One step forward through the carousel: roster index 1. The carousel is driven by
        // Navigate's Y axis (down = +1, see CharacterSelectionInputHandler), not X. Ordering is
        // pinned by CharacterSelectionGenerator.SortRoster (default first, then by characterID),
        // so index 1 is 67 rather than whatever GUID order happened to produce.
        yield return PulseStick(pad, Vector2.down);

        Shot("select-67-highlighted");

        yield return Press(pad, GamepadButton.South); // confirm
        yield return null;
        yield return Press(pad, GamepadButton.Start);
        yield return null;
        yield return null;

        Assert.AreEqual("Level1", SceneManager.GetActiveScene().name,
            "Start did not transition into the gameplay scene.");

        var stats = Object.FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        Assert.IsTrue(stats.Length >= 1, "PlayerSpawner did not spawn any player.");
        Assert.IsTrue(stats.Any(s => s.characterID == "67"),
            "Selected 67 but the spawned player's characterID is: " +
            string.Join(", ", stats.Select(s => s.characterID)));

        // The prefab must be playable, not merely present: these are the components whose absence
        // leaves a spawned character standing inert with no input.
        CharacterStats spawned = stats.First(s => s.characterID == "67");
        Assert.IsNotNull(spawned.GetComponent<TopDownPlayerController>(), "67 has no TopDownPlayerController.");
        Assert.IsNotNull(spawned.GetComponent<CombatController>(), "67 has no CombatController.");
        Assert.IsNotNull(spawned.GetComponent<PlayerInput>(), "67 has no PlayerInput.");
        Assert.IsNotNull(spawned.definition, "PlayerSpawner did not assign 67's CharacterDefinition.");

        Shot("select-67-gameplay-spawned");
    }
}
