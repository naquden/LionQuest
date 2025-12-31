using UnityEngine;
using UnityEngine.InputSystem;

namespace LionQuest.Character
{
    /// <summary>
    /// Simple character controller using force-based movement with attack functionality.
    /// Uses Unity's new Input System (works with WASD and arrow keys).
    /// Integrates with SPUM_Prefabs for animations.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SimpleCharacterController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveForce = 10f;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float drag = 5f;
        
        [Header("Attack Settings")]
        [SerializeField] private Key attackKey = Key.Space;
        [SerializeField] private float attackCooldown = 0.5f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        private Rigidbody2D rb;
        private SPUM_Prefabs spumPrefabs;
        private Vector2 movement;
        private Keyboard keyboard;
        private float lastAttackTime = 0f;
        private bool isAttacking = false;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spumPrefabs = GetComponent<SPUM_Prefabs>();
            
            if (rb == null)
            {
                Debug.LogError("SimpleCharacterController: Rigidbody2D component not found!");
                return;
            }
            
            // Set Rigidbody2D settings for 2D character (no gravity for top-down)
            rb.gravityScale = 0;
            rb.freezeRotation = true;
            rb.linearDamping = drag;
            
            // Ensure Rigidbody2D is not kinematic
            if (rb.bodyType == RigidbodyType2D.Kinematic)
            {
                Debug.LogWarning("SimpleCharacterController: Rigidbody2D is set to Kinematic! Changing to Dynamic.");
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
            
            // Wake up the rigidbody
            rb.WakeUp();
            
            // Get keyboard reference
            keyboard = Keyboard.current;
            
            // Check for SPUM_Prefabs
            if (spumPrefabs == null)
            {
                Debug.LogWarning("SimpleCharacterController: No SPUM_Prefabs component found! Attack animations will not work.");
            }
            else
            {
                // Ensure SPUM_Prefabs is initialized
                if (spumPrefabs.StateAnimationPairs == null || spumPrefabs.StateAnimationPairs.Count == 0)
                {
                    Debug.LogWarning("SimpleCharacterController: SPUM_Prefabs StateAnimationPairs not initialized. Calling OverrideControllerInit().");
                    spumPrefabs.OverrideControllerInit();
                }
                
                // Ensure animation lists are populated
                if (spumPrefabs.ATTACK_List == null || spumPrefabs.ATTACK_List.Count == 0)
                {
                    Debug.LogWarning("SimpleCharacterController: SPUM_Prefabs ATTACK_List is empty. Make sure to populate animation lists in SPUM_Prefabs.");
                }
            }
        }
        
        private void Update()
        {
            // Get input from new Input System
            movement = Vector2.zero;
            
            if (keyboard != null)
            {
                // Horizontal movement (A/D or Left/Right arrows)
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    movement.x -= 1f;
                }
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    movement.x += 1f;
                }
                
                // Vertical movement (W/S or Up/Down arrows)
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    movement.y += 1f;
                }
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    movement.y -= 1f;
                }
                
                // Attack input (configurable key)
                if (keyboard[attackKey].wasPressedThisFrame && !isAttacking)
                {
                    Attack();
                }
            }
            
            movement.Normalize();
            
            if (showDebugInfo && movement.magnitude > 0.1f)
            {
                Debug.Log($"Input detected: {movement}, Magnitude: {movement.magnitude}");
            }
        }
        
        private void Attack()
        {
            // Check cooldown
            if (Time.time - lastAttackTime < attackCooldown)
            {
                return;
            }
            
            // Check if SPUM_Prefabs exists
            if (spumPrefabs == null)
            {
                Debug.LogWarning("SimpleCharacterController: No SPUM_Prefabs component found!");
                return;
            }
            
            // Ensure StateAnimationPairs is initialized
            if (spumPrefabs.StateAnimationPairs == null || spumPrefabs.StateAnimationPairs.Count == 0)
            {
                Debug.LogWarning("SimpleCharacterController: SPUM_Prefabs StateAnimationPairs not initialized. Initializing now...");
                spumPrefabs.OverrideControllerInit();
            }
            
            // Check if ATTACK key exists in StateAnimationPairs
            if (!spumPrefabs.StateAnimationPairs.ContainsKey("ATTACK"))
            {
                Debug.LogError("SimpleCharacterController: 'ATTACK' key not found in SPUM_Prefabs StateAnimationPairs. Make sure OverrideControllerInit() has been called and ATTACK_List has animations.");
                return;
            }
            
            // Check if attack animations are available
            if (spumPrefabs.ATTACK_List == null || spumPrefabs.ATTACK_List.Count == 0)
            {
                Debug.LogWarning("SimpleCharacterController: No attack animations in SPUM_Prefabs ATTACK_List!");
                return;
            }
            
            // Play the first attack animation using SPUM's PlayAnimation method
            // Index 0 = first animation in the list
            try
            {
                spumPrefabs.PlayAnimation(PlayerState.ATTACK, 0);
                
                isAttacking = true;
                lastAttackTime = Time.time;
                
                if (showDebugInfo)
                {
                    Debug.Log($"Attack triggered: Playing first animation from ATTACK_List");
                }
                
                // Reset attack flag after animation duration
                // Get the duration of the first attack animation
                float attackDuration = spumPrefabs.ATTACK_List[0] != null ? spumPrefabs.ATTACK_List[0].length : 1f;
                Invoke(nameof(ResetAttack), attackDuration);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SimpleCharacterController: Error playing attack animation: {e.Message}");
            }
        }
        
        private void ResetAttack()
        {
            isAttacking = false;
        }
        
        private void FixedUpdate()
        {
            if (rb == null) return;
            
            // Don't apply movement force while attacking (optional - remove if you want to move while attacking)
            if (isAttacking)
            {
                // Apply drag to slow down during attack
                if (rb.linearVelocity.magnitude > 0.01f)
                {
                    rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * drag * 2f);
                }
                return;
            }
            
            // Apply force for movement
            if (movement.magnitude > 0.1f)
            {
                // Calculate desired velocity
                Vector2 desiredVelocity = movement * maxSpeed;
                
                // Calculate the force needed to reach desired velocity
                Vector2 velocityChange = desiredVelocity - rb.linearVelocity;
                Vector2 force = velocityChange * moveForce;
                
                // Apply force
                rb.AddForce(force, ForceMode2D.Force);
                
                if (showDebugInfo)
                {
                    Debug.Log($"Applying force: {force}, Current velocity: {rb.linearVelocity}, Desired: {desiredVelocity}");
                }
            }
            else
            {
                // Apply drag when not moving (helps stop the character)
                if (rb.linearVelocity.magnitude > 0.01f)
                {
                    rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * drag);
                }
            }
        }
        
        private void OnValidate()
        {
            // Ensure values are positive
            if (moveForce < 0) moveForce = 10f;
            if (maxSpeed < 0) maxSpeed = 5f;
            if (drag < 0) drag = 5f;
            if (attackCooldown < 0) attackCooldown = 0.5f;
        }
    }
}
