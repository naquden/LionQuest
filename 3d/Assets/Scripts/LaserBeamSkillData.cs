using UnityEngine;

/// <summary>
/// ScriptableObject that defines a hitscan laser-beam skill: an instant raycast that damages
/// everything on the line, visualized by a short flash-and-fade beam prefab.
/// </summary>
[CreateAssetMenu(fileName = "New Laser Beam Skill", menuName = "LionQuest/Laser Beam Skill Data")]
public class LaserBeamSkillData : ScriptableObject
{
    [Header("Skill Info")]
    [Tooltip("Name of the skill (for debugging/logging)")]
    public string skillName = "Laser Beam";

    [Tooltip("HUD icon for this skill. Optional — HUD shows a placeholder if unset.")]
    public Sprite icon;

    [Header("Beam")]
    [Tooltip("Beam prefab to spawn (must have a LineRenderer and a LaserBeam script)")]
    public GameObject beamPrefab;

    [Tooltip("Maximum distance the beam reaches")]
    public float range = 20f;

    [Tooltip("Visual width of the beam line")]
    public float beamWidth = 0.12f;

    [Tooltip("How long the beam visual stays on screen before fading out")]
    public float visualDuration = 0.25f;

    [Header("Damage")]
    [Tooltip("Base damage dealt to each enemy the beam hits")]
    public float damage = 45f;

    [Tooltip("Knockback force applied to targets")]
    public float knockbackForce = 2f;

    [Tooltip("How many enemies one shot passes through. 1 = stops at the first.")]
    public int pierceCount = 1;

    [Header("Timing")]
    [Tooltip("Delay from the button press to the beam firing, matched to the skill-2 animation " +
             "so the beam leaves the hand as it points.")]
    public float fireDelay = 0.35f;

    [Tooltip("Cooldown before skill can be used again")]
    public float cooldown = 3f;

    [Header("Spawn")]
    [Tooltip("Local-space fallback offset (from the capsule bounds center) used when the muzzle " +
             "transform cannot be resolved.")]
    public Vector3 muzzleFallbackOffset = new Vector3(0.25f, 1.2f, 0.35f);
}
