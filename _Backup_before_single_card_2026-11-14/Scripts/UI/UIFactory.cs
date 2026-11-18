using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helpers that create uGUI objects in code. Used both by the editor scene builder (so the scenes contain
/// real, editable objects) and at runtime for dynamic lists (floor tiles, collection grid, popups).
/// </summary>
public static class UIFactory
{
    // ---------------------------------------------------------------- palette
    public static readonly Color BgDark = Hex("0B0E17");
    public static readonly Color Panel = new Color(0.09f, 0.11f, 0.17f, 0.94f);
    public static readonly Color PanelLight = new Color(0.16f, 0.19f, 0.28f, 0.96f);
    public static readonly Color Accent = Hex("7C5CFF");
    public static readonly Color Accent2 = Hex("FF4D8D");
    public static readonly Color Gold = Hex("FFD54F");
    public static readonly Color TextMain = Hex("EEF1F7");
    public static readonly Color TextDim = Hex("9AA4B5");
    public static readonly Color Good = Hex("3DDC84");
    public static readonly Color Bad = Hex("FF5A5A");
    public static readonly Color HpColor = Hex("3DDC84");
    public static readonly Color ManaColor = Hex("4FA3FF");
    public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.55f);

    static Font font;

    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static UITheme Theme => UITheme.Instance;
    static Sprite Rounded => Theme != null ? Theme.rounded : null;
    static Sprite RoundedSmall => Theme != null ? (Theme.roundedSmall != null ? Theme.roundedSmall : Theme.rounded) : null;

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }

    public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    // ---------------------------------------------------------------- rect helpers

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI layer
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>Stretch to fill the parent with the given padding.</summary>
    public static RectTransform Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    /// <summary>Anchor and pivot at the same normalized point, then offset by pos with a fixed size.</summary>
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    /// <summary>Full-width strip anchored to the top of the parent: y = distance from top, h = height.</summary>
    public static RectTransform TopStrip(RectTransform rt, float y, float h, float padLeft = 0, float padRight = 0)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(padLeft, -y - h);
        rt.offsetMax = new Vector2(-padRight, -y);
        return rt;
    }

    /// <summary>Rect defined in pixels from the parent's top-left corner (fixed-size parents).</summary>
    public static RectTransform TopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    /// <summary>Region of the parent between normalized anchors with pixel padding.</summary>
    public static RectTransform Region(RectTransform rt, Vector2 min, Vector2 max, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    // ---------------------------------------------------------------- graphics

    public static Image CreateImage(Transform parent, string name, Color color, Sprite sprite = null, bool sliced = false, bool raycast = false)
    {
        var rt = CreateRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        if (sprite != null && sliced) img.type = Image.Type.Sliced;
        return img;
    }

    public static Image CreatePanel(Transform parent, string name, Color color, bool raycast = true)
    {
        return CreateImage(parent, name, color, Rounded, true, raycast);
    }

    public static Image CreateChip(Transform parent, string name, Color color)
    {
        return CreateImage(parent, name, color, RoundedSmall, true, false);
    }

    public static Text CreateText(Transform parent, string name, string text, int size, Color color,
                                  TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
    {
        var rt = CreateRect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.fontStyle = style;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return t;
    }

    /// <summary>Shrinks the font automatically when the text does not fit.</summary>
    public static Text BestFit(Text t, int min = 10)
    {
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = min;
        t.resizeTextMaxSize = t.fontSize;
        return t;
    }

    public static Shadow AddShadow(Graphic g, Color c, float distance = 2f)
    {
        var s = g.gameObject.AddComponent<Shadow>();
        s.effectColor = c;
        s.effectDistance = new Vector2(distance, -distance);
        return s;
    }

    public static Outline AddOutline(Graphic g, Color c, float distance = 1.5f)
    {
        var o = g.gameObject.AddComponent<Outline>();
        o.effectColor = c;
        o.effectDistance = new Vector2(distance, -distance);
        return o;
    }

    public static Button CreateButton(Transform parent, string name, string label, Color color, int fontSize = 28)
    {
        var img = CreatePanel(parent, name, color, true);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        var txt = CreateText(img.transform, "Label", label, fontSize, TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(txt.rectTransform, 10, 4, 10, 4);
        BestFit(txt, 12);
        AddShadow(txt, ShadowColor, 2f);
        btn.gameObject.AddComponent<UIButtonFx>();
        return btn;
    }

    public static Text ButtonLabel(Button b) => b != null ? b.GetComponentInChildren<Text>(true) : null;

    // ---------------------------------------------------------------- containers

    public static ScrollRect CreateScroll(Transform parent, string name, bool vertical, out RectTransform content)
    {
        var bg = CreateImage(parent, name, new Color(0, 0, 0, 0.001f), null, false, true);
        var scroll = bg.gameObject.AddComponent<ScrollRect>();

        var viewport = CreateRect("Viewport", bg.transform);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = CreateRect("Content", viewport);
        if (vertical)
        {
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
        }
        else
        {
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
        }
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        if (vertical) fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        else fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = !vertical;
        scroll.vertical = vertical;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        return scroll;
    }

    public static GridLayoutGroup AddGrid(RectTransform content, Vector2 cell, Vector2 spacing, int padding)
    {
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = cell;
        grid.spacing = spacing;
        grid.padding = new RectOffset(padding, padding, padding, padding);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        return grid;
    }

    public static VerticalLayoutGroup AddVertical(RectTransform content, float spacing, int padding)
    {
        var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = new RectOffset(padding, padding, padding, padding);
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperCenter;
        return v;
    }

    public static LayoutElement SetLayoutSize(Component c, float preferredWidth, float preferredHeight)
    {
        var le = c.GetComponent<LayoutElement>();
        if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
        if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        return le;
    }

    public static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i).gameObject;
            if (Application.isPlaying) Object.Destroy(child);
            else Object.DestroyImmediate(child);
        }
    }

    // ---------------------------------------------------------------- composite pieces

    /// <summary>Full-screen background: base colour, a tinted gradient from the top and a vignette.</summary>
    public static Image CreateBackground(Transform parent, Color top, Color bottom, out Image gradientOverlay)
    {
        var bg = CreateImage(parent, "Background", bottom, null, false, false);
        Stretch(bg.rectTransform);
        gradientOverlay = CreateImage(bg.transform, "Gradient", top, Theme != null ? Theme.gradient : null, false, false);
        Stretch(gradientOverlay.rectTransform);
        if (Theme != null && Theme.vignette != null)
        {
            var vig = CreateImage(bg.transform, "Vignette", new Color(0, 0, 0, 0.75f), Theme.vignette, false, false);
            Stretch(vig.rectTransform);
        }
        return bg;
    }

    /// <summary>Top bar with a back button, a title and a right-aligned info text.</summary>
    public static RectTransform CreateTopBar(Transform parent, string title, out Button back, out Text titleText, out Text infoText)
    {
        var bar = CreateImage(parent, "TopBar", new Color(0.04f, 0.05f, 0.09f, 0.85f), null, false, true);
        TopStrip(bar.rectTransform, 0, 96);

        back = CreateButton(bar.transform, "BackButton", "< BACK", PanelLight, 26);
        Place((RectTransform)back.transform, new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(170, 64));

        titleText = CreateText(bar.transform, "Title", title, 40, TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        Place(titleText.rectTransform, new Vector2(0, 0.5f), new Vector2(220, 0), new Vector2(900, 80));
        AddShadow(titleText, ShadowColor, 3f);

        infoText = CreateText(bar.transform, "Info", "", 26, TextMain, TextAnchor.MiddleRight);
        Place(infoText.rectTransform, new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(900, 80));
        return bar.rectTransform;
    }

    /// <summary>Standard "Gold / Diamonds / Stamina" line for top bars.</summary>
    public static string CurrencyLine()
    {
        var d = PlayerProfile.Data;
        PlayerProfile.UpdateStamina();
        string stam = $"<color=#7CFC6A>Stamina {d.stamina}/{PlayerProfile.MaxStamina}</color>";
        if (d.stamina < PlayerProfile.MaxStamina)
        {
            int s = PlayerProfile.SecondsToNextStamina;
            stam += $" <color=#9AA4B5>(+1 in {s / 60}:{s % 60:00})</color>";
        }
        return $"<color=#9AA4B5>Lv</color> {d.playerLevel}    <color=#FFD54F>Gold {d.gold:N0}</color>    <color=#6FD3FF>Diamonds {d.diamonds}</color>    {stam}";
    }
}
