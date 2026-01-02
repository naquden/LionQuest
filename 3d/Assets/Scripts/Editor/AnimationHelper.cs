using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor helper script to create and configure Animator Controllers for characters
/// Unity 6 compatible
/// </summary>
public class AnimationHelper : EditorWindow
{
    private GameObject selectedCharacter;
    private RuntimeAnimatorController animatorController;
    private List<string> availableAnimations = new List<string>();
    private Vector2 scrollPosition;
    
    [MenuItem("Tools/Animation Helper")]
    public static void ShowWindow()
    {
        GetWindow<AnimationHelper>("Animation Helper");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Character Animation Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Character selection
        selectedCharacter = EditorGUILayout.ObjectField("Character GameObject", selectedCharacter, typeof(GameObject), true) as GameObject;
        
        if (selectedCharacter == null)
        {
            EditorGUILayout.HelpBox("Select a character GameObject to set up animations.", MessageType.Info);
            return;
        }
        
        EditorGUILayout.Space();
        
        // Animator Controller selection
        animatorController = EditorGUILayout.ObjectField("Animator Controller", animatorController, typeof(RuntimeAnimatorController), false) as RuntimeAnimatorController;
        
        EditorGUILayout.Space();
        
        // List available animations from the character's model
        if (GUILayout.Button("Scan for Available Animations"))
        {
            ScanForAnimations();
        }
        
        if (availableAnimations.Count > 0)
        {
            EditorGUILayout.Space();
            GUILayout.Label($"Found {availableAnimations.Count} animations:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            foreach (string anim in availableAnimations)
            {
                EditorGUILayout.LabelField(anim);
            }
            EditorGUILayout.EndScrollView();
        }
        
        EditorGUILayout.Space();
        
        // Create Animator Controller button
        if (GUILayout.Button("Create New Animator Controller", GUILayout.Height(30)))
        {
            CreateAnimatorController();
        }
        
        EditorGUILayout.Space();
        
        // Setup instructions
        EditorGUILayout.HelpBox(
            "1. Select your character GameObject\n" +
            "2. Click 'Scan for Available Animations' to see what animations are available\n" +
            "3. Click 'Create New Animator Controller' to generate a controller with standard states\n" +
            "4. Manually assign animation clips to states in the Animator window",
            MessageType.Info
        );
    }
    
    private void ScanForAnimations()
    {
        availableAnimations.Clear();
        
        // Try to find animations from the character's model
        Animator animator = selectedCharacter.GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            foreach (AnimationClip clip in clips)
            {
                if (clip != null && !availableAnimations.Contains(clip.name))
                {
                    availableAnimations.Add(clip.name);
                }
            }
        }
        
        // Also check for model files in the character
        SkinnedMeshRenderer[] renderers = selectedCharacter.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            // Try to find the model asset
            string path = AssetDatabase.GetAssetPath(renderer.sharedMesh);
            if (!string.IsNullOrEmpty(path))
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object asset in assets)
                {
                    if (asset is AnimationClip clip && !availableAnimations.Contains(clip.name))
                    {
                        availableAnimations.Add(clip.name);
                    }
                }
            }
        }
        
        // Check for FBX files in Characters folder
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Characters" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !availableAnimations.Contains(clip.name))
                {
                    availableAnimations.Add(clip.name);
                }
            }
        }
        
        availableAnimations.Sort();
        
        if (availableAnimations.Count == 0)
        {
            EditorUtility.DisplayDialog("No Animations Found", 
                "Could not find any animation clips. Make sure:\n" +
                "1. Your character model (FBX) is imported with animations\n" +
                "2. The Animator Controller is assigned to the character\n" +
                "3. Animation clips are extracted from the model", 
                "OK");
        }
    }
    
    private void CreateAnimatorController()
    {
        if (selectedCharacter == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a character GameObject first.", "OK");
            return;
        }
        
        // Create save path
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Animator Controller",
            "PlayerAnimatorController",
            "controller",
            "Choose where to save the Animator Controller"
        );
        
        if (string.IsNullOrEmpty(path))
        {
            return; // User cancelled
        }
        
        // Create the Animator Controller
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        
        // Add parameters (Unity 6 standard)
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("AttackType", AnimatorControllerParameterType.Int);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Arise", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        
        // Get the root state machine
        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
        
        // Create states
        AnimatorState idleState = rootStateMachine.AddState("Idle");
        AnimatorState walkState = rootStateMachine.AddState("Walk");
        AnimatorState attackState = rootStateMachine.AddState("Attack");
        AnimatorState dieState = rootStateMachine.AddState("Die");
        AnimatorState ariseState = rootStateMachine.AddState("Arise");
        
        // Set Idle as default state
        rootStateMachine.defaultState = idleState;
        
        // Create transitions
        // Idle <-> Walk
        AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0.1f;
        
        AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0.1f;
        
        // Any state -> Attack
        AnimatorStateTransition anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.1f;
        
        // Attack -> Idle (after attack completes)
        AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.9f;
        attackToIdle.duration = 0.1f;
        
        // Any state -> Die
        AnimatorStateTransition anyToDie = rootStateMachine.AddAnyStateTransition(dieState);
        anyToDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
        anyToDie.hasExitTime = false;
        anyToDie.duration = 0.1f;
        
        // Die -> Arise
        AnimatorStateTransition dieToArise = dieState.AddTransition(ariseState);
        dieToArise.AddCondition(AnimatorConditionMode.If, 0, "Arise");
        dieToArise.hasExitTime = true;
        dieToArise.exitTime = 0.95f;
        dieToArise.duration = 0.2f;
        
        // Arise -> Idle
        AnimatorStateTransition ariseToIdle = ariseState.AddTransition(idleState);
        ariseToIdle.hasExitTime = true;
        ariseToIdle.exitTime = 0.9f;
        ariseToIdle.duration = 0.1f;
        
        // Save the controller
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Assign to character if it has an Animator component
        Animator animator = selectedCharacter.GetComponent<Animator>();
        if (animator == null)
        {
            animator = selectedCharacter.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = controller;
        
        EditorUtility.DisplayDialog("Success", 
            $"Animator Controller created successfully at:\n{path}\n\n" +
            "Next steps:\n" +
            "1. Open the Animator window (Window > Animation > Animator)\n" +
            "2. Select your character GameObject\n" +
            "3. Assign animation clips to each state\n" +
            "4. Adjust transition timings if needed", 
            "OK");
        
        // Select the created controller
        Selection.activeObject = controller;
    }
}

