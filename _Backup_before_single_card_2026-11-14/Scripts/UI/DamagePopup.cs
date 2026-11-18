using UnityEngine;
using UnityEngine.UI;

/// <summary>Floating combat text (damage numbers, MISS, heals...). Spawned into a full-screen layer.</summary>
public class DamagePopup : MonoBehaviour
{
    /// <summary>Global speed multiplier (battle speed x1/x2/x4).</summary>
    public static float SpeedMultiplier = 1f;

    Text text;
    RectTransform rt;
    Vector2 start;
    float life;
    float t;
    float drift;
    Color baseColor;

    public static DamagePopup Spawn(RectTransform layer, Vector2 localPos, string message, Color color, int size, float life = 1.1f)
    {
        if (layer == null) return null;
        var txt = UIFactory.CreateText(layer, "Popup", message, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        UIFactory.AddOutline(txt, new Color(0, 0, 0, 0.9f), 2.5f);
        UIFactory.AddShadow(txt, new Color(0, 0, 0, 0.5f), 4f);
        var rect = txt.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(400, size * 1.6f);
        rect.anchoredPosition = localPos;

        var p = txt.gameObject.AddComponent<DamagePopup>();
        p.text = txt;
        p.rt = rect;
        p.start = localPos;
        p.life = life;
        p.baseColor = color;
        p.drift = Random.Range(-40f, 40f);
        rect.localScale = Vector3.one * 0.4f;
        return p;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime * Mathf.Max(0.1f, SpeedMultiplier);
        float k = Mathf.Clamp01(t / life);

        // Pop in, then float up and fade out
        float scale = k < 0.15f ? Mathf.Lerp(0.4f, 1.25f, k / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((k - 0.15f) / 0.2f));
        rt.localScale = Vector3.one * scale;
        float ease = 1f - (1f - k) * (1f - k);
        rt.anchoredPosition = start + new Vector2(drift * ease, 130f * ease);
        float alpha = k < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.6f) / 0.4f);
        text.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

        if (k >= 1f) Destroy(gameObject);
    }
}
