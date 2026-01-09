using UnityEngine;

/// <summary>
/// ScriptableObject that defines attack properties including damage and knockback.
/// Requires an effect prefab with AttackEffect script for hit detection.
/// </summary>
[CreateAssetMenu(fileName = "New Attack Data", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("Attack Info")]
    [Tooltip("Name of the attack (for debugging/logging)")]
    public string attackName = "Attack";
    
    [Header("Effect (Required)")]
    [Tooltip("Prefab to spawn at AttackPoint. Must have a Collider and AttackEffect script for hit detection.")]
    public GameObject effectPrefab;
    
    [Tooltip("Lifetime of the effect (auto-destroy after this time, 0 = use particle duration)")]
    public float effectLifetime = 0f;
    
    [Header("Damage")]
    [Tooltip("Base damage dealt by this attack")]
    public float damage = 10f;
    
    [Header("Knockback")]
    [Tooltip("Force of knockback applied to the target (horizontal only, pushes away from attacker)")]
    public float knockbackForce = 5f;
    
    [Header("Cooldown")]
    [Tooltip("Cooldown time before this attack can be used again")]
    public float cooldown = 0.5f;
}


