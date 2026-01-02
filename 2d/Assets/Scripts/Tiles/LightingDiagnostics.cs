using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LionQuest.Character.Tiles
{
    /// <summary>
    /// Diagnoses lighting issues and helps fix shadow problems
    /// </summary>
    public class LightingDiagnostics : MonoBehaviour
    {
        [ContextMenu("Find All Lights")]
        public void FindAllLights()
        {
            Light2D[] allLights = FindObjectsOfType<Light2D>();
            
            Debug.Log($"=== Found {allLights.Length} Light 2D component(s) ===");
            
            foreach (Light2D light in allLights)
            {
                Debug.Log($"Light: {light.gameObject.name}");
                Debug.Log($"  - Type: {light.lightType}");
                Debug.Log($"  - Intensity: {light.intensity}");
                Debug.Log($"  - Color: {light.color}");
                Debug.Log($"  - Shadows Enabled: {light.shadowsEnabled}");
                Debug.Log($"  - Position: {light.transform.position}");
                
                if (light.lightType != Light2D.LightType.Global)
                {
                    Debug.LogWarning($"  ⚠ Non-Global light found! This might be causing shadows.");
                }
                
                if (light.shadowsEnabled)
                {
                    Debug.LogWarning($"  ⚠ Shadows are enabled on this light! This creates shadows.");
                }
            }
        }
        
        [ContextMenu("Disable All Non-Global Lights")]
        public void DisableNonGlobalLights()
        {
            Light2D[] allLights = FindObjectsOfType<Light2D>();
            int disabled = 0;
            
            foreach (Light2D light in allLights)
            {
                if (light.lightType != Light2D.LightType.Global)
                {
                    light.enabled = false;
                    disabled++;
                    Debug.Log($"Disabled non-global light: {light.gameObject.name}");
                }
            }
            
            Debug.Log($"Disabled {disabled} non-global light(s)");
        }
        
        [ContextMenu("Disable Shadows on All Lights")]
        public void DisableShadowsOnAllLights()
        {
            Light2D[] allLights = FindObjectsOfType<Light2D>();
            int changed = 0;
            
            foreach (Light2D light in allLights)
            {
                if (light.shadowsEnabled)
                {
                    light.shadowsEnabled = false;
                    changed++;
                    Debug.Log($"Disabled shadows on: {light.gameObject.name}");
                }
            }
            
            Debug.Log($"Disabled shadows on {changed} light(s)");
        }
        
        [ContextMenu("Ensure Uniform Global Light")]
        public void EnsureUniformGlobalLight()
        {
            // Find or create Global Light
            Light2D globalLight = null;
            Light2D[] allLights = FindObjectsOfType<Light2D>();
            
            foreach (Light2D light in allLights)
            {
                if (light.lightType == Light2D.LightType.Global)
                {
                    globalLight = light;
                    break;
                }
            }
            
            if (globalLight == null)
            {
                Debug.LogWarning("No Global Light found. Creating one...");
                GameObject lightObj = new GameObject("Global Light 2D");
                globalLight = lightObj.AddComponent<Light2D>();
                globalLight.lightType = Light2D.LightType.Global;
            }
            
            // Configure for uniform lighting
            globalLight.intensity = 1f;
            globalLight.color = Color.white;
            globalLight.shadowsEnabled = false;
            globalLight.enabled = true;
            
            Debug.Log("Global Light configured for uniform lighting:");
            Debug.Log($"  - Intensity: {globalLight.intensity}");
            Debug.Log($"  - Color: {globalLight.color}");
            Debug.Log($"  - Shadows: {globalLight.shadowsEnabled}");
        }
    }
}

