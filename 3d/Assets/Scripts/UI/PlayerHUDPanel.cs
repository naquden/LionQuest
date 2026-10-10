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
    [SerializeField] private TMP_Text[] skillCooldownTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private TMP_Text[] skillLockedMarks = System.Array.Empty<TMP_Text>();
    [SerializeField] private Image[] skillSlotOutlines = System.Array.Empty<Image>();
    [SerializeField] private Image[] skillCooldownDiscs = System.Array.Empty<Image>();

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
            boundPlayer.OnDowned += HandleDeath; // same greyed-out visual; downed is not yet distinguishable from dead on the HUD.
            boundPlayer.OnRevived += HandleRevived;
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
            boundPlayer.OnDowned -= HandleDeath;
            boundPlayer.OnRevived -= HandleRevived;
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

        // The slot box stays visible whether or not the skill is unlocked — a locked slot is
        // dimmed and marked, so the player can see the skill exists and which button owns it.
        // The background Image lives on the slot root by construction (PlayerHudGenerator.BuildSkillSlots).
        if (slot < skillSlotBackgrounds.Length && skillSlotBackgrounds[slot] != null)
        {
            skillSlotBackgrounds[slot].gameObject.SetActive(true);
            skillSlotBackgrounds[slot].color = unlocked ? UiPalette.SurfaceInset : UiPalette.Locked;
        }

        if (slot < skillSlotOutlines.Length && skillSlotOutlines[slot] != null)
        {
            skillSlotOutlines[slot].color = unlocked ? UiPalette.Gold : UiPalette.GoldDim;
        }

        if (slot < skillIconImages.Length && skillIconImages[slot] != null)
        {
            skillIconImages[slot].sprite = hasIcon ? icon : null;
            skillIconImages[slot].enabled = hasIcon && icon != null;
            skillIconImages[slot].color = unlocked ? Color.white : UiPalette.TextMuted;
        }

        // Button prompt: real binding for the device this player uses ("1"/"2"/"3" or "Y"/"B"/"RB").
        if (slot < skillSlotLabels.Length && skillSlotLabels[slot] != null)
        {
            skillSlotLabels[slot].gameObject.SetActive(true);
            skillSlotLabels[slot].text = boundPlayer != null
                ? boundPlayer.GetSkillBindingLabel(slot + 1)
                : (slot + 1).ToString();
            skillSlotLabels[slot].color = unlocked ? UiPalette.Gold : UiPalette.TextMuted;
        }

        if (slot < skillLockedMarks.Length && skillLockedMarks[slot] != null)
        {
            skillLockedMarks[slot].gameObject.SetActive(!unlocked);
        }

        if (slot < skillCooldownOverlays.Length && skillCooldownOverlays[slot] != null)
        {
            skillCooldownOverlays[slot].fillAmount = 0f;
        }

        if (slot < skillCooldownTexts.Length && skillCooldownTexts[slot] != null)
        {
            skillCooldownTexts[slot].text = string.Empty;
        }

        // The backing disc only exists to make the seconds readable — hide it while there is no
        // number to back, otherwise every ready slot carries a black dot over its icon.
        if (slot < skillCooldownDiscs.Length && skillCooldownDiscs[slot] != null)
        {
            skillCooldownDiscs[slot].enabled = false;
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
        float remaining = boundPlayer.SkillCooldownRemaining;

        for (int i = 0; i < SkillSlotCount; i++)
        {
            // The player's control scheme can resolve after Bind (PlayerInput only reports its
            // paired device once it sends input), so keep the prompt in sync with the binding.
            if (i < skillSlotLabels.Length && skillSlotLabels[i] != null)
            {
                string prompt = boundPlayer.GetSkillBindingLabel(i + 1);
                if (skillSlotLabels[i].text != prompt) skillSlotLabels[i].text = prompt;
            }

            if (!slotUnlocked[i]) continue;

            if (i < skillCooldownOverlays.Length && skillCooldownOverlays[i] != null)
            {
                skillCooldownOverlays[i].fillAmount = normalized;
            }

            bool counting = remaining > 0.05f;

            if (i < skillCooldownTexts.Length && skillCooldownTexts[i] != null)
            {
                // Whole seconds while locked, blank once ready — a number is readable at
                // 56px where the radial sweep alone is easy to miss.
                skillCooldownTexts[i].text = counting
                    ? Mathf.CeilToInt(remaining).ToString()
                    : string.Empty;
            }

            if (i < skillCooldownDiscs.Length && skillCooldownDiscs[i] != null)
            {
                skillCooldownDiscs[i].enabled = counting;
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
        // Show 0 out of the real max, not "0 / 0" — the latter reads as a broken/unset max health
        // bar rather than as a dead player, which is exactly how it was reported as a bug.
        if (healthText != null)
        {
            int max = boundPlayer != null ? Mathf.CeilToInt(boundPlayer.MaxHealth) : 0;
            healthText.text = $"0 / {max}";
        }
        if (portraitImage != null) portraitImage.color = new Color(0.4f, 0.4f, 0.4f, 1f);
    }

    /// <summary>
    /// TopDownPlayerController.Revive already raises OnHealthChanged with a positive value, which
    /// would re-whiten the portrait on its own via HandleHealthChanged. This handler exists anyway so
    /// "the player came back" is an explicit reaction to OnRevived rather than an incidental side
    /// effect of the new health happening to be positive — the two events mean different things even
    /// though today they'd produce the same portrait colour.
    /// </summary>
    private void HandleRevived()
    {
        if (portraitImage != null) portraitImage.color = Color.white;
    }

    private void HandleSoulsChanged(int souls)
    {
        if (soulsText != null) soulsText.text = souls.ToString();
    }
}
