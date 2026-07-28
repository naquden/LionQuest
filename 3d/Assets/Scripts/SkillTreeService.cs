using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stateless rules for skill-tree node purchases and effect resolution.
/// The tree (CharacterDefinition.skillTrees) is the source of truth; CharacterSaveData.purchasedSkillNodes
/// is the only persisted skill/attribute state. Shared by CharacterStats (in-game) and the character-select
/// upgrade screen (which has no CharacterStats instance yet — it edits CharacterSaveData directly).
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

    public static SkillNodeDefinition FindRootNode(SkillTreeDefinition tree)
    {
        if (tree == null || tree.nodes == null) return null;
        foreach (var node in tree.nodes)
        {
            if (node.effect == SkillEffect.Unlock && (node.prereqNodeIds == null || node.prereqNodeIds.Length == 0))
            {
                return node;
            }
        }
        return null;
    }

    /// <summary>Current rank of a node (0 if never purchased). Parses the ":rank" suffix.</summary>
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
        SkillNodeDefinition root = FindRootNode(tree);
        if (root == null) return false;
        return GetRank(save, skillId, root.nodeId) >= 1;
    }

    /// <summary>ANY-of: true if any listed prereq has rank >= 1, or the list is empty (root).</summary>
    public static bool ArePrereqsMet(CharacterSaveData save, string skillId, SkillNodeDefinition node)
    {
        if (node == null) return false;
        if (node.prereqNodeIds == null || node.prereqNodeIds.Length == 0) return true;

        foreach (string prereqId in node.prereqNodeIds)
        {
            if (GetRank(save, skillId, prereqId) >= 1) return true;
        }
        return false;
    }

    public static bool CanPurchase(CharacterDefinition definition, CharacterSaveData save, string skillId, string nodeId)
    {
        SkillTreeDefinition tree = FindTree(definition, skillId);
        SkillNodeDefinition node = FindNode(tree, nodeId);
        if (node == null) return false;
        if (GetRank(save, skillId, nodeId) >= node.maxRank) return false;
        if (!ArePrereqsMet(save, skillId, node)) return false;
        return save.souls >= node.cost;
    }

    /// <summary>Spends souls and increments the node's rank. Does not save to disk — caller decides when.</summary>
    public static bool TryPurchase(CharacterDefinition definition, CharacterSaveData save, string skillId, string nodeId)
    {
        if (!CanPurchase(definition, save, skillId, nodeId)) return false;

        SkillTreeDefinition tree = FindTree(definition, skillId);
        SkillNodeDefinition node = FindNode(tree, nodeId);
        int newRank = GetRank(save, skillId, nodeId) + 1;

        string prefix = KeyPrefix(skillId, nodeId);
        save.purchasedSkillNodes.RemoveAll(entry => entry.StartsWith(prefix));
        save.purchasedSkillNodes.Add($"{prefix}{newRank}");
        save.souls -= node.cost;
        return true;
    }

    /// <summary>Resolved (add, mul) totals per (skillId, effect), scaled by each purchased node's rank:
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

    /// <summary>1 + sum of ranks across all nodes in Attribute-target trees matching this effect.
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
