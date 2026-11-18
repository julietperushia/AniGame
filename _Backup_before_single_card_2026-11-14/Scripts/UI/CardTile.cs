using UnityEngine;
using UnityEngine.UI;

/// <summary>Small clickable card used in grids (collection, floors, team slots). 150 x 210 by default.</summary>
public class CardTile : MonoBehaviour
{
    public Button button;
    public Image frame;
    public Image highlight;
    public Image art;
    public Text initials;
    public Text nameText;
    public Text topLeft;
    public Text topRight;
    public Image badge;
    public Text badgeText;
    public Image lockOverlay;
    public Text lockText;

    public object Data { get; set; }
    Card shownCard;

    public void Set(Card card, string leftLabel, string rightLabel)
    {
        if (card == null)
        {
            shownCard = null;
            frame.color = new Color(0.25f, 0.27f, 0.33f);
            art.enabled = false;
            initials.text = "+";
            nameText.text = "Empty";
            topLeft.text = "";
            topRight.text = "";
            SetBadge(null, Color.clear);
            SetLocked(false, null);
            return;
        }

        shownCard = card;
        frame.color = CardStats.RarityColor(card.rarity);
        art.sprite = card.cardImg;
        art.enabled = card.cardImg != null;
        if (card.cardImg == null && !string.IsNullOrEmpty(card.imageUrl))
        {
            RemoteImageCache.Request(card.imageUrl, sprite =>
            {
                if (this == null || shownCard != card || sprite == null) return;
                art.sprite = sprite;
                art.enabled = true;
                initials.text = "";
            });
        }
        initials.text = card.cardImg == null ? card.DisplayName.Substring(0, Mathf.Min(2, card.DisplayName.Length)).ToUpperInvariant() : "";
        initials.color = ElementSystem.GetColor(card.element);
        nameText.text = card.DisplayName;
        topLeft.text = leftLabel;
        topRight.text = rightLabel;
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
        if (button != null) button.interactable = true;
    }

    public void SetSelected(bool selected)
    {
        highlight.gameObject.SetActive(selected);
    }

    public static CardTile Build(Transform parent, string name, float width = 150f, float height = 210f)
    {
        var root = UIFactory.CreatePanel(parent, name, Color.white, true);
        root.rectTransform.sizeDelta = new Vector2(width, height);
        var tile = root.gameObject.AddComponent<CardTile>();
        tile.frame = root;

        tile.button = root.gameObject.AddComponent<Button>();
        tile.button.targetGraphic = root;
        var colors = tile.button.colors;
        colors.normalColor = new Color(0.92f, 0.92f, 0.92f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.selectedColor = Color.white;
        tile.button.colors = colors;
        root.gameObject.AddComponent<UIButtonFx>();

        var theme = UIFactory.Theme;
        tile.highlight = UIFactory.CreateImage(root.transform, "Highlight", new Color(1f, 1f, 1f, 0.9f), theme != null ? theme.glow : null);
        UIFactory.Stretch(tile.highlight.rectTransform, -26, -26, -26, -26);
        tile.highlight.transform.SetAsFirstSibling();
        tile.highlight.gameObject.SetActive(false);

        var inner = UIFactory.CreatePanel(root.transform, "Inner", new Color(0.07f, 0.08f, 0.13f, 1f), false);
        UIFactory.Stretch(inner.rectTransform, 4, 4, 4, 4);

        var artBg = UIFactory.CreatePanel(inner.transform, "ArtBg", new Color(0.13f, 0.15f, 0.22f), false);
        UIFactory.TopStrip(artBg.rectTransform, 4, height - 50, 4, 4);
        tile.initials = UIFactory.CreateText(artBg.transform, "Initials", "", 48, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(tile.initials.rectTransform);
        tile.art = UIFactory.CreateImage(artBg.transform, "Art", Color.white);
        tile.art.preserveAspect = true;
        UIFactory.Stretch(tile.art.rectTransform, 2, 2, 2, 2);

        tile.topLeft = UIFactory.CreateText(artBg.transform, "TopLeft", "", 18, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.Stretch(tile.topLeft.rectTransform, 6, 4, 6, 4);
        UIFactory.AddOutline(tile.topLeft, new Color(0, 0, 0, 0.9f), 1.5f);
        tile.topRight = UIFactory.CreateText(artBg.transform, "TopRight", "", 18, Color.white, TextAnchor.UpperRight, FontStyle.Bold);
        UIFactory.Stretch(tile.topRight.rectTransform, 6, 4, 6, 4);
        UIFactory.AddOutline(tile.topRight, new Color(0, 0, 0, 0.9f), 1.5f);

        tile.nameText = UIFactory.CreateText(inner.transform, "Name", "", 18, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(tile.nameText.rectTransform, height - 46, 38, 4, 4);
        UIFactory.BestFit(tile.nameText, 10);

        tile.badge = UIFactory.CreateChip(root.transform, "Badge", UIFactory.Accent);
        UIFactory.Place(tile.badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 12), new Vector2(Mathf.Min(120, width - 20), 28));
        tile.badgeText = UIFactory.CreateText(tile.badge.transform, "Text", "", 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(tile.badgeText.rectTransform, 4, 0, 4, 0);
        UIFactory.BestFit(tile.badgeText, 9);
        tile.badge.gameObject.SetActive(false);

        tile.lockOverlay = UIFactory.CreatePanel(root.transform, "Lock", new Color(0, 0, 0, 0.75f), false);
        UIFactory.Stretch(tile.lockOverlay.rectTransform, 4, 4, 4, 4);
        tile.lockText = UIFactory.CreateText(tile.lockOverlay.transform, "Text", "", 20, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(tile.lockText.rectTransform, 4, 4, 4, 4);
        UIFactory.AddOutline(tile.lockText, new Color(0, 0, 0, 0.9f), 1.5f);
        tile.lockOverlay.gameObject.SetActive(false);

        return tile;
    }
}
