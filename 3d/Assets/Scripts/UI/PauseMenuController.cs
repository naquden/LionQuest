using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Lives on PauseMenuCanvas.prefab in the gameplay scenes. Freezes time and switches every
/// registered player's PlayerInput to the UI map while open, so gameplay input can't leak through.
///
/// Doubles as the game-over screen: the same panel, retitled, with Resume swapped for Retry. Death
/// used to be invisible outside the HUD — TopDownPlayerController.Die fired OnDeath, the portrait
/// greyed out and the health text read "0 / 0", but the run carried on with a corpse the player
/// could still walk around. This is the thing that listens.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button characterSelectionButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private string characterSelectionSceneName = "CharacterSelection";
    [SerializeField] private InputActionAsset inputActions;

    const string PausedTitle = "Paused";
    const string GameOverTitle = "Game Over";

    private InputAction cancelAction;
    private readonly List<PlayerInput> registeredPlayers = new List<PlayerInput>();
    private readonly List<TopDownPlayerController> trackedPlayers = new List<TopDownPlayerController>();

    public bool IsOpen { get; private set; }

    /// <summary>True once every registered player is dead. Terminal for this scene load — the only
    /// ways out are Retry, Character Selection and Exit, all of which leave or reload the scene.</summary>
    public bool IsGameOver { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryPressed);
        if (characterSelectionButton != null) characterSelectionButton.onClick.AddListener(OnCharacterSelectionPressed);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitPressed);

        if (panel != null) panel.SetActive(false);
        // Retry only ever appears on game over; Resume only ever appears on pause.
        if (retryButton != null) retryButton.gameObject.SetActive(false);

        if (inputActions != null)
        {
            cancelAction = inputActions.FindAction("UI/Cancel");
            cancelAction?.Enable();
        }
        else
        {
            Debug.LogError("PauseMenuController: inputActions is not assigned! Esc-to-close via UI/Cancel will not work.");
        }
    }

    private void OnDestroy()
    {
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
        if (retryButton != null) retryButton.onClick.RemoveListener(OnRetryPressed);
        if (characterSelectionButton != null) characterSelectionButton.onClick.RemoveListener(OnCharacterSelectionPressed);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExitPressed);

        foreach (TopDownPlayerController player in trackedPlayers)
        {
            if (player != null) player.OnDeath -= HandlePlayerDeath;
        }
        trackedPlayers.Clear();

        cancelAction?.Disable();

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;

        // Cancel closes the pause menu, but must not dismiss game over — there is nothing to
        // resume to, and closing it would hand the player back a dead body and a live HUD.
        if (!IsGameOver && cancelAction != null && cancelAction.WasPressedThisFrame())
        {
            Resume();
        }
    }

    /// <summary>Called by PlayerSpawner for each spawned player so the menu can switch their
    /// action map without any scene lookup.</summary>
    public void RegisterPlayer(PlayerInput playerInput)
    {
        if (playerInput == null)
        {
            Debug.LogError("PauseMenuController: RegisterPlayer called with a null PlayerInput.");
            return;
        }

        if (!registeredPlayers.Contains(playerInput))
        {
            registeredPlayers.Add(playerInput);
        }
    }

    /// <summary>Called by PlayerSpawner for each spawned player so death can end the run. An overload
    /// rather than an extra argument on RegisterPlayer(PlayerInput) so the two concerns stay
    /// independent: a player with no PlayerInput still has to be able to die, and locking input
    /// still has to work for a player whose death nobody watches.</summary>
    public void RegisterPlayer(TopDownPlayerController player)
    {
        if (player == null)
        {
            Debug.LogError("PauseMenuController: RegisterPlayer called with a null TopDownPlayerController — " +
                           "this player's death will not trigger game over.");
            return;
        }

        if (trackedPlayers.Contains(player)) return;

        trackedPlayers.Add(player);
        player.OnDeath += HandlePlayerDeath;
    }

    public void Toggle()
    {
        // Pausing a finished run would swap the title back to "Paused" and re-offer Resume.
        if (IsGameOver) return;

        if (IsOpen) Resume();
        else Open();
    }

    /// <summary>
    /// One player died. In co-op the run only ends when the last one does, so a single death
    /// leaves the others playing — matching how the HUD greys out only that player's portrait.
    /// </summary>
    private void HandlePlayerDeath()
    {
        if (IsGameOver) return;

        foreach (TopDownPlayerController player in trackedPlayers)
        {
            // A destroyed player (scene teardown mid-frame) counts as gone, not as alive.
            if (player != null && player.IsAlive) return;
        }

        ShowGameOver();
    }

    /// <summary>Ends the run: same frozen, input-locked panel as pause, retitled, Resume replaced by
    /// Retry. Public so a scene or test can end the run without killing every player by hand.</summary>
    public void ShowGameOver()
    {
        if (panel == null)
        {
            Debug.LogError("PauseMenuController: panel is not assigned — the player died and nothing can be shown!");
            return;
        }

        IsGameOver = true;
        IsOpen = true;
        panel.SetActive(true);

        if (titleText != null) titleText.text = GameOverTitle;
        else Debug.LogError("PauseMenuController: titleText is not assigned — the game-over panel will still read 'Paused'.");

        if (resumeButton != null) resumeButton.gameObject.SetActive(false);
        if (retryButton != null) retryButton.gameObject.SetActive(true);

        // Character Selection's up-link is authored to Resume, which is now hidden. Explicit
        // navigation to an inactive object doesn't fall through to the next candidate — it just
        // dead-ends — so point it at Retry, which occupies the same rung.
        if (characterSelectionButton != null && retryButton != null)
        {
            Navigation nav = characterSelectionButton.navigation;
            nav.selectOnUp = retryButton;
            characterSelectionButton.navigation = nav;
        }

        Time.timeScale = 0f;
        SetPlayerActionMap("UI");

        // Souls earned before dying are kept — the run is lost, the progression is not.
        if (GameSaveController.Instance != null)
        {
            GameSaveController.Instance.SaveAllActivePlayers();
        }

        if (EventSystem.current != null)
        {
            Button focus = retryButton != null ? retryButton : characterSelectionButton;
            if (focus != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);
        }
    }

    private void Open()
    {
        if (panel == null)
        {
            Debug.LogError("PauseMenuController: panel is not assigned!");
            return;
        }

        IsOpen = true;
        panel.SetActive(true);
        if (titleText != null) titleText.text = PausedTitle;
        Time.timeScale = 0f;
        SetPlayerActionMap("UI");

        if (EventSystem.current != null && resumeButton != null)
        {
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }
    }

    private void Resume()
    {
        // Defensive: Resume is hidden on game over, but a stale click/navigation event must not
        // un-freeze a finished run.
        if (IsGameOver) return;

        IsOpen = false;
        if (panel != null) panel.SetActive(false);
        Time.timeScale = 1f;
        SetPlayerActionMap("Player");

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void SetPlayerActionMap(string mapName)
    {
        foreach (PlayerInput playerInput in registeredPlayers)
        {
            if (playerInput != null) playerInput.SwitchCurrentActionMap(mapName);
        }
    }

    /// <summary>Restores real-time and flushes progress to disk before either leaving the run
    /// (character selection) or quitting outright.</summary>
    private void LeaveRun()
    {
        Time.timeScale = 1f;

        if (GameSaveController.Instance != null)
        {
            GameSaveController.Instance.SaveAllActivePlayers();
        }
    }

    /// <summary>Restarts the run by reloading the current scene. The selection made in
    /// CharacterSelection is preserved (CharacterSelectionData is not cleared), so Retry puts the
    /// same character back on the same map — which is what "retry" means, versus the Character
    /// Selection button next to it.</summary>
    private void OnRetryPressed()
    {
        LeaveRun();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnCharacterSelectionPressed()
    {
        LeaveRun();

        if (CharacterSelectionData.Instance != null)
        {
            CharacterSelectionData.Instance.Clear();
        }

        SceneManager.LoadScene(characterSelectionSceneName);
    }

    private void OnExitPressed()
    {
        LeaveRun();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
