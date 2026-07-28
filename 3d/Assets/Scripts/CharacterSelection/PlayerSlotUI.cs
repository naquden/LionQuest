using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Controls one player slot card's visuals. Driven entirely by CharacterSelectionManager.
/// </summary>
public class PlayerSlotUI : MonoBehaviour
{
    [System.Serializable]
    public class StatBar
    {
        public TMP_Text label;
        public Image[] segments;

        public void Set(int level)
        {
            if (label != null) label.text = $"Lv {level}";
            if (segments == null) return;
            int filled = Mathf.Clamp(level, 0, segments.Length);
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null) segments[i].color = i < filled ? UiPalette.Gold : UiPalette.SurfaceInset;
            }
        }
    }

    [Header("Identity")]
    [SerializeField] private TMP_Text playerLabelText;
    [SerializeField] private Image accentBanner;
    [SerializeField] private Image cardBorder;
    [SerializeField] private Image glow;

    [Header("Panels")]
    [SerializeField] private GameObject emptyPanel;
    [SerializeField] private GameObject selectingPanel;
    [SerializeField] private GameObject readyOverlay;

    [Header("Preview")]
    [SerializeField] private CharacterPreviewStage previewStage;

    [Header("Selecting panel content")]
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text soulsValueText;
    [SerializeField] private StatBar strengthBar;
    [SerializeField] private StatBar healthBar;
    [SerializeField] private StatBar speedBar;
    [SerializeField] private TMP_Text lockedFlashText;
    [SerializeField] private TMP_Text hintText;

    [Header("Ready overlay")]
    [SerializeField] private TMP_Text readyNameText;
    [SerializeField] private TMP_Text readySealText;

    [SerializeField] private Transform carouselContainer;
    [SerializeField] private GameObject carouselItemPrefab;

    private CharacterCarouselItem[] carouselItems = System.Array.Empty<CharacterCarouselItem>();
    private Color accentColor = UiPalette.Gold;

    public void SetPlayerIndex(int index)
    {
        accentColor = UiPalette.PlayerAccent(index);
        if (playerLabelText != null) playerLabelText.text = $"PLAYER {index + 1}";
        if (accentBanner != null) accentBanner.color = accentColor;
        if (cardBorder != null) cardBorder.color = accentColor;
        if (glow != null) glow.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f);
    }

    public void SetEmpty()
    {
        emptyPanel.SetActive(true);
        selectingPanel.SetActive(false);
        readyOverlay.SetActive(false);
        if (previewStage != null) previewStage.Hide();
    }

    public void SetSelecting(CharacterDefinition[] roster, int selectedIndex, System.Func<CharacterDefinition, bool> isUnlocked, CharacterSaveData saveData)
    {
        emptyPanel.SetActive(false);
        selectingPanel.SetActive(true);
        readyOverlay.SetActive(false);

        BuildCarousel(roster, isUnlocked);
        UpdateCarousel(selectedIndex);
        UpdateStats(roster[selectedIndex], saveData);
    }

    public void SetReady(CharacterDefinition character)
    {
        selectingPanel.SetActive(false);
        readyOverlay.SetActive(true);

        if (readyNameText != null) readyNameText.text = character.displayName;
        if (readySealText != null) readySealText.text = "READY";
        if (previewStage != null) previewStage.Show(character);
    }

    private void BuildCarousel(CharacterDefinition[] roster, System.Func<CharacterDefinition, bool> isUnlocked)
    {
        // Snapshot first — DestroyImmediate (edit-mode path) reindexes children as it goes,
        // which corrupts a live `foreach (Transform child in carouselContainer)`.
        var existingChildren = new Transform[carouselContainer.childCount];
        for (int i = 0; i < existingChildren.Length; i++) existingChildren[i] = carouselContainer.GetChild(i);
        foreach (Transform child in existingChildren)
        {
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        carouselItems = new CharacterCarouselItem[roster.Length];
        for (int i = 0; i < roster.Length; i++)
        {
            GameObject itemGo = Instantiate(carouselItemPrefab, carouselContainer);
            CharacterCarouselItem item = itemGo.GetComponent<CharacterCarouselItem>();
            item.Setup(roster[i], isUnlocked(roster[i]));
            carouselItems[i] = item;
        }
    }

    public void UpdateCarousel(int selectedIndex)
    {
        for (int i = 0; i < carouselItems.Length; i++)
        {
            int offset = i - selectedIndex;
            carouselItems[i].ApplyDistanceStyle(offset);
        }
    }

    public void UpdateStats(CharacterDefinition character, CharacterSaveData saveData)
    {
        if (characterNameText != null) characterNameText.text = character.displayName;
        if (soulsValueText != null) soulsValueText.text = saveData.souls.ToString();
        strengthBar?.Set(saveData.strengthLevel);
        healthBar?.Set(saveData.healthLevel);
        speedBar?.Set(saveData.speedLevel);

        if (previewStage != null) previewStage.Show(character);
    }

    public void FlashLocked()
    {
        if (lockedFlashText == null) return;
        CancelInvoke(nameof(HideLockedFlash));
        lockedFlashText.gameObject.SetActive(true);
        Invoke(nameof(HideLockedFlash), 1f);
    }

    private void HideLockedFlash()
    {
        if (lockedFlashText != null) lockedFlashText.gameObject.SetActive(false);
    }
}
