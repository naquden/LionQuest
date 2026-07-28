using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full-screen skill tree panel opened from CharacterSelection. No CharacterStats instance exists
/// yet at this point (the player hasn't spawned), so this reads/writes CharacterSaveData directly
/// through SaveManager + SkillTreeService — the same rules CharacterStats uses in-game.
/// Every branch (attributes + all skill trees) is laid out at once, fanned outward from one shared
/// root at bottom-centre — no tabs. Clicking a node only selects it (shows description + rank +
/// before/after); a separate Buy button spends souls and advances the node's rank.
/// </summary>
public class SkillTreeScreenController : MonoBehaviour
{
    private const float CellWidth = 130f;
    private const float CellHeight = 130f;
    private const float BranchGapX = 260f;

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text soulsText;
    [SerializeField] private RectTransform branchTotalsContainer;
    [SerializeField] private GameObject branchTotalPrefab;
    [SerializeField] private RectTransform nodeContainer;
    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private RectTransform linkContainer;
    [SerializeField] private GameObject linkPrefab;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;

    private CharacterDefinition definition;
    private CharacterSaveData save;
    private string selectedNodeId;
    private string selectedSkillId;

    private class SpawnedNode
    {
        public SkillTreeDefinition tree;
        public SkillNodeDefinition node;
        public SkillTreeNodeUI ui;
        public Vector2 position;
    }

    private readonly List<SpawnedNode> spawnedNodes = new List<SpawnedNode>();
    private readonly List<SkillTreeLinkUI> spawnedLinks = new List<SkillTreeLinkUI>();
    private readonly List<TMP_Text> spawnedBranchTotals = new List<TMP_Text>();
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
        selectedNodeId = null;
        selectedSkillId = null;

        if (panelRoot != null) panelRoot.SetActive(true);
        if (characterNameText != null) characterNameText.text = character.displayName;

        RebuildTree();
        SelectDefaultNode();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        definition = null;
        save = null;
        onClose?.Invoke();
        onClose = null;
    }

    /// <summary>Moves the node cursor to the nearest node in the pressed direction (spatial
    /// navigation across the unified canvas, not a walk through a flat list).</summary>
    public void Navigate(Vector2 direction)
    {
        if (spawnedNodes.Count == 0 || direction.sqrMagnitude < 0.01f) return;

        SpawnedNode current = FindSelected();
        if (current == null)
        {
            SelectDefaultNode();
            return;
        }

        Vector2 dir = direction.normalized;
        SpawnedNode best = null;
        float bestScore = float.MaxValue;

        foreach (SpawnedNode candidate in spawnedNodes)
        {
            if (candidate == current) continue;
            Vector2 offset = candidate.position - current.position;
            float alongDir = Vector2.Dot(offset, dir);
            if (alongDir <= 0.01f) continue;

            float lateral = (offset - dir * alongDir).magnitude;
            float score = alongDir + lateral * 2f;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null) Select(best.tree.skillId, best.node.nodeId);
    }

    public void SubmitSelected() => BuySelected();

    private void BuySelected()
    {
        if (definition == null || save == null || selectedNodeId == null) return;

        if (!SkillTreeService.TryPurchase(definition, save, selectedSkillId, selectedNodeId)) return;

        SaveManager.SaveCharacter(save);
        RefreshAllStates();
    }

    private void Select(string skillId, string nodeId)
    {
        selectedSkillId = skillId;
        selectedNodeId = nodeId;
        RefreshAllStates();
        CenterViewOnSelected();
    }

    /// <summary>Scrolls the node canvas so the selected node sits in the viewport centre —
    /// keyboard/gamepad navigation must never leave the cursor off-screen.</summary>
    private void CenterViewOnSelected()
    {
        SpawnedNode selected = FindSelected();
        if (selected == null || nodeContainer == null) return;

        var viewport = nodeContainer.parent as RectTransform;
        if (viewport == null) return;

        float viewportWidth = viewport.rect.width;
        float viewportHeight = viewport.rect.height;
        float contentWidth = nodeContainer.rect.width;
        float contentHeight = nodeContainer.rect.height;

        float targetX = viewportWidth * 0.5f - selected.position.x;
        float targetY = -viewportHeight * 0.5f - selected.position.y;

        float minX = Mathf.Min(0f, viewportWidth - contentWidth);
        float maxX = Mathf.Max(0f, viewportWidth - contentWidth);
        float minY = Mathf.Min(0f, viewportHeight - contentHeight);
        float maxY = Mathf.Max(0f, viewportHeight - contentHeight);

        nodeContainer.anchoredPosition = new Vector2(
            Mathf.Clamp(targetX, minX, maxX),
            Mathf.Clamp(targetY, minY, maxY));
    }

    private void SelectDefaultNode()
    {
        SpawnedNode first = spawnedNodes.FirstOrDefault(n => n.node.tier == SkillNodeTier.Keystone)
            ?? spawnedNodes.FirstOrDefault();
        if (first != null) Select(first.tree.skillId, first.node.nodeId);
    }

    private SpawnedNode FindSelected()
    {
        return spawnedNodes.FirstOrDefault(n => n.tree.skillId == selectedSkillId && n.node.nodeId == selectedNodeId);
    }

    // ------------------------------------------------------------------
    // Layout — every tree fanned outward from one shared root at bottom-centre
    // ------------------------------------------------------------------

    private void RebuildTree()
    {
        ClearSpawned();

        if (definition == null || definition.skillTrees == null || definition.skillTrees.Length == 0) return;
        if (soulsText != null) soulsText.text = $"Souls: {save.souls}";

        var positions = new Dictionary<(string skillId, string nodeId), Vector2>();
        int treeCount = definition.skillTrees.Length;
        float totalWidth = (treeCount - 1) * BranchGapX;
        float startX = -totalWidth * 0.5f;

        for (int t = 0; t < treeCount; t++)
        {
            SkillTreeDefinition tree = definition.skillTrees[t];
            if (tree.nodes == null) continue;

            float branchOriginX = startX + t * BranchGapX;

            foreach (SkillNodeDefinition node in tree.nodes)
            {
                Vector2 pos = new Vector2(branchOriginX + node.gridPos.x * CellWidth, node.gridPos.y * CellHeight);
                positions[(tree.skillId, node.nodeId)] = pos;

                GameObject go = Instantiate(nodePrefab, nodeContainer);
                var rect = (RectTransform)go.transform;
                rect.anchoredPosition = pos;

                var nodeUi = go.GetComponent<SkillTreeNodeUI>();
                nodeUi.Setup(node, () => Select(tree.skillId, node.nodeId));

                spawnedNodes.Add(new SpawnedNode { tree = tree, node = node, ui = nodeUi, position = pos });
            }
        }

        foreach (SpawnedNode spawned in spawnedNodes)
        {
            if (spawned.node.prereqNodeIds == null) continue;
            foreach (string prereqId in spawned.node.prereqNodeIds)
            {
                if (!positions.TryGetValue((spawned.tree.skillId, prereqId), out Vector2 from)) continue;
                spawnedLinks.Add(BuildLink(from, spawned.position, spawned.tree, prereqId));
            }
        }

        BuildBranchTotals();
        FitContainerToContent();
        RefreshAllStates();
    }

    private SkillTreeLinkUI BuildLink(Vector2 from, Vector2 to, SkillTreeDefinition tree, string prereqNodeId)
    {
        GameObject go = Instantiate(linkPrefab, linkContainer);
        var link = go.GetComponent<SkillTreeLinkUI>();
        link.Layout(from, to);

        bool active = SkillTreeService.GetRank(save, tree.skillId, prereqNodeId) >= 1;
        link.SetBranchColor(tree.branchColor, active);
        return link;
    }

    private void BuildBranchTotals()
    {
        foreach (var text in spawnedBranchTotals) if (text != null) Destroy(text.gameObject);
        spawnedBranchTotals.Clear();

        if (branchTotalsContainer == null || branchTotalPrefab == null) return;

        foreach (SkillTreeDefinition tree in definition.skillTrees)
        {
            GameObject go = Instantiate(branchTotalPrefab, branchTotalsContainer);
            TMP_Text text = go.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.color = tree.branchColor;
                spawnedBranchTotals.Add(text);
            }
        }

        RefreshBranchTotals();
    }

    private void RefreshBranchTotals()
    {
        for (int i = 0; i < definition.skillTrees.Length && i < spawnedBranchTotals.Count; i++)
        {
            SkillTreeDefinition tree = definition.skillTrees[i];
            int spent = SpentOnTree(tree);
            spawnedBranchTotals[i].text = $"{tree.displayName.ToUpperInvariant()} {spent}";
        }
    }

    private int SpentOnTree(SkillTreeDefinition tree)
    {
        int spent = 0;
        if (tree.nodes == null) return 0;
        foreach (SkillNodeDefinition node in tree.nodes)
        {
            spent += SkillTreeService.GetRank(save, tree.skillId, node.nodeId) * node.cost;
        }
        return spent;
    }

    /// <summary>Sizes the node container to the true bounds of all spawned nodes, not a fixed
    /// oversized guess — avoids the blank-viewport regression from mismatched anchoring.</summary>
    private void FitContainerToContent()
    {
        if (nodeContainer == null || spawnedNodes.Count == 0) return;

        float minX = spawnedNodes.Min(n => n.position.x);
        float maxX = spawnedNodes.Max(n => n.position.x);
        float minY = spawnedNodes.Min(n => n.position.y);
        float maxY = spawnedNodes.Max(n => n.position.y);

        const float padding = 150f;
        float width = (maxX - minX) + padding * 2f;
        float height = (maxY - minY) + padding * 2f;

        nodeContainer.sizeDelta = new Vector2(width, height);

        // Shift every spawned node/link so the bounds' min corner sits at the padded origin —
        // container is top-left anchored, so content must live in positive-x, negative-y space.
        Vector2 shift = new Vector2(-minX + padding, -maxY - padding);
        foreach (SpawnedNode spawned in spawnedNodes)
        {
            spawned.position += shift;
            spawned.ui.Rect.anchoredPosition = spawned.position;
        }

        RelayoutLinks(shift);
    }

    private void RelayoutLinks(Vector2 shift)
    {
        int linkIndex = 0;
        foreach (SpawnedNode spawned in spawnedNodes)
        {
            if (spawned.node.prereqNodeIds == null) continue;
            foreach (string prereqId in spawned.node.prereqNodeIds)
            {
                SpawnedNode from = spawnedNodes.FirstOrDefault(n => n.tree.skillId == spawned.tree.skillId && n.node.nodeId == prereqId);
                if (from == null) continue;
                if (linkIndex >= spawnedLinks.Count) break;
                spawnedLinks[linkIndex].Layout(from.position, spawned.position);
                linkIndex++;
            }
        }
    }

    // ------------------------------------------------------------------
    // State refresh
    // ------------------------------------------------------------------

    private void RefreshAllStates()
    {
        if (definition == null || save == null) return;
        if (soulsText != null) soulsText.text = $"Souls: {save.souls}";

        foreach (SpawnedNode spawned in spawnedNodes)
        {
            int rank = SkillTreeService.GetRank(save, spawned.tree.skillId, spawned.node.nodeId);
            bool prereqsMet = SkillTreeService.ArePrereqsMet(save, spawned.tree.skillId, spawned.node);
            bool isSelected = spawned.tree.skillId == selectedSkillId && spawned.node.nodeId == selectedNodeId;

            SkillNodeVisualState state;
            if (isSelected)
            {
                state = SkillNodeVisualState.Selected;
            }
            else if (rank >= spawned.node.maxRank)
            {
                state = SkillNodeVisualState.Maxed;
            }
            else if (!prereqsMet)
            {
                state = SkillNodeVisualState.Locked;
            }
            else if (rank > 0)
            {
                state = SkillNodeVisualState.Ranked;
            }
            else if (save.souls >= spawned.node.cost)
            {
                state = SkillNodeVisualState.Affordable;
            }
            else
            {
                state = SkillNodeVisualState.Unaffordable;
            }

            spawned.ui.SetState(state, rank, spawned.node.maxRank, spawned.node.cost, spawned.tree.branchColor);
        }

        RelinkColors();
        RefreshBranchTotals();
        RefreshDescription();
    }

    private void RelinkColors()
    {
        int linkIndex = 0;
        foreach (SpawnedNode spawned in spawnedNodes)
        {
            if (spawned.node.prereqNodeIds == null) continue;
            foreach (string prereqId in spawned.node.prereqNodeIds)
            {
                if (linkIndex >= spawnedLinks.Count) break;
                bool active = SkillTreeService.GetRank(save, spawned.tree.skillId, prereqId) >= 1;
                spawnedLinks[linkIndex].SetBranchColor(spawned.tree.branchColor, active);
                linkIndex++;
            }
        }
    }

    private void RefreshDescription()
    {
        SpawnedNode selected = FindSelected();
        if (selected == null)
        {
            if (descriptionText != null) descriptionText.text = "Select a node to see what it does.";
            SetBuyState(false, "");
            return;
        }

        SkillNodeDefinition node = selected.node;
        SkillTreeDefinition tree = selected.tree;
        int rank = SkillTreeService.GetRank(save, tree.skillId, node.nodeId);
        bool prereqsMet = SkillTreeService.ArePrereqsMet(save, tree.skillId, node);
        bool canAfford = save.souls >= node.cost;
        bool maxed = rank >= node.maxRank;

        string effectLine = string.IsNullOrEmpty(node.description) ? node.displayName : node.description;
        string rankLine = node.maxRank > 1 ? $"Rank {rank} / {node.maxRank}" : (rank >= 1 ? "Unlocked" : "");
        string beforeAfter = BuildBeforeAfterLine(tree, node, rank);

        if (descriptionText != null)
        {
            var lines = new List<string> { node.displayName, effectLine };
            if (!string.IsNullOrEmpty(rankLine)) lines.Add(rankLine);
            if (!string.IsNullOrEmpty(beforeAfter)) lines.Add(beforeAfter);
            if (maxed) lines.Add("Fully upgraded.");
            else if (!prereqsMet) lines.Add("Unlock a node above first.");
            else lines.Add($"Costs {node.cost} souls.");

            descriptionText.text = string.Join("\n\n", lines.Where(l => !string.IsNullOrEmpty(l)));
        }

        if (maxed) SetBuyState(false, "Maxed");
        else if (!prereqsMet) SetBuyState(false, "Locked");
        else SetBuyState(canAfford, $"Buy — {node.cost} souls");
    }

    private string BuildBeforeAfterLine(SkillTreeDefinition tree, SkillNodeDefinition node, int currentRank)
    {
        if (tree.displayBases == null) return null;
        SkillDisplayBase basis = tree.displayBases.FirstOrDefault(b => b.effect == node.effect);
        if (basis == null) return null;

        var beforeSave = new CharacterSaveData(save.characterID) { souls = save.souls, purchasedSkillNodes = save.purchasedSkillNodes };
        float before = SkillTreeService.GetValue(SkillTreeService.ResolveSkillValues(definition, beforeSave), tree.skillId, node.effect, basis.baseValue);

        // Simulate one more rank of this node to compute "after".
        var afterSave = new CharacterSaveData(save.characterID) { souls = save.souls, purchasedSkillNodes = new List<string>(save.purchasedSkillNodes) };
        SkillTreeService.TryPurchase(definition, afterSave, tree.skillId, node.nodeId);
        float after = SkillTreeService.GetValue(SkillTreeService.ResolveSkillValues(definition, afterSave), tree.skillId, node.effect, basis.baseValue);

        if (Mathf.Approximately(before, after)) return null;
        return $"{node.effect} {before}{basis.unit} → {after}{basis.unit}";
    }

    private void SetBuyState(bool interactable, string label)
    {
        if (buyButton != null) buyButton.interactable = interactable;
        if (buyButtonText != null) buyButtonText.text = label;
    }

    private void ClearSpawned()
    {
        foreach (var node in spawnedNodes) if (node.ui != null) Destroy(node.ui.gameObject);
        foreach (var link in spawnedLinks) if (link != null) Destroy(link.gameObject);
        spawnedNodes.Clear();
        spawnedLinks.Clear();
    }
}
