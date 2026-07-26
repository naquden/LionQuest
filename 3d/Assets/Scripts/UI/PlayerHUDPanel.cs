using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One local-co-op player's HUD panel: portrait/name, health bar, souls, and 3 skill slots
/// with a shared cooldown overlay (mirrors TopDownPlayerController's single skill lock).
/// Driven entirely by PlayerHUD.BindPlayer/UnbindPlayer.
/// </summary>
public class PlayerHUDPanel : MonoBehaviour
{
    private const int SkillSlotCount = 3;

    [SerializeField] private GameObject content;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text playerLabelText;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text soulsText;

    [Header("Skill slots 1-3")]
    [SerializeField] private Image[] skillIconImages = System.Array.Empty<Image>();
    [SerializeField] private Image[] skillCooldownOverlays = System.Array.Empty<Image>();
    [SerializeField] private TMP_Text[] skillCooldownTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private TMP_Text[] skillSlotLabels = System.Array.Empty<TMP_Text>();

    private TopDownPlayerController boundPlayer;
    private CharacterStats boundStats;

    public void Bind(int playerIndex, TopDownPlayerController player, CharacterStats stats, CharacterDefinition definition)
    {
        Unbind();

        boundPlayer = player;
        boundStats = stats;

        SetVisible(true);

        if (playerLabelText != null) playerLabelText.text = $"P{playerIndex + 1}";

        if (definition != null)
        {
            if (characterNameText != null) characterNameText.text = definition.displayName;
            if (portraitImage != null)
            {
                portraitImage.sprite = definition.portrait;
                portraitImage.enabled = definition.portrait != null;
            }
        }

        if (boundPlayer != null)
        {
            boundPlayer.OnHealthChanged += HandleHealthChanged;
            boundPlayer.OnDeath += HandleDeath;
            HandleHealthChanged(boundPlayer.CurrentHealth, boundPlayer.MaxHealth);
        }

        if (boundStats != null)
        {
            boundStats.OnSoulsChanged += HandleSoulsChanged;
            HandleSoulsChanged(boundStats.souls);
        }

        for (int i = 0; i < SkillSlotCount; i++)
        {
            BindSkillSlot(i);
        }
    }

    public void Unbind()
    {
        if (boundPlayer != null)
        {
            boundPlayer.OnHealthChanged -= HandleHealthChanged;
            boundPlayer.OnDeath -= HandleDeath;
            boundPlayer = null;
        }

        if (boundStats != null)
        {
            boundStats.OnSoulsChanged -= HandleSoulsChanged;
            boundStats = null;
        }

        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (content != null) content.SetActive(visible);
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void BindSkillSlot(int slot)
    {
        Sprite icon = null;
        bool hasIcon = boundPlayer != null && boundPlayer.TryGetSkillDisplay(slot + 1, out _, out icon);

        if (slot < skillIconImages.Length && skillIconImages[slot] != null)
        {
            skillIconImages[slot].sprite = hasIcon ? icon : null;
            skillIconImages[slot].enabled = hasIcon && icon != null;
        }

        if (slot < skillSlotLabels.Length && skillSlotLabels[slot] != null)
        {
            bool showNumberPlaceholder = !hasIcon || icon == null;
            skillSlotLabels[slot].gameObject.SetActive(showNumberPlaceholder);
        }

        if (slot < skillCooldownOverlays.Length && skillCooldownOverlays[slot] != null)
        {
            skillCooldownOverlays[slot].fillAmount = 0f;
        }

        if (slot < skillCooldownTexts.Length && skillCooldownTexts[slot] != null)
        {
            skillCooldownTexts[slot].gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (boundPlayer == null) return;

        float normalized = boundPlayer.SkillCooldownNormalized;
        float remaining = boundPlayer.SkillCooldownRemaining;

        for (int i = 0; i < SkillSlotCount; i++)
        {
            if (i < skillCooldownOverlays.Length && skillCooldownOverlays[i] != null)
            {
                skillCooldownOverlays[i].fillAmount = normalized;
            }

            if (i < skillCooldownTexts.Length && skillCooldownTexts[i] != null)
            {
                bool onCooldown = normalized > 0f;
                skillCooldownTexts[i].gameObject.SetActive(onCooldown);
                if (onCooldown) skillCooldownTexts[i].text = remaining.ToString("F1");
            }
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        if (portraitImage != null && current > 0f)
        {
            portraitImage.color = Color.white;
        }
    }

    private void HandleDeath()
    {
        if (healthFillImage != null) healthFillImage.fillAmount = 0f;
        if (healthText != null) healthText.text = "0 / 0";
        if (portraitImage != null) portraitImage.color = new Color(0.4f, 0.4f, 0.4f, 1f);
    }

    private void HandleSoulsChanged(int souls)
    {
        if (soulsText != null) soulsText.text = souls.ToString();
    }
}
