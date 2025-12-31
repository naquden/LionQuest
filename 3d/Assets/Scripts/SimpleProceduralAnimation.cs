using UnityEngine;

/// <summary>
/// Simple procedural animation for static mesh characters
/// Creates a bobbing/walking effect without requiring a rigged model
/// Attach this to your character GameObject
/// </summary>
public class SimpleProceduralAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("How fast the character bobs up and down when walking")]
    [SerializeField] private float bobSpeed = 8f;
    
    [Tooltip("How far the character moves up and down")]
    [SerializeField] private float bobAmount = 0.1f;
    
    [Tooltip("How much the character tilts side to side when walking")]
    [SerializeField] private float tiltAmount = 5f;
    
    [Tooltip("How fast the character tilts")]
    [SerializeField] private float tiltSpeed = 6f;
    
    [Tooltip("Smoothness of the animation")]
    [SerializeField] private float smoothness = 10f;
    
    [Header("References")]
    [Tooltip("The visual model to animate (if different from this GameObject)")]
    [SerializeField] private Transform visualModel;
    
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float bobTimer = 0f;
    private bool isMoving = false;
    private TopDownPlayerController playerController;
    private CharacterAnimator characterAnimator;
    
    private void Awake()
    {
        // If no visual model specified, use this transform
        if (visualModel == null)
        {
            visualModel = transform;
        }
        
        // Store original position and rotation
        originalPosition = visualModel.localPosition;
        originalRotation = visualModel.localRotation;
        
        // Try to find player controller to detect movement
        playerController = GetComponent<TopDownPlayerController>();
        characterAnimator = GetComponent<CharacterAnimator>();
    }
    
    private void Update()
    {
        // Check if character is moving
        UpdateMovementState();
        
        if (isMoving)
        {
            // Update bob timer
            bobTimer += Time.deltaTime * bobSpeed;
            
            // Calculate vertical bob (sine wave)
            float verticalBob = Mathf.Sin(bobTimer) * bobAmount;
            
            // Calculate horizontal tilt (cosine wave, slightly offset)
            float horizontalTilt = Mathf.Cos(bobTimer * 0.5f) * tiltAmount;
            
            // Apply vertical bob
            Vector3 newPosition = originalPosition;
            newPosition.y += verticalBob;
            visualModel.localPosition = Vector3.Lerp(visualModel.localPosition, newPosition, Time.deltaTime * smoothness);
            
            // Apply tilt (rotate around Z axis for side-to-side tilt)
            Quaternion tiltRotation = originalRotation * Quaternion.Euler(0, 0, horizontalTilt);
            visualModel.localRotation = Quaternion.Lerp(visualModel.localRotation, tiltRotation, Time.deltaTime * smoothness);
        }
        else
        {
            // Return to original position and rotation when not moving
            visualModel.localPosition = Vector3.Lerp(visualModel.localPosition, originalPosition, Time.deltaTime * smoothness);
            visualModel.localRotation = Quaternion.Lerp(visualModel.localRotation, originalRotation, Time.deltaTime * smoothness);
            
            // Reset timer when stopped
            if (Vector3.Distance(visualModel.localPosition, originalPosition) < 0.01f)
            {
                bobTimer = 0f;
            }
        }
    }
    
    private void UpdateMovementState()
    {
        // Try to get movement state from TopDownPlayerController
        if (playerController != null)
        {
            isMoving = playerController.IsMoving;
        }
        else if (characterAnimator != null)
        {
            // Alternative: check if animator has IsWalking parameter set
            // This requires the Animator to be set up
            Animator animator = GetComponent<Animator>();
            if (animator != null && animator.isActiveAndEnabled)
            {
                isMoving = animator.GetBool("IsWalking");
            }
        }
        // If SetMoving was called, that takes precedence (handled in SetMoving method)
    }
    
    /// <summary>
    /// Call this method from TopDownPlayerController or CharacterAnimator to update movement state
    /// </summary>
    public void SetMoving(bool moving)
    {
        isMoving = moving;
    }
    
    private void OnDisable()
    {
        // Reset to original position when disabled
        if (visualModel != null)
        {
            visualModel.localPosition = originalPosition;
            visualModel.localRotation = originalRotation;
        }
    }
}

