using UnityEngine;

/// <summary>
/// Handles player combat - detecting enemies and applying damage/knockback.
/// Attach to player and assign AttackData for different attacks.
/// </summary>
public class CombatController : MonoBehaviour
{
    [Header("Combat Settings")]
    [Tooltip("Multiplier applied to all knockback from this character's attacks")]
    [SerializeField] private float knockbackMultiplier = 1f;
    
    [Header("Hit Detection")]
    [Tooltip("Layer mask for what this entity can hit")]
    [SerializeField] private LayerMask hitLayers = -1;
    
    [Tooltip("Tag of entities this can hit (e.g., 'Enemy' for player)")]
    [SerializeField] private string targetTag = "Enemy";
    
    [Header("References")]
    [Tooltip("Point where attacks originate (defaults to transform if not set)")]
    [SerializeField] private Transform attackPoint;
    
    [Header("Debug")]
    [SerializeField] private bool debugCombat = false;
    
    private float lastAttackTime = 0f;
    
    /// <summary>
    /// Event called when hitting an enemy
    /// </summary>
    public System.Action<GameObject, float> OnHitEnemy; // target, damage
    
    private void Awake()
    {
        // Default attack point to this transform
        if (attackPoint == null)
        {
            attackPoint = transform;
        }
        
        // Auto-detect target tag based on this entity's tag
        if (string.IsNullOrEmpty(targetTag))
        {
            if (gameObject.CompareTag("Player"))
            {
                targetTag = "Enemy";
            }
            else if (gameObject.CompareTag("Enemy"))
            {
                targetTag = "Player";
            }
        }
    }
    
    /// <summary>
    /// Perform an attack with the given attack data
    /// </summary>
    public void PerformAttack(AttackData attackData)
    {
        if (attackData == null)
        {
            Debug.LogError($"[Combat] {gameObject.name}: PerformAttack called with null AttackData!");
            return;
        }
        
        // Check cooldown
        if (Time.time - lastAttackTime < attackData.cooldown)
        {
            return;
        }
        
        lastAttackTime = Time.time;
        
        if (debugCombat)
        {
            Debug.Log($"[Combat] {gameObject.name} performing '{attackData.attackName}'");
        }
        
        // Spawn effect prefab if available (handles its own collision detection)
        if (attackData.effectPrefab != null)
        {
            SpawnAttackEffect(attackData);
        }
        else
        {
            // Fallback: use sphere detection if no effect prefab
            DetectAndHitTargets(attackData);
        }
    }
    
    /// <summary>
    /// Spawn the attack effect prefab at the attack point
    /// </summary>
    private void SpawnAttackEffect(AttackData attackData)
    {
        Vector3 spawnPos = attackPoint.position;
        Quaternion spawnRot = transform.rotation;
        
        GameObject effectObj = Instantiate(attackData.effectPrefab, spawnPos, spawnRot);
        
        // Initialize the attack effect
        AttackEffect effect = effectObj.GetComponent<AttackEffect>();
        if (effect != null)
        {
            effect.Initialize(
                attackData.damage,
                attackData.knockbackForce,
                knockbackMultiplier,
                transform.forward,
                gameObject,
                attackData.effectLifetime
            );
            effect.SetTargetTag(targetTag);
            
            if (debugCombat)
            {
                Debug.Log($"[Combat] Spawned attack effect '{attackData.effectPrefab.name}' at {spawnPos}");
            }
        }
        else
        {
            Debug.LogWarning($"[Combat] Effect prefab '{attackData.effectPrefab.name}' has no AttackEffect script! Add it for collision detection.");
            
            // Still destroy the effect after lifetime
            float lifetime = attackData.effectLifetime > 0 ? attackData.effectLifetime : 1f;
            Destroy(effectObj, lifetime);
        }
    }
    
    /// <summary>
    /// Detect targets in range and apply damage/knockback
    /// </summary>
    private void DetectAndHitTargets(AttackData attackData)
    {
        Collider[] hitColliders = Physics.OverlapSphere(attackPoint.position, attackData.attackRange, hitLayers);
        Vector3 attackDirection = transform.forward;
        
        if (debugCombat && hitColliders.Length == 0)
        {
            Debug.Log($"[Combat] No targets found in range {attackData.attackRange}");
        }
        
        foreach (Collider hitCollider in hitColliders)
        {
            // Skip if wrong tag
            if (!string.IsNullOrEmpty(targetTag) && !hitCollider.CompareTag(targetTag))
            {
                continue;
            }
            
            // Check attack angle
            Vector3 directionToTarget = (hitCollider.transform.position - attackPoint.position);
            directionToTarget.y = 0f;
            
            if (directionToTarget.magnitude > 0.01f)
            {
                float angle = Vector3.Angle(attackDirection, directionToTarget.normalized);
                if (angle > attackData.attackAngle / 2f)
                {
                    continue;
                }
            }
            
            // Calculate knockback direction (away from attacker, horizontal only)
            Vector3 knockbackDir = directionToTarget.normalized;
            float totalKnockback = attackData.knockbackForce * knockbackMultiplier;
            
            // Try to hit Enemy script
            Enemy enemy = hitCollider.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(attackData.damage, knockbackDir, totalKnockback);
                
                Debug.Log($"[Combat] ✓ {gameObject.name} hit {hitCollider.gameObject.name} for {attackData.damage} damage!");
                OnHitEnemy?.Invoke(hitCollider.gameObject, attackData.damage);
                continue;
            }
            
            // Fallback: If target has Rigidbody but no Enemy script, just apply force
            Rigidbody targetRb = hitCollider.GetComponent<Rigidbody>();
            if (targetRb != null && !targetRb.isKinematic)
            {
                targetRb.AddForce(knockbackDir * totalKnockback, ForceMode.Impulse);
                
                if (debugCombat)
                {
                    Debug.Log($"[Combat] Applied knockback to {hitCollider.gameObject.name} (no Enemy script)");
                }
            }
        }
    }
    
    /// <summary>
    /// Set knockback multiplier (for character-specific knockback strength)
    /// </summary>
    public void SetKnockbackMultiplier(float multiplier)
    {
        knockbackMultiplier = multiplier;
    }
    
    /// <summary>
    /// Set the attack point transform (where attacks originate from)
    /// </summary>
    public void SetAttackPoint(Transform point)
    {
        attackPoint = point;
    }
    
    /// <summary>
    /// Get the current attack point transform
    /// </summary>
    public Transform GetAttackPoint()
    {
        return attackPoint;
    }
    
    private void OnDrawGizmosSelected()
    {
        Transform point = attackPoint != null ? attackPoint : transform;
        
        // Default attack range visualization
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(point.position, 1.5f);
        
        // Attack point
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(point.position, 0.1f);
    }
}
