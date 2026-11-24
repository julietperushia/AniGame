using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Anime-style scene transitions. A slanted panel with pink/purple edges sweeps over the screen,
/// the next scene loads under it, then the panel sweeps away while the new screen's panels slide
/// and fade in (<see cref="ScreenIntro"/>). <see cref="GameSession.LoadScene"/> goes through here.
/// Clicks are blocked only while the panel covers the screen.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public static bool Enabled = true;
    const float CoverTime = 0.2f;
    const float RevealTime = 0.32f;

    enum State { Idle, Covering, Loading, Revealing }

    static ScreenFader instance;

    RectTransform root;
    RectTransform sweep;
    RectTransform emblem;
    Image emblemImage;
    Image blocker;
    Coroutine routine;
    State state = State.Idle;
    float progress;          // 0 = off-screen left, 1 = covering, 2 = off-screen right
    string target;
    string queued;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Enabled) return;
        var f = Instance;
        if (f == null) return;
        f.progress = 1f;
        f.Apply();
        f.routine = f.StartCoroutine(f.RevealRoutine(true));
    }

    static ScreenFader Instance
    {
        get
        {
            if (instance == null && Application.isPlaying)
            {
                var go = new GameObject("ScreenFader", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                DontDestroyOnLoad(go);
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                instance = go.AddComponent<ScreenFader>();
                instance.Build();
            }
            return instance;
        }
    }

    /// <summary>Loads a scene behind the transition (falls back to a plain load outside Play mode).</summary>
    public static void Load(string scene)
    {
        if (!Application.isPlaying || !Enabled) { SceneManager.LoadScene(scene); return; }
        var f = Instance;
        if (f == null) { SceneManager.LoadScene(scene); return; }
        switch (f.state)
        {
            case State.Covering:
            case State.Loading:
                if (scene != f.target) f.queued = scene; // e.g. a screen redirecting in Start()
                return;
            default:
                if (f.routine != null) f.StopCoroutine(f.routine);
                f.routine = f.StartCoroutine(f.LoadRoutine(scene));
                return;
        }
    }

    void Build()
    {
        root = (RectTransform)transform;
        blocker = UIFactory.CreateImage(root, "Blocker", new Color(0, 0, 0, 0f));
        UIFactory.Stretch(blocker.rectTransform);
        blocker.raycastTarget = false;

        sweep = UIFactory.CreateRect("Sweep", root);
        sweep.anchorMin = sweep.anchorMax = new Vector2(0.5f, 0.5f);
        sweep.pivot = new Vector2(0.5f, 0.5f);
        sweep.localRotation = Quaternion.Euler(0, 0, -12f);

        var body = UIFactory.CreateImage(sweep, "Body", UIFactory.Hex("120E22"));
        UIFactory.Stretch(body.rectTransform);
        var theme = UIFactory.Theme;
        if (theme != null && theme.halftone != null)
        {
            var dots = UIFactory.CreateImage(sweep, "Dots", new Color(0.49f, 0.36f, 1f, 0.12f), theme.halftone);
            dots.type = Image.Type.Tiled;
            UIFactory.Stretch(dots.rectTransform);
        }
        if (theme != null && theme.speedLines != null)
        {
            var lines = UIFactory.CreateImage(sweep, "Lines", new Color(1f, 1f, 1f, 0.05f), theme.speedLines);
            UIFactory.Stretch(lines.rectTransform);
        }
        Edge(sweep, "LeadEdge", 1f, UIFactory.Accent2, 26f, 0f);
        Edge(sweep, "LeadEdge2", 1f, UIFactory.Gold, 8f, 34f);
        Edge(sweep, "TrailEdge", 0f, UIFactory.Accent, 26f, 0f);
        Edge(sweep, "TrailEdge2", 0f, UIFactory.Accent2, 8f, 34f);

        emblemImage = UIFactory.CreateImage(root, "Emblem", new Color(1f, 1f, 1f, 0f), theme != null && theme.sparkle != null ? theme.sparkle : UIFx.StarSprite);
        emblemImage.preserveAspect = true;
        emblem = emblemImage.rectTransform;
        emblem.anchorMin = emblem.anchorMax = new Vector2(0.5f, 0.5f);
        emblem.sizeDelta = new Vector2(150, 150);

        foreach (var g in GetComponentsInChildren<Graphic>(true)) if (g != blocker) g.raycastTarget = false;
        progress = 0f;
        Apply();
    }

    static void Edge(RectTransform parent, string name, float side, Color color, float width, float inset)
    {
        var img = UIFactory.CreateImage(parent, name, color);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(side, 0f);
        rt.anchorMax = new Vector2(side, 1f);
        rt.pivot = new Vector2(side, 0.5f);
        rt.sizeDelta = new Vector2(width, 0f);
        rt.anchoredPosition = new Vector2(side > 0.5f ? -inset : inset, 0f);
    }

    float Width => Mathf.Max(1280f, root.rect.width);
    float Height => Mathf.Max(720f, root.rect.height);

    void Apply()
    {
        if (sweep == null) return;
        float w = Width + Height * 0.5f;
        sweep.sizeDelta = new Vector2(w, Height * 1.5f);
        float off = (Width + w) / 2f + 120f;
        float x = progress <= 1f ? Mathf.Lerp(-off, 0f, progress) : Mathf.Lerp(0f, off, progress - 1f);
        sweep.anchoredPosition = new Vector2(x, 0f);
        bool visible = progress > 0.001f && progress < 1.999f;
        sweep.gameObject.SetActive(visible);
        emblem.gameObject.SetActive(visible);
    }

    IEnumerator LoadRoutine(string scene)
    {
        state = State.Covering;
        target = scene;
        queued = null;
        blocker.raycastTarget = true;
        if (progress <= 0f || progress >= 2f) progress = 0f;
        SfxSynth.Play("swoosh", 0.35f);
        while (!Mathf.Approximately(progress, 1f))
        {
            float step = Time.unscaledDeltaTime / CoverTime;
            progress = Mathf.MoveTowards(progress, 1f, step);
            Apply();
            AnimateEmblem(1f - Mathf.Abs(1f - progress));
            yield return null;
        }

        while (scene != null)
        {
            state = State.Loading;
            target = scene;
            queued = null;
            var op = SceneManager.LoadSceneAsync(scene);
            if (op == null) SceneManager.LoadScene(scene);
            else while (!op.isDone) { AnimateEmblem(1f); yield return null; }
            // Let the new screen run Start() (it may redirect to another scene).
            yield return null;
            AnimateEmblem(1f);
            yield return null;
            scene = queued;
        }

        target = null;
        yield return RevealRoutine(false);
    }

    IEnumerator RevealRoutine(bool firstScene)
    {
        state = State.Revealing;
        if (firstScene) { yield return null; yield return null; }
        ScreenIntro.Play();
        blocker.raycastTarget = false;
        float from = progress;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / RevealTime;
            float e = UIFx.EaseInOutCubic(Mathf.Clamp01(t));
            progress = Mathf.Lerp(from, 2f, e);
            Apply();
            AnimateEmblem(Mathf.Clamp01(1f - t * 2.2f));
            yield return null;
        }
        progress = 0f;
        Apply();
        state = State.Idle;
        routine = null;
    }

    void AnimateEmblem(float visibility)
    {
        if (emblemImage == null) return;
        emblemImage.color = new Color(1f, 0.9f, 0.97f, Mathf.Clamp01(visibility) * 0.9f);
        emblem.localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * 160f);
        emblem.localScale = Vector3.one * (0.8f + 0.15f * Mathf.Sin(Time.unscaledTime * 9f));
    }
}

/// <summary>
/// Entrance animation for a freshly loaded screen: each top-level panel of the scene's canvas fades in
/// and slides a little (top bars from above, everything else from below), one after another.
/// Full-screen layers, hidden panels and panels with their own CanvasGroup (modals) are left alone.
/// </summary>
public static class ScreenIntro
{
    public static void Play()
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas == null || !canvas.isRootCanvas || canvas.gameObject.scene != scene) continue;
            if (canvas.GetComponent<ScreenIntroRunner>() != null) continue;
            canvas.gameObject.AddComponent<ScreenIntroRunner>();
        }
    }
}
