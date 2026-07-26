using UnityEngine;

/// <summary>
/// ScriptableObject defining a temporary self-buff skill (Battle Cry).
/// The speed multiplier magnitude comes from CharacterStats.GetBattleCryMultiplier() (speedLevel);
/// this asset holds duration/cooldown and optional VFX/SFX hooks.
/// </summary>
[CreateAssetMenu(fileName = "New Battle Cry Skill", menuName = "LionQuest/Battle Cry Skill Data")]
public class BattleCrySkillData : ScriptableObject
{
    [Header("Skill Info")]
    public string skillName = "Battle Cry";

    [Tooltip("HUD icon for this skill. Optional — HUD shows a placeholder if unset.")]
    public Sprite icon;

    [Header("Buff")]
    [Tooltip("How long the speed buff lasts (seconds)")]
    public float duration = 5f;

    [Tooltip("Cooldown before Battle Cry can be used again (seconds)")]
    public float cooldown = 10f;

    [Header("VFX/SFX (optional)")]
    [Tooltip("Optional effect spawned on the caster when Battle Cry activates")]
    public GameObject activateEffectPrefab;
}
