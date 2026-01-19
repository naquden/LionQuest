using UnityEngine;

/// <summary>
/// Simple enemy script with health and knockback.
/// Just like Hittable but with health tracking.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    
    [Header("Movement")]
    [SerializeField] private float pullForce = 5f;
    [SerializeField] private float pullRange = 15f;
    [SerializeField] private float stopDistance = 1.5f; // Stop when close to attack
    [SerializeField] private float maxPullVelocity = 3f;
    [SerializeField] private string playerTag = "Player";
    
    [Header("Combat")]
    [SerializeField] private AttackData attackData;
    [SerializeField] private float attackCooldown = 2f;
    private float lastAttackTime;

    private Rigidbody rb;
    private bool isAlive = true;
    private Transform targetPlayer;
    private EnemyAnimator enemyAnimator;
    private CombatController combatController;

    // Events
    public System.Action<float, float> OnHealthChanged; // current, max
    public System.Action OnDeath;
    
    // Properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsAlive => isAlive;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        currentHealth = maxHealth;
        
        enemyAnimator = GetComponent<EnemyAnimator>();
        combatController = GetComponent<CombatController>();

        // Ensure Root Motion is off, as it overrides physics
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
        }
    }
    
    private void Start()
    {
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            targetPlayer = playerObj.transform;
        }
    }
    
    private void Update()
    {
        if (!isAlive || targetPlayer == null) return;

        float distance = Vector3.Distance(transform.position, targetPlayer.position);
        
        // Attack logic
        if (distance <= stopDistance && Time.time >= lastAttackTime + attackCooldown)
        {
            Attack();
        }
    }

    private void Attack()
    {
        lastAttackTime = Time.time;
        
        if (enemyAnimator != null) enemyAnimator.TriggerAttack();
        
        if (combatController != null && attackData != null)
        {
            combatController.PerformAttack(attackData);
        }
    }

    private void FixedUpdate()
    {
        if (!isAlive) return;
        
        // Find player if we don't have one
        if (targetPlayer == null)
        {
             GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
             if (playerObj != null) 
             {
                 targetPlayer = playerObj.transform;
             }
             else
             {
                 return; // No player found yet
             }
        }
        
        Vector3 directionToPlayer = targetPlayer.position - transform.position;
        directionToPlayer.y = 0f; // Horizontal only
        float distance = directionToPlayer.magnitude;
        
        // Move towards player if within range but outside stop distance
        if (distance <= pullRange && distance > stopDistance)
        {
            Vector3 pullDir = directionToPlayer.normalized;
            
            // Calculate current velocity towards player
            float currentVelocityTowardsPlayer = Vector3.Dot(rb.linearVelocity, pullDir);
            
            // Only add force if below max velocity
            if (currentVelocityTowardsPlayer < maxPullVelocity)
            {
                rb.AddForce(pullDir * pullForce, ForceMode.Force);
            }
            
            // Rotate to face player
            if (directionToPlayer.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(directionToPlayer.normalized);
            }
        }
    }
    
    /// <summary>
    /// Take damage and apply knockback force.
    /// </summary>
    public void TakeDamage(float damage, Vector3 knockbackDirection, float knockbackForce)
    {
        if (!isAlive) return;
        
        // Apply damage
        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        Debug.Log($"[Enemy] {gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
        
        // Apply knockback (horizontal only) - same as Hittable
        if (knockbackForce > 0f && rb != null)
        {
            Vector3 force = new Vector3(knockbackDirection.x, 0f, knockbackDirection.z).normalized * knockbackForce;
            rb.AddForce(force, ForceMode.Impulse);
            
            // Debug.Log($"[Enemy] Knockback force: {force} | Mass: {rb.mass} | Drag: {rb.linearDamping} | IsKinematic: {rb.isKinematic}");
        }

        // Trigger hit animation
        if (enemyAnimator != null) enemyAnimator.TriggerHit();
        
        // Check for death
        if (currentHealth <= 0f)
        {
            Die();
        }
    }
    
    /// <summary>
    /// Take damage without knockback
    /// </summary>
    public void TakeDamage(float damage)
    {
        TakeDamage(damage, Vector3.zero, 0f);
    }
    
    private void Die()
    {
        if (!isAlive) return;
        
        isAlive = false;
        Debug.Log($"[Enemy] {gameObject.name} died!");
        
        OnDeath?.Invoke();

        // Trigger animation
        if (enemyAnimator != null) enemyAnimator.TriggerDeath();
        
        // Notify GameController about the kill
        // It will handle rewarding all active players
        if (GameSaveController.Instance != null)
        {
            // Debug.Log($"[Enemy] Notifying GameController about kill: {gameObject.name}");
            GameSaveController.Instance.OnEnemyKilled(this);
        }
        else
        {
            // Fallback for testing/debugging
            Debug.LogWarning("[Enemy] GameSaveController not found! No rewards given.");
        }
        
        // Drop Loot
        LootDropper lootDropper = GetComponent<LootDropper>();
        if (lootDropper != null)
        {
            lootDropper.DropLoot();
        }
        
        // Disable physics
        rb.isKinematic = true;
        
        // Disable collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
    }
}
