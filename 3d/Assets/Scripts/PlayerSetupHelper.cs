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
    
    [Header("Attack Point Settings")]
    [SerializeField] private float attackPointDistance = 1f;
    [SerializeField] private float attackPointHeight = 1f;
    
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
        
        // Create or find AttackPoint child GameObject
        Transform attackPoint = transform.Find("AttackPoint");
        if (attackPoint == null)
        {
            GameObject attackPointObj = new GameObject("AttackPoint");
            attackPointObj.transform.SetParent(transform);
            attackPointObj.transform.localPosition = new Vector3(0f, attackPointHeight, attackPointDistance);
            attackPointObj.transform.localRotation = Quaternion.identity;
            attackPoint = attackPointObj.transform;
            Debug.Log($"[PlayerSetup] Created AttackPoint at local position {attackPointObj.transform.localPosition}");
        }
        else
        {
            Debug.Log($"[PlayerSetup] AttackPoint already exists");
        }
        
        // Add TopDownPlayerController if missing
        TopDownPlayerController playerController = GetComponent<TopDownPlayerController>();
        if (playerController == null)
        {
            playerController = gameObject.AddComponent<TopDownPlayerController>();
            Debug.Log($"[PlayerSetup] Added TopDownPlayerController");
        }
        
        // Add CombatController if missing
        CombatController combatController = GetComponent<CombatController>();
        if (combatController == null)
        {
            combatController = gameObject.AddComponent<CombatController>();
            Debug.Log($"[PlayerSetup] Added CombatController");
        }
        
        // Wire up AttackPoint reference in CombatController
        combatController.SetAttackPoint(attackPoint);
        Debug.Log($"[PlayerSetup] Assigned AttackPoint to CombatController");
        
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
        // Visualize attack point position
        Vector3 attackPointPos = transform.position + transform.forward * attackPointDistance + Vector3.up * attackPointHeight;
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPointPos, 0.2f);
        Gizmos.DrawLine(transform.position + Vector3.up * attackPointHeight, attackPointPos);
    }
}

