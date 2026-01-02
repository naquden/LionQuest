using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Helper script to list all available animation clips from a model file
/// Attach this to any GameObject and click "List Animations" in the inspector
/// </summary>
public class AnimationClipLister : MonoBehaviour
{
    [Header("Model Reference")]
    [Tooltip("Drag your FBX model file here to scan for animations")]
    [SerializeField] private GameObject modelPrefab;
    
    [Header("Output")]
    [SerializeField, TextArea(10, 20)] private string animationList = "";
    
#if UNITY_EDITOR
    [ContextMenu("List Animations from Model")]
    public void ListAnimations()
    {
        if (modelPrefab == null)
        {
            Debug.LogWarning("AnimationClipLister: No model assigned. Please assign a model prefab.");
            return;
        }
        
        string path = AssetDatabase.GetAssetPath(modelPrefab);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("AnimationClipLister: Could not find asset path for model.");
            return;
        }
        
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found animations in: {modelPrefab.name}");
        sb.AppendLine("=====================================");
        
        int animationCount = 0;
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip)
            {
                animationCount++;
                sb.AppendLine($"{animationCount}. {clip.name} ({clip.length:F2}s)");
            }
        }
        
        if (animationCount == 0)
        {
            sb.AppendLine("No animation clips found in this model.");
            sb.AppendLine("\nMake sure:");
            sb.AppendLine("1. The model was exported with animations");
            sb.AppendLine("2. The model is imported with 'Animation Type' set correctly");
        }
        
        animationList = sb.ToString();
        Debug.Log(animationList);
    }
    
    [ContextMenu("List Animations from Current Character")]
    public void ListAnimationsFromCharacter()
    {
        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("AnimationClipLister: No Animator or Animator Controller found on this GameObject or its children.");
            return;
        }
        
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found animations in Animator Controller: {animator.runtimeAnimatorController.name}");
        sb.AppendLine("=====================================");
        
        if (clips.Length == 0)
        {
            sb.AppendLine("No animation clips found in the Animator Controller.");
        }
        else
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                {
                    sb.AppendLine($"{i + 1}. {clips[i].name} ({clips[i].length:F2}s)");
                }
            }
        }
        
        animationList = sb.ToString();
        Debug.Log(animationList);
    }
#endif
}

