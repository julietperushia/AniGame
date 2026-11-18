using UnityEngine;
using UnityEngine.UI;

/// <summary>Shop (design doc section 9): Fusion Orb 1,000 gold, Element Orb 3,500 gold, Talent Orb 7,000 gold.</summary>
public class ShopScreen : MonoBehaviour
{
    static readonly OrbType[] Items = { OrbType.Fusion, OrbType.Element, OrbType.Talent };
    static readonly int[] Quantities = { 1, 5, 10 };

    public Button backButton;
    public Text titleText;
    public Button gachaButton;
    public Button myCardsButton;
    public Text[] ownedTexts = new Text[3];
    public Button[] buyButtons = new Button[9]; // item * 3 + quantity index
    public Text messageText;

    void Start()
    {
        backButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.MainMenu));
        gachaButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Gacha));
        myCardsButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Collection));
        for (int i = 0; i < Items.Length; i++)
            for (int q = 0; q < Quantities.Length; q++)
            {
                var type = Items[i];
                int qty = Quantities[q];
                buyButtons[i * 3 + q].onClick.AddListener(() => Buy(type, qty));
            }
        messageText.text = "";
        PlayerProfile.Changed += Refresh;
        Refresh();
    }

    void OnDestroy() => PlayerProfile.Changed -= Refresh;

    void Buy(OrbType type, int qty)
    {
        string err = PlayerProfile.BuyOrbs(type, qty);
        if (err != null)
        {
            SfxSynth.Play("miss", 0.6f);
            messageText.text = $"<color=#FF6B6B>{err}.</color>";
            return;
        }
        SfxSynth.Play("buff", 0.8f);
        messageText.text = $"<color=#7CFC6A>Bought {qty}x {PlayerProfile.OrbName(type)} for {PlayerProfile.OrbShopPrice(type) * qty:N0} gold.</color>";
        Refresh();
    }

    void Refresh()
    {
        int gold = PlayerProfile.Data.gold;
        for (int i = 0; i < Items.Length; i++)
        {
            ownedTexts[i].text = $"Owned: <b>{PlayerProfile.GetOrbs(Items[i])}</b>" + (PlayerProfile.Has(Buffs.MerchantsFavor) ? "   <color=#7CFC6A>-5% Merchant's Favor</color>" : "");
            for (int q = 0; q < Quantities.Length; q++)
                {
                int price = PlayerProfile.OrbShopPrice(Items[i]) * Quantities[q];
                buyButtons[i * 3 + q].interactable = gold >= price;
                UIFactory.ButtonLabel(buyButtons[i * 3 + q]).text = $"x{Quantities[q]}\n<size=17>{price:N0} gold</size>";
            }
        }
    }

    static string Describe(OrbType type)
    {
        switch (type)
        {
            case OrbType.Fusion: return "Used for <b>Fusion</b>: main card + 2 fodder of the same rarity.\nCommon/Uncommon need 1 orb, Mythic needs 20.\nAlso a 5% drop from every floor win.";
            case OrbType.Element: return "Rerolls a card's <b>element</b> (9 elements).\nUsed before gold: with no orb a reroll costs 3,500 gold.\nOdds: cycle elements 13%, Dark/Light 11%, Neutral 8%, Non-Neutral 5%.";
            default: return "Rerolls a card's <b>talent</b>.\nUsed before gold: with no orb a reroll costs 7,000 gold.\nAny talent on any rarity; rarity makes it up to 20% stronger.";
        }
    }

    static Color ItemColor(OrbType type) =>
        type == OrbType.Fusion ? UIFactory.Hex("C78BFF") : type == OrbType.Element ? UIFactory.Hex("7CFC6A") : UIFactory.Hex("FFB23D");

    public static ShopScreen BuildLayout(RectTransform canvas)
    {
        var screen = canvas.gameObject.AddComponent<ShopScreen>();
        AnimeBackground.Build(canvas, "shop", new Color(1f, 0.75f, 0.45f, 0.65f), new Color(0.14f, 0.06f, 0.16f), out _);
        UIFactory.CreateTopBarWithCurrency(canvas, "SHOP", out screen.backButton, out screen.titleText);
        var theme = UIFactory.Theme;

        var subtitle = UIFactory.CreateText(canvas, "Subtitle", "ORB SHOP  <size=24><color=#FFE6C9>pay with gold • earn gold by clearing floors</color></size>", 40, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(subtitle.rectTransform, 122, 56, 40, 40);
        UIFactory.AddShadow(subtitle, UIFactory.ShadowColor, 3f);

        const float w = 520f, h = 700f, gap = 40f;
        float total = Items.Length * w + (Items.Length - 1) * gap;
        for (int i = 0; i < Items.Length; i++)
        {
            var type = Items[i];
            var color = ItemColor(type);
            var panel = UIFactory.CreatePanel(canvas, $"Item {type}", new Color(0.08f, 0.06f, 0.14f, 0.92f), true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(-total / 2f + w / 2f + i * (w + gap), -200f), new Vector2(w, h));

            var header = UIFactory.CreatePanel(panel.transform, "Header", Color.Lerp(new Color(0.1f, 0.06f, 0.2f), color, 0.3f), false);
            UIFactory.TopStrip(header.rectTransform, 14, 290, 14, 14);
            var glow = UIFactory.CreateImage(header.transform, "Glow", UIFactory.WithAlpha(color, 0.6f), theme != null ? theme.glow : null);
            UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380, 380));
            if (theme != null && theme.halftone != null)
            {
                var ht = UIFactory.CreateImage(header.transform, "Halftone", new Color(1, 1, 1, 0.07f), theme.halftone);
                ht.type = Image.Type.Tiled;
                UIFactory.Stretch(ht.rectTransform);
            }
            var iconSprite = theme != null ? theme.OrbIcon(type) : null;
            var icon = UIFactory.CreateImage(header.transform, "Icon", iconSprite != null ? Color.white : color, iconSprite != null ? iconSprite : (theme != null ? theme.circle : null));
            icon.preserveAspect = true;
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190, 190));
            TooltipTrigger.Add(icon, ItemInfo.Describe(ItemInfo.FromOrb(type)));

            var name = UIFactory.CreateText(panel.transform, "Name", PlayerProfile.OrbName(type).ToUpperInvariant(), 38, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.TopStrip(name.rectTransform, 316, 50, 10, 10);
            UIFactory.AddShadow(name, UIFactory.ShadowColor, 3f);
            var price = UIFactory.CreateText(panel.transform, "Price", $"{PlayerProfile.OrbPrice(type):N0} gold each", 26, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.TopStrip(price.rectTransform, 366, 36, 10, 10);
            var desc = UIFactory.CreateText(panel.transform, "Description", Describe(type), 20, UIFactory.TextDim, TextAnchor.UpperCenter);
            UIFactory.TopStrip(desc.rectTransform, 410, 110, 22, 22);
            UIFactory.BestFit(desc, 12);
            screen.ownedTexts[i] = UIFactory.CreateText(panel.transform, "Owned", "", 24, UIFactory.TextMain, TextAnchor.MiddleCenter);
            UIFactory.TopStrip(screen.ownedTexts[i].rectTransform, 528, 36, 10, 10);

            float bw = (w - 28 - 2 * 12) / 3f;
            for (int q = 0; q < Quantities.Length; q++)
            {
                int qty = Quantities[q];
                var b = UIFactory.CreateButton(panel.transform, $"Buy x{qty}", $"x{qty}\n<size=17>{PlayerProfile.OrbPrice(type) * qty:N0} gold</size>", Color.Lerp(UIFactory.Accent, color, 0.4f), 26);
                UIFactory.TopLeft((RectTransform)b.transform, 14 + q * (bw + 12), 586, bw, 92);
                screen.buyButtons[i * 3 + q] = b;
            }
        }

        screen.messageText = UIFactory.CreateText(canvas, "Message", "", 28, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(screen.messageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(1000, 56));
        var note = UIFactory.CreateText(canvas, "Note", "Diamonds come from gold packs. God Orbs are never sold: God floors and diamond packs only.", 21, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(60, 36), new Vector2(900, 40));
        note.rectTransform.pivot = new Vector2(0f, 0f);
        screen.gachaButton = UIFactory.CreateButton(canvas, "Gacha", "GACHA", UIFactory.Accent2, 26);
        UIFactory.Place((RectTransform)screen.gachaButton.transform, new Vector2(1f, 0f), new Vector2(-470, 64), new Vector2(260, 72));
        screen.myCardsButton = UIFactory.CreateButton(canvas, "MyCards", "MY CARDS", UIFactory.Accent, 26);
        UIFactory.Place((RectTransform)screen.myCardsButton.transform, new Vector2(1f, 0f), new Vector2(-170, 64), new Vector2(280, 72));
        return screen;
    }
}
