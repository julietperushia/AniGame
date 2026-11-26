using UnityEngine;
using UnityEngine.UI;

/// <summary>HP / mana / XP bar with a trailing "ghost" segment that shows recent damage.</summary>
public class StatBar : MonoBehaviour
{
    public RectTransform fill;
    public RectTransform ghost;
    public Image fillImage;
    public Text label;
    public string format = "{0} / {1}";
    public float ghostSpeed = 0.9f;

    /// <summary>Light-blue segment for a barrier (Burst Barrier), created on first use.</summary>
    public RectTransform shieldSeg;

    float target = 1f;
    float ghostValue = 1f;

    public void Set(int current, int max, bool instant = false) => Set(current, max, 0, instant);

    /// <summary>Value plus an extra shield amount, drawn right after the fill (over its end when it doesn't fit).</summary>
    public void Set(int current, int max, int shield, bool instant)
    {
        SetFraction(max > 0 ? (float)current / max : 0f, instant);
        SetShield(current, max, shield);
        if (label != null) label.text = string.Format(format, current, max) + (shield > 0 ? $"  <color=#9FE3FF>+{shield}</color>" : "");
    }

    void SetShield(int current, int max, int shield)
    {
        if (shield <= 0 || max <= 0)
        {
            if (shieldSeg != null) shieldSeg.gameObject.SetActive(false);
            return;
        }
        if (shieldSeg == null)
        {
            var parent = fill != null ? fill.parent : transform;
            var img = UIFactory.CreatePanel(parent, "Shield", new Color(0.55f, 0.88f, 1f, 0.85f), false);
            shieldSeg = img.rectTransform;
            img.raycastTarget = false;
        }
        shieldSeg.gameObject.SetActive(true);
        shieldSeg.SetAsLastSibling();
        float w = Mathf.Clamp01((float)shield / max);
        float start = Mathf.Clamp01((float)current / max);
        if (start + w > 1f) start = 1f - w;
        shieldSeg.anchorMin = new Vector2(start, 0f);
        shieldSeg.anchorMax = new Vector2(start + w, 1f);
        shieldSeg.offsetMin = Vector2.zero;
        shieldSeg.offsetMax = Vector2.zero;
    }

    public void SetFraction(float value, bool instant = false)
    {
        target = Mathf.Clamp01(value);
        Apply(fill, target);
        if (instant || target >= ghostValue)
        {
            ghostValue = target;
            Apply(ghost, ghostValue);
        }
    }

    public void SetColor(Color c)
    {
        if (fillImage != null) fillImage.color = c;
    }

    void Update()
    {
        if (ghostValue > target)
        {
            ghostValue = Mathf.MoveTowards(ghostValue, target, Time.unscaledDeltaTime * ghostSpeed);
            Apply(ghost, ghostValue);
        }
    }

    static void Apply(RectTransform rt, float v)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(v, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Builds a bar inside <paramref name="parent"/>. Position it with the returned RectTransform.</summary>
    public static StatBar Build(Transform parent, string name, Color fillColor, int labelSize = 18)
    {
        var bg = UIFactory.CreatePanel(parent, name, new Color(0f, 0f, 0f, 0.55f), false);
        var track = UIFactory.CreateRect("Track", bg.transform);
        UIFactory.Stretch(track, 3, 3, 3, 3);

        var ghostImg = UIFactory.CreatePanel(track, "Ghost", new Color(1f, 0.85f, 0.85f, 0.85f), false);
        var fillImg = UIFactory.CreatePanel(track, "Fill", fillColor, false);

        var bar = bg.gameObject.AddComponent<StatBar>();
        bar.fill = fillImg.rectTransform;
        bar.ghost = ghostImg.rectTransform;
        bar.fillImage = fillImg;
        Apply(bar.fill, 1f);
        Apply(bar.ghost, 1f);

        if (labelSize > 0)
        {
            var t = UIFactory.CreateText(bg.transform, "Label", "", labelSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(t.rectTransform);
            UIFactory.AddOutline(t, new Color(0, 0, 0, 0.8f), 1.5f);
            bar.label = t;
        }
        return bar;
    }
}
