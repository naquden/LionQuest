using UnityEngine;

/// <summary>
/// Describes one entry in WaveSpawner's spawn table: which prefab, from which wave it starts
/// appearing, how often relative to the other archetypes, and whether it is a boss.
///
/// A separate asset rather than fields on WaveSpawner itself, so adding or re-tuning an archetype
/// (e.g. "ranged enemies should start at wave 3, not wave 2") is an asset edit, not a code change —
/// and so the same archetype list can be reused by a future level without re-authoring it.
///
/// Deliberately does not duplicate anything already tunable on the Enemy prefab itself (health,
/// damage, speed): those stay on the prefab/AttackData, exactly as they do today. This asset only
/// answers "when and how often", not "how strong".
/// </summary>
[CreateAssetMenu(fileName = "EnemyArchetype", menuName = "LionQuest/Enemy Archetype")]
public class EnemyArchetype : ScriptableObject
{
    [Tooltip("Enemy prefab to spawn. Must have an Enemy component.")]
    public GameObject prefab;

    [Tooltip("Display name for logging/HUD — not required to match the prefab name.")]
    public string archetypeName = "Enemy";

    [Tooltip("Wave number (1-based) this archetype first becomes eligible to spawn. Wave 1 is the " +
             "level's hand-placed enemies and never draws from this table.")]
    [Min(1)]
    public int minWave = 1;

    [Tooltip("Relative pick weight among archetypes eligible for the current wave. Ignored for " +
             "boss archetypes, which spawn alone.")]
    [Min(0f)]
    public float weight = 1f;

    [Tooltip("Boss archetypes are spawned alone on their wave (WaveSpawner.bossWaveInterval), not " +
             "mixed into the regular pool, and their death triggers victory instead of the next wave.")]
    public bool isBoss;
}
