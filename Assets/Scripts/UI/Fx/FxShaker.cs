using UnityEngine;

/// <summary>Shake + zoom punch for any RectTransform; restores the original position/scale.</summary>
public class FxShaker : MonoBehaviour
{
    RectTransform rt;
    Vector2 basePos;
    Vector3 baseScale;
    float shakeUntil, shakeDuration, shakeStrength;
    float punchStart, punchDuration, punchAmount;
    bool shaking, punching;

    public static FxShaker For(RectTransform target)
    {
        var s = target.GetComponent<FxShaker>();
        if (s == null)
        {
            s = target.gameObject.AddComponent<FxShaker>();
            s.rt = target;
        }
        return s;
    }

    public void Shake(float duration, float strength)
    {
        if (!shaking) basePos = rt.anchoredPosition;
        float remaining = shaking ? shakeUntil - Time.unscaledTime : 0f;
        float currentStrength = shaking && shakeDuration > 0f ? shakeStrength * Mathf.Clamp01(remaining / shakeDuration) : 0f;
        shakeDuration = Mathf.Max(duration, remaining);
        shakeUntil = Time.unscaledTime + shakeDuration;
        shakeStrength = Mathf.Max(strength, currentStrength);
        shaking = true;
    }

    public void Punch(float amount, float duration)
    {
        if (!punching) baseScale = rt.localScale;
        punchStart = Time.unscaledTime;
        punchDuration = Mathf.Max(0.05f, duration);
        punchAmount = amount;
        punching = true;
    }

    void LateUpdate()
    {
        if (rt == null) { Destroy(this); return; }
        if (shaking)
        {
            float remain = shakeUntil - Time.unscaledTime;
            if (remain <= 0f) { rt.anchoredPosition = basePos; shaking = false; }
            else rt.anchoredPosition = basePos + Random.insideUnitCircle * shakeStrength * (remain / shakeDuration);
        }
        if (punching)
        {
            float k = (Time.unscaledTime - punchStart) / punchDuration;
            if (k >= 1f) { rt.localScale = baseScale; punching = false; }
            else
            {
                float s = k < 0.2f ? k / 0.2f : (1f - (k - 0.2f) / 0.8f) * (1f - (k - 0.2f) / 0.8f);
                rt.localScale = baseScale * (1f + punchAmount * s);
            }
        }
    }

    void OnDisable()
    {
        if (rt == null) return;
        if (shaking) rt.anchoredPosition = basePos;
        if (punching) rt.localScale = baseScale;
        shaking = punching = false;
    }
}
