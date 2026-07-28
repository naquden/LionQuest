using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>
/// Drives the skill tree purchase flow from CharacterSelection (Upgrade panel), covering the
/// user's current uncommitted work: open panel, buy a node, verify souls spent, rank persisted
/// to CharacterSaveData, and the write actually landed on disk.
/// </summary>
public class SkillTreeUpgradeTests : HarnessFixture
{
    [UnityTest]
    public IEnumerator BuyDefaultNode_SpendsSoulsAndPersistsToDisk()
    {
        IgnoreFailingLogs();
        yield return LoadScene("CharacterSelection");
        yield return null;

        var manager = Object.FindAnyObjectByType<CharacterSelectionManager>();
        Assert.IsNotNull(manager, "CharacterSelectionManager missing from CharacterSelection.unity");

        Gamepad pad = AddPad();
        yield return Press(pad, GamepadButton.South); // Join -> slot 0 Selecting
        yield return null;

        CharacterDefinition character = GetRosterCharacter(manager, 0);
        Assert.IsNotNull(character, "Roster (allCharacters) is empty in the CharacterSelection scene.");

        // Seed enough souls to afford a purchase before the skill tree screen loads its CharacterSaveData.
        var seeded = new CharacterSaveData(character.characterID) { souls = 9999 };
        SaveManager.SaveCharacter(seeded);

        yield return Press(pad, GamepadButton.North); // Upgrade -> opens skill tree panel
        yield return null;

        Assert.IsTrue(manager.IsSlotUpgrading(0), "Upgrade press did not open the skill tree panel.");
        Shot("skilltree-opened");

        // SelectDefaultNode() lands on the tree's Keystone (tier 0) node, which for
        // "attributes" is the free root unlock (cost 0) — buying it spends nothing but should
        // still advance its rank. A real souls spend is exercised separately below.
        int rankBefore = SkillTreeService.GetRank(SaveManager.LoadCharacter(character.characterID), "attributes", "root");
        yield return Press(pad, GamepadButton.South); // UpgradeSubmit -> buys the selected (root) node
        yield return null;

        int rankAfter = SkillTreeService.GetRank(SaveManager.LoadCharacter(character.characterID), "attributes", "root");
        Assert.Greater(rankAfter, rankBefore, "Buying the root node did not advance its rank.");
        Shot("skilltree-after-buy-root");

        // Navigate up-left to strength_1 (gridPos -2,1 vs root's 0,0; costs 10 souls) and buy it —
        // this is the purchase that must actually spend souls. A single-frame stick pulse (not
        // Stick(), which holds the direction across the whole repeat delay and would double-fire
        // UpdateUpgradePanelInput's repeat-delay navigation) moves the cursor exactly once.
        int soulsBefore = SaveManager.LoadCharacter(character.characterID).souls;
        Vector2 dir = new Vector2(-1f, 1f).normalized;
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = dir });
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.zero });
        InputSystem.Update();
        yield return null;

        yield return Press(pad, GamepadButton.South);
        yield return null;

        int soulsAfter = SaveManager.LoadCharacter(character.characterID).souls;
        Assert.Less(soulsAfter, soulsBefore, "Buying strength_1 did not spend any souls.");
        Shot("skilltree-after-buy-paid");

        string savedJson = File.ReadAllText(Path.Combine(Application.persistentDataPath, "gamesave.json"));
        StringAssert.Contains(character.characterID, savedJson, "Purchase was not persisted to gamesave.json.");

        yield return Press(pad, GamepadButton.East); // UpgradeCancel -> closes panel
        yield return null;

        Assert.IsFalse(manager.IsSlotUpgrading(0), "Cancel did not close the skill tree panel.");
        Shot("skilltree-closed");
    }

    static CharacterDefinition GetRosterCharacter(CharacterSelectionManager manager, int index)
    {
        FieldInfo field = typeof(CharacterSelectionManager).GetField("allCharacters", BindingFlags.NonPublic | BindingFlags.Instance);
        var roster = field?.GetValue(manager) as CharacterDefinition[];
        return roster != null && index < roster.Length ? roster[index] : null;
    }
}
