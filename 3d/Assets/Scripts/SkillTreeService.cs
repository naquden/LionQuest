using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stateless rules for upgrade purchases and effect resolution.
/// The categories (CharacterDefinition.skillTrees) are the source of truth; CharacterSaveData.purchasedSkillNodes
/// is the only persisted skill/attribute state. Shared by CharacterStats (in-game) and the character-select
/// upgrade screen (which has no CharacterStats instance yet — it edits CharacterSaveData directly).
///
/// There is no prerequisite graph. A skill category has one Unlock row that gates every other row in
/// that category; the attribute category has no gate at all. Rows are ranked ladders whose price can
/// rise per rank (<see cref="SkillNodeDefinition.costIncrement"/>).
/// </summary>
public static class SkillTreeService
{
    private static string KeyPrefix(string skillId, string nodeId) => $"{skillId}/{nodeId}:";

    public static SkillTreeDefinition FindTree(CharacterDefinition definition, string skillId)
    {
        if (definition == null || definition.skillTrees == null) return null;
        foreach (var tree in definition.skillTrees)
        {
            if (tree.skillId == skillId) return tree;
        }
        return null;
    }

    public static SkillNodeDefinition FindNode(SkillTreeDefinition tree, string nodeId)
    {
        if (tree == null || tree.nodes == null) return null;
        foreach (var node in tree.nodes)
        {
            if (node.nodeId == nodeId) return node;
        }
        return null;
    }

    /// <summary>The category's gate row, or null for a category that has none (attributes).</summary>
    public static SkillNodeDefinition FindUnlockNode(SkillTreeDefinition tree)
    {
        if (tree == null || tree.nodes == null) return null;
        foreach (var node in tree.nodes)
        {
            if (node.effect == SkillEffect.Unlock) return node;
        }
        return null;
    }

    /// <summary>Current rank of a row (0 if never purchased). Parses the ":rank" suffix.</summary>
    public static int GetRank(CharacterSaveData save, string skillId, string nodeId)
    {
        if (save == null || save.purchasedSkillNodes == null) return 0;
        string prefix = KeyPrefix(skillId, nodeId);
        foreach (string entry in save.purchasedSkillNodes)
        {
            if (entry.StartsWith(prefix) && int.TryParse(entry.Substring(prefix.Length), out int rank))
            {
                return rank;
            }
        }
        return 0;
    }

    public static bool IsSkillUnlocked(CharacterDefinition definition, CharacterSaveData save, string skillId)
    {
        SkillTreeDefinition tree = FindTree(definition, skillId);
        SkillNodeDefinition gate = FindUnlockNode(tree);
        if (gate == null) return false;
        return GetRank(save, skillId, gate.nodeId) >= 1;
    }

    /// <summary>Price of this row's NEXT rank: cost + costIncrement * ranks already owned.</summary>
    public static int GetNextCost(CharacterSaveData save, string skillId, SkillNodeDefinition node)
    {
        if (node == null) return 0;
        int rank = GetRank(save, skillId, node.nodeId);
        return Mathf.Max(0, node.cost + node.costIncrement * rank);
    }

    /// <summary>A row is available once its category's Unlock row is bought (the Unlock row itself,
    /// and every row of a category without one, is always available).</summary>
    public static bool IsRowAvailable(SkillTreeDefinition tree, CharacterSaveData save, SkillNodeDefinition node)
    {
        if (tree == null || node == null) return false;
        if (node.effect == SkillEffect.Unlock) return true;

        SkillNodeDefinition gate = FindUnlockNode(tree);
        if (gate == null) return true; // attributes — nothing to unlock first
        return GetRank(save, tree.skillId, gate.nodeId) >= 1;
    }

    public static bool CanPurchase(CharacterDefinition definition, CharacterSaveData save, string skillId, string nodeId)
    {
        SkillTreeDefinition tree = FindTree(definition, skillId);
        SkillNodeDefinition node = FindNode(tree, nodeId);
        if (node == null || save == null) return false;
        if (GetRank(save, skillId, nodeId) >= node.maxRank) return false;
        if (!IsRowAvailable(tree, save, node)) return false;
        return save.souls >= GetNextCost(save, skillId, node);
    }

    /// <summary>Spends souls and increments the row's rank. Does not save to disk — caller decides when.</summary>
    public static bool TryPurchase(CharacterDefinition definition, CharacterSaveData save, string skillId, string nodeId)
    {
        if (!CanPurchase(definition, save, skillId, nodeId)) return false;

        SkillTreeDefinition tree = FindTree(definition, skillId);
        SkillNodeDefinition node = FindNode(tree, nodeId);
        int price = GetNextCost(save, skillId, node);
        int newRank = GetRank(save, skillId, nodeId) + 1;

        string prefix = KeyPrefix(skillId, nodeId);
        save.purchasedSkillNodes.RemoveAll(entry => entry.StartsWith(prefix));
        save.purchasedSkillNodes.Add($"{prefix}{newRank}");
        save.souls -= price;
        return true;
    }

    /// <summary>Total souls sunk into a category, priced with its per-rank increments.</summary>
    public static int GetSpentOnTree(CharacterSaveData save, SkillTreeDefinition tree)
    {
        if (tree == null || tree.nodes == null) return 0;

        int spent = 0;
        foreach (SkillNodeDefinition node in tree.nodes)
        {
            int rank = GetRank(save, tree.skillId, node.nodeId);
            for (int r = 0; r < rank; r++)
            {
                spent += Mathf.Max(0, node.cost + node.costIncrement * r);
            }
        }
        return spent;
    }

    /// <summary>Resolved (add, mul) totals per (skillId, effect), scaled by each purchased row's rank:
    /// add += value * rank, mul *= pow(value, rank).</summary>
    public static Dictionary<(string skillId, SkillEffect effect), (float add, float mul)> ResolveSkillValues(
        CharacterDefinition definition, CharacterSaveData save)
    {
        var resolved = new Dictionary<(string, SkillEffect), (float add, float mul)>();
        if (definition == null || definition.skillTrees == null || save == null || save.purchasedSkillNodes == null)
        {
            return resolved;
        }

        foreach (SkillTreeDefinition tree in definition.skillTrees)
        {
            if (tree.nodes == null) continue;
            foreach (SkillNodeDefinition node in tree.nodes)
            {
                int rank = GetRank(save, tree.skillId, node.nodeId);
                if (rank <= 0) continue;

                var mapKey = (tree.skillId, node.effect);
                if (!resolved.TryGetValue(mapKey, out var current))
                {
                    current = (0f, 1f);
                }

                if (node.op == SkillEffectOp.Add)
                {
                    current.add += node.value * rank;
                }
                else
                {
                    current.mul *= Mathf.Pow(node.value, rank);
                }
                resolved[mapKey] = current;
            }
        }

        return resolved;
    }

    public static float GetValue(
        Dictionary<(string skillId, SkillEffect effect), (float add, float mul)> resolved,
        string skillId, SkillEffect effect, float fallback)
    {
        if (resolved == null || !resolved.TryGetValue((skillId, effect), out var modifiers))
        {
            return fallback;
        }
        return (fallback + modifiers.add) * modifiers.mul;
    }

    /// <summary>1 + sum of ranks across all rows in Attribute-target categories matching this effect.
    /// Replaces the removed strengthLevel/healthLevel/speedLevel save fields.</summary>
    public static int GetAttributeLevel(CharacterDefinition definition, CharacterSaveData save, SkillEffect effect)
    {
        int total = 0;
        if (definition == null || definition.skillTrees == null) return 1;

        foreach (SkillTreeDefinition tree in definition.skillTrees)
        {
            if (tree.target != SkillEffectTarget.Attribute || tree.nodes == null) continue;
            foreach (SkillNodeDefinition node in tree.nodes)
            {
                if (node.effect != effect) continue;
                total += GetRank(save, tree.skillId, node.nodeId);
            }
        }

        return 1 + total;
    }
}
