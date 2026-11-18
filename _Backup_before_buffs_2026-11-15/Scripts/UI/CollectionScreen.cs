using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "My Cards" (design doc sections 3, 4, 5, 6, 10): every owned card, the 3 build slots, rerolls with a preview
/// (then save to a slot or discard), fusion with fail rates and extra orbs, and card locks.
/// </summary>
public class CollectionScreen : MonoBehaviour
{
    public Button backButton;
    public Text titleText;
    public Button rarityFilterButton;
    public Button elementFilterButton;
    public Button sortButton;
    public RectTransform grid;
    public RectTransform slotRow;
    public Text viewHeader;
    public CardView detailView;
    public Text detailText;
    public Button[] saveSlotButtons = new Button[3];
    public Button makeMainButton;
    public Button lockButton;
    public Button rerollStatsButton;
    public Button rerollElementButton;
    public Button rerollTalentButton;
    public Button fusionButton;
    public Text messageText;

    [Header("Reroll preview")]
    public GameObject previewPanel;
    public Text previewTitle;
    public Text previewOld;
    public Text previewNew;
    public Button[] previewSaveButtons = new Button[3];
    public Button previewDiscardButton;

    [Header("Fusion")]
    public GameObject fusionPanel;
    public RectTransform fusionMainHolder;
    public RectTransform fodderGrid;
    public Text fusionInfo;
    public Button extraMinusButton;
    public Button extraPlusButton;
    public Button fuseButton;
    public Button fusionCloseButton;
    public Text fusionResult;

    enum SortMode { Power, Rarity, Newest, Roll }

    int rarityFilter = -1;
    int elementFilter = -1;
    SortMode sortMode = SortMode.Power;

    CardInstance selected;
    BuildData viewBuild;
    int viewSlot = -1;
    BuildData pendingBuild;
    int unlockConfirmSlot = -1;
    float unlockConfirmUntil;

    readonly List<CardInstance> fodderSelection = new List<CardInstance>();
    int extraOrbs;
    readonly Dictionary<int, CardTile> tiles = new Dictionary<int, CardTile>();

    void Start()
    {
        backButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.MainMenu));
        rarityFilterButton.onClick.AddListener(() => { rarityFilter = rarityFilter + 1 > GameRules.MaxTier ? -1 : rarityFilter + 1; RebuildGrid(); });
        elementFilterButton.onClick.AddListener(() => { elementFilter = elementFilter + 1 >= ElementSystem.All.Length ? -1 : elementFilter + 1; RebuildGrid(); });
        sortButton.onClick.AddListener(() => { sortMode = (SortMode)(((int)sortMode + 1) % 4); RebuildGrid(); });
        for (int i = 0; i < saveSlotButtons.Length; i++)
        {
            int slotIndex = i;
            if (saveSlotButtons[i] != null) saveSlotButtons[i].onClick.AddListener(() => SaveViewToSlot(slotIndex));
            if (previewSaveButtons[i] != null) previewSaveButtons[i].onClick.AddListener(() => SavePendingToSlot(slotIndex));
        }
        makeMainButton.onClick.AddListener(MakeMain);
        lockButton.onClick.AddListener(ToggleLock);
        rerollStatsButton.onClick.AddListener(() => Reroll(0));
        rerollElementButton.onClick.AddListener(() => Reroll(1));
        rerollTalentButton.onClick.AddListener(() => Reroll(2));
        fusionButton.onClick.AddListener(OpenFusion);
        previewDiscardButton.onClick.AddListener(() => { pendingBuild = null; previewPanel.SetActive(false); Message("Reroll discarded."); });
        extraMinusButton.onClick.AddListener(() => { extraOrbs = Mathf.Max(0, extraOrbs - 1); RefreshFusion(); });
        extraPlusButton.onClick.AddListener(() => { extraOrbs = Mathf.Min(GameRules.MaxExtraFusionOrbs, extraOrbs + 1); RefreshFusion(); });
        fuseButton.onClick.AddListener(DoFuse);
        fusionCloseButton.onClick.AddListener(() => { fusionPanel.SetActive(false); RefreshAll(); });
        previewPanel.SetActive(false);
        fusionPanel.SetActive(false);
        messageText.text = "";

        int active = PlayerProfile.ActiveSlot;
        var slot = PlayerProfile.GetSlot(active);
        if (slot != null && slot.HasCard) ViewSlot(active);
        else if (PlayerProfile.Data.cards.Count > 0) ViewCard(PlayerProfile.Data.cards[0]);
        RefreshAll();
    }

    void Message(string text) => messageText.text = text;

    void RefreshAll()
    {
        RebuildGrid();
        RefreshSlots();
        RefreshDetail();
    }

    // ---------------------------------------------------------------- grid

    IEnumerable<CardInstance> FilteredCards()
    {
        IEnumerable<CardInstance> cards = PlayerProfile.Data.cards;
        if (rarityFilter >= 0) cards = cards.Where(c => c.Tier == rarityFilter);
        if (elementFilter >= 0) cards = cards.Where(c => c.build.element == ElementSystem.All[elementFilter]);
        switch (sortMode)
        {
            case SortMode.Rarity: return cards.OrderByDescending(c => c.Tier).ThenByDescending(c => CardStats.Power(c));
            case SortMode.Newest: return cards.OrderByDescending(c => c.uid);
            case SortMode.Roll: return cards.OrderByDescending(c => c.build.TotalRoll);
            default: return cards.OrderByDescending(c => CardStats.Power(c));
        }
    }

    void RebuildGrid()
    {
        UIFactory.ButtonLabel(rarityFilterButton).text = rarityFilter < 0 ? "RARITY: ALL" : "RARITY: " + CardStats.RarityName((CardRarity)rarityFilter).ToUpperInvariant();
        UIFactory.ButtonLabel(elementFilterButton).text = elementFilter < 0 ? "ELEMENT: ALL" : "ELEMENT: " + ElementSystem.DisplayName(ElementSystem.All[elementFilter]).ToUpperInvariant();
        UIFactory.ButtonLabel(sortButton).text = "SORT: " + sortMode.ToString().ToUpperInvariant();

        UIFactory.Clear(grid);
        tiles.Clear();
        var list = FilteredCards().ToList();
        foreach (var c in list)
        {
            var card = c;
            var tile = CardTile.Build(grid, $"Card {card.uid}", 150, 210, card.Tier >= 6);
            tile.SetInstance(card);
            var slots = Enumerable.Range(0, GameRules.SlotCount).Where(i => PlayerProfile.GetSlot(i).unlocked && PlayerProfile.GetSlot(i).cardUid == card.uid).Select(i => (i + 1).ToString()).ToList();
            if (slots.Count > 0) tile.SetBadge("SLOT " + string.Join(",", slots), UIFactory.Accent);
            tile.button.onClick.AddListener(() => ViewCard(card));
            tile.SetSelected(card == selected && viewSlot < 0);
            tiles[card.uid] = tile;
        }
        titleText.text = $"MY CARDS  <size=26><color=#C9C3D8>{list.Count}/{PlayerProfile.Data.cards.Count}</color></size>";
    }

    // ---------------------------------------------------------------- slots

    void RefreshSlots()
    {
        UIFactory.Clear(slotRow);
        int active = PlayerProfile.ActiveSlot;
        for (int i = 0; i < GameRules.SlotCount; i++)
        {
            int index = i;
            var slot = PlayerProfile.GetSlot(i);
            var tile = CardTile.Build(slotRow, $"Slot {i + 1}", 112, 156, true);
            var rt = (RectTransform)tile.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(190 + i * 132f, -6f);
            tile.nameText.fontSize = 13;

            if (!slot.unlocked)
            {
                tile.SetEmpty($"SLOT {i + 1}");
                tile.SetLocked(true, $"{GameRules.SlotUnlockCost(i)}\nDIAMONDS");
            }
            else if (!slot.HasCard)
            {
                tile.SetEmpty($"SLOT {i + 1}");
            }
            else
            {
                var card = PlayerProfile.FindCard(slot.cardUid);
                tile.SetInstance(card, slot.build);
                tile.SetLockIcon(false);
                tile.SetBadge(i == active ? "MAIN" : $"SLOT {i + 1}", i == active ? UIFactory.Accent2 : UIFactory.Accent);
            }
            tile.SetSelected(viewSlot == i);
            tile.button.onClick.AddListener(() => OnSlotClicked(index));
        }
    }

    void OnSlotClicked(int index)
    {
        var slot = PlayerProfile.GetSlot(index);
        if (!slot.unlocked)
        {
            if (unlockConfirmSlot == index && Time.unscaledTime < unlockConfirmUntil)
            {
                string err = PlayerProfile.UnlockSlot(index);
                Message(err == null ? $"<color=#7CFC6A>Slot {index + 1} unlocked!</color>" : $"<color=#FF6B6B>{err}.</color>");
                unlockConfirmSlot = -1;
                RefreshAll();
            }
            else
            {
                unlockConfirmSlot = index;
                unlockConfirmUntil = Time.unscaledTime + 3f;
                Message($"Click slot {index + 1} again to unlock it for {GameRules.SlotUnlockCost(index)} diamonds (you have {PlayerProfile.Data.diamonds}).");
            }
            return;
        }
        if (slot.HasCard) ViewSlot(index);
        else Message($"Slot {index + 1} is empty: pick a card and press SAVE TO S{index + 1}.");
    }

    // ---------------------------------------------------------------- view

    void ViewCard(CardInstance card)
    {
        selected = card;
        viewBuild = card != null ? card.build : null;
        viewSlot = -1;
        Message("");
        RefreshAll();
    }

    void ViewSlot(int index)
    {
        var slot = PlayerProfile.GetSlot(index);
        var card = PlayerProfile.FindCard(slot.cardUid);
        if (card == null) return;
        selected = card;
        viewBuild = slot.build;
        viewSlot = index;
        Message("");
        RefreshAll();
    }

    void RefreshDetail()
    {
        bool has = selected != null && viewBuild != null;
        detailView.gameObject.SetActive(has);
        foreach (var b in new[] { makeMainButton, lockButton, rerollStatsButton, rerollElementButton, rerollTalentButton, fusionButton })
            b.interactable = has;
        foreach (var b in saveSlotButtons) if (b != null) b.interactable = has;
        if (!has)
        {
            viewHeader.text = "Select a card";
            detailText.text = "";
            return;
        }

        var art = PlayerProfile.Art(selected);
        viewHeader.text = viewSlot >= 0
            ? $"SLOT {viewSlot + 1} BUILD{(viewSlot == PlayerProfile.ActiveSlot ? "  <color=#FF6FA8>(MAIN)</color>" : "")}"
            : $"CARD #{selected.uid}  <size=20><color=#C9C3D8>original build</color></size>";
        detailView.ShowInstance(selected, viewBuild);

        int tier = selected.Tier;
        float mult = GameRules.RarityMultiplier(tier);
        string Line(string stat, int roll, int value, bool hp) =>
            $"{stat} <b>{value}</b>  <color=#9AA4B5>= (100 {(roll >= 0 ? "+" : "-")} {Mathf.Abs(roll)}) x {mult:0.00}{(hp ? " x 10" : "")}</color>";
        var strong = ElementSystem.StrongAgainst(viewBuild.element).Select(ElementSystem.DisplayName).ToList();
        var talent = CardDatabase.Instance != null ? CardDatabase.Instance.FindTalent(viewBuild.talent) : null;
        detailText.text =
            $"<color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(selected.rarity))}><b>{CardStats.RarityName(selected.rarity)}</b></color>  +{(mult - 1f) * 100f:0}% stats, Roll max ±{GameRules.MaxDeviation(tier)}\n" +
            Line("HP ", viewBuild.hp, CardStats.HpOf(selected.rarity, viewBuild), true) + "\n" +
            Line("ATK", viewBuild.atk, CardStats.AtkOf(selected.rarity, viewBuild), false) + "\n" +
            Line("DEF", viewBuild.def, CardStats.DefOf(selected.rarity, viewBuild), false) + "\n" +
            Line("SPD", viewBuild.spd, CardStats.SpdOf(selected.rarity, viewBuild), false) + "\n" +
            $"<color=#{ColorUtility.ToHtmlStringRGB(ElementSystem.GetColor(viewBuild.element))}><b>{ElementSystem.DisplayName(viewBuild.element)}</b></color>  <color=#9AA4B5>strong vs {(strong.Count > 0 ? string.Join(", ", strong) : "nothing")}</color>\n" +
            (talent != null ? $"<b>{talent.DisplayName}</b> <color=#7CFC6A>+{(GameRules.TalentPower(tier) - 1f) * 100f:0}%</color>: <color=#C9C3D8>{talent.Description}</color>" : "<color=#9AA4B5>No talent</color>") +
            (art != null && !string.IsNullOrEmpty(art.anime) ? $"\n<color=#FFD54F>{art.anime}</color>" : "");

        UIFactory.ButtonLabel(lockButton).text = selected.locked ? "UNLOCK CARD" : "LOCK CARD";
        UIFactory.ButtonLabel(rerollStatsButton).text = $"REROLL STATS  <size=17>({GameRules.StatRerollCost(tier):N0} gold)</size>";
        UIFactory.ButtonLabel(rerollElementButton).text = $"REROLL ELEMENT  <size=17>(1 orb, have {PlayerProfile.Data.elementOrbs})</size>";
        UIFactory.ButtonLabel(rerollTalentButton).text = $"REROLL TALENT  <size=17>(1 orb, have {PlayerProfile.Data.talentOrbs})</size>";
        UIFactory.ButtonLabel(fusionButton).text = GameRules.CanFuseFrom(tier) ? $"FUSION  <size=17>({GameRules.FusionBaseRate(tier) * 100f:0}% to {CardStats.RarityName((CardRarity)(tier + 1))})</size>" : "FUSION  <size=17>(max rarity)</size>";
        makeMainButton.interactable = viewSlot >= 0 && viewSlot != PlayerProfile.ActiveSlot;
        for (int i = 0; i < saveSlotButtons.Length; i++)
        {
            var slot = PlayerProfile.GetSlot(i);
            saveSlotButtons[i].interactable = slot.unlocked;
            UIFactory.ButtonLabel(saveSlotButtons[i]).text = slot.unlocked ? $"SAVE TO S{i + 1}" : $"S{i + 1} LOCKED";
        }
    }

    void SaveViewToSlot(int index)
    {
        if (selected == null || viewBuild == null) return;
        string err = PlayerProfile.SaveToSlot(index, selected, viewBuild);
        if (err != null) { Message($"<color=#FF6B6B>{err}.</color>"); return; }
        Message($"<color=#7CFC6A>Saved to slot {index + 1}.</color>");
        ViewSlot(index);
    }

    void MakeMain()
    {
        if (viewSlot < 0) return;
        PlayerProfile.ActiveSlot = viewSlot;
        Message($"<color=#7CFC6A>Slot {viewSlot + 1} is now your main build.</color>");
        RefreshAll();
    }

    void ToggleLock()
    {
        if (selected == null) return;
        PlayerProfile.ToggleLock(selected);
        Message(selected.locked ? "Locked: this card can't be used as fodder." : (PlayerProfile.IsInSlot(selected.uid) ? "Unlocked, but it stays protected while it is in a slot." : "Unlocked."));
        RefreshAll();
    }

    // ---------------------------------------------------------------- rerolls

    void Reroll(int kind)
    {
        if (selected == null || viewBuild == null) return;
        BuildData result;
        string err = kind == 0 ? PlayerProfile.RerollStats(selected, viewBuild, out result)
                   : kind == 1 ? PlayerProfile.RerollElement(selected, viewBuild, out result)
                   : PlayerProfile.RerollTalent(selected, viewBuild, out result);
        if (err != null) { Message($"<color=#FF6B6B>{err}.</color>"); return; }
        SfxSynth.Play("skill", 0.7f);
        pendingBuild = result;
        previewTitle.text = kind == 0 ? "STAT REROLL" : kind == 1 ? "ELEMENT REROLL" : "TALENT REROLL";
        previewOld.text = BuildSummary("CURRENT", viewBuild, null);
        previewNew.text = BuildSummary("NEW", result, viewBuild);
        for (int i = 0; i < previewSaveButtons.Length; i++)
        {
            var slot = PlayerProfile.GetSlot(i);
            previewSaveButtons[i].interactable = slot.unlocked;
            UIFactory.ButtonLabel(previewSaveButtons[i]).text = slot.unlocked ? $"SAVE TO SLOT {i + 1}" : $"SLOT {i + 1} LOCKED";
        }
        previewPanel.SetActive(true);
        RefreshDetail();
    }

    string BuildSummary(string title, BuildData b, BuildData compare)
    {
        var r = selected.rarity;
        string Diff(int now, int before) => compare == null ? "" : now > before ? $" <color=#7CFC6A>(+{now - before})</color>" : now < before ? $" <color=#FF6B6B>({now - before})</color>" : "";
        var talent = CardDatabase.Instance != null ? CardDatabase.Instance.FindTalent(b.talent) : null;
        string talentName = talent != null ? talent.DisplayName : (b.talent ?? "-");
        bool talentChanged = compare != null && compare.talent != b.talent;
        bool elementChanged = compare != null && compare.element != b.element;
        return $"<b>{title}</b>  <color=#9AA4B5>Power {CardStats.Power(r, b)}</color>{Diff(CardStats.Power(r, b), compare != null ? CardStats.Power(r, compare) : 0)}\n\n" +
               $"HP  {CardStats.HpOf(r, b)}  <color=#9AA4B5>roll {b.hp:+0;-0;0}</color>{Diff(CardStats.HpOf(r, b), compare != null ? CardStats.HpOf(r, compare) : 0)}\n" +
               $"ATK {CardStats.AtkOf(r, b)}  <color=#9AA4B5>roll {b.atk:+0;-0;0}</color>{Diff(CardStats.AtkOf(r, b), compare != null ? CardStats.AtkOf(r, compare) : 0)}\n" +
               $"DEF {CardStats.DefOf(r, b)}  <color=#9AA4B5>roll {b.def:+0;-0;0}</color>{Diff(CardStats.DefOf(r, b), compare != null ? CardStats.DefOf(r, compare) : 0)}\n" +
               $"SPD {CardStats.SpdOf(r, b)}  <color=#9AA4B5>roll {b.spd:+0;-0;0}</color>{Diff(CardStats.SpdOf(r, b), compare != null ? CardStats.SpdOf(r, compare) : 0)}\n\n" +
               $"Element: <color=#{ColorUtility.ToHtmlStringRGB(ElementSystem.GetColor(b.element))}>{ElementSystem.DisplayName(b.element)}</color>{(elementChanged ? " <color=#FFD54F>NEW</color>" : "")}\n" +
               $"Talent: <b>{talentName}</b>{(talentChanged ? " <color=#FFD54F>NEW</color>" : "")}";
    }

    void SavePendingToSlot(int index)
    {
        if (pendingBuild == null) return;
        string err = PlayerProfile.SaveToSlot(index, selected, pendingBuild);
        if (err != null) { Message($"<color=#FF6B6B>{err}.</color>"); return; }
        pendingBuild = null;
        previewPanel.SetActive(false);
        Message($"<color=#7CFC6A>New build saved to slot {index + 1}.</color>");
        ViewSlot(index);
    }

    // ---------------------------------------------------------------- fusion

    void OpenFusion()
    {
        if (selected == null) return;
        if (!GameRules.CanFuseFrom(selected.Tier)) { Message("God is the maximum rarity."); return; }
        fodderSelection.Clear();
        foreach (var c in PlayerProfile.FodderCandidates(selected).Take(GameRules.FusionFodderCount)) fodderSelection.Add(c);
        extraOrbs = 0;
        fusionResult.text = "";
        fusionPanel.SetActive(true);
        RefreshFusion();
    }

    void RefreshFusion()
    {
        UIFactory.Clear(fusionMainHolder);
        var mainTile = CardTile.Build(fusionMainHolder, "Main", 220, 308, true);
        UIFactory.Place((RectTransform)mainTile.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 308));
        mainTile.SetInstance(selected, viewBuild);
        mainTile.SetBadge("MAIN CARD", UIFactory.Accent2);
        mainTile.button.interactable = false;

        UIFactory.Clear(fodderGrid);
        var candidates = PlayerProfile.FodderCandidates(selected);
        foreach (var c in candidates)
        {
            var card = c;
            var tile = CardTile.Build(fodderGrid, $"Fodder {card.uid}", 130, 182, false);
            tile.SetInstance(card);
            bool chosen = fodderSelection.Contains(card);
            tile.SetSelected(chosen);
            if (chosen) tile.SetBadge("FODDER", new Color(0.85f, 0.4f, 0.2f));
            tile.button.onClick.AddListener(() =>
            {
                if (fodderSelection.Contains(card)) fodderSelection.Remove(card);
                else if (fodderSelection.Count < GameRules.FusionFodderCount) fodderSelection.Add(card);
                RefreshFusion();
            });
        }

        int tier = selected.Tier;
        int orbCost = GameRules.FusionOrbCost(tier);
        float rate = GameRules.FusionSuccessRate(tier, extraOrbs);
        string reason = PlayerProfile.CanFuse(selected, fodderSelection, extraOrbs);
        fusionInfo.text =
            $"<b>{CardStats.RarityName(selected.rarity)} -> <color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor((CardRarity)Mathf.Min(8, tier + 1)))}>{CardStats.RarityName((CardRarity)Mathf.Min(8, tier + 1))}</color></b>\n\n" +
            $"Success: <size=40><b>{rate * 100f:0}%</b></size>\n<color=#9AA4B5>base {GameRules.FusionBaseRate(tier) * 100f:0}% + {extraOrbs} extra orb(s) x 5%</color>\n\n" +
            $"Fusion Orbs: {orbCost} + {extraOrbs} extra  <color=#9AA4B5>(have {PlayerProfile.Data.fusionOrbs})</color>\n" +
            $"Fodder: {fodderSelection.Count}/{GameRules.FusionFodderCount}  <color=#9AA4B5>({candidates.Count} available)</color>\n\n" +
            "<color=#C9C3D8>Success: +1 rarity, Roll/element/talent kept.\nFail: lose the fodder and all orbs; the main card stays.</color>" +
            (reason != null ? $"\n\n<color=#FF6B6B>{reason}</color>" : "");
        fuseButton.interactable = reason == null;
        UIFactory.ButtonLabel(fuseButton).text = $"FUSE  ({rate * 100f:0}%)";
    }

    void DoFuse()
    {
        var result = PlayerProfile.Fuse(selected, fodderSelection, extraOrbs);
        if (result == null) return;
        fodderSelection.Clear();
        extraOrbs = 0;
        if (result.success)
        {
            SfxSynth.Play("win");
            fusionResult.text = $"<color=#FFD54F><b>SUCCESS!</b> {CardStats.RarityName(result.from)} -> {CardStats.RarityName(result.to)}</color>";
        }
        else
        {
            SfxSynth.Play("lose");
            fusionResult.text = $"<color=#FF6B6B><b>FAILED</b> ({result.rate * 100f:0}%). Lost 2 fodder and {result.orbsSpent} orbs.</color>";
        }
        foreach (var c in PlayerProfile.FodderCandidates(selected).Take(GameRules.FusionFodderCount)) fodderSelection.Add(c);
        RefreshFusion();
    }

    // ---------------------------------------------------------------- layout

    public static CollectionScreen BuildLayout(RectTransform canvas)
    {
        var screen = canvas.gameObject.AddComponent<CollectionScreen>();
        AnimeBackground.Build(canvas, "cards", new Color(0.95f, 0.4f, 0.7f, 0.7f), new Color(0.1f, 0.06f, 0.18f), out _);
        UIFactory.CreateTopBarWithCurrency(canvas, "MY CARDS", out screen.backButton, out screen.titleText);
        var panelColor = new Color(0.06f, 0.05f, 0.12f, 0.96f);

        // Left: filters + grid + slots
        var left = UIFactory.CreatePanel(canvas, "CardsPanel", panelColor, true);
        UIFactory.Region(left.rectTransform, Vector2.zero, Vector2.one, 24, 120, 720, 24);
        screen.rarityFilterButton = UIFactory.CreateButton(left.transform, "RarityFilter", "RARITY: ALL", UIFactory.PanelLight, 21);
        UIFactory.TopLeft((RectTransform)screen.rarityFilterButton.transform, 20, 16, 300, 56);
        screen.elementFilterButton = UIFactory.CreateButton(left.transform, "ElementFilter", "ELEMENT: ALL", UIFactory.PanelLight, 21);
        UIFactory.TopLeft((RectTransform)screen.elementFilterButton.transform, 334, 16, 290, 56);
        screen.sortButton = UIFactory.CreateButton(left.transform, "SortButton", "SORT: POWER", UIFactory.PanelLight, 21);
        UIFactory.TopLeft((RectTransform)screen.sortButton.transform, 638, 16, 250, 56);

        var scroll = UIFactory.CreateScroll(left.transform, "GridScroll", true, out RectTransform gridContent);
        UIFactory.Stretch((RectTransform)scroll.transform, 10, 86, 10, 214);
        UIFactory.AddGrid(gridContent, new Vector2(150, 210), new Vector2(18, 34), 18);
        screen.grid = gridContent;

        var slotBar = UIFactory.CreatePanel(left.transform, "SlotBar", new Color(0.03f, 0.02f, 0.08f, 0.85f), false);
        UIFactory.Region(slotBar.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), 10, 0, 10, 10);
        slotBar.rectTransform.sizeDelta = new Vector2(slotBar.rectTransform.sizeDelta.x, 194);
        slotBar.rectTransform.anchoredPosition = new Vector2(slotBar.rectTransform.anchoredPosition.x, 107);
        var slotLabel = UIFactory.CreateText(slotBar.transform, "Label", "BUILD SLOTS\n<size=16><color=#C9C3D8>Choose one on the\nmap before a fight.\nSlotted cards are\nauto-locked.</color></size>", 24, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Place(slotLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(18, 0), new Vector2(170, 170));
        screen.slotRow = UIFactory.CreateRect("SlotRow", slotBar.transform);
        UIFactory.Stretch(screen.slotRow);

        // Right: detail
        var right = UIFactory.CreatePanel(canvas, "DetailPanel", panelColor, true);
        UIFactory.Region(right.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), 0, 120, 24, 24);
        right.rectTransform.sizeDelta = new Vector2(676, right.rectTransform.sizeDelta.y);
        right.rectTransform.anchoredPosition = new Vector2(-362, right.rectTransform.anchoredPosition.y);

        screen.viewHeader = UIFactory.CreateText(right.transform, "ViewHeader", "", 24, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopStrip(screen.viewHeader.rectTransform, 10, 36, 20, 20);

        screen.detailView = CardView.Build(right.transform, "DetailCard");
        var dv = (RectTransform)screen.detailView.transform;
        UIFactory.Place(dv, new Vector2(0f, 1f), new Vector2(22, -54), new Vector2(CardView.Width, CardView.Height));
        dv.localScale = Vector3.one * 0.7f;

        float bx = 22 + CardView.Width * 0.7f + 18;
        float bw = 676 - bx - 20;
        float y = 54;
        float third = (bw - 12) / 3f;
        for (int i = 0; i < 3; i++)
        {
            var b = UIFactory.CreateButton(right.transform, $"SaveSlot{i + 1}", $"SAVE TO S{i + 1}", UIFactory.Accent, 16);
            UIFactory.TopLeft((RectTransform)b.transform, bx + i * (third + 6), y, third, 52);
            screen.saveSlotButtons[i] = b;
        }
        y += 60;
        screen.makeMainButton = UIFactory.CreateButton(right.transform, "MakeMain", "USE THIS SLOT AS MAIN", new Color(0.85f, 0.3f, 0.55f), 18);
        UIFactory.TopLeft((RectTransform)screen.makeMainButton.transform, bx, y, bw, 50); y += 58;
        screen.lockButton = UIFactory.CreateButton(right.transform, "Lock", "LOCK CARD", UIFactory.PanelLight, 18);
        UIFactory.TopLeft((RectTransform)screen.lockButton.transform, bx, y, bw, 50); y += 58;
        screen.rerollStatsButton = UIFactory.CreateButton(right.transform, "RerollStats", "REROLL STATS", new Color(0.85f, 0.55f, 0.15f), 18);
        UIFactory.TopLeft((RectTransform)screen.rerollStatsButton.transform, bx, y, bw, 50); y += 58;
        screen.rerollElementButton = UIFactory.CreateButton(right.transform, "RerollElement", "REROLL ELEMENT", new Color(0.2f, 0.6f, 0.45f), 18);
        UIFactory.TopLeft((RectTransform)screen.rerollElementButton.transform, bx, y, bw, 50); y += 58;
        screen.rerollTalentButton = UIFactory.CreateButton(right.transform, "RerollTalent", "REROLL TALENT", new Color(0.75f, 0.6f, 0.15f), 18);
        UIFactory.TopLeft((RectTransform)screen.rerollTalentButton.transform, bx, y, bw, 50); y += 58;
        screen.fusionButton = UIFactory.CreateButton(right.transform, "Fusion", "FUSION", new Color(0.55f, 0.3f, 0.95f), 20);
        UIFactory.TopLeft((RectTransform)screen.fusionButton.transform, bx, y, bw, 58);

        screen.detailText = UIFactory.CreateText(right.transform, "DetailText", "", 19, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopStrip(screen.detailText.rectTransform, 500, 330, 22, 22);
        UIFactory.BestFit(screen.detailText, 12);

        screen.messageText = UIFactory.CreateText(right.transform, "Message", "", 19, UIFactory.TextMain);
        UIFactory.Place(screen.messageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(640, 64));

        BuildPreview(canvas, screen);
        BuildFusion(canvas, screen);
        return screen;
    }

    static void BuildPreview(RectTransform canvas, CollectionScreen screen)
    {
        var dim = UIFactory.CreateImage(canvas, "RerollPreview", new Color(0, 0, 0, 0.75f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        screen.previewPanel = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.09f, 0.07f, 0.17f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 640));
        screen.previewTitle = UIFactory.CreateText(panel.transform, "Title", "REROLL", 44, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(screen.previewTitle.rectTransform, 20, 60, 20, 20);
        screen.previewOld = UIFactory.CreateText(panel.transform, "Old", "", 24, UIFactory.TextDim, TextAnchor.UpperLeft);
        UIFactory.TopLeft(screen.previewOld.rectTransform, 60, 100, 420, 360);
        screen.previewNew = UIFactory.CreateText(panel.transform, "New", "", 24, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopLeft(screen.previewNew.rectTransform, 530, 100, 420, 360);
        var arrow = UIFactory.CreateText(panel.transform, "Arrow", ">", 60, UIFactory.Accent2, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopLeft(arrow.rectTransform, 470, 220, 60, 80);
        for (int i = 0; i < 3; i++)
        {
            var b = UIFactory.CreateButton(panel.transform, $"Save{i + 1}", $"SAVE TO SLOT {i + 1}", UIFactory.Accent, 22);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0f, 0f), new Vector2(40 + i * 240, 40), new Vector2(225, 76));
            screen.previewSaveButtons[i] = b;
        }
        screen.previewDiscardButton = UIFactory.CreateButton(panel.transform, "Discard", "DISCARD", UIFactory.PanelLight, 22);
        UIFactory.Place((RectTransform)screen.previewDiscardButton.transform, new Vector2(1f, 0f), new Vector2(-40, 40), new Vector2(180, 76));
        dim.gameObject.SetActive(false);
    }

    static void BuildFusion(RectTransform canvas, CollectionScreen screen)
    {
        var dim = UIFactory.CreateImage(canvas, "FusionPanel", new Color(0, 0, 0, 0.78f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        screen.fusionPanel = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.09f, 0.07f, 0.17f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 820));
        var title = UIFactory.CreateText(panel.transform, "Title", "FUSION", 48, new Color(0.8f, 0.6f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(title.rectTransform, 16, 64, 20, 20);
        UIFactory.AddShadow(title, UIFactory.ShadowColor, 3f);

        screen.fusionMainHolder = UIFactory.CreateRect("MainHolder", panel.transform);
        UIFactory.TopLeft(screen.fusionMainHolder, 40, 110, 260, 360);

        var fodderLabel = UIFactory.CreateText(panel.transform, "FodderLabel", "PICK 2 FODDER CARDS (same rarity, not locked)", 20, UIFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(fodderLabel.rectTransform, 330, 90, 700, 30);
        var scroll = UIFactory.CreateScroll(panel.transform, "FodderScroll", true, out RectTransform content);
        UIFactory.TopLeft((RectTransform)scroll.transform, 320, 124, 720, 670);
        UIFactory.AddGrid(content, new Vector2(130, 182), new Vector2(14, 30), 14);
        screen.fodderGrid = content;

        screen.fusionInfo = UIFactory.CreateText(panel.transform, "Info", "", 22, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopLeft(screen.fusionInfo.rectTransform, 1070, 110, 400, 470);
        UIFactory.BestFit(screen.fusionInfo, 12);

        screen.extraMinusButton = UIFactory.CreateButton(panel.transform, "ExtraMinus", "- ORB", UIFactory.PanelLight, 20);
        UIFactory.TopLeft((RectTransform)screen.extraMinusButton.transform, 1070, 590, 190, 56);
        screen.extraPlusButton = UIFactory.CreateButton(panel.transform, "ExtraPlus", "+ ORB (+5%)", UIFactory.PanelLight, 20);
        UIFactory.TopLeft((RectTransform)screen.extraPlusButton.transform, 1270, 590, 200, 56);
        screen.fuseButton = UIFactory.CreateButton(panel.transform, "Fuse", "FUSE", new Color(0.55f, 0.3f, 0.95f), 30);
        UIFactory.TopLeft((RectTransform)screen.fuseButton.transform, 1070, 660, 400, 84);

        screen.fusionResult = UIFactory.CreateText(panel.transform, "Result", "", 24, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopLeft(screen.fusionResult.rectTransform, 20, 490, 300, 200);
        screen.fusionCloseButton = UIFactory.CreateButton(panel.transform, "Close", "CLOSE", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.fusionCloseButton.transform, 60, 720, 220, 64);
        dim.gameObject.SetActive(false);
    }
}
