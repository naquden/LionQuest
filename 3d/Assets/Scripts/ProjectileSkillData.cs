using UnityEngine;

/// <summary>
/// ScriptableObject that defines a projectile-based skill.
/// Supports spawning multiple projectiles at specific animation times.
/// </summary>
[CreateAssetMenu(fileName = "New Projectile Skill", menuName = "LionQuest/Projectile Skill Data")]
public class ProjectileSkillData : ScriptableObject
{
    [Header("Skill Info")]
    [Tooltip("Name of the skill (for debugging/logging)")]
    public string skillName = "Projectile Skill";
    
    [Header("Projectile")]
    [Tooltip("Projectile prefab to spawn (must have Projectile script)")]
    public GameObject projectilePrefab;
    
    [Tooltip("Speed of the projectile")]
    public float projectileSpeed = 15f;
    
    [Header("Damage")]
    [Tooltip("Base damage per projectile")]
    public float damage = 15f;
    
    [Tooltip("Knockback force applied to targets")]
    public float knockbackForce = 3f;
    
    [Header("Spawn Timing")]
    [Tooltip("Animation length in seconds (used to calculate spawn times from percentages)")]
    public float animationLength = 1f;
    
    [Tooltip("Spawn times as percentages of animation (0.0 to 1.0). Example: 0.3 = 30%, 0.5 = 50%")]
    public float[] spawnTimesNormalized = new float[] { 0.3f, 0.5f };
    
    [Header("Spawn Position")]
    [Tooltip("Offset from player position where projectile spawns (local space)")]
    public Vector3 spawnOffset = new Vector3(0f, 1f, 0.5f);
    
    [Header("Cooldown")]
    [Tooltip("Cooldown before skill can be used again")]
    public float cooldown = 2f;
    
    /// <summary>
    /// Get spawn times in seconds (calculated from normalized times and animation length)
    /// </summary>
    public float[] GetSpawnTimesInSeconds()
    {
        float[] times = new float[spawnTimesNormalized.Length];
        for (int i = 0; i < spawnTimesNormalized.Length; i++)
        {
            times[i] = spawnTimesNormalized[i] * animationLength;
        }
        return times;
    }
}
