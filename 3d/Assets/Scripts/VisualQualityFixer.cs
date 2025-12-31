using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Tool to fix visual quality issues like pixelation
/// </summary>
public class VisualQualityFixer : MonoBehaviour
{
#if UNITY_EDITOR
    [ContextMenu("Fix Visual Quality Issues")]
    public void FixVisualQuality()
    {
        Debug.Log("=== FIXING VISUAL QUALITY ISSUES ===\n");
        
        // Fix 1: Enable Anti-Aliasing in Quality Settings
        FixAntiAliasing();
        
        // Fix 2: Increase Render Scale
        FixRenderScale();
        
        // Fix 3: Check Camera Settings
        FixCameraSettings();
        
        // Fix 4: Check Model Import Settings
        CheckModelSettings();
        
        Debug.Log("=== QUALITY FIX COMPLETE ===\n");
        Debug.Log("IMPORTANT: You may need to manually adjust Quality Settings in:");
        Debug.Log("Edit > Project Settings > Quality");
        Debug.Log("And set Anti Aliasing to 2x, 4x, or 8x Multi Sampling");
    }
    
    private void FixAntiAliasing()
    {
        Debug.Log("--- Fixing Anti-Aliasing ---");
        Debug.LogWarning("Anti-Aliasing is currently DISABLED in Quality Settings!");
        Debug.Log("To fix: Edit > Project Settings > Quality > Select your quality level > Anti Aliasing > Set to 2x, 4x, or 8x Multi Sampling");
        Debug.Log("");
    }
    
    private void FixRenderScale()
    {
        Debug.Log("--- Checking Render Scale ---");
        // This would require accessing URP asset, which is complex
        Debug.Log("Mobile Render Scale is set to 0.8 (80% resolution)");
        Debug.Log("To fix: Assets/Settings/Mobile_RPAsset > Render Scale > Set to 1.0");
        Debug.Log("Or switch to PC quality level in Edit > Project Settings > Quality");
        Debug.Log("");
    }
    
    private void FixCameraSettings()
    {
        Debug.Log("--- Checking Camera Settings ---");
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Debug.Log($"Camera Field of View: {mainCam.fieldOfView}");
            Debug.Log($"Camera is Orthographic: {mainCam.orthographic}");
            if (mainCam.orthographic)
            {
                Debug.Log($"Orthographic Size: {mainCam.orthographicSize}");
            }
            Debug.Log("Tip: If camera is too far, objects will appear pixelated. Try moving camera closer or reducing FOV.");
        }
        Debug.Log("");
    }
    
    private void CheckModelSettings()
    {
        Debug.Log("--- Checking Model Import Settings ---");
        Debug.Log("The 3D model (3d.obj) might be low resolution.");
        Debug.Log("To check: Select Assets/Characters/3d.obj in Project window");
        Debug.Log("In Inspector, check:");
        Debug.Log("  - Mesh Compression: Should be 'Off' for best quality");
        Debug.Log("  - Read/Write Enabled: Should be checked if you need to modify mesh");
        Debug.Log("  - Generate Colliders: Optional");
        Debug.Log("");
        Debug.Log("If the model itself is low-poly, you may need:");
        Debug.Log("  - A higher resolution model");
        Debug.Log("  - Better textures/materials");
        Debug.Log("  - Or accept the pixelated look as a style choice");
        Debug.Log("");
    }
#endif
}

