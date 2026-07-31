using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// Runs in AgentGymCoop.unity — the two-player variant of the gym.
///
/// Why a separate scene: TopDownCameraController has two branches that only execute with 2+ players
/// (CalculateCentralizedPosition's averaging, and the spread-based zoom-out in UpdateCameraPosition).
/// With a single player the average IS that player and the spread is always 0, so a single-player
/// scene cannot distinguish "centres on the midpoint" from "centres on player 0" — both look
/// identical. Every camera assertion in the suite before this one had that blind spot.
///
/// The framing constants (distance 11, height 0, tilt 55, yaw 0) are shared with Level1/AgentGym/
/// SampleScene, so what is verified here is the same rig the game ships.
/// </summary>
public class CoopCameraTests : HarnessFixture
{
    const string CoopScene = "AgentGymCoop";

    /// <summary>
    /// Both players spawn, are distinct objects at distinct positions, and both are registered with
    /// the camera. Everything below is meaningless if the scene quietly contains one player.
    /// </summary>
    [UnityTest]
    public IEnumerator Coop_SpawnsTwoDistinctPlayersBothRegisteredWithTheCamera()
    {
        IgnoreFailingLogs();

        yield return LoadScene(CoopScene);
        yield return null;
        yield return null;

        TopDownPlayerController[] players = Object.FindObjectsByType<TopDownPlayerController>(FindObjectsSortMode.None);
        Assert.AreEqual(2, players.Length,
            $"AgentGymCoop must contain exactly two players, found {players.Length}. " +
            "PlayerSpawner.fallbackPlayerCount is not 2, or the scene was generated from the single-player path.");

        Assert.AreNotSame(players[0], players[1], "The two 'players' are the same object.");
        Assert.Greater(Vector3.Distance(players[0].transform.position, players[1].transform.position), 0.5f,
            "Both players spawned on the same spot — they would shove each other apart and no " +
            "midpoint measurement below would mean anything.");

        Transform[] tracked = TrackedPlayers();
        Assert.AreEqual(2, tracked.Length,
            $"The camera tracks {tracked.Length} player(s), not 2. PlayerSpawner only registered some of them, " +
            "so the camera would centre on a subset and leave the other player off-centre or off-screen.");

        Shot("coop-two-players");
    }

    /// <summary>
    /// THE assertion this was asked for: with two players present the camera sits over their
    /// MIDPOINT, not over either player.
    ///
    /// Checked by projecting camera-to-midpoint onto the ground plane, which is what "centred"
    /// means for a tilted rig — the camera body is deliberately behind and above the target, so
    /// comparing raw positions would fail on a correct camera. The distinguishing part is the
    /// comparison against each individual player: a camera centred on player 0 also sits "near" the
    /// midpoint when the players are close, so the test additionally requires that the midpoint is
    /// closer to the camera's aim than either player is.
    /// </summary>
    [UnityTest]
    public IEnumerator Coop_CameraCentresOnTheMidpointNotOnEitherPlayer()
    {
        IgnoreFailingLogs();

        yield return LoadScene(CoopScene);
        yield return null;

        TopDownPlayerController[] players = Object.FindObjectsByType<TopDownPlayerController>(FindObjectsSortMode.None);
        Assert.AreEqual(2, players.Length, "Expected two players.");

        // Let SmoothDamp converge — the rig eases toward its solved position rather than snapping.
        yield return new WaitForSeconds(1.5f);

        Vector3 a = Ground(players[0].transform.position);
        Vector3 b = Ground(players[1].transform.position);
        Vector3 midpoint = (a + b) * 0.5f;

        Vector3 aim = CameraGroundAim();

        float toMidpoint = Vector3.Distance(aim, midpoint);
        float toPlayerA = Vector3.Distance(aim, a);
        float toPlayerB = Vector3.Distance(aim, b);

        Assert.Less(toMidpoint, 1.5f,
            $"Camera aims at {aim} but the players' midpoint is {midpoint} ({toMidpoint:F2}u away). " +
            "The camera is not centred between the two players.");

        // With the players separated, the midpoint must beat both individuals — this is what fails if
        // the camera follows only the first registered player.
        float separation = Vector3.Distance(a, b);
        Assert.Greater(separation, 2f,
            $"Players are only {separation:F2}u apart, too close for 'midpoint vs individual player' to " +
            "be a meaningful distinction. Spawn points moved?");

        Assert.Less(toMidpoint, toPlayerA,
            $"Camera aim is closer to player 0 ({toPlayerA:F2}u) than to the midpoint ({toMidpoint:F2}u) — " +
            "it is following one player instead of centring on both.");
        Assert.Less(toMidpoint, toPlayerB,
            $"Camera aim is closer to player 1 ({toPlayerB:F2}u) than to the midpoint ({toMidpoint:F2}u) — " +
            "it is following one player instead of centring on both.");

        Shot("coop-camera-centred");
    }

    /// <summary>
    /// Both players must be visible at once. This is the failure the user would actually see: a
    /// correctly-"centred" camera can still be wrong if it is pulled in so close that the second
    /// player sits off-screen. Directly relevant to distance 11 / height 0, which is a tighter
    /// framing than the rig previously used.
    /// </summary>
    [UnityTest]
    public IEnumerator Coop_BothPlayersAreOnScreen()
    {
        IgnoreFailingLogs();

        yield return LoadScene(CoopScene);
        yield return null;

        TopDownPlayerController[] players = Object.FindObjectsByType<TopDownPlayerController>(FindObjectsSortMode.None);
        Assert.AreEqual(2, players.Length, "Expected two players.");

        Camera camera = Rig().GetCamera();
        yield return new WaitForSeconds(1.5f);

        foreach (TopDownPlayerController player in players)
        {
            AssertVisible(camera, player);
        }

        Shot("coop-both-on-screen");
    }

    /// <summary>
    /// The camera must re-centre when the players' midpoint moves, not merely start centred.
    /// Drives player 0 with a real gamepad and re-checks the midpoint after it has walked away.
    ///
    /// This is the test that separates a genuinely tracking camera from one that happens to be
    /// correct at the spawn layout (both spawn points are symmetric about the origin, so a camera
    /// hardcoded at the origin would pass the static midpoint test above).
    /// </summary>
    [UnityTest]
    public IEnumerator Coop_CameraFollowsTheMidpointWhenOnePlayerMoves()
    {
        IgnoreFailingLogs();

        Gamepad pad = AddPad();
        yield return LoadScene(CoopScene);
        yield return null;
        yield return null;

        TopDownPlayerController[] players = Object.FindObjectsByType<TopDownPlayerController>(FindObjectsSortMode.None);
        Assert.AreEqual(2, players.Length, "Expected two players.");

        // Only the gamepad-paired player should respond. Identify it rather than assuming an order:
        // PlayerInput auto-assigns schemes, so which controller the pad drives is not guaranteed.
        TopDownPlayerController driven = null;
        foreach (TopDownPlayerController player in players)
        {
            var input = player.GetComponent<PlayerInput>();
            if (input == null) continue;
            foreach (InputDevice device in input.devices)
            {
                if (device == pad) { driven = player; break; }
            }
            if (driven != null) break;
        }
        Assert.IsNotNull(driven,
            "Neither player is paired to the synthetic gamepad, so no input can move anyone. " +
            "PlayerInput grabbed the keyboard scheme for both.");

        yield return new WaitForSeconds(1f);
        Vector3 aimBefore = CameraGroundAim();
        Vector3 drivenBefore = driven.transform.position;

        yield return HoldStick(pad, Vector2.up, 1.2f);
        yield return new WaitForSeconds(1.5f); // let the rig converge on the new midpoint

        float moved = Vector3.Distance(Ground(drivenBefore), Ground(driven.transform.position));
        Assert.Greater(moved, 1f,
            $"The gamepad player only moved {moved:F2}u — input did not drive it, so this test proves nothing.");

        Vector3 a = Ground(players[0].transform.position);
        Vector3 b = Ground(players[1].transform.position);
        Vector3 midpoint = (a + b) * 0.5f;
        Vector3 aimAfter = CameraGroundAim();

        Assert.Greater(Vector3.Distance(aimBefore, aimAfter), 0.3f,
            "The camera did not move at all while a player walked away — it is static, not following.");

        Assert.Less(Vector3.Distance(aimAfter, midpoint), 1.5f,
            $"After one player moved, the camera aims at {aimAfter} but the new midpoint is {midpoint} " +
            $"({Vector3.Distance(aimAfter, midpoint):F2}u away). The camera did not re-centre.");

        // Re-centring is not enough on its own: the players are now further apart than at spawn, and a
        // centred camera that does not zoom out to match leaves someone off screen. Checked here as
        // well as at spawn because the spread-based zoom only has anything to do once they separate.
        Camera camera = Rig().GetCamera();
        foreach (TopDownPlayerController player in players)
        {
            AssertVisible(camera, player);
        }

        Shot("coop-camera-recentred");
    }

    /// <summary>
    /// "This player can actually be seen." Deliberately more than a viewport-bounds check.
    ///
    /// Viewport coordinates alone are not visibility, and trusting them hid a real bug: a player that
    /// had fallen THROUGH the floor still projected to x/y inside 0..1 with positive z, so every
    /// camera assertion passed while the screenshot showed one player. WorldToViewportPoint knows
    /// nothing about what is in front of the point. So this also requires the player to be standing
    /// above the ground plane, be active, and have renderers enabled.
    /// </summary>
    static void AssertVisible(Camera camera, TopDownPlayerController player)
    {
        string name = player.gameObject.name;
        Vector3 position = player.transform.position;

        Assert.IsTrue(player.gameObject.activeInHierarchy, $"{name} is deactivated — nothing is drawn.");
        Assert.Greater(CountVisibleRenderers(player), 0, $"{name} has no enabled renderers — it is invisible.");

        // Feet on the floor. A submerged player is occluded by the ground mesh no matter how good its
        // viewport coordinates look.
        Assert.Greater(position.y, -0.5f,
            $"{name} is at y={position.y:F2}, below the ground plane — it has fallen through the floor and " +
            "is hidden by the ground mesh, however well-framed the camera is.");

        // Sample the body, not the pivot: the pivot is at the feet, and a player can be framed with
        // their feet just off the bottom edge while still visibly on screen.
        Vector3 viewport = camera.WorldToViewportPoint(position + Vector3.up);

        Assert.Greater(viewport.z, 0f, $"{name} is BEHIND the camera (viewport z={viewport.z:F2}).");
        Assert.That(viewport.x, Is.InRange(0f, 1f),
            $"{name} is off screen horizontally (viewport x={viewport.x:F2}). The camera is centred but " +
            "framed too tightly to fit both players.");
        Assert.That(viewport.y, Is.InRange(0f, 1f),
            $"{name} is off screen vertically (viewport y={viewport.y:F2}). The camera is centred but " +
            "framed too tightly to fit both players.");
    }

    /// <summary>
    /// The camera's own field, read back — a direct check that both players are registered rather
    /// than inferred from where the camera ended up. Guards the wiring that a scene regeneration
    /// could silently drop.
    /// </summary>
    static Transform[] TrackedPlayers()
    {
        FieldInfo field = typeof(TopDownCameraController)
            .GetField("players", BindingFlags.NonPublic | BindingFlags.Instance);
        var tracked = field?.GetValue(Rig()) as Transform[];
        Assert.IsNotNull(tracked, "TopDownCameraController.players is null — nobody was registered.");
        return tracked;
    }

    static TopDownCameraController Rig()
    {
        var rig = Object.FindAnyObjectByType<TopDownCameraController>();
        Assert.IsNotNull(rig, "The scene has no TopDownCameraController.");
        return rig;
    }

    /// <summary>
    /// Where the camera is actually looking, on the ground plane (y = 0).
    ///
    /// Ray-casts the camera's forward against y = 0 rather than comparing transform positions: the
    /// rig deliberately sits behind and above its target (distance 11, tilt 55), so its position is
    /// never equal to what it is centred on. This is the geometric definition of "centred" for a
    /// tilted top-down camera.
    /// </summary>
    static Vector3 CameraGroundAim()
    {
        Transform cam = Rig().transform;
        float denominator = cam.forward.y;
        Assert.Less(denominator, -0.01f,
            $"The camera is not looking downward (forward.y={denominator:F2}), so it never meets the ground plane.");
        float t = -cam.position.y / denominator;
        return Ground(cam.position + cam.forward * t);
    }

    static Vector3 Ground(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static int CountVisibleRenderers(Component root)
    {
        int visible = 0;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.enabled && renderer.gameObject.activeInHierarchy) visible++;
        }
        return visible;
    }
}
