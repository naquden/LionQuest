using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attach to attack effect prefabs. Handles collision-based hit detection
/// and auto-destruction after the particle effect completes.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AttackEffect : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tag of targets this effect can hit")]
    [SerializeField] private string targetTag = "Enemy";
    
    [Tooltip("Can this effect hit multiple targets?")]
    [SerializeField] private bool canHitMultiple = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugHits = false;
    
    // Set by CombatController when spawning
    private float damage;
    private float knockbackForce;
    private float knockbackMultiplier = 1f;
    private Vector3 attackDirection;
    private GameObject attacker;
    private float lifetime;
    
    private HashSet<GameObject> hitTargets = new HashSet<GameObject>();
    private ParticleSystem particles;
    private bool isInitialized = false;
    
    /// <summary>
    /// Initialize the attack effect with combat data
    /// </summary>
    public void Initialize(float damage, float knockbackForce, float knockbackMultiplier, 
                          Vector3 attackDirection, GameObject attacker, float lifetime)
    {
        this.damage = damage;
        this.knockbackForce = knockbackForce;
        this.knockbackMultiplier = knockbackMultiplier;
        this.attackDirection = attackDirection;
        this.attacker = attacker;
        this.lifetime = lifetime;
        this.isInitialized = true;
        
        // Get particle system
        particles = GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = GetComponentInChildren<ParticleSystem>();
        }
        
        // Ensure collider is trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        // Schedule destruction
        float destroyTime = lifetime;
        if (destroyTime <= 0f && particles != null)
        {
            // Use particle system duration
            destroyTime = particles.main.duration + particles.main.startLifetime.constantMax;
        }
        
        if (destroyTime <= 0f)
        {
            destroyTime = 1f; // Default fallback
        }
        
        Destroy(gameObject, destroyTime);
        
        if (debugHits)
        {
            Debug.Log($"[AttackEffect] Initialized - Damage: {damage}, Knockback: {knockbackForce}, Lifetime: {destroyTime}");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!isInitialized) return;
        
        // Check tag
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag))
        {
            return;
        }
        
        // Skip if already hit this target
        if (hitTargets.Contains(other.gameObject))
        {
            return;
        }
        
        // Don't hit the attacker
        if (attacker != null && other.gameObject == attacker)
        {
            return;
        }
        
        // Mark as hit
        hitTargets.Add(other.gameObject);
        
        // Calculate knockback direction (from attacker to target, horizontal only)
        Vector3 knockbackDir;
        if (attacker != null)
        {
            knockbackDir = (other.transform.position - attacker.transform.position);
            knockbackDir.y = 0f;
            knockbackDir = knockbackDir.normalized;
        }
        else
        {
            knockbackDir = attackDirection;
            knockbackDir.y = 0f;
            knockbackDir = knockbackDir.normalized;
        }
        
        float totalKnockback = knockbackForce * knockbackMultiplier;
        
        // Try to hit Enemy script
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, knockbackDir, totalKnockback);
            
            if (debugHits)
            {
                Debug.Log($"[AttackEffect] ✓ Hit enemy {other.gameObject.name} for {damage} damage!");
            }
            return;
        }
        
        // Try to hit Player script
        TopDownPlayerController player = other.GetComponent<TopDownPlayerController>();
        if (player != null)
        {
            player.TakeDamage(damage, knockbackDir, totalKnockback);
            
            if (debugHits)
            {
                Debug.Log($"[AttackEffect] ✓ Hit player {other.gameObject.name} for {damage} damage!");
            }
            return;
        }

        // Fallback: apply force to rigidbody
        Rigidbody targetRb = other.GetComponent<Rigidbody>();
        if (targetRb != null && !targetRb.isKinematic)
        {
            targetRb.AddForce(knockbackDir * totalKnockback, ForceMode.Impulse);
            
            if (debugHits)
            {
                Debug.Log($"[AttackEffect] Applied knockback to {other.gameObject.name} (no Enemy/Player script)");
            }
        }
        
        // Destroy if single-hit only
        if (!canHitMultiple)
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Set target tag (called by CombatController)
    /// </summary>
    public void SetTargetTag(string tag)
    {
        targetTag = tag;
    }
    
    private void OnDrawGizmos()
    {
        // Show collider bounds
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
