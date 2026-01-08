using UnityEngine;

/// <summary>
/// ScriptableObject that defines attack properties including damage and knockback
/// Can be created for different attack types and character types
/// </summary>
[CreateAssetMenu(fileName = "New Attack Data", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("Attack Info")]
    [Tooltip("Name of the attack (for debugging/logging)")]
    public string attackName = "Attack";
    
    [Header("Effect")]
    [Tooltip("Prefab to spawn at AttackPoint (should have particle effect and AttackEffect script)")]
    public GameObject effectPrefab;
    
    [Tooltip("Lifetime of the effect (auto-destroy after this time, 0 = use particle duration)")]
    public float effectLifetime = 0f;
    
    [Header("Damage")]
    [Tooltip("Base damage dealt by this attack")]
    public float damage = 10f;
    
    [Header("Knockback")]
    [Tooltip("Force of knockback applied to the target (horizontal only, pushes away from attacker)")]
    public float knockbackForce = 5f;
    
    [Tooltip("Duration of knockback effect (how long the force is applied)")]
    public float knockbackDuration = 0.2f;
    
    [Header("Attack Range (fallback if no effect prefab)")]
    [Tooltip("Range of the attack (for melee attacks)")]
    public float attackRange = 1.5f;
    
    [Tooltip("Angle of attack cone (for melee attacks, in degrees)")]
    public float attackAngle = 60f;
    
    [Header("Cooldown")]
    [Tooltip("Cooldown time before this attack can be used again")]
    public float cooldown = 0.5f;
}


