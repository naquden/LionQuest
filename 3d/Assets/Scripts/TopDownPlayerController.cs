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
    [Tooltip("MapGenerator holding the ground-type map and the island bounds. Players are prefabs " +
             "instantiated at runtime, so this cannot be wired in the Inspector — PlayerSpawner " +
             "injects it via SetMapGenerator. Left empty in scenes with no terrain (AgentGym).")]
    [SerializeField] private MapGenerator mapGenerator;
    
    [Tooltip("Enable debug logging for ground detection")]
    [SerializeField] private bool debugGroundDetection = false;

    [Tooltip("Log every hit taken. Off by default — a busy fight against several enemies logs this " +
             "every frame one lands, spamming the console badly enough to slow down headless test runs.")]
    [SerializeField] private bool debugDamage = false;
    
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
    
    [Header("Map Boundaries")]
    [Tooltip("Keep the player inside the island (MapGenerator.GetPlayableRadius). Replaces the old " +
             "per-player camera-viewport leash, which could freeze a player off-screen.")]
    [SerializeField] private bool enforceMapBounds = true;

    [Tooltip("How hard the map edge pushes the player back inward. Force, not a position clamp, so " +
             "it composes with knockback and never teleports.")]
    [SerializeField] private float boundsPushForce = 40f;

    [Header("Animation (Optional)")]
    [Tooltip("CharacterAnimator component for handling animations. Leave empty if not using animations.")]
    [SerializeField] private CharacterAnimator characterAnimator;
    
    [Header("Combat")]
    [Tooltip("CombatController component. If not assigned, will try to find it on this GameObject.")]
    [SerializeField] private CombatController combatController;
    
    [Tooltip("Attack data for basic attack")]
    [SerializeField] private AttackData basicAttackData;

    [Tooltip("Minimum seconds between basic attacks. Stops the attack button from being spammed: " +
             "without it, every press replayed the swing animation and spawned an effect, so holding " +
             "the button down machine-gunned hits. Uses basicAttackData.cooldown when that is larger, " +
             "so the data can ask for a slower weapon but never a faster one than this floor.")]
    [SerializeField] private float basicAttackMinInterval = 0.5f;

    [Tooltip("Attack data for Skill 1 (Whirlwind AoE, centered on the player)")]
    [SerializeField] private AttackData skill1AttackData;

    [Tooltip("Base cooldown for Skill 1 in seconds. Scaled by CharacterStats skill-1 cooldown level.")]
    [SerializeField] private float skill1BaseCooldown = 10f;

    [Tooltip("Attack data for Skill 2 (use this OR skill2ProjectileData, not both)")]
    [SerializeField] private AttackData skill2AttackData;

    [Tooltip("Projectile skill data for Skill 2 (e.g., shuriken throw). If set, this is used instead of skill2AttackData.")]
    [SerializeField] private ProjectileSkillData skill2ProjectileData;

    [Tooltip("Hitscan laser data for Skill 2 (e.g., finger beam). Takes precedence over " +
             "skill2ProjectileData and skill2AttackData when set.")]
    [SerializeField] private LaserBeamSkillData skill2LaserData;

    [Tooltip("Muzzle transform the laser beam fires from (e.g. a hand bone). Optional — falls back " +
             "to a name search for 'RightHand', then to the capsule center plus " +
             "LaserBeamSkillData.muzzleFallbackOffset.")]
    [SerializeField] private Transform laserMuzzle;

    [Tooltip("Attack data for Skill 3")]
    [SerializeField] private AttackData skill3AttackData;

    [Tooltip("Battle Cry buff data for Skill 3 (self speed buff). If set, Skill 3 uses this instead of skill3AttackData.")]
    [SerializeField] private BattleCrySkillData skill3BattleCryData;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] public float currentHealth;

    [Header("Downed / Revive")]
    [Tooltip("Radius a living teammate must stand within to revive this player while downed. " +
             "Generous rather than tight: in a four-player scrum around a dying player, the reviver " +
             "should not have to pixel-hunt the exact spot the downed body ended up knocked back to.")]
    [SerializeField] private float reviveRadius = 2.5f;

    [Tooltip("Seconds a living teammate must stay within reviveRadius, continuously, to revive this " +
             "player. Stepping out of range resets progress rather than pausing it — reviving is a " +
             "commitment, not something to tag and abandon while fighting off the enemy that downed " +
             "you in the first place.")]
    [SerializeField] private float reviveDuration = 2f;

    [Tooltip("Fraction of max health a revived player returns with. Below 1 so being downed still " +
             "costs the team something even after a successful revive, rather than being a free do-over.")]
    [SerializeField] private float reviveHealthFraction = 0.5f;

    private bool isDowned;
    private float reviveProgress;

    [Header("Stats")]
    [SerializeField] private CharacterStats characterStats;

    // Events
    public System.Action<float, float> OnHealthChanged; // current, max
    public System.Action OnDeath;

    /// <summary>Fired when this player goes down but the run continues — at least one teammate was
    /// still alive at the moment of death, so Die() (permanent, run-ending) did not fire instead.</summary>
    public System.Action OnDowned;

    /// <summary>Fired when a downed player is revived by a teammate's proximity.</summary>
    public System.Action OnRevived;

    // Properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    /// <summary>False while downed as well as while permanently dead — both states are "not playing
    /// right now" for every existing caller (movement/attack gating, camera framing, enemy targeting,
    /// PauseMenuController's all-dead check). Only EnterDowned/Revive/Die distinguish the two.</summary>
    public bool IsAlive => currentHealth > 0f;

    /// <summary>True between EnterDowned and Revive. A downed player is not IsAlive, but unlike a
    /// permanently dead one can still return to the run — see UpdateDownedState.</summary>
    public bool IsDowned => isDowned;

    /// <summary>0..1 progress toward reviving, for a HUD to show a fill ring over a downed
    /// character. 0 whenever no teammate is currently in range.</summary>
    public float ReviveProgress01 => reviveDuration > 0f ? Mathf.Clamp01(reviveProgress / reviveDuration) : 0f;
    
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
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
    private float basicAttackReadyTime = -999f; // no basic attack until Time.time >= this

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
    /// True when unlocked, or when the slot has no tree binding (matches HandleSkills' gate).
    /// This is the single source of truth for both the HUD's lock mark and HandleSkills' gate —
    /// HandleSkills must not re-derive it, or the two can disagree (HUD shows locked while the
    /// button still fires).</summary>
    public bool IsSkillSlotUnlocked(int slot)
    {
        string skillId = SkillIdForSlot(slot);
        if (characterStats == null || skillId == null) return true;
        return characterStats.IsSkillUnlocked(skillId);
    }

    /// <summary>
    /// The button the player must press for skill slot 1..3, as a short HUD label
    /// ("1"/"2"/"3" on keyboard, "Y"/"B"/"RB" on a gamepad), taken from the action's
    /// real bindings for the device this player is actually using. Falls back to the
    /// slot number when the action or its binding can't be resolved.
    /// </summary>
    public string GetSkillBindingLabel(int slot)
    {
        InputAction action = slot switch
        {
            1 => skill1Action,
            2 => skill2Action,
            3 => skill3Action,
            _ => null,
        };

        if (action == null) return slot.ToString();

        // currentControlScheme is still empty at HUD-bind time (it only resolves once the paired
        // device sends its first input), and PlayerSpawner pairs the device while the instance is
        // still inactive, so playerInput.devices can also be empty this early. Prefer a paired
        // gamepad, then fall back to whether a gamepad is present at all.
        bool usingGamepad = Gamepad.current != null;
        if (playerInput != null && playerInput.devices.Count > 0)
        {
            usingGamepad = false;
            foreach (InputDevice device in playerInput.devices)
            {
                if (device is Gamepad)
                {
                    usingGamepad = true;
                    break;
                }
            }
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            string path = action.bindings[i].effectivePath;
            if (string.IsNullOrEmpty(path)) continue;

            bool isGamepadBinding = path.StartsWith("<Gamepad>", System.StringComparison.Ordinal);
            if (isGamepadBinding != usingGamepad) continue;

            return ShortBindingLabel(path, slot);
        }

        return slot.ToString();
    }

    /// <summary>Compresses an input path into something that fits a 56px HUD badge.</summary>
    private static string ShortBindingLabel(string path, int slot)
    {
        int slash = path.LastIndexOf('/');
        string control = slash >= 0 ? path.Substring(slash + 1) : path;

        switch (control)
        {
            case "buttonNorth": return "Y";
            case "buttonSouth": return "A";
            case "buttonEast": return "B";
            case "buttonWest": return "X";
            case "leftShoulder": return "LB";
            case "rightShoulder": return "RB";
            case "leftTrigger": return "LT";
            case "rightTrigger": return "RT";
            default:
                // Keyboard paths are already short ("1", "2", "q"); uppercase letter keys.
                return control.Length <= 3 ? control.ToUpperInvariant() : slot.ToString();
        }
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
                if (skill2LaserData != null)
                {
                    displayName = skill2LaserData.skillName;
                    icon = skill2LaserData.icon;
                    return true;
                }
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
        capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider == null)
        {
            Debug.LogWarning($"TopDownPlayerController: Adding CapsuleCollider for physics collision...");
            capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.height = 2f;
            capsuleCollider.radius = 0.5f;
            capsuleCollider.center = new Vector3(0, 1f, 0); // Center at half height
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
        
        // Stand the player ON the ground rather than putting its pivot at ground level.
        //
        // This used to be `position.y = groundLevel`, which buried the capsule: the collider is
        // centred on the pivot (center 0, height 1), so its bottom sits half a unit BELOW the pivot.
        // Spawning the pivot at y=0 left the lower half of the capsule inside the ground collider,
        // and PhysX resolved that interpenetration in whichever direction it liked — sometimes
        // shoving the player up onto the surface, sometimes pushing it straight through the floor to
        // fall forever at terminal velocity (~0.79 u/s against stopDrag). In a two-player scene the
        // two players routinely resolved differently, so one stood and one vanished under the map.
        SnapToGroundLevel();

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
            
            if (skill2AttackData == null && skill2ProjectileData == null && skill2LaserData == null && skill2Action != null)
            {
                Debug.LogWarning($"TopDownPlayerController on '{gameObject.name}': Skill 2 attack data is not assigned. Skill 2 will not work.");
            }
            
            if (skill3AttackData == null && skill3Action != null)
            {
                Debug.LogWarning($"TopDownPlayerController on '{gameObject.name}': Skill 3 attack data is not assigned. Skill 3 will not work.");
            }
        }
        
        // mapGenerator is deliberately NOT resolved here — see SetMapGenerator and Start.

        // Initialize health
        if (characterStats == null) characterStats = GetComponent<CharacterStats>();
        
        if (characterStats != null)
        {
            maxHealth = characterStats.GetMaxHealth();
            moveSpeed = characterStats.GetBaseMoveSpeed(moveSpeed);
            if (characterAnimator != null) characterAnimator.SetMaxSpeedForNormalization(moveSpeed);
            Debug.Log($"[Player] Stats loaded from CharacterStats. Max Health: {maxHealth}, Move Speed: {moveSpeed}");

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
    
    /// <summary>
    /// Injects the scene's terrain. Players are prefabs instantiated at runtime, so this reference
    /// cannot live in the prefab; PlayerSpawner passes its own Inspector-assigned MapGenerator. It
    /// used to be resolved with FindFirstObjectByType, which the project's no-lookup-fallbacks rule
    /// bans — and which would silently pick an arbitrary terrain in any scene holding more than one.
    /// </summary>
    public void SetMapGenerator(MapGenerator generator)
    {
        mapGenerator = generator;
    }

    private void Start()
    {
        // Validated in Start, not Awake: Awake runs during Instantiate, before PlayerSpawner has had
        // any chance to inject. By Start the injection has happened.
        if (mapGenerator == null)
        {
            Debug.LogError($"TopDownPlayerController on '{gameObject.name}': no MapGenerator was injected. " +
                           "Ground-type speed scaling, hole detection and island bounds are all dead. " +
                           "Assign PlayerSpawner.mapGenerator in the scene — expected to be empty only in " +
                           "deliberately terrain-free scenes such as AgentGym.");
        }

        // Registered here as well as in OnEnable, for the same reason CharacterStats does: the
        // singleton may not exist yet when OnEnable runs (PlayerSpawner creates the GameController
        // prefab in its own Start), and a player that misses registration is a player no enemy will
        // ever target. Register* is idempotent.
        if (GameSaveController.Instance != null)
        {
            GameSaveController.Instance.RegisterPlayerController(this);
        }
    }

    private void OnEnable()
    {
        // Re-snap here, not only in Awake: PlayerSpawner instantiates the prefab (which runs Awake
        // immediately) and only THEN assigns spawnPoint.position, overwriting whatever Awake set. So
        // the Awake snap alone left every spawned player buried to the waist in the floor. OnEnable
        // runs on the spawner's SetActive(true) call, which happens after the position is assigned.
        SnapToGroundLevel();

        if (GameSaveController.Instance != null)
        {
            GameSaveController.Instance.RegisterPlayerController(this);
        }

        // PlayerInput manages its own action map lifecycle; only the shared-asset fallback needs this.
        if (playerInput == null && inputActions != null)
        {
            inputActions.Enable();
        }
    }

    /// <summary>
    /// Places the player's feet on groundLevel, so its capsule rests on the ground instead of
    /// intersecting it. Called from Awake and OnEnable — see the note in OnEnable for why both.
    /// </summary>
    private void SnapToGroundLevel()
    {
        Vector3 position = transform.position;
        position.y = groundLevel + CapsuleBottomOffset();
        transform.position = position;
    }

    private void OnDisable()
    {
        if (GameSaveController.Instance != null)
        {
            GameSaveController.Instance.UnregisterPlayerController(this);
        }

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

        // Downed players still need a frame tick — that is how revive progress accumulates — so
        // this branches before the corpse early-out below rather than folding into it.
        if (isDowned)
        {
            UpdateDownedState();
            return;
        }

        // A corpse must not move, swing or cast. Die() only disabled input on the shared-asset
        // fallback path (playerInput == null), so a PlayerInput-driven player — i.e. every player
        // PlayerSpawner creates — kept full control of a body at 0 health.
        if (!IsAlive) return;

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
        // Update stops calling HandleMovement once dead, but moveDirection keeps its last value, so
        // without this the corpse coasts on in whatever direction it was heading.
        if (!IsAlive)
        {
            moveDirection = Vector3.zero;
            return;
        }

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

        ApplyMapBoundsForce();
    }

    /// <summary>
    /// Pushes the player back toward the island when they stray past the playable radius.
    ///
    /// Runs unconditionally, which is the whole point: the leash this replaced only evaluated while
    /// the player was pressing a direction, so a player who was standing still could be carried out
    /// of bounds and then had every movement axis rejected — permanently stuck. A force that always
    /// applies can never trap anyone, because it only ever pushes inward, and the player keeps full
    /// authority over their own input.
    /// </summary>
    private void ApplyMapBoundsForce()
    {
        if (!enforceMapBounds || mapGenerator == null) return;

        Vector3 center = mapGenerator.GetIslandCenter();
        Vector3 offset = transform.position - center;
        offset.y = 0f;

        float radius = mapGenerator.GetPlayableRadius();
        float distance = offset.magnitude;
        if (distance <= radius || distance < 0.001f) return;

        // Ramps in over the first 4 units past the edge so the boundary feels like a shove rather
        // than a wall, and so a knockback that flings a player far out still brings them home.
        float overshoot = Mathf.Clamp01((distance - radius) / 4f);
        rb.AddForce(-offset.normalized * (boundsPushForce * overshoot), ForceMode.Force);
    }
    
    /// <summary>
    /// Checks if the player is grounded using raycast
    /// </summary>
    private void CheckGrounded()
    {
        // Cast from just above the capsule's bottom, not from the pivot.
        //
        // The pivot sits at the player's feet, i.e. exactly ON the ground surface when standing, and a
        // ray that starts on a surface does not reliably report a hit — so this returned false even
        // with the player firmly on the floor and isGrounded was effectively always false. Starting
        // the ray a little higher and adding that lift to the length keeps the same effective reach
        // below the feet while giving the cast somewhere to travel from.
        const float rayLift = 0.2f;
        Vector3 origin = transform.position + Vector3.up * (CapsuleBottomOffset() + rayLift);
        isGrounded = Physics.Raycast(origin, Vector3.down, rayLift + groundCheckDistance + 0.1f, groundLayerMask);

        // Reset falling state if back on ground
        if (isGrounded && isFalling)
        {
            isFalling = false;
        }

        // Check if fallen too far (respawn logic)
        if (transform.position.y < groundLevel - 10f)
        {
            // Player has fallen too far - reset position. Standing height, not pivot-at-ground:
            // respawning with the capsule half inside the floor is what caused the fall to begin with.
            Vector3 pos = transform.position;
            pos.y = groundLevel + CapsuleBottomOffset();
            transform.position = pos;
            rb.linearVelocity = Vector3.zero;
            isFalling = false;
        }
    }

    /// <summary>
    /// How far above the pivot the player's feet must be placed so the capsule rests on, rather than
    /// inside, ground at groundLevel. Accounts for the collider's centre offset, its height/radius
    /// (a capsule shorter than 2*radius is a sphere of that radius) and the transform's Y scale.
    /// </summary>
    private float CapsuleBottomOffset()
    {
        if (capsuleCollider == null) return 0f;

        // direction 1 = Y-aligned, so the capsule's vertical extent is half its height (never less
        // than the radius). Laid on its side (X/Z) the vertical extent is just the radius.
        float verticalExtent = capsuleCollider.direction == 1
            ? Mathf.Max(capsuleCollider.height * 0.5f, capsuleCollider.radius)
            : capsuleCollider.radius;

        return (verticalExtent - capsuleCollider.center.y) * Mathf.Abs(transform.lossyScale.y);
    }
    
    /// <summary>Seconds until the basic attack is usable again (0 = ready).</summary>
    public float BasicAttackCooldownRemaining => Mathf.Max(0f, basicAttackReadyTime - Time.time);

    /// <summary>Enforced gap between basic attacks: the larger of basicAttackMinInterval and the
    /// AttackData's own cooldown. CombatController applies the AttackData cooldown to the effect
    /// spawn anyway, so taking the max keeps the animation and the damage on the same schedule
    /// instead of letting the animation run at a rate the damage can't match.</summary>
    private float BasicAttackInterval =>
        Mathf.Max(basicAttackMinInterval, basicAttackData != null ? basicAttackData.cooldown : 0f);

    private void HandleAttack()
    {
        // Check if attack button was pressed this frame
        if (attackAction != null && attackAction.WasPressedThisFrame())
        {
            // Spam guard. Deliberately covers the animation and the log too, not only the damage:
            // CombatController already rate-limited the effect spawn by basicAttackData.cooldown, so
            // spamming the button dealt no extra damage but still restarted the swing animation on
            // every press, which reads as a working machine-gun attack.
            if (Time.time < basicAttackReadyTime)
            {
                return;
            }
            basicAttackReadyTime = Time.time + BasicAttackInterval;

            Debug.Log($"[Player] Attack button pressed!");

            // Trigger attack animation
            if (characterAnimator != null)
            {
                characterAnimator.TriggerAttack(0); // Default to light attack (type 0)
            }

            // Perform combat attack. No SFX call here — Enemy/TopDownPlayerController.TakeDamage
            // already plays "Hit" on impact; a second call at swing time would double up the sound
            // on every attack that actually connects and play a "Hit" with nothing hit on every one
            // that doesn't.
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

    /// <summary>Skill id (from the character's skill tree) bound to input slot 1/2/3, or null if none declared.
    /// Null means "this slot has no tree", which makes the slot permanently unlocked — so a player
    /// whose CharacterStats.definition was never assigned can fire every skill for free. Only
    /// PlayerSpawner assigns definition, so any player not created by it hits that path; the gym
    /// scene exists to guarantee there is exactly one, spawner-created player.</summary>
    private string SkillIdForSlot(int slot)
    {
        if (characterStats == null) return null;

        if (characterStats.definition == null)
        {
            Debug.LogError($"TopDownPlayerController on '{gameObject.name}': CharacterStats.definition is not assigned, " +
                           "so no skill slot can resolve its skill tree — every skill will behave as unlocked and the " +
                           "HUD's lock state will disagree with what the buttons actually do. This player must be " +
                           "spawned by PlayerSpawner (which assigns definition), not baked into the scene.");
            return null;
        }

        if (characterStats.definition.skillTrees == null) return null;
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

        // Every successful skill cast (all 3 slots) ends by calling this, right after its own
        // TriggerSkillN animation call — a single hook point instead of three duplicated ones.
        if (AudioDirector.Instance != null) AudioDirector.Instance.PlaySfx("SkillCast");
    }

    private void HandleSkills()
    {
        // Basic attack is handled separately (HandleAttack) and is never gated by the skill lock.
        if (SkillsLocked) return;

        // Check if skill buttons were pressed this frame
        if (skill1Action != null && skill1Action.WasPressedThisFrame())
        {
            string skillId = SkillIdForSlot(1);
            if (!IsSkillSlotUnlocked(1))
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
            if (!IsSkillSlotUnlocked(2))
            {
                Debug.Log($"[Player] Skill 2 ({skillId}) is locked — purchase its Unlock node in the skill tree.");
                return; // locked — not yet purchased in the skill tree
            }

            // Check if using a hitscan laser skill (e.g., finger beam)
            if (skill2LaserData != null && skill2LaserData.beamPrefab != null)
            {
                if (characterAnimator != null)
                {
                    characterAnimator.TriggerSkill2();
                }

                float beamDamage = characterStats != null
                    ? characterStats.GetSkillValue(skillId, SkillEffect.Damage, skill2LaserData.damage)
                    : skill2LaserData.damage;
                float beamRange = characterStats != null
                    ? characterStats.GetSkillValue(skillId, SkillEffect.Range, skill2LaserData.range)
                    : skill2LaserData.range;
                int pierce = characterStats != null
                    ? Mathf.Max(1, Mathf.RoundToInt(characterStats.GetSkillValue(skillId, SkillEffect.PierceCount, skill2LaserData.pierceCount)))
                    : skill2LaserData.pierceCount;

                StartCoroutine(FireLaserAfterDelay(skill2LaserData, beamDamage, beamRange, pierce));

                LockSkills(skillId, skill2LaserData.cooldown);
                return;
            }

            // Check if using projectile skill (e.g., shuriken throw)
            if (skill2ProjectileData != null && skill2ProjectileData.projectilePrefab != null)
            {
                if (characterAnimator != null)
                {
                    characterAnimator.TriggerSkill2();
                }

                // Upgrade rows for this skill: extra blades and bonus damage per blade. Both were
                // authored in the upgrade screen but previously never read here, so buying them
                // did nothing in game.
                int baseCount = skill2ProjectileData.spawnTimesNormalized != null
                    ? skill2ProjectileData.spawnTimesNormalized.Length
                    : 0;
                int projectileCount = characterStats != null
                    ? Mathf.Max(1, Mathf.RoundToInt(characterStats.GetSkillValue(skillId, SkillEffect.ProjectileCount, baseCount)))
                    : baseCount;
                float projectileDamage = characterStats != null
                    ? characterStats.GetSkillValue(skillId, SkillEffect.Damage, skill2ProjectileData.damage)
                    : skill2ProjectileData.damage;

                // Start timed projectile spawning
                StartCoroutine(SpawnProjectilesAtTimes(skill2ProjectileData, projectileCount, projectileDamage));

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
            if (!IsSkillSlotUnlocked(3))
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
    /// Spawns `count` projectiles during the skill animation, each dealing `damagePerProjectile`.
    /// The asset's authored spawn times cover the base count; extra projectiles bought from the
    /// upgrade screen are appended at an even spacing after the last authored time so a higher
    /// count throws more blades instead of silently reusing the same two.
    /// </summary>
    private IEnumerator SpawnProjectilesAtTimes(ProjectileSkillData skillData, int count, float damagePerProjectile)
    {
        if (skillData == null || skillData.projectilePrefab == null)
        {
            Debug.LogError($"TopDownPlayerController: ProjectileSkillData or projectilePrefab is null!");
            yield break;
        }

        float[] spawnTimes = BuildSpawnTimes(skillData, count);
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
                SpawnProjectile(skillData, skillDirection, damagePerProjectile);
                projectilesSpawned++;
            }
            
            yield return null;
        }
    }
    
    /// <summary>Authored spawn times, extended (or trimmed) to exactly `count` entries.</summary>
    private static float[] BuildSpawnTimes(ProjectileSkillData skillData, int count)
    {
        float[] authored = skillData.GetSpawnTimesInSeconds();
        count = Mathf.Max(1, count);
        if (authored.Length == count) return authored;
        if (count < authored.Length) return authored[..count];

        var times = new float[count];
        System.Array.Copy(authored, times, authored.Length);

        // Even spacing for the extras, matching the authored gap (or a tenth of the animation
        // when there is only one authored time to measure from).
        float last = authored.Length > 0 ? authored[^1] : 0f;
        float gap = authored.Length >= 2
            ? Mathf.Max(0.01f, authored[^1] - authored[^2])
            : Mathf.Max(0.01f, skillData.animationLength * 0.1f);

        for (int i = authored.Length; i < count; i++)
        {
            last += gap;
            times[i] = last;
        }
        return times;
    }

    /// <summary>
    /// Spawn a single projectile dealing `baseDamage` before the character's damage multiplier.
    /// </summary>
    private void SpawnProjectile(ProjectileSkillData skillData, Vector3 direction, float baseDamage)
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
                baseDamage * damageMultiplier,
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

    private bool warnedAboutMissingLaserMuzzleBone = false;

    /// <summary>
    /// Waits for the skill-2 animation's fireDelay, then fires the beam. The facing direction is
    /// captured at the start (like SpawnProjectilesAtTimes' skillDirection), so turning mid-cast
    /// does not curve the beam.
    /// </summary>
    private IEnumerator FireLaserAfterDelay(LaserBeamSkillData skillData, float damage, float range, int pierce)
    {
        if (skillData == null || skillData.beamPrefab == null)
        {
            Debug.LogError($"TopDownPlayerController: LaserBeamSkillData or beamPrefab is null!");
            yield break;
        }

        Vector3 direction = transform.forward;

        if (skillData.fireDelay > 0f)
        {
            yield return new WaitForSeconds(skillData.fireDelay);
        }

        if (this == null || !gameObject.activeInHierarchy || !IsAlive)
        {
            yield break;
        }

        Vector3 origin = ResolveLaserMuzzlePosition(skillData);
        Quaternion rot = Quaternion.LookRotation(direction);

        GameObject beamObj = Instantiate(skillData.beamPrefab, origin, rot);
        LaserBeam beam = beamObj.GetComponent<LaserBeam>();
        if (beam != null)
        {
            float damageMultiplier = combatController != null
                ? combatController.GetDamageMultiplier()
                : 1f;

            beam.Fire(
                origin,
                direction,
                range,
                damage * damageMultiplier,
                skillData.knockbackForce,
                pierce,
                skillData.visualDuration,
                skillData.beamWidth,
                gameObject,
                "Enemy"
            );
        }
        else
        {
            Debug.LogError($"TopDownPlayerController: Laser beam prefab '{skillData.beamPrefab.name}' has no LaserBeam script!");
            Destroy(beamObj);
        }
    }

    /// <summary>
    /// World position the laser fires from: the authored laserMuzzle transform if assigned,
    /// otherwise a name search for a "RightHand" bone, otherwise the capsule's grounded footprint
    /// plus LaserBeamSkillData.muzzleFallbackOffset. The rig has animationType Generic with no
    /// avatar, so Animator.GetBoneTransform cannot be used — this is a plain name search.
    /// </summary>
    private Vector3 ResolveLaserMuzzlePosition(LaserBeamSkillData skillData)
    {
        if (laserMuzzle != null)
        {
            return laserMuzzle.position;
        }

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "RightHand")
            {
                laserMuzzle = t;
                return t.position;
            }
        }

        if (!warnedAboutMissingLaserMuzzleBone)
        {
            warnedAboutMissingLaserMuzzleBone = true;
            Debug.LogWarning($"TopDownPlayerController on '{gameObject.name}': no laserMuzzle assigned and no " +
                              "'RightHand' bone found — laser beam falls back to a capsule-center offset " +
                              "(muzzleFallbackOffset), which reads as firing from the chest rather than the hand.");
        }

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        Vector3 basePos = capsule != null ? capsule.bounds.center : transform.position;
        return basePos + transform.TransformDirection(skillData.muzzleFallbackOffset);
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
        
        if (debugDamage) Debug.Log($"[Player] {gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
        
        // Apply knockback (horizontal only)
        if (knockbackForce > 0f && rb != null)
        {
            Vector3 horizontalKnockback = new Vector3(knockbackDirection.x, 0f, knockbackDirection.z).normalized;
            rb.AddForce(horizontalKnockback * knockbackForce, ForceMode.Impulse);
        }
        
        // Trigger hit animation
        characterAnimator?.TriggerHit();

        if (AudioDirector.Instance != null) AudioDirector.Instance.PlaySfx("Hit");

        // Check for death. Downed vs permanently dead is decided here, once, at the moment health
        // hits 0 — see EnterDowned/Die.
        if (currentHealth <= 0f)
        {
            bool anyTeammateAlive = GameSaveController.Instance != null && GameSaveController.Instance.AnyPlayerAlive();
            if (anyTeammateAlive)
            {
                EnterDowned();
            }
            else
            {
                Die();
            }
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
    /// Drops this player into the downed state: not IsAlive (so movement/attack/skills lock exactly
    /// like permanent death), but revivable by a teammate standing close for reviveDuration — see
    /// UpdateDownedState, called from Update while isDowned.
    ///
    /// Split out of Die() because the two states diverge immediately: Die() fires OnDeath (which
    /// PauseMenuController listens to for its all-dead game-over check) and disables the shared-asset
    /// input fallback for good, neither of which should happen to a player who can still come back.
    /// </summary>
    private void EnterDowned()
    {
        if (isDowned) return; // already down; a second lethal hit while downed is a no-op (see TakeDamage's IsAlive guard) but guard anyway for direct callers.

        isDowned = true;
        reviveProgress = 0f;

        if (battleCryRoutine != null)
        {
            StopCoroutine(battleCryRoutine);
            battleCryRoutine = null;
        }
        battleCrySpeedMultiplier = 1f;

        characterAnimator?.TriggerDeath(); // same "collapsed" pose as permanent death; TriggerArise reverses it on revive.
        OnDowned?.Invoke();
    }

    /// <summary>
    /// Called every frame this player is downed (see Update). Teammates in range make progress
    /// continuous, not banked — see reviveDuration's doc comment for why leaving range resets rather
    /// than pausing.
    /// </summary>
    private void UpdateDownedState()
    {
        if (!AnyTeammateInReviveRange())
        {
            reviveProgress = 0f;
            return;
        }

        reviveProgress += Time.deltaTime;
        if (reviveProgress >= reviveDuration)
        {
            Revive();
        }
    }

    /// <summary>
    /// Whether a living, non-downed teammate is currently within reviveRadius. Goes through
    /// GameSaveController's player registry rather than a physics overlap check — the registry
    /// already exists for enemy targeting (FindNearestLivingPlayer) and reusing it here keeps the
    /// project's no-lookup-fallbacks rule without adding a second way to enumerate players.
    /// </summary>
    private bool AnyTeammateInReviveRange()
    {
        if (GameSaveController.Instance == null) return false;

        float sqrRadius = reviveRadius * reviveRadius;
        foreach (TopDownPlayerController other in GameSaveController.Instance.PlayerControllers)
        {
            if (other == null || other == this) continue;
            if (!other.IsAlive) continue; // a downed or dead teammate cannot revive anyone.

            if ((other.transform.position - transform.position).sqrMagnitude <= sqrRadius) return true;
        }
        return false;
    }

    /// <summary>Brings a downed player back into the run at reviveHealthFraction of max health.</summary>
    private void Revive()
    {
        isDowned = false;
        reviveProgress = 0f;
        currentHealth = maxHealth * reviveHealthFraction;

        characterAnimator?.TriggerArise();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnRevived?.Invoke();
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

        if (AudioDirector.Instance != null) AudioDirector.Instance.PlaySfx("PlayerDeath");

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
        
        // Draw the map boundary the player is held inside
        if (enforceMapBounds && mapGenerator != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = mapGenerator.GetIslandCenter();
            center.y = transform.position.y;
            Gizmos.DrawWireSphere(center, mapGenerator.GetPlayableRadius());
        }
    }
}

