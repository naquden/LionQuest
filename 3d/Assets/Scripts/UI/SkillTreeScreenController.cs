using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full-screen skill tree panel opened from CharacterSelection. No CharacterStats instance exists
/// yet at this point (the player hasn't spawned), so this reads/writes CharacterSaveData directly
/// through SaveManager + SkillTreeService — the same rules CharacterStats uses in-game.
/// One skill tree shown at a time; explicit tab buttons switch between a character's skillTrees[].
/// Clicking a node only selects it (shows its description + cost); a separate Buy button purchases.
/// </summary>
public class SkillTreeScreenController : MonoBehaviour
{
    private const float CellWidth = 220f;
    private const float CellHeight = 160f;

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text soulsText;
    [SerializeField] private RectTransform tabContainer;
    [SerializeField] private GameObject tabButtonPrefab;
    [SerializeField] private RectTransform nodeContainer;
    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private RectTransform edgeContainer;
    [SerializeField] private GameObject edgePrefab;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;

    private CharacterDefinition definition;
    private CharacterSaveData save;
    private int currentTreeIndex;
    private int selectedIndex;
    private readonly List<SkillTreeNodeUI> spawnedNodes = new List<SkillTreeNodeUI>();
    private readonly List<GameObject> spawnedEdges = new List<GameObject>();
    private readonly List<Button> spawnedTabButtons = new List<Button>();
    private readonly List<SkillNodeDefinition> orderedNodes = new List<SkillNodeDefinition>();
    private System.Action onClose;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (buyButton != null) buyButton.onClick.AddListener(BuySelected);
    }

    public void Open(CharacterDefinition character, System.Action onCloseCallback)
    {
        definition = character;
        save = SaveManager.LoadCharacter(character.characterID);
        onClose = onCloseCallback;
        currentTreeIndex = 0;
        selectedIndex = 0;

        if (panelRoot != null) panelRoot.SetActive(true);
        if (characterNameText != null) characterNameText.text = character.displayName;

        RebuildTabs();
        RebuildTree();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        definition = null;
        save = null;
        onClose?.Invoke();
        onClose = null;
    }

    public void SwitchTab(int direction)
    {
        if (definition == null || definition.skillTrees == null || definition.skillTrees.Length == 0) return;

        int count = definition.skillTrees.Length;
        SelectTab(((currentTreeIndex + direction) % count + count) % count);
    }

    public void SelectTab(int index)
    {
        if (definition == null || definition.skillTrees == null || definition.skillTrees.Length == 0) return;
        if (index < 0 || index >= definition.skillTrees.Length) return;

        currentTreeIndex = index;
        selectedIndex = 0;
        RebuildTree();
        RefreshTabButtons();
    }

    /// <summary>Moves the node cursor. Does NOT buy — Buy is a separate, explicit action.</summary>
    public void Navigate(int direction)
    {
        if (orderedNodes.Count == 0) return;
        selectedIndex = ((selectedIndex + direction) % orderedNodes.Count + orderedNodes.Count) % orderedNodes.Count;
        RefreshNodeStates();
    }

    public void SubmitSelected() => BuySelected();

    private void BuySelected()
    {
        if (definition == null || save == null) return;
        if (selectedIndex < 0 || selectedIndex >= orderedNodes.Count) return;

        SkillTreeDefinition tree = definition.skillTrees[currentTreeIndex];
        SkillNodeDefinition node = orderedNodes[selectedIndex];

        if (!SkillTreeService.TryPurchase(definition, save, tree.skillId, node.nodeId)) return;

        SaveManager.SaveCharacter(save);
        RefreshNodeStates();
        RefreshTabButtons();
        if (soulsText != null) soulsText.text = $"Souls: {save.souls}";
    }

    private void RebuildTabs()
    {
        foreach (var button in spawnedTabButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        spawnedTabButtons.Clear();

        if (definition == null || definition.skillTrees == null) return;

        for (int i = 0; i < definition.skillTrees.Length; i++)
        {
            SkillTreeDefinition tree = definition.skillTrees[i];
            GameObject go = Instantiate(tabButtonPrefab, tabContainer);
            Button button = go.GetComponent<Button>();
            TMP_Text label = go.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = tree.displayName;

            int index = i; // capture for closure
            button.onClick.AddListener(() => SelectTab(index));
            spawnedTabButtons.Add(button);
        }

        RefreshTabButtons();
    }

    private void RefreshTabButtons()
    {
        for (int i = 0; i < spawnedTabButtons.Count; i++)
        {
            var colors = spawnedTabButtons[i].colors;
            bool isCurrent = i == currentTreeIndex;
            bool unlocked = definition != null && SkillTreeService.IsSkillUnlocked(definition, save, definition.skillTrees[i].skillId);

            Color baseColor = isCurrent
                ? UiPalette.SelectedOutline
                : unlocked
                    ? UiPalette.Surface
                    : UiPalette.Locked;

            colors.normalColor = baseColor;
            colors.selectedColor = baseColor;
            spawnedTabButtons[i].colors = colors;
        }
    }

    private void RebuildTree()
    {
        ClearSpawnedNodes();
        orderedNodes.Clear();

        if (definition == null || definition.skillTrees == null || definition.skillTrees.Length == 0) return;

        SkillTreeDefinition tree = definition.skillTrees[currentTreeIndex];
        if (soulsText != null) soulsText.text = $"Souls: {save.souls}";

        if (tree.nodes == null) return;

        // Traversal order for controller navigation: top-to-bottom, left-to-right by authored grid position.
        var sorted = new List<SkillNodeDefinition>(tree.nodes);
        sorted.Sort((a, b) => a.gridPos.y != b.gridPos.y ? a.gridPos.y.CompareTo(b.gridPos.y) : a.gridPos.x.CompareTo(b.gridPos.x));
        orderedNodes.AddRange(sorted);

        var positions = new Dictionary<string, RectTransform>();

        foreach (SkillNodeDefinition node in tree.nodes)
        {
            GameObject go = Instantiate(nodePrefab, nodeContainer);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = new Vector2(node.gridPos.x * CellWidth, -node.gridPos.y * CellHeight);
            positions[node.nodeId] = rect;

            var nodeUi = go.GetComponent<SkillTreeNodeUI>();
            nodeUi.Setup(node, () => OnNodeClicked(node.nodeId));
            spawnedNodes.Add(nodeUi);
        }

        foreach (SkillNodeDefinition node in tree.nodes)
        {
            if (node.prereqNodeIds == null) continue;
            foreach (string prereqId in node.prereqNodeIds)
            {
                if (!positions.TryGetValue(prereqId, out RectTransform from)) continue;
                if (!positions.TryGetValue(node.nodeId, out RectTransform to)) continue;
                spawnedEdges.Add(BuildEdge(from, to));
            }
        }

        RefreshNodeStates();
    }

    /// <summary>Clicking a node only selects it — it does NOT buy. Buy is a separate button so a
    /// player always sees what a node does before spending souls on it.</summary>
    private void OnNodeClicked(string nodeId)
    {
        int index = orderedNodes.FindIndex(n => n.nodeId == nodeId);
        if (index < 0) return;
        selectedIndex = index;
        RefreshNodeStates();
    }

    private void RefreshNodeStates()
    {
        if (definition == null || save == null) return;
        SkillTreeDefinition tree = definition.skillTrees[currentTreeIndex];

        for (int i = 0; i < orderedNodes.Count; i++)
        {
            SkillNodeDefinition node = orderedNodes[i];
            SkillTreeNodeUI ui = spawnedNodes[i];

            bool purchased = SkillTreeService.IsNodePurchased(save, tree.skillId, node.nodeId);
            bool prereqsMet = SkillTreeService.ArePrereqsMet(save, tree.skillId, node);
            SkillNodeVisualState state;

            if (i == selectedIndex)
            {
                state = SkillNodeVisualState.Selected;
            }
            else if (purchased)
            {
                state = SkillNodeVisualState.Purchased;
            }
            else if (!prereqsMet)
            {
                state = SkillNodeVisualState.Locked;
            }
            else if (save.souls >= node.cost)
            {
                state = SkillNodeVisualState.Affordable;
            }
            else
            {
                state = SkillNodeVisualState.Unaffordable;
            }

            ui.SetState(state, node.cost);
        }

        RefreshDescription(tree);
    }

    private void RefreshDescription(SkillTreeDefinition tree)
    {
        if (selectedIndex < 0 || selectedIndex >= orderedNodes.Count)
        {
            if (descriptionText != null) descriptionText.text = "";
            SetBuyState(false, "");
            return;
        }

        SkillNodeDefinition node = orderedNodes[selectedIndex];
        bool purchased = SkillTreeService.IsNodePurchased(save, tree.skillId, node.nodeId);
        bool prereqsMet = SkillTreeService.ArePrereqsMet(save, tree.skillId, node);
        bool canAfford = save.souls >= node.cost;

        string effectLine = string.IsNullOrEmpty(node.description) ? node.displayName : node.description;

        if (descriptionText != null)
        {
            if (purchased)
            {
                descriptionText.text = $"{node.displayName}\n{effectLine}\n\nAlready unlocked.";
            }
            else if (!prereqsMet)
            {
                descriptionText.text = $"{node.displayName}\n{effectLine}\n\nUnlock the node(s) above first.";
            }
            else
            {
                descriptionText.text = $"{node.displayName}\n{effectLine}\n\nCosts {node.cost} souls.";
            }
        }

        if (purchased)
        {
            SetBuyState(false, "Unlocked");
        }
        else if (!prereqsMet)
        {
            SetBuyState(false, "Locked");
        }
        else
        {
            SetBuyState(canAfford, $"Buy — {node.cost} souls");
        }
    }

    private void SetBuyState(bool interactable, string label)
    {
        if (buyButton != null) buyButton.interactable = interactable;
        if (buyButtonText != null) buyButtonText.text = label;
    }

    private GameObject BuildEdge(RectTransform from, RectTransform to)
    {
        GameObject go = Instantiate(edgePrefab, edgeContainer);
        var rect = (RectTransform)go.transform;

        Vector2 a = from.anchoredPosition;
        Vector2 b = to.anchoredPosition;
        Vector2 mid = (a + b) * 0.5f;
        float length = Vector2.Distance(a, b);
        float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

        rect.anchoredPosition = mid;
        rect.sizeDelta = new Vector2(length, rect.sizeDelta.y);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);

        return go;
    }

    private void ClearSpawnedNodes()
    {
        foreach (var node in spawnedNodes) if (node != null) Destroy(node.gameObject);
        foreach (var edge in spawnedEdges) if (edge != null) Destroy(edge);
        spawnedNodes.Clear();
        spawnedEdges.Clear();
    }
}
