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
    [SerializeField] private TMP_Text[] skillSlotLabels = System.Array.Empty<TMP_Text>();
    [SerializeField] private Image[] skillSlotBackgrounds = System.Array.Empty<Image>();

    private static readonly Color BackgroundReadyColor = new Color(0.15f, 0.15f, 0.18f, 0.9f);
    private static readonly Color BackgroundLockedColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
    private static readonly Color IconReadyColor = Color.white;
    private static readonly Color IconLockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    private static readonly Color BadgeReadyColor = new Color(1f, 1f, 1f, 0.85f);
    private static readonly Color BadgeLockedColor = new Color(1f, 1f, 1f, 0.35f);

    private TopDownPlayerController boundPlayer;
    private CharacterStats boundStats;
    private readonly bool[] slotUnlocked = new bool[SkillSlotCount];

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
            boundStats.OnStatsUpgraded += HandleStatsUpgraded;
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
            boundStats.OnStatsUpgraded -= HandleStatsUpgraded;
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
        bool unlocked = boundPlayer == null || boundPlayer.IsSkillSlotUnlocked(slot + 1);
        slotUnlocked[slot] = unlocked;

        if (slot < skillIconImages.Length && skillIconImages[slot] != null)
        {
            skillIconImages[slot].sprite = hasIcon ? icon : null;
            skillIconImages[slot].enabled = hasIcon && icon != null;
            skillIconImages[slot].color = unlocked ? IconReadyColor : IconLockedColor;
        }

        if (slot < skillSlotBackgrounds.Length && skillSlotBackgrounds[slot] != null)
        {
            skillSlotBackgrounds[slot].color = unlocked ? BackgroundReadyColor : BackgroundLockedColor;
        }

        if (slot < skillSlotLabels.Length && skillSlotLabels[slot] != null)
        {
            skillSlotLabels[slot].color = unlocked ? BadgeReadyColor : BadgeLockedColor;
        }

        if (slot < skillCooldownOverlays.Length && skillCooldownOverlays[slot] != null)
        {
            // Locked slots stay fully covered (static — Update skips them via slotUnlocked).
            skillCooldownOverlays[slot].fillAmount = unlocked ? 0f : 1f;
        }
    }

    private void HandleStatsUpgraded()
    {
        for (int i = 0; i < SkillSlotCount; i++)
        {
            BindSkillSlot(i);
        }
    }

    private void Update()
    {
        if (boundPlayer == null || !boundPlayer.IsAlive) return;

        float normalized = boundPlayer.SkillCooldownNormalized;

        for (int i = 0; i < SkillSlotCount; i++)
        {
            if (!slotUnlocked[i]) continue;

            if (i < skillCooldownOverlays.Length && skillCooldownOverlays[i] != null)
            {
                skillCooldownOverlays[i].fillAmount = normalized;
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
