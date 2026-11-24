using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Shows <see cref="text"/> in a tooltip while hovered. Needs a raycast-target Graphic on the same object.</summary>
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea] public string text;

    public static TooltipTrigger Add(Graphic graphic, string text)
    {
        if (graphic == null) return null;
        graphic.raycastTarget = true;
        var t = graphic.GetComponent<TooltipTrigger>();
        if (t == null) t = graphic.gameObject.AddComponent<TooltipTrigger>();
        t.text = text;
        return t;
    }

    public void OnPointerEnter(PointerEventData eventData) => UITooltip.Show(text, (RectTransform)transform, this);
    public void OnPointerExit(PointerEventData eventData) => UITooltip.Hide(this);
    void OnDisable() => UITooltip.Hide(this);
}
