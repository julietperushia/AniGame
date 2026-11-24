using UnityEngine;
using UnityEngine.UI;

/// <summary>Pulsing alpha and scale (glows behind important buttons, halos).</summary>
public class FxBreathe : MonoBehaviour
{
    public float minAlpha = 0.2f, maxAlpha = 0.6f, speed = 2f, scaleAmount = 0.04f;
    Graphic g;
    Vector3 baseScale;
    float phase;

    void Awake()
    {
        g = GetComponent<Graphic>();
        baseScale = transform.localScale;
        phase = Random.Range(0f, 6.3f);
    }

    void Update()
    {
        if (g == null) return;
        float s = (Mathf.Sin(Time.unscaledTime * speed + phase) + 1f) * 0.5f;
        var c = g.color;
        g.color = new Color(c.r, c.g, c.b, Mathf.Lerp(minAlpha, maxAlpha, s));
        transform.localScale = baseScale * (1f + scaleAmount * s);
    }
}
