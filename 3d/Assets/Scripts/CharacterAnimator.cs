using UnityEngine;

/// <summary>
/// Handles animation parameters for the character based on movement state
/// Attach this to a GameObject with an Animator component
/// </summary>
[RequireComponent(typeof(Animator))]
public class CharacterAnimator : MonoBehaviour
{
    [Header("Animation Parameters")]
    [Tooltip("Name of the boolean parameter for walking state")]
    [SerializeField] private string isWalkingParameter = "IsWalking";
    
    [Tooltip("Name of the float parameter for movement speed (optional)")]
    [SerializeField] private string speedParameter = "Speed";
    
    [Tooltip("Name of the boolean parameter for sprinting state (optional)")]
    [SerializeField] private string isSprintingParameter = "IsSprinting";
    
    private Animator animator;
    private bool hasIsWalkingParam;
    private bool hasSpeedParam;
    private bool hasIsSprintingParam;
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
        
        if (animator == null)
        {
            Debug.LogError($"CharacterAnimator on '{gameObject.name}': Animator component is missing!");
            return;
        }
        
        // Check which parameters exist in the Animator Controller
        hasIsWalkingParam = HasParameter(isWalkingParameter, AnimatorControllerParameterType.Bool);
        hasSpeedParam = HasParameter(speedParameter, AnimatorControllerParameterType.Float);
        hasIsSprintingParam = HasParameter(isSprintingParameter, AnimatorControllerParameterType.Bool);
        
        if (!hasIsWalkingParam && !hasSpeedParam)
        {
            Debug.LogWarning($"CharacterAnimator on '{gameObject.name}': No valid animation parameters found. " +
                           $"Make sure your Animator Controller has either '{isWalkingParameter}' (bool) or '{speedParameter}' (float) parameter.");
        }
    }
    
    /// <summary>
    /// Updates animation based on movement state
    /// Call this from your movement controller each frame
    /// </summary>
    /// <param name="isMoving">Whether the character is currently moving</param>
    /// <param name="speed">Current movement speed (0-1 normalized or actual speed)</param>
    /// <param name="isSprinting">Whether the character is sprinting</param>
    public void UpdateAnimations(bool isMoving, float speed = 0f, bool isSprinting = false)
    {
        if (animator == null || !animator.enabled)
        {
            return;
        }
        
        // Set walking state
        if (hasIsWalkingParam)
        {
            animator.SetBool(isWalkingParameter, isMoving);
        }
        
        // Set speed parameter
        if (hasSpeedParam)
        {
            animator.SetFloat(speedParameter, speed);
        }
        
        // Set sprinting state
        if (hasIsSprintingParam)
        {
            animator.SetBool(isSprintingParameter, isSprinting);
        }
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
}

