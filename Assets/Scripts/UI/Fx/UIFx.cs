using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Animation / effects module. Everything is created at runtime, so screens need no scene rebuild:
/// sparks, confetti, rings, slashes, light bursts, glows, wind streaks, text slams, screen flashes,
/// card afterimages, shakes, zoom punches, pop-ins, spins, bobs and slow motion.
/// Effects spawn into a layer you pass (e.g. the battle's popup layer) or into <see cref="Overlay"/>,
/// a shared canvas above every screen. Positions are local to the layer (use <see cref="LocalPos"/>).
/// </summary>
public static class UIFx
{
    // ---------------------------------------------------------------- time

    /// <summary>Animation speed (battle speed x1/x2/x4). Battle resets it to 1 when it closes.</summary>
    public static float Speed = 1f;

    /// <summary>Master switch for screen shake / flashes (kept for a future settings toggle).</summary>
    public static bool ScreenEffects = true;

    static float slowFactor = 1f;
    static float slowUntil;

    /// <summary>Speed x slow motion. Card moves and effects advance by <see cref="Dt"/>.</summary>
    public static float TimeScale => Mathf.Max(0.05f, Speed) * (Time.unscaledTime < slowUntil ? slowFactor : 1f);
    public static float Dt => Time.unscaledDeltaTime * TimeScale;

    /// <summary>Slows card moves and effects for a moment (crit impacts).</summary>
    public static void SlowMo(float factor, float realSeconds)
    {
        slowFactor = Mathf.Clamp(factor, 0.05f, 1f);
        slowUntil = Time.unscaledTime + Mathf.Max(0f, realSeconds);
    }

    // ---------------------------------------------------------------- easing

    public static float EaseOutQuad(float k) => 1f - (1f - k) * (1f - k);
    public static float EaseInQuad(float k) => k * k;
    public static float EaseOutCubic(float k) { float f = 1f - k; return 1f - f * f * f; }
    public static float EaseInOutCubic(float k) => k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;
    public static float EaseOutBack(float k) { const float c1 = 1.70158f, c3 = c1 + 1f; float f = k - 1f; return 1f + c3 * f * f * f + c1 * f * f; }
    public static float Linear(float k) => k;

    public static float EaseOutBounce(float k)
    {
        const float n1 = 7.5625f, d1 = 2.75f;
        if (k < 1f / d1) return n1 * k * k;
        if (k < 2f / d1) { k -= 1.5f / d1; return n1 * k * k + 0.75f; }
        if (k < 2.5f / d1) { k -= 2.25f / d1; return n1 * k * k + 0.9375f; }
        k -= 2.625f / d1;
        return n1 * k * k + 0.984375f;
    }

    // ---------------------------------------------------------------- overlay + positions

    static RectTransform overlay;
    static bool sceneHooked;

    /// <summary>Shared effects canvas above every screen (no raycasts, cleared on scene change).</summary>
    public static RectTransform Overlay
    {
        get
        {
            if (overlay == null)
            {
                var go = new GameObject("UIFx Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                Object.DontDestroyOnLoad(go);
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 400;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                overlay = (RectTransform)go.transform;
                if (!sceneHooked)
                {
                    sceneHooked = true;
                    SceneManager.sceneLoaded += (s, m) => { if (overlay != null) UIFactory.Clear(overlay); };
                }
            }
            return overlay;
        }
    }

    static Camera CanvasCamera(Component c)
    {
        var canvas = c != null ? c.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    /// <summary>Screen point -> anchored position (centre anchor) inside a layer.</summary>
    public static Vector2 ScreenToLocal(RectTransform layer, Vector2 screen)
    {
        if (layer == null) return Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, CanvasCamera(layer), out Vector2 local);
        return local - layer.rect.center;
    }

    /// <summary>Centre of a UI element as an anchored position inside a layer (works across canvases).</summary>
    public static Vector2 LocalPos(RectTransform layer, RectTransform target, Vector2 offset = default)
    {
        if (layer == null || target == null) return offset;
        Vector3 world = target.TransformPoint(target.rect.center);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(CanvasCamera(target), world);
        return ScreenToLocal(layer, screen) + offset;
    }

    /// <summary>Mouse / touch position inside a layer.</summary>
    public static Vector2 PointerPos(RectTransform layer)
    {
        Vector2 screen = new Vector2(Screen.width / 2f, Screen.height / 2f);
        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer != null) screen = pointer.position.ReadValue();
        return ScreenToLocal(layer, screen);
    }

    // ---------------------------------------------------------------- generated sprites

    static Sprite ringSprite, slashSprite, dotSprite, streakSprite, starSprite;

    public static Sprite RingSprite => ringSprite != null ? ringSprite : (ringSprite = MakeSprite(128, 128, (u, v) =>
    {
        float d = Mathf.Sqrt(u * u + v * v);
        return Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(d - 0.84f) / 0.13f), 1.4f);
    }));

    public static Sprite DotSprite => dotSprite != null ? dotSprite : (dotSprite = MakeSprite(64, 64, (u, v) =>
    {
        float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
        return (1f - d) * (1f - d);
    }));

    /// <summary>Crescent blade streak, thin at the start, thick near the tip.</summary>
    public static Sprite SlashSprite => slashSprite != null ? slashSprite : (slashSprite = MakeSprite(256, 48, (u, v) =>
    {
        float x = (u + 1f) * 0.5f;
        float th = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(x)), 0.65f) * Mathf.Lerp(0.25f, 1f, x);
        float edge = Mathf.Clamp01((th - Mathf.Abs(v)) / (0.45f * th + 0.0001f));
        return edge * Mathf.Clamp01(x * 6f);
    }));

    public static Sprite StreakSprite => streakSprite != null ? streakSprite : (streakSprite = MakeSprite(128, 8, (u, v) =>
        Mathf.Sin(Mathf.PI * (u + 1f) * 0.5f) * Mathf.Clamp01(1f - Mathf.Abs(v))));

    /// <summary>4-point star (fallback when the theme has no sparkle sprite).</summary>
    public static Sprite StarSprite => starSprite != null ? starSprite : (starSprite = MakeSprite(64, 64, (u, v) =>
    {
        float au = Mathf.Abs(u), av = Mathf.Abs(v);
        float star = Mathf.Clamp01(1f - (au * av * 9f + Mathf.Sqrt(au * au + av * av) * 0.9f));
        return star * star;
    }));

    static Sprite MakeSprite(int w, int h, Func<float, float, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UIFx" };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(u, v)) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite Sparkle => UITheme.Instance != null && UITheme.Instance.sparkle != null ? UITheme.Instance.sparkle : StarSprite;
    static Sprite Glow => UITheme.Instance != null && UITheme.Instance.glow != null ? UITheme.Instance.glow : DotSprite;
    static Sprite Rays => UITheme.Instance != null && UITheme.Instance.lightRays != null ? UITheme.Instance.lightRays : null;

    static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    static Image NewImage(RectTransform layer, string name, Color color, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var img = UIFactory.CreateImage(layer, name, color, sprite);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return img;
    }

    /// <summary>Runs step(k) with k going 0..1 over life seconds, then destroys the object (or just the tween).</summary>
    public static FxTween Tween(GameObject go, float life, Action<float> step, float delay = 0f, bool fxTime = true, bool destroyObject = true)
    {
        if (go == null) return null;
        var tw = go.AddComponent<FxTween>();
        tw.life = Mathf.Max(0.01f, life);
        tw.delay = delay;
        tw.step = step;
        tw.fxTime = fxTime;
        tw.destroyObject = destroyObject;
        try { step?.Invoke(0f); } catch (Exception e) { Debug.LogException(e); }
        return tw;
    }

    // ---------------------------------------------------------------- particles

    /// <summary>Burst of sparks flying out from a point.</summary>
    public static FxParticles Sparks(RectTransform layer, Vector2 pos, Color color, int count = 16, float speed = 700f,
                                     float size = 34f, float life = 0.55f, float gravity = 900f, Sprite sprite = null,
                                     float angle = 90f, float spread = 360f, float delay = 0f)
    {
        if (layer == null || count <= 0) return null;
        var holder = UIFactory.CreateRect("Sparks", layer);
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
        holder.sizeDelta = Vector2.zero;
        var ps = holder.gameObject.AddComponent<FxParticles>();
        ps.gravity = gravity;
        ps.delay = delay;
        for (int i = 0; i < count; i++)
        {
            float a = (angle + UnityEngine.Random.Range(-spread / 2f, spread / 2f)) * Mathf.Deg2Rad;
            var vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * UnityEngine.Random.Range(0.35f, 1f);
            var c = Color.Lerp(color, Color.white, UnityEngine.Random.Range(0f, 0.45f));
            c.a = color.a;
            ps.Add(sprite != null ? sprite : Sparkle, pos, vel, size * UnityEngine.Random.Range(0.55f, 1.2f), c,
                   life * UnityEngine.Random.Range(0.7f, 1.15f), UnityEngine.Random.Range(-360f, 360f));
        }
        return ps;
    }

    /// <summary>Soft particles floating upward (heals, buffs, sparkles).</summary>
    public static FxParticles Rise(RectTransform layer, Vector2 pos, Color color, int count = 12, float width = 260f, float size = 30f, float life = 0.9f, Sprite sprite = null)
    {
        if (layer == null) return null;
        var holder = UIFactory.CreateRect("Rise", layer);
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
        var ps = holder.gameObject.AddComponent<FxParticles>();
        ps.gravity = -260f;
        for (int i = 0; i < count; i++)
        {
            var start = pos + new Vector2(UnityEngine.Random.Range(-width / 2f, width / 2f), UnityEngine.Random.Range(-width / 3f, width / 6f));
            var vel = new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(60f, 220f));
            ps.Add(sprite != null ? sprite : Sparkle, start, vel, size * UnityEngine.Random.Range(0.5f, 1.2f), color,
                   life * UnityEngine.Random.Range(0.7f, 1.2f), UnityEngine.Random.Range(-120f, 120f), UnityEngine.Random.Range(0f, 0.25f));
        }
        return ps;
    }

    static readonly Color[] confettiColors =
    {
        new Color(1f, 0.83f, 0.31f), new Color(1f, 0.3f, 0.55f), new Color(0.49f, 0.36f, 1f),
        new Color(0.3f, 0.86f, 0.52f), new Color(0.3f, 0.75f, 1f), new Color(1f, 1f, 1f)
    };

    /// <summary>Confetti falling across the whole layer.</summary>
    public static FxParticles Confetti(RectTransform layer, int count = 70, float life = 2.6f)
    {
        if (layer == null) return null;
        var holder = UIFactory.CreateRect("Confetti", layer);
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
        var ps = holder.gameObject.AddComponent<FxParticles>();
        ps.gravity = 520f;
        ps.drag = 0.6f;
        ps.flutter = true;
        float w = Mathf.Max(800f, layer.rect.width), h = Mathf.Max(600f, layer.rect.height);
        for (int i = 0; i < count; i++)
        {
            var start = new Vector2(UnityEngine.Random.Range(-w / 2f, w / 2f), h / 2f + UnityEngine.Random.Range(10f, 160f));
            var vel = new Vector2(UnityEngine.Random.Range(-160f, 160f), UnityEngine.Random.Range(-260f, 60f));
            var p = ps.Add(UIFactory.Theme != null ? UIFactory.Theme.roundedSmall : null, start, vel, UnityEngine.Random.Range(14f, 26f),
                           confettiColors[UnityEngine.Random.Range(0, confettiColors.Length)], life * UnityEngine.Random.Range(0.8f, 1.2f),
                           UnityEngine.Random.Range(-540f, 540f), UnityEngine.Random.Range(0f, 0.6f));
            p.aspect = UnityEngine.Random.Range(0.35f, 0.6f);
            p.fadeLate = true;
        }
        return ps;
    }

    // ---------------------------------------------------------------- shapes

    /// <summary>Expanding (or, with from &gt; to, shrinking) ring.</summary>
    public static Image Ring(RectTransform layer, Vector2 pos, Color color, float from, float to, float life = 0.4f, float delay = 0f)
    {
        if (layer == null) return null;
        float max = Mathf.Max(from, to);
        var img = NewImage(layer, "Ring", A(color, 0f), RingSprite, pos, Vector2.one * max);
        var rt = img.rectTransform;
        Tween(img.gameObject, life, k =>
        {
            float e = EaseOutCubic(k);
            rt.localScale = Vector3.one * (Mathf.Lerp(from, to, e) / max);
            float fadeIn = from > to ? Mathf.Clamp01(k * 4f) : 1f;
            img.color = A(color, color.a * fadeIn * (1f - k));
        }, delay);
        return img;
    }

    /// <summary>Blade slash drawn along angle (degrees; 0 = left to right).</summary>
    public static RectTransform Slash(RectTransform layer, Vector2 pos, float angle, Color color, float length = 480f, float thickness = 64f, float life = 0.3f, float delay = 0f)
    {
        if (layer == null) return null;
        var holder = UIFactory.CreateRect("Slash", layer);
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
        holder.sizeDelta = new Vector2(length, thickness);
        holder.anchoredPosition = pos;
        holder.localRotation = Quaternion.Euler(0, 0, angle);
        Image MakeBlade(string n, Color c, float th)
        {
            var img = UIFactory.CreateImage(holder, n, c, SlashSprite);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(length, th);
            rt.anchoredPosition = Vector2.zero;
            return img;
        }
        var outer = MakeBlade("Glow", A(color, 0f), thickness);
        var core = MakeBlade("Core", new Color(1f, 1f, 1f, 0f), thickness * 0.42f);
        Tween(holder.gameObject, life, k =>
        {
            float draw = EaseOutCubic(Mathf.Clamp01(k / 0.35f));
            float fade = k < 0.35f ? 1f : 1f - (k - 0.35f) / 0.65f;
            float thin = k < 0.35f ? 1f : Mathf.Lerp(1f, 0.25f, (k - 0.35f) / 0.65f);
            foreach (var b in new[] { outer, core })
                b.rectTransform.localScale = new Vector3(draw, thin, 1f);
            outer.color = A(color, color.a * fade);
            core.color = new Color(1f, 1f, 1f, fade);
        }, delay);
        return holder;
    }

    /// <summary>Rotating light rays burst.</summary>
    public static Image Starburst(RectTransform layer, Vector2 pos, Color color, float size = 700f, float life = 0.7f, float spin = 70f, float delay = 0f)
    {
        if (layer == null) return null;
        var sprite = Rays;
        var img = NewImage(layer, "Starburst", A(color, 0f), sprite != null ? sprite : Glow, pos, Vector2.one * size);
        var rt = img.rectTransform;
        float start = UnityEngine.Random.Range(0f, 360f);
        Tween(img.gameObject, life, k =>
        {
            rt.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.1f, EaseOutCubic(k));
            rt.localRotation = Quaternion.Euler(0, 0, start + spin * k);
            float a = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
            img.color = A(color, color.a * a);
        }, delay);
        return img;
    }

    /// <summary>Soft glow flash.</summary>
    public static Image GlowFlash(RectTransform layer, Vector2 pos, Color color, float size = 420f, float life = 0.4f, float delay = 0f)
    {
        if (layer == null) return null;
        var img = NewImage(layer, "Glow", A(color, 0f), Glow, pos, Vector2.one * size);
        var rt = img.rectTransform;
        Tween(img.gameObject, life, k =>
        {
            rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.25f, EaseOutQuad(k));
            float a = k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f;
            img.color = A(color, color.a * a);
        }, delay);
        return img;
    }

    /// <summary>Wind / speed streaks moving in direction dir (+1 right, -1 left).</summary>
    public static void Streaks(RectTransform layer, Vector2 pos, float dir, Color color, int count = 6, float height = 300f, float life = 0.32f)
    {
        if (layer == null) return;
        for (int i = 0; i < count; i++)
        {
            float len = UnityEngine.Random.Range(160f, 320f);
            var start = pos + new Vector2(-dir * UnityEngine.Random.Range(0f, 120f), UnityEngine.Random.Range(-height / 2f, height / 2f));
            var img = NewImage(layer, "Streak", A(color, 0f), StreakSprite, start, new Vector2(len, UnityEngine.Random.Range(4f, 9f)));
            var rt = img.rectTransform;
            float travel = UnityEngine.Random.Range(160f, 300f);
            float d = UnityEngine.Random.Range(0f, 0.08f);
            Tween(img.gameObject, life, k =>
            {
                rt.anchoredPosition = start + new Vector2(dir * travel * EaseOutQuad(k), 0f);
                rt.localScale = new Vector3(Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(k * 3f)), 1f, 1f);
                img.color = A(color, color.a * Mathf.Sin(Mathf.PI * k));
            }, d);
        }
    }

    /// <summary>Full-layer colour flash.</summary>
    public static Image ScreenFlash(RectTransform layer, Color color, float life = 0.3f)
    {
        if (layer == null || !ScreenEffects) return null;
        var img = UIFactory.CreateImage(layer, "Flash", color);
        img.raycastTarget = false;
        UIFactory.Stretch(img.rectTransform, -200, -200, -200, -200);
        Tween(img.gameObject, life, k => img.color = A(color, color.a * (1f - EaseOutQuad(k))), 0f, false);
        return img;
    }

    /// <summary>Big text that slams in, holds, then fades ("CRITICAL!", "EVADE!", "SUCCESS!").</summary>
    public static Text TextSlam(RectTransform layer, Vector2 pos, string text, Color color, int size = 96, float life = 0.9f, float tilt = -5f, float delay = 0f)
    {
        if (layer == null) return null;
        var txt = UIFactory.CreateText(layer, "Slam", text, size, A(color, 0f), TextAnchor.MiddleCenter, FontStyle.Bold);
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        UIFactory.AddOutline(txt, new Color(0f, 0f, 0f, 0.95f), Mathf.Max(2f, size / 30f));
        UIFactory.AddShadow(txt, A(Color.Lerp(color, Color.black, 0.55f), 0.9f), Mathf.Max(3f, size / 16f));
        var rt = txt.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1400f, size * 1.8f);
        rt.anchoredPosition = pos;
        rt.localRotation = Quaternion.Euler(0, 0, tilt);
        Tween(txt.gameObject, life, k =>
        {
            float s, a = 1f;
            Vector2 jitter = Vector2.zero;
            if (k < 0.14f) s = Mathf.Lerp(2.6f, 0.9f, EaseInQuad(k / 0.14f));
            else if (k < 0.24f) s = Mathf.Lerp(0.9f, 1f, (k - 0.14f) / 0.1f);
            else if (k < 0.72f) s = 1f + 0.02f * Mathf.Sin((k - 0.24f) * 40f);
            else { float f = (k - 0.72f) / 0.28f; s = 1f + 0.18f * f; a = 1f - f; }
            if (k > 0.12f && k < 0.3f) jitter = UnityEngine.Random.insideUnitCircle * size * 0.06f;
            rt.localScale = Vector3.one * s;
            rt.anchoredPosition = pos + jitter;
            txt.color = A(color, a * (k < 0.05f ? k / 0.05f : 1f));
        }, delay, false);
        return txt;
    }

    /// <summary>Small text floating up and fading ("+500 gold", "SAVED!").</summary>
    public static Text FloatText(RectTransform layer, Vector2 pos, string text, Color color, int size = 34, float life = 1.1f)
    {
        if (layer == null) return null;
        var txt = UIFactory.CreateText(layer, "FloatText", text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        UIFactory.AddOutline(txt, new Color(0f, 0f, 0f, 0.9f), 2f);
        var rt = txt.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, size * 1.6f);
        Tween(txt.gameObject, life, k =>
        {
            rt.anchoredPosition = pos + new Vector2(0f, 110f * EaseOutQuad(k));
            rt.localScale = Vector3.one * (k < 0.12f ? Mathf.Lerp(0.5f, 1.15f, k / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((k - 0.12f) / 0.15f)));
            txt.color = A(color, k < 0.65f ? 1f : 1f - (k - 0.65f) / 0.35f);
        }, 0f, false);
        return txt;
    }

    // ---------------------------------------------------------------- combos used by screens

    /// <summary>Celebration burst: rays + ring + sparks (+ optional glow).</summary>
    public static void Burst(RectTransform layer, Vector2 pos, Color color, float scale = 1f, bool rays = true)
    {
        if (layer == null) return;
        if (rays) Starburst(layer, pos, A(color, 0.6f), 620f * scale, 0.8f);
        GlowFlash(layer, pos, A(color, 0.7f), 380f * scale, 0.45f);
        Ring(layer, pos, A(color, 0.9f), 60f * scale, 420f * scale, 0.45f);
        Sparks(layer, pos, color, Mathf.RoundToInt(18 * Mathf.Clamp(scale, 0.6f, 2f)), 760f * scale, 32f * Mathf.Sqrt(scale), 0.6f);
    }

    /// <summary>Grey puff for failures.</summary>
    public static void Puff(RectTransform layer, Vector2 pos, Color color, float scale = 1f)
    {
        if (layer == null) return;
        Sparks(layer, pos, A(color, 0.75f), 14, 260f * scale, 60f * scale, 0.7f, -80f, DotSprite);
        Ring(layer, pos, A(color, 0.5f), 40f * scale, 260f * scale, 0.4f);
    }

    /// <summary>Burst at the mouse pointer, on the overlay.</summary>
    public static void BurstAtPointer(Color color, float scale = 0.8f)
    {
        var o = Overlay;
        Burst(o, PointerPos(o), color, scale, false);
    }

    /// <summary>Burst centred on a UI element, on the overlay.</summary>
    public static void BurstAt(RectTransform target, Color color, float scale = 1f, bool rays = true)
    {
        if (target == null) return;
        var o = Overlay;
        Burst(o, LocalPos(o, target), color, scale, rays);
    }

    // ---------------------------------------------------------------- element motion

    /// <summary>Shakes a UI element (restores its position afterwards).</summary>
    public static void Shake(RectTransform target, float duration, float strength)
    {
        if (target == null || !ScreenEffects) return;
        FxShaker.For(target).Shake(duration, strength);
    }

    /// <summary>Quick zoom punch (scale up then settle).</summary>
    public static void Punch(RectTransform target, float amount, float duration)
    {
        if (target == null || !ScreenEffects) return;
        FxShaker.For(target).Punch(amount, duration);
    }

    /// <summary>Scales an element in from zero with a little overshoot.</summary>
    public static void PopIn(RectTransform rt, float delay = 0f, float life = 0.3f, float from = 0f)
    {
        if (rt == null) return;
        var baseScale = rt.localScale;
        if (baseScale.sqrMagnitude < 0.0001f) baseScale = Vector3.one;
        var existing = rt.GetComponent<FxTween>();
        if (existing != null) Object.Destroy(existing);
        Tween(rt.gameObject, life, k =>
        {
            if (rt == null) return;
            float s = k < 0.7f ? Mathf.Lerp(from, 1.12f, EaseOutQuad(k / 0.7f)) : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
            rt.localScale = baseScale * s;
        }, delay, false, false);
    }

    /// <summary>Text / element punch: big then settle back to its scale.</summary>
    public static void PopScale(RectTransform rt, float from = 2.2f, float life = 0.35f, float delay = 0f)
    {
        if (rt == null) return;
        var baseScale = rt.localScale;
        if (baseScale.sqrMagnitude < 0.0001f) baseScale = Vector3.one;
        Tween(rt.gameObject, life, k =>
        {
            if (rt == null) return;
            float s = k < 0.45f ? Mathf.Lerp(from, 0.92f, EaseInQuad(k / 0.45f)) : Mathf.Lerp(0.92f, 1f, EaseOutBack((k - 0.45f) / 0.55f));
            rt.localScale = baseScale * s;
        }, delay, false, false);
    }

    /// <summary>Drops an element in from above with a bounce.</summary>
    public static void DropIn(RectTransform rt, float height = 260f, float life = 0.6f, float delay = 0f)
    {
        if (rt == null) return;
        var basePos = rt.anchoredPosition;
        Tween(rt.gameObject, life, k =>
        {
            if (rt == null) return;
            rt.anchoredPosition = basePos + new Vector2(0f, height * (1f - EaseOutBounce(k)));
        }, delay, false, false);
    }

    /// <summary>Spins an element forever (light rays behind a panel).</summary>
    public static void Spin(RectTransform rt, float degreesPerSecond)
    {
        if (rt == null) return;
        var s = rt.GetComponent<FxSpin>();
        if (s == null) s = rt.gameObject.AddComponent<FxSpin>();
        s.speed = degreesPerSecond;
    }

    /// <summary>Gentle up/down float (keeps the element's own position as the centre).</summary>
    public static void Bob(RectTransform rt, float amplitude = 6f, float speed = 1.6f)
    {
        if (rt == null) return;
        var b = rt.GetComponent<FxBob>();
        if (b == null) b = rt.gameObject.AddComponent<FxBob>();
        b.amplitude = amplitude;
        b.speed = speed;
    }

    /// <summary>Pulsing alpha / scale (glows behind important buttons).</summary>
    public static void Breathe(Graphic g, float minAlpha, float maxAlpha, float speed = 2f, float scaleAmount = 0.04f)
    {
        if (g == null) return;
        var b = g.GetComponent<FxBreathe>();
        if (b == null) b = g.gameObject.AddComponent<FxBreathe>();
        b.minAlpha = minAlpha;
        b.maxAlpha = maxAlpha;
        b.speed = speed;
        b.scaleAmount = scaleAmount;
    }

    /// <summary>A soft breathing glow placed right behind an element (important buttons).</summary>
    public static Image GlowBehind(RectTransform target, Color color, float pad = 40f, float minAlpha = 0.2f, float maxAlpha = 0.65f)
    {
        if (target == null || target.parent == null) return null;
        var img = UIFactory.CreateImage(target.parent, target.name + " Glow", color, Glow);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = target.anchorMin;
        rt.anchorMax = target.anchorMax;
        rt.pivot = target.pivot;
        rt.anchoredPosition = target.anchoredPosition;
        rt.sizeDelta = target.sizeDelta + Vector2.one * pad * 2f;
        rt.SetSiblingIndex(target.GetSiblingIndex());
        Breathe(img, minAlpha, maxAlpha, 2.2f, 0.05f);
        return img;
    }

    /// <summary>Tweens a graphic's colour to a flash colour and back.</summary>
    public static void FlashGraphic(Graphic g, Color flash, float life = 0.4f)
    {
        if (g == null) return;
        var baseColor = g.color;
        Tween(g.gameObject, life, k =>
        {
            if (g == null) return;
            g.color = Color.Lerp(flash, baseColor, EaseOutQuad(k));
        }, 0f, false, false);
    }

    // ---------------------------------------------------------------- card afterimage

    /// <summary>
    /// Leaves a fading, tinted copy of a card where it is now (behind it). Used for dashes and dodges.
    /// </summary>
    public static void Ghost(CardView view, Color tint, float life = 0.3f, float alpha = 0.5f, float tintAmount = 0.6f)
    {
        if (view == null || view.body == null || !view.gameObject.activeInHierarchy) return;
        var root = (RectTransform)view.transform;
        var parent = root.parent as RectTransform;
        if (parent == null) return;

        var holder = UIFactory.CreateRect("Ghost", parent);
        holder.gameObject.SetActive(false);
        holder.anchorMin = root.anchorMin;
        holder.anchorMax = root.anchorMax;
        holder.pivot = root.pivot;
        holder.sizeDelta = root.sizeDelta;
        holder.anchoredPosition = root.anchoredPosition;
        holder.localScale = root.localScale;
        holder.localRotation = root.localRotation;
        holder.SetSiblingIndex(root.GetSiblingIndex());

        var clone = Object.Instantiate(view.body.gameObject, holder, false);
        Strip(clone);
        var crt = (RectTransform)clone.transform;
        crt.anchoredPosition = view.body.anchoredPosition;
        crt.localRotation = view.body.localRotation;
        crt.localScale = view.body.localScale;
        foreach (var g in clone.GetComponentsInChildren<Graphic>(true))
        {
            g.raycastTarget = false;
            var c = g.color;
            g.color = new Color(Mathf.Lerp(c.r, tint.r, tintAmount), Mathf.Lerp(c.g, tint.g, tintAmount), Mathf.Lerp(c.b, tint.b, tintAmount), c.a);
        }
        var group = holder.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = alpha;
        holder.gameObject.SetActive(true);
        var baseScale = crt.localScale;
        Tween(holder.gameObject, life, k =>
        {
            group.alpha = alpha * (1f - k) * (1f - k);
            crt.localScale = baseScale * (1f + 0.06f * k);
        });
    }

    static void Strip(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb is Graphic || mb is BaseMeshEffect || mb is Mask || mb is RectMask2D) continue;
            Object.DestroyImmediate(mb);
        }
    }
}
