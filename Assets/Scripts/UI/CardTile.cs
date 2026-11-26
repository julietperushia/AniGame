using UnityEngine;
using UnityEngine.UI;

/// <summary>Small clickable card for grids (cards, floors, slots, pack reveals) with its rarity frame. 150 x 210 by default.</summary>
public class CardTile : MonoBehaviour
{
    public Button button;
    public Image frame;          // background tinted by rarity
    public CardFrame frameFx;
    public Image highlight;
    public Image art;
    public Text initials;
    public Text nameText;
    public Text topLeft;
    public Text topRight;
    public Image elementDot;
    public Image talentDot;
    public Image talentDot2;     // God cards: the second talent
    public Text elementText;
    public Image lockIcon;
    public Image badge;
    public Text badgeText;
    public Image lockOverlay;
    public Text lockText;
    public Image lockOverlayIcon;

    public object Data { get; set; }
    Card shownCard;
    CardRarity shownRarity;
    bool layoutChecked;

    /// <summary>A floor / sheet card with its own rarity and element.</summary>
    public void Set(Card card, string leftLabel, string rightLabel)
    {
        if (card == null) { SetEmpty("Empty"); return; }
        Set(card, card.rarity, card.element, leftLabel, rightLabel);
        SetTalent(card.talent, card.talent2 != card.talent ? card.talent2 : null);
    }

    /// <summary>An owned card instance (rarity and element from the instance / build).</summary>
    public void SetInstance(CardInstance card, BuildData build = null)
    {
        if (card == null) { SetEmpty("Empty"); return; }
        var b = build ?? card.build;
        Set(PlayerProfile.Art(card), card.rarity, b.element, CardStats.RarityName(card.rarity), CardStats.Power(card, b).ToString());
        nameText.text = PlayerProfile.CardName(card, b);
        var db = CardDatabase.Instance;
        var t2 = card.Tier >= GameRules.MaxTier && db != null ? db.FindTalent(b.talent2) : null;
        SetTalent(db != null ? db.FindTalent(b.talent) : null, t2);
        SetLockIcon(PlayerProfile.IsProtected(card));
    }

    public void Set(Card card, CardRarity rarity, CardElement element, string leftLabel, string rightLabel)
    {
        EnsureLayout();
        shownCard = card;
        shownRarity = rarity;
        frame.color = CardStats.RarityColorDark(rarity);
        if (frameFx != null) frameFx.SetRarity(rarity);
        Sprite sprite = card != null ? card.cardImg : null;
        art.sprite = sprite;
        art.enabled = sprite != null;
        string name = card != null ? card.DisplayName : "?";
        initials.text = sprite == null ? name.Substring(0, Mathf.Min(2, name.Length)).ToUpperInvariant() : "";
        initials.color = ElementSystem.GetColor(element);
        if (card != null && sprite == null && !string.IsNullOrEmpty(card.imageUrl))
        {
            var wanted = card;
            RemoteImageCache.Request(card.imageUrl, s =>
            {
                if (this == null || shownCard != wanted || s == null) return;
                art.sprite = s;
                art.enabled = true;
                initials.text = "";
            });
        }
        nameText.text = name;
        topLeft.text = leftLabel;
        topRight.text = rightLabel;
        topLeft.color = CardStats.RarityColor(rarity);

        elementDot.gameObject.SetActive(true);
        var theme = UITheme.Instance;
        var icon = theme != null ? theme.ElementIcon(element) : null;
        elementDot.sprite = icon != null ? icon : (theme != null ? theme.circle : null);
        elementDot.color = icon != null ? Color.white : ElementSystem.GetColor(element);
        elementText.text = icon != null ? "" : ElementSystem.ShortName(element).Substring(0, 1);
        TooltipTrigger.Add(elementDot, IconInfo.Element(element));
        SetTalent(null);
        SetLockIcon(false);
    }

    /// <summary>The card's talent pictures at the bottom right (God cards: both), each with its tooltip.</summary>
    public void SetTalent(Talent talent, Talent talent2 = null)
    {
        EnsureLayout();
        if (talentDot == null) return;
        var theme = UITheme.Instance;
        var icon = theme != null ? theme.TalentIcon(talent) : null;
        var icon2 = theme != null && talent2 != null ? theme.TalentIcon(talent2) : null;
        talentDot.sprite = icon;
        talentDot.gameObject.SetActive(icon != null);
        if (icon != null) TooltipTrigger.Add(talentDot, IconInfo.Talent(talent, shownRarity));
        if (talentDot2 != null)
        {
            talentDot2.sprite = icon2;
            talentDot2.gameObject.SetActive(icon2 != null);
            if (icon2 != null) TooltipTrigger.Add(talentDot2, IconInfo.Talent(talent2, shownRarity));
        }
        // One talent sits in the corner; with two, the first moves left of the second
        float dot = talentDot.rectTransform.sizeDelta.x;
        talentDot.rectTransform.anchoredPosition = new Vector2(icon2 != null ? -8f - dot : -4f, 4f);
    }

    /// <summary>Element in the top-right corner (power label left of it), talents at the bottom right.
    /// Also fixes tiles that were saved in a scene with the older layout.</summary>
    void EnsureLayout()
    {
        if (layoutChecked || elementDot == null) return;
        layoutChecked = true;
        float dot = elementDot.rectTransform.sizeDelta.x;
        UIFactory.Place(elementDot.rectTransform, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(dot, dot));
        // Rarity name on the left half, power on the right half (left of the element): one line each, shrunk to fit
        if (topLeft != null)
        {
            float lh = topLeft.fontSize + 8f;
            var l = topLeft.rectTransform;
            l.anchorMin = new Vector2(0f, 1f); l.anchorMax = new Vector2(0.5f, 1f); l.pivot = new Vector2(0f, 1f);
            l.offsetMin = new Vector2(6f, -4f - lh); l.offsetMax = new Vector2(0f, -4f);
            UIFactory.BestFit(topLeft, 8);
        }
        if (topRight != null)
        {
            float lh = topRight.fontSize + 8f;
            var r = topRight.rectTransform;
            r.anchorMin = new Vector2(0.5f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(2f, -4f - lh); r.offsetMax = new Vector2(-(dot + 8f), -4f);
            UIFactory.BestFit(topRight, 8);
        }
        if (talentDot != null)
        {
            UIFactory.Place(talentDot.rectTransform, new Vector2(1f, 0f), new Vector2(-4f, 4f), new Vector2(dot, dot));
            if (talentDot2 == null)
            {
                talentDot2 = UIFactory.CreateImage(talentDot.transform.parent, "Talent2", Color.white);
                talentDot2.preserveAspect = true;
                UIFactory.Place(talentDot2.rectTransform, new Vector2(1f, 0f), new Vector2(-4f, 4f), new Vector2(dot, dot));
                talentDot2.gameObject.SetActive(false);
            }
        }
    }

    public void SetEmpty(string label)
    {
        shownCard = null;
        frame.color = new Color(0.18f, 0.2f, 0.27f);
        if (frameFx != null) frameFx.SetRarity(CardRarity.Common);
        if (frameFx != null && frameFx.frame != null) frameFx.frame.enabled = false;
        art.enabled = false;
        initials.text = "+";
        initials.color = UIFactory.TextDim;
        nameText.text = label;
        topLeft.text = "";
        topRight.text = "";
        elementDot.gameObject.SetActive(false);
        if (talentDot != null) talentDot.gameObject.SetActive(false);
        if (talentDot2 != null) talentDot2.gameObject.SetActive(false);
        SetLockIcon(false);
        SetBadge(null, Color.clear);
        SetLocked(false, null);
    }

    public void SetLockIcon(bool locked)
    {
        if (lockIcon != null) lockIcon.gameObject.SetActive(locked);
    }

    public void SetBadge(string text, Color color)
    {
        bool show = !string.IsNullOrEmpty(text);
        badge.gameObject.SetActive(show);
        if (show)
        {
            badge.color = color;
            badgeText.text = text;
        }
    }

    public void SetLocked(bool locked, string label)
    {
        lockOverlay.gameObject.SetActive(locked || !string.IsNullOrEmpty(label));
        lockOverlay.color = locked ? new Color(0.02f, 0.03f, 0.06f, 0.78f) : new Color(0f, 0f, 0f, 0f);
        lockText.text = label ?? "";
        if (lockOverlayIcon != null) lockOverlayIcon.gameObject.SetActive(locked && lockOverlayIcon.sprite != null);
    }

    public void SetSelected(bool selected)
    {
        highlight.gameObject.SetActive(selected);
    }

    public static CardTile Build(Transform parent, string name, float width = 150f, float height = 210f, bool effects = true)
    {
        var root = UIFactory.CreatePanel(parent, name, Color.white, true);
        root.rectTransform.sizeDelta = new Vector2(width, height);
        var tile = root.gameObject.AddComponent<CardTile>();
        tile.frame = root;

        tile.button = root.gameObject.AddComponent<Button>();
        tile.button.targetGraphic = root;
        var colors = tile.button.colors;
        colors.normalColor = new Color(0.94f, 0.94f, 0.94f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.selectedColor = Color.white;
        tile.button.colors = colors;
        root.gameObject.AddComponent<UIButtonFx>();

        var theme = UIFactory.Theme;
        var glowImg = UIFactory.CreateImage(root.transform, "Glow", new Color(1f, 1f, 1f, 0.5f), theme != null ? theme.glow : null);
        UIFactory.Stretch(glowImg.rectTransform, -30, -30, -30, -30);
        glowImg.transform.SetAsFirstSibling();
        glowImg.gameObject.SetActive(false);

        tile.highlight = UIFactory.CreateImage(root.transform, "Highlight", new Color(1f, 1f, 1f, 0.95f), theme != null ? theme.glow : null);
        UIFactory.Stretch(tile.highlight.rectTransform, -26, -26, -26, -26);
        tile.highlight.transform.SetSiblingIndex(1);
        tile.highlight.gameObject.SetActive(false);

        var inner = UIFactory.CreatePanel(root.transform, "Inner", new Color(0.06f, 0.07f, 0.11f, 1f), false);
        UIFactory.Stretch(inner.rectTransform, 6, 6, 6, 6);

        var artBg = UIFactory.CreatePanel(inner.transform, "ArtBg", new Color(0.13f, 0.15f, 0.22f), false);
        UIFactory.TopStrip(artBg.rectTransform, 4, height - 54, 4, 4);
        tile.initials = UIFactory.CreateText(artBg.transform, "Initials", "", Mathf.RoundToInt(height * 0.22f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(tile.initials.rectTransform);
        tile.art = UIFactory.CreateImage(artBg.transform, "Art", Color.white);
        tile.art.preserveAspect = true;
        UIFactory.Stretch(tile.art.rectTransform, 2, 2, 2, 2);

        int labelSize = Mathf.Clamp(Mathf.RoundToInt(width * 0.12f), 12, 20);
        tile.topLeft = UIFactory.CreateText(artBg.transform, "TopLeft", "", labelSize, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.Stretch(tile.topLeft.rectTransform, 6, 4, 6, 4);
        UIFactory.AddOutline(tile.topLeft, new Color(0, 0, 0, 0.95f), 1.5f);
        tile.topRight = UIFactory.CreateText(artBg.transform, "TopRight", "", labelSize, UIFactory.Gold, TextAnchor.UpperRight, FontStyle.Bold);
        UIFactory.Stretch(tile.topRight.rectTransform, 6, 4, 6, 4);
        UIFactory.AddOutline(tile.topRight, new Color(0, 0, 0, 0.95f), 1.5f);

        float dot = Mathf.Clamp(width * 0.2f, 18f, 30f);
        tile.elementDot = UIFactory.CreateImage(artBg.transform, "Element", Color.white, theme != null ? theme.circle : null);
        tile.elementDot.preserveAspect = true;
        UIFactory.Place(tile.elementDot.rectTransform, new Vector2(1f, 0f), new Vector2(-4f, 4f), new Vector2(dot, dot));
        tile.elementText = UIFactory.CreateText(tile.elementDot.transform, "Text", "", Mathf.RoundToInt(dot * 0.55f), new Color(0.08f, 0.08f, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(tile.elementText.rectTransform);

        tile.talentDot = UIFactory.CreateImage(artBg.transform, "Talent", Color.white);
        tile.talentDot.preserveAspect = true;
        UIFactory.Place(tile.talentDot.rectTransform, new Vector2(1f, 0f), new Vector2(-6f - dot, 4f), new Vector2(dot, dot));
        tile.talentDot.gameObject.SetActive(false);

        tile.lockIcon = UIFactory.CreateImage(artBg.transform, "Lock", Color.white, theme != null ? theme.lockIcon : null);
        tile.lockIcon.preserveAspect = true;
        UIFactory.Place(tile.lockIcon.rectTransform, new Vector2(0f, 0f), new Vector2(4f, 4f), new Vector2(dot, dot));
        tile.lockIcon.gameObject.SetActive(false);

        tile.nameText = UIFactory.CreateText(inner.transform, "Name", "", Mathf.Clamp(Mathf.RoundToInt(width * 0.12f), 11, 18), UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(tile.nameText.rectTransform, height - 50, 38, 4, 4);
        UIFactory.BestFit(tile.nameText, 9);

        tile.frameFx = CardFrame.Attach((RectTransform)root.transform, glowImg, artBg.rectTransform, 0f);
        tile.frameFx.effects = effects;

        tile.badge = UIFactory.CreateChip(root.transform, "Badge", UIFactory.Accent);
        UIFactory.Place(tile.badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 12), new Vector2(Mathf.Min(120, width - 20), 28));
        tile.badgeText = UIFactory.CreateText(tile.badge.transform, "Text", "", 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(tile.badgeText.rectTransform, 4, 0, 4, 0);
        UIFactory.BestFit(tile.badgeText, 9);
        tile.badge.gameObject.SetActive(false);

        tile.lockOverlay = UIFactory.CreatePanel(root.transform, "LockOverlay", new Color(0, 0, 0, 0.75f), false);
        UIFactory.Stretch(tile.lockOverlay.rectTransform, 6, 6, 6, 6);
        tile.lockOverlayIcon = UIFactory.CreateImage(tile.lockOverlay.transform, "LockIcon", Color.white, theme != null ? theme.lockIcon : null);
        tile.lockOverlayIcon.preserveAspect = true;
        float lockSize = Mathf.Min(width, height) * 0.38f;
        UIFactory.Place(tile.lockOverlayIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, lockSize * 0.32f), new Vector2(lockSize, lockSize));
        tile.lockOverlayIcon.gameObject.SetActive(false);
        tile.lockText = UIFactory.CreateText(tile.lockOverlay.transform, "Text", "", 20, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(tile.lockText.rectTransform, 4, 4, 4, 4);
        tile.lockText.alignment = TextAnchor.LowerCenter;
        UIFactory.Stretch(tile.lockText.rectTransform, 4, 4, 4, height * 0.18f);
        UIFactory.BestFit(tile.lockText, 10);
        UIFactory.AddOutline(tile.lockText, new Color(0, 0, 0, 0.9f), 1.5f);
        tile.lockOverlay.gameObject.SetActive(false);
        return tile;
    }
}
