using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-down player controller with 4-directional movement using Rigidbody forces
/// Uses physics-based movement for interaction with environment forces
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class TopDownPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [Tooltip("Force applied for movement (higher = faster acceleration)")]
    [SerializeField] private float moveForce = 50f;
    [Tooltip("Force applied to stop movement (higher = stops faster)")]
    [SerializeField] private float stopForce = 200f;
    [Tooltip("Drag to slow down movement (higher = stops faster)")]
    [SerializeField] private float drag = 5f;
    
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
    [Tooltip("Distance to check if player is on ground")]
    [SerializeField] private float groundCheckDistance = 0.1f;
    [Tooltip("Layer mask for ground detection")]
    [SerializeField] private LayerMask groundLayerMask = -1;
    
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    
    [Header("Animation (Optional)")]
    [Tooltip("CharacterAnimator component for handling animations. Leave empty if not using animations.")]
    [SerializeField] private CharacterAnimator characterAnimator;
    
    private Rigidbody rb;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction attackAction;
    private InputAction skill1Action;
    private InputAction skill2Action;
    private InputAction skill3Action;
    private Vector3 moveDirection;
    private float currentSpeed;
    private bool isCurrentlyMoving = false;
    private bool isFalling = false;
    private bool isGrounded = false;
    private GroundType currentGroundType;
    
    /// <summary>
    /// Returns whether the character is currently moving
    /// </summary>
    public bool IsMoving => isCurrentlyMoving;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Ensure Rigidbody exists
        if (rb == null)
        {
            Debug.LogError($"TopDownPlayerController on '{gameObject.name}': Rigidbody component is missing!");
            return;
        }
        
        // Ensure Collider exists
        CapsuleCollider collider = GetComponent<CapsuleCollider>();
        if (collider == null)
        {
            Debug.LogWarning($"TopDownPlayerController: Adding CapsuleCollider for physics collision...");
            collider = gameObject.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.5f;
            collider.center = new Vector3(0, 1f, 0); // Center at half height
        }
        
        // Configure Rigidbody for top-down movement (Unity 6 best practices)
        rb.useGravity = true;
        rb.linearDamping = drag; // Unity 6: Use linearDamping instead of drag
        rb.angularDamping = 0f; // Prevent rotation from forces
        rb.freezeRotation = true; // Lock rotation on all axes (prevents unwanted rotation)
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Smooth visual movement between physics updates
        
        // Unity 6 best practice: Use Continuous collision detection for fast-moving characters
        // Prevents "tunneling" through colliders at high speeds
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // Set initial position at ground level
        Vector3 position = transform.position;
        position.y = groundLevel;
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
                // Debug: Test input reading
                Debug.Log($"TopDownPlayerController: Move action found and enabled. Action type: {moveAction.type}, Expected control type: {moveAction.expectedControlType}");
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
        DetectGround();
        CheckGrounded();
        HandleMovement();
        HandleAttack();
        HandleSkills();
    }
    
    private void FixedUpdate()
    {
        // Apply movement forces in FixedUpdate for physics
        ApplyMovementForces();
    }
    
    private void HandleMovement()
    {
        // Check if input actions are properly set up
        if (inputActions == null || moveAction == null)
        {
            return; // Can't move without input
        }
        
        // Check if Rigidbody exists and GameObject is active
        if (rb == null || !gameObject.activeInHierarchy)
        {
            return; // Can't move if rigidbody is missing or GameObject is inactive
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
            if (currentGroundType.isHole && isGrounded && !isFalling)
            {
                // Start falling through hole - disable ground collision temporarily
                isFalling = true;
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
        
        // Calculate movement speed
        currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f) * groundSpeedMultiplier;
        
        // Convert 2D input to 3D movement on X-Z plane
        // Reset moveDirection first to ensure clean state
        moveDirection = Vector3.zero;
        
        // Only set direction if there's actual input
        if (input.magnitude > 0.1f)
        {
            // Convert 2D input (X, Y) to 3D movement (X, 0, Z)
            // input.x = horizontal (left/right)
            // input.y = vertical (forward/back)
            moveDirection = new Vector3(input.x, 0f, input.y);
            
            // Normalize to prevent faster diagonal movement
            moveDirection.Normalize();
        }
        
        // Check if moving
        isCurrentlyMoving = moveDirection.magnitude > 0.1f;
        
        // Debug: Log movement direction
        if (debugGroundDetection)
        {
            if (isCurrentlyMoving)
            {
                Debug.Log($"Move Direction: {moveDirection}, Input: {input}, Current Speed: {currentSpeed}");
            }
            else if (input.magnitude > 0.01f)
            {
                Debug.LogWarning($"Input detected ({input}) but moveDirection is zero! Magnitude: {input.magnitude}");
            }
        }
        
        // Rotate towards movement direction
        if (isCurrentlyMoving && rotateTowardsMovement)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
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
    
    /// <summary>
    /// Applies movement forces to the Rigidbody (called in FixedUpdate)
    /// Simple force-based movement: apply force in movement direction when input, stop force when no input
    /// </summary>
    private void ApplyMovementForces()
    {
        if (rb == null)
        {
            return;
        }
        
        // Get current horizontal velocity (X-Z plane only, preserve Y for gravity)
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        
        // Debug: Check if Y velocity is being affected
        if (debugGroundDetection && isCurrentlyMoving && Mathf.Abs(currentVelocity.y) > 0.1f)
        {
            Debug.LogWarning($"Y velocity detected during movement: {currentVelocity.y}. This might be from terrain collision or other forces.");
        }
        
        if (isCurrentlyMoving && moveDirection.magnitude > 0.01f)
        {
            // Apply force in the movement direction, scaled by current speed
            // This ensures the force respects ground type multipliers and sprint
            Vector3 force = moveDirection * moveForce * (currentSpeed / moveSpeed);
            
            // Debug: Log force being applied
            if (debugGroundDetection)
            {
                Debug.Log($"Applying Force: {force}, MoveDirection: {moveDirection}, MoveForce: {moveForce}, CurrentSpeed: {currentSpeed}, SpeedRatio: {currentSpeed / moveSpeed}");
            }
            
            rb.AddForce(force, ForceMode.Force);
        }
        else
        {
            // No input - stop horizontal movement
            // Use drag to slow down naturally, but also directly zero small velocities
            if (horizontalVelocity.magnitude > 0.1f)
            {
                // Apply stopping force to bring velocity to zero
                Vector3 stopDirection = -horizontalVelocity.normalized;
                Vector3 stoppingForce = stopDirection * stopForce;
                rb.AddForce(stoppingForce, ForceMode.Force);
            }
            else if (horizontalVelocity.magnitude > 0.01f)
            {
                // For very small velocities, directly zero them to prevent jitter
                Vector3 vel = rb.linearVelocity;
                vel.x = 0f;
                vel.z = 0f;
                rb.linearVelocity = vel;
            }
        }
    }
    
    /// <summary>
    /// Checks if the player is grounded using raycast
    /// </summary>
    private void CheckGrounded()
    {
        // Raycast downward to check if on ground
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance + 0.1f, groundLayerMask);
        
        // Reset falling state if back on ground
        if (isGrounded && isFalling)
        {
            isFalling = false;
        }
        
        // Check if fallen too far (respawn logic)
        if (transform.position.y < groundLevel - 10f)
        {
            // Player has fallen too far - reset position
            Vector3 pos = transform.position;
            pos.y = groundLevel;
            transform.position = pos;
            rb.linearVelocity = Vector3.zero;
            isFalling = false;
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

