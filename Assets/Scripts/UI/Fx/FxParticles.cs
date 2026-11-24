using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Lightweight 2D particles made of UI Images (sparks, confetti, rising sparkles).</summary>
public class FxParticles : MonoBehaviour
{
    public class P
    {
        public RectTransform rt;
        public Image img;
        public Vector2 pos, vel;
        public float size, life, age, spin, rot, delay, aspect = 1f;
        public Color color;
        public bool fadeLate;
        public float phase;
    }

    public float gravity = 900f;
    public float drag = 1.5f;
    public bool flutter;
    public float delay;
    readonly List<P> parts = new List<P>();

    public P Add(Sprite sprite, Vector2 pos, Vector2 vel, float size, Color color, float life, float spin, float delay = 0f)
    {
        var img = UIFactory.CreateImage(transform, "P", new Color(color.r, color.g, color.b, 0f), sprite);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = pos;
        var p = new P { rt = rt, img = img, pos = pos, vel = vel, size = size, color = color, life = Mathf.Max(0.05f, life), spin = spin, delay = delay, rot = Random.Range(0f, 360f), phase = Random.Range(0f, 6.3f) };
        parts.Add(p);
        return p;
    }

    void Update()
    {
        float dt = UIFx.Dt;
        if (delay > 0f) { delay -= dt; return; }
        int alive = 0;
        foreach (var p in parts)
        {
            if (p.rt == null) continue;
            if (p.delay > 0f) { p.delay -= dt; alive++; continue; }
            p.age += dt;
            float k = p.age / p.life;
            if (k >= 1f) { Destroy(p.rt.gameObject); p.rt = null; continue; }
            alive++;
            p.vel.y -= gravity * dt;
            p.vel *= Mathf.Max(0f, 1f - drag * dt);
            var offset = flutter ? new Vector2(Mathf.Sin(p.age * 5f + p.phase) * 120f * dt, 0f) : Vector2.zero;
            p.pos += p.vel * dt + offset;
            p.rot += p.spin * dt;
            p.rt.anchoredPosition = p.pos;
            p.rt.localRotation = Quaternion.Euler(0, 0, p.rot);
            float flip = flutter ? Mathf.Abs(Mathf.Cos(p.age * 7f + p.phase)) * 0.8f + 0.2f : 1f;
            float s = p.fadeLate ? 1f : Mathf.Lerp(1f, 0.3f, k);
            p.rt.sizeDelta = new Vector2(p.size * s, p.size * p.aspect * s * flip);
            float a = p.fadeLate ? (k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f) : (k < 0.1f ? k / 0.1f : 1f - (k - 0.1f) / 0.9f);
            p.img.color = new Color(p.color.r, p.color.g, p.color.b, p.color.a * a);
        }
        if (alive == 0) Destroy(gameObject);
    }
}
