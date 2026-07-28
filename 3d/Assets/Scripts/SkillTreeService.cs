using System.Collections.Generic;

/// <summary>
/// Stateless rules for skill-tree node purchases and effect resolution.
/// The tree (CharacterDefinition.skillTrees) is the source of truth; CharacterSaveData.purchasedSkillNodes
/// is the only persisted skill state. Shared by CharacterStats (in-game) and the character-select
/// upgrade screen (which has no CharacterStats instance yet — it edits CharacterSaveData directly).
/// </summary>
public static class SkillTreeService
{
    private static string Key(string skillId, string nodeId) => $"{skillId}/{nodeId}";

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

    public static bool IsNodePurchased(CharacterSaveData save, string skillId, string nodeId)
    {
        return save != null && save.purchasedSkillNodes != null && save.purchasedSkillNodes.Contains(Key(skillId, nodeId));
    }

    public static bool IsSkillUnlocked(CharacterDefinition definition, CharacterSaveData save, string skillId)
    {
        SkillTreeDefinition tree = FindTree(definition, skillId);
        SkillNodeDefinition root = FindRootNode(tree);
        if (root == null) return false;
        return IsNodePurchased(save, skillId, root.nodeId);
    }

    public static bool ArePrereqsMet(CharacterSaveData save, string skillId, SkillNodeDefinition node)
    {
        if (node == null) return false;
        if (node.prereqNodeIds == null || node.prereqNodeIds.Length == 0) return true;

        foreach (string prereqId in node.prereqNodeIds)
        {
            if (!IsNodePurchased(save, skillId, prereqId)) return false;
        }
        return true;
    }

    public static bool CanPurchase(CharacterDefinition definition, CharacterSaveData save, string skillId, string nodeId)
    {
        SkillTreeDefinition tree = FindTree(definition, skillId);
        SkillNodeDefinition node = FindNode(tree, nodeId);
        if (node == null) return false;
        if (IsNodePurchased(save, skillId, nodeId)) return false;
        if (!ArePrereqsMet(save, skillId, node)) return false;
        return save.souls >= node.cost;
    }

    /// <summary>Spends souls and records the purchase. Does not save to disk — caller decides when.</summary>
    public static bool TryPurchase(CharacterDefinition definition, CharacterSaveData save, string skillId, string nodeId)
    {
        if (!CanPurchase(definition, save, skillId, nodeId)) return false;

        SkillTreeDefinition tree = FindTree(definition, skillId);
        SkillNodeDefinition node = FindNode(tree, nodeId);

        save.souls -= node.cost;
        save.purchasedSkillNodes.Add(Key(skillId, nodeId));
        return true;
    }

    /// <summary>Resolved (add, mul) totals per (skillId, effect), from every purchased node.</summary>
    public static Dictionary<(string skillId, SkillEffect effect), (float add, float mul)> ResolveSkillValues(
        CharacterDefinition definition, CharacterSaveData save)
    {
        var resolved = new Dictionary<(string, SkillEffect), (float add, float mul)>();
        if (definition == null || definition.skillTrees == null || save == null || save.purchasedSkillNodes == null)
        {
            return resolved;
        }

        var purchased = new HashSet<string>(save.purchasedSkillNodes);

        foreach (SkillTreeDefinition tree in definition.skillTrees)
        {
            if (tree.nodes == null) continue;
            foreach (SkillNodeDefinition node in tree.nodes)
            {
                if (!purchased.Contains(Key(tree.skillId, node.nodeId))) continue;

                var mapKey = (tree.skillId, node.effect);
                if (!resolved.TryGetValue(mapKey, out var current))
                {
                    current = (0f, 1f);
                }

                if (node.op == SkillEffectOp.Add)
                {
                    current.add += node.value;
                }
                else
                {
                    current.mul *= node.value;
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
}
