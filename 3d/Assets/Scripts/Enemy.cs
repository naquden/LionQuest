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

    [Header("Debug")]
    [Tooltip("Log every hit taken. Off by default — a full wave clear is dozens of enemies each " +
             "taking several hits, and logging every one spams the console badly enough to slow " +
             "down headless test runs.")]
    [SerializeField] private bool debugDamage = false;
    
    [Header("Movement")]
    [SerializeField] private float pullForce = 5f;
    [SerializeField] private float pullRange = 15f;
    [SerializeField] private float stopDistance = 1.5f; // Stop when close to attack
    [SerializeField] private float maxPullVelocity = 3f;

    [Tooltip("Seconds between re-picking which player to chase. Not every frame: with four players " +
             "and a full arena of enemies that is thousands of distance checks a second for a target " +
             "that barely changes. Staggered per enemy so they don't all retarget on the same frame.")]
    [SerializeField] private float retargetInterval = 0.5f;

    [Header("Separation")]
    [Tooltip("Enemies within this distance of each other push apart. All of them chase the same point " +
             "(the player), so without this every enemy in a mob converges on the exact same " +
             "coordinate and they end up stacked inside one another — which also makes CombatController " +
             "hit detection unreliable, since overlapping colliders confuse which target a swing lands " +
             "on. Roughly two capsule widths (enemy radius 0.5), so bodies push apart before they " +
             "visibly overlap.")]
    [SerializeField] private float separationRadius = 1.2f;

    [SerializeField] private float separationForce = 8f;

    /// <summary>
    /// Enemies re-use one shared buffer for their OverlapSphere separation check rather than each
    /// allocating their own, since only one enemy's FixedUpdate runs at a time (Unity's physics step
    /// is single-threaded per this component) and a fresh array every enemy every physics frame is
    /// real GC pressure with a full arena.
    /// </summary>
    private static readonly Collider[] SeparationBuffer = new Collider[16];


    [Header("Combat")]
    [SerializeField] private AttackData attackData;
    [SerializeField] private float attackCooldown = 2f;
    private float lastAttackTime;

    [Header("Ranged Attack (optional)")]
    [Tooltip("If assigned, Attack() fires this projectile instead of the melee AttackEffect — turns " +
             "this archetype into a ranged attacker. The projectile prefab needs a Projectile " +
             "component (Assets/Scripts/Projectile.cs); damage/knockback still come from attackData, " +
             "so the same asset drives both the animation trigger and the numbers regardless of which " +
             "attack path fires. Leave null for a melee archetype.")]
    [SerializeField] private GameObject rangedProjectilePrefab;

    [Tooltip("World-space speed of the fired projectile.")]
    [SerializeField] private float rangedProjectileSpeed = 12f;

    [Tooltip("Vertical offset the projectile spawns at, relative to this enemy's feet. Spawning at " +
             "foot height sends it straight into the ground; players' capsules sit roughly waist-high.")]
    [SerializeField] private float rangedMuzzleHeight = 1f;

    [Header("Death")]
    [Tooltip("Seconds a corpse lingers before being destroyed. Without this, every enemy killed in a " +
             "run stays in the scene forever — in a full arena clear that is dozens of dead bodies " +
             "still sitting on the island, still counted by any FindObjectsByType<Enemy> sweep, and " +
             "still taking up physics/render cost for the rest of the session.")]
    [SerializeField] private float corpseLifetime = 5f;

    private Rigidbody rb;
    private bool isAlive = true;
    private TopDownPlayerController targetPlayer;
    private float nextRetargetTime;
    private float retargetJitter = 1f;

    /// <summary>
    /// How long the player registry is allowed to not exist yet before that becomes an error. Covers
    /// scene startup, where PlayerSpawner creates the GameController in its own Start and Unity gives
    /// no ordering guarantee against this enemy's Update.
    /// </summary>
    const float RegistryGraceSeconds = 2f;
    private float registryGraceDeadline;
    private bool registryErrorLogged;
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

        // Per-enemy phase offset, so a wave that spawns on a single frame does not then re-scan the
        // player list on the same frame forever — that turns a cheap amortised cost into a periodic
        // spike. Multiplicative rather than an absolute Awake offset because the first retarget
        // happens immediately (no target yet), which would overwrite any absolute stagger.
        retargetJitter = Random.Range(0.75f, 1.25f);

        registryGraceDeadline = Time.time + RegistryGraceSeconds;

        // Ensure Root Motion is off, as it overrides physics
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
        }
    }
    
    /// <summary>
    /// Re-picks the nearest living player when the retarget timer is up, and drops a target that has
    /// died or been destroyed immediately.
    ///
    /// This replaces a one-shot GameObject.FindGameObjectWithTag("Player") in Start, which was the
    /// single worst co-op bug in the game: the tag lookup returns whichever tagged object Unity finds
    /// first, so in a four-player run *every* enemy latched onto the same player and the other three
    /// could walk through the horde untouched. It also never re-ran, so killing that player left the
    /// whole level standing still. Targeting now goes through GameSaveController's player registry,
    /// which also satisfies the project's no-lookup-fallbacks rule.
    /// </summary>
    private void UpdateTarget()
    {
        // A dead target is dropped at once rather than at the next interval — otherwise an enemy keeps
        // swinging at a corpse for up to retargetInterval seconds.
        if (targetPlayer != null && !targetPlayer.IsAlive) targetPlayer = null;

        if (targetPlayer != null && Time.time < nextRetargetTime) return;

        nextRetargetTime = Time.time + retargetInterval * retargetJitter;

        if (GameSaveController.Instance == null)
        {
            // Absence is normal for the first few frames — PlayerSpawner creates the GameController in
            // its own Start, and Unity does not order that against this enemy's Update — so nothing is
            // reported until the grace window closes. An enemy that then stands still forever reads as
            // a movement bug rather than as missing wiring, hence the report at all.
            //
            // Warning, not error: the singleton is DontDestroyOnLoad and never vanishes mid-run in
            // game, but the PlayMode harness destroys it in TearDown while a scene full of enemies is
            // still ticking. As an error that noise failed whichever test ran next. The build-time
            // version of this check is the LogError in Level1Generator/AgentGymGenerator when the
            // GameController prefab is missing, which is where a real wiring mistake actually surfaces.
            if (!registryErrorLogged && Time.time > registryGraceDeadline)
            {
                registryErrorLogged = true;
                Debug.LogWarning($"[Enemy] {gameObject.name}: GameSaveController.Instance is still missing " +
                                 $"{RegistryGraceSeconds}s in, so there is no player registry to target. " +
                                 "This enemy will never chase anyone — the scene is missing its " +
                                 "GameController prefab, or PlayerSpawner failed to instantiate it.");
            }
            return;
        }

        targetPlayer = GameSaveController.Instance.FindNearestLivingPlayer(transform.position);
    }

    private void Update()
    {
        if (!isAlive) return;

        UpdateTarget();
        if (targetPlayer == null) return;

        float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);

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

        if (rangedProjectilePrefab != null)
        {
            FireProjectile();
        }
        else if (combatController != null && attackData != null)
        {
            combatController.PerformAttack(attackData);
        }
    }

    /// <summary>
    /// Ranged archetype's attack: reuses the same Projectile component the player's shuriken skill
    /// fires (Assets/Scripts/Projectile.cs), aimed at the current target. Damage/knockback come from
    /// attackData rather than duplicated fields, so tuning an archetype's numbers is one asset either
    /// way — the animation trigger above and this both read it.
    /// </summary>
    private void FireProjectile()
    {
        if (targetPlayer == null) return;

        Vector3 origin = transform.position + Vector3.up * rangedMuzzleHeight;
        Vector3 direction = targetPlayer.transform.position + Vector3.up * rangedMuzzleHeight - origin;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

        GameObject projectileObj = Instantiate(rangedProjectilePrefab, origin, Quaternion.LookRotation(direction));
        Projectile projectile = projectileObj.GetComponent<Projectile>();
        if (projectile == null)
        {
            Debug.LogError($"[Enemy] {gameObject.name}: rangedProjectilePrefab '{rangedProjectilePrefab.name}' has no Projectile component.");
            Destroy(projectileObj);
            return;
        }

        float damage = attackData != null ? attackData.damage : 0f;
        float knockback = attackData != null ? attackData.knockbackForce : 0f;
        projectile.Initialize(damage, knockback, direction, gameObject);
        projectile.SetSpeed(rangedProjectileSpeed);
        projectile.SetTargetTag("Player");
    }

    private void FixedUpdate()
    {
        if (!isAlive) return;

        ApplySeparationForce();

        if (targetPlayer == null) return;

        Vector3 directionToPlayer = targetPlayer.transform.position - transform.position;
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
    /// Pushes this enemy away from other living enemies standing inside separationRadius. Runs even
    /// with no target so a mob still spreads out while idle, not only while chasing.
    /// </summary>
    private void ApplySeparationForce()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, SeparationBuffer);
        // OverlapSphereNonAlloc silently drops hits past the buffer's length instead of erroring, so a
        // full buffer means the true count is unknown, not zero — worth knowing about if arenas grow
        // past the density this was sized for.
        if (count >= SeparationBuffer.Length)
        {
            Debug.LogWarning($"[Enemy] {gameObject.name}: separation overlap filled its {SeparationBuffer.Length}-slot " +
                             "buffer — some nearby enemies were not considered. Raise SeparationBuffer's size.");
        }

        Vector3 push = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider other = SeparationBuffer[i];
            if (other.gameObject == gameObject) continue;

            Enemy otherEnemy = other.GetComponent<Enemy>();
            if (otherEnemy == null || !otherEnemy.IsAlive) continue;

            Vector3 offset = transform.position - other.transform.position;
            offset.y = 0f;
            float dist = offset.magnitude;
            if (dist < 0.001f)
            {
                // Exactly coincident (e.g. spawned on the same point) — pick an arbitrary direction
                // rather than dividing by zero and doing nothing forever.
                offset = Random.insideUnitSphere;
                offset.y = 0f;
                dist = 0.001f;
            }

            push += offset.normalized * (1f - dist / separationRadius);
        }

        if (push.sqrMagnitude > 0.0001f)
        {
            rb.AddForce(push * separationForce, ForceMode.Force);
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
        
        if (debugDamage) Debug.Log($"[Enemy] {gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
        
        // Apply knockback (horizontal only) - same as Hittable
        if (knockbackForce > 0f && rb != null)
        {
            Vector3 force = new Vector3(knockbackDirection.x, 0f, knockbackDirection.z).normalized * knockbackForce;
            rb.AddForce(force, ForceMode.Impulse);
            
            // Debug.Log($"[Enemy] Knockback force: {force} | Mass: {rb.mass} | Drag: {rb.linearDamping} | IsKinematic: {rb.isKinematic}");
        }

        // Trigger hit animation
        if (enemyAnimator != null) enemyAnimator.TriggerHit();

        if (AudioDirector.Instance != null) AudioDirector.Instance.PlaySfx("Hit");

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

        if (AudioDirector.Instance != null) AudioDirector.Instance.PlaySfx("EnemyDeath");
        
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

        // corpseLifetime <= 0 keeps the old forever-corpse behaviour, for anything (a screenshot rig,
        // a future boss with a death cutscene) that wants the body to stick around on purpose.
        if (corpseLifetime > 0f)
        {
            Destroy(gameObject, corpseLifetime);
        }
    }
}
