using UnityEngine;

/// <summary>
/// Enhanced animation controller for Unity 6 that handles all character animation states
/// Supports: Idle, Walk, Attack, Die, Arise, and more
/// Attach this to a GameObject with an Animator component
/// </summary>
[RequireComponent(typeof(Animator))]
public class CharacterAnimator : MonoBehaviour
{
    [Header("Movement Parameters")]
    [Tooltip("Name of the float parameter for movement speed (0 = idle, >0 = walking)")]
    [SerializeField] private string speedParameter = "Speed";
    
    [Tooltip("Name of the boolean parameter for walking state (optional, alternative to Speed)")]
    [SerializeField] private string isWalkingParameter = "IsWalking";
    
    [Tooltip("Name of the boolean parameter for sprinting state (optional)")]
    [SerializeField] private string isSprintingParameter = "IsSprinting";
    
    [Header("Combat Parameters")]
    [Tooltip("Name of the trigger parameter for attack animation")]
    [SerializeField] private string attackTriggerParameter = "Attack";
    
    [Tooltip("Name of the integer parameter for attack type (0 = light, 1 = heavy, etc.)")]
    [SerializeField] private string attackTypeParameter = "AttackType";
    
    [Header("State Parameters")]
    [Tooltip("Name of the trigger parameter for death animation")]
    [SerializeField] private string dieTriggerParameter = "Die";
    
    [Tooltip("Name of the trigger parameter for arise/respawn animation")]
    [SerializeField] private string ariseTriggerParameter = "Arise";
    
    [Tooltip("Name of the boolean parameter for death state")]
    [SerializeField] private string isDeadParameter = "IsDead";
    
    [Header("Debug")]
    [Tooltip("Enable to see animation parameter updates in console")]
    [SerializeField] private bool debugLog = false;
    
    private Animator animator;
    
    // Parameter existence flags
    private bool hasSpeedParam;
    private bool hasIsWalkingParam;
    private bool hasIsSprintingParam;
    private bool hasAttackTriggerParam;
    private bool hasAttackTypeParam;
    private bool hasDieTriggerParam;
    private bool hasAriseTriggerParam;
    private bool hasIsDeadParam;
    
    // State tracking
    private bool isDead = false;
    private bool isAttacking = false;
    
    /// <summary>
    /// Returns whether the character is currently dead
    /// </summary>
    public bool IsDead => isDead;
    
    /// <summary>
    /// Returns whether the character is currently attacking
    /// </summary>
    public bool IsAttacking => isAttacking;
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
        
        if (animator == null)
        {
            Debug.LogError($"CharacterAnimator on '{gameObject.name}': Animator component is missing!");
            return;
        }
        
        // Check which parameters exist in the Animator Controller
        hasSpeedParam = HasParameter(speedParameter, AnimatorControllerParameterType.Float);
        hasIsWalkingParam = HasParameter(isWalkingParameter, AnimatorControllerParameterType.Bool);
        hasIsSprintingParam = HasParameter(isSprintingParameter, AnimatorControllerParameterType.Bool);
        hasAttackTriggerParam = HasParameter(attackTriggerParameter, AnimatorControllerParameterType.Trigger);
        hasAttackTypeParam = HasParameter(attackTypeParameter, AnimatorControllerParameterType.Int);
        hasDieTriggerParam = HasParameter(dieTriggerParameter, AnimatorControllerParameterType.Trigger);
        hasAriseTriggerParam = HasParameter(ariseTriggerParameter, AnimatorControllerParameterType.Trigger);
        hasIsDeadParam = HasParameter(isDeadParameter, AnimatorControllerParameterType.Bool);
        
        // Log available parameters for debugging
        if (debugLog)
        {
            LogAvailableParameters();
        }
    }
    
    /// <summary>
    /// Updates movement-based animations (Idle, Walk, Sprint)
    /// Call this from your movement controller each frame
    /// </summary>
    /// <param name="isMoving">Whether the character is currently moving</param>
    /// <param name="speed">Current movement speed (0-1 normalized or actual speed)</param>
    /// <param name="isSprinting">Whether the character is sprinting</param>
    public void UpdateAnimations(bool isMoving, float speed = 0f, bool isSprinting = false)
    {
        if (animator == null || !animator.enabled || isDead)
        {
            return; // Don't update movement animations if dead
        }
        
        // Set speed parameter (preferred method for smooth blending)
        if (hasSpeedParam)
        {
            animator.SetFloat(speedParameter, speed);
            
            if (debugLog)
            {
                Debug.Log($"CharacterAnimator: Setting Speed = {speed} (isMoving: {isMoving})");
            }
        }
        else if (debugLog)
        {
            Debug.LogWarning($"CharacterAnimator: Speed parameter '{speedParameter}' not found in Animator Controller!");
        }
        
        // Set walking state (alternative method)
        if (hasIsWalkingParam)
        {
            animator.SetBool(isWalkingParameter, isMoving);
        }
        
        // Set sprinting state
        if (hasIsSprintingParam)
        {
            animator.SetBool(isSprintingParameter, isSprinting);
        }
    }
    
    /// <summary>
    /// Triggers an attack animation
    /// </summary>
    /// <param name="attackType">Type of attack (0 = light, 1 = heavy, etc.)</param>
    public void TriggerAttack(int attackType = 0)
    {
        if (animator == null || !animator.enabled || isDead)
        {
            return; // Can't attack if dead
        }
        
        // Set attack type if parameter exists
        if (hasAttackTypeParam)
        {
            animator.SetInteger(attackTypeParameter, attackType);
        }
        
        // Trigger attack animation
        if (hasAttackTriggerParam)
        {
            animator.SetTrigger(attackTriggerParameter);
            isAttacking = true;
            
            if (debugLog)
            {
                Debug.Log($"CharacterAnimator: Triggered attack (type: {attackType})");
            }
        }
        else if (debugLog)
        {
            Debug.LogWarning($"CharacterAnimator: Attack trigger parameter '{attackTriggerParameter}' not found in Animator Controller!");
        }
    }
    
    /// <summary>
    /// Called when attack animation finishes (should be called from animation event or state machine)
    /// </summary>
    public void OnAttackComplete()
    {
        isAttacking = false;
    }
    
    /// <summary>
    /// Triggers the death animation
    /// </summary>
    public void TriggerDeath()
    {
        if (animator == null || !animator.enabled || isDead)
        {
            return; // Already dead
        }
        
        isDead = true;
        
        // Set dead state
        if (hasIsDeadParam)
        {
            animator.SetBool(isDeadParameter, true);
        }
        
        // Trigger death animation
        if (hasDieTriggerParam)
        {
            animator.SetTrigger(dieTriggerParameter);
            
            if (debugLog)
            {
                Debug.Log("CharacterAnimator: Triggered death");
            }
        }
        else if (debugLog)
        {
            Debug.LogWarning($"CharacterAnimator: Die trigger parameter '{dieTriggerParameter}' not found in Animator Controller!");
        }
    }
    
    /// <summary>
    /// Triggers the arise/respawn animation and resets death state
    /// </summary>
    public void TriggerArise()
    {
        if (animator == null || !animator.enabled)
        {
            return;
        }
        
        isDead = false;
        isAttacking = false;
        
        // Reset dead state
        if (hasIsDeadParam)
        {
            animator.SetBool(isDeadParameter, false);
        }
        
        // Trigger arise animation
        if (hasAriseTriggerParam)
        {
            animator.SetTrigger(ariseTriggerParameter);
            
            if (debugLog)
            {
                Debug.Log("CharacterAnimator: Triggered arise");
            }
        }
        else if (debugLog)
        {
            Debug.LogWarning($"CharacterAnimator: Arise trigger parameter '{ariseTriggerParameter}' not found in Animator Controller!");
        }
    }
    
    /// <summary>
    /// Resets all animation states (useful for respawning or resetting)
    /// </summary>
    public void ResetAnimator()
    {
        if (animator == null || !animator.enabled)
        {
            return;
        }
        
        isDead = false;
        isAttacking = false;
        
        // Reset all parameters
        if (hasSpeedParam)
        {
            animator.SetFloat(speedParameter, 0f);
        }
        
        if (hasIsWalkingParam)
        {
            animator.SetBool(isWalkingParameter, false);
        }
        
        if (hasIsSprintingParam)
        {
            animator.SetBool(isSprintingParameter, false);
        }
        
        if (hasIsDeadParam)
        {
            animator.SetBool(isDeadParameter, false);
        }
        
        if (hasAttackTypeParam)
        {
            animator.SetInteger(attackTypeParameter, 0);
        }
        
        // Reset triggers (triggers auto-reset, but we can force it)
        animator.ResetTrigger(attackTriggerParameter);
        animator.ResetTrigger(dieTriggerParameter);
        animator.ResetTrigger(ariseTriggerParameter);
    }
    
    /// <summary>
    /// Checks if a parameter exists in the Animator Controller
    /// </summary>
    private bool HasParameter(string paramName, AnimatorControllerParameterType paramType)
    {
        if (string.IsNullOrEmpty(paramName) || animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }
        
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName && param.type == paramType)
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Logs all available parameters in the Animator Controller (for debugging)
    /// </summary>
    private void LogAvailableParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("CharacterAnimator: No Animator Controller assigned!");
            return;
        }
        
        Debug.Log($"CharacterAnimator: Available parameters in '{animator.runtimeAnimatorController.name}':");
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            Debug.Log($"  - {param.name} ({param.type})");
        }
    }
}
