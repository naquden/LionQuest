using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-down player controller with 4-directional movement (left, right, up, down)
/// Movement is locked to the X-Z plane (no Y-axis movement)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class TopDownPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    
    [Header("Ground Detection")]
    [Tooltip("MapGenerator that contains the ground type map. If not assigned, will try to find it in scene.")]
    [SerializeField] private MapGenerator mapGenerator;
    
    [Tooltip("Enable debug logging for ground detection")]
    [SerializeField] private bool debugGroundDetection = false;
    
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private bool rotateTowardsMovement = true;
    
    [Header("Ground Settings")]
    [SerializeField] private float groundLevel = 0f; // Y position where the ground is
    
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    
    [Header("Animation (Optional)")]
    [Tooltip("CharacterAnimator component for handling animations. Leave empty if not using animations.")]
    [SerializeField] private CharacterAnimator characterAnimator;
    
    private CharacterController characterController;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction attackAction;
    private InputAction skill1Action;
    private InputAction skill2Action;
    private InputAction skill3Action;
    private Vector3 moveDirection;
    private float currentSpeed;
    private float lockedYPosition;
    private bool isCurrentlyMoving = false;
    private bool isFalling = false;
    private float fallSpeed = 0f;
    private GroundType currentGroundType;
    
    /// <summary>
    /// Returns whether the character is currently moving
    /// </summary>
    public bool IsMoving => isCurrentlyMoving;
    
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        
        // Ensure CharacterController exists and is enabled
        if (characterController == null)
        {
            Debug.LogError($"TopDownPlayerController on '{gameObject.name}': CharacterController component is missing!");
            return;
        }
        
        // Ensure CharacterController is enabled
        characterController.enabled = true;
        
        // Calculate Y position so the bottom of the CharacterController is at ground level
        // Bottom of CharacterController = transform.position.y + center.y - height/2
        // We want: transform.position.y + center.y - height/2 = groundLevel
        // So: transform.position.y = groundLevel - center.y + height/2
        float bottomOffset = characterController.center.y - (characterController.height * 0.5f);
        lockedYPosition = groundLevel - bottomOffset;
        
        // Set the initial position
        Vector3 position = transform.position;
        position.y = lockedYPosition;
        transform.position = position;
        
        // Enable input actions
        if (inputActions != null)
        {
            inputActions.Enable();
            moveAction = inputActions.FindAction("Player/Move");
            sprintAction = inputActions.FindAction("Player/Sprint");
            attackAction = inputActions.FindAction("Player/Attack");
            skill1Action = inputActions.FindAction("Player/Skill1");
            skill2Action = inputActions.FindAction("Player/Skill2");
            skill3Action = inputActions.FindAction("Player/Skill3");
            
            if (moveAction == null)
            {
                Debug.LogError($"TopDownPlayerController: Could not find 'Player/Move' action in InputActions asset '{inputActions.name}'. Please check the action map and action names.");
            }
            else
            {
                moveAction.Enable();
            }
            
            if (sprintAction == null)
            {
                Debug.LogWarning($"TopDownPlayerController: Could not find 'Player/Sprint' action. Sprint functionality will be disabled.");
            }
            else
            {
                sprintAction.Enable();
            }
            
            if (attackAction == null)
            {
                Debug.LogWarning($"TopDownPlayerController: Could not find 'Player/Attack' action. Attack functionality will be disabled.");
            }
            else
            {
                attackAction.Enable();
            }
            
            if (skill1Action == null)
            {
                Debug.LogWarning($"TopDownPlayerController: Could not find 'Player/Skill1' action. Skill1 functionality will be disabled.");
            }
            else
            {
                skill1Action.Enable();
            }
            
            if (skill2Action == null)
            {
                Debug.LogWarning($"TopDownPlayerController: Could not find 'Player/Skill2' action. Skill2 functionality will be disabled.");
            }
            else
            {
                skill2Action.Enable();
            }
            
            if (skill3Action == null)
            {
                Debug.LogWarning($"TopDownPlayerController: Could not find 'Player/Skill3' action. Skill3 functionality will be disabled.");
            }
            else
            {
                skill3Action.Enable();
            }
        }
        else
        {
            Debug.LogError($"TopDownPlayerController on '{gameObject.name}': InputActions asset is not assigned! Please assign it in the inspector.");
        }
        
        // Try to find CharacterAnimator if not assigned
        if (characterAnimator == null)
        {
            characterAnimator = GetComponent<CharacterAnimator>();
        }
        
        // Try to find MapGenerator if not assigned
        if (mapGenerator == null)
        {
            mapGenerator = FindObjectOfType<MapGenerator>();
        }
    }
    
    private void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Enable();
        }
    }
    
    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
        }
    }
    
    private void Update()
    {
        // Ensure CharacterController is still enabled (it might get disabled by other scripts)
        if (characterController != null && !characterController.enabled)
        {
            characterController.enabled = true;
        }
        
        DetectGround();
        HandleMovement();
        HandleAttack();
        HandleSkills();
        LockYPosition();
    }
    
    private void HandleMovement()
    {
        // Check if input actions are properly set up
        if (inputActions == null || moveAction == null)
        {
            return; // Can't move without input
        }
        
        // Check if CharacterController is enabled and GameObject is active
        if (characterController == null || !characterController.enabled || !gameObject.activeInHierarchy)
        {
            return; // Can't move if controller is disabled or GameObject is inactive
        }
        
        // Get input from Input System
        Vector2 input = moveAction.ReadValue<Vector2>();
        
        // Check if sprinting
        bool isSprinting = sprintAction != null && sprintAction.IsPressed();
        
        // Get ground type speed multiplier
        float groundSpeedMultiplier = 1f;
        if (currentGroundType != null)
        {
            groundSpeedMultiplier = currentGroundType.movementSpeedMultiplier;
            
            if (debugGroundDetection && isCurrentlyMoving)
            {
                Debug.Log($"Ground Type: {currentGroundType.groundName}, Speed Multiplier: {groundSpeedMultiplier}");
            }
            
            // Check if player is on a hole and should fall through
            if (currentGroundType.isHole && !isFalling)
            {
                // Start falling through hole
                isFalling = true;
                fallSpeed = 0f;
                fallSpeed = currentGroundType.fallSpeedMultiplier;
            }
        }
        else
        {
            // If no ground type detected, use default speed (no multiplier)
            groundSpeedMultiplier = 1f;
            if (debugGroundDetection)
            {
                Debug.LogWarning("No ground type detected! Make sure MapGenerator has generated the map and is in the scene.");
            }
        }
        
        // Handle falling through holes
        if (isFalling)
        {
            // Apply gravity/falling
            fallSpeed += Physics.gravity.magnitude * Time.deltaTime;
            characterController.Move(Vector3.down * fallSpeed * Time.deltaTime);
            
            // Check if we've fallen below a certain threshold (could respawn or trigger death)
            if (transform.position.y < groundLevel - 50f)
            {
                // Player has fallen too far - could respawn or trigger death
                // For now, stop falling and reset position
                isFalling = false;
                Vector3 pos = transform.position;
                pos.y = lockedYPosition;
                transform.position = pos;
            }
            
            // Don't allow normal movement while falling
            return;
        }
        
        currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f) * groundSpeedMultiplier;
        
        // Convert 2D input to 3D movement on X-Z plane
        moveDirection = new Vector3(input.x, 0f, input.y);
        
        // Normalize to prevent faster diagonal movement
        if (moveDirection.magnitude > 1f)
        {
            moveDirection.Normalize();
        }
        
        // Apply movement
        isCurrentlyMoving = moveDirection.magnitude > 0.1f;
        
        if (isCurrentlyMoving)
        {
            // Double-check CharacterController is still enabled before moving
            if (characterController != null && characterController.enabled)
            {
                // Move the character
                characterController.Move(moveDirection * currentSpeed * Time.deltaTime);
                
                // Rotate towards movement direction
                if (rotateTowardsMovement)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
            else
            {
                Debug.LogWarning($"TopDownPlayerController: CharacterController is disabled or null, cannot move. GameObject active: {gameObject.activeInHierarchy}, Controller enabled: {(characterController != null ? characterController.enabled.ToString() : "null")}");
            }
        }
        
        // Update animations (Unity 6 standard: Speed parameter for smooth blending)
        if (characterAnimator != null)
        {
            // Set speed to 1.0 when moving, 0.0 when idle (Unity 6 standard)
            // This allows smooth blending between Idle and Walk states
            float normalizedSpeed = isCurrentlyMoving ? 1.0f : 0.0f;
            characterAnimator.UpdateAnimations(isCurrentlyMoving, normalizedSpeed, isSprinting);
        }
    }
    
    private void HandleAttack()
    {
        // Check if attack button was pressed this frame
        if (attackAction != null && attackAction.WasPressedThisFrame())
        {
            // Trigger attack animation
            if (characterAnimator != null)
            {
                characterAnimator.TriggerAttack(0); // Default to light attack (type 0)
            }
        }
    }
    
    private void HandleSkills()
    {
        // Check if skill buttons were pressed this frame
        if (skill1Action != null && skill1Action.WasPressedThisFrame())
        {
            if (characterAnimator != null)
            {
                characterAnimator.TriggerSkill1();
            }
        }
        
        if (skill2Action != null && skill2Action.WasPressedThisFrame())
        {
            if (characterAnimator != null)
            {
                characterAnimator.TriggerSkill2();
            }
        }
        
        if (skill3Action != null && skill3Action.WasPressedThisFrame())
        {
            if (characterAnimator != null)
            {
                characterAnimator.TriggerSkill3();
            }
        }
    }
    
    private void LockYPosition()
    {
        // Constantly lock the Y position to prevent any vertical movement
        // Only do this if the GameObject and CharacterController are active
        // Don't lock Y if player is falling through a hole
        if (characterController != null && characterController.enabled && gameObject.activeInHierarchy && !isFalling)
        {
            Vector3 position = transform.position;
            position.y = lockedYPosition;
            transform.position = position;
        }
        
        // Reset falling state if we're back on solid ground
        if (isFalling && currentGroundType != null && !currentGroundType.isHole)
        {
            // Check if we're back at ground level
            if (Mathf.Abs(transform.position.y - lockedYPosition) < 0.5f)
            {
                isFalling = false;
                fallSpeed = 0f;
            }
        }
    }
    
    /// <summary>
    /// Detects the ground type at the player's position
    /// </summary>
    private void DetectGround()
    {
        // Try to find MapGenerator if not assigned
        if (mapGenerator == null)
        {
            mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator == null)
            {
                currentGroundType = null;
                return;
            }
        }
        
        // Get ground type from MapGenerator
        currentGroundType = mapGenerator.GetGroundTypeAtPosition(transform.position);
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw movement direction in editor
        if (Application.isPlaying && moveDirection.magnitude > 0.1f)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, moveDirection * 2f);
        }
        
        // Draw ground type indicator
        if (currentGroundType != null)
        {
            Gizmos.color = currentGroundType.biomeColor;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}

