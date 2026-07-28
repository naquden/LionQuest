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
        Assert.AreEqual("SampleScene", active.name, "Start did not transition into the gameplay scene.");

        var stats = Object.FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        Assert.IsTrue(stats.Length >= 1, "PlayerSpawner did not spawn any CharacterStats-bearing player.");

        Shot("select-gameplay-spawned");
    }
}
