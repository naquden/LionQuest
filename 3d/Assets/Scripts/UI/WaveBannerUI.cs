using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays an on-screen banner showing the current wave number and remaining enemy count.
/// Driven by WaveSpawner events (OnWaveStarted, OnWaveCleared). Fades in on wave start and
/// fades out briefly when cleared before hiding.
/// </summary>
public class WaveBannerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeOutDelay = 1.5f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private WaveSpawner spawner;
    private int currentWaveNumber;
    private int remainingEnemies;

    private void Awake()
    {
        // Hidden from frame 1 — otherwise the placeholder text authored at edit time ("Wave 1 — 10
        // enemies") is what players see for the whole opening wave, since OnWaveStarted only fires
        // once WaveSpawner takes over from it.
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void Start()
    {
        if (spawner != null)
        {
            spawner.OnWaveStarted += HandleWaveStarted;
            spawner.OnWaveCleared += HandleWaveCleared;
        }
        else
        {
            Debug.LogError("WaveBannerUI: spawner is not assigned — wave events will never be shown.");
        }
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.OnWaveStarted -= HandleWaveStarted;
            spawner.OnWaveCleared -= HandleWaveCleared;
        }
    }

    private void HandleWaveStarted(int waveNumber, int enemyCount)
    {
        currentWaveNumber = waveNumber;
        remainingEnemies = enemyCount;
        UpdateText();
        ShowBanner();
    }

    private void HandleWaveCleared(int waveNumber)
    {
        currentWaveNumber = waveNumber;
        remainingEnemies = 0;
        UpdateText();
        StartCoroutine(FadeOutAfterDelay());
    }

    private void UpdateText()
    {
        if (waveText != null)
        {
            if (remainingEnemies > 0)
            {
                waveText.text = $"Wave {currentWaveNumber} — {remainingEnemies} enemies";
            }
            else
            {
                waveText.text = $"Wave {currentWaveNumber} cleared!";
            }
        }
    }

    private void ShowBanner()
    {
        // Fades via alpha only — never SetActive(false) on this object. Disabling the very
        // GameObject a coroutine is running on stops StartCoroutine from working the next time a
        // wave starts (Unity refuses to start a coroutine on an inactive GameObject), which would
        // have made the banner show once and then silently never again.
        StopAllCoroutines();
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutAfterDelay()
    {
        yield return new WaitForSeconds(fadeOutDelay);

        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
}
