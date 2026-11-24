using System;
using UnityEngine;

/// <summary>Generic tween driver used by <see cref="UIFx.Tween"/>.</summary>
public class FxTween : MonoBehaviour
{
    public float life = 0.5f;
    public float delay;
    public Action<float> step;
    public bool fxTime = true;
    public bool destroyObject = true;
    float t;

    void Update()
    {
        float dt = fxTime ? UIFx.Dt : Time.unscaledDeltaTime;
        if (delay > 0f) { delay -= dt; return; }
        t += dt;
        float k = Mathf.Clamp01(t / life);
        try { step?.Invoke(k); }
        catch (Exception e) { Debug.LogException(e); k = 1f; }
        if (k >= 1f)
        {
            if (destroyObject) Destroy(gameObject);
            else Destroy(this);
        }
    }
}
