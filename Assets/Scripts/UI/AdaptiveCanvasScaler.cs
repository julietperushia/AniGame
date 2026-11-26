using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps every screen inside its 1920x1080 design area on any window shape.
/// Wider than 16:9 -> match height (the canvas is always 1080 tall, extra width goes to the sides);
/// narrower than 16:9 -> match width (always 1920 wide, extra height goes to top and bottom).
/// With the old fixed 0.5 blend a wide window (e.g. "Free Aspect" in the Game view) squeezed the height,
/// so bottom buttons slid into the content above them. Added automatically to every canvas at runtime.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class AdaptiveCanvasScaler : MonoBehaviour
{
    const float DesignAspect = 1920f / 1080f;
    CanvasScaler scaler;
    int lastW, lastH;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        AttachAll();
        SceneManager.sceneLoaded += (s, m) => AttachAll();
    }

    /// <summary>Adds the component to every CanvasScaler that does not have it yet (scene and overlay canvases).</summary>
    public static void AttachAll()
    {
        foreach (var s in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (s != null && s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && s.GetComponent<AdaptiveCanvasScaler>() == null)
                s.gameObject.AddComponent<AdaptiveCanvasScaler>();
    }

    void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    void Update()
    {
        var size = DisplaySize();
        if ((int)size.x != lastW || (int)size.y != lastH) Apply();
    }

    /// <summary>Pixel size the canvas draws to (the screen, or a camera's render target).</summary>
    Vector2 DisplaySize()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            var r = canvas.pixelRect;
            if (r.width > 1f && r.height > 1f) return new Vector2(r.width, r.height);
        }
        return new Vector2(Screen.width, Screen.height);
    }

    /// <summary>Re-evaluates now (used by the editor renderer).</summary>
    public void Apply()
    {
        var size = DisplaySize();
        lastW = (int)size.x;
        lastH = (int)size.y;
        if (scaler == null || lastH <= 0) return;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = (float)lastW / lastH >= DesignAspect ? 1f : 0f;
    }
}
