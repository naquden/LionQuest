using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles player combat - detecting enemies and applying damage/knockback.
/// Attach to player and assign AttackData for different attacks.
/// </summary>
public class CombatController : MonoBehaviour
{
    [Header("Combat Settings")]
    [Tooltip("Multiplier applied to all knockback from this character's attacks")]
    [SerializeField] private float knockbackMultiplier = 1f;
    
    [Tooltip("Multiplier applied to damage (from stats/powerups)")]
    [SerializeField] private float damageMultiplier = 1f;
    
    [Tooltip("Tag of entities this can hit (e.g., 'Enemy' for player, 'Player' for enemy)")]
    [SerializeField] private string targetTag = "Enemy";
    
    [Header("References")]
    [Tooltip("Point where attacks originate (auto-created if not set)")]
    [SerializeField] private Transform attackPoint;
    
    [Header("Attack Point Settings")]
    [Tooltip("Distance in front of character where AttackPoint is created")]
    [SerializeField] private float attackPointDistance = 0.8f;
    
    [Tooltip("Height of the AttackPoint")]
    [SerializeField] private float attackPointHeight = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool debugCombat = false;
    
    private readonly Dictionary<AttackData, float> lastUseTimes = new Dictionary<AttackData, float>();
    
    /// <summary>
    /// Event called when hitting an enemy
    /// </summary>
    public System.Action<GameObject, float> OnHitEnemy; // target, damage
    
    private void Awake()
    {
        // Create AttackPoint if not assigned
        if (attackPoint == null)
        {
            // First check if one already exists as child
            attackPoint = transform.Find("AttackPoint");
            
            if (attackPoint == null)
            {
                // Create new AttackPoint
                GameObject attackPointObj = new GameObject("AttackPoint");
                attackPointObj.transform.SetParent(transform);
                attackPointObj.transform.localPosition = new Vector3(0, attackPointHeight, attackPointDistance);
                attackPointObj.transform.localRotation = Quaternion.identity;
                attackPoint = attackPointObj.transform;
                
                if (debugCombat)
                {
                    Debug.Log($"[Combat] Created AttackPoint for {gameObject.name}");
                }
            }
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
    /// Perform an attack with the given attack data.
    /// </summary>
    /// <param name="damageOverride">If >= 0, used instead of attackData.damage (before damageMultiplier).</param>
    /// <param name="radiusOverride">If > 0, overrides the effect's SphereCollider world radius (stat-scaled AoE).</param>
    /// <param name="bypassCooldown">Skip this controller's per-attack cooldown check (caller owns gating, e.g. a global skill lock).</param>
    public void PerformAttack(AttackData attackData, float damageOverride = -1f, float radiusOverride = -1f, bool bypassCooldown = false)
    {
        if (attackData == null)
        {
            Debug.LogError($"[Combat] {gameObject.name}: PerformAttack called with null AttackData!");
            return;
        }

        if (attackData.effectPrefab == null)
        {
            Debug.LogError($"[Combat] {gameObject.name}: AttackData '{attackData.attackName}' has no effectPrefab assigned! Attack requires an effect prefab with AttackEffect script.");
            return;
        }

        // Check cooldown (per-AttackData, unless the caller owns gating)
        if (!bypassCooldown)
        {
            float lastUse = lastUseTimes.TryGetValue(attackData, out float t) ? t : -999f;
            if (Time.time - lastUse < attackData.cooldown)
            {
                return;
            }
        }

        lastUseTimes[attackData] = Time.time;

        if (debugCombat)
        {
            Debug.Log($"[Combat] {gameObject.name} performing '{attackData.attackName}'");
        }

        // Spawn effect prefab (handles its own collision detection)
        SpawnAttackEffect(attackData, damageOverride, radiusOverride);
    }

    /// <summary>
    /// Spawn the attack effect prefab at the attack point (or character center)
    /// </summary>
    private void SpawnAttackEffect(AttackData attackData, float damageOverride, float radiusOverride)
    {
        Vector3 basePos = attackData.spawnAtCharacterCenter ? transform.position : attackPoint.position;
        Vector3 spawnPos = basePos + transform.TransformDirection(attackData.spawnOffset);
        Quaternion spawnRot = transform.rotation;

        GameObject effectObj = Instantiate(attackData.effectPrefab, spawnPos, spawnRot);

        // Initialize the attack effect
        AttackEffect effect = effectObj.GetComponent<AttackEffect>();
        if (effect == null)
        {
            Debug.LogError($"[Combat] Effect prefab '{attackData.effectPrefab.name}' has no AttackEffect script! Add AttackEffect component to the prefab.");
            Destroy(effectObj);
            return;
        }

        if (radiusOverride > 0f)
        {
            effect.SetRadius(radiusOverride);
        }

        float baseDamage = damageOverride >= 0f ? damageOverride : attackData.damage;

        effect.Initialize(
            baseDamage * damageMultiplier,
            attackData.knockbackForce,
            knockbackMultiplier,
            transform.forward,
            gameObject,
            attackData.hitWindowDuration
        );
        effect.SetTargetTag(targetTag);

        if (debugCombat)
        {
            Debug.Log($"[Combat] Spawned attack effect '{attackData.effectPrefab.name}' at {spawnPos}");
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
    /// Set damage multiplier (for stats/powerups)
    /// </summary>
    public void SetDamageMultiplier(float multiplier)
    {
        damageMultiplier = multiplier;
    }

    public float GetDamageMultiplier()
    {
        return damageMultiplier;
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
        
        // Attack point visualization
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(point.position, 0.15f);
        Gizmos.DrawLine(transform.position, point.position);
    }
}
