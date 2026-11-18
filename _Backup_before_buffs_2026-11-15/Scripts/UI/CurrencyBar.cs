using UnityEngine;
using UnityEngine.UI;

/// <summary>Row of currency chips (icon + amount): gold, diamonds, the 3 orbs and stamina. Refreshes every second.</summary>
public class CurrencyBar : MonoBehaviour
{
    public Text gold;
    public Text diamonds;
    public Text fusionOrbs;
    public Text elementOrbs;
    public Text talentOrbs;
    public Text stamina;

    void OnEnable()
    {
        InvokeRepeating(nameof(Refresh), 0f, 1f);
        PlayerProfile.Changed += Refresh;
    }

    void OnDisable()
    {
        CancelInvoke(nameof(Refresh));
        PlayerProfile.Changed -= Refresh;
    }

    public void Refresh()
    {
        if (this == null) return;
        var d = PlayerProfile.Data;
        PlayerProfile.UpdateStamina();
        if (gold) gold.text = d.gold.ToString("N0");
        if (diamonds) diamonds.text = d.diamonds.ToString("N0");
        if (fusionOrbs) fusionOrbs.text = d.fusionOrbs.ToString();
        if (elementOrbs) elementOrbs.text = d.elementOrbs.ToString();
        if (talentOrbs) talentOrbs.text = d.talentOrbs.ToString();
        if (stamina) stamina.text = $"{d.stamina}/{PlayerProfile.MaxStamina}";
    }

    /// <summary>Builds the bar right-aligned in <paramref name="parent"/> (e.g. a top bar).</summary>
    public static CurrencyBar Build(Transform parent, float rightMargin = 24f)
    {
        var theme = UIFactory.Theme;
        var root = UIFactory.CreateRect("CurrencyBar", parent);
        UIFactory.Place(root, new Vector2(1f, 0.5f), new Vector2(-rightMargin, 0f), new Vector2(980f, 56f));
        var bar = root.gameObject.AddComponent<CurrencyBar>();

        float x = 0f;
        Text Chip(string name, Sprite icon, Color fallback, float width)
        {
            var chip = UIFactory.CreateChip(root, name, new Color(0f, 0f, 0f, 0.45f));
            var rt = chip.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-x, 0f);
            rt.sizeDelta = new Vector2(width, 48f);
            x += width + 10f;
            UIFactory.Icon(chip.transform, "Icon", icon, fallback, 38f, new Vector2(8f, 0f));
            var t = UIFactory.CreateText(chip.transform, "Value", "0", 22, UIFactory.TextMain, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Stretch(t.rectTransform, 50f, 0f, 12f, 0f);
            UIFactory.BestFit(t, 12);
            return t;
        }

        bar.stamina = Chip("Stamina", theme != null ? theme.staminaIcon : null, new Color(0.45f, 1f, 0.45f), 140f);
        bar.talentOrbs = Chip("TalentOrbs", theme != null ? theme.talentOrbIcon : null, new Color(1f, 0.8f, 0.3f), 100f);
        bar.elementOrbs = Chip("ElementOrbs", theme != null ? theme.elementOrbIcon : null, new Color(0.4f, 0.9f, 0.6f), 100f);
        bar.fusionOrbs = Chip("FusionOrbs", theme != null ? theme.fusionOrbIcon : null, new Color(0.75f, 0.45f, 1f), 100f);
        bar.diamonds = Chip("Diamonds", theme != null ? theme.diamondIcon : null, new Color(0.4f, 0.85f, 1f), 140f);
        bar.gold = Chip("Gold", theme != null ? theme.goldIcon : null, new Color(1f, 0.82f, 0.25f), 170f);
        return bar;
    }
}
