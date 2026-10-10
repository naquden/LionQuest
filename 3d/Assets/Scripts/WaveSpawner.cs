using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime enemy waves after the level's hand-placed enemies (wave 1) are cleared, escalating enemy
/// count with player count, and ending the run in victory when a boss wave's Enemy dies.
///
/// Wave 1 is intentionally NOT this spawner's concern — it is Level1Generator.SpawnEnemies' 24
/// hand-placed enemies, baked into the scene at generate time. Rewriting that placement to be
/// runtime-spawned would have broken five existing tests that assert on it directly (enemy count,
/// positions, "enemies exist at scene load"). Instead this spawner watches for wave 1's enemies to
/// run out and takes over from wave 2 onward — the level keeps its known-good opening, and the new
/// system only owns what didn't exist before.
///
/// Player-count scaling: enemyCountPerWave is PER PLAYER, using GameSaveController's registry
/// (already the source of truth for "how many players are actually in this run" — see
/// FindNearestLivingPlayer/AnyPlayerAlive) rather than a hardcoded count. A solo run and a four-player
/// run both need to feel appropriately full without separately tuning two spawn tables.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    [Tooltip("Root Level1Generator parents wave-1 enemies under (\"Enemies\"). Watched to detect " +
             "when the hand-placed opening wave is cleared; this spawner's own waves parent " +
             "elsewhere so a scene inspection can always tell wave 1 apart from a spawned wave.")]
    [SerializeField] private Transform openingWaveParent;

    [SerializeField] private MapGenerator mapGenerator;

    [Tooltip("Regular (non-boss) archetypes. Which ones are eligible for a given wave is decided by " +
             "each archetype's minWave.")]
    [SerializeField] private EnemyArchetype[] archetypes = System.Array.Empty<EnemyArchetype>();

    [Tooltip("Boss archetypes, spawned alone once every bossWaveInterval waves. Killing one ends the " +
             "run in victory.")]
    [SerializeField] private EnemyArchetype[] bossArchetypes = System.Array.Empty<EnemyArchetype>();

    [Tooltip("Enemies to spawn per player for a regular wave, before the per-wave growth below.")]
    [SerializeField] private int baseEnemiesPerPlayer = 4;

    [Tooltip("Extra enemies added to the per-player base for every wave past the first spawned one, " +
             "so late waves are harder than early ones rather than a flat repeat.")]
    [SerializeField] private float extraEnemiesPerWave = 1.5f;

    [Tooltip("Every Nth spawned wave is a boss wave instead of a regular one.")]
    [SerializeField] private int bossWaveInterval = 4;

    [Tooltip("Seconds after the previous wave is fully cleared before the next one spawns — a beat " +
             "for players to loot/regroup rather than the next wave landing on top of the last kill.")]
    [SerializeField] private float delayBetweenWaves = 3f;

    [Tooltip("Difficulty regions. Which one a spawned enemy lands in is picked after its archetype is " +
             "chosen, weighted by each eligible region's densityMultiplier — a restricted archetype " +
             "(e.g. Elite/Boss-only) can then never land outside a region it's allowed in. Empty " +
             "falls back to spawning uniformly across the whole map.")]
    [SerializeField] private RegionDefinition[] regions = System.Array.Empty<RegionDefinition>();

    /// <summary>1-based count of spawner-owned waves only — wave 1 is the opening and is never
    /// counted here, so this starts at 0 and the first spawned wave reports as wave 2 to the HUD.</summary>
    private int spawnedWaveNumber;

    private readonly List<Enemy> liveWaveEnemies = new List<Enemy>();
    private bool victoryTriggered;

    /// <summary>Raised whenever a new wave starts spawning, with the 1-based wave number (2, 3, ...)
    /// and total enemy count — PlayerHUD-adjacent code can subscribe for a "Wave 3" banner without
    /// this class knowing anything about UI.</summary>
    public System.Action<int, int> OnWaveStarted;

    /// <summary>Raised when a wave is fully cleared, before the next wave's delay starts.</summary>
    public System.Action<int> OnWaveCleared;

    private void Start()
    {
        if (archetypes.Length == 0 && bossArchetypes.Length == 0)
        {
            Debug.LogError("WaveSpawner: no archetypes and no boss archetypes assigned — there is " +
                           "nothing to spawn after the opening wave, and the run can never be won.");
        }

        if (openingWaveParent == null)
        {
            Debug.LogError("WaveSpawner: openingWaveParent is not assigned — cannot detect when the " +
                           "opening wave clears, so no further waves will ever spawn.");
            return;
        }

        StartCoroutine(WatchOpeningWave());
    }

    /// <summary>
    /// Polls rather than subscribing to each opening-wave Enemy's OnDeath: those enemies are baked
    /// into the scene by Level1Generator with no reference back to this spawner, and wiring 24
    /// individual event subscriptions from a generator that has no reason to know this class exists
    /// would couple two systems that should stay independent. A half-second poll is cheap and this
    /// only runs until wave 1 clears once.
    /// </summary>
    private IEnumerator WatchOpeningWave()
    {
        while (AnyChildAlive(openingWaveParent))
        {
            yield return new WaitForSeconds(0.5f);
        }

        yield return StartCoroutine(RunWaves());
    }

    private static bool AnyChildAlive(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Enemy enemy = parent.GetChild(i).GetComponent<Enemy>();
            if (enemy != null && enemy.IsAlive) return true;
        }
        return false;
    }

    private IEnumerator RunWaves()
    {
        while (true)
        {
            if (delayBetweenWaves > 0f)
            {
                yield return new WaitForSeconds(delayBetweenWaves);
            }

            // The run can end two ways while this loop is between waves: everyone died (game over,
            // via PauseMenuController's own death tracking) or the boss died (victory, set by
            // HandleBossDeath). Either way spawning more enemies is wasted work against a run that is
            // already over — and without the victoryTriggered check, a won run with players still
            // alive would sail straight into wave 3.
            if (victoryTriggered ||
                GameSaveController.Instance == null || !GameSaveController.Instance.AnyPlayerAlive())
            {
                yield break;
            }

            spawnedWaveNumber++;
            int waveNumber = spawnedWaveNumber + 1; // wave 1 is the opening; first spawned wave is 2.
            bool isBossWave = bossArchetypes.Length > 0 && spawnedWaveNumber % bossWaveInterval == 0;

            yield return StartCoroutine(SpawnWave(waveNumber, isBossWave));

            // SpawnWave can end with zero enemies (e.g. every archetype's minWave is above the
            // current wave) — nothing to wait on, so loop straight to the next wave rather than
            // stalling forever on an empty liveWaveEnemies list.
            if (liveWaveEnemies.Count > 0)
            {
                yield return StartCoroutine(WaitForWaveClear(waveNumber));
            }
        }
    }

    private IEnumerator SpawnWave(int waveNumber, bool isBossWave)
    {
        liveWaveEnemies.Clear();

        int playerCount = Mathf.Max(1, GameSaveController.Instance.PlayerControllers.Count);

        if (isBossWave)
        {
            EnemyArchetype boss = PickRandom(bossArchetypes);
            if (boss == null || boss.prefab == null)
            {
                Debug.LogError("WaveSpawner: boss wave triggered but no usable boss archetype is assigned.");
                    yield break;
            }
            SpawnOne(boss);
            OnWaveStarted?.Invoke(waveNumber, 1);
            yield break;
        }

        int enemyCount = Mathf.RoundToInt(
            playerCount * (baseEnemiesPerPlayer + extraEnemiesPerWave * (waveNumber - 2)));
        enemyCount = Mathf.Max(1, enemyCount);

        List<EnemyArchetype> eligible = EligibleArchetypes(waveNumber);
        if (eligible.Count == 0)
        {
            Debug.LogError($"WaveSpawner: no archetype has minWave <= {waveNumber} — wave {waveNumber} " +
                           "spawns nothing. Check the archetype table's minWave values.");
            yield break;
        }

        for (int i = 0; i < enemyCount; i++)
        {
            SpawnOne(PickWeighted(eligible));
            // Spread spawns across a couple of frames rather than one Instantiate storm — a dozen-plus
            // enemies landing (and each running Awake/Start) on a single frame is the kind of spike a
            // player notices as a hitch, especially in a four-player split-screen-adjacent camera.
            if (i % 4 == 3) yield return null;
        }

        OnWaveStarted?.Invoke(waveNumber, enemyCount);
    }

    /// <summary>
    /// Archetypes eligible for a wave, gated by minWave alone — spatial (region) gating is applied
    /// separately in PickSpawnRegion once an archetype from this list has already been picked, so
    /// wave-number gating and region gating stack without either list needing to know about the other.
    /// </summary>
    private List<EnemyArchetype> EligibleArchetypes(int waveNumber)
    {
        var eligible = new List<EnemyArchetype>();
        foreach (EnemyArchetype archetype in archetypes)
        {
            if (archetype != null && archetype.prefab != null && archetype.minWave <= waveNumber)
            {
                eligible.Add(archetype);
            }
        }
        return eligible;
    }

    private static EnemyArchetype PickRandom(EnemyArchetype[] pool)
    {
        var usable = new List<EnemyArchetype>();
        foreach (EnemyArchetype a in pool)
        {
            if (a != null && a.prefab != null) usable.Add(a);
        }
        return usable.Count == 0 ? null : usable[Random.Range(0, usable.Count)];
    }

    private static EnemyArchetype PickWeighted(List<EnemyArchetype> eligible)
    {
        float totalWeight = 0f;
        foreach (EnemyArchetype a in eligible) totalWeight += Mathf.Max(0f, a.weight);

        if (totalWeight <= 0f) return eligible[Random.Range(0, eligible.Count)];

        float roll = Random.Range(0f, totalWeight);
        foreach (EnemyArchetype a in eligible)
        {
            roll -= Mathf.Max(0f, a.weight);
            if (roll <= 0f) return a;
        }
        return eligible[eligible.Count - 1];
    }

    /// <summary>
    /// Picks which region a spawned enemy of the given archetype lands in, weighted by each
    /// eligible region's densityMultiplier. Region is chosen AFTER the archetype so a restricted
    /// archetype (e.g. Elite/Boss-only) can never be assigned a region it isn't allowed in — no
    /// reject-and-retry loop needed. Returns null if no regions are configured or none are eligible.
    /// </summary>
    private RegionDefinition PickSpawnRegion(EnemyArchetype archetype)
    {
        if (regions == null || regions.Length == 0) return null;

        var eligible = new List<RegionDefinition>();
        foreach (RegionDefinition region in regions)
        {
            if (region == null) continue;
            if (!region.restrictArchetypes || System.Array.IndexOf(region.eligibleArchetypes, archetype) >= 0)
            {
                eligible.Add(region);
            }
        }
        if (eligible.Count == 0) return null;

        float totalWeight = 0f;
        foreach (RegionDefinition region in eligible) totalWeight += Mathf.Max(0f, region.densityMultiplier);

        if (totalWeight <= 0f) return eligible[Random.Range(0, eligible.Count)];

        float roll = Random.Range(0f, totalWeight);
        foreach (RegionDefinition region in eligible)
        {
            roll -= Mathf.Max(0f, region.densityMultiplier);
            if (roll <= 0f) return region;
        }
        return eligible[eligible.Count - 1];
    }

    private void SpawnOne(EnemyArchetype archetype)
    {
        Vector3 center = mapGenerator != null ? mapGenerator.GetIslandCenter() : Vector3.zero;
        float playableRadius = mapGenerator != null ? mapGenerator.GetPlayableRadius() : 26f;

        RegionDefinition region = PickSpawnRegion(archetype);
        float minFraction = region != null ? region.minFraction : 0f;
        float maxFraction = region != null ? region.maxFraction : 1f;
        float radius = Random.Range(minFraction, maxFraction) * playableRadius;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 position = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

        GameObject instance = Instantiate(archetype.prefab, position, Quaternion.LookRotation(-position.normalized, Vector3.up));
        instance.name = $"{archetype.archetypeName}_{spawnedWaveNumber}_{liveWaveEnemies.Count}";
        instance.transform.SetParent(transform, true);

        Enemy enemy = instance.GetComponent<Enemy>();
        if (enemy == null)
        {
            Debug.LogError($"WaveSpawner: archetype '{archetype.archetypeName}' prefab has no Enemy component — spawned but will never move or die.");
            return;
        }

        liveWaveEnemies.Add(enemy);
        if (archetype.isBoss)
        {
            enemy.OnDeath += HandleBossDeath;
        }
    }

    private void HandleBossDeath()
    {
        if (victoryTriggered) return;
        victoryTriggered = true;

        if (PauseMenuController.Instance != null)
        {
            PauseMenuController.Instance.ShowVictory();
        }
        else
        {
            Debug.LogError("WaveSpawner: boss died but PauseMenuController.Instance is missing — no victory screen can be shown.");
        }
    }

    private IEnumerator WaitForWaveClear(int waveNumber)
    {
        while (true)
        {
            liveWaveEnemies.RemoveAll(e => e == null);
            bool anyAlive = false;
            foreach (Enemy e in liveWaveEnemies)
            {
                if (e.IsAlive) { anyAlive = true; break; }
            }
            if (!anyAlive) break;

            // Victory already ended the run — no reason to keep polling a boss wave that will never
            // report "cleared" in the normal sense (ShowVictory already fired from HandleBossDeath).
            if (victoryTriggered) yield break;

            yield return new WaitForSeconds(0.5f);
        }

        OnWaveCleared?.Invoke(waveNumber);
    }
}
