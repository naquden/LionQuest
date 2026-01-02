using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace LionQuest.Character.Tiles
{
    /// <summary>
    /// Helper script to configure 2D lighting for Tilemaps.
    /// Attach this to a GameObject and use the context menu to auto-configure lighting.
    /// </summary>
    public class LightingSetupHelper : MonoBehaviour
    {
        [ContextMenu("Setup Global Light for Default Layer")]
        public void SetupGlobalLightForDefaultLayer()
        {
            // Find Global Light 2D
            Light2D globalLight = FindObjectOfType<Light2D>();
            
            if (globalLight == null)
            {
                Debug.LogError("LightingSetupHelper: No Global Light 2D found in scene! Please create one first (GameObject -> Light -> 2D -> Global Light 2D)");
                return;
            }
            
            // Check if it's a global light
            if (globalLight.lightType != Light2D.LightType.Global)
            {
                Debug.LogWarning("LightingSetupHelper: Found Light 2D but it's not set to Global type. Changing it to Global.");
                globalLight.lightType = Light2D.LightType.Global;
            }
            
            // Enable "Apply To Sorting Layers"
            // Note: This requires reflection or manual setup in Unity Editor
            // The sorting layers are managed through Unity's internal system
            
            Debug.Log("LightingSetupHelper: Global Light 2D found!");
            Debug.Log("LightingSetupHelper: To complete setup:");
            Debug.Log("  1. Select the 'Global Light 2D' GameObject in Hierarchy");
            Debug.Log("  2. In Inspector, check 'Apply To Sorting Layers'");
            Debug.Log("  3. Enable the 'Default' sorting layer (or your tile's sorting layer)");
            Debug.Log("  4. Adjust Intensity and Color as needed");
        }
        
        [ContextMenu("Check Tilemap Material")]
        public void CheckTilemapMaterial()
        {
            Tilemap[] tilemaps = FindObjectsOfType<Tilemap>();
            
            if (tilemaps.Length == 0)
            {
                Debug.LogWarning("LightingSetupHelper: No Tilemaps found in scene!");
                return;
            }
            
            foreach (Tilemap tilemap in tilemaps)
            {
                TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
                if (renderer != null)
                {
                    Material mat = renderer.material;
                    if (mat != null)
                    {
                        Debug.Log($"LightingSetupHelper: Tilemap '{tilemap.name}' uses material: {mat.name}");
                        Debug.Log($"  - Shader: {mat.shader.name}");
                        
                        // Check if material supports lighting
                        if (mat.shader.name.Contains("Lit") || mat.shader.name.Contains("Sprite-Lit"))
                        {
                            Debug.Log($"  ✓ Material supports 2D lighting!");
                        }
                        else
                        {
                            Debug.LogWarning($"  ⚠ Material does NOT support 2D lighting. Consider using 'Sprite-Lit-Default' material.");
                        }
                        
                        Debug.Log($"  - Sorting Layer: {SortingLayer.IDToName(renderer.sortingLayerID)} (ID: {renderer.sortingLayerID})");
                    }
                    else
                    {
                        Debug.LogWarning($"LightingSetupHelper: Tilemap '{tilemap.name}' has no material assigned!");
                    }
                }
            }
        }
        
        [ContextMenu("Print Lighting Info")]
        public void PrintLightingInfo()
        {
            Debug.Log("=== 2D Lighting Setup Guide ===");
            Debug.Log("");
            Debug.Log("To make Global Light 2D affect your tiles:");
            Debug.Log("");
            Debug.Log("1. SELECT 'Global Light 2D' GameObject in Hierarchy");
            Debug.Log("2. In Inspector, find 'Light 2D' component");
            Debug.Log("3. Check 'Apply To Sorting Layers' checkbox");
            Debug.Log("4. In the list below, enable the sorting layer your tiles use");
            Debug.Log("   (Usually 'Default' if you haven't changed it)");
            Debug.Log("5. Adjust 'Intensity' (0-1) to control brightness");
            Debug.Log("6. Adjust 'Color' to change light color");
            Debug.Log("");
            Debug.Log("To check your tile's sorting layer:");
            Debug.Log("- Select your Tilemap GameObject");
            Debug.Log("- Look at 'Tilemap Renderer' component");
            Debug.Log("- Check 'Sorting Layer' field");
            Debug.Log("");
            Debug.Log("If tiles don't respond to light:");
            Debug.Log("- Tilemap Renderer material must support lighting");
            Debug.Log("- Use 'Sprite-Lit-Default' material (or similar)");
            Debug.Log("- Default 'Sprite-Default' material does NOT support lighting");
        }
    }
}

