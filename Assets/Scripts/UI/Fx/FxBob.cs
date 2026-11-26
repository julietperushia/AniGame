using UnityEngine;

/// <summary>Gentle up/down float around the element's own position.</summary>
public class FxBob : MonoBehaviour
{
    public float amplitude = 6f;
    public float speed = 1.6f;
    RectTransform rt;
    Vector2 basePos, lastSet;
    float phase;
    bool started;

    void Start()
    {
        rt = (RectTransform)transform;
        basePos = lastSet = rt.anchoredPosition;
        phase = Random.Range(0f, 6.3f);
        started = true;
    }

    void Update()
    {
        if (!started) return;
        if ((rt.anchoredPosition - lastSet).sqrMagnitude > 0.25f) basePos = rt.anchoredPosition; // moved by someone else: follow
        lastSet = basePos + new Vector2(0f, Mathf.Sin(Time.unscaledTime * speed + phase) * amplitude);
        rt.anchoredPosition = lastSet;
    }

    void OnDisable() { if (started && rt != null) rt.anchoredPosition = basePos; }
    void OnEnable() { if (started && rt != null) lastSet = rt.anchoredPosition; }
}
