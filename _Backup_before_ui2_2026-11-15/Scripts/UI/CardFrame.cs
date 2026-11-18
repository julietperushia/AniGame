using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rarity frame drawn over a card, with effects that grow with rarity:
///  Super Rare+  pulsing glow behind the card
///  Legendary+   a light band sweeping across the art
///  Mythic+      twinkling sparkles; Mythic glow shifts cyan/magenta
///  God          rainbow-tinted frame and glow
/// Frame sprites come from UITheme.rarityFrames (generated placeholders or your art).
/// </summary>
public class CardFrame : MonoBehaviour
{
    public Image frame;
    public Image glow;
    public RectTransform shineMask;
    public Image shine;
    public RectTransform sparkleLayer;
    public bool effects = true;

    CardRarity rarity;
    float time;
    float shineTimer;
    readonly List<Image> sparkles = new List<Image>();
    readonly List<float> sparklePhase = new List<float>();

    public CardRarity Rarity => rarity;

    public void SetRarity(CardRarity value)
    {
        rarity = value;
        var theme = UITheme.Instance;
        var sprite = theme != null ? theme.Frame(value) : null;
        if (frame != null)
        {
            frame.enabled = sprite != null;
            frame.sprite = sprite;
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;
            FitBorder();
        }
        int tier = GameRules.Tier(value);
        if (glow != null)
        {
            glow.gameObject.SetActive(effects && tier >= 3);
            glow.color = UIFactory.WithAlpha(CardStats.RarityColor(value), 0.45f);
        }
        if (shineMask != null) shineMask.gameObject.SetActive(effects && tier >= 6);
        BuildSparkles(effects && tier >= 7 ? (tier >= 8 ? 7 : 4) : 0);
        shineTimer = Random.Range(0f, 2f);
    }

    /// <summary>Fraction of the card width covered by the frame's 9-slice corner, so frames look the same on tiles and big cards.</summary>
    public float borderFraction = 0.17f;

    /// <summary>Scales the 9-slice border to the card size (generated frames have a 64 px border, user frames ~128 px).</summary>
    public void FitBorder()
    {
        if (frame == null || frame.sprite == null) return;
        float border = Mathf.Max(frame.sprite.border.x, frame.sprite.border.z);
        if (border <= 0f) return;
        var r = frame.rectTransform.rect;
        float size = Mathf.Min(r.width, r.height);
        if (size <= 1f) return;
        float wanted = Mathf.Max(10f, size * borderFraction);
        frame.pixelsPerUnitMultiplier = Mathf.Max(0.05f, border * 100f / (frame.sprite.pixelsPerUnit * wanted));
    }

    void OnRectTransformDimensionsChange() => FitBorder();

    void Awake()
    {
        // Sparkles made while building the scene in the editor are not tracked after loading: drop them, SetRarity re-creates them.
        if (!Application.isPlaying || sparkleLayer == null || sparkles.Count > 0) return;
        for (int i = sparkleLayer.childCount - 1; i >= 0; i--) Destroy(sparkleLayer.GetChild(i).gameObject);
    }

    void BuildSparkles(int count)
    {
        if (sparkleLayer == null) return;
        while (sparkles.Count > count)
        {
            var s = sparkles[sparkles.Count - 1];
            if (s != null) { if (Application.isPlaying) Destroy(s.gameObject); else DestroyImmediate(s.gameObject); }
            sparkles.RemoveAt(sparkles.Count - 1);
            sparklePhase.RemoveAt(sparklePhase.Count - 1);
        }
        var theme = UITheme.Instance;
        while (sparkles.Count < count)
        {
            var img = UIFactory.CreateImage(sparkleLayer, "Sparkle", Color.white, theme != null ? theme.sparkle : null);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(Random.value, Random.Range(0.05f, 0.95f));
            rt.sizeDelta = Vector2.one * Random.Range(18f, 34f);
            sparkles.Add(img);
            sparklePhase.Add(Random.Range(0f, 6.28f));
        }
    }

    void Update()
    {
        if (!effects) return;
        float dt = Time.unscaledDeltaTime;
        time += dt;
        int tier = GameRules.Tier(rarity);
        if (tier < 3) return;

        Color baseColor = CardStats.RarityColor(rarity);
        if (tier == 7) baseColor = Color.Lerp(new Color(0.2f, 0.95f, 0.95f), new Color(1f, 0.4f, 0.9f), 0.5f + 0.5f * Mathf.Sin(time * 1.3f));
        if (tier >= 8)
        {
            Color rainbow = Color.HSVToRGB(Mathf.Repeat(time * 0.12f, 1f), 0.45f, 1f);
            baseColor = rainbow;
            if (frame != null && frame.enabled) frame.color = Color.Lerp(Color.white, rainbow, 0.55f);
        }
        if (glow != null && glow.gameObject.activeSelf)
        {
            float pulse = 0.35f + 0.25f * (0.5f + 0.5f * Mathf.Sin(time * 2.2f));
            glow.color = UIFactory.WithAlpha(baseColor, pulse);
        }

        if (shine != null && shineMask != null && shineMask.gameObject.activeSelf)
        {
            shineTimer -= dt;
            float width = shineMask.rect.width;
            var rt = shine.rectTransform;
            if (shineTimer <= 0f)
            {
                float k = Mathf.Clamp01(-shineTimer / 0.9f);
                rt.anchoredPosition = new Vector2(Mathf.Lerp(-width * 0.9f, width * 0.9f, k), 0f);
                shine.color = new Color(1f, 1f, 1f, 0.55f * Mathf.Sin(k * Mathf.PI));
                if (k >= 1f) shineTimer = tier >= 8 ? 1.8f : 3.2f;
            }
            else shine.color = new Color(1f, 1f, 1f, 0f);
        }

        for (int i = 0; i < sparkles.Count; i++)
        {
            if (sparkles[i] == null) continue;
            float s = Mathf.Max(0f, Mathf.Sin(time * 2.5f + sparklePhase[i]));
            sparkles[i].color = UIFactory.WithAlpha(Color.Lerp(Color.white, baseColor, 0.4f), s);
            sparkles[i].rectTransform.localScale = Vector3.one * (0.4f + 0.8f * s);
            sparkles[i].rectTransform.localRotation = Quaternion.Euler(0, 0, time * 40f + sparklePhase[i] * 30f);
        }
    }

    /// <summary>
    /// Adds a frame overlay (last child of <paramref name="body"/>), a shine band masked to <paramref name="artArea"/>
    /// and a sparkle layer. <paramref name="glowImage"/> is an existing glow behind the card (may be null).
    /// </summary>
    public static CardFrame Attach(RectTransform body, Image glowImage, RectTransform artArea, float frameInset = 0f)
    {
        var fx = body.gameObject.AddComponent<CardFrame>();
        fx.glow = glowImage;

        var theme = UITheme.Instance;
        if (artArea != null)
        {
            fx.shineMask = UIFactory.CreateRect("ShineMask", artArea);
            UIFactory.Stretch(fx.shineMask);
            fx.shineMask.gameObject.AddComponent<RectMask2D>();
            fx.shine = UIFactory.CreateImage(fx.shineMask, "Shine", new Color(1, 1, 1, 0), theme != null ? theme.frameShine : null);
            var srt = fx.shine.rectTransform;
            srt.anchorMin = new Vector2(0.5f, 0f);
            srt.anchorMax = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(140f, 80f);
            srt.localRotation = Quaternion.Euler(0, 0, -18f);
            fx.shineMask.gameObject.SetActive(false);
        }

        fx.frame = UIFactory.CreateImage(body, "RarityFrame", Color.white, null, true, false);
        UIFactory.Stretch(fx.frame.rectTransform, -frameInset, -frameInset, -frameInset, -frameInset);
        fx.frame.enabled = false;

        fx.sparkleLayer = UIFactory.CreateRect("Sparkles", body);
        UIFactory.Stretch(fx.sparkleLayer, -10, -10, -10, -10);
        return fx;
    }
}
