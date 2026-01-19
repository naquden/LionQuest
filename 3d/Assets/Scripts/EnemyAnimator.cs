using UnityEngine;

/// <summary>
/// Handles enemy animation states based on enemy behavior.
/// Automatically detects which animation parameters exist in the Animator.
/// </summary>
[RequireComponent(typeof(Animator))]
public class EnemyAnimator : MonoBehaviour
{
    [Header("Animation Parameter Names")]
    [SerializeField] private string movingParam = "IsMoving";
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string deathTrigger = "Death";
    [SerializeField] private string deadParam = "IsDead";
    
    private Animator animator;
    private Rigidbody rb;
    private Vector3 lastPos;
    
    // Track which parameters exist
    private bool hasMovingParam;
    private bool hasSpeedParam;
    private bool hasAttackTrigger;
    private bool hasHitTrigger;
    private bool hasDeathTrigger;
    private bool hasDeadParam;
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
        // Changed to GetComponentInParent to handle case where script is on Child Model but RB is on Root
        rb = GetComponentInParent<Rigidbody>(); 
        
        if (rb == null) 
        {
            Debug.LogError($"[EnemyAnimator] CRITICAL: No Rigidbody found on {gameObject.name} or parents! Animation will not work.");
        }

        // Check which parameters exist in the animator
        DetectParameters();
    }
    
    /// <summary>
    /// Detect which animation parameters exist in the animator controller
    /// </summary>
    private void DetectParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"[EnemyAnimator] Animator or Controller missing on {gameObject.name}");
            return;
        }
        
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == movingParam) hasMovingParam = true;
            else if (param.name == speedParam) hasSpeedParam = true;
            else if (param.name == attackTrigger) hasAttackTrigger = true;
            else if (param.name == hitTrigger) hasHitTrigger = true;
            else if (param.name == deathTrigger) hasDeathTrigger = true;
            else if (param.name == deadParam) hasDeadParam = true;

            // Debug help for typos
            if (!hasSpeedParam && param.name.ToLower() == speedParam.ToLower())
            {
                Debug.LogWarning($"[EnemyAnimator] Case mismatch! Found '{param.name}' but script expects '{speedParam}'.");
            }
        }
    }
    
    private void Update()
    {
        if (rb == null) return;

        // Physics speed
        float physicsSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        
        // Manual speed
        float manualSpeed = 0f;
        if (Time.deltaTime > 0f)
        {
            manualSpeed = (transform.position - lastPos).magnitude / Time.deltaTime;
        }
        lastPos = transform.position;

        float effectiveSpeed = Mathf.Max(physicsSpeed, manualSpeed);

        if (hasSpeedParam)
        {
            animator.SetFloat(speedParam, effectiveSpeed);
        }
    }
    
    /// <summary>
    /// Set whether the enemy is moving
    /// </summary>
    public void SetMoving(bool isMoving)
    {
        if (hasMovingParam)
        {
            animator.SetBool(movingParam, isMoving);
        }
    }
    
    /// <summary>
    /// Trigger attack animation
    /// </summary>
    public void TriggerAttack()
    {
        if (hasAttackTrigger)
        {
            animator.SetTrigger(attackTrigger);
        }
    }
    
    /// <summary>
    /// Trigger hit/damage animation
    /// </summary>
    public void TriggerHit()
    {
        if (hasHitTrigger)
        {
            animator.SetTrigger(hitTrigger);
        }
    }
    
    /// <summary>
    /// Trigger death animation
    /// </summary>
    public void TriggerDeath()
    {
        if (hasDeadParam)
        {
            animator.SetBool(deadParam, true);
        }
        if (hasDeathTrigger)
        {
            animator.SetTrigger(deathTrigger);
        }
    }
}
