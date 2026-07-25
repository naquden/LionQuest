using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Controls one player slot panel's visuals. Driven entirely by CharacterSelectionManager.
/// </summary>
public class PlayerSlotUI : MonoBehaviour
{
    [SerializeField] private GameObject emptyPanel;
    [SerializeField] private GameObject selectingPanel;
    [SerializeField] private GameObject readyOverlay;

    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text soulsText;
    [SerializeField] private TMP_Text strengthText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text lockedFlashText;

    [Header("Ready overlay (separate visuals from the selecting panel above)")]
    [SerializeField] private Image readyPortraitImage;
    [SerializeField] private TMP_Text readyNameText;

    [SerializeField] private Transform carouselContainer;
    [SerializeField] private GameObject carouselItemPrefab;

    private CharacterCarouselItem[] carouselItems = System.Array.Empty<CharacterCarouselItem>();

    public void SetEmpty()
    {
        emptyPanel.SetActive(true);
        selectingPanel.SetActive(false);
        readyOverlay.SetActive(false);
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

        if (readyPortraitImage != null) readyPortraitImage.sprite = character.portrait;
        if (readyNameText != null) readyNameText.text = character.displayName;
    }

    private void BuildCarousel(CharacterDefinition[] roster, System.Func<CharacterDefinition, bool> isUnlocked)
    {
        foreach (Transform child in carouselContainer)
        {
            Destroy(child.gameObject);
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
        if (soulsText != null) soulsText.text = $"Souls: {saveData.souls}";
        if (strengthText != null) strengthText.text = $"Str: {saveData.strengthLevel}";
        if (healthText != null) healthText.text = $"HP: {saveData.healthLevel}";
        if (speedText != null) speedText.text = $"Spd: {saveData.speedLevel}";
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
