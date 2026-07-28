using UnityEngine;

public enum SkillEffect { Unlock, Damage, Cooldown, Area, Duration, Magnitude, ProjectileCount, Strength, Health, Speed }

public enum SkillEffectOp { Add, Mul }

public enum SkillNodeTier { Keystone, Major, Minor }

public enum SkillEffectTarget { Skill, Attribute }

[System.Serializable]
public class SkillNodeDefinition
{
    [Tooltip("Unique within the tree, e.g. \"cooldown_2s\"")]
    public string nodeId;
    public string displayName;
    public string description;
    public SkillEffect effect;
    public SkillEffectOp op = SkillEffectOp.Add;
    public float value;
    public int cost = 10;
    [Tooltip(">1 = repeatable (e.g. +1 Strength, 5 ranks). Every rank costs the same flat `cost`.")]
    public int maxRank = 1;
    public SkillNodeTier tier = SkillNodeTier.Minor;
    [Tooltip("nodeIds within the same tree; ANY one at rank >= 1 satisfies this node. Empty = root.")]
    public string[] prereqNodeIds = System.Array.Empty<string>();
    [Tooltip("Authored layout position for the tree UI.")]
    public Vector2Int gridPos;
}

[System.Serializable]
public class SkillDisplayBase
{
    public SkillEffect effect;
    public float baseValue;
    public string unit;
}

[System.Serializable]
public class SkillTreeDefinition
{
    [Tooltip("Stable id, e.g. \"attributes\", \"whirlwind\", \"shuriken\", \"battlecry\"")]
    public string skillId;
    public string displayName;
    [Tooltip("0 = non-skill (attributes), 1..3 map to skill1/2/3 input")]
    public int skillSlot;
    public Color branchColor = Color.white;
    public SkillEffectTarget target = SkillEffectTarget.Skill;
    [Tooltip("Display-only \"before\" values sourced from SO assets, used to render before->after in the info panel.")]
    public SkillDisplayBase[] displayBases = System.Array.Empty<SkillDisplayBase>();
    public SkillNodeDefinition[] nodes = System.Array.Empty<SkillNodeDefinition>();
}
