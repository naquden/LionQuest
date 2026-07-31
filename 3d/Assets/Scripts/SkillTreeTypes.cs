using UnityEngine;

// Append only — enum values serialize as ints in .asset YAML, so inserting a member would
// silently re-map every already-purchased skill node in every CharacterDefinition asset.
public enum SkillEffect { Unlock, Damage, Cooldown, Area, Duration, Magnitude, ProjectileCount, Strength, Health, Speed, Range, PierceCount }

public enum SkillEffectOp { Add, Mul }

public enum SkillEffectTarget { Skill, Attribute }

/// <summary>
/// One purchasable upgrade row. There is no node graph any more: a row is either the tree's
/// single <see cref="SkillEffect.Unlock"/> row (which gates the rest of that tree) or a plain
/// ranked upgrade that can be bought up to <see cref="maxRank"/> times.
/// </summary>
[System.Serializable]
public class SkillNodeDefinition
{
    [Tooltip("Unique within the tree, e.g. \"cooldown\"")]
    public string nodeId;
    public string displayName;
    [Tooltip("Plain-language, one short sentence. Shown on the row itself, not in a side panel.")]
    public string description;
    public SkillEffect effect;
    public SkillEffectOp op = SkillEffectOp.Add;
    [Tooltip("Value added (or multiplied) per rank.")]
    public float value;
    [Tooltip("Cost of the FIRST rank.")]
    public int cost = 10;
    [Tooltip("Added to the cost for each rank already owned. 0 = flat cost per rank.")]
    public int costIncrement = 0;
    [Tooltip(">1 = buyable several times (e.g. Damage, 5 ranks). 1 = one-shot.")]
    public int maxRank = 1;
}

[System.Serializable]
public class SkillDisplayBase
{
    public SkillEffect effect;
    public float baseValue;
    public string unit;
}

/// <summary>
/// One category in the upgrade screen: either the shared attribute category or one skill.
/// Rows are shown in array order; a skill category's <see cref="SkillEffect.Unlock"/> row is
/// always presented first and every other row stays locked until it is bought.
/// </summary>
[System.Serializable]
public class SkillTreeDefinition
{
    [Tooltip("Stable id, e.g. \"attributes\", \"whirlwind\", \"shuriken\", \"battlecry\"")]
    public string skillId;
    public string displayName;
    [Tooltip("0 = non-skill (attributes), 1..3 map to skill1/2/3 input")]
    public int skillSlot;
    [Tooltip("Category accent colour used by the tab and the row highlight.")]
    public Color branchColor = Color.white;
    public SkillEffectTarget target = SkillEffectTarget.Skill;
    [Tooltip("One short line describing what this skill does, shown under the category title.")]
    public string summary;
    [Tooltip("Display-only \"before\" values sourced from SO assets, used to render before->after on a row.")]
    public SkillDisplayBase[] displayBases = System.Array.Empty<SkillDisplayBase>();
    public SkillNodeDefinition[] nodes = System.Array.Empty<SkillNodeDefinition>();
}
