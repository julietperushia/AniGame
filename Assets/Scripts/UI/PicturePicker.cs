using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>What the picture picker returns: a built-in card's art, or an imported picture (cropped PNG, not saved yet).</summary>
public class PictureChoice
{
    public Card galleryCard;   // set when picked from the gallery
    public byte[] png;         // set when imported (file, path or URL), already cropped to the card shape
    public Sprite preview;
    public string GallerySource => galleryCard != null ? PictureStore.Gallery + galleryCard.Key : null;
}

/// <summary>
/// Overlay to choose a picture: the anime gallery (every character card in the game, searchable) or a picture from the PC
/// (file picker, path or image URL). Used by the card editor (collection) and the boss editor.
/// </summary>
public class PicturePicker : MonoBehaviour
{
    const int PageSize = 21;

    Action<PictureChoice> onPick;
    InputField search, pathInput;
    RectTransform grid;
    Text pageText, message;
    Button prev, next;
    List<Card> all = new List<Card>(), filtered = new List<Card>();
    int page;

    public static PicturePicker Show(Transform canvas, string title, Action<PictureChoice> onPick)
    {
        if (canvas == null) return null;
        var dim = UIFactory.CreateImage(canvas, "PicturePicker", new Color(0f, 0f, 0f, 0.85f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        dim.transform.SetAsLastSibling();
        var p = dim.gameObject.AddComponent<PicturePicker>();
        p.onPick = onPick;

        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.08f, 0.07f, 0.15f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1680, 980));
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(UIFactory.Accent, 0.8f), 3f);
        var pt = panel.transform;

        var head = UIFactory.CreateText(pt, "Title", title, 36, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(head.rectTransform, 40, 18, 700, 56);
        var close = UIFactory.CreateButton(pt, "Close", "CANCEL", UIFactory.PanelLight, 24);
        UIFactory.TopLeft((RectTransform)close.transform, 1440, 20, 200, 58);
        close.onClick.AddListener(p.Close);

        // ---- from the PC
        var import = UIFactory.CreateButton(pt, "Import", "IMPORT FROM PC", UIFactory.Accent, 24);
        UIFactory.TopLeft((RectTransform)import.transform, 40, 92, 330, 62);
        import.interactable = CharacterPortrait.CanPickFiles;
        import.onClick.AddListener(p.ImportFromPc);
        p.pathInput = UIFactory.CreateInputField(pt, "Path", "or paste a picture path (C:\\...\\art.png) or an image URL", 22);
        UIFactory.TopLeft((RectTransform)p.pathInput.transform, 390, 92, 1060, 62);
        var load = UIFactory.CreateButton(pt, "Load", "LOAD", UIFactory.PanelLight, 24);
        UIFactory.TopLeft((RectTransform)load.transform, 1470, 92, 170, 62);
        load.onClick.AddListener(p.LoadPath);
        p.message = UIFactory.CreateText(pt, "Message", "PNG or JPG, cropped to the card shape automatically.", 19, UIFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
        UIFactory.TopLeft(p.message.rectTransform, 40, 160, 1600, 30);

        // ---- gallery
        var gl = UIFactory.CreateText(pt, "GalleryLabel", "ANIME GALLERY", 24, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(gl.rectTransform, 40, 200, 400, 50);
        p.search = UIFactory.CreateInputField(pt, "Search", "Search a character or anime...", 24);
        UIFactory.TopLeft((RectTransform)p.search.transform, 440, 196, 1200, 58);
        p.search.onValueChanged.AddListener(_ => { p.page = 0; p.Filter(); });

        p.grid = UIFactory.CreateRect("Grid", pt);
        UIFactory.Stretch(p.grid, 40, 268, 40, 100);
        var g = p.grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(145, 190);
        g.spacing = new Vector2(80, 10);
        g.padding = new RectOffset(20, 10, 4, 4);
        g.childAlignment = TextAnchor.UpperCenter;

        p.prev = UIFactory.CreateButton(pt, "Prev", "< PREV", UIFactory.PanelLight, 24);
        UIFactory.Place((RectTransform)p.prev.transform, new Vector2(0.5f, 0f), new Vector2(-420, 22), new Vector2(200, 60));
        p.prev.onClick.AddListener(() => { p.page = Mathf.Max(0, p.page - 1); p.Refresh(); });
        p.pageText = UIFactory.CreateText(pt, "Page", "", 22, UIFactory.TextDim);
        UIFactory.Place(p.pageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(560, 60));
        p.next = UIFactory.CreateButton(pt, "Next", "NEXT >", UIFactory.PanelLight, 24);
        UIFactory.Place((RectTransform)p.next.transform, new Vector2(0.5f, 0f), new Vector2(420, 22), new Vector2(200, 60));
        p.next.onClick.AddListener(() => { p.page = Mathf.Min(p.MaxPage, p.page + 1); p.Refresh(); });

        var db = CardDatabase.Instance;
        if (db != null)
        {
            var seen = new HashSet<string>();
            foreach (var c in db.AllCharacterCards())
            {
                if (c == null || (c.cardImg == null && string.IsNullOrEmpty(c.imageUrl))) continue;
                string id = !string.IsNullOrEmpty(c.characterId) ? c.characterId : c.DisplayName;
                if (seen.Add(id)) p.all.Add(c);
            }
            p.all = p.all.OrderBy(c => c.DisplayName).ToList();
        }
        dim.gameObject.AddComponent<PromptKeys>().onEscape = p.Close;
        p.Filter();
        UIFx.PopIn(panel.rectTransform, 0f, 0.2f);
        return p;
    }

    int MaxPage => Mathf.Max(0, (filtered.Count - 1) / PageSize);

    void Close() { if (this != null) Destroy(gameObject); }

    void Choose(PictureChoice c)
    {
        var cb = onPick;
        Close();
        cb?.Invoke(c);
    }

    void Filter()
    {
        string q = (search.text ?? "").Trim().ToLowerInvariant();
        filtered = q.Length == 0 ? all : all.Where(c => c.DisplayName.ToLowerInvariant().Contains(q) ||
                                                        (!string.IsNullOrEmpty(c.anime) && c.anime.ToLowerInvariant().Contains(q))).ToList();
        Refresh();
    }

    void Refresh()
    {
        UIFactory.Clear(grid);
        foreach (var c in filtered.Skip(page * PageSize).Take(PageSize))
        {
            var card = c;
            var tile = CardTile.Build(grid, card.DisplayName, 145, 190, false);
            tile.Set(card, CardRarity.Common, card.element, "", "");
            tile.topLeft.text = "";
            tile.elementDot.gameObject.SetActive(false);
            tile.SetTalent(null);
            if (tile.frameFx != null && tile.frameFx.frame != null) tile.frameFx.frame.enabled = false;
            if (!string.IsNullOrEmpty(card.anime)) TooltipTrigger.Add(tile.frame, $"<b>{card.DisplayName}</b>\n{card.anime}");
            tile.button.onClick.AddListener(() => Choose(new PictureChoice { galleryCard = card, preview = card.cardImg }));
        }
        pageText.text = filtered.Count == 0 ? "No match" : $"Page {page + 1} / {MaxPage + 1}   ({filtered.Count} pictures)";
        prev.interactable = page > 0;
        next.interactable = page < MaxPage;
    }

    void Error(string text) => message.text = $"<color=#FF6B6B>{text}</color>";

    void ImportFromPc()
    {
        string path = CharacterPortrait.PickFile();
        if (string.IsNullOrEmpty(path)) { Error("No file chosen."); return; }
        UseFile(path);
    }

    void UseFile(string path)
    {
        string err = CharacterPortrait.CropFile(path, out byte[] png, out Sprite preview);
        if (err != null) { Error(err); return; }
        Choose(new PictureChoice { png = png, preview = preview });
    }

    void LoadPath()
    {
        string text = (pathInput.text ?? "").Trim().Trim('"');
        if (text.Length == 0) { Error("Paste a picture path or an image URL first."); return; }
        if (text.StartsWith("http://") || text.StartsWith("https://"))
        {
            message.text = "Downloading...";
            StartCoroutine(CharacterPortrait.DownloadAndCrop(text, (err, png, preview) =>
            {
                if (this == null) return;
                if (err != null) { Error(err); return; }
                Choose(new PictureChoice { png = png, preview = preview });
            }));
        }
        else UseFile(text);
    }
}
