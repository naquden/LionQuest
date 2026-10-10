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
        // Routed through the shared record (see LoadCharacterSave) rather than SaveManager.SaveCharacter
        // directly: CharacterSelection now has a GameController (AudioSystemGenerator wires one in for
        // music), so joining above already cached this characterID's record — a direct disk write at
        // this point would be invisible to it, same staleness trap CharacterStats.Record exists to avoid
        // in production.
        LoadCharacterSave(character.characterID).souls = 9999;
        FlushCharacterSave(character.characterID);

        yield return Press(pad, GamepadButton.North); // Upgrade -> opens skill tree panel
        yield return null;

        Assert.IsTrue(manager.IsSlotUpgrading(0), "Upgrade press did not open the skill tree panel.");
        Shot("skilltree-opened");

        // Panel opens with the cursor in the CATEGORY list, so the first Submit only drills into
        // "Body" — it must not buy anything.
        int soulsOnOpen = LoadCharacterSave(character.characterID).souls;
        yield return Press(pad, GamepadButton.South); // UpgradeSubmit -> focus the Body rows
        yield return null;

        Assert.AreEqual(soulsOnOpen, LoadCharacterSave(character.characterID).souls,
            "Submit on the category list spent souls — it should only move focus into the rows.");
        Shot("skilltree-rows-focused");

        // Cursor now sits on the first Body row, a real priced upgrade, so this Submit must both
        // advance its rank AND spend souls.
        CharacterSaveData before = LoadCharacterSave(character.characterID);
        int rankBefore = SkillTreeService.GetRank(before, "attributes", "strength_1");
        int soulsBefore = before.souls;

        yield return Press(pad, GamepadButton.South); // UpgradeSubmit -> buys the highlighted row
        yield return null;

        CharacterSaveData after = LoadCharacterSave(character.characterID);
        Assert.Greater(SkillTreeService.GetRank(after, "attributes", "strength_1"), rankBefore,
            "Buying the first Body row did not advance its rank.");
        Assert.Less(after.souls, soulsBefore, "Buying the first Body row did not spend any souls.");
        Shot("skilltree-after-buy-body-row");

        // Down moves to the next row inside the focused pane. A single-frame stick pulse (not a held
        // direction, which would double-fire the repeat-delay navigation) moves the cursor once.
        yield return PulseStick(pad, Vector2.down);
        yield return Press(pad, GamepadButton.South);
        yield return null;

        Assert.Greater(SkillTreeService.GetRank(LoadCharacterSave(character.characterID), "attributes", "health_1"), 0,
            "Down + Submit did not buy the second Body row — row navigation is broken.");
        Shot("skilltree-after-buy-second-row");

        // Left moves focus back OUT to the category list (it must not change category), then Down
        // picks Whirlwind and Submit drills into its rows.
        yield return PulseStick(pad, Vector2.left);
        Shot("skilltree-categories-refocused");

        yield return PulseStick(pad, Vector2.down);
        yield return Press(pad, GamepadButton.South); // drill into Whirlwind's rows
        yield return null;

        // First Whirlwind row is the Unlock gate; buying it must flip IsSkillUnlocked.
        yield return Press(pad, GamepadButton.South);
        yield return null;

        CharacterSaveData afterUnlock = LoadCharacterSave(character.characterID);
        Assert.IsTrue(SkillTreeService.IsSkillUnlocked(character, afterUnlock, "whirlwind"),
            "Buying the Whirlwind Unlock row did not unlock the skill.");
        Shot("skilltree-after-unlock-whirlwind");

        // SaveManager.GetSavePath(), not persistentDataPath — the harness redirects the save file
        // into Temp/ so the suite can't wipe the player's real progress.
        string savedJson = File.ReadAllText(SaveManager.GetSavePath());
        StringAssert.Contains(character.characterID, savedJson, "Purchase was not persisted to gamesave.json.");

        // Cancel is the inverse of Submit: from the row list it steps back to the categories, and
        // only a second press closes the screen.
        yield return Press(pad, GamepadButton.East);
        yield return null;

        Assert.IsTrue(manager.IsSlotUpgrading(0),
            "Cancel closed the whole screen from the row list — it should step back to the category list first.");

        yield return Press(pad, GamepadButton.East);
        yield return null;

        Assert.IsFalse(manager.IsSlotUpgrading(0), "Cancel on the category list did not close the panel.");
        Shot("skilltree-closed");
    }

    static CharacterDefinition GetRosterCharacter(CharacterSelectionManager manager, int index)
    {
        FieldInfo field = typeof(CharacterSelectionManager).GetField("allCharacters", BindingFlags.NonPublic | BindingFlags.Instance);
        var roster = field?.GetValue(manager) as CharacterDefinition[];
        return roster != null && index < roster.Length ? roster[index] : null;
    }

    /// <summary>Same routing CharacterSelectionManager.LoadCharacterSave/CharacterStats.Record use —
    /// see the note at this test's souls-seeding call site for why a direct SaveManager read/write
    /// is not safe here anymore.</summary>
    static CharacterSaveData LoadCharacterSave(string characterID)
    {
        return GameSaveController.Instance != null
            ? GameSaveController.Instance.GetSharedRecord(characterID)
            : SaveManager.LoadCharacter(characterID);
    }

    static void FlushCharacterSave(string characterID)
    {
        if (GameSaveController.Instance != null) GameSaveController.Instance.FlushCharacter(characterID);
        else SaveManager.SaveCharacter(LoadCharacterSave(characterID));
    }
}
