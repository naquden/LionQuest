using UnityEngine;

/// <summary>
/// Screen-space HUD entry point. Holds one PlayerHUDPanel per local-co-op slot (0-3);
/// PlayerSpawner calls BindPlayer/UnbindPlayer as players join/leave.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private PlayerHUDPanel[] panels = System.Array.Empty<PlayerHUDPanel>();

    private void Awake()
    {
        foreach (PlayerHUDPanel panel in panels)
        {
            if (panel != null) panel.SetVisible(false);
        }
    }

    public void BindPlayer(int playerIndex, TopDownPlayerController player, CharacterStats stats, CharacterDefinition definition)
    {
        if (playerIndex < 0 || playerIndex >= panels.Length)
        {
            Debug.LogError($"PlayerHUD: playerIndex {playerIndex} is out of range (panels.Length={panels.Length}).");
            return;
        }

        PlayerHUDPanel panel = panels[playerIndex];
        if (panel == null)
        {
            Debug.LogError($"PlayerHUD: panel for playerIndex {playerIndex} is not assigned.");
            return;
        }

        panel.Bind(playerIndex, player, stats, definition);
    }

    public void UnbindPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= panels.Length)
        {
            Debug.LogError($"PlayerHUD: playerIndex {playerIndex} is out of range (panels.Length={panels.Length}).");
            return;
        }

        PlayerHUDPanel panel = panels[playerIndex];
        if (panel != null) panel.Unbind();
    }
}
