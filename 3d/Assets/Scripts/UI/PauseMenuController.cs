using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Lives on PauseMenuCanvas.prefab in SampleScene. Freezes time and switches every registered
/// player's PlayerInput to the UI map while open, so gameplay input can't leak through.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button characterSelectionButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private string characterSelectionSceneName = "CharacterSelection";
    [SerializeField] private InputActionAsset inputActions;

    private InputAction cancelAction;
    private readonly List<PlayerInput> registeredPlayers = new List<PlayerInput>();

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (characterSelectionButton != null) characterSelectionButton.onClick.AddListener(OnCharacterSelectionPressed);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitPressed);

        if (panel != null) panel.SetActive(false);

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
        if (characterSelectionButton != null) characterSelectionButton.onClick.RemoveListener(OnCharacterSelectionPressed);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExitPressed);

        cancelAction?.Disable();

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;

        if (cancelAction != null && cancelAction.WasPressedThisFrame())
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

    public void Toggle()
    {
        if (IsOpen) Resume();
        else Open();
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
        Time.timeScale = 0f;
        SetPlayerActionMap("UI");

        if (EventSystem.current != null && resumeButton != null)
        {
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }
    }

    private void Resume()
    {
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
