using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Collection / team builder: grid of owned cards with filters, a detail panel with the big card,
/// team slot assignment, enhancing (feed fodder cards for XP) and evolving (two copies at max level).
/// </summary>
public class CollectionScreen : MonoBehaviour
{
    public Button backButton;
    public Text titleText;
    public Text currencyText;
    public Button rarityFilterButton;
    public Button elementFilterButton;
    public Button sortButton;
    public RectTransform grid;
    public ScrollRect gridScroll;
    public RectTransform teamRow;
    public CardView detailView;
    public StatBar xpBar;
    public Text detailText;
    public Button[] slotButtons = new Button[3];
    public Button enhanceButton;
    public Button evolveButton;
    public Button favoriteButton;
    public Text messageText;

    enum SortMode { Power, Level, Rarity, Newest, Name }

    int rarityFilter = -1;   // -1 = all
    int elementFilter = -1;  // -1 = all
    SortMode sortMode = SortMode.Power;
    OwnedCard selected;
    readonly Dictionary<int, CardTile> tiles = new Dictionary<int, CardTile>();

    void Start()
    {
        backButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.MainMenu));
        rarityFilterButton.onClick.AddListener(() => { rarityFilter = NextFilter(rarityFilter, System.Enum.GetValues(typeof(CardRarity)).Length); RebuildGrid(); });
        elementFilterButton.onClick.AddListener(() => { elementFilter = NextFilter(elementFilter, System.Enum.GetValues(typeof(CardElement)).Length); RebuildGrid(); });
        sortButton.onClick.AddListener(() => { sortMode = (SortMode)(((int)sortMode + 1) % 5); RebuildGrid(); });
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i;
            if (slotButtons[i] != null) slotButtons[i].onClick.AddListener(() => AssignSlot(slot));
        }
        enhanceButton.onClick.AddListener(OnEnhance);
        evolveButton.onClick.AddListener(OnEvolve);
        favoriteButton.onClick.AddListener(OnFavorite);
        messageText.text = "";

        if (CardDatabase.Instance == null)
        {
            titleText.text = "COLLECTION - database missing (Tools > AniGame > Build Everything)";
            return;
        }

        var team = PlayerProfile.GetTeam();
        selected = team.Count > 0 ? team[0] : PlayerProfile.Data.cards.FirstOrDefault();
        RebuildGrid();
        RefreshTeamRow();
        RefreshDetail();
        InvokeRepeating(nameof(RefreshCurrency), 0f, 1f);
    }

    static int NextFilter(int current, int count) => current + 1 >= count ? -1 : current + 1;

    void RefreshCurrency()
    {
        currencyText.text = UIFactory.CurrencyLine();
    }

    IEnumerable<OwnedCard> FilteredCards()
    {
        var cards = PlayerProfile.Data.cards.Where(c => PlayerProfile.Resolve(c) != null);
        if (rarityFilter >= 0) cards = cards.Where(c => (int)PlayerProfile.Resolve(c).rarity == rarityFilter);
        if (elementFilter >= 0) cards = cards.Where(c => (int)PlayerProfile.Resolve(c).element == elementFilter);
        switch (sortMode)
        {
            case SortMode.Level: return cards.OrderByDescending(c => c.level).ThenByDescending(c => c.evolution);
            case SortMode.Rarity: return cards.OrderByDescending(c => (int)PlayerProfile.Resolve(c).rarity).ThenByDescending(c => c.level);
            case SortMode.Newest: return cards.OrderByDescending(c => c.uid);
            case SortMode.Name: return cards.OrderBy(c => PlayerProfile.Resolve(c).DisplayName);
            default: return cards.OrderByDescending(c => CardStats.Power(PlayerProfile.Resolve(c), c.level, c.evolution));
        }
    }

    void RebuildGrid()
    {
        UIFactory.ButtonLabel(rarityFilterButton).text = rarityFilter < 0 ? "RARITY: ALL" : "RARITY: " + CardStats.RarityName((CardRarity)rarityFilter).ToUpperInvariant();
        UIFactory.ButtonLabel(elementFilterButton).text = elementFilter < 0 ? "ELEMENT: ALL" : "ELEMENT: " + ((CardElement)elementFilter).ToString().ToUpperInvariant();
        UIFactory.ButtonLabel(sortButton).text = "SORT: " + sortMode.ToString().ToUpperInvariant();

        UIFactory.Clear(grid);
        tiles.Clear();
        var list = FilteredCards().ToList();
        foreach (var oc in list)
        {
            var owned = oc;
            var card = PlayerProfile.Resolve(owned);
            var tile = CardTile.Build(grid, $"Card {owned.uid}", 150, 210);
            tile.Set(card, owned.evolution > 0 ? $"Lv{owned.level} E{owned.evolution}" : $"Lv{owned.level}", CardStats.RarityShort(card.rarity));
            int slot = PlayerProfile.Data.team.IndexOf(owned.uid);
            if (slot >= 0) tile.SetBadge($"TEAM {slot + 1}", UIFactory.Accent);
            else if (owned.favorite) tile.SetBadge("FAVORITE", new Color(0.85f, 0.55f, 0.1f));
            tile.button.onClick.AddListener(() => Select(owned));
            tile.SetSelected(owned == selected);
            tiles[owned.uid] = tile;
        }
        titleText.text = $"COLLECTION  <size=26><color=#9AA4B5>{list.Count}/{PlayerProfile.Data.cards.Count} cards</color></size>";
    }

    void RefreshTeamRow()
    {
        UIFactory.Clear(teamRow);
        for (int i = 0; i < PlayerProfile.TeamSize; i++)
        {
            var owned = PlayerProfile.GetTeamSlot(i);
            var card = PlayerProfile.Resolve(owned);
            var tile = CardTile.Build(teamRow, $"Slot {i + 1}", 112, 156);
            var rt = (RectTransform)tile.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(170 + i * 128f, 0f);
            tile.Set(card, owned != null ? $"Lv{owned.level}" : "", $"#{i + 1}");
            tile.nameText.fontSize = 14;
            if (owned != null)
            {
                var o = owned;
                tile.button.onClick.AddListener(() => Select(o));
            }
        }
    }

    void Select(OwnedCard owned)
    {
        if (selected != null && tiles.TryGetValue(selected.uid, out CardTile old)) old.SetSelected(false);
        selected = owned;
        if (selected != null && tiles.TryGetValue(selected.uid, out CardTile now)) now.SetSelected(true);
        messageText.text = "";
        RefreshDetail();
    }

    void RefreshDetail()
    {
        var card = PlayerProfile.Resolve(selected);
        bool has = card != null;
        detailView.gameObject.SetActive(has);
        foreach (var b in slotButtons) if (b != null) b.interactable = has;
        enhanceButton.interactable = has;
        evolveButton.interactable = has;
        favoriteButton.interactable = has;
        if (!has)
        {
            detailText.text = "Select a card.";
            xpBar.Set(0, 1, true);
            return;
        }

        detailView.Show(card, selected.level, selected.evolution);
        bool maxed = selected.level >= CardStats.MaxLevel;
        int need = CardStats.XpToNext(selected.level);
        xpBar.format = maxed ? "MAX LEVEL" : "XP {0} / {1}";
        xpBar.Set(maxed ? 1 : selected.xp, maxed ? 1 : need, true);

        string talent = card.talent != null ? $"<b>{card.talent.DisplayName}</b> <color=#9AA4B5>({(card.talent.IsActive ? "Active" : "Passive")})</color>\n<color=#C9D1DC>{card.talent.Description}</color>" : "No talent";
        string source = card.location > 0 ? $"From LOC {card.location} floor {card.floor}" : "";
        if (!string.IsNullOrEmpty(card.anime)) source += $"\n<color=#FFD54F>{card.anime}</color>";
        detailText.text = $"{talent}\n<color=#9AA4B5>Evolution {selected.evolution}/{CardStats.MaxEvolution}   Power {CardStats.Power(card, selected.level, selected.evolution)}   {source}</color>";

        var fodder = PlayerProfile.FodderCandidates(selected).FirstOrDefault();
        UIFactory.ButtonLabel(enhanceButton).text = maxed ? "MAX LEVEL" : fodder != null ? $"ENHANCE (+{PlayerProfile.FodderXpValue(fodder)} XP)" : "NO FODDER";
        enhanceButton.interactable = !maxed && fodder != null;

        string reason = PlayerProfile.CanEvolve(selected, out _);
        UIFactory.ButtonLabel(evolveButton).text = reason == null ? $"EVOLVE ({CardStats.EvolveCost(card.rarity):N0} G)" : "EVOLVE";
        UIFactory.ButtonLabel(favoriteButton).text = selected.favorite ? "UNFAVORITE" : "FAVORITE";

        int slot = PlayerProfile.Data.team.IndexOf(selected.uid);
        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null) continue;
            UIFactory.ButtonLabel(slotButtons[i]).text = slot == i ? $"IN SLOT {i + 1}" : $"SLOT {i + 1}";
        }
    }

    void AssignSlot(int slot)
    {
        if (selected == null) return;
        PlayerProfile.SetTeamSlot(slot, selected.uid);
        messageText.text = $"<color=#7CFC6A>{PlayerProfile.Resolve(selected).DisplayName} is now in team slot {slot + 1}.</color>";
        RebuildGrid();
        RefreshTeamRow();
        RefreshDetail();
    }

    void OnEnhance()
    {
        if (selected == null) return;
        var fodder = PlayerProfile.FodderCandidates(selected).FirstOrDefault();
        if (fodder == null)
        {
            messageText.text = "<color=#FF6B6B>No spare cards to feed. Win floors to collect more.</color>";
            return;
        }
        var fodderCard = PlayerProfile.Resolve(fodder);
        int before = selected.level;
        int xp = PlayerProfile.Enhance(selected, fodder);
        SfxSynth.Play("buff");
        messageText.text = $"<color=#7CFC6A>Fed {fodderCard.DisplayName} (+{xp} XP){(selected.level > before ? $"  Lv {before} -> {selected.level}!" : "")}</color>";
        RebuildGrid();
        RefreshTeamRow();
        RefreshDetail();
    }

    void OnEvolve()
    {
        if (selected == null) return;
        string reason = PlayerProfile.CanEvolve(selected, out _);
        if (reason != null)
        {
            messageText.text = $"<color=#FF6B6B>Can't evolve: {reason}.</color>";
            return;
        }
        PlayerProfile.Evolve(selected);
        SfxSynth.Play("win");
        messageText.text = $"<color=#FFD54F>Evolved to EVO {selected.evolution}! Stats +6%, level reset to 1.</color>";
        RebuildGrid();
        RefreshTeamRow();
        RefreshDetail();
    }

    void OnFavorite()
    {
        if (selected == null) return;
        selected.favorite = !selected.favorite;
        PlayerProfile.Save();
        messageText.text = selected.favorite ? "Favorite cards are never used as fodder." : "";
        RebuildGrid();
        RefreshDetail();
    }

    // ---------------------------------------------------------------- layout

    public static CollectionScreen BuildLayout(RectTransform canvas)
    {
        var screen = canvas.gameObject.AddComponent<CollectionScreen>();
        UIFactory.CreateBackground(canvas, new Color(0.6f, 0.2f, 0.45f, 0.7f), UIFactory.BgDark, out _);
        UIFactory.CreateTopBar(canvas, "COLLECTION", out screen.backButton, out screen.titleText, out screen.currencyText);

        // Left: filters + grid + team row
        var left = UIFactory.CreatePanel(canvas, "CardsPanel", UIFactory.Panel, true);
        UIFactory.Region(left.rectTransform, Vector2.zero, Vector2.one, 24, 120, 700, 24);

        screen.rarityFilterButton = UIFactory.CreateButton(left.transform, "RarityFilter", "RARITY: ALL", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.rarityFilterButton.transform, 20, 16, 300, 58);
        screen.elementFilterButton = UIFactory.CreateButton(left.transform, "ElementFilter", "ELEMENT: ALL", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.elementFilterButton.transform, 336, 16, 280, 58);
        screen.sortButton = UIFactory.CreateButton(left.transform, "SortButton", "SORT: POWER", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.sortButton.transform, 632, 16, 260, 58);

        screen.gridScroll = UIFactory.CreateScroll(left.transform, "GridScroll", true, out RectTransform gridContent);
        UIFactory.Stretch((RectTransform)screen.gridScroll.transform, 10, 90, 10, 200);
        UIFactory.AddGrid(gridContent, new Vector2(150, 210), new Vector2(18, 34), 18);
        screen.grid = gridContent;

        var teamBar = UIFactory.CreatePanel(left.transform, "TeamBar", new Color(0.03f, 0.04f, 0.08f, 0.8f), false);
        UIFactory.Region(teamBar.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), 10, 0, 10, 10);
        teamBar.rectTransform.sizeDelta = new Vector2(teamBar.rectTransform.sizeDelta.x, 180);
        teamBar.rectTransform.anchoredPosition = new Vector2(teamBar.rectTransform.anchoredPosition.x, 100);
        var teamLabel = UIFactory.CreateText(teamBar.transform, "Label", "TEAM\n<size=18><color=#9AA4B5>Slot 1 fights first,\nthe others take over\nif it falls.</color></size>", 28, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Place(teamLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(18, 0), new Vector2(150, 160));
        screen.teamRow = UIFactory.CreateRect("TeamRow", teamBar.transform);
        UIFactory.Stretch(screen.teamRow, 0, 0, 0, 0);

        // Right: detail
        var right = UIFactory.CreatePanel(canvas, "DetailPanel", UIFactory.Panel, true);
        UIFactory.Region(right.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), 0, 120, 24, 24);
        right.rectTransform.sizeDelta = new Vector2(656, right.rectTransform.sizeDelta.y);
        right.rectTransform.anchoredPosition = new Vector2(-352, right.rectTransform.anchoredPosition.y);

        screen.detailView = CardView.Build(right.transform, "DetailCard");
        var dv = (RectTransform)screen.detailView.transform;
        UIFactory.Place(dv, new Vector2(0f, 1f), new Vector2(20, -20), new Vector2(CardView.Width, CardView.Height));
        dv.localScale = Vector3.one * 0.72f;

        // Buttons column next to the card
        float bx = 20 + CardView.Width * 0.72f + 20;
        float bw = 656 - bx - 20;
        for (int i = 0; i < 3; i++)
        {
            var b = UIFactory.CreateButton(right.transform, $"Slot{i + 1}Button", $"SLOT {i + 1}", UIFactory.Accent, 24);
            UIFactory.TopLeft((RectTransform)b.transform, bx, 20 + i * 76, bw, 64);
            screen.slotButtons[i] = b;
        }
        screen.enhanceButton = UIFactory.CreateButton(right.transform, "EnhanceButton", "ENHANCE", new Color(0.2f, 0.6f, 0.4f), 22);
        UIFactory.TopLeft((RectTransform)screen.enhanceButton.transform, bx, 268, bw, 64);
        screen.evolveButton = UIFactory.CreateButton(right.transform, "EvolveButton", "EVOLVE", new Color(0.85f, 0.55f, 0.1f), 22);
        UIFactory.TopLeft((RectTransform)screen.evolveButton.transform, bx, 344, bw, 64);
        screen.favoriteButton = UIFactory.CreateButton(right.transform, "FavoriteButton", "FAVORITE", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.favoriteButton.transform, bx, 420, bw, 56);

        screen.xpBar = StatBar.Build(right.transform, "XPBar", UIFactory.Gold, 18);
        UIFactory.TopStrip((RectTransform)screen.xpBar.transform, 490, 30, 20, 20);

        screen.detailText = UIFactory.CreateText(right.transform, "DetailText", "", 21, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopStrip(screen.detailText.rectTransform, 532, 250, 22, 22);

        screen.messageText = UIFactory.CreateText(right.transform, "Message", "", 22, UIFactory.TextMain);
        UIFactory.Place(screen.messageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(620, 110));
        return screen;
    }
}
