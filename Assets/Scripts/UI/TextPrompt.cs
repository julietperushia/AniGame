using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small modal dialog with one text field (built at runtime, destroyed when closed).
/// Used to rename cards. Enter saves, Escape or CANCEL closes.
/// </summary>
public static class TextPrompt
{
    /// <param name="onSave">Gets the typed text (not cleaned).</param>
    /// <param name="resetLabel">Optional third button (e.g. "DEFAULT NAME"); calls onReset.</param>
    public static GameObject Show(Transform canvas, string title, string hint, string initial, int maxLength,
                                  Action<string> onSave, string resetLabel = null, Action onReset = null)
    {
        if (canvas == null) return null;
        var dim = UIFactory.CreateImage(canvas, "TextPrompt", new Color(0f, 0f, 0f, 0.72f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        dim.transform.SetAsLastSibling();

        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.09f, 0.1f, 0.17f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 330));
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(UIFactory.Accent, 0.8f), 3f);

        var titleText = UIFactory.CreateText(panel.transform, "Title", title, 34, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(titleText.rectTransform, 20, 50, 20, 20);
        var hintText = UIFactory.CreateText(panel.transform, "Hint", hint ?? "", 18, UIFactory.TextDim, TextAnchor.MiddleCenter);
        UIFactory.TopStrip(hintText.rectTransform, 72, 30, 20, 20);

        var input = UIFactory.CreateInputField(panel.transform, "Input", "Type a name", 28);
        UIFactory.TopStrip((RectTransform)input.transform, 114, 64, 50, 50);
        input.characterLimit = Mathf.Max(1, maxLength);
        input.lineType = InputField.LineType.SingleLine;
        input.text = initial ?? "";

        var counter = UIFactory.CreateText(panel.transform, "Counter", "", 16, UIFactory.TextDim, TextAnchor.MiddleRight);
        UIFactory.TopStrip(counter.rectTransform, 180, 24, 50, 50);
        void Count(string s) => counter.text = $"{(s ?? "").Length} / {maxLength}";
        Count(input.text);
        input.onValueChanged.AddListener(Count);

        void Close() { if (dim != null) UnityEngine.Object.Destroy(dim.gameObject); }
        void Save() { var text = input.text; Close(); onSave?.Invoke(text); }

        bool hasReset = !string.IsNullOrEmpty(resetLabel);
        float w = hasReset ? 200f : 260f;
        var save = UIFactory.CreateButton(panel.transform, "Save", "SAVE", new Color(0.2f, 0.65f, 0.4f), 24);
        var cancel = UIFactory.CreateButton(panel.transform, "Cancel", "CANCEL", UIFactory.PanelLight, 24);
        if (hasReset)
        {
            UIFactory.Place((RectTransform)save.transform, new Vector2(0.5f, 0f), new Vector2(-220, 30), new Vector2(w, 64));
            var reset = UIFactory.CreateButton(panel.transform, "Reset", resetLabel, new Color(0.35f, 0.3f, 0.45f), 20);
            UIFactory.Place((RectTransform)reset.transform, new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(w, 64));
            reset.onClick.AddListener(() => { Close(); onReset?.Invoke(); });
            UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(220, 30), new Vector2(w, 64));
        }
        else
        {
            UIFactory.Place((RectTransform)save.transform, new Vector2(0.5f, 0f), new Vector2(-145, 30), new Vector2(w, 64));
            UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(145, 30), new Vector2(w, 64));
        }
        save.onClick.AddListener(Save);
        cancel.onClick.AddListener(Close);
        input.onEndEdit.AddListener(s =>
        {
            if (PromptKeys.EnterPressed) Save();
        });
        dim.gameObject.AddComponent<PromptKeys>().onEscape = Close;

        input.Select();
        input.ActivateInputField();
        UIFx.PopIn(panel.rectTransform, 0f, 0.2f);
        return dim.gameObject;
    }
}
