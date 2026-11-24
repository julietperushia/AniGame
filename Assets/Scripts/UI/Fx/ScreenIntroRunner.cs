using System.Collections.Generic;
using UnityEngine;

/// <summary>Runs the entrance animation of one canvas (see ScreenIntro).</summary>
public class ScreenIntroRunner : MonoBehaviour
{
    class Item
    {
        public RectTransform rt;
        public CanvasGroup group;
        public Vector2 basePos, lastSet, offset;
        public float delay;
        public bool slide = true;
    }

    const float Duration = 0.4f;
    readonly List<Item> items = new List<Item>();
    float t;

    void Start()
    {
        var rootRt = (RectTransform)transform;
        int n = 0;
        foreach (Transform childT in rootRt)
        {
            var child = childT as RectTransform;
            if (child == null || !child.gameObject.activeSelf) continue;
            if (child.GetComponent<CanvasGroup>() != null) continue;
            if (child.GetComponent<AnimeBackground>() != null) continue;
            if (child.GetComponent<FxBob>() != null || child.GetComponent<FxShaker>() != null) continue;
            if (child.anchorMin == Vector2.zero && child.anchorMax == Vector2.one) continue; // full-screen layers
            bool topBar = child.anchorMin.y > 0.99f && child.anchorMax.y > 0.99f && child.anchorMax.x - child.anchorMin.x > 0.9f;
            var item = new Item
            {
                rt = child,
                group = child.gameObject.AddComponent<CanvasGroup>(),
                basePos = child.anchoredPosition,
                offset = topBar ? new Vector2(0f, 80f) : new Vector2(0f, -40f),
                delay = Mathf.Min(0.32f, n * 0.04f)
            };
            item.group.alpha = 0f;
            item.rt.anchoredPosition = item.basePos + item.offset;
            item.lastSet = item.rt.anchoredPosition;
            items.Add(item);
            if (++n >= 48) break;
        }
        if (items.Count == 0) Destroy(this);
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        bool done = true;
        foreach (var it in items)
        {
            if (it.rt == null) continue;
            float k = Mathf.Clamp01((t - it.delay) / Duration);
            if (k < 1f) done = false;
            float e = UIFx.EaseOutCubic(k);
            if (it.group != null) it.group.alpha = Mathf.Clamp01(k * 1.6f);
            if (!it.slide) continue;
            if (it.rt.anchoredPosition != it.lastSet) { it.slide = false; continue; } // moved by its own script
            var p = it.basePos + it.offset * (1f - e);
            it.rt.anchoredPosition = p;
            it.lastSet = p;
        }
        if (done) Finish();
    }

    void Finish()
    {
        foreach (var it in items)
        {
            if (it.rt == null) continue;
            if (it.group != null) Destroy(it.group);
            if (it.slide) it.rt.anchoredPosition = it.basePos;
        }
        items.Clear();
        Destroy(this);
    }

    void OnDestroy()
    {
        foreach (var it in items)
        {
            if (it.rt == null) continue;
            if (it.group != null) it.group.alpha = 1f;
        }
    }
}
