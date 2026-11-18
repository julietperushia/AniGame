using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Anime-style animated background: colour gradient, optional screen art (UITheme), slowly turning light rays,
/// a halftone dot overlay, drifting sakura petals and twinkling sparkles. Runs on unscaled time.
/// </summary>
public class AnimeBackground : MonoBehaviour
{
    public string screenKey = "menu";
    public Image art;
    public Image rays;
    public Image halftone;
    public RectTransform petalLayer;
    public RectTransform sparkleLayer;
    public int petalCount = 18;
    public int sparkleCount = 12;
    public Color petalTint = new Color(1f, 0.75f, 0.85f, 0.85f);
    public float raysSpeed = 4f;

    class Petal
    {
        public RectTransform rt;
        public Image img;
        public Vector2 velocity;
        public float spin;
        public float swayPhase;
        public float swayAmount;
    }

    readonly List<Petal> petals = new List<Petal>();
    readonly List<Image> sparkles = new List<Image>();
    readonly List<float> sparklePhase = new List<float>();
    float time;

    void Start()
    {
        var theme = UITheme.Instance;
        if (art != null)
        {
            var sprite = theme != null ? theme.Background(screenKey) : null;
            art.sprite = sprite;
            art.enabled = sprite != null;
            if (sprite != null) art.preserveAspect = false;
        }
        // Adopt the petals / sparkles placed when the scene was built, then top up to the wanted count.
        if (petalLayer != null && theme != null && theme.sakuraPetal != null)
        {
            foreach (Transform child in petalLayer)
            {
                var img = child.GetComponent<Image>();
                if (img == null) continue;
                var p = new Petal { img = img, rt = img.rectTransform };
                p.rt.anchorMin = p.rt.anchorMax = new Vector2(0.5f, 0.5f);
                Respawn(p, true);
                petals.Add(p);
            }
            while (petals.Count < petalCount) petals.Add(SpawnPetal(theme.sakuraPetal, true));
        }
        if (sparkleLayer != null && theme != null && theme.sparkle != null)
        {
            foreach (Transform child in sparkleLayer)
            {
                var img = child.GetComponent<Image>();
                if (img == null) continue;
                sparkles.Add(img);
                sparklePhase.Add(Random.Range(0f, 6.28f));
            }
            while (sparkles.Count < sparkleCount) AddSparkle(theme.sparkle);
        }
    }

    void AddSparkle(Sprite sprite)
    {
        var img = UIFactory.CreateImage(sparkleLayer, "Sparkle", Color.white, sprite);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(Random.value, Random.value);
        rt.sizeDelta = Vector2.one * Random.Range(14f, 30f);
        sparkles.Add(img);
        sparklePhase.Add(Random.Range(0f, 6.28f));
    }

    Petal SpawnPetal(Sprite sprite, bool anywhere)
    {
        var img = UIFactory.CreateImage(petalLayer, "Petal", petalTint, sprite);
        var p = new Petal { img = img, rt = img.rectTransform };
        p.rt.anchorMin = p.rt.anchorMax = new Vector2(0.5f, 0.5f);
        Respawn(p, anywhere);
        return p;
    }

    void Respawn(Petal p, bool anywhere)
    {
        var size = petalLayer.rect.size;
        if (size.x < 10f) size = new Vector2(1920f, 1080f);
        float x = Random.Range(-size.x * 0.6f, size.x * 0.5f);
        float y = anywhere ? Random.Range(-size.y * 0.5f, size.y * 0.5f) : size.y * 0.5f + Random.Range(20f, 160f);
        p.rt.anchoredPosition = new Vector2(x, y);
        float scale = Random.Range(0.5f, 1.15f);
        p.rt.sizeDelta = new Vector2(34f, 28f) * scale;
        p.velocity = new Vector2(Random.Range(25f, 70f), -Random.Range(40f, 95f)) * (0.6f + scale * 0.5f);
        p.spin = Random.Range(-90f, 90f);
        p.swayPhase = Random.Range(0f, 6.28f);
        p.swayAmount = Random.Range(10f, 35f);
        p.img.color = UIFactory.WithAlpha(petalTint, petalTint.a * Random.Range(0.55f, 1f));
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        time += dt;
        if (rays != null) rays.rectTransform.localRotation = Quaternion.Euler(0, 0, time * raysSpeed);

        if (petalLayer != null)
        {
            float bottom = -petalLayer.rect.height * 0.5f - 60f;
            float right = petalLayer.rect.width * 0.5f + 80f;
            foreach (var p in petals)
            {
                var pos = p.rt.anchoredPosition;
                pos += p.velocity * dt;
                pos.x += Mathf.Sin(time * 1.4f + p.swayPhase) * p.swayAmount * dt;
                p.rt.anchoredPosition = pos;
                p.rt.localRotation = Quaternion.Euler(0, 0, p.rt.localEulerAngles.z + p.spin * dt);
                if (pos.y < bottom || pos.x > right) Respawn(p, false);
            }
        }

        for (int i = 0; i < sparkles.Count; i++)
        {
            float s = Mathf.Max(0f, Mathf.Sin(time * 1.6f + sparklePhase[i]));
            sparkles[i].color = new Color(1f, 1f, 1f, s * 0.8f);
            sparkles[i].rectTransform.localScale = Vector3.one * (0.3f + s);
            if (s <= 0.001f && Random.value < 0.02f)
                sparkles[i].rectTransform.anchorMin = sparkles[i].rectTransform.anchorMax = new Vector2(Random.value, Random.value);
        }
    }

    /// <summary>Builds the whole background as the first child of <paramref name="parent"/>.</summary>
    public static AnimeBackground Build(Transform parent, string screenKey, Color top, Color bottom, out Image gradientOverlay)
    {
        var theme = UIFactory.Theme;
        var root = UIFactory.CreateImage(parent, "AnimeBackground", bottom, null, false, false);
        UIFactory.Stretch(root.rectTransform);
        var bg = root.gameObject.AddComponent<AnimeBackground>();
        bg.screenKey = screenKey;

        gradientOverlay = UIFactory.CreateImage(root.transform, "Gradient", top, theme != null ? theme.gradient : null);
        UIFactory.Stretch(gradientOverlay.rectTransform);

        bg.art = UIFactory.CreateImage(root.transform, "ScreenArt", Color.white);
        UIFactory.Stretch(bg.art.rectTransform);
        var screenArt = theme != null ? theme.Background(screenKey) : null;
        bg.art.sprite = screenArt;
        bg.art.enabled = screenArt != null;

        bg.rays = UIFactory.CreateImage(root.transform, "LightRays", new Color(1f, 1f, 1f, 0.10f), theme != null ? theme.lightRays : null);
        UIFactory.Place(bg.rays.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 600), new Vector2(2600, 2600));
        bg.rays.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        bg.rays.enabled = theme != null && theme.lightRays != null;

        bg.halftone = UIFactory.CreateImage(root.transform, "Halftone", new Color(1f, 1f, 1f, 0.03f), theme != null ? theme.halftone : null);
        UIFactory.Stretch(bg.halftone.rectTransform);
        if (theme != null && theme.halftone != null) bg.halftone.type = Image.Type.Tiled;
        else bg.halftone.enabled = false;

        bg.petalLayer = UIFactory.CreateRect("Petals", root.transform);
        UIFactory.Stretch(bg.petalLayer);
        bg.sparkleLayer = UIFactory.CreateRect("Sparkles", root.transform);
        UIFactory.Stretch(bg.sparkleLayer);

        // A few static petals / sparkles so the scene already looks alive in the editor; Start() animates them.
        var rng = new System.Random(screenKey.GetHashCode());
        if (theme != null && theme.sakuraPetal != null)
            for (int i = 0; i < bg.petalCount; i++)
            {
                var petal = UIFactory.CreateImage(bg.petalLayer, "Petal", UIFactory.WithAlpha(bg.petalTint, bg.petalTint.a * (0.55f + 0.45f * (float)rng.NextDouble())), theme.sakuraPetal);
                var prt = petal.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                prt.sizeDelta = new Vector2(34f, 28f) * (0.5f + 0.65f * (float)rng.NextDouble());
                prt.localRotation = Quaternion.Euler(0, 0, (float)rng.NextDouble() * 360f);
            }
        if (theme != null && theme.sparkle != null)
            for (int i = 0; i < bg.sparkleCount; i++)
            {
                var sp = UIFactory.CreateImage(bg.sparkleLayer, "Sparkle", new Color(1f, 1f, 1f, 0.3f + 0.5f * (float)rng.NextDouble()), theme.sparkle);
                var srt = sp.rectTransform;
                srt.anchorMin = srt.anchorMax = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                srt.sizeDelta = Vector2.one * (14f + 16f * (float)rng.NextDouble());
            }

        if (theme != null && theme.vignette != null)
        {
            var vig = UIFactory.CreateImage(root.transform, "Vignette", new Color(0, 0, 0, 0.7f), theme.vignette);
            UIFactory.Stretch(vig.rectTransform);
        }
        return bg;
    }
}
