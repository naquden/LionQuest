using UnityEngine;

public enum SkillEffect { Unlock, Damage, Cooldown, Area, Duration, Magnitude, ProjectileCount }

public enum SkillEffectOp { Add, Mul }

[System.Serializable]
public class SkillNodeDefinition
{
    [Tooltip("Unique within the skill, e.g. \"cooldown_2s\"")]
    public string nodeId;
    public string displayName;
    public string description;
    public SkillEffect effect;
    public SkillEffectOp op = SkillEffectOp.Add;
    public float value;
    public int cost = 10;
    [Tooltip("nodeIds within the same skill that must ALL be purchased first. Empty = root/unlock node.")]
    public string[] prereqNodeIds = System.Array.Empty<string>();
    [Tooltip("Authored layout position for the tree UI.")]
    public Vector2Int gridPos;
}

[System.Serializable]
public class SkillTreeDefinition
{
    [Tooltip("Stable id, e.g. \"whirlwind\", \"shuriken\", \"battlecry\"")]
    public string skillId;
    public string displayName;
    [Tooltip("1..3 — maps to skill1/2/3 input")]
    public int skillSlot;
    public SkillNodeDefinition[] nodes = System.Array.Empty<SkillNodeDefinition>();
}
