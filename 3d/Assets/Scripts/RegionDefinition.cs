using UnityEngine;

/// <summary>
/// Describes one difficulty band of the island, as a fraction of MapGenerator.GetPlayableRadius()
/// measured from MapGenerator.GetIslandCenter(). Regions are not gated areas — every band is always
/// walkable from the start; a region only changes how hard survival is once a player reaches it
/// (enemy archetype eligibility, spawn density, hole hazard density, ground-type bias).
/// </summary>
[CreateAssetMenu(fileName = "New Region", menuName = "LionQuest/Region Definition")]
public class RegionDefinition : ScriptableObject
{
    [Tooltip("Display name for logging/HUD — e.g. \"Wildwood\".")]
    public string regionName = "Region";

    [Tooltip("Inclusive lower bound of distance-from-center, as a fraction of GetPlayableRadius() (0..1).")]
    [Range(0f, 1f)]
    public float minFraction;

    [Tooltip("Exclusive upper bound of distance-from-center, as a fraction of GetPlayableRadius() (0..1).")]
    [Range(0f, 1f)]
    public float maxFraction = 1f;

    [Tooltip("Multiplies this region's weight when WaveSpawner picks a spawn region for an eligible archetype. Higher = more enemies land here.")]
    [Min(0f)]
    public float densityMultiplier = 1f;

    [Tooltip("Multiplies MapGenerator's hole-hazard spawn probability for candidate holes that land in this region.")]
    [Min(0f)]
    public float holeDensityMultiplier = 1f;

    [Tooltip("If true, only archetypes listed in eligibleArchetypes may spawn in this region. If false, eligibleArchetypes is ignored and any archetype may spawn here.")]
    public bool restrictArchetypes;

    [Tooltip("Archetypes allowed to spawn in this region. Only consulted when restrictArchetypes is true.")]
    public EnemyArchetype[] eligibleArchetypes;

    /// <summary>One GroundType and its relative pick weight for this region's terrain-painting bias.</summary>
    [System.Serializable]
    public struct RegionGroundBias
    {
        public GroundType type;
        [Min(0f)] public float weight;
    }

    [Tooltip("Weighted nudge toward specific GroundTypes when painting this region's terrain cells. Empty = no bias, use MapGenerator's default ground-type painting.")]
    public RegionGroundBias[] groundBias;
}
