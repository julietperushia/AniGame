using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Permanent buffs: bought once, kept forever. Four groups; economy and stamina buffs cost gold, the rest diamonds.</summary>
public class BuffsScreen : MonoBehaviour
{
    public Button backButton;
    public Text titleText;
    public Text messageText;
    public Text summaryText;
    public Button[] buyButtons;
    public Image[] cardBackgrounds;
    public Text[] priceTexts;

    int confirmIndex = -1;
    float confirmUntil;

    void Start()
    {
        backButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.MainMenu));
        for (int i = 0; i < buyButtons.Length && i < Buffs.All.Length; i++)
        {
            int index = i;
            buyButtons[i].onClick.AddListener(() => OnBuy(index));
        }
        messageText.text = "Click a buff's price, then click again to confirm.";
        PlayerProfile.Changed += Refresh;
        Refresh();
    }

    void OnDestroy() => PlayerProfile.Changed -= Refresh;

    void Update()
    {
        if (confirmIndex >= 0 && Time.unscaledTime > confirmUntil) { confirmIndex = -1; Refresh(); }
    }

    static string Price(BuffDefinition b) =>
        b.currency == BuffCurrency.Gold ? $"<color=#FFD54F>{b.price:N0} gold</color>" : $"<color=#6FD3FF>{b.price:N0} diamonds</color>";

    void Refresh()
    {
        if (this == null) return;
        int owned = 0;
        for (int i = 0; i < buyButtons.Length && i < Buffs.All.Length; i++)
        {
            var b = Buffs.All[i];
            bool has = PlayerProfile.Has(b.id);
            if (has) owned++;
            string reason = PlayerProfile.CanBuyBuff(b);
            buyButtons[i].interactable = !has && reason == null;
            var label = UIFactory.ButtonLabel(buyButtons[i]);
            label.text = has ? "OWNED" : confirmIndex == i ? "CONFIRM?" : (b.currency == BuffCurrency.Gold ? $"{b.price:N0} G" : $"{b.price:N0} D");
            buyButtons[i].image.color = has ? new Color(0.25f, 0.6f, 0.35f) : confirmIndex == i ? UIFactory.Accent2 : (b.currency == BuffCurrency.Gold ? new Color(0.75f, 0.55f, 0.12f) : new Color(0.2f, 0.55f, 0.85f));
            if (cardBackgrounds != null && i < cardBackgrounds.Length && cardBackgrounds[i] != null)
                cardBackgrounds[i].color = has ? new Color(0.1f, 0.2f, 0.14f, 0.95f) : new Color(0.08f, 0.07f, 0.15f, 0.95f);
            if (priceTexts != null && i < priceTexts.Length && priceTexts[i] != null)
                priceTexts[i].text = has ? "<color=#7CFC6A>Active</color>" : Price(b);
        }
        summaryText.text = $"{owned} / {Buffs.All.Length} buffs owned";
    }

    void OnBuy(int index)
    {
        var b = Buffs.All[index];
        if (confirmIndex != index)
        {
            confirmIndex = index;
            confirmUntil = Time.unscaledTime + 3f;
            messageText.text = $"Buy <b>{b.name}</b> for {Price(b)}? Click again to confirm.";
            Refresh();
            return;
        }
        confirmIndex = -1;
        string err = PlayerProfile.BuyBuff(b.id);
        if (err != null)
        {
            SfxSynth.Play("miss", 0.6f);
            messageText.text = $"<color=#FF6B6B>{err}.</color>";
        }
        else
        {
            SfxSynth.Play("win", 0.7f);
            messageText.text = $"<color=#7CFC6A><b>{b.name}</b> is now active forever!</color>";
        }
        Refresh();
        BuyFx(index, err == null, b);
    }

    void BuyFx(int index, bool bought, BuffDefinition b)
    {
        if (index < 0 || index >= buyButtons.Length || buyButtons[index] == null) return;
        var brt = (RectTransform)buyButtons[index].transform;
        if (!bought) { UIFx.Shake(brt, 0.3f, 8f); return; }
        var color = b.currency == BuffCurrency.Gold ? UIFactory.Gold : new Color(0.45f, 0.8f, 1f);
        var card = cardBackgrounds != null && index < cardBackgrounds.Length ? cardBackgrounds[index] : null;
        var target = card != null ? card.rectTransform : brt;
        UIFx.BurstAt(target, color, 0.9f);
        UIFx.FloatText(UIFx.Overlay, UIFx.LocalPos(UIFx.Overlay, target, new Vector2(0f, 40f)), "UNLOCKED!", new Color(0.55f, 1f, 0.6f), 32);
        if (card != null) { UIFx.FlashGraphic(card, new Color(0.6f, 1f, 0.7f, 1f), 0.6f); UIFx.PopScale(card.rectTransform, 1.08f, 0.3f); }
        SfxSynth.Play("sparkle", 0.6f);
    }

    // ---------------------------------------------------------------- layout

    public static BuffsScreen BuildLayout(RectTransform canvas)
    {
        var screen = canvas.gameObject.AddComponent<BuffsScreen>();
        AnimeBackground.Build(canvas, "gacha", new Color(0.6f, 0.5f, 1f, 0.7f), new Color(0.08f, 0.05f, 0.18f), out _);
        UIFactory.CreateTopBarWithCurrency(canvas, "BUFFS", out screen.backButton, out screen.titleText);
        var theme = UIFactory.Theme;

        int n = Buffs.All.Length;
        screen.buyButtons = new Button[n];
        screen.cardBackgrounds = new Image[n];
        screen.priceTexts = new Text[n];

        var groups = new[] { BuffGroup.Fusion, BuffGroup.Collection, BuffGroup.Economy, BuffGroup.Combat };
        const float colW = 448f, gap = 18f;
        float total = groups.Length * colW + (groups.Length - 1) * gap;
        for (int g = 0; g < groups.Length; g++)
        {
            var group = groups[g];
            var col = UIFactory.CreatePanel(canvas, $"Group {group}", new Color(0.04f, 0.03f, 0.09f, 0.88f), true);
            UIFactory.Place(col.rectTransform, new Vector2(0.5f, 1f), new Vector2(-total / 2f + colW / 2f + g * (colW + gap), -116f), new Vector2(colW, 860f));
            var header = UIFactory.CreateText(col.transform, "Header", Buffs.GroupName(group).ToUpperInvariant(), 26, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.TopStrip(header.rectTransform, 10, 40, 10, 10);
            UIFactory.AddShadow(header, UIFactory.ShadowColor, 2f);
            string hint = group == BuffGroup.Economy ? "paid with gold" : group == BuffGroup.Combat ? "stamina buffs: gold, others: diamonds" : "paid with diamonds";
            var sub = UIFactory.CreateText(col.transform, "Hint", hint, 17, UIFactory.TextDim, TextAnchor.MiddleCenter, FontStyle.Italic);
            UIFactory.TopStrip(sub.rectTransform, 48, 24, 10, 10);

            var scroll = UIFactory.CreateScroll(col.transform, "List", true, out RectTransform content);
            UIFactory.Stretch((RectTransform)scroll.transform, 8, 78, 8, 8);
            var v = UIFactory.AddVertical(content, 10f, 4);
            v.childControlHeight = true;

            for (int i = 0; i < n; i++)
            {
                var b = Buffs.All[i];
                if (b.group != group) continue;
                var card = UIFactory.CreatePanel(content, b.id, new Color(0.08f, 0.07f, 0.15f, 0.95f), true);
                UIFactory.SetLayoutSize(card, colW - 24, 112f);
                screen.cardBackgrounds[i] = card;
                var icon = UIFactory.CreateImage(card.transform, "Icon", Color.white, theme != null ? theme.BuffIcon(b.id) : null);
                icon.preserveAspect = true;
                UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(10, 0), new Vector2(84, 84));
                TooltipTrigger.Add(icon, $"<b>{b.name}</b>\n{b.description}");
                var name = UIFactory.CreateText(card.transform, "Name", b.name, 21, UIFactory.TextMain, TextAnchor.UpperLeft, FontStyle.Bold);
                UIFactory.TopLeft(name.rectTransform, 104, 8, 200, 28);
                UIFactory.BestFit(name, 13);
                var desc = UIFactory.CreateText(card.transform, "Description", b.description, 16, UIFactory.TextDim, TextAnchor.UpperLeft);
                UIFactory.TopLeft(desc.rectTransform, 104, 36, 200, 70);
                UIFactory.BestFit(desc, 11);
                screen.priceTexts[i] = UIFactory.CreateText(card.transform, "Price", "", 15, UIFactory.TextMain, TextAnchor.MiddleCenter);
                UIFactory.Place(screen.priceTexts[i].rectTransform, new Vector2(1f, 1f), new Vector2(-8, -8), new Vector2(112, 24));
                UIFactory.BestFit(screen.priceTexts[i], 10);
                var buy = UIFactory.CreateButton(card.transform, "Buy", "BUY", UIFactory.Accent, 18);
                UIFactory.Place((RectTransform)buy.transform, new Vector2(1f, 0f), new Vector2(-8, 10), new Vector2(112, 56));
                screen.buyButtons[i] = buy;
            }
        }

        screen.summaryText = UIFactory.CreateText(canvas, "Summary", "", 22, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.Place(screen.summaryText.rectTransform, new Vector2(0f, 0f), new Vector2(40, 30), new Vector2(500, 50));
        screen.messageText = UIFactory.CreateText(canvas, "Message", "", 24, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(screen.messageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1100, 50));
        return screen;
    }
}
