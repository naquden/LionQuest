using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Projectile that flies forward and deals damage on hit.
/// Attach to projectile prefabs (shuriken, arrow, fireball, etc.)
/// </summary>
[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Speed the projectile travels")]
    [SerializeField] private float speed = 15f;
    
    [Tooltip("Maximum distance before auto-destroy")]
    [SerializeField] private float maxDistance = 30f;
    
    [Tooltip("Should the projectile rotate/spin while flying?")]
    [SerializeField] private bool spinWhileFlying = true;
    
    [Tooltip("Spin speed in degrees per second")]
    [SerializeField] private float spinSpeed = 720f;
    
    [Header("Combat")]
    [Tooltip("Tag of targets this can hit")]
    [SerializeField] private string targetTag = "Enemy";
    
    [Tooltip("Can hit multiple targets (piercing)")]
    [SerializeField] private bool piercing = false;
    
    [Header("On Hit")]
    [Tooltip("Destroy on hit (if not piercing)")]
    [SerializeField] private bool destroyOnHit = true;
    
    [Tooltip("Effect to spawn on hit (optional)")]
    [SerializeField] private GameObject hitEffectPrefab;
    
    [Header("Debug")]
    [SerializeField] private bool debugProjectile = false;
    
    // Set by spawner
    private float damage;
    private float knockbackForce;
    private Vector3 flyDirection;
    private GameObject owner;
    private Vector3 startPosition;
    
    private HashSet<GameObject> hitTargets = new HashSet<GameObject>();
    private Collider projectileCollider;
    private bool isInitialized = false;
    
    private void Awake()
    {
        projectileCollider = GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }
    }
    
    /// <summary>
    /// Initialize the projectile with combat data
    /// </summary>
    public void Initialize(float damage, float knockbackForce, Vector3 direction, GameObject owner)
    {
        this.damage = damage;
        this.knockbackForce = knockbackForce;
        this.flyDirection = direction.normalized;
        this.owner = owner;
        this.startPosition = transform.position;
        this.isInitialized = true;
        
        // Face the direction of travel
        if (flyDirection.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(flyDirection);
        }
        
        if (debugProjectile)
        {
            Debug.Log($"[Projectile] Initialized - Direction: {flyDirection}, Damage: {damage}, Speed: {speed}");
        }
    }
    
    /// <summary>
    /// Set target tag
    /// </summary>
    public void SetTargetTag(string tag)
    {
        targetTag = tag;
    }
    
    /// <summary>
    /// Set speed (can be overridden by skill data)
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }
    
    private void Update()
    {
        if (!isInitialized) return;
        
        // Move forward
        transform.position += flyDirection * speed * Time.deltaTime;
        
        // Spin while flying (for shurikens, etc.)
        if (spinWhileFlying)
        {
            transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);
        }
        
        // Check max distance
        float distanceTraveled = Vector3.Distance(startPosition, transform.position);
        if (distanceTraveled >= maxDistance)
        {
            if (debugProjectile)
            {
                Debug.Log($"[Projectile] Max distance reached ({maxDistance}m), destroying");
            }
            Destroy(gameObject);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!isInitialized) return;
        
        // Don't hit owner
        if (owner != null && other.gameObject == owner)
        {
            return;
        }
        
        // Don't hit owner's children
        if (owner != null && other.transform.IsChildOf(owner.transform))
        {
            return;
        }
        
        // Check tag
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag))
        {
            // Hit something else (wall, etc.) - destroy if not piercing
            if (!piercing && other.gameObject.layer != LayerMask.NameToLayer("Ignore Raycast"))
            {
                // Only destroy on solid objects, not triggers
                if (!other.isTrigger)
                {
                    SpawnHitEffect(other.ClosestPoint(transform.position));
                    Destroy(gameObject);
                }
            }
            return;
        }
        
        // Skip if already hit this target (for piercing projectiles)
        if (hitTargets.Contains(other.gameObject))
        {
            return;
        }
        
        // Mark as hit
        hitTargets.Add(other.gameObject);
        
        // Calculate knockback direction
        Vector3 knockbackDir = flyDirection;
        knockbackDir.y = 0f;
        knockbackDir = knockbackDir.normalized;
        
        // Try to hit Enemy
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, knockbackDir, knockbackForce);
            
            if (debugProjectile)
            {
                Debug.Log($"[Projectile] ✓ Hit enemy {other.gameObject.name} for {damage} damage!");
            }
            
            SpawnHitEffect(other.ClosestPoint(transform.position));
            
            if (destroyOnHit && !piercing)
            {
                Destroy(gameObject);
            }
            return;
        }
        
        // Try to hit Player
        TopDownPlayerController player = other.GetComponent<TopDownPlayerController>();
        if (player != null)
        {
            player.TakeDamage(damage, knockbackDir, knockbackForce);
            
            if (debugProjectile)
            {
                Debug.Log($"[Projectile] ✓ Hit player {other.gameObject.name} for {damage} damage!");
            }
            
            SpawnHitEffect(other.ClosestPoint(transform.position));
            
            if (destroyOnHit && !piercing)
            {
                Destroy(gameObject);
            }
            return;
        }
        
        // Fallback: apply knockback to rigidbody
        Rigidbody targetRb = other.GetComponent<Rigidbody>();
        if (targetRb != null && !targetRb.isKinematic)
        {
            targetRb.AddForce(knockbackDir * knockbackForce, ForceMode.Impulse);
            
            if (debugProjectile)
            {
                Debug.Log($"[Projectile] Applied knockback to {other.gameObject.name}");
            }
            
            SpawnHitEffect(other.ClosestPoint(transform.position));
            
            if (destroyOnHit && !piercing)
            {
                Destroy(gameObject);
            }
        }
    }
    
    private void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, position, Quaternion.identity);
        }
    }
    
    private void OnDrawGizmos()
    {
        if (isInitialized)
        {
            // Draw flight path
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + flyDirection * 2f);
        }
    }
}
