using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Campaign map: locations on the left, the selected location's floors in the middle, and on the right the floor's
/// enemy, its rewards, the build slot to fight with (doc 10: pick a slot before each battle) and FIGHT.
/// </summary>
public class LocationMapScreen : MonoBehaviour
{
    public Button backButton;
    public Text titleText;
    public RectTransform locationList;
    public ScrollRect locationScroll;
    public Text locationHeader;
    public RectTransform floorGrid;
    public ScrollRect floorScroll;
    public CardView enemyPreview;
    public Text enemyInfo;
    public Button fightButton;
    public Text messageText;
    public Button[] slotButtons = new Button[3];
    public Button cardsButton;

    int selectedLocation = 1;
    int selectedFloor = 1;
    readonly List<Button> locationButtons = new List<Button>();
    readonly List<CardTile> floorTiles = new List<CardTile>();

    void Start()
    {
        backButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.MainMenu));
        fightButton.onClick.AddListener(OnFight);
        cardsButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Collection));
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i;
            if (slotButtons[i] != null) slotButtons[i].onClick.AddListener(() => SelectSlot(slot));
        }
        messageText.text = "";
        locationHeader.resizeTextForBestFit = true;
        locationHeader.resizeTextMinSize = 16;
        locationHeader.resizeTextMaxSize = locationHeader.fontSize;

        var db = CardDatabase.Instance;
        if (db == null || db.LocationCount == 0)
        {
            locationHeader.text = "Card database missing - run Tools > AniGame > Build Everything";
            fightButton.interactable = false;
            return;
        }

        selectedLocation = GameSession.SelectedLocation > 0 ? GameSession.SelectedLocation : PlayerProfile.HighestUnlockedLocation;
        if (!PlayerProfile.IsLocationUnlocked(selectedLocation)) selectedLocation = PlayerProfile.HighestUnlockedLocation;

        BuildLocationList();
        SelectLocation(selectedLocation);
        RefreshSlots();
        StartCoroutine(ScrollToSelectedLocation());
    }

    IEnumerator ScrollToSelectedLocation()
    {
        yield return null;
        var db = CardDatabase.Instance;
        if (locationScroll != null && db.LocationCount > 1)
            locationScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((selectedLocation - 1f) / (db.LocationCount - 1f));
    }

    void BuildLocationList()
    {
        var db = CardDatabase.Instance;
        UIFactory.Clear(locationList);
        locationButtons.Clear();
        foreach (var loc in db.locations)
        {
            int number = loc.number;
            bool unlocked = PlayerProfile.IsLocationUnlocked(number);
            bool cleared = PlayerProfile.IsLocationCleared(number);
            int done = PlayerProfile.ClearedFloors(number);

            Color color = cleared ? new Color(0.16f, 0.45f, 0.32f) : unlocked ? new Color(0.85f, 0.3f, 0.55f) : new Color(0.13f, 0.12f, 0.2f);
            var btn = UIFactory.CreateButton(locationList, $"LOC {number}", "", color, 24);
            UIFactory.SetLayoutSize(btn, -1, string.IsNullOrEmpty(loc.anime) ? 74 : 88);
            var label = UIFactory.ButtonLabel(btn);
            label.alignment = TextAnchor.MiddleLeft;
            label.resizeTextForBestFit = false;
            string state = cleared ? "<color=#7CFC6A>CLEARED</color>" : unlocked ? $"{done}/{loc.FloorCount}" : "<color=#9A93B5>LOCKED</color>";
            label.text = string.IsNullOrEmpty(loc.anime)
                ? $"  {loc.Title}   <size=20>{state}</size>"
                : $"  {loc.Title}   <size=20>{state}</size>\n  <size=17><color=#F3E3EE>{loc.anime}</color></size>";
            btn.interactable = unlocked;
            if (!unlocked)
            {
                var theme = UITheme.Instance;
                var lockImg = UIFactory.Icon(btn.transform, "Lock", theme != null ? theme.lockIcon : null, UIFactory.TextDim, 44f, Vector2.zero);
                var lrt = lockImg.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 0.5f);
                lrt.pivot = new Vector2(1f, 0.5f);
                lrt.anchoredPosition = new Vector2(-14f, 0f);
                TooltipTrigger.Add(lockImg, $"<b>LOCKED</b>\nClear every floor of LOC {number - 1} to unlock LOC {number}.");
            }
            btn.onClick.AddListener(() => SelectLocation(number));
            if (locationButtons.Count < 20) UIFx.PopIn((RectTransform)btn.transform, 0.15f + 0.03f * locationButtons.Count, 0.28f, 0.4f);
            locationButtons.Add(btn);
        }
    }

    void SelectLocation(int number)
    {
        var db = CardDatabase.Instance;
        var loc = db.GetLocation(number);
        if (loc == null) return;
        selectedLocation = number;
        GameSession.SelectedLocation = number;

        for (int i = 0; i < locationButtons.Count; i++)
        {
            var outline = locationButtons[i].GetComponent<Outline>();
            if (outline == null) outline = locationButtons[i].gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3, -3);
            outline.enabled = db.locations[i].number == number;
        }

        int cleared = PlayerProfile.ClearedFloors(number);
        int minPower = loc.floors.Where(c => c != null).Select(CardStats.EnemyPower).DefaultIfEmpty(0).Min();
        int maxPower = loc.floors.Where(c => c != null).Select(CardStats.EnemyPower).DefaultIfEmpty(0).Max();
        string anime = string.IsNullOrEmpty(loc.anime) ? "" : $"  <color=#FFD54F>{loc.anime}</color>";
        locationHeader.text = $"{loc.Title}{anime}   <size=22><color=#C9C3D8>{cleared}/{loc.FloorCount} cleared  |  Enemy power {minPower}-{maxPower}</color></size>";

        UIFactory.Clear(floorGrid);
        floorTiles.Clear();
        for (int f = 1; f <= loc.FloorCount; f++)
        {
            int floor = f;
            var card = loc.floors[f - 1];
            var tile = CardTile.Build(floorGrid, $"Floor {f}", 150, 210, false);
            tile.Set(card, $"F{f}", card != null ? CardStats.EnemyPower(card).ToString() : "");
            bool unlocked = PlayerProfile.IsFloorUnlocked(number, f);
            bool done = PlayerProfile.IsFloorCleared(number, f);
            if (!unlocked) tile.SetLocked(true, "LOCKED");
            if (!unlocked && tile.lockOverlayIcon != null) TooltipTrigger.Add(tile.lockOverlayIcon, $"<b>LOCKED</b>\nClear floor {f - 1} first.");
            else if (done) tile.SetBadge("CLEARED", new Color(0.2f, 0.6f, 0.35f));
            else tile.SetBadge("NEXT", UIFactory.Accent2);
            tile.button.onClick.AddListener(() => SelectFloor(floor));
            UIFx.PopIn((RectTransform)tile.transform, Mathf.Min(0.6f, 0.025f * floorTiles.Count), 0.26f);
            floorTiles.Add(tile);
        }

        if (floorScroll != null) floorScroll.verticalNormalizedPosition = 1f;
        SelectFloor(PlayerProfile.NextFloor(number));
    }

    void SelectFloor(int floor)
    {
        var db = CardDatabase.Instance;
        var card = db.GetFloorCard(selectedLocation, floor);
        if (card == null) return;
        selectedFloor = floor;
        for (int i = 0; i < floorTiles.Count; i++) floorTiles[i].SetSelected(i == floor - 1);

        enemyPreview.ShowEnemy(card);

        bool unlocked = PlayerProfile.IsFloorUnlocked(selectedLocation, floor);
        bool cleared = PlayerProfile.IsFloorCleared(selectedLocation, floor);
        string talent = card.talent != null ? "Hover the talent picture on the card to see what it does." : "No talent";
        float drop = card.rarity == CardRarity.God ? 0f : PlayerProfile.CardDropChance(card) * 100f;
        enemyInfo.text =
            (string.IsNullOrEmpty(card.anime) ? "" : $"<color=#FFD54F>{card.anime}</color>\n") +
            $"<b>LOC {selectedLocation} - Floor {floor}</b>   {(cleared ? "<color=#7CFC6A>Cleared</color>" : unlocked ? "<color=#FF6FA8>Not cleared</color>" : "<color=#6A6480>Locked</color>")}\n" +
            $"<color=#C9C3D8>{talent}</color>\n" +
            $"<color=#FFD54F>Win: +{GameRules.FloorGold(selectedLocation, floor):N0} gold  |  {drop:0.#}% {PlayerProfile.CharacterName} card  |  {GameRules.FusionOrbDropChance * 100f:0}% Fusion Orb" +
            (card.rarity == CardRarity.God ? $"  |  {GameRules.GodOrbEnemyDrop * 100f:0}% God Orb" : "") + "</color>" +
            (!cleared && unlocked ? $"\n<color=#7CFC6A>First clear: +{GameRules.StaminaNewFloor} stamina</color>" : "");

        fightButton.interactable = unlocked;
        UIFactory.ButtonLabel(fightButton).text = unlocked ? $"FIGHT  (-{PlayerProfile.StaminaCost} STAMINA)" : "LOCKED - clear the floor before it";
        messageText.text = "";
        RefreshSlots();
    }

    void RefreshSlots()
    {
        int active = PlayerProfile.ActiveSlot;
        for (int i = 0; i < slotButtons.Length; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;
            var slot = PlayerProfile.GetSlot(i);
            var label = UIFactory.ButtonLabel(btn);
            var img = btn.GetComponent<Image>();
            if (slot == null || !slot.unlocked)
            {
                label.text = $"SLOT {i + 1}\n<size=15>LOCKED</size>";
                img.color = new Color(0.12f, 0.11f, 0.18f);
                btn.interactable = false;
                continue;
            }
            var card = PlayerProfile.FindCard(slot.cardUid);
            btn.interactable = card != null;
            if (card == null)
            {
                label.text = $"SLOT {i + 1}\n<size=15>EMPTY</size>";
                img.color = new Color(0.16f, 0.15f, 0.24f);
                continue;
            }
            var art = PlayerProfile.Art(card);
            string rarity = $"<color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(card.rarity))}>{CardStats.RarityShort(card.rarity)}</color>";
            label.text = $"SLOT {i + 1}  {rarity}\n<size=15>{(art != null ? art.DisplayName : "?")}  P{CardStats.Power(card.rarity, slot.build)}</size>";
            img.color = i == active ? new Color(0.85f, 0.3f, 0.55f) : new Color(0.22f, 0.2f, 0.34f);
        }
    }

    void SelectSlot(int slot)
    {
        PlayerProfile.ActiveSlot = slot;
        RefreshSlots();
    }

    void OnFight()
    {
        if (PlayerProfile.SlotRuntime(PlayerProfile.ActiveSlot) == null)
        {
            messageText.text = "<color=#FF6B6B>Put a card in a build slot first (My Cards).</color>";
            return;
        }
        if (!PlayerProfile.TrySpendStamina(PlayerProfile.StaminaCost))
        {
            int s = PlayerProfile.SecondsToNextStamina;
            messageText.text = $"<color=#FF6B6B>Not enough stamina ({PlayerProfile.Data.stamina}/{PlayerProfile.StaminaCost}). Next +{PlayerProfile.StaminaPerTick} in {s / 60}:{s % 60:00}. Beat new floors for +{GameRules.StaminaNewFloor}.</color>";
            return;
        }
        GameSession.StartBattle(selectedLocation, selectedFloor);
    }

    // ---------------------------------------------------------------- layout

    public static LocationMapScreen BuildLayout(RectTransform canvas)
    {
        var screen = canvas.gameObject.AddComponent<LocationMapScreen>();
        AnimeBackground.Build(canvas, "map", new Color(0.45f, 0.65f, 1f, 0.7f), new Color(0.08f, 0.07f, 0.2f), out _);
        UIFactory.CreateTopBarWithCurrency(canvas, "WORLD MAP", out screen.backButton, out screen.titleText);

        var panelColor = new Color(0.06f, 0.05f, 0.12f, 0.95f);

        var left = UIFactory.CreatePanel(canvas, "LocationsPanel", panelColor, true);
        UIFactory.Region(left.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), 24, 120, 0, 24);
        left.rectTransform.sizeDelta = new Vector2(400, left.rectTransform.sizeDelta.y);
        left.rectTransform.anchoredPosition = new Vector2(224, left.rectTransform.anchoredPosition.y);
        var leftTitle = UIFactory.CreateText(left.transform, "Title", "LOCATIONS", 22, UIFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopStrip(leftTitle.rectTransform, 10, 34, 20, 20);
        screen.locationScroll = UIFactory.CreateScroll(left.transform, "LocationScroll", true, out RectTransform locContent);
        UIFactory.Stretch((RectTransform)screen.locationScroll.transform, 10, 50, 10, 10);
        UIFactory.AddVertical(locContent, 8, 6);
        screen.locationList = locContent;

        var mid = UIFactory.CreatePanel(canvas, "FloorsPanel", panelColor, true);
        UIFactory.Region(mid.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), 444, 120, 610, 24);
        screen.locationHeader = UIFactory.CreateText(mid.transform, "Header", "LOC 1", 34, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopStrip(screen.locationHeader.rectTransform, 10, 56, 24, 24);
        screen.floorScroll = UIFactory.CreateScroll(mid.transform, "FloorScroll", true, out RectTransform floorContent);
        UIFactory.Stretch((RectTransform)screen.floorScroll.transform, 14, 76, 14, 14);
        UIFactory.AddGrid(floorContent, new Vector2(150, 210), new Vector2(20, 34), 18);
        screen.floorGrid = floorContent;

        var right = UIFactory.CreatePanel(canvas, "EnemyPanel", panelColor, true);
        UIFactory.Region(right.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), 0, 120, 24, 24);
        right.rectTransform.sizeDelta = new Vector2(566, right.rectTransform.sizeDelta.y);
        right.rectTransform.anchoredPosition = new Vector2(-307, right.rectTransform.anchoredPosition.y);

        screen.enemyPreview = CardView.Build(right.transform, "EnemyPreview");
        var ep = (RectTransform)screen.enemyPreview.transform;
        UIFactory.Place(ep, new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(CardView.Width, CardView.Height));
        ep.localScale = Vector3.one * 0.66f;

        screen.enemyInfo = UIFactory.CreateText(right.transform, "EnemyInfo", "", 19, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopStrip(screen.enemyInfo.rectTransform, 436, 160, 22, 22);
        UIFactory.BestFit(screen.enemyInfo, 12);

        var slotLabel = UIFactory.CreateText(right.transform, "SlotLabel", "FIGHT WITH", 18, UIFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopStrip(slotLabel.rectTransform, 600, 26, 22, 22);
        for (int i = 0; i < 3; i++)
        {
            var b = UIFactory.CreateButton(right.transform, $"Slot{i + 1}", $"SLOT {i + 1}", UIFactory.PanelLight, 18);
            UIFactory.TopLeft((RectTransform)b.transform, 22 + i * 176, 630, 166, 72);
            var label = UIFactory.ButtonLabel(b);
            label.resizeTextForBestFit = false;
            label.fontSize = 18;
            screen.slotButtons[i] = b;
        }

        screen.fightButton = UIFactory.CreateButton(right.transform, "FightButton", "FIGHT", new Color(1f, 0.35f, 0.62f), 34);
        UIFactory.Place((RectTransform)screen.fightButton.transform, new Vector2(0.5f, 0f), new Vector2(0, 104), new Vector2(520, 88));
        screen.messageText = UIFactory.CreateText(right.transform, "Message", "", 19, UIFactory.TextMain);
        UIFactory.Place(screen.messageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 196), new Vector2(520, 36));

        screen.cardsButton = UIFactory.CreateButton(right.transform, "CardsButton", "MY CARDS", UIFactory.Accent, 22);
        UIFactory.Place((RectTransform)screen.cardsButton.transform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(520, 62));
        return screen;
    }
}
