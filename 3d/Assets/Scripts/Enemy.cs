using UnityEngine;

/// <summary>
/// Main enemy script handling health, movement toward players, and knockback.
/// Uses Rigidbody forces for movement - configure mass and drag on the Rigidbody component.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stoppingDistance = 1.5f;
    
    [Header("Detection")]
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private string playerTag = "Player";
    
    [Header("Debug")]
    [SerializeField] private bool debugEnemy = false;
    
    // Components
    private Rigidbody rb;
    private EnemyAnimator enemyAnimator;
    
    // State
    private Transform targetPlayer;
    private bool isAlive = true;
    private bool isKnockedBack = false;
    private float knockbackEndTime;
    
    // Events
    public System.Action<float, float> OnHealthChanged; // current, max
    public System.Action OnDeath;
    
    // Properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsAlive => isAlive;
    public bool IsMoving => rb.linearVelocity.magnitude > 0.1f;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        enemyAnimator = GetComponent<EnemyAnimator>();
        currentHealth = maxHealth;
        
        // Configure Rigidbody for smooth physics movement
        ConfigureRigidbody();
    }
    
    private void Start()
    {
        // Find players in scene
        FindNearestPlayer();
        
        if (targetPlayer == null && debugEnemy)
        {
            Debug.LogWarning($"[Enemy] {gameObject.name}: No player found with tag '{playerTag}' in scene.");
        }
    }
    
    /// <summary>
    /// Configure Rigidbody for proper physics-based movement and knockback
    /// </summary>
    private void ConfigureRigidbody()
    {
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Smooth visual movement
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // Prevent tipping
        
        // Set reasonable defaults if not configured
        // Mass and drag should be set in Inspector, but ensure sane defaults
        if (rb.mass <= 0.01f) rb.mass = 1f;
        if (rb.linearDamping < 0.1f) rb.linearDamping = 5f; // Drag for gradual slowdown
    }
    
    private void Update()
    {
        if (!isAlive) return;
        
        // Check if knockback has ended
        if (isKnockedBack && Time.time >= knockbackEndTime)
        {
            isKnockedBack = false;
        }
        
        // Periodically re-find nearest player
        if (Time.frameCount % 30 == 0) // Every ~0.5 seconds at 60fps
        {
            FindNearestPlayer();
        }
    }
    
    private void FixedUpdate()
    {
        if (!isAlive) return;
        if (isKnockedBack) return; // Don't move while being knocked back
        
        MoveTowardPlayer();
    }
    
    /// <summary>
    /// Find the nearest player in the scene
    /// </summary>
    private void FindNearestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);
        
        if (players.Length == 0)
        {
            targetPlayer = null;
            return;
        }
        
        float nearestDistance = float.MaxValue;
        Transform nearestPlayer = null;
        
        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < nearestDistance && distance <= detectionRange)
            {
                nearestDistance = distance;
                nearestPlayer = player.transform;
            }
        }
        
        targetPlayer = nearestPlayer;
    }
    
    /// <summary>
    /// Move toward the target player using Rigidbody forces
    /// </summary>
    private void MoveTowardPlayer()
    {
        if (targetPlayer == null)
        {
            // No target - stop moving
            enemyAnimator?.SetMoving(false);
            return;
        }
        
        Vector3 directionToPlayer = targetPlayer.position - transform.position;
        directionToPlayer.y = 0f; // Keep movement horizontal
        float distanceToPlayer = directionToPlayer.magnitude;
        
        if (distanceToPlayer <= stoppingDistance)
        {
            // Close enough - stop and maybe attack
            enemyAnimator?.SetMoving(false);
            
            // Face the player
            if (directionToPlayer.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(directionToPlayer.normalized);
            }
            return;
        }
        
        // Move toward player
        Vector3 moveDirection = directionToPlayer.normalized;
        Vector3 targetVelocity = moveDirection * moveSpeed;
        
        // Apply force to reach target velocity (let Rigidbody drag handle deceleration)
        Vector3 velocityDiff = targetVelocity - new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(velocityDiff, ForceMode.VelocityChange);
        
        // Face movement direction
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }
        
        enemyAnimator?.SetMoving(true);
        
        if (debugEnemy)
        {
            Debug.Log($"[Enemy] Moving toward {targetPlayer.name}, distance: {distanceToPlayer:F1}");
        }
    }
    
    /// <summary>
    /// Take damage and apply knockback force.
    /// Called by player's combat system when hitting the enemy.
    /// </summary>
    /// <param name="damage">Amount of damage to take</param>
    /// <param name="knockbackDirection">Direction to knock back (from attacker to this enemy)</param>
    /// <param name="knockbackForce">Force magnitude of knockback</param>
    public void TakeDamage(float damage, Vector3 knockbackDirection, float knockbackForce)
    {
        if (!isAlive) return;
        
        // Apply damage
        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        if (debugEnemy)
        {
            Debug.Log($"[Enemy] {gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
        }
        
        // Apply knockback (horizontal only for top-down game)
        if (knockbackForce > 0f)
        {
            // DEBUG: Check Rigidbody state
            Debug.Log($"[Enemy] PRE-KNOCKBACK: isKinematic={rb.isKinematic}, constraints={rb.constraints}, mass={rb.mass}, drag={rb.linearDamping}");
            Debug.Log($"[Enemy] Has NavMeshAgent: {GetComponent<UnityEngine.AI.NavMeshAgent>() != null}, Has CharacterController: {GetComponent<CharacterController>() != null}");
            
            Vector3 horizontalKnockback = new Vector3(knockbackDirection.x, 0f, knockbackDirection.z).normalized;
            Vector3 force = horizontalKnockback * knockbackForce;
            
            // Apply knockback force as impulse
            rb.AddForce(force, ForceMode.Impulse);
            
            // Pause movement AI during knockback
            isKnockedBack = true;
            knockbackEndTime = Time.time + 0.5f;
            
            Debug.Log($"[Enemy] AddForce called with: {force} (ForceMode.Impulse)");
            
            // Log position after physics processes the force
            StartCoroutine(LogPositionAfterDelay());
        }
        
        // Trigger hit animation
        enemyAnimator?.TriggerHit();
        
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
    
    /// <summary>
    /// Debug coroutine to check if position changes after knockback
    /// </summary>
    private System.Collections.IEnumerator LogPositionAfterDelay()
    {
        Vector3 startPos = transform.position;
        yield return new WaitForFixedUpdate();
        Debug.Log($"[Enemy] After 1 physics frame: pos={transform.position}, velocity={rb.linearVelocity}");
        yield return new WaitForSeconds(0.1f);
        Debug.Log($"[Enemy] After 0.1s: pos={transform.position}, moved={Vector3.Distance(startPos, transform.position):F2} units");
    }
    
    /// <summary>
    /// Handle enemy death
    /// </summary>
    private void Die()
    {
        if (!isAlive) return;
        
        isAlive = false;
        
        if (debugEnemy)
        {
            Debug.Log($"[Enemy] {gameObject.name} died!");
        }
        
        OnDeath?.Invoke();
        enemyAnimator?.TriggerDeath();
        
        // Disable physics
        rb.isKinematic = true;
        
        // Disable collider so player can walk through
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
    }
    
    /// <summary>
    /// Heal the enemy
    /// </summary>
    public void Heal(float amount)
    {
        if (!isAlive) return;
        
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    private void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Stopping distance
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);
        
        // Line to target
        if (targetPlayer != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, targetPlayer.position);
        }
    }
}

