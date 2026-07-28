using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Top-down player controller with 4-directional movement using Rigidbody forces
/// Uses physics-based movement for interaction with environment forces
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class TopDownPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Base movement speed (affected by stats)")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    
    [Header("Physics Feel")]
    [Tooltip("How fast the character accelerates")]
    [SerializeField] private float acceleration = 50f;
    [Tooltip("How snappy the character stops (Drag)")]
    [SerializeField] private float stopDrag = 10f;
    
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
    
    [Tooltip("Camera to check viewport bounds against. Must be assigned in inspector. Should be the camera from TopDownCameraController.")]
    [SerializeField] private Camera viewportCamera;
    
    [Header("Viewport Boundaries")]
    [Tooltip("Margin from viewport edges (0-1). 0.1 means player can move within 10% of screen edge.")]
    [SerializeField] private float viewportMargin = 0.1f;
    
    [Tooltip("Enable viewport boundary checking to prevent player from moving outside camera view.")]
    [SerializeField] private bool enforceViewportBounds = true;
    
    [Header("Animation (Optional)")]
    [Tooltip("CharacterAnimator component for handling animations. Leave empty if not using animations.")]
    [SerializeField] private CharacterAnimator characterAnimator;
    
    [Header("Combat")]
    [Tooltip("CombatController component. If not assigned, will try to find it on this GameObject.")]
    [SerializeField] private CombatController combatController;
    
    [Tooltip("Attack data for basic attack")]
    [SerializeField] private AttackData basicAttackData;
    
    [Tooltip("Attack data for Skill 1 (Whirlwind AoE, centered on the player)")]
    [SerializeField] private AttackData skill1AttackData;

    [Tooltip("Base cooldown for Skill 1 in seconds. Scaled by CharacterStats skill-1 cooldown level.")]
    [SerializeField] private float skill1BaseCooldown = 10f;

    [Tooltip("Attack data for Skill 2 (use this OR skill2ProjectileData, not both)")]
    [SerializeField] private AttackData skill2AttackData;

    [Tooltip("Projectile skill data for Skill 2 (e.g., shuriken throw). If set, this is used instead of skill2AttackData.")]
    [SerializeField] private ProjectileSkillData skill2ProjectileData;

    [Tooltip("Attack data for Skill 3")]
    [SerializeField] private AttackData skill3AttackData;

    [Tooltip("Battle Cry buff data for Skill 3 (self speed buff). If set, Skill 3 uses this instead of skill3AttackData.")]
    [SerializeField] private BattleCrySkillData skill3BattleCryData;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] public float currentHealth;
    
    [Header("Stats")]
    [SerializeField] private CharacterStats characterStats;
    
    // Events
    public System.Action<float, float> OnHealthChanged; // current, max
    public System.Action OnDeath;
    
    // Properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0f;
    
    private Rigidbody rb;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction attackAction;
    private InputAction skill1Action;
    private InputAction skill2Action;
    private InputAction skill3Action;
    private InputAction pauseAction;
    private Vector3 moveDirection;
    private float currentSpeed;
    private bool isCurrentlyMoving = false;
    private bool isFalling = false;
    private bool isGrounded = false;
    private GroundType currentGroundType;
    private bool hasWarnedAboutMissingGroundType = false;
    private float skillLockUntilTime = -999f; // no skill usable until Time.time >= this
    private float skillLockDuration = 0f; // total duration of the active lock, for HUD fill ratio
    private float battleCrySpeedMultiplier = 1f;
    private Coroutine battleCryRoutine;
    
    /// <summary>
    /// Returns whether the character is currently moving
    /// </summary>
    public bool IsMoving => isCurrentlyMoving;

    /// <summary>Seconds until skills are usable again (0 = ready). Shared across all 3 skills.</summary>
    public float SkillCooldownRemaining => Mathf.Max(0f, skillLockUntilTime - Time.time);

    /// <summary>Total duration of the active skill lock, for HUD fill ratio. 0 when never used.</summary>
    public float SkillCooldownDuration => skillLockDuration;

    /// <summary>1 = fully on cooldown, 0 = ready.</summary>
    public float SkillCooldownNormalized =>
        skillLockDuration <= 0f ? 0f : Mathf.Clamp01(SkillCooldownRemaining / skillLockDuration);

    /// <summary>False when slot 1..3 is bound to a skill tree whose root Unlock node isn't purchased.
    /// True when unlocked, or when the slot has no tree binding (matches HandleSkills' gate).</summary>
    public bool IsSkillSlotUnlocked(int slot)
    {
        string skillId = SkillIdForSlot(slot);
        if (characterStats == null || skillId == null) return true;
        return characterStats.IsSkillUnlocked(skillId);
    }

    /// <summary>HUD display data for skill slot 1..3. Returns false if the slot is unassigned.</summary>
    public bool TryGetSkillDisplay(int slot, out string displayName, out Sprite icon)
    {
        displayName = null;
        icon = null;

        switch (slot)
        {
            case 1:
                if (skill1AttackData == null) return false;
                displayName = skill1AttackData.attackName;
                icon = skill1AttackData.icon;
                return true;
            case 2:
                if (skill2ProjectileData != null)
                {
                    displayName = skill2ProjectileData.skillName;
                    icon = skill2ProjectileData.icon;
                    return true;
                }
                if (skill2AttackData != null)
                {
                    displayName = skill2AttackData.attackName;
                    icon = skill2AttackData.icon;
                    return true;
                }
                return false;
            case 3:
                if (skill3BattleCryData != null)
                {
                    displayName = skill3BattleCryData.skillName;
                    icon = skill3BattleCryData.icon;
                    return true;
                }
                if (skill3AttackData != null)
                {
                    displayName = skill3AttackData.attackName;
                    icon = skill3AttackData.icon;
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

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
        rb.linearDamping = stopDrag; // Initialize with high drag
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
        
        // Per-instance PlayerInput (multi-player local co-op) takes priority over the shared asset.
        playerInput = GetComponent<PlayerInput>();

        if (playerInput != null)
        {
            moveAction = playerInput.actions["Player/Move"];
            sprintAction = playerInput.actions["Player/Sprint"];
            attackAction = playerInput.actions["Player/Attack"];
            skill1Action = playerInput.actions["Player/Skill1"];
            skill2Action = playerInput.actions["Player/Skill2"];
            skill3Action = playerInput.actions["Player/Skill3"];
            pauseAction = playerInput.actions["Player/Pause"];
        }
        else if (inputActions != null)
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

            pauseAction = inputActions.FindAction("Player/Pause");
            if (pauseAction == null)
            {
                Debug.LogWarning($"TopDownPlayerController: Could not find 'Player/Pause' action. Pause menu will be disabled.");
            }
            else
            {
                pauseAction.Enable();
            }
        }
        else
        {
            Debug.LogError($"TopDownPlayerController on '{gameObject.name}': InputActions asset is not assigned! Please assign it in the inspector.");
        }
        
        // Try to find CharacterAnimator if not assigned (often on child model, like EnemyHero)
        if (characterAnimator == null)
        {
            characterAnimator = GetComponent<CharacterAnimator>();
            if (characterAnimator == null)
                characterAnimator = GetComponentInChildren<CharacterAnimator>();
            if (characterAnimator == null)
                Debug.LogError($"TopDownPlayerController on '{gameObject.name}': CharacterAnimator is not assigned and could not be found on this object or children. Assign it in the inspector or add a CharacterAnimator to the character model for walk/run animations.");
        }
        
        // Try to find or create CombatController if not assigned
        if (combatController == null)
        {
            combatController = GetComponent<CombatController>();
            if (combatController == null)
            {
                // Auto-add CombatController
                combatController = gameObject.AddComponent<CombatController>();
                Debug.Log($"TopDownPlayerController on '{gameObject.name}': Auto-added CombatController component.");
            }
        }
        
        // Validate attack data - only warn about basic attack since that's essential
        if (combatController != null)
        {
            if (basicAttackData == null)
            {
                Debug.LogError($"TopDownPlayerController on '{gameObject.name}': Basic attack data is not assigned! Please create an AttackData ScriptableObject and assign it. Basic attack will not work.");
            }
            
            // Skills are optional, only warn
            if (skill1AttackData == null && skill1Action != null)
            {
                Debug.LogWarning($"TopDownPlayerController on '{gameObject.name}': Skill 1 attack data is not assigned. Skill 1 will not work.");
            }
            
            if (skill2AttackData == null && skill2Action != null)
            {
                Debug.LogWarning($"TopDownPlayerController on '{gameObject.name}': Skill 2 attack data is not assigned. Skill 2 will not work.");
            }
            
            if (skill3AttackData == null && skill3Action != null)
            {
                Debug.LogWarning($"TopDownPlayerController on '{gameObject.name}': Skill 3 attack data is not assigned. Skill 3 will not work.");
            }
        }
        
        // Try to find MapGenerator if not assigned
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<MapGenerator>();
            if (mapGenerator == null)
            {
                Debug.LogError($"TopDownPlayerController on '{gameObject.name}': MapGenerator is not assigned and could not be found in the scene! Ground type detection will not work. Please ensure a MapGenerator component exists in the scene or assign it in the inspector.");
            }
        }
        
        // Find viewportCamera if not assigned - look for MainCamera tag
        if (viewportCamera == null)
        {
            viewportCamera = Camera.main;
            if (viewportCamera != null)
            {
                Debug.Log($"TopDownPlayerController on '{gameObject.name}': Found viewport camera '{viewportCamera.name}' via Camera.main");
            }
            else
            {
                Debug.LogError($"TopDownPlayerController on '{gameObject.name}': ViewportCamera is not assigned and no camera with 'MainCamera' tag found! Viewport bounds checking will not work.");
            }
        }
        
        // Initialize health
        if (characterStats == null) characterStats = GetComponent<CharacterStats>();
        
        if (characterStats != null)
        {
            maxHealth = characterStats.GetMaxHealth();
            Debug.Log($"[Player] Stats loaded from CharacterStats. Max Health: {maxHealth}");
            
            // Listen for upgrades to update stats dynamically
            characterStats.OnStatsUpgraded += UpdateStatsFromUpgrade;
        }
        
        currentHealth = maxHealth;
        
        // Initialize damage multiplier
        if (combatController != null && characterStats != null)
        {
            float dmgMult = characterStats.GetDamageMultiplier();
            combatController.SetDamageMultiplier(dmgMult);
            Debug.Log($"[Player] Damage Multiplier set to {dmgMult}x");
        }
    }
    
    private void UpdateStatsFromUpgrade()
    {
        if (characterStats == null) return;
        
        // Update Max Health (keep percentage or just add difference? Let's just update max)
        float oldMax = maxHealth;
        maxHealth = characterStats.GetMaxHealth();
        
        // Heal the difference so leveling up feels good
        if (maxHealth > oldMax)
        {
            currentHealth += (maxHealth - oldMax);
        }
        
        // Update Damage
        if (combatController != null)
        {
            combatController.SetDamageMultiplier(characterStats.GetDamageMultiplier());
        }
        
        // Notify UI
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    private void OnEnable()
    {
        // PlayerInput manages its own action map lifecycle; only the shared-asset fallback needs this.
        if (playerInput == null && inputActions != null)
        {
            inputActions.Enable();
        }
    }

    private void OnDisable()
    {
        if (playerInput == null && inputActions != null)
        {
            inputActions.Disable();
        }
    }
    
    private void Update()
    {
        if (pauseAction != null && pauseAction.WasPressedThisFrame())
        {
            if (PauseMenuController.Instance != null)
            {
                PauseMenuController.Instance.Toggle();
            }
            else
            {
                Debug.LogError($"TopDownPlayerController on '{gameObject.name}': PauseMenuController.Instance is missing — pause menu was not generated into this scene.");
            }
        }

        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsOpen) return;

        // Debug Player Position for Jitter
        if (Time.frameCount % 5 == 0 && rb != null)
        {
             // Debug.Log($"[Player] Pos: {transform.position.ToString("F4")} | Vel: {rb.linearVelocity.ToString("F4")}");
        }

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
        if (moveAction == null)
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
        
        // Get ground type speed multiplier (smoothed)
        float groundSpeedMultiplier = 1f;
        
        if (mapGenerator != null && mapGenerator.IsMapGenerated())
        {
            // Use smoothed speed multiplier for fluid movement
            groundSpeedMultiplier = mapGenerator.GetSmoothedSpeedMultiplierAtPosition(transform.position);
            
            // Check for holes (fall logic)
            // We still use the discrete check for falling because falling is binary (you fall or you don't)
            if (mapGenerator.IsHoleAtPosition(transform.position) && isGrounded && !isFalling)
            {
                isFalling = true;
            }
            
            if (debugGroundDetection && isCurrentlyMoving)
            {
                Debug.Log($"Smoothed Speed Multiplier: {groundSpeedMultiplier:F2}");
            }
        }
        else
        {
            // Allow some time for map to generate on start
            if (Time.time > 1f && !hasWarnedAboutMissingGroundType)
            {
                Debug.LogWarning($"TopDownPlayerController on '{gameObject.name}': No ground type detected! Make sure MapGenerator has generated the map.");
                hasWarnedAboutMissingGroundType = true;
            }
        }
        
        // Calculate movement speed
        float speedStatMultiplier = characterStats != null ? characterStats.GetMoveSpeedMultiplier() : 1f;
        currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f) * groundSpeedMultiplier * speedStatMultiplier * battleCrySpeedMultiplier;
        
        // Convert 2D input to 3D movement on X-Z plane
        // Reset moveDirection first to ensure clean state
        moveDirection = Vector3.zero;
        
        // Store original input for animation purposes
        bool hasInput = input.magnitude > 0.1f;
        Vector3 desiredDirection = Vector3.zero;
        
        // Only set direction if there's actual input
        if (hasInput)
        {
            // Convert 2D input (X, Y) to 3D movement (X, 0, Z)
            // input.x = horizontal (left/right)
            // input.y = vertical (forward/back)
            desiredDirection = new Vector3(input.x, 0f, input.y);
            
            // Normalize to prevent faster diagonal movement
            desiredDirection.Normalize();
            
            // Check viewport bounds if enabled
            if (enforceViewportBounds && viewportCamera != null)
            {
                // Calculate next position based on movement direction
                Vector3 nextPosition = transform.position + desiredDirection * (currentSpeed * Time.deltaTime);
                
                // Check if next position would be outside viewport
                if (!IsPositionWithinViewport(nextPosition))
                {
                    // Block movement in the direction that would go outside viewport
                    // Try to allow movement in other directions if possible
                    desiredDirection = GetClampedMovementDirection(desiredDirection, transform.position, currentSpeed * Time.deltaTime);
                }
            }
            
            moveDirection = desiredDirection;
        }
        else
        {
            moveDirection = Vector3.zero;
        }
        
        // Check if moving (use original input for animation, not clamped direction)
        // This allows walk animation to play even when movement is blocked
        isCurrentlyMoving = hasInput;
        
        // Debug: Log movement direction
        if (debugGroundDetection)
        {
            if (isCurrentlyMoving)
            {
                // Debug.Log($"Move Direction: {moveDirection}, Input: {input}, Current Speed: {currentSpeed}");
            }
            else if (input.magnitude > 0.01f)
            {
                Debug.LogWarning($"Input detected ({input}) but moveDirection is zero! Magnitude: {input.magnitude}");
            }
        }
        
        // Rotate towards movement direction
        if (isCurrentlyMoving && rotateTowardsMovement && moveDirection.sqrMagnitude > 0.001f)
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
        if (rb == null) return;
        
        // Get current horizontal velocity
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        
        if (isCurrentlyMoving && moveDirection.magnitude > 0.01f)
        {
            // Moving: Low drag for responsiveness
            rb.linearDamping = 0f;

            // Calculate force to reach target speed (P-Controller style)
            // This automatically handles acceleration and maintains max speed naturally
            Vector3 targetVelocity = moveDirection * currentSpeed;
            Vector3 velocityDiff = targetVelocity - horizontalVelocity;
            
            // Apply acceleration force
            rb.AddForce(velocityDiff * acceleration, ForceMode.Force);
        }
        else
        {
            // Stopping: High drag
            rb.linearDamping = stopDrag;
            
            // Snap to stop if very slow
            if (horizontalVelocity.sqrMagnitude < 0.1f)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
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
            Debug.Log($"[Player] Attack button pressed!");
            
            // Trigger attack animation
            if (characterAnimator != null)
            {
                characterAnimator.TriggerAttack(0); // Default to light attack (type 0)
            }
            
            // Perform combat attack
            if (combatController != null && basicAttackData != null)
            {
                combatController.PerformAttack(basicAttackData);
            }
            else
            {
                if (combatController == null)
                {
                    Debug.LogError($"[Player] CombatController is null! Cannot perform attack.");
                }
                if (basicAttackData == null)
                {
                    Debug.LogError($"[Player] BasicAttackData is null! Cannot perform attack.");
                }
            }
        }
    }
    
    /// <summary>True if any skill is currently locked out by the global skill-lock cooldown.</summary>
    private bool SkillsLocked => Time.time < skillLockUntilTime;

    /// <summary>Skill id (from the character's skill tree) bound to input slot 1/2/3, or null if none declared.</summary>
    private string SkillIdForSlot(int slot)
    {
        if (characterStats == null || characterStats.definition == null || characterStats.definition.skillTrees == null)
        {
            return null;
        }
        foreach (SkillTreeDefinition tree in characterStats.definition.skillTrees)
        {
            if (tree.skillSlot == slot) return tree.skillId;
        }
        return null;
    }

    /// <summary>Start the global skill lock using skillId's tree-resolved cooldown (falls back to baseCooldown).</summary>
    private void LockSkills(string skillId, float baseCooldown)
    {
        skillLockDuration = (characterStats != null && skillId != null)
            ? characterStats.GetSkillValue(skillId, SkillEffect.Cooldown, baseCooldown)
            : baseCooldown;
        skillLockUntilTime = Time.time + skillLockDuration;
    }

    private void HandleSkills()
    {
        // Basic attack is handled separately (HandleAttack) and is never gated by the skill lock.
        if (SkillsLocked) return;

        // Check if skill buttons were pressed this frame
        if (skill1Action != null && skill1Action.WasPressedThisFrame())
        {
            string skillId = SkillIdForSlot(1);
            if (characterStats != null && skillId != null && !characterStats.IsSkillUnlocked(skillId))
            {
                Debug.Log($"[Player] Skill 1 ({skillId}) is locked — purchase its Unlock node in the skill tree.");
                return; // locked — not yet purchased in the skill tree
            }

            if (combatController != null && skill1AttackData != null && basicAttackData != null)
            {
                float dmgMult = characterStats != null ? characterStats.GetSkillValue(skillId, SkillEffect.Damage, 2f) : 2f;
                float radius = characterStats != null ? characterStats.GetSkillValue(skillId, SkillEffect.Area, 3f) : 3f;

                if (characterAnimator != null)
                {
                    characterAnimator.TriggerSkill1();
                }

                combatController.PerformAttack(
                    skill1AttackData,
                    basicAttackData.damage * dmgMult,
                    radius,
                    bypassCooldown: true);

                LockSkills(skillId, skill1BaseCooldown);
            }
            else
            {
                Debug.LogError($"[Player] Skill 1 needs combatController, skill1AttackData and basicAttackData assigned.");
            }
            return; // one skill per frame
        }

        if (skill2Action != null && skill2Action.WasPressedThisFrame())
        {
            string skillId = SkillIdForSlot(2);
            if (characterStats != null && skillId != null && !characterStats.IsSkillUnlocked(skillId))
            {
                Debug.Log($"[Player] Skill 2 ({skillId}) is locked — purchase its Unlock node in the skill tree.");
                return; // locked — not yet purchased in the skill tree
            }

            // Check if using projectile skill (e.g., shuriken throw)
            if (skill2ProjectileData != null && skill2ProjectileData.projectilePrefab != null)
            {
                if (characterAnimator != null)
                {
                    characterAnimator.TriggerSkill2();
                }

                // Start timed projectile spawning
                StartCoroutine(SpawnProjectilesAtTimes(skill2ProjectileData));

                LockSkills(skillId, skill2ProjectileData.cooldown);
            }
            else
            {
                // Fallback to regular attack data
                if (characterAnimator != null)
                {
                    characterAnimator.TriggerSkill2();
                }

                if (combatController != null && skill2AttackData != null)
                {
                    combatController.PerformAttack(skill2AttackData, bypassCooldown: true);
                    LockSkills(skillId, skill2AttackData.cooldown);
                }
            }
            return;
        }

        if (skill3Action != null && skill3Action.WasPressedThisFrame())
        {
            string skillId = SkillIdForSlot(3);
            if (characterStats != null && skillId != null && !characterStats.IsSkillUnlocked(skillId))
            {
                Debug.Log($"[Player] Skill 3 ({skillId}) is locked — purchase its Unlock node in the skill tree.");
                return; // locked — not yet purchased in the skill tree
            }

            if (skill3BattleCryData != null)
            {
                // Battle Cry self-buff (global skill lock gates re-use, no self-stacking)
                if (characterAnimator != null)
                {
                    characterAnimator.TriggerSkill3();
                }

                if (battleCryRoutine != null) StopCoroutine(battleCryRoutine);
                battleCryRoutine = StartCoroutine(BattleCryRoutine(skill3BattleCryData, skillId));

                LockSkills(skillId, skill3BattleCryData.cooldown);
            }
            else
            {
                // Legacy fallback: AttackData-based skill 3
                if (characterAnimator != null)
                {
                    characterAnimator.TriggerSkill3();
                }

                if (combatController != null && skill3AttackData != null)
                {
                    combatController.PerformAttack(skill3AttackData, bypassCooldown: true);
                    LockSkills(skillId, skill3AttackData.cooldown);
                }
            }
            return;
        }
    }

    private IEnumerator BattleCryRoutine(BattleCrySkillData data, string skillId)
    {
        float mult = characterStats != null ? characterStats.GetSkillValue(skillId, SkillEffect.Magnitude, 2f) : 2f;
        float duration = characterStats != null ? characterStats.GetSkillValue(skillId, SkillEffect.Duration, data.duration) : data.duration;

        if (data.activateEffectPrefab != null)
        {
            Instantiate(data.activateEffectPrefab, transform.position, transform.rotation, transform);
        }

        battleCrySpeedMultiplier = mult;
        Debug.Log($"[Player] Battle Cry! Speed x{mult} for {duration}s");

        yield return new WaitForSeconds(duration);

        battleCrySpeedMultiplier = 1f;
        battleCryRoutine = null;
        Debug.Log($"[Player] Battle Cry ended.");
    }


    
    /// <summary>
    /// Spawns projectiles at specific times during the skill animation
    /// </summary>
    private IEnumerator SpawnProjectilesAtTimes(ProjectileSkillData skillData)
    {
        if (skillData == null || skillData.projectilePrefab == null)
        {
            Debug.LogError($"TopDownPlayerController: ProjectileSkillData or projectilePrefab is null!");
            yield break;
        }
        
        float[] spawnTimes = skillData.GetSpawnTimesInSeconds();
        float elapsedTime = 0f;
        int projectilesSpawned = 0;
        
        // Store the facing direction at skill start (so all projectiles go the same way)
        Vector3 skillDirection = transform.forward;
        
        while (projectilesSpawned < spawnTimes.Length)
        {
            elapsedTime += Time.deltaTime;
            
            // Check if it's time to spawn the next projectile
            if (projectilesSpawned < spawnTimes.Length && elapsedTime >= spawnTimes[projectilesSpawned])
            {
                SpawnProjectile(skillData, skillDirection);
                projectilesSpawned++;
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Spawn a single projectile
    /// </summary>
    private void SpawnProjectile(ProjectileSkillData skillData, Vector3 direction)
    {
        // Base the spawn on the collider's actual grounded footprint rather than the raw
        // transform, so a stale/mis-snapped pivot can't bury the projectile in terrain.
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        Vector3 basePos = capsule != null ? capsule.bounds.center : transform.position;
        Vector3 spawnPos = basePos + transform.TransformDirection(skillData.spawnOffset);
        Quaternion spawnRot = Quaternion.LookRotation(direction);

        // Instantiate projectile
        GameObject projectileObj = Instantiate(skillData.projectilePrefab, spawnPos, spawnRot);
        
        // Initialize projectile
        Projectile projectile = projectileObj.GetComponent<Projectile>();
        if (projectile != null)
        {
            float damageMultiplier = combatController != null
                ? combatController.GetDamageMultiplier()
                : 1f;
            
            projectile.Initialize(
                skillData.damage * damageMultiplier,
                skillData.knockbackForce,
                direction,
                gameObject
            );
            projectile.SetSpeed(skillData.projectileSpeed);
            projectile.SetTargetTag("Enemy");
        }
        else
        {
            Debug.LogError($"TopDownPlayerController: Projectile prefab '{skillData.projectilePrefab.name}' has no Projectile script!");
            Destroy(projectileObj);
        }
    }
    
    /// <summary>
    /// Detects the ground type at the player's position
    /// </summary>
    private void DetectGround()
    {
        // Ground detection is now handled directly in HandleMovement using smoothed lookups
        // This method is kept empty or can be removed if not used elsewhere
    }
    
    /// <summary>
    /// Check if a world position is within the camera viewport bounds
    /// </summary>
    private bool IsPositionWithinViewport(Vector3 worldPosition)
    {
        if (viewportCamera == null)
        {
            Debug.LogError($"TopDownPlayerController on '{gameObject.name}': Cannot check viewport bounds - viewportCamera is null! Please assign a camera in the inspector.");
            return true; // Allow movement if camera is missing to prevent blocking
        }
        
        // Convert world position to viewport coordinates (0-1 range)
        Vector3 viewportPos = viewportCamera.WorldToViewportPoint(worldPosition);
        
        // Check if position is within viewport bounds (with margin)
        // X and Y should be between margin and (1 - margin)
        // Z should be positive (in front of camera)
        bool withinX = viewportPos.x >= viewportMargin && viewportPos.x <= (1f - viewportMargin);
        bool withinY = viewportPos.y >= viewportMargin && viewportPos.y <= (1f - viewportMargin);
        bool inFront = viewportPos.z > 0f;
        
        return withinX && withinY && inFront;
    }
    
    /// <summary>
    /// Get a movement direction that is clamped to stay within viewport bounds
    /// Tries to allow partial movement if only one axis would go outside
    /// </summary>
    private Vector3 GetClampedMovementDirection(Vector3 desiredDirection, Vector3 currentPosition, float moveDistance)
    {
        if (viewportCamera == null)
        {
            Debug.LogError($"TopDownPlayerController on '{gameObject.name}': Cannot clamp movement direction - viewportCamera is null! Please assign a camera in the inspector.");
            return desiredDirection;
        }
        
        Vector3 clampedDirection = Vector3.zero;
        
        // Test X movement separately
        Vector3 testX = currentPosition + new Vector3(desiredDirection.x * moveDistance, 0f, 0f);
        bool canMoveX = IsPositionWithinViewport(testX);
        
        // Test Z movement separately
        Vector3 testZ = currentPosition + new Vector3(0f, 0f, desiredDirection.z * moveDistance);
        bool canMoveZ = IsPositionWithinViewport(testZ);
        
        // Allow movement in directions that stay within bounds
        if (canMoveX)
        {
            clampedDirection.x = desiredDirection.x;
        }
        
        if (canMoveZ)
        {
            clampedDirection.z = desiredDirection.z;
        }
        
        // Normalize to maintain consistent speed
        if (clampedDirection.magnitude > 0.1f)
        {
            clampedDirection.Normalize();
        }
        
        return clampedDirection;
    }
    
    /// <summary>
    /// Take damage and apply knockback force.
    /// Called by enemy's combat system when hitting the player.
    /// </summary>
    /// <param name="damage">Amount of damage to take</param>
    /// <param name="knockbackDirection">Direction to knock back (horizontal only)</param>
    /// <param name="knockbackForce">Force magnitude of knockback</param>
    public void TakeDamage(float damage, Vector3 knockbackDirection, float knockbackForce)
    {
        if (!IsAlive) return;
        
        // Apply damage
        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        Debug.Log($"[Player] {gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
        
        // Apply knockback (horizontal only)
        if (knockbackForce > 0f && rb != null)
        {
            Vector3 horizontalKnockback = new Vector3(knockbackDirection.x, 0f, knockbackDirection.z).normalized;
            rb.AddForce(horizontalKnockback * knockbackForce, ForceMode.Impulse);
        }
        
        // Trigger hit animation
        characterAnimator?.TriggerHit();
        
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
    /// Handle player death
    /// </summary>
    private void Die()
    {
        Debug.Log($"[Player] {gameObject.name} died!");

        if (battleCryRoutine != null)
        {
            StopCoroutine(battleCryRoutine);
            battleCryRoutine = null;
        }
        battleCrySpeedMultiplier = 1f;

        OnDeath?.Invoke();
        characterAnimator?.TriggerDeath();

        // Disable input
        if (playerInput == null && inputActions != null)
        {
            inputActions.Disable();
        }
    }
    
    /// <summary>
    /// Heal the player
    /// </summary>
    public void Heal(float amount)
    {
        if (!IsAlive) return;
        
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
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
        
        // Draw viewport bounds indicator
        if (enforceViewportBounds && viewportCamera != null && Application.isPlaying)
        {
            // Draw a line from player to viewport edge
            Vector3 viewportPos = viewportCamera.WorldToViewportPoint(transform.position);
            Gizmos.color = Color.yellow;
            
            // Draw lines to viewport boundaries
            if (viewportPos.x <= viewportMargin || viewportPos.x >= (1f - viewportMargin))
            {
                Gizmos.color = Color.red;
            }
            else if (viewportPos.y <= viewportMargin || viewportPos.y >= (1f - viewportMargin))
            {
                Gizmos.color = Color.red;
            }
        }
    }
}

