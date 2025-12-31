using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Helper script to set up a character model as the player for top-down gameplay
/// Attach this to your character model GameObject, then click "Setup Character" in the inspector
/// </summary>
public class CharacterSetupHelper : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Click this button to automatically set up the character as a player")]
    [SerializeField] private bool setupCharacter = false;
    
    [Header("Character Settings")]
    [SerializeField] private float characterHeight = 1f; // Changed default from 2f to 1f to match working player
    [SerializeField] private float characterRadius = 0.5f;
    [SerializeField] private Vector3 modelScale = Vector3.one;
    [SerializeField] private float groundLevel = 0f; // Y position where the ground is
    [SerializeField] private float modelYOffset = 0f; // Adjust if the model's pivot is not at the bottom
    
    [Header("Animation Setup (Optional)")]
    [Tooltip("If enabled, will add Animator and CharacterAnimator components for animation support")]
    [SerializeField] private bool setupAnimation = false;
    
    [Tooltip("If enabled, will add SimpleProceduralAnimation for simple bobbing animation (works with static meshes)")]
    [SerializeField] private bool setupProceduralAnimation = false;
    
    private void OnValidate()
    {
        if (setupCharacter)
        {
            setupCharacter = false;
            SetupCharacter();
        }
    }
    
    [ContextMenu("Setup Character")]
    public void SetupCharacter()
    {
        // Ensure GameObject and all parents are active
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"CharacterSetupHelper: GameObject '{gameObject.name}' is not active in hierarchy. Activating it...");
            gameObject.SetActive(true);
        }
        
        // Ensure CharacterController exists
        CharacterController controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }
        
        // Ensure CharacterController is enabled (do this first before any other operations)
        controller.enabled = true;
        
        // Configure CharacterController for top-down gameplay
        controller.height = characterHeight;
        controller.radius = characterRadius;
        // Use center at (0,0,0) to match working player setup - this places the bottom at ground level when position.y = groundLevel
        controller.center = Vector3.zero;
        controller.slopeLimit = 90f; // Prevent sliding on slopes
        controller.stepOffset = 0f; // No step offset for top-down
        
        // Ensure TopDownPlayerController exists
        TopDownPlayerController playerController = GetComponent<TopDownPlayerController>();
        if (playerController == null)
        {
            playerController = gameObject.AddComponent<TopDownPlayerController>();
        }
        
        // Always try to find and assign the Input Actions asset (even if component already existed)
        #if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("InputSystem_Actions t:InputActionAsset");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (inputActions != null)
            {
                // Use SerializedObject to set the private serialized field
                var serializedObject = new SerializedObject(playerController);
                var inputActionsField = serializedObject.FindProperty("inputActions");
                if (inputActionsField != null)
                {
                    inputActionsField.objectReferenceValue = inputActions;
                    serializedObject.ApplyModifiedProperties();
                    Debug.Log($"CharacterSetupHelper: Assigned Input Actions to TopDownPlayerController on '{gameObject.name}'");
                }
            }
            else
            {
                Debug.LogWarning($"CharacterSetupHelper: Could not load Input Actions asset from path: {path}");
            }
        }
        else
        {
            Debug.LogWarning($"CharacterSetupHelper: Could not find InputSystem_Actions asset in project!");
        }
        #endif
        
        // Ensure TopDownPlayerController is enabled
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        
        // Set up animation components if requested
        CharacterAnimator characterAnimator = null;
        if (setupAnimation)
        {
            // Add Animator component if it doesn't exist
            Animator animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = gameObject.AddComponent<Animator>();
                Debug.Log($"CharacterSetupHelper: Added Animator component to '{gameObject.name}'. " +
                         $"Remember to assign an Animator Controller in the inspector!");
            }
            
            // Add CharacterAnimator component if it doesn't exist
            characterAnimator = GetComponent<CharacterAnimator>();
            if (characterAnimator == null)
            {
                characterAnimator = gameObject.AddComponent<CharacterAnimator>();
                Debug.Log($"CharacterSetupHelper: Added CharacterAnimator component to '{gameObject.name}'");
            }
        }
        
        // Set the ground level and link animator in TopDownPlayerController
        #if UNITY_EDITOR
        var playerControllerSerialized = new SerializedObject(playerController);
        
        // Set ground level
        var groundLevelField = playerControllerSerialized.FindProperty("groundLevel");
        if (groundLevelField != null)
        {
            groundLevelField.floatValue = groundLevel;
        }
        
        // Link CharacterAnimator if animation setup was requested
        if (setupAnimation && characterAnimator != null)
        {
            var animatorField = playerControllerSerialized.FindProperty("characterAnimator");
            if (animatorField != null)
            {
                animatorField.objectReferenceValue = characterAnimator;
                Debug.Log($"CharacterSetupHelper: Linked CharacterAnimator to TopDownPlayerController");
            }
        }
        
        playerControllerSerialized.ApplyModifiedProperties();
        #endif
        
        // Set up procedural animation if requested (for static meshes without rigging)
        if (setupProceduralAnimation)
        {
            SimpleProceduralAnimation proceduralAnim = GetComponent<SimpleProceduralAnimation>();
            if (proceduralAnim == null)
            {
                proceduralAnim = gameObject.AddComponent<SimpleProceduralAnimation>();
                Debug.Log($"CharacterSetupHelper: Added SimpleProceduralAnimation component to '{gameObject.name}'. " +
                         $"This creates a simple bobbing animation for static mesh models.");
            }
        }
        
        // Set up tag
        if (!gameObject.CompareTag("Player"))
        {
            gameObject.tag = "Player";
        }
        
        // Ensure the model is at the right scale
        if (transform.localScale != modelScale)
        {
            transform.localScale = modelScale;
        }
        
        // Calculate Y position so the bottom of the CharacterController is at ground level
        // Bottom of CharacterController = transform.position.y + center.y - height/2
        // We want: transform.position.y + center.y - height/2 = groundLevel
        // Since center is (0,0,0), this simplifies to: position.y - height/2 = groundLevel
        // So: position.y = groundLevel + height/2
        // This places the center at height/2 above ground, with bottom at ground level
        float correctYPosition = groundLevel + (controller.height * 0.5f);
        
        // Position at ground level so the bottom of the CharacterController is at groundLevel
        Vector3 position = transform.position;
        position.y = correctYPosition;
        transform.position = position;
        
        // If modelYOffset is set, apply it to adjust the visual model position
        // This doesn't change the CharacterController bottom, just the visual appearance
        if (!Mathf.Approximately(modelYOffset, 0f))
        {
            position.y += modelYOffset;
            transform.position = position;
            
            // Adjust CharacterController center to compensate so bottom stays at ground level
            Vector3 adjustedCenter = controller.center;
            adjustedCenter.y -= modelYOffset;
            controller.center = adjustedCenter;
        }
        
        // CRITICAL: Ensure CharacterController is enabled after all modifications
        // This must be done last to ensure it's active when TopDownPlayerController uses it
        controller.enabled = true;
        
        // Ensure GameObject is active
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        // Force the TopDownPlayerController to recalculate its locked position
        // This ensures it uses the correct ground level
        #if UNITY_EDITOR
        EditorUtility.SetDirty(playerController);
        EditorUtility.SetDirty(controller);
        #endif
        
        // Calculate the actual bottom of the CharacterController for verification
        float actualBottom = position.y + controller.center.y - (controller.height * 0.5f);
        
        Debug.Log($"Character '{gameObject.name}' setup complete!\n" +
                  $"  Position Y: {position.y:F2}\n" +
                  $"  CharacterController center: {controller.center}\n" +
                  $"  CharacterController height: {controller.height}\n" +
                  $"  CharacterController bottom: {actualBottom:F2} (target: {groundLevel:F2})\n" +
                  $"  Model Y offset: {modelYOffset:F2}\n" +
                  $"  Input Actions assigned: {(playerController != null ? "Yes" : "No")}");
    }
}

