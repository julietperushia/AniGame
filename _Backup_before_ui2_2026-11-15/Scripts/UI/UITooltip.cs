using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Floating tooltip shown while the pointer is over a <see cref="TooltipTrigger"/> (talent and element icons, buffs...).</summary>
public class UITooltip : MonoBehaviour
{
    const float MaxWidth = 420f;

    static UITooltip instance;
    RectTransform rect;
    Text text;
    LayoutElement textLayout;
    Object owner;

    static UITooltip Get(Transform near)
    {
        if (instance != null) return instance;
        var canvas = near != null ? near.GetComponentInParent<Canvas>() : null;
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas == null) return null;

        var panel = UIFactory.CreatePanel(canvas.transform, "Tooltip", new Color(0.05f, 0.04f, 0.1f, 0.97f), false);
        var rt = panel.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(UIFactory.Accent, 0.9f), 2f);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var t = UIFactory.CreateText(panel.transform, "Text", "", 20, UIFactory.TextMain, TextAnchor.UpperLeft);
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;

        instance = panel.gameObject.AddComponent<UITooltip>();
        instance.rect = rt;
        instance.text = t;
        instance.textLayout = t.gameObject.AddComponent<LayoutElement>();
        panel.gameObject.SetActive(false);
        return instance;
    }

    public static void Show(string message, RectTransform target, Object who)
    {
        if (string.IsNullOrEmpty(message) || target == null) return;
        var tip = Get(target);
        if (tip == null) return;
        tip.owner = who;
        tip.text.text = message;
        tip.textLayout.preferredWidth = -1;
        tip.gameObject.SetActive(true);
        tip.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        if (tip.text.preferredWidth > MaxWidth) tip.textLayout.preferredWidth = MaxWidth;
        LayoutRebuilder.ForceRebuildLayoutImmediate(tip.rect);

        // Above the target (below when there is no room), kept inside the screen.
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        var canvasRt = (RectTransform)tip.rect.parent;
        Vector2 top = canvasRt.InverseTransformPoint((corners[1] + corners[2]) * 0.5f);
        Vector2 bottom = canvasRt.InverseTransformPoint((corners[0] + corners[3]) * 0.5f);
        Vector2 size = tip.rect.rect.size;
        Rect bounds = canvasRt.rect;
        bool above = top.y + 12f + size.y < bounds.yMax;
        tip.rect.pivot = new Vector2(0.5f, above ? 0f : 1f);
        Vector2 pos = above ? top + new Vector2(0f, 10f) : bottom - new Vector2(0f, 10f);
        pos.x = Mathf.Clamp(pos.x, bounds.xMin + size.x * 0.5f + 8f, bounds.xMax - size.x * 0.5f - 8f);
        tip.rect.anchoredPosition = pos;
    }

    public static void Hide(Object who)
    {
        if (instance == null || (who != null && instance.owner != who)) return;
        instance.gameObject.SetActive(false);
        instance.owner = null;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}

/// <summary>Shows <see cref="text"/> in a tooltip while hovered. Needs a raycast-target Graphic on the same object.</summary>
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea] public string text;

    public static TooltipTrigger Add(Graphic graphic, string text)
    {
        if (graphic == null) return null;
        graphic.raycastTarget = true;
        var t = graphic.GetComponent<TooltipTrigger>();
        if (t == null) t = graphic.gameObject.AddComponent<TooltipTrigger>();
        t.text = text;
        return t;
    }

    public void OnPointerEnter(PointerEventData eventData) => UITooltip.Show(text, (RectTransform)transform, this);
    public void OnPointerExit(PointerEventData eventData) => UITooltip.Hide(this);
    void OnDisable() => UITooltip.Hide(this);
}

/// <summary>Tooltip text for talents and elements (names live here, not on the cards).</summary>
public static class IconInfo
{
    public static string Talent(Talent t, CardRarity rarity)
    {
        if (t == null) return "No talent";
        int bonus = Mathf.RoundToInt((GameRules.TalentPower(GameRules.Tier(rarity)) - 1f) * 100f);
        string kind = t.IsActive ? "<color=#6FD3FF>ACTIVE</color> (when mana is full)" : "<color=#FFB23D>PASSIVE</color> (always on)";
        return $"<b>{t.DisplayName}</b>  {kind}\n{t.Description}" + (bonus > 0 ? $"\n<color=#7CFC6A>+{bonus}% from {CardStats.RarityName(rarity)} rarity</color>" : "");
    }

    public static string Element(CardElement e)
    {
        var strong = string.Join(", ", System.Linq.Enumerable.Select(ElementSystem.StrongAgainst(e), ElementSystem.DisplayName));
        return $"<b><color=#{ColorUtility.ToHtmlStringRGB(ElementSystem.GetColor(e))}>{ElementSystem.DisplayName(e)}</color></b>" +
               (strong.Length > 0 ? $"\nStrong vs {strong}" : "");
    }
}
