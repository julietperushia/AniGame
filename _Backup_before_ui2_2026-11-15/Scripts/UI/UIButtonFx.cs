using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Small hover/press scale animation and a click sound for buttons.</summary>
public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public float hoverScale = 1.04f;
    public float pressScale = 0.96f;

    Vector3 baseScale = Vector3.one;
    float target = 1f;
    float current = 1f;
    Button button;

    void Awake()
    {
        baseScale = transform.localScale;
        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(() => SfxSynth.Play("click", 0.6f));
    }

    void OnDisable()
    {
        current = target = 1f;
        transform.localScale = baseScale;
    }

    bool Interactable => button == null || button.interactable;

    public void OnPointerEnter(PointerEventData eventData) { if (Interactable) target = hoverScale; }
    public void OnPointerExit(PointerEventData eventData) { target = 1f; }
    public void OnPointerDown(PointerEventData eventData) { if (Interactable) target = pressScale; }
    public void OnPointerUp(PointerEventData eventData) { target = 1f; }

    void Update()
    {
        if (Mathf.Approximately(current, target)) return;
        current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 1.5f);
        transform.localScale = baseScale * current;
    }
}
