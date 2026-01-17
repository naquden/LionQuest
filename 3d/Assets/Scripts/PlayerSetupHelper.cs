using UnityEngine;

/// <summary>
/// Editor helper script to set up player GameObjects with required components.
/// Add this to a player prefab, click "Setup Player" in context menu, then remove this script.
/// </summary>
public class PlayerSetupHelper : MonoBehaviour
{
    [Header("Collider Settings")]
    [SerializeField] private float colliderRadius = 0.5f;
    [SerializeField] private float colliderHeight = 2f;
    
    [Header("Optional Prefabs")]
    [Tooltip("Health Bar prefab (World Space Canvas) to instantiate")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0, 2.2f, 0);
    
    /// <summary>
    /// Sets up the player with all required components.
    /// Call from context menu in editor.
    /// </summary>
    [ContextMenu("Setup Player")]
    public void SetupPlayer()
    {
        Debug.Log($"[PlayerSetup] Setting up player: {gameObject.name}");
        
        // Set tag
        if (!gameObject.CompareTag("Player"))
        {
            gameObject.tag = "Player";
            Debug.Log($"[PlayerSetup] Set tag to 'Player'");
        }
        
        // Add Rigidbody if missing
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            Debug.Log($"[PlayerSetup] Added Rigidbody");
        }
        
        // Configure Rigidbody for top-down game
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        Debug.Log($"[PlayerSetup] Configured Rigidbody");
        
        // Add CapsuleCollider if missing
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = colliderRadius;
            capsule.height = colliderHeight;
            capsule.center = new Vector3(0, colliderHeight / 2f, 0);
            Debug.Log($"[PlayerSetup] Added CapsuleCollider");
        }
        
        // Add TopDownPlayerController if missing
        TopDownPlayerController playerController = GetComponent<TopDownPlayerController>();
        if (playerController == null)
        {
            playerController = gameObject.AddComponent<TopDownPlayerController>();
            Debug.Log($"[PlayerSetup] Added TopDownPlayerController");
        }
        
        // Add CombatController if missing (it auto-creates AttackPoint)
        CombatController combatController = GetComponent<CombatController>();
        if (combatController == null)
        {
            combatController = gameObject.AddComponent<CombatController>();
            Debug.Log($"[PlayerSetup] Added CombatController");
        }
        
        // Instantiate Health Bar if provided
        if (healthBarPrefab != null)
        {
            // Remove existing health bar if found
            Transform existingHealthBar = transform.Find("HealthBar");
            if (existingHealthBar != null)
            {
                DestroyImmediate(existingHealthBar.gameObject);
                Debug.Log($"[PlayerSetup] Removed existing Health Bar");
            }

            GameObject healthBar = Instantiate(healthBarPrefab, transform);
            healthBar.name = "HealthBar";
            healthBar.transform.localPosition = healthBarOffset;
            Debug.Log($"[PlayerSetup] Instantiated new Health Bar prefab");
        }
        
        // Add CharacterAnimator if has Animator
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            CharacterAnimator charAnimator = GetComponent<CharacterAnimator>();
            if (charAnimator == null)
            {
                charAnimator = gameObject.AddComponent<CharacterAnimator>();
                Debug.Log($"[PlayerSetup] Added CharacterAnimator");
            }
        }
        
        // Add CharacterStats (Progression)
        CharacterStats stats = GetComponent<CharacterStats>();
        if (stats == null)
        {
            stats = gameObject.AddComponent<CharacterStats>();
            // Default ID based on name or random
            stats.characterID = gameObject.name;
            Debug.Log($"[PlayerSetup] Added CharacterStats (ID: {stats.characterID})");
        }
        
        Debug.Log($"[PlayerSetup] Setup complete for {gameObject.name}");
        Debug.Log($"[PlayerSetup] IMPORTANT: Assign InputActions asset to TopDownPlayerController in Inspector!");
        Debug.Log($"[PlayerSetup] IMPORTANT: Create and assign AttackData ScriptableObjects for attacks!");
        Debug.Log($"[PlayerSetup] You can now remove PlayerSetupHelper from this GameObject.");
    }
    
    private void OnValidate()
    {
        if (!Application.isPlaying && !gameObject.CompareTag("Player"))
        {
            Debug.LogWarning($"[PlayerSetup] '{gameObject.name}' is not tagged as 'Player'. Right-click this component and select 'Setup Player'.");
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Visualize attack point position (default values from CombatController)
        float attackPointDistance = 0.8f;
        float attackPointHeight = 1f;
        
        Vector3 attackPointPos = transform.position + transform.forward * attackPointDistance + Vector3.up * attackPointHeight;
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPointPos, 0.2f);
        Gizmos.DrawLine(transform.position + Vector3.up * attackPointHeight, attackPointPos);
    }
}

