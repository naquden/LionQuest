using UnityEngine;

/// <summary>
/// Defines properties for different ground/biome types
/// Create instances of this in Project window: Right-click > Create > Ground Type
/// </summary>
[CreateAssetMenu(fileName = "New Ground Type", menuName = "Game/Ground Type")]
public class GroundType : ScriptableObject
{
    [Header("Visual")]
    [Tooltip("Name of this ground type (e.g., Grass, Dirt, Hole)")]
    public string groundName = "Grass";
    
    [Tooltip("Color used for this biome in the map")]
    public Color biomeColor = Color.green;
    
    [Header("Movement Properties")]
    [Tooltip("Movement speed multiplier (1.0 = normal, 0.5 = half speed, 2.0 = double speed)")]
    [Range(0f, 2f)]
    public float movementSpeedMultiplier = 1.0f;
    
    [Tooltip("Can the player walk on this ground type?")]
    public bool isWalkable = true;
    
    [Header("Physics")]
    [Tooltip("Does the player fall through this ground type?")]
    public bool isHole = false;
    
    [Tooltip("Fall speed multiplier when falling through holes")]
    [Range(0.1f, 5f)]
    public float fallSpeedMultiplier = 1.0f;
    
    [Header("Terrain")]
    [Tooltip("Terrain texture/material for this ground type")]
    public Texture2D terrainTexture;
    
    [Tooltip("Normal map for terrain texture")]
    public Texture2D normalMap;
    
    [Tooltip("Texture tiling size")]
    public Vector2 textureTiling = new Vector2(15, 15);
}

