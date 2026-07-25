using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartScreenController : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private string playSceneName = "CharacterSelection";

    private void Awake()
    {
        if (playButton != null) playButton.onClick.AddListener(OnPlayPressed);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitPressed);
    }

    private void OnDestroy()
    {
        if (playButton != null) playButton.onClick.RemoveListener(OnPlayPressed);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExitPressed);
    }

    private void OnPlayPressed() => SceneManager.LoadScene(playSceneName);

    private void OnExitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
