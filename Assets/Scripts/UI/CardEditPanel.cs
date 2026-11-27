using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edit one card copy's own name and picture (opened by clicking the card's name in the collection).
/// Empty name / no picture = the player's character name / picture, which the Character screen changes.
/// </summary>
public class CardEditPanel : MonoBehaviour
{
    CardInstance card;
    BuildData build;
    string picture;
    InputField nameInput;
    Text counter, pictureText;
    CardTile preview;
    Action<string, string> onSave;

    public static CardEditPanel Show(Transform canvas, CardInstance card, BuildData build, string hint, Action<string, string> onSave)
    {
        if (canvas == null || card == null || build == null) return null;
        var dim = UIFactory.CreateImage(canvas, "CardEdit", new Color(0f, 0f, 0f, 0.75f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        dim.transform.SetAsLastSibling();
        var p = dim.gameObject.AddComponent<CardEditPanel>();
        p.card = card;
        p.build = build;
        p.picture = build.picture ?? "";
        p.onSave = onSave;

        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.09f, 0.1f, 0.17f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1220, 720));
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(UIFactory.Accent, 0.8f), 3f);
        var pt = panel.transform;

        var title = UIFactory.CreateText(pt, "Title", "EDIT CARD", 36, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(title.rectTransform, 18, 52, 20, 20);
        var hintText = UIFactory.CreateText(pt, "Hint", hint ?? "", 19, UIFactory.TextDim, TextAnchor.MiddleCenter);
        UIFactory.TopStrip(hintText.rectTransform, 70, 30, 20, 20);

        p.preview = CardTile.Build(pt, "Preview", 330, 440, true);
        UIFactory.TopLeft((RectTransform)p.preview.transform, 50, 120, 330, 440);

        const float x = 430;
        var nl = UIFactory.CreateText(pt, "NameLabel", "NAME", 24, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(nl.rectTransform, x, 120, 400, 36);
        p.nameInput = UIFactory.CreateInputField(pt, "Name", "", 28);
        UIFactory.TopLeft((RectTransform)p.nameInput.transform, x, 160, 520, 66);
        p.nameInput.characterLimit = PlayerProfile.MaxCardNameLength;
        p.nameInput.lineType = InputField.LineType.SingleLine;
        p.nameInput.text = build.name ?? "";
        var ph = p.nameInput.placeholder as Text;
        if (ph != null) ph.text = $"{PlayerProfile.CharacterName} (character name)";
        var defName = UIFactory.CreateButton(pt, "DefaultName", "DEFAULT NAME", new Color(0.35f, 0.3f, 0.45f), 20);
        UIFactory.TopLeft((RectTransform)defName.transform, x + 540, 160, 200, 66);
        defName.onClick.AddListener(() => p.nameInput.text = "");
        p.counter = UIFactory.CreateText(pt, "Counter", "", 16, UIFactory.TextDim, TextAnchor.MiddleRight);
        UIFactory.TopLeft(p.counter.rectTransform, x, 230, 520, 24);
        p.nameInput.onValueChanged.AddListener(_ => p.Refresh());

        var pl = UIFactory.CreateText(pt, "PictureLabel", "PICTURE", 24, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(pl.rectTransform, x, 280, 400, 36);
        p.pictureText = UIFactory.CreateText(pt, "PictureText", "", 21, UIFactory.TextMain, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(p.pictureText.rectTransform, x, 318, 740, 36);
        var change = UIFactory.CreateButton(pt, "ChangePicture", "CHANGE PICTURE", UIFactory.Accent, 24);
        UIFactory.TopLeft((RectTransform)change.transform, x, 366, 360, 76);
        change.onClick.AddListener(p.ChangePicture);
        TooltipTrigger.Add(change.targetGraphic, "Pick a picture from the anime gallery or import one from your PC (PNG / JPG, path or URL).");
        var useChar = UIFactory.CreateButton(pt, "CharacterPicture", "CHARACTER PICTURE", new Color(0.35f, 0.3f, 0.45f), 22);
        UIFactory.TopLeft((RectTransform)useChar.transform, x + 380, 366, 360, 76);
        useChar.onClick.AddListener(() => { p.picture = ""; p.Refresh(); });

        var note = UIFactory.CreateText(pt, "Note",
            "Only this copy changes. Without its own name or picture a card uses your character's (Character screen), " +
            "so changing your character later does not touch cards you edited here.", 18, UIFactory.TextDim, TextAnchor.UpperLeft, FontStyle.Italic);
        UIFactory.TopLeft(note.rectTransform, x, 466, 740, 90);

        var save = UIFactory.CreateButton(pt, "Save", "SAVE", new Color(0.2f, 0.65f, 0.4f), 26);
        UIFactory.Place((RectTransform)save.transform, new Vector2(0.5f, 0f), new Vector2(-150, 34), new Vector2(260, 70));
        save.onClick.AddListener(p.Save);
        var cancel = UIFactory.CreateButton(pt, "Cancel", "CANCEL", UIFactory.PanelLight, 26);
        UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(150, 34), new Vector2(260, 70));
        cancel.onClick.AddListener(p.Close);
        dim.gameObject.AddComponent<PromptKeys>().onEscape = p.Close;

        p.Refresh();
        UIFx.PopIn(panel.rectTransform, 0f, 0.2f);
        return p;
    }

    void Close() { if (this != null) Destroy(gameObject); }

    void Save()
    {
        string name = nameInput.text, pic = picture;
        var cb = onSave;
        Close();
        cb?.Invoke(name, pic);
    }

    void ChangePicture()
    {
        PicturePicker.Show(transform.parent, "CARD PICTURE", choice =>
        {
            if (this == null || choice == null) return;
            if (choice.galleryCard != null) picture = choice.GallerySource;
            else if (choice.png != null)
            {
                string src = PictureStore.SavePlayer(choice.png, choice.preview, PlayerProfile.CleanCardName(nameInput.text));
                if (src == null) { pictureText.text = "<color=#FF6B6B>Could not save the picture.</color>"; return; }
                picture = src;
            }
            Refresh();
        });
    }

    void Refresh()
    {
        string name = PlayerProfile.CleanCardName(nameInput.text);
        counter.text = $"{(nameInput.text ?? "").Length} / {PlayerProfile.MaxCardNameLength}";
        var g = PictureStore.GalleryCard(picture);
        pictureText.text = string.IsNullOrEmpty(picture) ? "<color=#9AA4B5>Your character's picture</color>"
                         : g != null ? $"Own picture: <b>{g.DisplayName}</b>{(string.IsNullOrEmpty(g.anime) ? "" : $" <color=#9AA4B5>({g.anime})</color>")}"
                         : "Own picture: <b>imported</b>";
        var tmp = build.Clone();
        tmp.name = name;
        tmp.picture = picture;
        preview.SetInstance(card, tmp);
    }
}
