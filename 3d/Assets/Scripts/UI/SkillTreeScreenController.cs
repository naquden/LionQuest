using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full-screen upgrade shop opened from CharacterSelection. No CharacterStats instance exists yet at
/// this point (the player hasn't spawned), so this reads/writes CharacterSaveData directly through
/// SaveManager + SkillTreeService — the same rules CharacterStats uses in-game.
///
/// Layout is deliberately a plain shop, not a graph: category tabs on the left (Body, then one per
/// skill), a vertical list of upgrade rows for the selected category on the right. Everything a row
/// costs and does is printed on the row itself, so there is no side panel, no legend, and nothing to
/// pan or zoom.
///
/// Navigation is a two-pane focus model, not axis-per-list. Exactly one pane holds the cursor at a
/// time: Up/Down always moves within the focused pane, Left/Right moves the cursor BETWEEN panes
/// (left from the rows focuses the category list; right from the categories focuses the rows). That
/// way a direction press always moves the visible highlight the way the stick points, instead of
/// Left/Right silently changing a list the cursor isn't in.
/// </summary>
public class SkillTreeScreenController : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text soulsText;
    [SerializeField] private RectTransform categoryContainer;
    [SerializeField] private GameObject categoryPrefab;
    [SerializeField] private RectTransform rowContainer;
    [SerializeField] private GameObject rowPrefab;
    [SerializeField] private ScrollRect rowScrollRect;
    [SerializeField] private TMP_Text categoryTitleText;
    [SerializeField] private TMP_Text categorySummaryText;
    [SerializeField] private TMP_Text statSummaryText;
    [SerializeField] private TMP_Text helpText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;

    /// <summary>Which list the navigation cursor currently lives in.</summary>
    private enum FocusPane { Categories, Rows }

    private CharacterDefinition definition;
    private CharacterSaveData save;
    private int categoryIndex;
    private int rowIndex;
    private FocusPane focus = FocusPane.Categories;

    private readonly List<SkillTreeLinkUI> spawnedCategories = new List<SkillTreeLinkUI>();
    private readonly List<SkillTreeNodeUI> spawnedRows = new List<SkillTreeNodeUI>();
    private System.Action onClose;

    private SkillTreeDefinition[] Categories =>
        definition != null && definition.skillTrees != null ? definition.skillTrees : System.Array.Empty<SkillTreeDefinition>();

    private SkillTreeDefinition CurrentCategory =>
        Categories.Length > 0 ? Categories[Mathf.Clamp(categoryIndex, 0, Categories.Length - 1)] : null;

    private SkillNodeDefinition[] CurrentRows
    {
        get
        {
            SkillTreeDefinition category = CurrentCategory;
            if (category == null || category.nodes == null) return System.Array.Empty<SkillNodeDefinition>();
            return category.nodes;
        }
    }

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (buyButton != null) buyButton.onClick.AddListener(BuySelected);
    }

    public void Open(CharacterDefinition character, System.Action onCloseCallback)
    {
        definition = character;
        // Routed through GameSaveController when it exists (DontDestroyOnLoad, so it survives the
        // Level1 <-> CharacterSelection round trip) rather than SaveManager.LoadCharacter directly.
        // GameSaveController caches one CharacterSaveData per characterID for the whole session
        // (CharacterStats reads/writes the same cached object) — loading straight from SaveManager
        // here would hand this screen a SEPARATE object, so a purchase made here would write to disk
        // correctly but leave the cached object CharacterStats reads on the next Level1 load stale,
        // silently reverting to whatever it held before this purchase.
        save = GameSaveController.Instance != null
            ? GameSaveController.Instance.GetSharedRecord(character.characterID)
            : SaveManager.LoadCharacter(character.characterID);
        onClose = onCloseCallback;
        categoryIndex = 0;
        rowIndex = 0;
        focus = FocusPane.Categories;

        if (panelRoot != null) panelRoot.SetActive(true);
        if (characterNameText != null) characterNameText.text = character.displayName;

        BuildCategories();
        BuildRows();
        Refresh();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        definition = null;
        save = null;
        onClose?.Invoke();
        onClose = null;
    }

    /// <summary>Up/Down moves inside the focused pane; Left/Right moves the cursor between the two
    /// panes. The highlight always follows the direction pressed.</summary>
    public void Navigate(Vector2 direction)
    {
        if (definition == null) return;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            if (Mathf.Abs(direction.x) < 0.5f) return;
            SetFocus(direction.x > 0f ? FocusPane.Rows : FocusPane.Categories);
        }
        else
        {
            if (Mathf.Abs(direction.y) < 0.5f) return;
            // Screen-space: up on the stick moves to the entry above (earlier in the list).
            int step = direction.y > 0f ? -1 : 1;
            if (focus == FocusPane.Categories) SelectCategory(categoryIndex + step);
            else SelectRow(rowIndex + step);
        }
    }

    /// <summary>Moves the cursor to a pane. Focusing the rows of a category with no rows is a no-op,
    /// so the cursor can never land somewhere with nothing to highlight.</summary>
    private void SetFocus(FocusPane pane)
    {
        if (pane == focus) return;
        if (pane == FocusPane.Rows && CurrentRows.Length == 0) return;

        focus = pane;
        Refresh();
        if (focus == FocusPane.Rows) ScrollSelectedIntoView();
    }

    /// <summary>Submit in the category pane steps INTO that category's rows — the natural "drill in"
    /// press for a player who just picked a group. In the row pane it buys.</summary>
    public void SubmitSelected()
    {
        if (definition == null) return;

        if (focus == FocusPane.Categories)
        {
            SetFocus(FocusPane.Rows);
            return;
        }
        BuySelected();
    }

    /// <summary>Cancel walks back out the way Submit walked in: rows -> categories -> close screen.
    /// Never closes straight out of the row list, so Cancel is the exact inverse of Submit.</summary>
    public void CancelPressed()
    {
        if (focus == FocusPane.Rows)
        {
            SetFocus(FocusPane.Categories);
            return;
        }
        Close();
    }

    private void SelectCategory(int index)
    {
        if (Categories.Length == 0) return;
        int clamped = Mathf.Clamp(index, 0, Categories.Length - 1);
        if (clamped == categoryIndex) return;

        categoryIndex = clamped;
        rowIndex = 0;
        BuildRows();
        Refresh();
    }

    private void SelectRow(int index)
    {
        int count = CurrentRows.Length;
        if (count == 0) return;

        int clamped = Mathf.Clamp(index, 0, count - 1);
        if (clamped == rowIndex) return;

        rowIndex = clamped;
        Refresh();
        ScrollSelectedIntoView();
    }

    private void BuySelected()
    {
        SkillNodeDefinition row = SelectedRow();
        if (definition == null || save == null || row == null) return;

        if (!SkillTreeService.TryPurchase(definition, save, CurrentCategory.skillId, row.nodeId)) return;

        // FlushCharacter when GameSaveController owns `save` (the normal case — see Open), so this
        // writes through the same single-writer path CharacterStats.TryPurchaseNode uses. Falls back
        // to a direct save only in the no-GameSaveController case Open also falls back for.
        if (GameSaveController.Instance != null) GameSaveController.Instance.FlushCharacter(save.characterID);
        else SaveManager.SaveCharacter(save);
        Refresh();
    }

    private SkillNodeDefinition SelectedRow()
    {
        SkillNodeDefinition[] rows = CurrentRows;
        if (rows.Length == 0) return null;
        return rows[Mathf.Clamp(rowIndex, 0, rows.Length - 1)];
    }

    // ------------------------------------------------------------------
    // Spawning
    // ------------------------------------------------------------------

    private void BuildCategories()
    {
        foreach (SkillTreeLinkUI tab in spawnedCategories) if (tab != null) Destroy(tab.gameObject);
        spawnedCategories.Clear();

        if (categoryContainer == null || categoryPrefab == null) return;

        for (int i = 0; i < Categories.Length; i++)
        {
            SkillTreeDefinition category = Categories[i];
            GameObject go = Instantiate(categoryPrefab, categoryContainer);
            var tab = go.GetComponent<SkillTreeLinkUI>();
            if (tab == null) continue;

            int captured = i;
            tab.Setup(category.displayName, category.branchColor, () =>
            {
                // Clicking a tab must also move the cursor there, or the focus ring would sit in the
                // row list while the stick appears to control the tabs.
                focus = FocusPane.Categories;
                SelectCategory(captured);
                Refresh();
            });
            spawnedCategories.Add(tab);
        }
    }

    private void BuildRows()
    {
        foreach (SkillTreeNodeUI row in spawnedRows) if (row != null) Destroy(row.gameObject);
        spawnedRows.Clear();

        if (rowContainer == null || rowPrefab == null) return;

        SkillTreeDefinition category = CurrentCategory;
        if (category == null) return;

        SkillNodeDefinition[] rows = CurrentRows;
        for (int i = 0; i < rows.Length; i++)
        {
            SkillNodeDefinition node = rows[i];
            GameObject go = Instantiate(rowPrefab, rowContainer);
            var ui = go.GetComponent<SkillTreeNodeUI>();
            if (ui == null) continue;

            int captured = i;
            ui.Setup(node, category.branchColor, () =>
            {
                // Second click on the already-focused row buys it; the first only moves the cursor,
                // so a mouse user can't buy something by clicking once in the wrong place.
                bool wasFocusedHere = focus == FocusPane.Rows && captured == rowIndex;
                focus = FocusPane.Rows;
                rowIndex = captured;

                if (wasFocusedHere)
                {
                    BuySelected();
                    return;
                }
                Refresh();
            });
            spawnedRows.Add(ui);
        }
    }

    /// <summary>Keeps the highlighted row inside the viewport. The list is short by design, but a
    /// category with many rows must never hide the cursor.</summary>
    private void ScrollSelectedIntoView()
    {
        if (rowScrollRect == null || rowContainer == null) return;
        if (rowIndex < 0 || rowIndex >= spawnedRows.Count) return;

        Canvas.ForceUpdateCanvases();

        RectTransform viewport = rowScrollRect.viewport != null ? rowScrollRect.viewport : (RectTransform)rowScrollRect.transform;
        float contentHeight = rowContainer.rect.height;
        float viewportHeight = viewport.rect.height;
        if (contentHeight <= viewportHeight)
        {
            rowScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        var rowRect = (RectTransform)spawnedRows[rowIndex].transform;
        // Row anchoredPosition.y is negative going down from the content's top edge.
        float rowCenterFromTop = -rowRect.anchoredPosition.y;
        float target = (rowCenterFromTop - viewportHeight * 0.5f) / (contentHeight - viewportHeight);
        rowScrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(target);
    }

    // ------------------------------------------------------------------
    // Refresh
    // ------------------------------------------------------------------

    private void Refresh()
    {
        if (definition == null || save == null) return;

        if (soulsText != null) soulsText.text = $"{save.souls} souls to spend";

        SkillTreeDefinition category = CurrentCategory;
        SkillNodeDefinition[] rows = CurrentRows;

        for (int i = 0; i < spawnedCategories.Count && i < Categories.Length; i++)
        {
            SkillTreeDefinition tab = Categories[i];
            SkillNodeDefinition gate = SkillTreeService.FindUnlockNode(tab);
            bool locked = gate != null && SkillTreeService.GetRank(save, tab.skillId, gate.nodeId) < 1;

            int owned = 0, total = 0;
            if (tab.nodes != null)
            {
                foreach (SkillNodeDefinition node in tab.nodes)
                {
                    owned += SkillTreeService.GetRank(save, tab.skillId, node.nodeId);
                    total += node.maxRank;
                }
            }

            spawnedCategories[i].SetState(
                i == categoryIndex, focus == FocusPane.Categories, locked, owned, total, tab.branchColor);
        }

        if (category != null)
        {
            if (categoryTitleText != null) categoryTitleText.text = category.displayName;
            if (categorySummaryText != null) categorySummaryText.text = category.summary;
        }

        for (int i = 0; i < spawnedRows.Count && i < rows.Length; i++)
        {
            SkillNodeDefinition node = rows[i];
            int rank = SkillTreeService.GetRank(save, category.skillId, node.nodeId);
            bool available = SkillTreeService.IsRowAvailable(category, save, node);
            int price = SkillTreeService.GetNextCost(save, category.skillId, node);
            bool maxed = rank >= node.maxRank;

            SkillNodeVisualState state;
            if (maxed) state = SkillNodeVisualState.Maxed;
            else if (!available) state = SkillNodeVisualState.Locked;
            else if (save.souls >= price) state = SkillNodeVisualState.Affordable;
            else state = SkillNodeVisualState.Unaffordable;

            spawnedRows[i].SetState(
                state,
                i == rowIndex,
                focus == FocusPane.Rows,
                rank,
                node.maxRank,
                price,
                Mathf.Max(0, price - save.souls),
                BuildChangeLine(category, node, rank),
                category.branchColor);
        }

        RefreshStatSummary();
        RefreshBuyButton();
        RefreshHelpText();
    }

    /// <summary>Prompts describe only what the currently focused pane can do, so the hint never lists
    /// a press that does nothing right now.</summary>
    private void RefreshHelpText()
    {
        if (helpText == null) return;

        helpText.text = focus == FocusPane.Categories
            ? "Up / Down: choose a group     Right or A: see its upgrades     B: back"
            : "Up / Down: choose an upgrade     A: buy     Left or B: back to groups";
    }

    /// <summary>Always-visible readout of what the player actually has right now, so the effect of a
    /// purchase is visible without reading any node text.</summary>
    private void RefreshStatSummary()
    {
        if (statSummaryText == null) return;

        int strength = SkillTreeService.GetAttributeLevel(definition, save, SkillEffect.Strength);
        int health = SkillTreeService.GetAttributeLevel(definition, save, SkillEffect.Health);
        int speed = SkillTreeService.GetAttributeLevel(definition, save, SkillEffect.Speed);

        float baseMaxHealth = definition != null ? definition.baseMaxHealth : 100f;
        float damagePercent = (strength - 1) * 20f;
        float maxHealth = baseMaxHealth + (health - 1) * 20f;
        float speedPercent = (speed - 1) * 10f;

        statSummaryText.text =
            $"Damage +{damagePercent:0}%     Health {maxHealth:0}     Move speed +{speedPercent:0}%";
    }

    private void RefreshBuyButton()
    {
        SkillNodeDefinition row = SelectedRow();
        SkillTreeDefinition category = CurrentCategory;
        if (row == null || category == null)
        {
            SetBuyState(false, "Buy");
            return;
        }

        // While the cursor is in the tab list, Submit drills into the category rather than buying —
        // the button has to say so, or it advertises a purchase that press won't make.
        if (focus == FocusPane.Categories)
        {
            SetBuyState(false, $"Pick an upgrade in {category.displayName}");
            return;
        }

        int rank = SkillTreeService.GetRank(save, category.skillId, row.nodeId);
        bool available = SkillTreeService.IsRowAvailable(category, save, row);
        int price = SkillTreeService.GetNextCost(save, category.skillId, row);

        if (rank >= row.maxRank) SetBuyState(false, "Already bought");
        else if (!available) SetBuyState(false, "Unlock this skill first");
        else if (save.souls < price) SetBuyState(false, $"Need {price - save.souls} more souls");
        else SetBuyState(true, $"Buy for {price} souls");
    }

    /// <summary>Greys the button out whenever it can't be pressed. Without this it stays bright
    /// green while reading "Need 250 more souls", which looks clickable.</summary>
    private void ApplyBuyButtonTint(bool interactable)
    {
        if (buyButton == null) return;

        var image = buyButton.targetGraphic as Graphic;
        if (image != null) image.color = interactable ? UiPalette.Purchased : UiPalette.SurfaceInset;
        if (buyButtonText != null) buyButtonText.color = interactable ? UiPalette.TextLight : UiPalette.TextMuted;
    }

    /// <summary>"10s -> 8s" for the selected row's effect, using the category's authored base value.
    /// Returns empty when the effect has no displayable base (e.g. the Unlock row).</summary>
    private string BuildChangeLine(SkillTreeDefinition category, SkillNodeDefinition node, int currentRank)
    {
        if (category.displayBases == null || node.effect == SkillEffect.Unlock) return string.Empty;

        SkillDisplayBase basis = category.displayBases.FirstOrDefault(b => b.effect == node.effect);
        if (basis == null) return string.Empty;

        var resolvedNow = SkillTreeService.ResolveSkillValues(definition, save);
        float before = SkillTreeService.GetValue(resolvedNow, category.skillId, node.effect, basis.baseValue);

        if (currentRank >= node.maxRank)
        {
            return $"Now {Format(before)}{basis.unit}";
        }

        // Simulate one more rank without touching the live save.
        var preview = new CharacterSaveData(save.characterID)
        {
            souls = int.MaxValue,
            purchasedSkillNodes = new List<string>(save.purchasedSkillNodes),
        };
        SkillTreeService.TryPurchase(definition, preview, category.skillId, node.nodeId);
        float after = SkillTreeService.GetValue(
            SkillTreeService.ResolveSkillValues(definition, preview), category.skillId, node.effect, basis.baseValue);

        if (Mathf.Approximately(before, after)) return string.Empty;
        return $"{Format(before)}{basis.unit} → {Format(after)}{basis.unit}";
    }

    private static string Format(float value) => value.ToString("0.##");

    private void SetBuyState(bool interactable, string label)
    {
        if (buyButton != null) buyButton.interactable = interactable;
        if (buyButtonText != null) buyButtonText.text = label;
        ApplyBuyButtonTint(interactable);
    }
}
