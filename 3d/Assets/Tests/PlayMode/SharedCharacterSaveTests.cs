using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Pins the Phase 6 save model: "one save per game instance, each character has its own collected
/// souls, characters are shared between players" (the user's own words for the design).
///
/// Before this, each CharacterStats loaded its OWN copy of the same characterID's CharacterSaveData
/// in LoadStats(). Two players who both picked the same character therefore had two independent
/// souls counters, each writing to the same disk record — whichever saved last won, silently
/// discarding the other's progress, and OnEnemyKilled's "+1 soul to every registered player" doubled
/// a shared character's gain per kill instead of counting it once. GameSaveController now hands out
/// one CharacterSaveData per characterID (GetSharedRecord) that every CharacterStats bound to that
/// character reads and writes through, so "sharing a character" is one object in memory, not two
/// racing copies.
/// </summary>
public class SharedCharacterSaveTests : HarnessFixture
{
    const string LevelScene = "Level1";
    const string SharedCharacterID = "AssCap";

    /// <summary>Two players on the same character must see the SAME souls balance update from a
    /// single kill — not each gaining their own +1 on top of a private copy.</summary>
    [UnityTest]
    public IEnumerator TwoPlayersSharingACharacter_ShareOneSoulsBalance()
    {
        IgnoreFailingLogs();
        yield return LoadSharedCharacterCoopLevel();

        CharacterStats[] stats = Object.FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        Assert.AreEqual(2, stats.Length, "Expected two players in the seeded co-op run.");
        Assert.AreEqual(SharedCharacterID, stats[0].characterID, "Player 0 is not on the shared character.");
        Assert.AreEqual(SharedCharacterID, stats[1].characterID, "Player 1 is not on the shared character.");

        int soulsBefore = stats[0].souls;
        Assert.AreEqual(soulsBefore, stats[1].souls,
            "Two fresh CharacterStats on the same characterID disagree on souls before anything happened — " +
            "they are not reading the same shared record.");

        Enemy enemy = Object.FindAnyObjectByType<Enemy>();
        Assert.IsNotNull(enemy, $"{LevelScene} has no enemies.");
        enemy.TakeDamage(999999f);
        yield return null;

        // GameSaveController.OnEnemyKilled awards +1 soul per REGISTERED PLAYER, which is the co-op
        // design (doc/project.md: "1 per enemy killed", paid to every active player) — that part is
        // unchanged and correct. What must NOT happen is the shared character's balance ending up
        // higher than one kill's worth of reward, or the two instances disagreeing with each other.
        Assert.AreEqual(stats[0].souls, stats[1].souls,
            $"After a kill, player 0 reads {stats[0].souls} souls and player 1 reads {stats[1].souls} for the " +
            "SAME character — they still hold independent copies instead of one shared record.");
        Assert.AreEqual(soulsBefore + 1, stats[0].souls,
            $"One kill with two players registered should add 1 soul once to the shared character's balance " +
            $"(not once per player instance), but it went {soulsBefore} -> {stats[0].souls}.");

        Shot("shared-character-souls-in-sync");
    }

    /// <summary>A purchase made by one instance must be visible to the other instance sharing the
    /// same character, without either instance reloading from disk.</summary>
    [UnityTest]
    public IEnumerator PurchaseByOneInstance_IsVisibleToTheOther()
    {
        IgnoreFailingLogs();
        yield return LoadSharedCharacterCoopLevel();

        CharacterStats[] stats = Object.FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        Assert.AreEqual(2, stats.Length, "Expected two players.");

        // Give the shared character enough souls to buy something, via the same registry path a real
        // kill would use, then spend from instance 0 and check instance 1 sees the drop.
        stats[0].AddSouls(999);
        int soulsAfterGrant = stats[1].souls;
        Assert.AreEqual(stats[0].souls, soulsAfterGrant,
            "AddSouls on instance 0 is not visible to instance 1 sharing the same character.");

        string skillId = FindAnyUnlockableSkillId(stats[0]);
        Assert.IsNotNull(skillId, "Could not find any skill tree to test a purchase against.");

        bool bought = stats[0].TryPurchaseNode(skillId, "unlock");
        Assert.IsTrue(bought, $"TryPurchaseNode('{skillId}', 'unlock') failed — check the node id matches the tree.");

        Assert.AreEqual(stats[0].souls, stats[1].souls,
            $"After a purchase on instance 0 (souls now {stats[0].souls}), instance 1 still reads " +
            $"{stats[1].souls} for the same character — the two are not sharing one record.");
        Assert.IsTrue(stats[1].IsSkillUnlocked(skillId),
            "Instance 1 does not see the skill instance 0 just unlocked on their shared character.");
    }

    /// <summary>The first tree that actually has an Unlock-effect node — "attributes" (the first
    /// tree in AssCap's array) has none, since attribute rows are always purchasable directly.</summary>
    static string FindAnyUnlockableSkillId(CharacterStats stats)
    {
        if (stats.definition == null || stats.definition.skillTrees == null) return null;
        foreach (SkillTreeDefinition tree in stats.definition.skillTrees)
        {
            if (tree == null || tree.nodes == null) continue;
            foreach (SkillNodeDefinition node in tree.nodes)
            {
                if (node.effect == SkillEffect.Unlock) return tree.skillId;
            }
        }
        return null;
    }

    /// <summary>Seeds two confirmed selections on the SAME characterID and loads Level1 — the
    /// sharing scenario the save model exists for.</summary>
    IEnumerator LoadSharedCharacterCoopLevel()
    {
        CharacterSelectionData data = CharacterSelectionData.GetOrCreate();
        data.Clear();
        data.selections.Add(new CharacterSelectionData.Selection { characterID = SharedCharacterID, deviceId = -1 });
        data.selections.Add(new CharacterSelectionData.Selection { characterID = SharedCharacterID, deviceId = -1 });

        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;
    }
}
