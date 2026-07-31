using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>
/// Runs in Level1.unity — the generated, text-YAML playable level.
///
/// These tests pin the facts that make Level1 actually playable and that a generated scene can
/// silently get wrong: the player spawns ON the island (not in the ocean or under the terrain),
/// MapGenerator's runtime queries answer, the follow camera tracks the spawned player, enemies
/// exist and home in, and a gamepad player is paired to the gamepad (the regression that hid in
/// SampleScene's baked state).
/// </summary>
public class Level1Tests : HarnessFixture
{
    const string LevelScene = "Level1";

    /// <summary>
    /// Structure: exactly one player, standing on the island surface, with the terrain live.
    /// Spawn height is the subtle one — TopDownPlayerController.Awake clamps spawn y to its own
    /// groundLevel (0), so if the terrain were not offset to put its surface at y = 0 the player
    /// would start buried and fall forever.
    /// </summary>
    [UnityTest]
    public IEnumerator Level1_SpawnsOnePlayerStandingOnTheIsland()
    {
        IgnoreFailingLogs();

        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;

        TopDownPlayerController[] players = Object.FindObjectsByType<TopDownPlayerController>(FindObjectsSortMode.None);
        Assert.AreEqual(1, players.Length,
            "Level1 must contain exactly one player (PlayerSpawner's clone). More than one means a " +
            "baked player crept into the scene — the condition behind the HUD/input mismatch bug.");

        var mapGenerator = Object.FindAnyObjectByType<MapGenerator>();
        Assert.IsNotNull(mapGenerator, "Level1 has no MapGenerator — ground speed and hole detection are dead.");
        Assert.IsTrue(mapGenerator.IsMapGenerated(),
            "MapGenerator did not generate at runtime, so every ground query returns a default.");

        Transform player = players[0].transform;
        Vector3 islandCenter = mapGenerator.GetIslandCenter();

        // Terrain height under the player, sampled from the live terrain rather than assumed.
        var terrain = Object.FindAnyObjectByType<Terrain>();
        Assert.IsNotNull(terrain, "Level1 has no Terrain.");
        float groundY = terrain.SampleHeight(player.position) + terrain.transform.position.y;

        Assert.AreEqual(groundY, player.position.y, 1.5f,
            $"Player spawned at y={player.position.y:F2} but the island surface under it is y={groundY:F2}. " +
            "The terrain origin and the controller's groundLevel disagree — the player is buried or floating.");

        float distanceFromCenter = Vector2.Distance(
            new Vector2(player.position.x, player.position.z),
            new Vector2(islandCenter.x, islandCenter.z));
        Assert.Less(distanceFromCenter, 20f,
            $"Player spawned {distanceFromCenter:F1}u from the island centre — that is out past the beach, in the ocean.");

        Shot("level1-spawn");
    }

    /// <summary>The follow camera must actually be tracking the runtime-spawned player.</summary>
    [UnityTest]
    public IEnumerator Level1_FollowCameraTracksTheSpawnedPlayer()
    {
        IgnoreFailingLogs();

        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;

        var camera = Object.FindAnyObjectByType<TopDownCameraController>();
        Assert.IsNotNull(camera, "Level1 has no TopDownCameraController.");

        FieldInfo field = typeof(TopDownCameraController).GetField("players", BindingFlags.NonPublic | BindingFlags.Instance);
        var tracked = field?.GetValue(camera) as Transform[];
        Assert.IsNotNull(tracked, "TopDownCameraController.players is null — PlayerSpawner never registered anyone.");
        Assert.AreEqual(1, tracked.Length,
            "The follow camera should track exactly the one spawned player. Empty means PlayerSpawner's " +
            "followCamera reference is not wired, so the camera stares at empty terrain.");

        var player = Object.FindAnyObjectByType<TopDownPlayerController>();
        Assert.AreSame(player.transform, tracked[0], "The camera is tracking something other than the player.");

        // The camera must be looking at the player, not merely holding a reference to it.
        Vector3 toPlayer = (player.transform.position - camera.transform.position).normalized;
        float alignment = Vector3.Dot(camera.transform.forward, toPlayer);
        Assert.Greater(alignment, 0.85f,
            $"Camera forward is only {alignment:F2} aligned with the direction to the player — it is not framing them.");

        Shot("level1-camera");
    }

    /// <summary>Enemies exist, are alive, and close in on the player once it is in their pull range.</summary>
    [UnityTest]
    public IEnumerator Level1_EnemiesExistAndHomeInOnThePlayer()
    {
        IgnoreFailingLogs();

        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;

        Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        Assert.Greater(enemies.Length, 0, "Level1 has no enemies — nothing to fight and no souls to earn.");
        foreach (Enemy enemy in enemies)
        {
            Assert.IsTrue(enemy.IsAlive, $"{enemy.gameObject.name} starts dead.");
        }

        var player = Object.FindAnyObjectByType<TopDownPlayerController>();

        // Nearest enemy: Enemy.FixedUpdate only pulls within pullRange (15u), and the ring sits at
        // 16u from centre, so measure the one that is genuinely in range rather than all of them.
        Enemy nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Enemy enemy in enemies)
        {
            float distance = Vector3.Distance(enemy.transform.position, player.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = enemy;
            }
        }
        Assert.Less(nearestDistance, 15f,
            $"Nearest enemy is {nearestDistance:F1}u away, outside Enemy.pullRange — no enemy will ever approach.");

        float before = nearestDistance;
        yield return new WaitForSeconds(2f);
        float after = Vector3.Distance(nearest.transform.position, player.transform.position);

        Assert.Less(after, before,
            $"Nearest enemy did not close in over 2s ({before:F2}u -> {after:F2}u). Enemy AI is not running " +
            "(it finds the player by 'Player' tag, so an untagged player prefab breaks this silently).");

        Shot("level1-enemies");
    }

    /// <summary>
    /// Holes must be pits, not chasms. GenerateHoles used to slam hole pixels to heightmap 0 — the
    /// absolute terrain floor, 20u down and well below the waterline — so the map-wide water quad
    /// rendered inside every hole and the shader's shore foam ringed it in white. Visually that read
    /// as a blue canyon; mechanically the fall-through pit was under water. Pins both: holes exist,
    /// and every hole's floor stays above the waterline.
    /// </summary>
    [UnityTest]
    public IEnumerator Level1_HolesArePitsAboveTheWaterline()
    {
        IgnoreFailingLogs();

        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;

        var mapGenerator = Object.FindAnyObjectByType<MapGenerator>();
        Assert.IsTrue(mapGenerator.IsMapGenerated(), "MapGenerator did not generate at runtime.");

        var terrain = Object.FindAnyObjectByType<Terrain>();
        Transform water = null;
        for (int i = 0; i < terrain.transform.childCount; i++)
        {
            Transform child = terrain.transform.GetChild(i);
            if (child.name.StartsWith("WaterSurface_")) { water = child; break; }
        }
        Assert.IsNotNull(water, "No WaterSurface_ quad under the terrain — MapGenerator.SpawnOceanSurface did not run.");
        float waterY = water.position.y;

        // Sweep the island plateau on a 1u grid rather than reaching into MapGenerator's private
        // hole list: IsHoleAtPosition is what the player controller actually queries to start a fall.
        Vector3 center = mapGenerator.GetIslandCenter();
        int holeSamples = 0;
        float lowestHoleY = float.MaxValue;
        for (float dx = -30f; dx <= 30f; dx += 1f)
        {
            for (float dz = -30f; dz <= 30f; dz += 1f)
            {
                var probe = new Vector3(center.x + dx, 0f, center.z + dz);
                if (!mapGenerator.IsHoleAtPosition(probe)) continue;

                holeSamples++;
                float groundY = terrain.SampleHeight(probe) + terrain.transform.position.y;
                if (groundY < lowestHoleY) lowestHoleY = groundY;
            }
        }

        Assert.Greater(holeSamples, 0,
            "No hole ground found anywhere on the island — holeCount is set but GenerateHoles painted nothing, " +
            "so the level has no fall-through hazard at all.");
        Assert.Greater(lowestHoleY, waterY,
            $"Deepest hole floor is y={lowestHoleY:F2}, below the waterline y={waterY:F2}. The water quad renders " +
            "inside the pit and the shader's shore foam outlines it — the hole reads as a flooded chasm.");
    }

    /// <summary>
    /// Pressing left must move the player left ON SCREEN — the thing a player actually judges.
    ///
    /// The regression: Level1's camera rig was generated at rotationAngle (yaw) 45, while
    /// TopDownPlayerController maps stick input straight onto world axes
    /// (new Vector3(input.x, 0, input.y)) with no camera-relative transform. World -X under a
    /// 45-degree-yawed camera projects to down-and-left, so holding left walked the player diagonally
    /// toward the bottom-left corner. Every existing test still passed: the player moved, the camera
    /// followed, the scene was valid — nothing compared input direction against screen direction.
    ///
    /// Asserting in screen space rather than pinning yaw == 0 keeps this honest under either fix:
    /// zero the yaw (what was done) or make movement camera-relative (the better long-term fix).
    /// Both satisfy this test; a rig whose angles silently drift again does not.
    ///
    /// "On screen" is measured by projecting the player's world displacement onto the camera's own
    /// ground-plane basis (its right vector, and its forward flattened to XZ) rather than by diffing
    /// Camera.WorldToScreenPoint. A follow camera chases the player, so raw screen coordinates
    /// largely cancel the very motion under test and what survives is SmoothDamp lag — an earlier
    /// draft of this test measured exactly that and reported nonsense angles. The projection is what
    /// the player's eye actually integrates: which way the world slid past them.
    /// </summary>
    [UnityTest]
    public IEnumerator Level1_StickDirectionMatchesOnScreenDirection()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();
        yield return LoadScene(LevelScene);
        yield return null;
        yield return null;

        var player = Object.FindAnyObjectByType<TopDownPlayerController>();
        Assert.IsNotNull(player, "No player spawned into Level1.");

        var rig = Object.FindAnyObjectByType<TopDownCameraController>();
        Camera camera = rig.GetCamera();
        Assert.IsNotNull(camera, "TopDownCameraController has no Camera.");

        // Enemies home in and knock the player back, which would contaminate a displacement
        // measurement with motion the stick did not cause. Their own behaviour is covered by
        // Level1_EnemiesExistAndHomeInOnThePlayer; here they are noise, so clear them. Everything
        // else about the scene stays exactly as generated.
        foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            Object.Destroy(enemy.gameObject);
        }
        yield return null;

        // The camera's ground-plane basis: screen-right and screen-up as they exist on the XZ plane.
        Vector3 screenRight = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
        Vector3 screenUp = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;

        // Cardinal directions, and the screen axis each one must actually push the player along.
        // Tolerance is generous on the intended axis but strict on the cross axis: the bug was a
        // 45-degree shear, which shows up as cross-axis drift as large as the intended motion.
        var cases = new (string name, Vector2 stick, Vector2 expectedScreen)[]
        {
            ("left", Vector2.left, Vector2.left),
            ("right", Vector2.right, Vector2.right),
            ("up", Vector2.up, Vector2.up),
            ("down", Vector2.down, Vector2.down),
        };

        foreach ((string name, Vector2 stick, Vector2 expectedScreen) in cases)
        {
            Vector3 startWorld = player.transform.position;

            yield return HoldStick(pad, stick, 0.6f);
            // Movement is force-based, so the player keeps coasting after the stick releases. Let the
            // drag settle before sampling, otherwise the tail of the previous direction leaks in.
            yield return new WaitForSeconds(0.3f);

            Vector3 delta = player.transform.position - startWorld;
            delta.y = 0f;

            Assert.Greater(delta.magnitude, 0.5f,
                $"Holding {name} moved the player only {delta.magnitude:F2}u — input never reached movement, " +
                "so the direction assertions below would be vacuous.");

            // Displacement expressed in screen axes.
            var onScreen = new Vector2(Vector3.Dot(delta, screenRight), Vector3.Dot(delta, screenUp)).normalized;

            float along = Vector2.Dot(onScreen, expectedScreen);
            float across = Vector2.Dot(onScreen, new Vector2(-expectedScreen.y, expectedScreen.x));

            Assert.Greater(along, 0.9f,
                $"Holding {name} moved the player {Mathf.Acos(Mathf.Clamp(along, -1f, 1f)) * Mathf.Rad2Deg:F0} degrees " +
                $"off the expected on-screen direction (on-screen delta {onScreen}). The camera rig's yaw and the " +
                "world-axis input mapping disagree — this is the diagonal-movement bug.");
            Assert.Less(Mathf.Abs(across), 0.35f,
                $"Holding {name} drifted sideways on screen (cross-axis component {across:F2}), i.e. it moved " +
                "diagonally instead of straight.");
        }

        Shot("level1-movement-direction");
    }

    /// <summary>
    /// Gamepad path end to end: join in CharacterSelection, start, and land in Level1 with the
    /// player paired to the gamepad. This is the regression that hid in SampleScene — pairing was
    /// discarded on activation and the player silently ended up on the keyboard.
    /// </summary>
    [UnityTest]
    public IEnumerator CharacterSelection_StartsLevel1WithTheGamepadPaired()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();
        yield return LoadScene("CharacterSelection");
        yield return null;

        yield return Press(pad, GamepadButton.South); // join
        yield return null;
        yield return Press(pad, GamepadButton.South); // confirm character
        yield return null;
        yield return Press(pad, GamepadButton.Start); // start run
        yield return null;
        yield return null;

        Assert.AreEqual(LevelScene, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            "Start did not transition into Level1 — CharacterSelectionManager.gameplaySceneName is still pointing elsewhere.");

        var player = Object.FindAnyObjectByType<TopDownPlayerController>();
        Assert.IsNotNull(player, "No player spawned into Level1 after starting the run.");

        var playerInput = player.GetComponent<PlayerInput>();
        Assert.IsNotNull(playerInput, "The spawned player has no PlayerInput.");
        Assert.AreEqual("Gamepad", playerInput.currentControlScheme,
            $"Player joined with a gamepad but resolved to the '{playerInput.currentControlScheme}' scheme. " +
            "PlayerInput.OnEnable unpaired the device and grabbed the keyboard instead.");
        Assert.AreEqual(1, playerInput.devices.Count,
            "The gamepad player should be paired to exactly the gamepad.");
        Assert.IsInstanceOf<Gamepad>(playerInput.devices[0],
            $"Player is paired to '{playerInput.devices[0]}' rather than a Gamepad.");

        // The prompt the HUD prints derives from the same pairing, so it is the visible symptom.
        Assert.AreEqual("Y", player.GetSkillBindingLabel(1),
            "HUD skill prompt is not the gamepad glyph, so the pairing is still wrong.");

        Shot("level1-gamepad-start");
    }
}
