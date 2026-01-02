using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LionQuest.Character.Tiles
{
    /// <summary>
    /// Controls lighting settings for different maps/scenes.
    /// Attach this to a GameObject in your scene to control Global Light 2D settings.
    /// </summary>
    public class MapLightingController : MonoBehaviour
    {
        [Header("Lighting Presets")]
        [Tooltip("Full brightness - no shadows, entire map lit")]
        public bool fullBrightness = true;
        
        [Tooltip("Light intensity (0 = dark, 1 = full brightness)")]
        [Range(0f, 2f)]
        public float lightIntensity = 1f;
        
        [Tooltip("Light color")]
        public Color lightColor = Color.white;
        
        [Header("Dungeon Mode")]
        [Tooltip("Enable for dark dungeon maps")]
        public bool isDungeon = false;
        
        [Tooltip("Dungeon light intensity (usually very low)")]
        [Range(0f, 1f)]
        public float dungeonIntensity = 0.2f;
        
        [Tooltip("Dungeon light color (usually dim/warm)")]
        public Color dungeonColor = new Color(0.8f, 0.7f, 0.6f, 1f); // Warm dim light
        
        private Light2D globalLight;
        
        private void Awake()
        {
            FindGlobalLight();
            ApplyLightingSettings();
        }
        
        private void FindGlobalLight()
        {
            // Find Global Light 2D in scene
            Light2D[] lights = FindObjectsOfType<Light2D>();
            foreach (Light2D light in lights)
            {
                if (light.lightType == Light2D.LightType.Global)
                {
                    globalLight = light;
                    break;
                }
            }
            
            if (globalLight == null)
            {
                Debug.LogWarning("MapLightingController: No Global Light 2D found in scene! Creating one...");
                CreateGlobalLight();
            }
        }
        
        private void CreateGlobalLight()
        {
            GameObject lightObject = new GameObject("Global Light 2D");
            globalLight = lightObject.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.intensity = 1f;
            globalLight.color = Color.white;
        }
        
        /// <summary>
        /// Apply lighting settings based on current configuration
        /// </summary>
        [ContextMenu("Apply Lighting Settings")]
        public void ApplyLightingSettings()
        {
            if (globalLight == null)
            {
                FindGlobalLight();
            }
            
            if (globalLight == null)
            {
                Debug.LogError("MapLightingController: Cannot find or create Global Light 2D!");
                return;
            }
            
            if (isDungeon)
            {
                // Dungeon mode - dim lighting
                globalLight.intensity = dungeonIntensity;
                globalLight.color = dungeonColor;
                Debug.Log($"MapLightingController: Applied dungeon lighting (Intensity: {dungeonIntensity}, Color: {dungeonColor})");
            }
            else if (fullBrightness)
            {
                // Full brightness - no shadows, entire map lit
                globalLight.intensity = lightIntensity;
                globalLight.color = lightColor;
                Debug.Log($"MapLightingController: Applied full brightness lighting (Intensity: {lightIntensity}, Color: {lightColor})");
            }
            else
            {
                // Custom settings
                globalLight.intensity = lightIntensity;
                globalLight.color = lightColor;
                Debug.Log($"MapLightingController: Applied custom lighting (Intensity: {lightIntensity}, Color: {lightColor})");
            }
        }
        
        /// <summary>
        /// Set to full brightness mode (entire map lit, no shadows)
        /// </summary>
        [ContextMenu("Set Full Brightness")]
        public void SetFullBrightness()
        {
            isDungeon = false;
            fullBrightness = true;
            lightIntensity = 1f;
            lightColor = Color.white;
            ApplyLightingSettings();
        }
        
        /// <summary>
        /// Set to dungeon mode (dark, dim lighting)
        /// </summary>
        [ContextMenu("Set Dungeon Mode")]
        public void SetDungeonMode()
        {
            isDungeon = true;
            fullBrightness = false;
            ApplyLightingSettings();
        }
        
        /// <summary>
        /// Turn off all lighting (pitch black)
        /// </summary>
        [ContextMenu("Turn Off Light")]
        public void TurnOffLight()
        {
            if (globalLight == null)
            {
                FindGlobalLight();
            }
            
            if (globalLight != null)
            {
                globalLight.intensity = 0f;
                Debug.Log("MapLightingController: Turned off Global Light 2D");
            }
        }
        
        /// <summary>
        /// Turn on light with current settings
        /// </summary>
        [ContextMenu("Turn On Light")]
        public void TurnOnLight()
        {
            ApplyLightingSettings();
        }
        
        // Update lighting in real-time when values change in inspector
        private void OnValidate()
        {
            if (Application.isPlaying && globalLight != null)
            {
                ApplyLightingSettings();
            }
        }
    }
}

