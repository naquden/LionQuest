using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Pins the roguelike map-regeneration contract: the same seed always reproduces the same map,
/// different seeds produce different maps (so "every run is unique" actually holds), and region
/// difficulty escalates monotonically with distance from the island centre — independent of any
/// visual judgement of the generated terrain.
/// </summary>
public class RegionTests : HarnessFixture
{
    const string LevelScene = "Level1";

    /// <summary>Same seed twice must produce identical ground-type layout at fixed sample points.</summary>
    [UnityTest]
    public IEnumerator SameSeed_ProducesIdenticalGroundTypes()
    {
        IgnoreFailingLogs();

        yield return LoadScene(LevelScene);
        yield return null;

        var mapGenerator = Object.FindAnyObjectByType<MapGenerator>();
        Assert.IsNotNull(mapGenerator, "Level1 has no MapGenerator.");

        Vector3 center = mapGenerator.GetIslandCenter();
        Vector3[] samples = SamplePoints(center, mapGenerator.GetPlayableRadius());

        mapGenerator.GenerateMap(12345);
        GroundType[] first = SampleGroundTypes(mapGenerator, samples);

        mapGenerator.GenerateMap(12345);
        GroundType[] second = SampleGroundTypes(mapGenerator, samples);

        for (int i = 0; i < samples.Length; i++)
        {
            Assert.AreEqual(first[i], second[i],
                $"Sample {i} differs between two GenerateMap(12345) calls — the seed is not fully " +
                "determining the layout (something outside Random.InitState's scope is leaking in).");
        }
    }

    /// <summary>Different seeds must produce a different layout — otherwise "unique every run" is false.</summary>
    [UnityTest]
    public IEnumerator DifferentSeeds_ProduceDifferentGroundTypes()
    {
        IgnoreFailingLogs();

        yield return LoadScene(LevelScene);
        yield return null;

        var mapGenerator = Object.FindAnyObjectByType<MapGenerator>();
        Vector3 center = mapGenerator.GetIslandCenter();
        Vector3[] samples = SamplePoints(center, mapGenerator.GetPlayableRadius());

        mapGenerator.GenerateMap(1);
        GroundType[] first = SampleGroundTypes(mapGenerator, samples);

        mapGenerator.GenerateMap(2);
        GroundType[] second = SampleGroundTypes(mapGenerator, samples);

        int differences = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            if (first[i] != second[i]) differences++;
        }

        Assert.Greater(differences, 0,
            "Seeds 1 and 2 produced identical ground types at every sample point — GenerateMap(int) " +
            "is not actually varying the layout by seed.");
    }

    /// <summary>Region density must never decrease as distance from the island centre increases.</summary>
    [UnityTest]
    public IEnumerator RegionDensity_IsNonDecreasingWithDistance()
    {
        IgnoreFailingLogs();

        yield return LoadScene(LevelScene);
        yield return null;

        var mapGenerator = Object.FindAnyObjectByType<MapGenerator>();
        Vector3 center = mapGenerator.GetIslandCenter();
        float radius = mapGenerator.GetPlayableRadius();

        float previousDensity = -1f;
        for (float t = 0.01f; t < 1f; t += 0.05f)
        {
            Vector3 probe = center + new Vector3(t * radius, 0f, 0f);
            RegionDefinition region = mapGenerator.GetRegionAt(probe);
            Assert.IsNotNull(region, $"No region matched fraction {t:F2} of playable radius.");

            Assert.GreaterOrEqual(region.densityMultiplier, previousDensity,
                $"Region '{region.regionName}' at fraction {t:F2} has densityMultiplier " +
                $"{region.densityMultiplier}, lower than a region closer to the centre " +
                $"({previousDensity}) — difficulty must not decrease with distance.");
            previousDensity = region.densityMultiplier;
        }
    }

    /// <summary>An Elite/Boss-only region's WaveSpawner eligibility must exclude the Grunt archetype
    /// regardless of wave number.</summary>
    [UnityTest]
    public IEnumerator RestrictedRegion_ExcludesIneligibleArchetypeFromSpawnSelection()
    {
        IgnoreFailingLogs();

        yield return LoadScene(LevelScene);
        yield return null;

        var spawner = Object.FindAnyObjectByType<WaveSpawner>();
        Assert.IsNotNull(spawner, "Level1 has no WaveSpawner.");

        var regionsField = typeof(WaveSpawner).GetField("regions", BindingFlags.NonPublic | BindingFlags.Instance);
        var regions = regionsField?.GetValue(spawner) as RegionDefinition[];
        Assert.IsNotNull(regions, "WaveSpawner.regions is null or missing.");

        RegionDefinition restricted = null;
        foreach (RegionDefinition region in regions)
        {
            if (region != null && region.restrictArchetypes) { restricted = region; break; }
        }
        Assert.IsNotNull(restricted, "No restricted region found — expected at least Shattered Reach/The Maw.");

        var archetypesField = typeof(WaveSpawner).GetField("archetypes", BindingFlags.NonPublic | BindingFlags.Instance);
        var archetypes = archetypesField?.GetValue(spawner) as EnemyArchetype[];
        Assert.IsNotNull(archetypes, "WaveSpawner.archetypes is null or missing.");

        EnemyArchetype grunt = null;
        foreach (EnemyArchetype archetype in archetypes)
        {
            if (archetype != null && archetype.archetypeName == "Grunt") { grunt = archetype; break; }
        }
        Assert.IsNotNull(grunt, "No Grunt archetype found in WaveSpawner.archetypes.");

        bool gruntEligibleInRestrictedRegion = System.Array.IndexOf(restricted.eligibleArchetypes, grunt) >= 0;
        Assert.IsFalse(gruntEligibleInRestrictedRegion,
            $"Grunt is listed as eligible in restricted region '{restricted.regionName}' — a restricted " +
            "region must only allow its authored high-tier archetypes.");
    }

    /// <summary>
    /// Every region must stay reachable on foot from the centre — regions are difficulty bands, not
    /// gates. Proxy check: walking outward along a spread of angles never crosses more than a
    /// couple of consecutive hole cells, which would otherwise wall off everything beyond it even
    /// though the region system itself imposes no lock.
    /// </summary>
    [UnityTest]
    public IEnumerator OuterRegions_AreNotWalledOffByAContinuousHoleRing()
    {
        IgnoreFailingLogs();

        yield return LoadScene(LevelScene);
        yield return null;

        var mapGenerator = Object.FindAnyObjectByType<MapGenerator>();
        Vector3 center = mapGenerator.GetIslandCenter();
        float radius = mapGenerator.GetPlayableRadius();

        const int angleSamples = 24;
        const int radialSteps = 60;
        const int maxConsecutiveHoles = 3;

        for (int a = 0; a < angleSamples; a++)
        {
            float angle = a * (360f / angleSamples) * Mathf.Deg2Rad;
            int consecutiveHoles = 0;

            for (int r = 1; r <= radialSteps; r++)
            {
                float dist = radius * r / radialSteps;
                Vector3 probe = center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                if (mapGenerator.IsHoleAtPosition(probe))
                {
                    consecutiveHoles++;
                    Assert.LessOrEqual(consecutiveHoles, maxConsecutiveHoles,
                        $"Angle {a * (360f / angleSamples):F0} deg has {consecutiveHoles} consecutive hole " +
                        $"cells around distance {dist:F1}u — a wide enough belt of holes to wall off " +
                        "everything beyond it, even though regions are meant to always stay walkable.");
                }
                else
                {
                    consecutiveHoles = 0;
                }
            }
        }
    }

    /// <summary>
    /// Visual proof that terrain stays continuous and visibly harder-looking (more Dirt/hole,
    /// less Grass) the farther out a player stands, and that no seam appears at a region
    /// boundary. Moves the player directly rather than walking — covering 90u at normal speed
    /// would make this test slow for no extra signal, since the region system itself has no
    /// gameplay effect on movement.
    /// </summary>
    [UnityTest]
    public IEnumerator VisualCheck_TerrainAtIncreasingRadiusFractions()
    {
        IgnoreFailingLogs();

        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;

        var mapGenerator = Object.FindAnyObjectByType<MapGenerator>();
        var player = Object.FindAnyObjectByType<TopDownPlayerController>();
        Assert.IsNotNull(player, "No player spawned into Level1.");

        Vector3 center = mapGenerator.GetIslandCenter();
        float radius = mapGenerator.GetPlayableRadius();

        var body = player.GetComponent<Rigidbody>();

        (string label, float fraction)[] samples =
        {
            ("50pct", 0.5f), ("80pct", 0.8f), ("95pct", 0.95f),
        };

        foreach (var (label, fraction) in samples)
        {
            Vector3 target = center + new Vector3(fraction * radius, 0f, 0f);
            if (body != null) body.linearVelocity = Vector3.zero;
            player.transform.position = new Vector3(target.x, player.transform.position.y, target.z);

            // Let the follow camera catch up before capturing.
            yield return null;
            yield return new WaitForSeconds(1.5f);

            Shot($"region-terrain-{label}");
        }
    }

    static Vector3[] SamplePoints(Vector3 center, float radius)
    {
        var points = new Vector3[8];
        for (int i = 0; i < points.Length; i++)
        {
            float t = (i + 1f) / (points.Length + 1f);
            float angle = i * 47f * Mathf.Deg2Rad; // arbitrary spread, not aligned to any symmetry
            points[i] = center + new Vector3(Mathf.Cos(angle) * t * radius, 0f, Mathf.Sin(angle) * t * radius);
        }
        return points;
    }

    static GroundType[] SampleGroundTypes(MapGenerator mapGenerator, Vector3[] samples)
    {
        var result = new GroundType[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            result[i] = mapGenerator.GetGroundTypeAtPosition(samples[i]);
        }
        return result;
    }
}
