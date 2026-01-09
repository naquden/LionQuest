using UnityEngine;

/// <summary>
/// Editor helper script to set up enemy GameObjects with required components.
/// Add this to an enemy prefab, click "Setup Enemy" in context menu, then remove this script.
/// </summary>
public class EnemySetupHelper : MonoBehaviour
{
    [Header("Setup Settings")]
    [SerializeField] private float colliderRadius = 0.5f;
    [SerializeField] private float colliderHeight = 2f;
    
    /// <summary>
    /// Sets up the enemy with all required components.
    /// Call from context menu in editor.
    /// </summary>
    [ContextMenu("Setup Enemy")]
    public void SetupEnemy()
    {
        // Set tag
        if (!gameObject.CompareTag("Enemy"))
        {
            gameObject.tag = "Enemy";
            Debug.Log($"[EnemySetup] Set tag to 'Enemy' on {gameObject.name}");
        }
        
        // Add Rigidbody if missing
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            Debug.Log($"[EnemySetup] Added Rigidbody to {gameObject.name}");
        }
        
        // Configure Rigidbody for top-down game
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // Prevent tipping over
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.linearDamping = 0f; // Ensure drag doesn't kill knockback forces
        // Note: Set mass on the Rigidbody component directly as needed
        Debug.Log($"[EnemySetup] Configured Rigidbody on {gameObject.name}");
        
        // Add Collider if missing
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = colliderRadius;
            capsule.height = colliderHeight;
            capsule.center = new Vector3(0, colliderHeight / 2f, 0);
            Debug.Log($"[EnemySetup] Added CapsuleCollider to {gameObject.name}");
        }
        
        // Add Enemy script if missing
        Enemy enemy = GetComponent<Enemy>();
        if (enemy == null)
        {
            enemy = gameObject.AddComponent<Enemy>();
            Debug.Log($"[EnemySetup] Added Enemy script to {gameObject.name}");
        }
        
        // Add EnemyAnimator if missing and has Animator
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            // Disable Root Motion to allow physics knockback
            animator.applyRootMotion = false;
            Debug.Log($"[EnemySetup] Disabled Root Motion on Animator");

            EnemyAnimator enemyAnimator = GetComponent<EnemyAnimator>();
            if (enemyAnimator == null)
            {
                enemyAnimator = gameObject.AddComponent<EnemyAnimator>();
                Debug.Log($"[EnemySetup] Added EnemyAnimator to {gameObject.name}");
            }
        }
        
        // Add CombatController if missing (it auto-creates AttackPoint)
        CombatController combat = GetComponent<CombatController>();
        if (combat == null)
        {
            combat = gameObject.AddComponent<CombatController>();
            Debug.Log($"[EnemySetup] Added CombatController to {gameObject.name}");
        }
        
        Debug.Log($"[EnemySetup] Setup complete for {gameObject.name}. You can now remove EnemySetupHelper.");
    }
    
    private void OnValidate()
    {
        // Reminder in editor
        if (!Application.isPlaying && !gameObject.CompareTag("Enemy"))
        {
            Debug.LogWarning($"[EnemySetup] '{gameObject.name}' is not tagged as 'Enemy'. Right-click this component and select 'Setup Enemy'.");
        }
    }
}

