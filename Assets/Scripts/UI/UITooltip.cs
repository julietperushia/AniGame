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

/// <summary>Tooltip text for talents and elements (names live here, not on the cards).</summary>
public static class IconInfo
{
    public static string Talent(Talent t, CardRarity rarity)
    {
        if (t == null) return "No talent";
        int bonus = Mathf.RoundToInt((GameRules.TalentPower(GameRules.Tier(rarity)) - 1f) * 100f);
        string kind = t.IsActive ? "<color=#6FD3FF>ACTIVE</color>" : "<color=#FFB23D>PASSIVE</color>";
        return $"<b>{t.DisplayName}</b>  {kind}\n{t.DescriptionFor(rarity)}" +
               (bonus > 0 ? $"\n<color=#7CFC6A>Numbers include +{bonus}% from {CardStats.RarityName(rarity)} rarity</color>" : "");
    }

    public static string Element(CardElement e)
    {
        var strong = string.Join(", ", System.Linq.Enumerable.Select(ElementSystem.StrongAgainst(e), ElementSystem.DisplayName));
        return $"<b><color=#{ColorUtility.ToHtmlStringRGB(ElementSystem.GetColor(e))}>{ElementSystem.DisplayName(e)}</color></b>" +
               (strong.Length > 0 ? $"\nStrong vs {strong}" : "");
    }
}

public enum ItemKind { Gold, Diamonds, FusionOrb, ElementOrb, TalentOrb, GodOrb, Stamina }

/// <summary>Names, icons, colours and hover descriptions for every currency / item.</summary>
public static class ItemInfo
{
    public static string Name(ItemKind k)
    {
        switch (k)
        {
            case ItemKind.Gold: return "Gold";
            case ItemKind.Diamonds: return "Diamonds";
            case ItemKind.FusionOrb: return "Fusion Orb";
            case ItemKind.ElementOrb: return "Element Orb";
            case ItemKind.TalentOrb: return "Talent Orb";
            case ItemKind.GodOrb: return "God Orb";
            default: return "Stamina";
        }
    }

    public static Sprite Icon(ItemKind k)
    {
        var t = UITheme.Instance;
        if (t == null) return null;
        switch (k)
        {
            case ItemKind.Gold: return t.goldIcon;
            case ItemKind.Diamonds: return t.diamondIcon;
            case ItemKind.FusionOrb: return t.fusionOrbIcon;
            case ItemKind.ElementOrb: return t.elementOrbIcon;
            case ItemKind.TalentOrb: return t.talentOrbIcon;
            case ItemKind.GodOrb: return t.godOrbIcon;
            default: return t.staminaIcon;
        }
    }

    public static Color Tint(ItemKind k)
    {
        switch (k)
        {
            case ItemKind.Gold: return UIFactory.Gold;
            case ItemKind.Diamonds: return UIFactory.Hex("6FD3FF");
            case ItemKind.FusionOrb: return UIFactory.Hex("C78BFF");
            case ItemKind.ElementOrb: return UIFactory.Hex("7CFC6A");
            case ItemKind.TalentOrb: return UIFactory.Hex("FFB23D");
            case ItemKind.GodOrb: return UIFactory.Hex("FFE9A3");
            default: return UIFactory.Hex("8CFF6A");
        }
    }

    public static string Describe(ItemKind k)
    {
        string head = $"<b><color=#{ColorUtility.ToHtmlStringRGB(Tint(k))}>{Name(k)}</color></b>\n";
        switch (k)
        {
            case ItemKind.Gold: return head + "Earned from floor wins (20 x LOC + 5 x floor), gold packs and dismantling.\nSpent on gold packs, the orb shop, stat rerolls, rerolls without an orb and economy/stamina buffs.";
            case ItemKind.Diamonds: return head + "Drop from gold packs (and Treasure Hunter loot). Can't be bought.\nSpent on diamond packs, build slots 2-3 and most buffs.";
            case ItemKind.FusionOrb: return head + $"Needed to fuse cards (1 orb at Common up to 20 at Mythic).\n{GameRules.FusionOrbPrice:N0} gold in the shop, 5% drop from floor wins, also in packs.";
            case ItemKind.ElementOrb: return head + $"Rerolls a card's element. Used before gold; without one a reroll costs {GameRules.ElementOrbPrice:N0} gold.";
            case ItemKind.TalentOrb: return head + $"Rerolls a card's talent. Used before gold; without one a reroll costs {GameRules.TalentOrbPrice:N0} gold.";
            case ItemKind.GodOrb: return head + $"God cards only: {GameRules.GodOrbSuccess * 100f:0}% chance to add +1% to every stat (max +{GameRules.GodOrbMaxStacks}%).\nNever sold: God floor enemies and diamond packs only.";
            default:
                if (!Application.isPlaying) return head + $"Each fight costs {GameRules.StaminaCost}. +1 every 5 minutes, +{GameRules.StaminaNewFloor} for a new floor, full refill when a new LOC unlocks.";
                int s = PlayerProfile.SecondsToNextStamina;
                string next = s > 0 ? $"Next +{PlayerProfile.StaminaPerTick} in {s / 60}:{s % 60:00}." : "Full.";
                return head + $"{PlayerProfile.Data.stamina}/{PlayerProfile.MaxStamina}. Each fight costs {GameRules.StaminaCost}. {next}\n+{GameRules.StaminaNewFloor} for a new floor, full refill when a new LOC unlocks.";
        }
    }

    public static ItemKind? FromReward(RewardType t)
    {
        switch (t)
        {
            case RewardType.Gold: return ItemKind.Gold;
            case RewardType.Diamonds: return ItemKind.Diamonds;
            case RewardType.FusionOrb: return ItemKind.FusionOrb;
            case RewardType.ElementOrb: return ItemKind.ElementOrb;
            case RewardType.TalentOrb: return ItemKind.TalentOrb;
            case RewardType.GodOrb: return ItemKind.GodOrb;
        }
        return null;
    }

    public static ItemKind FromOrb(OrbType t) =>
        t == OrbType.Fusion ? ItemKind.FusionOrb : t == OrbType.Element ? ItemKind.ElementOrb : t == OrbType.Talent ? ItemKind.TalentOrb : ItemKind.GodOrb;

    /// <summary>A reward picture: icon, "+amount" and the item name, with its hover description.</summary>
    public static RectTransform Chip(Transform parent, ItemKind kind, string amount, float size = 150f)
    {
        var panel = UIFactory.CreatePanel(parent, Name(kind), new Color(0.05f, 0.04f, 0.11f, 0.95f), true);
        panel.rectTransform.sizeDelta = new Vector2(size, size * 1.18f);
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(Tint(kind), 0.7f), 2f);
        var glow = UIFactory.CreateImage(panel.transform, "Glow", UIFactory.WithAlpha(Tint(kind), 0.35f), UIFactory.Theme != null ? UIFactory.Theme.glow : null);
        UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -size * 0.06f), new Vector2(size * 1.1f, size * 1.1f));
        var icon = UIFactory.CreateImage(panel.transform, "Icon", Color.white, Icon(kind));
        icon.preserveAspect = true;
        UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -size * 0.12f), new Vector2(size * 0.56f, size * 0.56f));
        var amt = UIFactory.CreateText(panel.transform, "Amount", amount, Mathf.RoundToInt(size * 0.22f), Tint(kind), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(amt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, size * 0.3f), new Vector2(size, size * 0.3f));
        UIFactory.AddShadow(amt, UIFactory.ShadowColor, 2f);
        UIFactory.BestFit(amt, 10);
        var label = UIFactory.CreateText(panel.transform, "Label", Name(kind).ToUpperInvariant(), Mathf.RoundToInt(size * 0.1f), UIFactory.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, size * 0.08f), new Vector2(size - 8, size * 0.2f));
        UIFactory.BestFit(label, 8);
        TooltipTrigger.Add(panel, Describe(kind));
        return panel.rectTransform;
    }
}
