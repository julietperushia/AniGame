using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Gacha (design doc section 7): 6 packs, 5 items each, slot 1 is a guaranteed card of the pack's rarity.</summary>
public class GachaScreen : MonoBehaviour
{
    public Button backButton;
    public Text titleText;
    public Button myCardsButton;
    public Text infoText;
    public Text messageText;
    public Button[] openButtons = new Button[6];
    public Button[] oddsButtons = new Button[6];
    public Text[] priceTexts = new Text[6];
    public CardFrame[] packFrames = new CardFrame[6];

    [Header("Odds")]
    public GameObject oddsPanel;
    public Text oddsTitle;
    public Text oddsText;
    public Button oddsCloseButton;

    [Header("Reveal")]
    public GameObject revealPanel;
    public Text revealTitle;
    public RectTransform revealRow;
    public Text revealSummary;
    public Button revealAllButton;
    public Button openAgainButton;
    public Button revealCloseButton;
    public Button revealCardsButton;

    PackDefinition lastPack;
    readonly List<RectTransform> backs = new List<RectTransform>();
    readonly List<RectTransform> fronts = new List<RectTransform>();
    readonly List<PackItem> shownItems = new List<PackItem>();
    Coroutine revealRoutine;

    void Start()
    {
        backButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.MainMenu));
        myCardsButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Collection));
        for (int i = 0; i < Packs.All.Length && i < openButtons.Length; i++)
        {
            var pack = Packs.All[i];
            openButtons[i].onClick.AddListener(() => Open(pack));
            oddsButtons[i].onClick.AddListener(() => ShowOdds(pack));
            if (i < packFrames.Length && packFrames[i] != null) packFrames[i].SetRarity(pack.guaranteed);
        }
        oddsCloseButton.onClick.AddListener(() => oddsPanel.SetActive(false));
        revealAllButton.onClick.AddListener(RevealAll);
        openAgainButton.onClick.AddListener(() => { if (lastPack != null) Open(lastPack); });
        revealCloseButton.onClick.AddListener(() => { revealPanel.SetActive(false); Refresh(); });
        revealCardsButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Collection));
        oddsPanel.SetActive(false);
        revealPanel.SetActive(false);
        messageText.text = "";
        PlayerProfile.Changed += Refresh;
        Refresh();
    }

    void OnDestroy() => PlayerProfile.Changed -= Refresh;

    void Refresh()
    {
        var d = PlayerProfile.Data;
        for (int i = 0; i < Packs.All.Length && i < openButtons.Length; i++)
        {
            var pack = Packs.All[i];
            bool can = PlayerProfile.CanOpenPack(pack) == null;
            openButtons[i].interactable = can;
            priceTexts[i].color = can ? UIFactory.TextMain : UIFactory.Bad;
        }
        infoText.text = $"Packs opened: {d.packsOpened}    Cards owned: {d.cards.Count}\n" +
                        "<color=#9AA4B5>Gold packs can drop Diamonds. Diamond packs give higher rarities.</color>";
    }

    // ---------------------------------------------------------------- labels

    public static string ItemLabel(RewardType type, int amount, CardRarity rarity)
    {
        switch (type)
        {
            case RewardType.Card: return $"{CardStats.RarityName(rarity)} card";
            case RewardType.Gold: return $"{amount:N0} Gold";
            case RewardType.Diamonds: return $"{amount:N0} Diamonds";
            case RewardType.FusionOrb: return amount == 1 ? "1 Fusion Orb" : $"{amount} Fusion Orbs";
            case RewardType.ElementOrb: return amount == 1 ? "1 Element Orb" : $"{amount} Element Orbs";
            case RewardType.TalentOrb: return amount == 1 ? "1 Talent Orb" : $"{amount} Talent Orbs";
        }
        return type.ToString();
    }

    public static Sprite ItemIcon(RewardType type)
    {
        var t = UIFactory.Theme;
        if (t == null) return null;
        switch (type)
        {
            case RewardType.Gold: return t.goldIcon;
            case RewardType.Diamonds: return t.diamondIcon;
            case RewardType.FusionOrb: return t.fusionOrbIcon;
            case RewardType.ElementOrb: return t.elementOrbIcon;
            case RewardType.TalentOrb: return t.talentOrbIcon;
        }
        return null;
    }

    public static Color ItemColor(RewardType type)
    {
        switch (type)
        {
            case RewardType.Gold: return UIFactory.Gold;
            case RewardType.Diamonds: return UIFactory.Hex("6FD3FF");
            case RewardType.FusionOrb: return UIFactory.Hex("C78BFF");
            case RewardType.ElementOrb: return UIFactory.Hex("7CFC6A");
            case RewardType.TalentOrb: return UIFactory.Hex("FFB23D");
        }
        return UIFactory.TextMain;
    }

    static string PriceLabel(PackDefinition p) =>
        p.currency == PackCurrency.Gold ? $"<color=#FFD54F>{p.price:N0} Gold</color>" : $"<color=#6FD3FF>{p.price:N0} Diamonds</color>";

    // ---------------------------------------------------------------- odds

    void ShowOdds(PackDefinition pack)
    {
        oddsTitle.text = pack.name.ToUpperInvariant() + "  ODDS";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Price: {PriceLabel(pack)}    Items: {PackDefinition.ItemCount}\n");
        sb.AppendLine($"<b>Item 1</b>: guaranteed <color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(pack.guaranteed))}><b>{CardStats.RarityName(pack.guaranteed)} card</b></color>\n");
        sb.AppendLine("<b>Items 2-5</b> (each rolled on its own):");
        foreach (var e in pack.entries.OrderByDescending(e => e.chance))
        {
            string label = ItemLabel(e.type, e.amount, e.rarity);
            string color = e.type == RewardType.Card ? ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(e.rarity)) : ColorUtility.ToHtmlStringRGB(ItemColor(e.type));
            sb.AppendLine($"   <color=#{color}>{label}</color>  <color=#9AA4B5>{e.chance:0.##}%</color>");
        }
        if (pack.ExpectedDiamonds > 0f) sb.AppendLine($"\n<color=#9AA4B5>Average diamonds per pack: {pack.ExpectedDiamonds:0.#}</color>");
        sb.AppendLine("\n<color=#9AA4B5>Cards get a random character of that rarity, a random Roll (± by rarity), element and talent.</color>");
        oddsText.text = sb.ToString();
        oddsPanel.SetActive(true);
    }

    // ---------------------------------------------------------------- open + reveal

    void Open(PackDefinition pack)
    {
        string err = PlayerProfile.CanOpenPack(pack);
        if (err != null) { messageText.text = $"<color=#FF6B6B>{err}.</color>"; return; }
        var items = PlayerProfile.OpenPack(pack);
        if (items == null) return;
        lastPack = pack;
        messageText.text = "";
        SfxSynth.Play("buff");
        ShowReveal(pack, items);
    }

    void ShowReveal(PackDefinition pack, List<PackItem> items)
    {
        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealPanel.SetActive(true);
        revealTitle.text = pack.name.ToUpperInvariant();
        revealTitle.color = CardStats.RarityColor(pack.guaranteed);
        UIFactory.Clear(revealRow);
        backs.Clear();
        fronts.Clear();
        shownItems.Clear();
        shownItems.AddRange(items);

        const float w = 250f, h = 350f, gap = 34f;
        float total = items.Count * w + (items.Count - 1) * gap;
        for (int i = 0; i < items.Count; i++)
        {
            var holder = UIFactory.CreateRect($"Item {i + 1}", revealRow);
            UIFactory.Place(holder, new Vector2(0.5f, 0.5f), new Vector2(-total / 2f + w / 2f + i * (w + gap), 0), new Vector2(w, h));
            var front = BuildItemFront(holder, items[i], w, h);
            front.gameObject.SetActive(false);
            var back = BuildBack(holder, pack, i, w, h);
            backs.Add(back);
            fronts.Add(front);
        }

        revealSummary.text = "";
        openAgainButton.interactable = PlayerProfile.CanOpenPack(pack) == null;
        UIFactory.ButtonLabel(openAgainButton).text = $"OPEN AGAIN  <size=18>({StripColor(PriceLabel(pack))})</size>";
        revealAllButton.gameObject.SetActive(true);
        revealRoutine = StartCoroutine(RevealSequence());
    }

    static string StripColor(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");

    RectTransform BuildBack(RectTransform holder, PackDefinition pack, int index, float w, float h)
    {
        var color = CardStats.RarityColor(pack.guaranteed);
        var back = UIFactory.CreatePanel(holder, "Back", Color.Lerp(new Color(0.08f, 0.06f, 0.16f), color, 0.35f), false);
        UIFactory.Stretch(back.rectTransform);
        var theme = UIFactory.Theme;
        int packIndex = System.Array.IndexOf(Packs.All, pack);
        var art = theme != null ? theme.PackArt(packIndex) : null;
        if (art != null)
        {
            var img = UIFactory.CreateImage(back.transform, "PackArt", Color.white, art);
            UIFactory.Stretch(img.rectTransform, 8, 8, 8, 8);
            img.preserveAspect = false;
        }
        var q = UIFactory.CreateText(back.transform, "Mark", "?", 140, UIFactory.WithAlpha(Color.white, 0.85f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(q.rectTransform);
        UIFactory.AddShadow(q, UIFactory.ShadowColor, 4f);
        var label = UIFactory.CreateText(back.transform, "Label", index == 0 ? "GUARANTEED" : $"ITEM {index + 1}", 20, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(w, 32));
        var frameSprite = theme != null ? theme.Frame(pack.guaranteed) : null;
        if (frameSprite != null)
        {
            var frame = UIFactory.CreateImage(back.transform, "Frame", Color.white, frameSprite, true);
            UIFactory.Stretch(frame.rectTransform, -6, -6, -6, -6);
        }
        return back.rectTransform;
    }

    RectTransform BuildItemFront(RectTransform holder, PackItem item, float w, float h)
    {
        if (item.type == RewardType.Card && item.card != null)
        {
            var tile = CardTile.Build(holder, "Card", w, h, true);
            UIFactory.Stretch((RectTransform)tile.transform);
            tile.SetInstance(item.card);
            tile.nameText.fontSize = 20;
            tile.SetBadge("NEW", UIFactory.Accent2);
            return (RectTransform)tile.transform;
        }

        var color = ItemColor(item.type);
        var panel = UIFactory.CreatePanel(holder, "Reward", new Color(0.08f, 0.07f, 0.15f, 0.97f), false);
        UIFactory.Stretch(panel.rectTransform);
        var glow = UIFactory.CreateImage(panel.transform, "Glow", UIFactory.WithAlpha(color, 0.35f), UIFactory.Theme != null ? UIFactory.Theme.glow : null);
        UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(w * 1.1f, w * 1.1f));
        var iconSprite = ItemIcon(item.type);
        var icon = UIFactory.CreateImage(panel.transform, "Icon", iconSprite != null ? Color.white : color, iconSprite != null ? iconSprite : (UIFactory.Theme != null ? UIFactory.Theme.circle : null));
        icon.preserveAspect = true;
        UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(w * 0.5f, w * 0.5f));
        var amount = UIFactory.CreateText(panel.transform, "Amount", item.amount >= 1000 ? $"{item.amount:N0}" : $"x{item.amount}", 44, color, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(amount.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 86), new Vector2(w, 56));
        UIFactory.AddShadow(amount, UIFactory.ShadowColor, 3f);
        var label = UIFactory.CreateText(panel.transform, "Label", ItemLabel(item.type, item.amount, item.rarity).ToUpperInvariant(), 18, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 36), new Vector2(w - 16, 40));
        UIFactory.BestFit(label, 11);
        return panel.rectTransform;
    }

    IEnumerator RevealSequence()
    {
        yield return new WaitForSecondsRealtime(0.35f);
        for (int i = 0; i < backs.Count; i++)
        {
            if (!backs[i].gameObject.activeSelf) continue;
            yield return Flip(i);
            yield return new WaitForSecondsRealtime(0.12f);
        }
        Finish();
    }

    IEnumerator Flip(int i)
    {
        var back = backs[i];
        var front = fronts[i];
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.14f;
            back.localScale = new Vector3(Mathf.Lerp(1f, 0f, t), 1f, 1f);
            yield return null;
        }
        ShowFront(i);
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.16f;
            float s = Mathf.Lerp(0f, 1f, t);
            front.localScale = new Vector3(s, 1f + 0.08f * Mathf.Sin(Mathf.PI * t), 1f);
            yield return null;
        }
        front.localScale = Vector3.one;
    }

    void ShowFront(int i)
    {
        backs[i].gameObject.SetActive(false);
        fronts[i].gameObject.SetActive(true);
        var item = shownItems[i];
        if (item.type == RewardType.Card)
        {
            int tier = GameRules.Tier(item.rarity);
            SfxSynth.Play(tier >= 5 ? "skill" : tier >= 3 ? "buff" : "click", 0.8f, 1f + 0.05f * tier);
        }
        else SfxSynth.Play(item.type == RewardType.Diamonds ? "heal" : "click", 0.7f);
    }

    void RevealAll()
    {
        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealRoutine = null;
        for (int i = 0; i < backs.Count; i++)
        {
            if (backs[i].gameObject.activeSelf) ShowFront(i);
            fronts[i].localScale = Vector3.one;
        }
        Finish();
    }

    void Finish()
    {
        revealRoutine = null;
        revealAllButton.gameObject.SetActive(false);
        var best = shownItems.Where(x => x.type == RewardType.Card).Select(x => GameRules.Tier(x.rarity)).DefaultIfEmpty(0).Max();
        if (best >= 6) SfxSynth.Play("win");
        var parts = new List<string>();
        foreach (var g in shownItems.GroupBy(x => x.type))
        {
            if (g.Key == RewardType.Card)
                parts.Add(string.Join(", ", g.GroupBy(x => x.rarity).OrderByDescending(r => r.Key)
                    .Select(r => $"<color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(r.Key))}>{r.Count()}x {CardStats.RarityName(r.Key)}</color>")));
            else
                parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(ItemColor(g.Key))}>{ItemLabel(g.Key, g.Sum(x => x.amount), CardRarity.Common)}</color>");
        }
        revealSummary.text = "You got: " + string.Join("   ", parts);
        openAgainButton.interactable = lastPack != null && PlayerProfile.CanOpenPack(lastPack) == null;
    }

    // ---------------------------------------------------------------- layout

    public static GachaScreen BuildLayout(RectTransform canvas)
    {
        var screen = canvas.gameObject.AddComponent<GachaScreen>();
        AnimeBackground.Build(canvas, "gacha", new Color(1f, 0.55f, 0.85f, 0.75f), new Color(0.12f, 0.05f, 0.22f), out _);
        UIFactory.CreateTopBarWithCurrency(canvas, "GACHA", out screen.backButton, out screen.titleText);

        var subtitle = UIFactory.CreateText(canvas, "Subtitle", "SUMMON PACKS  <size=24><color=#F3D9FF>5 items per pack • item 1 is a guaranteed card</color></size>", 40, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(subtitle.rectTransform, 118, 56, 40, 40);
        UIFactory.AddShadow(subtitle, UIFactory.ShadowColor, 3f);

        const float pw = 284f, ph = 690f, gap = 20f;
        float total = Packs.All.Length * pw + (Packs.All.Length - 1) * gap;
        var theme = UIFactory.Theme;
        for (int i = 0; i < Packs.All.Length; i++)
        {
            var pack = Packs.All[i];
            var color = CardStats.RarityColor(pack.guaranteed);
            int tier = GameRules.Tier(pack.guaranteed);

            var panel = UIFactory.CreatePanel(canvas, $"Pack {pack.id}", new Color(0.07f, 0.05f, 0.14f, 0.92f), true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(-total / 2f + pw / 2f + i * (pw + gap), -190f), new Vector2(pw, ph));

            // Pack art (user art if provided, else a rarity-coloured placeholder with the frame)
            var frameGlow = UIFactory.CreateImage(panel.transform, "FrameGlow", UIFactory.WithAlpha(color, 0f), theme != null ? theme.glow : null);
            UIFactory.TopStrip(frameGlow.rectTransform, -26, 432, -26, -26);
            var artHolder = UIFactory.CreatePanel(panel.transform, "ArtArea", Color.Lerp(new Color(0.1f, 0.06f, 0.2f), color, 0.45f), false);
            UIFactory.TopStrip(artHolder.rectTransform, 14, 380, 14, 14);
            var glow = UIFactory.CreateImage(artHolder.transform, "Glow", UIFactory.WithAlpha(color, 0.55f), theme != null ? theme.glow : null);
            UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 360));
            if (theme != null && theme.halftone != null)
            {
                var ht = UIFactory.CreateImage(artHolder.transform, "Halftone", new Color(1, 1, 1, 0.08f), theme.halftone);
                ht.type = Image.Type.Tiled;
                UIFactory.Stretch(ht.rectTransform);
            }
            var packArt = theme != null ? theme.PackArt(i) : null;
            if (packArt != null)
            {
                var img = UIFactory.CreateImage(artHolder.transform, "PackArt", Color.white, packArt);
                UIFactory.Stretch(img.rectTransform);
            }
            else
            {
                var big = UIFactory.CreateText(artHolder.transform, "RarityMark", CardStats.RarityShort(pack.guaranteed), 92, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Stretch(big.rectTransform, 0, 0, 0, 40);
                UIFactory.AddOutline(big, UIFactory.WithAlpha(CardStats.RarityColorDark(pack.guaranteed), 0.9f), 3f);
                UIFactory.AddShadow(big, UIFactory.ShadowColor, 5f);
                var packWord = UIFactory.CreateText(artHolder.transform, "PackWord", "PACK", 34, UIFactory.WithAlpha(Color.white, 0.9f), TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Place(packWord.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(240, 50));
            }
            var body = artHolder.rectTransform;
            screen.packFrames[i] = CardFrame.Attach(body, frameGlow, body, 6f);
            screen.packFrames[i].SetRarity(pack.guaranteed);

            var name = UIFactory.CreateText(panel.transform, "Name", pack.name.ToUpperInvariant(), 28, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.TopStrip(name.rectTransform, 404, 44, 8, 8);
            UIFactory.AddShadow(name, UIFactory.ShadowColor, 2f);
            UIFactory.BestFit(name, 16);
            var guaranteed = UIFactory.CreateText(panel.transform, "Guaranteed", $"Guaranteed <b>{CardStats.RarityName(pack.guaranteed)}</b> card\n+ 4 random items", 19, UIFactory.TextDim, TextAnchor.MiddleCenter);
            UIFactory.TopStrip(guaranteed.rectTransform, 450, 56, 10, 10);

            screen.priceTexts[i] = UIFactory.CreateText(panel.transform, "Price", "", 26, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.TopStrip(screen.priceTexts[i].rectTransform, 512, 40, 10, 10);
            var priceIcon = pack.currency == PackCurrency.Gold ? (theme != null ? theme.goldIcon : null) : (theme != null ? theme.diamondIcon : null);
            if (priceIcon != null)
            {
                screen.priceTexts[i].text = $"      {pack.price:N0}";
                UIFactory.Icon(screen.priceTexts[i].transform, "Icon", priceIcon, Color.white, 32, new Vector2(pw / 2f - 70, 0));
            }
            else screen.priceTexts[i].text = PriceLabel(pack);

            screen.openButtons[i] = UIFactory.CreateButton(panel.transform, "Open", "OPEN", Color.Lerp(UIFactory.Accent2, color, 0.35f), 28);
            UIFactory.TopLeft((RectTransform)screen.openButtons[i].transform, 16, 562, pw - 32, 62);
            screen.oddsButtons[i] = UIFactory.CreateButton(panel.transform, "Odds", "ODDS", UIFactory.PanelLight, 20);
            UIFactory.TopLeft((RectTransform)screen.oddsButtons[i].transform, 16, 632, pw - 32, 44);
        }

        screen.infoText = UIFactory.CreateText(canvas, "Info", "", 22, UIFactory.TextMain, TextAnchor.MiddleLeft);
        UIFactory.Place(screen.infoText.rectTransform, new Vector2(0f, 0f), new Vector2(60, 30), new Vector2(1100, 90));
        screen.infoText.rectTransform.pivot = new Vector2(0f, 0f);
        screen.messageText = UIFactory.CreateText(canvas, "Message", "", 26, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(screen.messageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(200, 60), new Vector2(700, 50));
        screen.myCardsButton = UIFactory.CreateButton(canvas, "MyCards", "MY CARDS", UIFactory.Accent, 26);
        UIFactory.Place((RectTransform)screen.myCardsButton.transform, new Vector2(1f, 0f), new Vector2(-190, 64), new Vector2(300, 72));

        BuildReveal(canvas, screen);
        BuildOdds(canvas, screen);
        return screen;
    }

    static void BuildOdds(RectTransform canvas, GachaScreen screen)
    {
        var dim = UIFactory.CreateImage(canvas, "OddsPanel", new Color(0, 0, 0, 0.75f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        screen.oddsPanel = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.09f, 0.07f, 0.17f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 820));
        screen.oddsTitle = UIFactory.CreateText(panel.transform, "Title", "ODDS", 40, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(screen.oddsTitle.rectTransform, 20, 56, 20, 20);
        screen.oddsText = UIFactory.CreateText(panel.transform, "Text", "", 24, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.Stretch(screen.oddsText.rectTransform, 50, 96, 50, 120);
        UIFactory.BestFit(screen.oddsText, 14);
        screen.oddsCloseButton = UIFactory.CreateButton(panel.transform, "Close", "CLOSE", UIFactory.Accent, 26);
        UIFactory.Place((RectTransform)screen.oddsCloseButton.transform, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(260, 70));
        dim.gameObject.SetActive(false);
    }

    static void BuildReveal(RectTransform canvas, GachaScreen screen)
    {
        var dim = UIFactory.CreateImage(canvas, "RevealPanel", new Color(0.02f, 0.01f, 0.06f, 0.95f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        screen.revealPanel = dim.gameObject;
        var theme = UIFactory.Theme;
        if (theme != null && theme.lightRays != null)
        {
            var rays = UIFactory.CreateImage(dim.transform, "Rays", new Color(1f, 0.85f, 1f, 0.045f), theme.lightRays);
            UIFactory.Place(rays.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2200, 2200));
        }
        screen.revealTitle = UIFactory.CreateText(dim.transform, "Title", "PACK", 56, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(screen.revealTitle.rectTransform, 90, 80, 40, 40);
        UIFactory.AddShadow(screen.revealTitle, UIFactory.ShadowColor, 4f);
        UIFactory.AddOutline(screen.revealTitle, new Color(0, 0, 0, 0.6f), 2f);
        screen.revealRow = UIFactory.CreateRect("Row", dim.transform);
        UIFactory.Place(screen.revealRow, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1600, 420));
        screen.revealSummary = UIFactory.CreateText(dim.transform, "Summary", "", 28, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(screen.revealSummary.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 230), new Vector2(1700, 60));
        UIFactory.BestFit(screen.revealSummary, 14);

        screen.revealAllButton = UIFactory.CreateButton(dim.transform, "RevealAll", "REVEAL ALL", UIFactory.PanelLight, 26);
        UIFactory.Place((RectTransform)screen.revealAllButton.transform, new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(300, 70));
        screen.openAgainButton = UIFactory.CreateButton(dim.transform, "OpenAgain", "OPEN AGAIN", UIFactory.Accent2, 26);
        UIFactory.Place((RectTransform)screen.openAgainButton.transform, new Vector2(0.5f, 0f), new Vector2(-360, 60), new Vector2(400, 76));
        screen.revealCardsButton = UIFactory.CreateButton(dim.transform, "MyCards", "MY CARDS", UIFactory.Accent, 26);
        UIFactory.Place((RectTransform)screen.revealCardsButton.transform, new Vector2(0.5f, 0f), new Vector2(40, 60), new Vector2(300, 76));
        screen.revealCloseButton = UIFactory.CreateButton(dim.transform, "Close", "CLOSE", UIFactory.PanelLight, 26);
        UIFactory.Place((RectTransform)screen.revealCloseButton.transform, new Vector2(0.5f, 0f), new Vector2(380, 60), new Vector2(260, 76));
        dim.gameObject.SetActive(false);
    }
}
