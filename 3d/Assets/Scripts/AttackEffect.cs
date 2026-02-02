using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attach to attack effect prefabs. Handles collision-based hit detection
/// and auto-destruction after the particle effect completes.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AttackEffect : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tag of targets this effect can hit")]
    [SerializeField] private string targetTag = "Enemy";
    
    [Tooltip("Can this effect hit multiple targets?")]
    [SerializeField] private bool canHitMultiple = true;
    
    [Header("Hit Detection")]
    [Tooltip("Also check OnTriggerStay (catches enemies already inside collider)")]
    [SerializeField] private bool useStayDetection = true;
    
    [Tooltip("Do an initial overlap sphere check when spawned (radius based on collider bounds)")]
    [SerializeField] private bool doInitialOverlapCheck = true;
    
    [Tooltip("Layer mask for overlap checks (default: all layers)")]
    [SerializeField] private LayerMask hitLayerMask = -1;
    
    [Header("Debug")]
    [SerializeField] private bool debugHits = false;
    
    [Tooltip("Show visible collider area during gameplay (red = hit area)")]
    [SerializeField] private bool showDebugVisual = false;
    
    [Tooltip("Color of the debug visualization")]
    [SerializeField] private Color debugColor = new Color(1f, 0f, 0f, 0.4f);
    
    // Set by CombatController when spawning
    private float damage;
    private float knockbackForce;
    private float knockbackMultiplier = 1f;
    private Vector3 attackDirection;
    private GameObject attacker;
    private float hitWindowDuration;
    
    private HashSet<GameObject> hitTargets = new HashSet<GameObject>();
    private ParticleSystem particles;
    private Collider hitCollider;
    private GameObject debugVisualObj;
    private bool isInitialized = false;
    private bool colliderDisabled = false;
    
    /// <summary>
    /// Initialize the attack effect with combat data
    /// </summary>
    public void Initialize(float damage, float knockbackForce, float knockbackMultiplier, 
                          Vector3 attackDirection, GameObject attacker, float hitWindowDuration)
    {
        this.damage = damage;
        this.knockbackForce = knockbackForce;
        this.knockbackMultiplier = knockbackMultiplier;
        this.attackDirection = attackDirection;
        this.attacker = attacker;
        this.hitWindowDuration = hitWindowDuration;
        this.isInitialized = true;
        
        // Get particle system
        particles = GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = GetComponentInChildren<ParticleSystem>();
        }
        
        // Ensure collider is trigger
        hitCollider = GetComponent<Collider>();
        if (hitCollider != null)
        {
            hitCollider.isTrigger = true;
        }
        
        // Schedule collider disable after hit window (collider becomes inactive, but visual continues)
        float hitWindow = hitWindowDuration > 0f ? hitWindowDuration : 0.15f;
        Invoke(nameof(DisableCollider), hitWindow);
        
        // Schedule destruction after particle finishes (visual lifetime, separate from hit window)
        float visualLifetime = 1f; // Default fallback
        if (particles != null)
        {
            visualLifetime = particles.main.duration + particles.main.startLifetime.constantMax;
        }
        // Ensure we don't destroy before hit window ends
        visualLifetime = Mathf.Max(visualLifetime, hitWindow + 0.1f);
        
        Destroy(gameObject, visualLifetime);
        
        if (debugHits)
        {
            Debug.Log($"[AttackEffect] Initialized - Damage: {damage}, Knockback: {knockbackForce}, Hit Window: {hitWindow}s, Visual Lifetime: {visualLifetime}s");
        }
        
        // Create debug visualization if enabled
        if (showDebugVisual && hitCollider != null)
        {
            CreateDebugVisual();
        }
        
        // Do initial overlap check to catch enemies already in range
        if (doInitialOverlapCheck && hitCollider != null)
        {
            PerformOverlapCheck();
        }
    }
    
    /// <summary>
    /// Disable the collider after hit window expires (visual continues playing)
    /// </summary>
    private void DisableCollider()
    {
        colliderDisabled = true;
        
        if (hitCollider != null)
        {
            hitCollider.enabled = false;
        }
        
        // Also hide debug visual
        if (debugVisualObj != null)
        {
            debugVisualObj.SetActive(false);
        }
        
        if (debugHits)
        {
            Debug.Log($"[AttackEffect] Hit window ended - collider disabled");
        }
    }
    
    /// <summary>
    /// Create a visible debug mesh to show the hit area during gameplay
    /// </summary>
    private void CreateDebugVisual()
    {
        GameObject debugObj = new GameObject("DebugHitArea");
        debugObj.transform.SetParent(transform);
        debugObj.transform.localPosition = Vector3.zero;
        debugObj.transform.localRotation = Quaternion.identity;
        
        MeshFilter meshFilter = debugObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = debugObj.AddComponent<MeshRenderer>();
        
        // Determine mesh type based on collider
        if (hitCollider is SphereCollider sphere)
        {
            meshFilter.mesh = CreateSphereMesh();
            float diameter = sphere.radius * 2f;
            debugObj.transform.localScale = new Vector3(diameter, diameter, diameter);
            debugObj.transform.localPosition = sphere.center;
        }
        else if (hitCollider is BoxCollider box)
        {
            meshFilter.mesh = CreateCubeMesh();
            debugObj.transform.localScale = box.size;
            debugObj.transform.localPosition = box.center;
        }
        else if (hitCollider is CapsuleCollider capsule)
        {
            meshFilter.mesh = CreateCapsuleMesh();
            float diameter = capsule.radius * 2f;
            debugObj.transform.localScale = new Vector3(diameter, capsule.height, diameter);
            debugObj.transform.localPosition = capsule.center;
        }
        else
        {
            // Fallback: use bounds
            meshFilter.mesh = CreateCubeMesh();
            debugObj.transform.position = hitCollider.bounds.center;
            debugObj.transform.localScale = hitCollider.bounds.size;
        }
        
        // Create transparent material
        Material debugMat = new Material(Shader.Find("Sprites/Default"));
        debugMat.color = debugColor;
        meshRenderer.material = debugMat;
        
        // Disable shadows
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        
        // Save reference so we can hide it when collider is disabled
        debugVisualObj = debugObj;
    }
    
    private Mesh CreateCubeMesh()
    {
        // Use Unity's built-in cube
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = temp.GetComponent<MeshFilter>().mesh;
        Destroy(temp);
        return mesh;
    }
    
    private Mesh CreateSphereMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh mesh = temp.GetComponent<MeshFilter>().mesh;
        Destroy(temp);
        return mesh;
    }
    
    private Mesh CreateCapsuleMesh()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Mesh mesh = temp.GetComponent<MeshFilter>().mesh;
        Destroy(temp);
        return mesh;
    }
    
    private void Update()
    {
        // Draw debug lines in Scene view when debug is enabled
        if (debugHits && hitCollider != null)
        {
            DrawDebugBounds();
        }
    }
    
    /// <summary>
    /// Draw collider bounds using Debug.DrawLine (visible in Scene view with Gizmos on)
    /// </summary>
    private void DrawDebugBounds()
    {
        Bounds b = hitCollider.bounds;
        Color c = debugColor;
        
        // Bottom face
        Debug.DrawLine(new Vector3(b.min.x, b.min.y, b.min.z), new Vector3(b.max.x, b.min.y, b.min.z), c);
        Debug.DrawLine(new Vector3(b.max.x, b.min.y, b.min.z), new Vector3(b.max.x, b.min.y, b.max.z), c);
        Debug.DrawLine(new Vector3(b.max.x, b.min.y, b.max.z), new Vector3(b.min.x, b.min.y, b.max.z), c);
        Debug.DrawLine(new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.min.x, b.min.y, b.min.z), c);
        
        // Top face
        Debug.DrawLine(new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.max.x, b.max.y, b.min.z), c);
        Debug.DrawLine(new Vector3(b.max.x, b.max.y, b.min.z), new Vector3(b.max.x, b.max.y, b.max.z), c);
        Debug.DrawLine(new Vector3(b.max.x, b.max.y, b.max.z), new Vector3(b.min.x, b.max.y, b.max.z), c);
        Debug.DrawLine(new Vector3(b.min.x, b.max.y, b.max.z), new Vector3(b.min.x, b.max.y, b.min.z), c);
        
        // Vertical edges
        Debug.DrawLine(new Vector3(b.min.x, b.min.y, b.min.z), new Vector3(b.min.x, b.max.y, b.min.z), c);
        Debug.DrawLine(new Vector3(b.max.x, b.min.y, b.min.z), new Vector3(b.max.x, b.max.y, b.min.z), c);
        Debug.DrawLine(new Vector3(b.max.x, b.min.y, b.max.z), new Vector3(b.max.x, b.max.y, b.max.z), c);
        Debug.DrawLine(new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.min.x, b.max.y, b.max.z), c);
    }
    
    /// <summary>
    /// Perform an overlap sphere check to find targets already in range
    /// </summary>
    private void PerformOverlapCheck()
    {
        // Use collider bounds to determine check radius
        float radius = Mathf.Max(hitCollider.bounds.extents.x, hitCollider.bounds.extents.z);
        Vector3 center = hitCollider.bounds.center;
        
        Collider[] hits = Physics.OverlapSphere(center, radius, hitLayerMask);
        
        if (debugHits)
        {
            Debug.Log($"[AttackEffect] Initial overlap check at {center} with radius {radius}: found {hits.Length} colliders");
        }
        
        foreach (Collider hit in hits)
        {
            TryHitTarget(hit);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        TryHitTarget(other);
    }
    
    private void OnTriggerStay(Collider other)
    {
        // OnTriggerStay catches enemies that were already inside the collider when it spawned
        if (useStayDetection)
        {
            TryHitTarget(other);
        }
    }
    
    /// <summary>
    /// Attempt to hit a target (shared logic for OnTriggerEnter, OnTriggerStay, and overlap checks)
    /// </summary>
    private void TryHitTarget(Collider other)
    {
        if (!isInitialized) return;
        
        // Check tag
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag))
        {
            return;
        }
        
        // Skip if already hit this target
        if (hitTargets.Contains(other.gameObject))
        {
            return;
        }
        
        // Don't hit the attacker
        if (attacker != null && other.gameObject == attacker)
        {
            return;
        }
        
        // Mark as hit
        hitTargets.Add(other.gameObject);
        
        // Calculate knockback direction (from attacker to target, horizontal only)
        Vector3 knockbackDir;
        if (attacker != null)
        {
            knockbackDir = (other.transform.position - attacker.transform.position);
            knockbackDir.y = 0f;
            knockbackDir = knockbackDir.normalized;
        }
        else
        {
            knockbackDir = attackDirection;
            knockbackDir.y = 0f;
            knockbackDir = knockbackDir.normalized;
        }
        
        float totalKnockback = knockbackForce * knockbackMultiplier;
        
        // Try to hit Enemy script
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, knockbackDir, totalKnockback);
            
            if (debugHits)
            {
                Debug.Log($"[AttackEffect] ✓ Hit enemy {other.gameObject.name} for {damage} damage!");
            }
            
            if (!canHitMultiple) Destroy(gameObject);
            return;
        }
        
        // Try to hit Player script
        TopDownPlayerController player = other.GetComponent<TopDownPlayerController>();
        if (player != null)
        {
            player.TakeDamage(damage, knockbackDir, totalKnockback);
            
            if (debugHits)
            {
                Debug.Log($"[AttackEffect] ✓ Hit player {other.gameObject.name} for {damage} damage!");
            }
            
            if (!canHitMultiple) Destroy(gameObject);
            return;
        }

        // Fallback: apply force to rigidbody
        Rigidbody targetRb = other.GetComponent<Rigidbody>();
        if (targetRb != null && !targetRb.isKinematic)
        {
            targetRb.AddForce(knockbackDir * totalKnockback, ForceMode.Impulse);
            
            if (debugHits)
            {
                Debug.Log($"[AttackEffect] Applied knockback to {other.gameObject.name} (no Enemy/Player script)");
            }
            
            if (!canHitMultiple) Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Set target tag (called by CombatController)
    /// </summary>
    public void SetTargetTag(string tag)
    {
        targetTag = tag;
    }
    
    private void OnDrawGizmos()
    {
        // Show collider bounds in red so you can see the actual hit area
        Collider col = hitCollider != null ? hitCollider : GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            
            // Also draw sphere to show overlap check radius
            if (doInitialOverlapCheck)
            {
                float radius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawWireSphere(col.bounds.center, radius);
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // More visible when selected
        Collider col = hitCollider != null ? hitCollider : GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
    }
}
