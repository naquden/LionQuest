using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// An idle player must stay standing on the ground.
///
/// Written because a two-player scene surfaced a player at y = -2.85 while its viewport coordinates
/// still read as on-screen — i.e. it had sunk through the floor but every camera assertion still
/// passed. Camera framing tests are blind to this: a submerged player projects to the same screen
/// position as a standing one. Run against both gym scenes so the single- and multi-player cases are
/// distinguished rather than assumed to behave the same.
/// </summary>
public class GroundStandingTests : HarnessFixture
{
    [UnityTest]
    public IEnumerator SinglePlayerGym_PlayerStaysOnTheGroundWhileIdle()
    {
        yield return AssertPlayersStayGrounded("AgentGym", expectedPlayers: 1);
    }

    [UnityTest]
    public IEnumerator CoopGym_BothPlayersStayOnTheGroundWhileIdle()
    {
        yield return AssertPlayersStayGrounded("AgentGymCoop", expectedPlayers: 2);
    }

    /// <summary>
    /// Loads the scene, leaves every player completely idle, and samples their y for 3 seconds.
    /// No input is sent on purpose: standing still is the weakest possible demand on the controller,
    /// so a failure here is unambiguous.
    /// </summary>
    IEnumerator AssertPlayersStayGrounded(string sceneName, int expectedPlayers)
    {
        IgnoreFailingLogs();

        yield return LoadScene(sceneName);
        yield return null;
        yield return null;

        TopDownPlayerController[] players = Object.FindObjectsByType<TopDownPlayerController>(FindObjectsSortMode.None);
        Assert.AreEqual(expectedPlayers, players.Length, $"{sceneName} should contain {expectedPlayers} player(s).");

        var lowest = new float[players.Length];
        for (int i = 0; i < players.Length; i++) lowest[i] = players[i].transform.position.y;

        for (float t = 0f; t < 3f; t += 0.25f)
        {
            yield return new WaitForSeconds(0.25f);
            for (int i = 0; i < players.Length; i++)
            {
                float y = players[i].transform.position.y;
                if (y < lowest[i]) lowest[i] = y;
            }
        }

        for (int i = 0; i < players.Length; i++)
        {
            Debug.Log($"DIAG|grounded scene={sceneName} player={i} " +
                      $"finalY={players[i].transform.position.y:F2} lowestY={lowest[i]:F2}");
        }

        for (int i = 0; i < players.Length; i++)
        {
            // -1 is generous: the pivot sits at the feet, so any real standing pose keeps y at ~0.
            Assert.Greater(lowest[i], -1f,
                $"{sceneName}: player {i} sank to y={lowest[i]:F2} while standing still — it is falling " +
                "through the ground collider. CheckGrounded's raycast starts at the pivot (y=0), which is " +
                "exactly on the ground surface, so it can miss and let gravity pull the player under.");
        }
    }
}
