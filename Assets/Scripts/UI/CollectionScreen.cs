using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Collection": a big showcase of the selected card or slot build, an upgrade panel (stats, element, talent, rerolls,
/// fusion, auto merge, God Orb, dismantle) and the collection grid with dropdown filters and the 3 build slots.
/// Rerolls never pop up: the new roll is shown on the card and stays unsaved until SAVE TO SLOT, so you can keep rolling.
/// </summary>
public class CollectionScreen : MonoBehaviour
{
    static readonly string[] StatNames = { "HP", "ATK", "DEF", "SPD" };

    public Button backButton;
    public Text titleText;

    [Header("Showcase")]
    public Text showcaseHeader;
    public CardView showcaseView;
    public Image showcaseGlow;
    public GameObject newRollRibbon;
    public Button[] saveSlotButtons = new Button[3];
    public Button lockButton;
    public Button makeMainButton;
    public Text pendingText;
    public Button discardButton;

    [Header("Upgrade panel")]
    public Text rarityLine;
    public Text[] statValues = new Text[4];
    public Image[] statFills = new Image[4];
    public Text[] statRolls = new Text[4];
    public TooltipTrigger[] statTips = new TooltipTrigger[4];
    public Image elementIcon;
    public RectTransform strongRow;
    public GameObject elementNew;
    public Image talentIcon;
    public Text talentBonus;
    public GameObject talentNew;
    public Text talentName;
    public Text talentDesc;
    public Image talentIcon2;   // God cards: passive talent
    public Text talentName2;
    public Text talentDesc2;
    public Button rerollStatsButton, rerollElementButton, rerollTalentButton, fusionButton, autoMergeButton, godOrbButton, dismantleButton;
    public Image rerollStatsIcon, rerollElementIcon, rerollTalentIcon, fusionIcon, godOrbIcon, dismantleIcon;
    public Text rerollStatsCost, rerollElementCost, rerollTalentCost, fusionCost, godOrbCost, dismantleCost;
    public Text messageText;

    [Header("Collection")]
    public Dropdown rarityFilter;
    public Dropdown elementFilter;
    public Dropdown sortDropdown;
    public RectTransform grid;
    public Text countText;
    public RectTransform slotRow;

    [Header("Fusion")]
    public GameObject fusionPanel;
    public RectTransform fusionMainHolder;
    public RectTransform fodderGrid;
    public Text fusionInfo;
    public Button fuseButton;
    public Button fusionCloseButton;
    public Text fusionResult;

    [Header("Reroll pop-up")]
    public GameObject rerollPanel;
    public CardView rerollCurrentView;
    public CardView rerollNewView;
    public Text rerollNoRoll;
    public Text rerollCompare;
    public Button rrStatsButton, rrElementButton, rrTalentButton, rrGodOrbButton;
    public Image rrStatsIcon, rrElementIcon, rrTalentIcon, rrGodOrbIcon;
    public Text rrStatsCost, rrElementCost, rrTalentCost, rrGodOrbCost;
    public Button[] rrSaveButtons = new Button[3];
    public Button rrDiscardButton;
    public Button rrCloseButton;
    public Text rrMessage;

    [Header("Unsaved roll dialog")]
    public GameObject unsavedPanel;
    public Text unsavedBody;
    public Button[] unsavedSaveButtons = new Button[3];
    public Button unsavedDiscardButton;
    public Button unsavedCancelButton;

    [Header("Materials")]
    public Button materialsButton;
    public GameObject materialsPanel;
    public RectTransform materialsRow;
    public Text materialsInfo;
    public Button exchangeSelectedButton;
    public Dropdown exchangeTarget;
    public Button exchangeAllButton;
    public Button autoExchangeButton;
    public Dropdown autoExchangeTarget;
    public Text materialsResult;
    public Button materialsCloseButton;

    [Header("Auto merge")]
    public GameObject mergePanel;
    public Dropdown mergeTarget;
    public InputField mergeOrbs;
    public Button mergeOrbs10, mergeOrbs50, mergeOrbsAll;
    public Text mergeInfo;
    public Button mergeStartButton;
    public Button mergeCloseButton;
    public Text mergeResultText;
    public RectTransform mergeResultGrid;

    enum SortMode { Power, Rarity, Newest, Roll }

    int rarityIndex = -1;
    int elementIndex = -1;
    SortMode sortMode = SortMode.Power;

    CardInstance selected;
    BuildData viewBuild;     // saved build being viewed (card's own build or a slot build)
    BuildData pendingBuild;  // unsaved reroll result
    int viewSlot = -1;
    int unlockConfirmSlot = -1;
    float unlockConfirmUntil;
    int dismantleConfirmUid;
    float dismantleConfirmUntil;
    bool mergeConfirm;
    float mergeConfirmUntil;
    bool exchangeConfirm;
    float exchangeConfirmUntil;
    System.Action afterUnsaved;   // what to do once the unsaved roll is saved or refused
    readonly List<CardInstance> fodderSelection = new List<CardInstance>();

    BuildData ShownBuild => pendingBuild ?? viewBuild;

    // ---------------------------------------------------------------- lifecycle

    void Start()
    {
        backButton.onClick.AddListener(() => GuardUnsaved(() => GameSession.LoadScene(SceneNames.MainMenu)));
        FillDropdowns();
        rarityFilter.onValueChanged.AddListener(v => { rarityIndex = v - 1; RebuildGrid(); });
        elementFilter.onValueChanged.AddListener(v => { elementIndex = v - 1; RebuildGrid(); });
        sortDropdown.onValueChanged.AddListener(v => { sortMode = (SortMode)v; RebuildGrid(); });
        for (int i = 0; i < saveSlotButtons.Length; i++)
        {
            int slotIndex = i;
            saveSlotButtons[i].onClick.AddListener(() => SaveShownToSlot(slotIndex));
        }
        lockButton.onClick.AddListener(ToggleLock);
        makeMainButton.onClick.AddListener(MakeMain);
        SetupRename();
        discardButton.onClick.AddListener(() => { if (pendingBuild != null) { pendingBuild = null; Message("New roll discarded."); RefreshDetail(); } });
        rerollStatsButton.onClick.AddListener(() => OpenReroll(0));
        rerollElementButton.onClick.AddListener(() => OpenReroll(1));
        rerollTalentButton.onClick.AddListener(() => OpenReroll(2));
        fusionButton.onClick.AddListener(OpenFusion);
        autoMergeButton.onClick.AddListener(OpenAutoMerge);
        godOrbButton.onClick.AddListener(UseGodOrb);
        dismantleButton.onClick.AddListener(DismantleSelected);
        fuseButton.onClick.AddListener(DoFuse);
        fusionCloseButton.onClick.AddListener(() => { fusionPanel.SetActive(false); RefreshAll(); });
        mergeStartButton.onClick.AddListener(OnMergeStart);
        mergeCloseButton.onClick.AddListener(() => { mergePanel.SetActive(false); RefreshAll(); });
        mergeTarget.onValueChanged.AddListener(_ => { mergeConfirm = false; RefreshMergeInfo(); });
        mergeOrbs.onValueChanged.AddListener(_ => { mergeConfirm = false; RefreshMergeInfo(); });
        mergeOrbs10.onClick.AddListener(() => mergeOrbs.text = "10");
        mergeOrbs50.onClick.AddListener(() => mergeOrbs.text = "50");
        mergeOrbsAll.onClick.AddListener(() => mergeOrbs.text = PlayerProfile.Data.fusionOrbs.ToString());
        fusionPanel.SetActive(false);
        mergePanel.SetActive(false);
        if (materialsPanel != null)
        {
            materialsPanel.SetActive(false);
            materialsButton.onClick.AddListener(OpenMaterials);
            materialsCloseButton.onClick.AddListener(() => { materialsPanel.SetActive(false); RefreshAll(); });
            exchangeSelectedButton.onClick.AddListener(ExchangeSelected);
            exchangeAllButton.onClick.AddListener(ExchangeAll);
            autoExchangeButton.onClick.AddListener(() => { PlayerProfile.SetAutoExchange(!PlayerProfile.Data.autoExchange, autoExchangeTarget.value); RefreshMaterials(); });
            autoExchangeTarget.onValueChanged.AddListener(v => { PlayerProfile.SetAutoExchange(PlayerProfile.Data.autoExchange, v); RefreshMaterials(); });
            exchangeTarget.onValueChanged.AddListener(_ => { exchangeConfirm = false; RefreshMaterials(); });
        }
        messageText.text = "";
        if (rerollPanel != null)
        {
            rerollPanel.SetActive(false);
            rrStatsButton.onClick.AddListener(() => Reroll(0));
            rrElementButton.onClick.AddListener(() => Reroll(1));
            rrTalentButton.onClick.AddListener(() => Reroll(2));
            rrGodOrbButton.onClick.AddListener(UseGodOrb);
            for (int i = 0; i < rrSaveButtons.Length; i++)
            {
                int s = i;
                rrSaveButtons[i].onClick.AddListener(() => { SaveShownToSlot(s); RefreshReroll(); });
            }
            rrDiscardButton.onClick.AddListener(() => { pendingBuild = null; rrMessage.text = "New roll discarded."; RefreshDetail(); RefreshReroll(); });
            rrCloseButton.onClick.AddListener(RequestCloseReroll);
        }
        if (unsavedPanel != null)
        {
            unsavedPanel.SetActive(false);
            for (int i = 0; i < unsavedSaveButtons.Length; i++)
            {
                int s = i;
                unsavedSaveButtons[i].onClick.AddListener(() =>
                {
                    SaveShownToSlot(s);
                    if (pendingBuild == null) ResolveUnsaved(); // saved
                });
            }
            unsavedDiscardButton.onClick.AddListener(RefuseUnsaved);
            unsavedCancelButton.onClick.AddListener(() => { afterUnsaved = null; unsavedPanel.SetActive(false); });
        }

        int active = PlayerProfile.ActiveSlot;
        var slot = PlayerProfile.GetSlot(active);
        if (slot != null && slot.HasCard) ViewSlot(active);
        else if (PlayerProfile.Data.cards.Count > 0) ViewCard(PlayerProfile.Data.cards[0]);
        else RefreshAll();
    }

    void Update()
    {
        if (mergeConfirm && Time.unscaledTime > mergeConfirmUntil) { mergeConfirm = false; RefreshMergeInfo(); }
        if (exchangeConfirm && Time.unscaledTime > exchangeConfirmUntil) { exchangeConfirm = false; RefreshMaterials(); }
        if (showcaseGlow != null && selected != null)
        {
            var c = CardStats.RarityColor(selected.rarity);
            showcaseGlow.color = UIFactory.WithAlpha(c, 0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 2f));
            showcaseGlow.rectTransform.localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * 6f);
        }
    }

    void Message(string text) => messageText.text = text;

    // ---------------------------------------------------------------- rename (click the card's name)

    void SetupRename()
    {
        if (showcaseView == null || showcaseView.nameText == null) return;
        var nt = showcaseView.nameText;
        nt.raycastTarget = true;
        var btn = nt.GetComponent<Button>();
        if (btn == null) btn = nt.gameObject.AddComponent<Button>();
        btn.targetGraphic = nt;
        var c = btn.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(1f, 0.85f, 0.45f);
        c.pressedColor = new Color(0.85f, 0.7f, 0.4f);
        c.selectedColor = Color.white;
        btn.colors = c;
        btn.onClick.AddListener(OpenRename);
        TooltipTrigger.Add(nt, "<b>Rename</b>\nClick to give this card its own name. Each saved copy (the card and every slot) keeps its own name.");
    }

    void OpenRename()
    {
        if (selected == null || viewBuild == null) return;
        string copy = viewSlot >= 0 ? $"the copy in slot {viewSlot + 1}" : $"card #{selected.uid}";
        TextPrompt.Show(transform, "RENAME CARD", $"Only {copy} changes. Other saved copies keep their names.",
                        PlayerProfile.CardName(selected, viewBuild), PlayerProfile.MaxCardNameLength,
                        ApplyRename, "DEFAULT NAME", () => ApplyRename(""));
    }

    void ApplyRename(string text)
    {
        if (selected == null || viewBuild == null) return;
        PlayerProfile.RenameBuild(viewBuild, text);
        if (pendingBuild != null) pendingBuild.name = viewBuild.name; // an unsaved roll keeps the new name when it is saved
        Message(string.IsNullOrEmpty(viewBuild.name) ? $"Name set back to {PlayerProfile.CardName(selected, viewBuild)}." : $"Renamed to {viewBuild.name}.");
        SfxSynth.Play("buff", 0.5f);
        RefreshAll();
    }

    void RefreshAll()
    {
        RebuildGrid();
        RefreshSlots();
        RefreshDetail();
    }

    // ---------------------------------------------------------------- collection grid

    void FillDropdowns()
    {
        var theme = UITheme.Instance;
        rarityFilter.ClearOptions();
        var ro = new List<Dropdown.OptionData> { new Dropdown.OptionData("All rarities") };
        for (int t = 0; t <= GameRules.MaxTier; t++)
            ro.Add(new Dropdown.OptionData($"<color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor((CardRarity)t))}>{CardStats.RarityName((CardRarity)t)}</color>",
                                           theme != null ? theme.Frame((CardRarity)t) : null));
        rarityFilter.AddOptions(ro);
        elementFilter.ClearOptions();
        var eo = new List<Dropdown.OptionData> { new Dropdown.OptionData("All elements") };
        foreach (var e in ElementSystem.All) eo.Add(new Dropdown.OptionData(ElementSystem.DisplayName(e), theme != null ? theme.ElementIcon(e) : null));
        elementFilter.AddOptions(eo);
        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(new List<string> { "Sort: Power", "Sort: Rarity", "Sort: Newest", "Sort: Best Roll" });
        rarityFilter.SetValueWithoutNotify(0);
        elementFilter.SetValueWithoutNotify(0);
        sortDropdown.SetValueWithoutNotify(0);
    }

    IEnumerable<CardInstance> FilteredCards()
    {
        IEnumerable<CardInstance> cards = PlayerProfile.Data.cards;
        if (rarityIndex >= 0) cards = cards.Where(c => c.Tier == rarityIndex);
        if (elementIndex >= 0) cards = cards.Where(c => c.build.element == ElementSystem.All[elementIndex]);
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
        UIFactory.Clear(grid);
        var list = FilteredCards().ToList();
        foreach (var c in list)
        {
            var card = c;
            var tile = CardTile.Build(grid, $"Card {card.uid}", 150, 210, card.Tier >= 6);
            tile.SetInstance(card);
            var slots = Enumerable.Range(0, GameRules.SlotCount).Where(i => PlayerProfile.GetSlot(i).unlocked && PlayerProfile.GetSlot(i).cardUid == card.uid).Select(i => (i + 1).ToString()).ToList();
            if (slots.Count > 0) tile.SetBadge("SLOT " + string.Join(",", slots), UIFactory.Accent);
            tile.button.onClick.AddListener(() => GuardUnsaved(() => ViewCard(card)));
            tile.SetSelected(card == selected && viewSlot < 0);
        }
        var counts = PlayerProfile.Data.cards.GroupBy(c => c.rarity).OrderByDescending(g => g.Key)
            .Select(g => $"<color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(g.Key))}>{g.Count()} {CardStats.RarityShort(g.Key)}</color>");
        countText.text = $"{list.Count} shown / {PlayerProfile.Data.cards.Count} cards    " + string.Join("  ", counts);
        titleText.text = "COLLECTION";
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
            rt.anchoredPosition = new Vector2(250 + i * 150f, -6f);
            tile.nameText.fontSize = 13;
            if (!slot.unlocked)
            {
                tile.SetEmpty($"SLOT {i + 1}");
                tile.SetLocked(true, $"{PlayerProfile.SlotUnlockPrice(i)} DIAMONDS");
            }
            else if (!slot.HasCard) tile.SetEmpty($"SLOT {i + 1}");
            else
            {
                tile.SetInstance(PlayerProfile.FindCard(slot.cardUid), slot.build);
                tile.SetLockIcon(false);
                tile.SetBadge(i == active ? "MAIN" : $"SLOT {i + 1}", i == active ? UIFactory.Accent2 : UIFactory.Accent);
            }
            tile.SetSelected(viewSlot == i);
            tile.button.onClick.AddListener(() => GuardUnsaved(() => OnSlotClicked(index)));
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
                Message($"Click slot {index + 1} again to unlock it for {PlayerProfile.SlotUnlockPrice(index)} diamonds (you have {PlayerProfile.Data.diamonds}).");
            }
            return;
        }
        if (slot.HasCard) ViewSlot(index);
        else Message($"Slot {index + 1} is empty: pick a card and press SAVE TO S{index + 1}.");
    }

    // ---------------------------------------------------------------- selection

    void DropPending()
    {
        if (pendingBuild != null) Message("Unsaved new roll discarded.");
        pendingBuild = null;
    }

    void ViewCard(CardInstance card)
    {
        DropPending();
        selected = card;
        viewBuild = card != null ? card.build : null;
        viewSlot = -1;
        RefreshAll();
    }

    void ViewSlot(int index)
    {
        var slot = PlayerProfile.GetSlot(index);
        var card = slot != null ? PlayerProfile.FindCard(slot.cardUid) : null;
        if (card == null) return;
        DropPending();
        selected = card;
        viewBuild = slot.build;
        viewSlot = index;
        RefreshAll();
    }

    // ---------------------------------------------------------------- detail

    static void SetCost(Image icon, Text text, ItemKind? kind, string value)
    {
        icon.sprite = kind.HasValue ? ItemInfo.Icon(kind.Value) : null;
        icon.enabled = icon.sprite != null;
        text.text = value;
        if (kind.HasValue) TooltipTrigger.Add(icon, ItemInfo.Describe(kind.Value));
    }

    void RefreshDetail()
    {
        bool has = selected != null && viewBuild != null;
        foreach (var b in new[] { lockButton, makeMainButton, rerollStatsButton, rerollElementButton, rerollTalentButton, fusionButton, godOrbButton, dismantleButton })
            b.interactable = has;
        foreach (var b in saveSlotButtons) b.interactable = has;
        showcaseView.gameObject.SetActive(has);
        newRollRibbon.SetActive(has && pendingBuild != null);
        discardButton.gameObject.SetActive(has && pendingBuild != null);
        if (!has)
        {
            showcaseHeader.text = "Pick a card";
            rarityLine.text = "";
            pendingText.text = "";
            return;
        }

        var shown = ShownBuild;
        showcaseHeader.text = viewSlot >= 0
            ? $"SLOT {viewSlot + 1} BUILD{(viewSlot == PlayerProfile.ActiveSlot ? "  <color=#FF6FA8>MAIN</color>" : "")}"
            : $"CARD #{selected.uid}";
        showcaseView.ShowInstance(selected, shown);

        int tier = selected.Tier;
        float mult = GameRules.RarityMultiplier(tier);
        int max = GameRules.MaxDeviation(tier);
        rarityLine.text = $"<color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(selected.rarity))}><b>{CardStats.RarityName(selected.rarity).ToUpperInvariant()}</b></color>   " +
                          $"+{(mult - 1f) * 100f:0}% stats   Roll ±{max}" + (selected.godBonus > 0 ? $"   <color=#FFE9A3>God +{selected.godBonus}%</color>" : "");

        // Stat rows: value, Roll bar (centre = 0), and the change vs the saved build when a new roll is shown
        int[] rolls = { shown.hp, shown.atk, shown.def, shown.spd };
        int[] values = { CardStats.HpOf(selected.rarity, shown, selected.godBonus), CardStats.AtkOf(selected.rarity, shown, selected.godBonus),
                         CardStats.DefOf(selected.rarity, shown, selected.godBonus), CardStats.SpdOf(selected.rarity, shown, selected.godBonus) };
        int[] saved = { CardStats.HpOf(selected.rarity, viewBuild, selected.godBonus), CardStats.AtkOf(selected.rarity, viewBuild, selected.godBonus),
                        CardStats.DefOf(selected.rarity, viewBuild, selected.godBonus), CardStats.SpdOf(selected.rarity, viewBuild, selected.godBonus) };
        for (int i = 0; i < 4; i++)
        {
            int diff = values[i] - saved[i];
            statValues[i].text = $"{values[i]:N0}" + (pendingBuild != null && diff != 0 ? (diff > 0 ? $" <color=#7CFC6A>+{diff}</color>" : $" <color=#FF6B6B>{diff}</color>") : "");
            float k = max > 0 ? Mathf.Clamp(rolls[i] / (float)max, -1f, 1f) : 0f;
            var frt = statFills[i].rectTransform;
            frt.anchorMin = new Vector2(k >= 0 ? 0.5f : 0.5f + 0.5f * k, 0f);
            frt.anchorMax = new Vector2(k >= 0 ? 0.5f + 0.5f * k : 0.5f, 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            statFills[i].color = k >= 0 ? Color.Lerp(new Color(0.4f, 0.8f, 0.4f), new Color(0.45f, 1f, 0.35f), k) : Color.Lerp(new Color(0.9f, 0.5f, 0.4f), new Color(1f, 0.3f, 0.3f), -k);
            statRolls[i].text = rolls[i] > 0 ? $"<color=#7CFC6A>+{rolls[i]}</color>" : rolls[i] < 0 ? $"<color=#FF6B6B>{rolls[i]}</color>" : "0";
            statTips[i].text = $"<b>{StatNames[i]} {values[i]:N0}</b>\n= (100 {(rolls[i] >= 0 ? "+" : "-")} {Mathf.Abs(rolls[i])}) x {mult:0.00}" +
                               (selected.godBonus > 0 ? $" x {1f + GameRules.GodOrbStatBonus * selected.godBonus:0.00}" : "") + (i == 0 ? $" x {GameRules.HpMultiplier}" : "") +
                               $"\nRoll range at this rarity: -{max} to +{max}";
        }

        // Element + what it beats, talent: pictures with tooltips
        var theme = UITheme.Instance;
        elementIcon.sprite = theme != null ? theme.ElementIcon(shown.element) : null;
        elementIcon.enabled = elementIcon.sprite != null;
        TooltipTrigger.Add(elementIcon, IconInfo.Element(shown.element));
        elementNew.SetActive(pendingBuild != null && pendingBuild.element != viewBuild.element);
        UIFactory.Clear(strongRow);
        var strong = ElementSystem.StrongAgainst(shown.element);
        if (strong.Count > 0)
        {
            var arrow = UIFactory.CreateImage(strongRow, "Beats", new Color(0.55f, 1f, 0.45f), theme != null ? theme.arrow : null);
            arrow.preserveAspect = true;
            UIFactory.Place(arrow.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(38, 20));
            TooltipTrigger.Add(arrow, "Deals +25% (or +12.5%) damage to:");
            float size = strong.Count > 5 ? 32f : 40f;
            for (int i = 0; i < strong.Count; i++)
            {
                var icon = UIFactory.Icon(strongRow, "Strong", theme != null ? theme.ElementIcon(strong[i]) : null, ElementSystem.GetColor(strong[i]), size, new Vector2(46 + i * (size + 4), 0));
                TooltipTrigger.Add(icon, IconInfo.Element(strong[i]));
            }
        }
        var db = CardDatabase.Instance;
        var talent = db != null ? db.FindTalent(shown.talent) : null;
        bool god = tier >= GameRules.MaxTier;
        var talent2 = god && db != null ? db.FindTalent(shown.talent2) : null;
        talentIcon.sprite = theme != null ? theme.TalentIcon(talent) : null;
        talentIcon.enabled = talentIcon.sprite != null;
        TooltipTrigger.Add(talentIcon, IconInfo.Talent(talent, selected.rarity));
        talentBonus.text = "";
        ShowTalentInfo(talent, talent2, tier, theme);
        talentNew.SetActive(pendingBuild != null && (pendingBuild.talent != viewBuild.talent || pendingBuild.talent2 != viewBuild.talent2));
        UIFactory.ButtonLabel(rerollTalentButton).text = god ? "REROLL BOTH TALENTS" : "REROLL TALENT";

        // Showcase buttons
        UIFactory.ButtonLabel(lockButton).text = selected.locked ? "UNLOCK" : "LOCK";
        makeMainButton.interactable = viewSlot >= 0 && viewSlot != PlayerProfile.ActiveSlot && pendingBuild == null;
        for (int i = 0; i < saveSlotButtons.Length; i++)
        {
            var slot = PlayerProfile.GetSlot(i);
            saveSlotButtons[i].interactable = slot.unlocked;
            UIFactory.ButtonLabel(saveSlotButtons[i]).text = !slot.unlocked ? $"S{i + 1} LOCKED" : pendingBuild != null ? $"SAVE NEW > S{i + 1}" : $"SAVE TO S{i + 1}";
            saveSlotButtons[i].image.color = pendingBuild != null && slot.unlocked ? UIFactory.Accent2 : UIFactory.Accent;
        }
        pendingText.text = pendingBuild != null
            ? $"<b><color=#FF6FA8>NEW ROLL (not saved)</color></b>   Power {CardStats.Power(selected, viewBuild)} > <b>{CardStats.Power(selected, pendingBuild)}</b>\n" +
              "<color=#C9C3D8>Keep rerolling as long as you like. Press SAVE NEW > S1/S2/S3 to keep this roll, or DISCARD.</color>"
            : "<color=#9AA4B5>Reroll as many times as you want: the new roll shows on the card and nothing changes until you press SAVE TO SLOT.</color>";

        // Upgrade buttons with their cost
        var d = PlayerProfile.Data;
        SetCost(rerollStatsIcon, rerollStatsCost, ItemKind.Gold, $"{PlayerProfile.StatRerollPrice(tier):N0}");
        if (d.elementOrbs > 0) SetCost(rerollElementIcon, rerollElementCost, ItemKind.ElementOrb, $"1 / {d.elementOrbs}");
        else SetCost(rerollElementIcon, rerollElementCost, ItemKind.Gold, $"{PlayerProfile.OrbRerollGoldPrice(OrbType.Element):N0}");
        if (d.talentOrbs > 0) SetCost(rerollTalentIcon, rerollTalentCost, ItemKind.TalentOrb, $"1 / {d.talentOrbs}");
        else SetCost(rerollTalentIcon, rerollTalentCost, ItemKind.Gold, $"{PlayerProfile.OrbRerollGoldPrice(OrbType.Talent):N0}");

        bool godLocked = selected.rarity == CardRarity.Mythic && !PlayerProfile.GodUnlocked;
        UIFactory.ButtonLabel(fusionButton).text = !GameRules.CanFuseFrom(tier) ? "FUSION  <size=16>max rarity</size>"
            : godLocked ? "FUSION  <size=16>GOD locked</size>"
            : $"FUSION  <size=16>{PlayerProfile.FusionRate(tier) * 100f:0}% > {CardStats.RarityName((CardRarity)(tier + 1))}</size>";
        SetCost(fusionIcon, fusionCost, ItemKind.FusionOrb, GameRules.CanFuseFrom(tier) ? $"{GameRules.FusionOrbCost(tier)} / {d.fusionOrbs}" : "-");

        godOrbButton.interactable = PlayerProfile.CanUseGodOrb(selected) == null;
        SetCost(godOrbIcon, godOrbCost, ItemKind.GodOrb, $"1 / {d.godOrbs}");
        UIFactory.ButtonLabel(godOrbButton).text = selected.rarity == CardRarity.God ? "GOD ORB" : "GOD ORB  <size=14>God only</size>";

        bool salvage = PlayerProfile.Has(Buffs.SalvageExpert);
        dismantleButton.interactable = salvage && !PlayerProfile.IsProtected(selected);
        SetCost(dismantleIcon, dismantleCost, ItemKind.Gold, $"+{PlayerProfile.DismantleValue(selected):N0}");
        UIFactory.ButtonLabel(dismantleButton).text = !salvage ? "DISMANTLE  <size=14>buff</size>"
            : dismantleConfirmUid == selected.uid && Time.unscaledTime < dismantleConfirmUntil ? "CONFIRM?" : "DISMANTLE";
    }

    void SaveShownToSlot(int index)
    {
        if (selected == null || ShownBuild == null) return;
        bool wasPending = pendingBuild != null;
        string err = PlayerProfile.SaveToSlot(index, selected, ShownBuild);
        if (err != null) { Message($"<color=#FF6B6B>{err}.</color>"); return; }
        pendingBuild = null;
        SfxSynth.Play("buff", 0.7f);
        if (index >= 0 && index < saveSlotButtons.Length && saveSlotButtons[index] != null)
        {
            var brt = (RectTransform)saveSlotButtons[index].transform;
            UIFx.BurstAt(brt, UIFactory.Good, 0.55f, false);
            UIFx.FloatText(UIFx.Overlay, UIFx.LocalPos(UIFx.Overlay, brt, new Vector2(0f, 40f)), "SAVED!", new Color(0.55f, 1f, 0.6f), 30);
        }
        ViewSlot(index);
        Message($"<color=#7CFC6A>{(wasPending ? "New roll" : "Build")} saved to slot {index + 1}.</color>");
        if (rrMessage != null && rerollPanel != null && rerollPanel.activeSelf)
        {
            rrMessage.text = $"<color=#7CFC6A>Saved to slot {index + 1}! Keep rolling or close.</color>";
            UIFx.BurstAt((RectTransform)rrSaveButtons[index].transform, UIFactory.Good, 0.6f, false);
        }
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
        Message(selected.locked ? "Locked: this card can't be fodder or dismantled." : (PlayerProfile.IsInSlot(selected.uid) ? "Unlocked, but it stays protected while it is in a slot." : "Unlocked."));
        RefreshAll();
    }

    // ---------------------------------------------------------------- rerolls (inline, no pop-up)

    void Reroll(int kind)
    {
        if (selected == null || ShownBuild == null) return;
        BuildData result;
        string err = kind == 0 ? PlayerProfile.RerollStats(selected, ShownBuild, out result)
                   : kind == 1 ? PlayerProfile.RerollElement(selected, ShownBuild, out result)
                   : PlayerProfile.RerollTalent(selected, ShownBuild, out result);
        if (err != null)
        {
            Message($"<color=#FF6B6B>{err}.</color>");
            if (rrMessage != null) rrMessage.text = $"<color=#FF6B6B>{err}.</color>";
            return;
        }
        SfxSynth.Play("skill", 0.6f, 1f + Random.Range(-0.05f, 0.1f));
        pendingBuild = result;
        Message(kind == 0 ? "New stats rolled." : kind == 1 ? "New element rolled." : "New talent rolled.");
        RefreshDetail();
        RefreshReroll();
        if (rrMessage != null) rrMessage.text = kind == 0 ? "New stats rolled." : kind == 1 ? "New element rolled." : "New talent(s) rolled.";
        RerollFx(kind, result);
    }

    /// <summary>Sparkle on the showcase card + a pop on the values that changed.</summary>
    void RerollFx(int kind, BuildData result)
    {
        var color = kind == 0 ? UIFactory.Gold : kind == 1 ? ElementSystem.GetColor(result.element) : new Color(0.75f, 0.45f, 1f);
        var view = rerollPanel != null && rerollPanel.activeSelf ? rerollNewView : showcaseView;
        view.Pulse(0.3f, 0.06f);
        view.Flash(new Color(1f, 1f, 1f, 0.55f), 0.35f);
        var o = UIFx.Overlay;
        var at = UIFx.LocalPos(o, (RectTransform)view.transform);
        UIFx.Ring(o, at, UIFactory.WithAlpha(color, 0.85f), 80f, 520f, 0.45f);
        UIFx.Sparks(o, at, color, 18, 700f, 30f, 0.55f, 500f);
        if (kind == 0) { foreach (var v in statValues) if (v != null) UIFx.PopScale(v.rectTransform, 1.6f, 0.3f); }
        else if (kind == 1 && elementIcon != null) { UIFx.PopScale(elementIcon.rectTransform, 2f, 0.35f); UIFx.Sparks(o, UIFx.LocalPos(o, elementIcon.rectTransform), color, 10, 380f, 22f, 0.45f, 200f); }
        else if (kind == 2 && talentIcon != null) { UIFx.PopScale(talentIcon.rectTransform, 2f, 0.35f); UIFx.Sparks(o, UIFx.LocalPos(o, talentIcon.rectTransform), color, 10, 380f, 22f, 0.45f, 200f); }
        if (newRollRibbon != null && newRollRibbon.activeSelf) UIFx.PopScale((RectTransform)newRollRibbon.transform, 1.5f, 0.3f);
    }

    // ---------------------------------------------------------------- fusion (manual)

    void OpenFusion()
    {
        if (selected == null) return;
        if (pendingBuild != null) { GuardUnsaved(OpenFusion); return; }
        if (!GameRules.CanFuseFrom(selected.Tier)) { Message("God is the maximum rarity."); return; }
        DropPending();
        fodderSelection.Clear();
        int matsFirst = Mathf.Min(GameRules.FusionFodderCount, PlayerProfile.Materials(selected.Tier));
        foreach (var c in PlayerProfile.FodderCandidates(selected).Take(GameRules.FusionFodderCount - matsFirst)) fodderSelection.Add(c);
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
        float rate = PlayerProfile.FusionRate(tier);
        int mats = FusionMaterials;
        string reason = PlayerProfile.CanFuse(selected, fodderSelection, mats);
        var next = (CardRarity)Mathf.Min(GameRules.MaxTier, tier + 1);
        fusionInfo.text =
            $"<b>{CardStats.RarityName(selected.rarity)} > <color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(next))}>{CardStats.RarityName(next)}</color></b>\n\n" +
            $"Success: <size=40><b>{rate * 100f:0}%</b></size>\n" +
            $"Fusion Orbs: {orbCost}  <color=#9AA4B5>(have {PlayerProfile.Data.fusionOrbs})</color>\n" +
            $"Fodder: {fodderSelection.Count + mats}/{GameRules.FusionFodderCount}  <color=#9AA4B5>({candidates.Count} cards, {PlayerProfile.Materials(tier)} {PlayerProfile.MaterialName(tier)}s)</color>\n" +
            (mats > 0 ? $"<color=#7CFC6A>Using {mats} {PlayerProfile.MaterialName(tier)}{(mats > 1 ? "s" : "")} (they fill empty fodder slots)</color>\n\n" : "\n") +
            "<color=#C9C3D8>Success: +1 rarity, Roll/element/talent kept.\nFail: lose the fodder and the orbs; the main card stays.</color>" +
            BuffNotes(tier, orbCost) +
            (reason != null ? $"\n\n<color=#FF6B6B>{reason}</color>" : "");
        fuseButton.interactable = reason == null;
        UIFactory.ButtonLabel(fuseButton).text = $"FUSE  ({rate * 100f:0}%)";
    }

    static string BuffNotes(int tier, int orbCost)
    {
        var buffs = new List<string>();
        if (PlayerProfile.Has(Buffs.FusionFortune)) buffs.Add("Fusion Fortune +3%");
        if (PlayerProfile.Has(Buffs.FusionProtection)) buffs.Add("Protection: a fail costs 1 fodder");
        if (PlayerProfile.Has(Buffs.FusionBlessing) && tier + 2 <= (int)CardRarity.Legendary) buffs.Add("Blessing: 3% for +2 rarities");
        if (PlayerProfile.Has(Buffs.LuckyFusion)) buffs.Add($"Lucky Fusion: {PlayerProfile.LuckyFusionChance(orbCost) * 100f:0}% orb back");
        return buffs.Count > 0 ? $"\n\n<color=#7CFC6A>{string.Join("\n", buffs)}</color>" : "";
    }

    /// <summary>Materials of the main card's rarity fill the fodder slots not taken by picked cards.</summary>
    int FusionMaterials => selected == null ? 0 : Mathf.Clamp(GameRules.FusionFodderCount - fodderSelection.Count, 0, PlayerProfile.Materials(selected.Tier));

    void DoFuse()
    {
        var result = PlayerProfile.Fuse(selected, fodderSelection, FusionMaterials);
        if (result == null) return;
        fodderSelection.Clear();
        string extra = (result.orbRefund ? "\n<color=#7CFC6A>Lucky Fusion: +1 Fusion Orb back!</color>" : "") +
                       (result.fodderSaved > 0 ? $"\n<color=#7CFC6A>Fusion Protection saved {result.fodderSaved} card.</color>" : "");
        FusionFx(result.success, result.blessed, result.to);
        if (result.success)
        {
            SfxSynth.Play("win");
            fusionResult.text = $"<color=#FFD54F><b>{(result.blessed ? "BLESSED!" : "SUCCESS!")}</b> {CardStats.RarityName(result.from)} > {CardStats.RarityName(result.to)}</color>" + extra;
        }
        else
        {
            SfxSynth.Play("lose");
            fusionResult.text = $"<color=#FF6B6B><b>FAILED</b> ({result.rate * 100f:0}%). Lost {result.fodderLost} fodder and {result.orbsSpent} orbs.</color>" + extra;
        }
        int matsNext = Mathf.Min(GameRules.FusionFodderCount, PlayerProfile.Materials(selected.Tier));
        foreach (var c in PlayerProfile.FodderCandidates(selected).Take(GameRules.FusionFodderCount - matsNext)) fodderSelection.Add(c);
        RefreshFusion();
    }

    void FusionFx(bool success, bool blessed, CardRarity to)
    {
        var o = UIFx.Overlay;
        var target = fusionMainHolder != null ? fusionMainHolder : (RectTransform)fusionPanel.transform;
        var at = UIFx.LocalPos(o, target);
        if (success)
        {
            var color = CardStats.RarityColor(to);
            UIFx.ScreenFlash(o, new Color(1f, 0.95f, 0.8f, 0.45f), 0.35f);
            UIFx.Burst(o, at, color, 1.5f);
            UIFx.Burst(o, at, UIFactory.Gold, 0.9f, false);
            UIFx.TextSlam(o, at + new Vector2(0f, 230f), blessed ? "BLESSED!" : "SUCCESS!", UIFactory.Gold, 96);
            UIFx.Confetti(o, GameRules.Tier(to) >= 5 ? 90 : 45);
            UIFx.PopScale(target, 1.25f, 0.4f);
            SfxSynth.Play("sparkle", 0.8f);
        }
        else
        {
            UIFx.Puff(o, at, new Color(0.55f, 0.55f, 0.65f), 1.6f);
            UIFx.Shake(target, 0.4f, 14f);
            UIFx.TextSlam(o, at + new Vector2(0f, 230f), "FAILED", UIFactory.Bad, 90, 0.9f, 4f);
            SfxSynth.Play("fail", 0.8f);
        }
    }

    // ---------------------------------------------------------------- reroll pop-up (focus on rolling)

    void OpenReroll(int kind)
    {
        if (selected == null || rerollPanel == null) { Reroll(kind); return; }
        rrMessage.text = "";
        rerollPanel.SetActive(true);
        rerollPanel.transform.SetAsLastSibling();
        if (unsavedPanel != null) unsavedPanel.transform.SetAsLastSibling();
        UIFx.PopIn((RectTransform)rerollPanel.transform.GetChild(0), 0f, 0.25f, 0.85f);
        RefreshReroll();
        Reroll(kind);
    }

    void RequestCloseReroll() => GuardUnsaved(CloseReroll);

    void RefuseUnsaved()
    {
        pendingBuild = null;
        Message("New roll discarded.");
        ResolveUnsaved();
    }

    void CloseReroll()
    {
        if (rerollPanel != null) rerollPanel.SetActive(false);
        RefreshAll();
    }

    void RefreshReroll()
    {
        if (rerollPanel == null || !rerollPanel.activeSelf) return;
        if (selected == null || viewBuild == null) { rerollPanel.SetActive(false); return; }
        bool has = pendingBuild != null;
        rerollCurrentView.ShowInstance(selected, viewBuild);
        rerollNewView.gameObject.SetActive(has);
        rerollNoRoll.gameObject.SetActive(!has);
        if (has) rerollNewView.ShowInstance(selected, pendingBuild);
        rerollCompare.text = CompareText(viewBuild, pendingBuild);

        var d = PlayerProfile.Data;
        int tier = selected.Tier;
        bool god = tier >= GameRules.MaxTier;
        SetCost(rrStatsIcon, rrStatsCost, ItemKind.Gold, $"{PlayerProfile.StatRerollPrice(tier):N0}");
        if (d.elementOrbs > 0) SetCost(rrElementIcon, rrElementCost, ItemKind.ElementOrb, $"1 / {d.elementOrbs}");
        else SetCost(rrElementIcon, rrElementCost, ItemKind.Gold, $"{PlayerProfile.OrbRerollGoldPrice(OrbType.Element):N0}");
        if (d.talentOrbs > 0) SetCost(rrTalentIcon, rrTalentCost, ItemKind.TalentOrb, $"1 / {d.talentOrbs}");
        else SetCost(rrTalentIcon, rrTalentCost, ItemKind.Gold, $"{PlayerProfile.OrbRerollGoldPrice(OrbType.Talent):N0}");
        UIFactory.ButtonLabel(rrTalentButton).text = god ? "REROLL BOTH TALENTS" : "REROLL TALENT";
        rrGodOrbButton.interactable = PlayerProfile.CanUseGodOrb(selected) == null;
        SetCost(rrGodOrbIcon, rrGodOrbCost, ItemKind.GodOrb, $"1 / {d.godOrbs}");
        UIFactory.ButtonLabel(rrGodOrbButton).text = god ? "GOD ORB" : "GOD ORB  <size=14>God only</size>";

        for (int i = 0; i < rrSaveButtons.Length; i++)
        {
            var slot = PlayerProfile.GetSlot(i);
            rrSaveButtons[i].interactable = has && slot.unlocked;
            UIFactory.ButtonLabel(rrSaveButtons[i]).text = !slot.unlocked ? $"S{i + 1} LOCKED" : $"SAVE TO S{i + 1}" + (i == PlayerProfile.ActiveSlot ? " <size=14>MAIN</size>" : "");
        }
        rrDiscardButton.interactable = has;
    }

    string CompareText(BuildData from, BuildData to)
    {
        var db = CardDatabase.Instance;
        var r = selected.rarity;
        int g = selected.godBonus;
        string Diff(int a, int b) => b > a ? $"<color=#7CFC6A>+{b - a:N0}</color>" : b < a ? $"<color=#FF6B6B>{b - a:N0}</color>" : "<color=#9AA4B5>=</color>";
        string Roll(int v) => v > 0 ? $"<color=#7CFC6A>+{v}</color>" : v < 0 ? $"<color=#FF6B6B>{v}</color>" : "0";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b><color=#FFD54F>{(to != null ? "CURRENT  >  NEW ROLL" : "CURRENT BUILD")}</color></b>   <color=#9AA4B5>Roll range ±{GameRules.MaxDeviation(selected.Tier)}</color>");
        string[] names = { "HP", "ATK", "DEF", "SPD" };
        int[] a = { CardStats.HpOf(r, from, g), CardStats.AtkOf(r, from, g), CardStats.DefOf(r, from, g), CardStats.SpdOf(r, from, g) };
        int[] ra = { from.hp, from.atk, from.def, from.spd };
        for (int i = 0; i < 4; i++)
        {
            if (to == null) { sb.AppendLine($"<color=#9AA4B5>{names[i]}</color>  <b>{a[i]:N0}</b>  <size=17>Roll {Roll(ra[i])}</size>"); continue; }
            int[] b = { CardStats.HpOf(r, to, g), CardStats.AtkOf(r, to, g), CardStats.DefOf(r, to, g), CardStats.SpdOf(r, to, g) };
            int[] rb = { to.hp, to.atk, to.def, to.spd };
            sb.AppendLine($"<color=#9AA4B5>{names[i]}</color>  {a[i]:N0}  >  <b>{b[i]:N0}</b>  {Diff(a[i], b[i])}   <size=17>Roll {Roll(ra[i])} > {Roll(rb[i])}</size>");
        }
        sb.AppendLine($"<color=#9AA4B5>POWER</color>  {CardStats.Power(selected, from)}" + (to != null ? $"  >  <b>{CardStats.Power(selected, to)}</b>  {Diff(CardStats.Power(selected, from), CardStats.Power(selected, to))}" : ""));
        string El(CardElement e) => $"<color=#{ColorUtility.ToHtmlStringRGB(ElementSystem.GetColor(e))}>{ElementSystem.DisplayName(e)}</color>";
        sb.AppendLine();
        sb.AppendLine($"<color=#9AA4B5>ELEMENT</color>  {El(from.element)}" + (to != null && to.element != from.element ? $"  >  <b>{El(to.element)}</b>  <color=#FF6FA8>NEW</color>" : ""));
        var showT = to ?? from;
        var t1 = db != null ? db.FindTalent(showT.talent) : null;
        var t2 = db != null && selected.Tier >= GameRules.MaxTier ? db.FindTalent(showT.talent2) : null;
        bool talentChanged = to != null && (to.talent != from.talent || to.talent2 != from.talent2);
        sb.AppendLine($"<color=#9AA4B5>TALENT</color>  " + (talentChanged ? $"{from.talent}{(string.IsNullOrEmpty(from.talent2) ? "" : " + " + from.talent2)}  >  " : "") +
                      $"<b>{t1?.DisplayName ?? "-"}{(t2 != null ? " + " + t2.DisplayName : "")}</b>" + (talentChanged ? "  <color=#FF6FA8>NEW</color>" : ""));
        if (t1 != null) sb.AppendLine($"<size=16><color=#C9CEDA>{t1.DisplayName}: {t1.DescriptionFor(selected.rarity)}</color></size>");
        if (t2 != null) sb.AppendLine($"<size=16><color=#C9CEDA>{t2.DisplayName}: {t2.DescriptionFor(selected.rarity)}</color></size>");
        return sb.ToString();
    }

    // ---------------------------------------------------------------- unsaved roll dialog

    /// <summary>Runs <paramref name="action"/> now, or first asks to save / refuse the unsaved new roll.</summary>
    void GuardUnsaved(System.Action action)
    {
        if (pendingBuild == null || unsavedPanel == null) { action?.Invoke(); return; }
        afterUnsaved = action;
        unsavedPanel.SetActive(true);
        unsavedPanel.transform.SetAsLastSibling();
        UIFx.PopIn((RectTransform)unsavedPanel.transform.GetChild(0), 0f, 0.22f, 0.8f);
        SfxSynth.Play("miss", 0.5f);
        unsavedBody.text = $"Your new roll for this {CardStats.RarityName(selected.rarity)} card is not saved.\n" +
                           $"Power {CardStats.Power(selected, viewBuild)} > <b>{CardStats.Power(selected, pendingBuild)}</b>   " +
                           $"Element {ElementSystem.DisplayName(pendingBuild.element)}   Talent {pendingBuild.talent}{(string.IsNullOrEmpty(pendingBuild.talent2) ? "" : " + " + pendingBuild.talent2)}\n" +
                           "<color=#C9C3D8>Save it to a slot, or refuse it (the roll is lost).</color>";
        for (int i = 0; i < unsavedSaveButtons.Length; i++)
        {
            var slot = PlayerProfile.GetSlot(i);
            unsavedSaveButtons[i].interactable = slot.unlocked;
            UIFactory.ButtonLabel(unsavedSaveButtons[i]).text = slot.unlocked ? $"SAVE TO S{i + 1}" + (i == PlayerProfile.ActiveSlot ? " <size=14>MAIN</size>" : "") : $"S{i + 1} LOCKED";
        }
    }

    void ResolveUnsaved()
    {
        unsavedPanel.SetActive(false);
        var next = afterUnsaved;
        afterUnsaved = null;
        RefreshDetail();
        RefreshReroll();
        next?.Invoke();
    }

    // ---------------------------------------------------------------- talent info

    void ShowTalentInfo(Talent t1, Talent t2, int tier, UITheme theme)
    {
        if (talentName == null) return;
        int bonus = Mathf.RoundToInt((GameRules.TalentPower(tier) - 1f) * 100f);
        bool two = t2 != null;
        // Layout: one big block, or two compact blocks for God cards (active + passive)
        Place(talentIcon.rectTransform, 20, two ? 352 : 358, two ? 48 : 56);
        UIFactory.TopLeft(talentName.rectTransform, two ? 76 : 88, two ? 350 : 352, two ? 424 : 412, two ? 26 : 30);
        UIFactory.TopLeft(talentDesc.rectTransform, two ? 76 : 88, two ? 375 : 382, two ? 424 : 412, two ? 34 : 84);
        talentName.fontSize = two ? 18 : 20;
        talentDesc.fontSize = two ? 14 : 16;
        talentDesc.resizeTextMaxSize = talentDesc.fontSize;
        talentName.text = TalentHeader(t1, bonus);
        float power = GameRules.TalentPower(tier); // the card's own numbers at its rarity
        talentDesc.text = t1 != null ? t1.DescriptionFor(power) : "<color=#9AA4B5>No talent</color>";
        talentIcon2.gameObject.SetActive(two);
        talentName2.gameObject.SetActive(two);
        talentDesc2.gameObject.SetActive(two);
        if (!two) return;
        talentIcon2.sprite = theme != null ? theme.TalentIcon(t2) : null;
        talentIcon2.enabled = talentIcon2.sprite != null;
        TooltipTrigger.Add(talentIcon2, IconInfo.Talent(t2, selected.rarity));
        talentName2.text = TalentHeader(t2, bonus);
        talentDesc2.text = t2.DescriptionFor(power);
    }

    static void Place(RectTransform rt, float x, float y, float size) => UIFactory.TopLeft(rt, x, y, size, size);

    static string TalentHeader(Talent t, int bonus)
    {
        if (t == null) return "";
        string type = t.IsActive ? "<color=#6FD3FF>ACTIVE</color>" : "<color=#FFB23D>PASSIVE</color>";
        return $"<b>{t.DisplayName}</b>  <size=15>{type} <color=#9AA4B5>· {TalentCatalog.Name(t.Category)}</color>" +
               (bonus > 0 ? $"  <color=#7CFC6A>+{bonus}% power</color>" : "") + "</size>";
    }

    // ---------------------------------------------------------------- materials (card exchange)

    void OpenMaterials()
    {
        if (pendingBuild != null) { GuardUnsaved(OpenMaterials); return; }
        exchangeConfirm = false;
        materialsResult.text = "";
        var opts = new List<Dropdown.OptionData>();
        var theme = UITheme.Instance;
        for (int t = 0; t < GameRules.MaxTier; t++)
            opts.Add(new Dropdown.OptionData($"<color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor((CardRarity)t))}>{CardStats.RarityName((CardRarity)t)}</color>", theme != null ? theme.Frame((CardRarity)t) : null));
        exchangeTarget.ClearOptions();
        exchangeTarget.AddOptions(opts);
        exchangeTarget.SetValueWithoutNotify(Mathf.Clamp(PlayerProfile.Data.autoExchangeMaxTier, 0, opts.Count - 1));
        autoExchangeTarget.ClearOptions();
        autoExchangeTarget.AddOptions(opts);
        autoExchangeTarget.SetValueWithoutNotify(Mathf.Clamp(PlayerProfile.Data.autoExchangeMaxTier, 0, opts.Count - 1));
        materialsPanel.SetActive(true);
        RefreshMaterials();
    }

    void RefreshMaterials()
    {
        if (materialsPanel == null || !materialsPanel.activeSelf) return;
        UIFactory.Clear(materialsRow);
        var theme = UITheme.Instance;
        const float w = 112f, gap = 10f;
        int n = GameRules.MaxTier + 1;
        float total = n * w + (n - 1) * gap;
        for (int t = 0; t < n; t++)
        {
            var color = CardStats.RarityColor((CardRarity)t);
            var chip = UIFactory.CreatePanel(materialsRow, $"Mat {t}", new Color(0.06f, 0.05f, 0.12f, 0.98f), true);
            UIFactory.Place(chip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-total / 2f + w / 2f + t * (w + gap), 0f), new Vector2(w, 132));
            UIFactory.AddOutline(chip, UIFactory.WithAlpha(color, 0.8f), 2f);
            var icon = UIFactory.CreateImage(chip.transform, "Icon", color, theme != null ? theme.diamondIcon : null);
            icon.preserveAspect = true;
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(50, 50));
            var count = UIFactory.CreateText(chip.transform, "Count", PlayerProfile.Materials(t).ToString("N0"), 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(count.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 34), new Vector2(w, 30));
            var name = UIFactory.CreateText(chip.transform, "Name", CardStats.RarityShort((CardRarity)t), 16, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(w, 24));
            TooltipTrigger.Add(chip, $"<b>{PlayerProfile.MaterialName(t)}</b>: {PlayerProfile.Materials(t)}\nCounts as one {CardStats.RarityName((CardRarity)t)} fodder card in fusion and auto merge.");
        }

        int max = exchangeTarget.value;
        var list = PlayerProfile.ExchangeableCards(max);
        string err = PlayerProfile.CanExchange(selected);
        exchangeSelectedButton.interactable = err == null;
        UIFactory.ButtonLabel(exchangeSelectedButton).text = selected == null ? "EXCHANGE SELECTED CARD" :
            err == null ? $"EXCHANGE SELECTED CARD  <size=17>(+1 {PlayerProfile.MaterialName(selected.Tier)})</size>" : $"SELECTED CARD: <size=17>{err}</size>";
        exchangeAllButton.interactable = list.Count > 0;
        UIFactory.ButtonLabel(exchangeAllButton).text = exchangeConfirm ? $"CONFIRM - EXCHANGE {list.Count} CARDS" : $"EXCHANGE ALL UP TO THIS RARITY  <size=17>({list.Count} cards)</size>";
        exchangeAllButton.image.color = exchangeConfirm ? UIFactory.Accent2 : new Color(0.55f, 0.3f, 0.95f);
        bool on = PlayerProfile.Data.autoExchange;
        UIFactory.ButtonLabel(autoExchangeButton).text = on ? "AUTO EXCHANGE: ON" : "AUTO EXCHANGE: OFF";
        autoExchangeButton.image.color = on ? new Color(0.2f, 0.6f, 0.35f) : UIFactory.PanelLight;
        materialsInfo.text =
            "1 card = 1 material of its rarity. Materials take no space in your collection and count as fodder cards of their rarity: " +
            "fusion and auto merge use them first, before cards.\n" +
            "<color=#9AA4B5>Locked and slotted cards are never exchanged.</color>";
    }

    void ExchangeSelected()
    {
        if (selected == null) return;
        var card = selected;
        string err = PlayerProfile.ExchangeCard(card);
        if (err != null) { materialsResult.text = $"<color=#FF6B6B>{err}.</color>"; return; }
        materialsResult.text = $"<color=#7CFC6A>+1 {PlayerProfile.MaterialName(card.Tier)}</color>";
        SfxSynth.Play("coin", 0.7f);
        UIFx.BurstAt((RectTransform)exchangeSelectedButton.transform, CardStats.RarityColor(card.rarity), 0.7f, false);
        selected = null; viewBuild = null; pendingBuild = null;
        var slot = PlayerProfile.GetSlot(PlayerProfile.ActiveSlot);
        if (slot != null && slot.HasCard) ViewSlot(PlayerProfile.ActiveSlot); else RefreshAll();
        RefreshMaterials();
    }

    void ExchangeAll()
    {
        if (!exchangeConfirm)
        {
            exchangeConfirm = true;
            exchangeConfirmUntil = Time.unscaledTime + 5f;
            RefreshMaterials();
            return;
        }
        exchangeConfirm = false;
        var gained = PlayerProfile.ExchangeAll(exchangeTarget.value);
        var parts = new List<string>();
        for (int t = 0; t < gained.Length; t++)
            if (gained[t] > 0) parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor((CardRarity)t))}>+{gained[t]} {PlayerProfile.MaterialName(t)}</color>");
        materialsResult.text = parts.Count > 0 ? string.Join("   ", parts) : "Nothing to exchange.";
        if (parts.Count > 0)
        {
            SfxSynth.Play("sparkle", 0.7f);
            UIFx.BurstAt(materialsRow, UIFactory.Gold, 1.1f, false);
        }
        if (selected != null && PlayerProfile.FindCard(selected.uid) == null) { selected = null; viewBuild = null; pendingBuild = null; }
        var slot = PlayerProfile.GetSlot(PlayerProfile.ActiveSlot);
        if (selected == null && slot != null && slot.HasCard) ViewSlot(PlayerProfile.ActiveSlot); else RefreshAll();
        RefreshMaterials();
    }

    // ---------------------------------------------------------------- auto merge

    void OpenAutoMerge()
    {
        if (pendingBuild != null) { GuardUnsaved(OpenAutoMerge); return; }
        var theme = UITheme.Instance;
        mergeTarget.ClearOptions();
        var opts = new List<Dropdown.OptionData>();
        int top = PlayerProfile.GodUnlocked ? GameRules.MaxTier : GameRules.MaxTier - 1;
        for (int t = 1; t <= top; t++)
            opts.Add(new Dropdown.OptionData($"<color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor((CardRarity)t))}>{CardStats.RarityName((CardRarity)t)}</color>", theme != null ? theme.Frame((CardRarity)t) : null));
        mergeTarget.AddOptions(opts);
        mergeTarget.SetValueWithoutNotify(Mathf.Clamp(2 - 1, 0, opts.Count - 1)); // Rare
        mergeOrbs.SetTextWithoutNotify(Mathf.Min(10, PlayerProfile.Data.fusionOrbs).ToString());
        mergeConfirm = false;
        mergeResultText.text = "";
        UIFactory.Clear(mergeResultGrid);
        mergePanel.SetActive(true);
        RefreshMergeInfo();
    }

    CardRarity MergeTargetRarity => (CardRarity)(mergeTarget.value + 1);
    int MergeOrbBudget => int.TryParse(mergeOrbs.text, out int n) ? Mathf.Max(0, n) : 0;

    void RefreshMergeInfo()
    {
        var target = MergeTargetRarity;
        int budget = MergeOrbBudget;
        var est = PlayerProfile.AutoMergeEstimate(target, budget);
        var counts = new List<string>();
        for (int t = 0; t < (int)target; t++)
        {
            int free = PlayerProfile.Data.cards.Count(c => c.Tier == t && !PlayerProfile.IsProtected(c));
            int guarded = PlayerProfile.Data.cards.Count(c => c.Tier == t && PlayerProfile.IsProtected(c));
            if (free + guarded > 0)
                counts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor((CardRarity)t))}>{CardStats.RarityName((CardRarity)t)}</color> {free}" + (guarded > 0 ? $" <color=#9AA4B5>(+{guarded} locked)</color>" : ""));
        }
        mergeInfo.text =
            $"Merge every card up to <b><color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(target))}>{CardStats.RarityName(target)}</color></b>, lowest rarity first.\n" +
            $"Cards: {(counts.Count > 0 ? string.Join("   ", counts) : "none below that rarity")}\n" +
            $"Fusion Orbs: you have <b>{PlayerProfile.Data.fusionOrbs}</b>, budget <b>{budget}</b>\n" +
            $"<color=#FFD54F>Up to {est.fusions} fusions, up to {est.orbs} Fusion Orbs and {est.cards} fodder cards (if every fusion succeeds).</color>\n" +
            "<color=#9AA4B5>Locked and slotted cards are never used as fodder (they can be upgraded as the main card).</color>" +
            BuffNotes(0, 1).Replace("\n\n", "\n");
        mergeStartButton.interactable = est.fusions > 0;
        UIFactory.ButtonLabel(mergeStartButton).text = mergeConfirm ? $"CONFIRM - SPEND UP TO {est.orbs} ORBS" : "START AUTO MERGE";
        mergeStartButton.image.color = mergeConfirm ? UIFactory.Accent2 : new Color(0.55f, 0.3f, 0.95f);
    }

    void OnMergeStart()
    {
        if (!mergeConfirm)
        {
            mergeConfirm = true;
            mergeConfirmUntil = Time.unscaledTime + 6f;
            var est = PlayerProfile.AutoMergeEstimate(MergeTargetRarity, MergeOrbBudget);
            mergeResultText.text = $"<color=#FFD54F>Price check: this can spend up to <b>{est.orbs} Fusion Orbs</b> (worth {est.orbs * GameRules.FusionOrbPrice:N0} gold) and use up to {est.cards} cards. Click CONFIRM to continue.</color>";
            RefreshMergeInfo();
            return;
        }
        mergeConfirm = false;
        int before = PlayerProfile.Data.cards.Count;
        var r = PlayerProfile.AutoMerge(MergeTargetRarity, MergeOrbBudget);
        SfxSynth.Play(r.successes > 0 ? "win" : "lose", 0.8f);
        mergeResultText.text =
            $"<b>AUTO MERGE DONE</b>  <color=#9AA4B5>({r.stopReason})</color>\n" +
            $"Fusions: <b>{r.attempts}</b>   <color=#7CFC6A>Success {r.successes}</color>   <color=#FF6B6B>Failed {r.fails}</color>" + (r.blessed > 0 ? $"   <color=#FFD54F>Blessed {r.blessed}</color>" : "") + "\n" +
            $"Fusion Orbs spent: {r.orbsSpent}" + (r.orbsRefunded > 0 ? $"   <color=#7CFC6A>saved {r.orbsRefunded} (Lucky Fusion)</color>" : "") + "\n" +
            $"Cards used: {r.fodderUsed}" + (r.fodderSaved > 0 ? $"   <color=#7CFC6A>saved {r.fodderSaved} (Fusion Protection)</color>" : "") +
            $"   Cards: {before} > {PlayerProfile.Data.cards.Count}\n" +
            (r.upgraded.Count > 0 ? "Cards you obtained:" : "<color=#9AA4B5>No card ranked up this time.</color>");
        UIFactory.Clear(mergeResultGrid);
        foreach (var kv in r.upgraded.OrderByDescending(k => (int)k.Value.to))
        {
            var card = PlayerProfile.FindCard(kv.Key);
            if (card == null) continue;
            var tile = CardTile.Build(mergeResultGrid, $"Up {card.uid}", 120, 168, card.Tier >= 6);
            tile.SetInstance(card);
            tile.SetBadge($"{CardStats.RarityShort(kv.Value.from)} > {CardStats.RarityShort(kv.Value.to)}", new Color(0.85f, 0.55f, 0.15f));
            tile.button.interactable = false;
            UIFx.PopIn((RectTransform)tile.transform, Mathf.Min(0.8f, 0.06f * mergeResultGrid.childCount), 0.3f);
        }
        if (r.successes > 0)
        {
            var o = UIFx.Overlay;
            var at = UIFx.LocalPos(o, mergeResultGrid);
            UIFx.Burst(o, at, UIFactory.Gold, 1.1f, false);
            UIFx.TextSlam(o, at + new Vector2(0f, 140f), $"{r.successes} UPGRADE{(r.successes > 1 ? "S" : "")}!", UIFactory.Gold, 70);
            SfxSynth.Play("sparkle", 0.7f);
        }
        else if (r.attempts > 0) UIFx.Shake(mergeResultGrid, 0.3f, 10f);
        if (selected != null && PlayerProfile.FindCard(selected.uid) == null) { selected = null; viewBuild = null; }
        RefreshMergeInfo();
    }

    // ---------------------------------------------------------------- God Orb + dismantle

    void UseGodOrb()
    {
        if (selected == null) return;
        string err = PlayerProfile.UseGodOrb(selected, out bool success);
        if (err != null) { Message($"<color=#FF6B6B>{err}.</color>"); return; }
        SfxSynth.Play(success ? "win" : "fail", 0.8f);
        var o = UIFx.Overlay;
        var srt = (RectTransform)showcaseView.transform;
        var at = UIFx.LocalPos(o, srt);
        if (success)
        {
            UIFx.ScreenFlash(o, new Color(1f, 0.9f, 0.55f, 0.5f), 0.4f);
            UIFx.Burst(o, at, new Color(1f, 0.88f, 0.45f), 1.7f);
            UIFx.TextSlam(o, at + new Vector2(0f, 300f), $"GOD ORB +{selected.godBonus}%!", new Color(1f, 0.9f, 0.5f), 80);
            showcaseView.Flash(new Color(1f, 0.9f, 0.5f, 0.7f), 0.6f);
            showcaseView.Pulse(0.5f, 0.08f);
        }
        else
        {
            UIFx.Puff(o, at, new Color(0.7f, 0.65f, 0.5f), 1.4f);
            showcaseView.Shake(0.4f, 12f);
        }
        if (rrMessage != null) rrMessage.text = success ? $"<color=#FFE9A3>GOD ORB WORKED! +{selected.godBonus}% to every stat.</color>" : "<color=#FF6B6B>The God Orb faded... no effect this time.</color>";
        Message(success ? $"<color=#FFE9A3>GOD ORB WORKED! +{selected.godBonus}% to every stat.</color>" : "<color=#FF6B6B>The God Orb faded... no effect this time.</color>");
        RefreshAll();
        RefreshReroll();
    }

    void DismantleSelected()
    {
        if (selected == null) return;
        string err = PlayerProfile.CanDismantle(selected);
        if (err != null) { Message($"<color=#FF6B6B>{err}.</color>"); return; }
        if (dismantleConfirmUid != selected.uid || Time.unscaledTime > dismantleConfirmUntil)
        {
            dismantleConfirmUid = selected.uid;
            dismantleConfirmUntil = Time.unscaledTime + 3f;
            Message($"Click DISMANTLE again to turn this {CardStats.RarityName(selected.rarity)} card into {PlayerProfile.DismantleValue(selected):N0} gold.");
            RefreshDetail();
            return;
        }
        dismantleConfirmUid = 0;
        err = PlayerProfile.Dismantle(selected, out int gold);
        if (err != null) { Message($"<color=#FF6B6B>{err}.</color>"); return; }
        SfxSynth.Play("buff", 0.8f);
        selected = null;
        viewBuild = null;
        pendingBuild = null;
        var slot = PlayerProfile.GetSlot(PlayerProfile.ActiveSlot);
        if (slot != null && slot.HasCard) ViewSlot(PlayerProfile.ActiveSlot);
        else RefreshAll();
        Message($"<color=#7CFC6A>Dismantled for +{gold:N0} gold.</color>");
        var ov = UIFx.Overlay;
        var pp = UIFx.PointerPos(ov);
        UIFx.Sparks(ov, pp, UIFactory.Gold, 16, 520f, 30f, 0.6f, 900f, UIFactory.Theme != null ? UIFactory.Theme.goldIcon : null);
        UIFx.FloatText(ov, pp + new Vector2(0f, 30f), $"+{gold:N0} gold", UIFactory.Gold, 34);
        SfxSynth.Play("coin", 0.7f);
    }

    // ================================================================ layout

    static Button ActionButton(Transform parent, string name, string label, Color color, float x, float y, float w, float h, out Image costIcon, out Text costText)
    {
        var b = UIFactory.CreateButton(parent, name, label, color, 20);
        UIFactory.TopLeft((RectTransform)b.transform, x, y, w, h);
        var lbl = UIFactory.ButtonLabel(b);
        lbl.alignment = TextAnchor.MiddleLeft;
        UIFactory.Stretch(lbl.rectTransform, 16, 4, w > 300 ? 150 : 104, 4);
        var chip = UIFactory.CreateChip(b.transform, "Cost", new Color(0f, 0f, 0f, 0.38f));
        float cw = w > 300 ? 140f : 96f;
        UIFactory.Place(chip.rectTransform, new Vector2(1f, 0.5f), new Vector2(-7, 0), new Vector2(cw, h - 14));
        costIcon = UIFactory.Icon(chip.transform, "Icon", null, Color.clear, h - 24, new Vector2(5, 0));
        costText = UIFactory.CreateText(chip.transform, "Value", "", 18, UIFactory.TextMain, TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.Stretch(costText.rectTransform, h - 16, 0, 8, 0);
        UIFactory.BestFit(costText, 10);
        return b;
    }

    static Text Section(Transform parent, string text, float y, float width)
    {
        var t = UIFactory.CreateText(parent, "Section " + text, text, 22, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(t.rectTransform, 20, y, width - 40, 30);
        UIFactory.AddShadow(t, UIFactory.ShadowColor, 2f);
        var line = UIFactory.CreateImage(parent, "Line", UIFactory.WithAlpha(UIFactory.Gold, 0.35f));
        UIFactory.TopLeft(line.rectTransform, 20, y + 30, width - 40, 2);
        return t;
    }

    static GameObject NewBadge(Transform parent, Vector2 pos)
    {
        var chip = UIFactory.CreateChip(parent, "New", UIFactory.Accent2);
        UIFactory.TopLeft(chip.rectTransform, pos.x, pos.y, 60, 26);
        var t = UIFactory.CreateText(chip.transform, "Text", "NEW", 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(t.rectTransform);
        chip.gameObject.SetActive(false);
        return chip.gameObject;
    }

    public static CollectionScreen BuildLayout(RectTransform canvas)
    {
        var screen = canvas.gameObject.AddComponent<CollectionScreen>();
        var theme = UIFactory.Theme;
        AnimeBackground.Build(canvas, "cards", new Color(0.95f, 0.4f, 0.7f, 0.7f), new Color(0.1f, 0.06f, 0.18f), out _);
        UIFactory.CreateTopBarWithCurrency(canvas, "COLLECTION", out screen.backButton, out screen.titleText);
        var panelColor = new Color(0.06f, 0.05f, 0.12f, 0.95f);

        // ---------- showcase (left)
        var show = UIFactory.CreatePanel(canvas, "Showcase", panelColor, true);
        UIFactory.TopLeft(show.rectTransform, 24, 112, 560, 944);
        show.rectTransform.anchorMin = show.rectTransform.anchorMax = new Vector2(0f, 1f);
        UIFactory.AddOutline(show, UIFactory.WithAlpha(UIFactory.Accent2, 0.5f), 2f);
        screen.showcaseHeader = UIFactory.CreateText(show.transform, "Header", "", 26, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(screen.showcaseHeader.rectTransform, 10, 40, 16, 16);
        screen.showcaseGlow = UIFactory.CreateImage(show.transform, "Glow", new Color(1f, 1f, 1f, 0.4f), theme != null ? theme.lightRays : null);
        UIFactory.Place(screen.showcaseGlow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -320), new Vector2(760, 760));
        screen.showcaseGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        screen.showcaseGlow.rectTransform.anchoredPosition = new Vector2(0, -320);
        var softGlow = UIFactory.CreateImage(show.transform, "SoftGlow", new Color(1f, 0.6f, 0.85f, 0.25f), theme != null ? theme.glow : null);
        UIFactory.Place(softGlow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(560, 560));
        screen.showcaseView = CardView.Build(show.transform, "ShowcaseCard");
        var sv = (RectTransform)screen.showcaseView.transform;
        UIFactory.Place(sv, new Vector2(0.5f, 1f), new Vector2(0, -58), new Vector2(CardView.Width, CardView.Height));
        sv.localScale = Vector3.one * 0.86f;
        var ribbon = UIFactory.CreateChip(show.transform, "NewRoll", UIFactory.Accent2);
        UIFactory.Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -46), new Vector2(300, 36));
        UIFactory.AddOutline(ribbon, Color.white, 2f);
        var rt = UIFactory.CreateText(ribbon.transform, "Text", "NEW ROLL  -  NOT SAVED", 19, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(rt.rectTransform);
        screen.newRollRibbon = ribbon.gameObject;
        ribbon.gameObject.SetActive(false);

        float third = (560 - 40 - 16) / 3f;
        for (int i = 0; i < 3; i++)
        {
            var b = UIFactory.CreateButton(show.transform, $"SaveSlot{i + 1}", $"SAVE TO S{i + 1}", UIFactory.Accent, 18);
            UIFactory.TopLeft((RectTransform)b.transform, 20 + i * (third + 8), 606, third, 58);
            screen.saveSlotButtons[i] = b;
        }
        screen.lockButton = UIFactory.CreateButton(show.transform, "Lock", "LOCK", UIFactory.PanelLight, 20);
        UIFactory.TopLeft((RectTransform)screen.lockButton.transform, 20, 674, 254, 52);
        screen.makeMainButton = UIFactory.CreateButton(show.transform, "MakeMain", "USE AS MAIN", new Color(0.85f, 0.3f, 0.55f), 20);
        UIFactory.TopLeft((RectTransform)screen.makeMainButton.transform, 286, 674, 254, 52);
        var pending = UIFactory.CreatePanel(show.transform, "PendingBox", new Color(0.02f, 0.02f, 0.06f, 0.85f), false);
        UIFactory.TopLeft(pending.rectTransform, 20, 740, 520, 186);
        screen.pendingText = UIFactory.CreateText(pending.transform, "Text", "", 20, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.Stretch(screen.pendingText.rectTransform, 16, 12, 16, 70);
        UIFactory.BestFit(screen.pendingText, 12);
        screen.discardButton = UIFactory.CreateButton(pending.transform, "Discard", "DISCARD NEW ROLL", UIFactory.PanelLight, 20);
        UIFactory.Place((RectTransform)screen.discardButton.transform, new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(300, 50));

        // ---------- upgrade panel (middle)
        const float uw = 520f;
        var up = UIFactory.CreatePanel(canvas, "Upgrade", panelColor, true);
        UIFactory.TopLeft(up.rectTransform, 600, 112, uw, 944);
        UIFactory.AddOutline(up, UIFactory.WithAlpha(UIFactory.Accent, 0.5f), 2f);
        Section(up.transform, "BUILD", 12, uw);
        screen.rarityLine = UIFactory.CreateText(up.transform, "Rarity", "", 21, UIFactory.TextMain, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(screen.rarityLine.rectTransform, 20, 50, uw - 40, 30);
        UIFactory.BestFit(screen.rarityLine, 12);
        for (int i = 0; i < 4; i++)
        {
            float y = 88 + i * 50;
            var row = UIFactory.CreateImage(up.transform, $"Stat {StatNames[i]}", new Color(1f, 1f, 1f, i % 2 == 0 ? 0.04f : 0.02f), theme != null ? theme.roundedSmall : null, true, true);
            UIFactory.TopLeft(row.rectTransform, 16, y, uw - 32, 44);
            screen.statTips[i] = TooltipTrigger.Add(row, StatNames[i]);
            var label = UIFactory.CreateText(row.transform, "Label", StatNames[i], 20, UIFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.TopLeft(label.rectTransform, 12, 0, 56, 44);
            screen.statValues[i] = UIFactory.CreateText(row.transform, "Value", "", 22, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.TopLeft(screen.statValues[i].rectTransform, 68, 0, 140, 44);
            var barBg = UIFactory.CreateImage(row.transform, "RollBar", new Color(0f, 0f, 0f, 0.5f), theme != null ? theme.roundedSmall : null, true);
            UIFactory.TopLeft(barBg.rectTransform, 214, 13, 190, 18);
            screen.statFills[i] = UIFactory.CreateImage(barBg.transform, "Fill", UIFactory.Good, theme != null ? theme.roundedSmall : null, true);
            var center = UIFactory.CreateImage(barBg.transform, "Zero", new Color(1f, 1f, 1f, 0.7f));
            UIFactory.Place(center.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 24));
            screen.statRolls[i] = UIFactory.CreateText(row.transform, "Roll", "", 20, UIFactory.TextMain, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.TopLeft(screen.statRolls[i].rectTransform, 408, 0, 64, 44);
        }
        screen.elementIcon = UIFactory.CreateImage(up.transform, "ElementIcon", Color.white);
        screen.elementIcon.preserveAspect = true;
        UIFactory.TopLeft(screen.elementIcon.rectTransform, 20, 292, 60, 60);
        screen.strongRow = UIFactory.CreateRect("StrongVs", up.transform);
        UIFactory.TopLeft(screen.strongRow, 90, 302, 360, 40);
        screen.elementNew = NewBadge(up.transform, new Vector2(440, 308));
        // Talent box: full information (name, type, group, description); God cards show two compact blocks
        var talentBox = UIFactory.CreateImage(up.transform, "TalentBox", new Color(1f, 1f, 1f, 0.04f), theme != null ? theme.roundedSmall : null, true);
        UIFactory.TopLeft(talentBox.rectTransform, 12, 346, uw - 24, 124);
        screen.talentIcon = UIFactory.CreateImage(up.transform, "TalentIcon", Color.white);
        screen.talentIcon.preserveAspect = true;
        UIFactory.TopLeft(screen.talentIcon.rectTransform, 20, 358, 56, 56);
        screen.talentBonus = UIFactory.CreateText(up.transform, "TalentBonus", "", 16, new Color(0.55f, 1f, 0.45f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(screen.talentBonus.rectTransform, 20, 420, 60, 24);
        screen.talentName = UIFactory.CreateText(up.transform, "TalentName", "", 20, UIFactory.TextMain, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(screen.talentName.rectTransform, 88, 352, 412, 30);
        UIFactory.BestFit(screen.talentName, 12);
        screen.talentDesc = UIFactory.CreateText(up.transform, "TalentDesc", "", 16, new Color(0.85f, 0.87f, 0.93f), TextAnchor.UpperLeft);
        UIFactory.TopLeft(screen.talentDesc.rectTransform, 88, 382, 412, 84);
        UIFactory.BestFit(screen.talentDesc, 10);
        screen.talentIcon2 = UIFactory.CreateImage(up.transform, "TalentIcon2", Color.white);
        screen.talentIcon2.preserveAspect = true;
        UIFactory.TopLeft(screen.talentIcon2.rectTransform, 20, 412, 48, 48);
        screen.talentName2 = UIFactory.CreateText(up.transform, "TalentName2", "", 18, UIFactory.TextMain, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(screen.talentName2.rectTransform, 76, 410, 424, 26);
        UIFactory.BestFit(screen.talentName2, 12);
        screen.talentDesc2 = UIFactory.CreateText(up.transform, "TalentDesc2", "", 14, new Color(0.85f, 0.87f, 0.93f), TextAnchor.UpperLeft);
        UIFactory.TopLeft(screen.talentDesc2.rectTransform, 76, 435, 424, 34);
        UIFactory.BestFit(screen.talentDesc2, 10);
        screen.talentIcon2.gameObject.SetActive(false);
        screen.talentName2.gameObject.SetActive(false);
        screen.talentDesc2.gameObject.SetActive(false);
        screen.talentNew = NewBadge(up.transform, new Vector2(436, 440));

        Section(up.transform, "UPGRADE  (keep rolling, save when you like it)", 474, uw);
        screen.rerollStatsButton = ActionButton(up.transform, "RerollStats", "REROLL STATS", new Color(0.85f, 0.55f, 0.15f), 20, 510, uw - 40, 50, out screen.rerollStatsIcon, out screen.rerollStatsCost);
        screen.rerollElementButton = ActionButton(up.transform, "RerollElement", "REROLL ELEMENT", new Color(0.2f, 0.6f, 0.45f), 20, 564, uw - 40, 50, out screen.rerollElementIcon, out screen.rerollElementCost);
        screen.rerollTalentButton = ActionButton(up.transform, "RerollTalent", "REROLL TALENT", new Color(0.75f, 0.55f, 0.15f), 20, 618, uw - 40, 50, out screen.rerollTalentIcon, out screen.rerollTalentCost);
        Section(up.transform, "RANK UP", 674, uw);
        screen.fusionButton = ActionButton(up.transform, "Fusion", "FUSION", new Color(0.55f, 0.3f, 0.95f), 20, 710, uw - 40, 50, out screen.fusionIcon, out screen.fusionCost);
        screen.autoMergeButton = UIFactory.CreateButton(up.transform, "AutoMerge", "AUTO MERGE  <size=16>fuse everything up to a rarity</size>", new Color(0.35f, 0.25f, 0.75f), 20);
        UIFactory.TopLeft((RectTransform)screen.autoMergeButton.transform, 20, 766, uw - 40, 46);
        screen.godOrbButton = ActionButton(up.transform, "GodOrb", "GOD ORB", new Color(0.75f, 0.62f, 0.25f), 20, 818, 234, 50, out screen.godOrbIcon, out screen.godOrbCost);
        screen.dismantleButton = ActionButton(up.transform, "Dismantle", "DISMANTLE", new Color(0.5f, 0.32f, 0.24f), 266, 818, 234, 50, out screen.dismantleIcon, out screen.dismantleCost);
        screen.messageText = UIFactory.CreateText(up.transform, "Message", "", 20, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopLeft(screen.messageText.rectTransform, 16, 874, uw - 32, 62);
        UIFactory.BestFit(screen.messageText, 12);

        // ---------- collection (right)
        var col = UIFactory.CreatePanel(canvas, "Collection", panelColor, true);
        UIFactory.TopLeft(col.rectTransform, 1136, 112, 760, 944);
        UIFactory.AddOutline(col, UIFactory.WithAlpha(UIFactory.Gold, 0.4f), 2f);
        screen.rarityFilter = UIFactory.CreateDropdown(col.transform, "RarityFilter", 20);
        UIFactory.TopLeft((RectTransform)screen.rarityFilter.transform, 16, 14, 250, 52);
        screen.elementFilter = UIFactory.CreateDropdown(col.transform, "ElementFilter", 20);
        UIFactory.TopLeft((RectTransform)screen.elementFilter.transform, 276, 14, 250, 52);
        screen.sortDropdown = UIFactory.CreateDropdown(col.transform, "Sort", 20, 230f);
        UIFactory.TopLeft((RectTransform)screen.sortDropdown.transform, 536, 14, 208, 52);
        screen.countText = UIFactory.CreateText(col.transform, "Count", "", 18, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(screen.countText.rectTransform, 20, 70, 530, 26);
        screen.materialsButton = UIFactory.CreateButton(col.transform, "Materials", "MATERIALS / EXCHANGE", new Color(0.2f, 0.5f, 0.75f), 17);
        UIFactory.TopLeft((RectTransform)screen.materialsButton.transform, 556, 66, 188, 32);
        UIFactory.BestFit(screen.countText, 11);
        var scroll = UIFactory.CreateScroll(col.transform, "GridScroll", true, out RectTransform gridContent);
        UIFactory.Stretch((RectTransform)scroll.transform, 8, 100, 8, 214);
        UIFactory.AddGrid(gridContent, new Vector2(150, 210), new Vector2(22, 34), 18);
        screen.grid = gridContent;
        var slotBar = UIFactory.CreatePanel(col.transform, "SlotBar", new Color(0.03f, 0.02f, 0.08f, 0.9f), false);
        UIFactory.Region(slotBar.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), 8, 0, 8, 8);
        slotBar.rectTransform.sizeDelta = new Vector2(slotBar.rectTransform.sizeDelta.x, 196);
        slotBar.rectTransform.anchoredPosition = new Vector2(slotBar.rectTransform.anchoredPosition.x, 106);
        var slotLabel = UIFactory.CreateText(slotBar.transform, "Label", "BUILD SLOTS\n<size=16><color=#C9C3D8>Choose one on the map\nbefore a fight. Slotted\ncards are auto-locked.</color></size>", 24, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Place(slotLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(18, 0), new Vector2(230, 170));
        screen.slotRow = UIFactory.CreateRect("SlotRow", slotBar.transform);
        UIFactory.Stretch(screen.slotRow);

        BuildFusion(canvas, screen);
        BuildAutoMerge(canvas, screen);
        BuildMaterials(canvas, screen);
        BuildReroll(canvas, screen);
        BuildUnsaved(canvas, screen);
        return screen;
    }

    static void BuildReroll(RectTransform canvas, CollectionScreen screen)
    {
        var dim = UIFactory.CreateImage(canvas, "RerollPanel", new Color(0, 0, 0, 0.82f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        screen.rerollPanel = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.08f, 0.06f, 0.16f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1780, 940));
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(UIFactory.Gold, 0.6f), 3f);
        var theme = UIFactory.Theme;
        if (theme != null && theme.lightRays != null)
        {
            var rays = UIFactory.CreateImage(panel.transform, "Rays", new Color(1f, 0.85f, 0.6f, 0.06f), theme.lightRays);
            UIFactory.Place(rays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-480, 0), new Vector2(1300, 1300));
        }
        var title = UIFactory.CreateText(panel.transform, "Title", "REROLL", 46, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, 40, 18, 400, 60);
        UIFactory.AddShadow(title, UIFactory.ShadowColor, 3f);
        var sub = UIFactory.CreateText(panel.transform, "Sub", "Roll as many times as you like. Nothing changes until you save the new roll to a slot.", 20, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(sub.rectTransform, 260, 30, 1000, 40);

        var curLabel = UIFactory.CreateText(panel.transform, "CurLabel", "CURRENT", 24, UIFactory.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopLeft(curLabel.rectTransform, 40, 92, 330, 34);
        screen.rerollCurrentView = CardView.Build(panel.transform, "Current");
        var crt = (RectTransform)screen.rerollCurrentView.transform;
        UIFactory.Place(crt, new Vector2(0f, 1f), new Vector2(205, -134), new Vector2(CardView.Width, CardView.Height));
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(205, -134);
        crt.localScale = Vector3.one * 0.8f;

        var arrow = UIFactory.CreateImage(panel.transform, "Arrow", UIFactory.Gold, theme != null ? theme.arrow : null);
        arrow.preserveAspect = true;
        UIFactory.TopLeft(arrow.rectTransform, 384, 360, 64, 40);

        var newLabel = UIFactory.CreateText(panel.transform, "NewLabel", "NEW ROLL", 24, UIFactory.Accent2, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopLeft(newLabel.rectTransform, 462, 92, 330, 34);
        screen.rerollNewView = CardView.Build(panel.transform, "New");
        var nrt = (RectTransform)screen.rerollNewView.transform;
        UIFactory.Place(nrt, new Vector2(0f, 1f), new Vector2(627, -134), new Vector2(CardView.Width, CardView.Height));
        nrt.pivot = new Vector2(0.5f, 1f);
        nrt.anchoredPosition = new Vector2(627, -134);
        nrt.localScale = Vector3.one * 0.8f;
        screen.rerollNoRoll = UIFactory.CreateText(panel.transform, "NoRoll", "No new roll yet.\nPress a reroll button.", 24, UIFactory.TextDim, TextAnchor.MiddleCenter, FontStyle.Italic);
        UIFactory.TopLeft(screen.rerollNoRoll.rectTransform, 467, 134, 320, 496);

        var cmp = UIFactory.CreateImage(panel.transform, "CompareBox", new Color(1f, 1f, 1f, 0.04f), theme != null ? theme.roundedSmall : null, true);
        UIFactory.TopLeft(cmp.rectTransform, 840, 92, 900, 452);
        screen.rerollCompare = UIFactory.CreateText(panel.transform, "Compare", "", 22, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopLeft(screen.rerollCompare.rectTransform, 862, 104, 860, 430);
        UIFactory.BestFit(screen.rerollCompare, 12);

        screen.rrStatsButton = ActionButton(panel.transform, "RR Stats", "REROLL STATS", new Color(0.85f, 0.55f, 0.15f), 840, 560, 440, 60, out screen.rrStatsIcon, out screen.rrStatsCost);
        screen.rrElementButton = ActionButton(panel.transform, "RR Element", "REROLL ELEMENT", new Color(0.2f, 0.6f, 0.45f), 1300, 560, 440, 60, out screen.rrElementIcon, out screen.rrElementCost);
        screen.rrTalentButton = ActionButton(panel.transform, "RR Talent", "REROLL TALENT", new Color(0.75f, 0.55f, 0.15f), 840, 630, 440, 60, out screen.rrTalentIcon, out screen.rrTalentCost);
        screen.rrGodOrbButton = ActionButton(panel.transform, "RR GodOrb", "GOD ORB", new Color(0.75f, 0.62f, 0.25f), 1300, 630, 440, 60, out screen.rrGodOrbIcon, out screen.rrGodOrbCost);

        float sw = (900 - 2 * 14) / 3f;
        for (int i = 0; i < 3; i++)
        {
            screen.rrSaveButtons[i] = UIFactory.CreateButton(panel.transform, $"RR Save {i + 1}", $"SAVE TO S{i + 1}", UIFactory.Accent2, 22);
            UIFactory.TopLeft((RectTransform)screen.rrSaveButtons[i].transform, 840 + i * (sw + 14), 712, sw, 64);
        }
        screen.rrDiscardButton = UIFactory.CreateButton(panel.transform, "RR Discard", "DISCARD NEW ROLL", new Color(0.45f, 0.2f, 0.25f), 20);
        UIFactory.TopLeft((RectTransform)screen.rrDiscardButton.transform, 840, 790, 440, 58);
        screen.rrCloseButton = UIFactory.CreateButton(panel.transform, "RR Close", "CLOSE", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.rrCloseButton.transform, 1300, 790, 440, 58);
        screen.rrMessage = UIFactory.CreateText(panel.transform, "Message", "", 22, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopLeft(screen.rrMessage.rectTransform, 40, 870, 1700, 50);
        UIFactory.BestFit(screen.rrMessage, 12);
        dim.gameObject.SetActive(false);
    }

    static void BuildUnsaved(RectTransform canvas, CollectionScreen screen)
    {
        var dim = UIFactory.CreateImage(canvas, "UnsavedPanel", new Color(0, 0, 0, 0.75f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        screen.unsavedPanel = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.12f, 0.06f, 0.12f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 470));
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(UIFactory.Accent2, 0.8f), 3f);
        var title = UIFactory.CreateText(panel.transform, "Title", "YOU HAVE NOT SAVED YOUR CARD", 38, UIFactory.Accent2, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(title.rectTransform, 20, 56, 20, 20);
        UIFactory.AddShadow(title, UIFactory.ShadowColor, 3f);
        screen.unsavedBody = UIFactory.CreateText(panel.transform, "Body", "", 22, UIFactory.TextMain, TextAnchor.MiddleCenter);
        UIFactory.TopStrip(screen.unsavedBody.rectTransform, 86, 140, 40, 40);
        UIFactory.BestFit(screen.unsavedBody, 12);
        float sw = (900 - 2 * 14) / 3f;
        for (int i = 0; i < 3; i++)
        {
            screen.unsavedSaveButtons[i] = UIFactory.CreateButton(panel.transform, $"Save {i + 1}", $"SAVE TO S{i + 1}", UIFactory.Accent, 22);
            UIFactory.TopLeft((RectTransform)screen.unsavedSaveButtons[i].transform, 40 + i * (sw + 14), 246, sw, 70);
        }
        screen.unsavedDiscardButton = UIFactory.CreateButton(panel.transform, "Refuse", "DON'T SAVE", new Color(0.55f, 0.18f, 0.22f), 22);
        UIFactory.TopLeft((RectTransform)screen.unsavedDiscardButton.transform, 40, 344, 443, 70);
        screen.unsavedCancelButton = UIFactory.CreateButton(panel.transform, "Cancel", "KEEP ROLLING", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.unsavedCancelButton.transform, 497, 344, 443, 70);
        dim.gameObject.SetActive(false);
    }

    static void BuildMaterials(RectTransform canvas, CollectionScreen screen)
    {
        var dim = UIFactory.CreateImage(canvas, "MaterialsPanel", new Color(0, 0, 0, 0.8f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        screen.materialsPanel = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.07f, 0.08f, 0.16f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 780));
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(new Color(0.3f, 0.7f, 1f), 0.6f), 3f);
        var title = UIFactory.CreateText(panel.transform, "Title", "CARD MATERIALS", 46, new Color(0.55f, 0.85f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(title.rectTransform, 16, 60, 20, 20);
        UIFactory.AddShadow(title, UIFactory.ShadowColor, 3f);
        screen.materialsInfo = UIFactory.CreateText(panel.transform, "Info", "", 20, UIFactory.TextMain, TextAnchor.MiddleCenter);
        UIFactory.TopStrip(screen.materialsInfo.rectTransform, 80, 76, 60, 60);
        UIFactory.BestFit(screen.materialsInfo, 12);
        screen.materialsRow = UIFactory.CreateRect("Row", panel.transform);
        UIFactory.TopStrip(screen.materialsRow, 166, 140, 20, 20);

        Section(panel.transform, "EXCHANGE", 326, 1200);
        screen.exchangeSelectedButton = UIFactory.CreateButton(panel.transform, "ExchangeSelected", "EXCHANGE SELECTED CARD", new Color(0.2f, 0.5f, 0.75f), 22);
        UIFactory.TopLeft((RectTransform)screen.exchangeSelectedButton.transform, 40, 372, 1120, 60);
        var upTo = UIFactory.CreateText(panel.transform, "UpTo", "Every unlocked card up to:", 20, UIFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(upTo.rectTransform, 40, 446, 290, 56);
        screen.exchangeTarget = UIFactory.CreateDropdown(panel.transform, "ExchangeTarget", 20, 360f);
        UIFactory.TopLeft((RectTransform)screen.exchangeTarget.transform, 330, 446, 260, 56);
        screen.exchangeAllButton = UIFactory.CreateButton(panel.transform, "ExchangeAll", "EXCHANGE ALL", new Color(0.55f, 0.3f, 0.95f), 20);
        UIFactory.TopLeft((RectTransform)screen.exchangeAllButton.transform, 606, 446, 554, 56);

        Section(panel.transform, "AUTO EXCHANGE  (new cards from packs and drops)", 526, 1200);
        screen.autoExchangeButton = UIFactory.CreateButton(panel.transform, "AutoExchange", "AUTO EXCHANGE: OFF", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.autoExchangeButton.transform, 40, 572, 420, 58);
        var autoUpTo = UIFactory.CreateText(panel.transform, "AutoUpTo", "for cards up to:", 20, UIFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(autoUpTo.rectTransform, 480, 572, 180, 58);
        screen.autoExchangeTarget = UIFactory.CreateDropdown(panel.transform, "AutoTarget", 20, 360f);
        UIFactory.TopLeft((RectTransform)screen.autoExchangeTarget.transform, 660, 572, 260, 58);

        screen.materialsResult = UIFactory.CreateText(panel.transform, "Result", "", 22, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(screen.materialsResult.rectTransform, 642, 44, 40, 40);
        UIFactory.BestFit(screen.materialsResult, 12);
        screen.materialsCloseButton = UIFactory.CreateButton(panel.transform, "Close", "CLOSE", UIFactory.PanelLight, 24);
        UIFactory.Place((RectTransform)screen.materialsCloseButton.transform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(260, 60));
        dim.gameObject.SetActive(false);
    }

    static void BuildFusion(RectTransform canvas, CollectionScreen screen)
    {
        var dim = UIFactory.CreateImage(canvas, "FusionPanel", new Color(0, 0, 0, 0.8f), null, false, true);
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
        UIFactory.TopLeft(screen.fusionInfo.rectTransform, 1070, 110, 400, 530);
        UIFactory.BestFit(screen.fusionInfo, 12);
        screen.fuseButton = UIFactory.CreateButton(panel.transform, "Fuse", "FUSE", new Color(0.55f, 0.3f, 0.95f), 30);
        UIFactory.TopLeft((RectTransform)screen.fuseButton.transform, 1070, 660, 400, 84);
        screen.fusionResult = UIFactory.CreateText(panel.transform, "Result", "", 22, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopLeft(screen.fusionResult.rectTransform, 20, 490, 300, 220);
        UIFactory.BestFit(screen.fusionResult, 12);
        screen.fusionCloseButton = UIFactory.CreateButton(panel.transform, "Close", "CLOSE", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.fusionCloseButton.transform, 60, 720, 220, 64);
        dim.gameObject.SetActive(false);
    }

    static void BuildAutoMerge(RectTransform canvas, CollectionScreen screen)
    {
        var dim = UIFactory.CreateImage(canvas, "AutoMergePanel", new Color(0, 0, 0, 0.8f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        screen.mergePanel = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.08f, 0.06f, 0.16f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 900));
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(new Color(0.7f, 0.5f, 1f), 0.8f), 3f);
        var title = UIFactory.CreateText(panel.transform, "Title", "AUTO MERGE", 50, new Color(0.8f, 0.6f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, 40, 20, 600, 64);
        UIFactory.AddShadow(title, UIFactory.ShadowColor, 3f);
        var sub = UIFactory.CreateText(panel.transform, "Sub", "Fuses your cards automatically so you don't have to do it one by one.", 21, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(sub.rectTransform, 40, 84, 1000, 30);

        var t1 = UIFactory.CreateText(panel.transform, "TargetLabel", "MAX RARITY", 22, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(t1.rectTransform, 40, 130, 300, 30);
        screen.mergeTarget = UIFactory.CreateDropdown(panel.transform, "Target", 22, 400f);
        UIFactory.TopLeft((RectTransform)screen.mergeTarget.transform, 40, 164, 320, 58);
        var t2 = UIFactory.CreateText(panel.transform, "OrbLabel", "MAX FUSION ORBS TO SPEND", 22, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(t2.rectTransform, 400, 130, 500, 30);
        screen.mergeOrbs = UIFactory.CreateInputField(panel.transform, "Orbs", "10", 26);
        screen.mergeOrbs.contentType = InputField.ContentType.IntegerNumber;
        UIFactory.TopLeft((RectTransform)screen.mergeOrbs.transform, 400, 164, 200, 58);
        var orbIcon = UIFactory.Icon(screen.mergeOrbs.transform, "OrbIcon", UIFactory.Theme != null ? UIFactory.Theme.fusionOrbIcon : null, Color.clear, 40, Vector2.zero);
        var ort = orbIcon.rectTransform; ort.anchorMin = ort.anchorMax = new Vector2(1f, 0.5f); ort.pivot = new Vector2(1f, 0.5f); ort.anchoredPosition = new Vector2(-8, 0);
        TooltipTrigger.Add(orbIcon, ItemInfo.Describe(ItemKind.FusionOrb));
        screen.mergeOrbs10 = UIFactory.CreateButton(panel.transform, "Orbs10", "10", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.mergeOrbs10.transform, 616, 164, 110, 58);
        screen.mergeOrbs50 = UIFactory.CreateButton(panel.transform, "Orbs50", "50", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.mergeOrbs50.transform, 736, 164, 110, 58);
        screen.mergeOrbsAll = UIFactory.CreateButton(panel.transform, "OrbsAll", "ALL", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.mergeOrbsAll.transform, 856, 164, 110, 58);

        screen.mergeInfo = UIFactory.CreateText(panel.transform, "Info", "", 21, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopLeft(screen.mergeInfo.rectTransform, 40, 240, 1320, 190);
        UIFactory.BestFit(screen.mergeInfo, 12);
        screen.mergeStartButton = UIFactory.CreateButton(panel.transform, "Start", "START AUTO MERGE", new Color(0.55f, 0.3f, 0.95f), 26);
        UIFactory.TopLeft((RectTransform)screen.mergeStartButton.transform, 1000, 150, 360, 80);

        screen.mergeResultText = UIFactory.CreateText(panel.transform, "Result", "", 22, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopLeft(screen.mergeResultText.rectTransform, 40, 440, 1320, 150);
        UIFactory.BestFit(screen.mergeResultText, 12);
        var scroll = UIFactory.CreateScroll(panel.transform, "Obtained", false, out RectTransform content);
        UIFactory.TopLeft((RectTransform)scroll.transform, 40, 596, 1320, 196);
        var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 14; h.padding = new RectOffset(4, 4, 14, 4);
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        screen.mergeResultGrid = content;
        screen.mergeCloseButton = UIFactory.CreateButton(panel.transform, "Close", "CLOSE", UIFactory.PanelLight, 24);
        UIFactory.Place((RectTransform)screen.mergeCloseButton.transform, new Vector2(1f, 0f), new Vector2(-40, 28), new Vector2(240, 66));
        dim.gameObject.SetActive(false);
    }
}
