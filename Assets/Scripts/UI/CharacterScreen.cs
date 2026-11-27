using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Create (first launch) or edit the player's character: a name and a picture, either imported from the PC
/// (or a path / URL) or picked from the built-in anime portraits. It is the only character the player collects.
/// </summary>
public class CharacterScreen : MonoBehaviour
{
    const int PageSize = 24;

    public Button backButton;
    public Text titleText;
    public Text subtitleText;
    public CardView preview;
    public InputField nameInput;
    public Button importButton;
    public Button galleryButton;
    public InputField pathInput;
    public Button loadPathButton;
    public Button saveButton;
    public Text messageText;

    [Header("Gallery")]
    public GameObject galleryPanel;
    public InputField searchInput;
    public RectTransform galleryGrid;
    public Button prevPageButton;
    public Button nextPageButton;
    public Text pageText;
    public Button galleryCloseButton;

    bool firstTime;
    string pendingSource;   // "file" or "gallery:KEY"
    Sprite pendingSprite;
    List<Card> galleryCards = new List<Card>();
    List<Card> filtered = new List<Card>();
    int page;

    void Start()
    {
        firstTime = !PlayerProfile.HasCharacter;
        backButton.gameObject.SetActive(!firstTime);
        backButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.MainMenu));
        titleText.text = firstTime ? "CREATE YOUR CHARACTER" : "YOUR CHARACTER";
        subtitleText.text = firstTime
            ? "Pick a name and a picture. This is the only character you will collect: every card you get is this hero, in a different rarity."
            : "Change your character's name or picture. Every card you own updates, except cards you gave their own name or picture (click a card's name in the collection).";
        UIFactory.ButtonLabel(saveButton).text = firstTime ? "START ADVENTURE" : "SAVE CHANGES";

        nameInput.characterLimit = 20;
        nameInput.text = PlayerProfile.HasCharacter ? PlayerProfile.Data.characterName : "";
        nameInput.onValueChanged.AddListener(_ => RefreshPreview());
        importButton.onClick.AddListener(ImportFromPc);
        importButton.interactable = CharacterPortrait.CanPickFiles;
        galleryButton.onClick.AddListener(OpenGallery);
        loadPathButton.onClick.AddListener(LoadPath);
        saveButton.onClick.AddListener(SaveCharacter);
        searchInput.onValueChanged.AddListener(_ => { page = 0; Filter(); });
        prevPageButton.onClick.AddListener(() => { page = Mathf.Max(0, page - 1); RefreshGallery(); });
        nextPageButton.onClick.AddListener(() => { page = Mathf.Min(MaxPage, page + 1); RefreshGallery(); });
        galleryCloseButton.onClick.AddListener(() => galleryPanel.SetActive(false));
        galleryPanel.SetActive(false);
        messageText.text = "";
        if (!string.IsNullOrEmpty(PlayerProfile.LoadWarning))
        {
            messageText.text = $"<color=#FF6B6B>{PlayerProfile.LoadWarning}</color>";
            PlayerProfile.LoadWarning = null;
        }

        var db = CardDatabase.Instance;
        if (db != null)
        {
            var seen = new HashSet<string>();
            foreach (var loc in db.locations)
                foreach (var c in loc.floors)
                {
                    if (c == null || c.cardImg == null) continue;
                    string id = string.IsNullOrEmpty(c.characterId) ? c.DisplayName : c.characterId;
                    if (seen.Add(id)) galleryCards.Add(c);
                }
            galleryCards = galleryCards.OrderBy(c => c.DisplayName).ToList();
        }
        RefreshPreview();
    }

    void OnDestroy()
    {
        PlayerProfile.PreviewName = null;
        PlayerProfile.PreviewSprite = null;
    }

    void Message(string text) => messageText.text = text;

    void RefreshPreview()
    {
        string name = nameInput.text.Trim();
        PlayerProfile.PreviewName = name.Length > 0 ? name : "Your hero";
        PlayerProfile.PreviewSprite = pendingSprite;
        var card = PlayerProfile.Data.cards.FirstOrDefault();
        if (card == null) return;
        var b = card.build.Clone();
        b.name = null;      // show the character itself, not a copy's own name / picture
        b.picture = null;
        preview.ShowInstance(card, b);
    }

    // ---------------------------------------------------------------- picture sources

    void ImportFromPc()
    {
        string path = CharacterPortrait.PickFile();
        if (string.IsNullOrEmpty(path)) { Message("No file chosen."); return; }
        UseFile(path);
    }

    void LoadPath()
    {
        string text = (pathInput.text ?? "").Trim().Trim('"');
        if (text.Length == 0) { Message("Paste a picture path (C:\\...\\hero.png) or an image URL first."); return; }
        if (text.StartsWith("http://") || text.StartsWith("https://"))
        {
            Message("Downloading...");
            StartCoroutine(CharacterPortrait.ImportUrl(text, (err, sprite) =>
            {
                if (err != null) { Message($"<color=#FF6B6B>{err}</color>"); return; }
                SetPending("file", sprite, "Picture downloaded.");
            }));
        }
        else UseFile(text);
    }

    void UseFile(string path)
    {
        string err = CharacterPortrait.ImportFile(path, out Sprite sprite);
        if (err != null) { Message($"<color=#FF6B6B>{err}</color>"); return; }
        SetPending("file", sprite, $"Picture imported: {System.IO.Path.GetFileName(path)}");
    }

    void SetPending(string source, Sprite sprite, string message)
    {
        pendingSource = source;
        pendingSprite = sprite;
        Message($"<color=#7CFC6A>{message}</color>");
        RefreshPreview();
    }

    // ---------------------------------------------------------------- gallery

    int MaxPage => Mathf.Max(0, (filtered.Count - 1) / PageSize);

    void OpenGallery()
    {
        galleryPanel.SetActive(true);
        page = 0;
        Filter();
    }

    void Filter()
    {
        string q = (searchInput.text ?? "").Trim().ToLowerInvariant();
        filtered = q.Length == 0 ? galleryCards : galleryCards.Where(c =>
            c.DisplayName.ToLowerInvariant().Contains(q) || (!string.IsNullOrEmpty(c.anime) && c.anime.ToLowerInvariant().Contains(q))).ToList();
        RefreshGallery();
    }

    void RefreshGallery()
    {
        UIFactory.Clear(galleryGrid);
        foreach (var c in filtered.Skip(page * PageSize).Take(PageSize))
        {
            var card = c;
            var tile = CardTile.Build(galleryGrid, card.DisplayName, 150, 210, false);
            tile.Set(card, CardRarity.Common, card.element, "", "");
            tile.topLeft.text = "";
            tile.elementDot.gameObject.SetActive(false);
            tile.SetTalent(null);
            if (tile.frameFx != null && tile.frameFx.frame != null) tile.frameFx.frame.enabled = false;
            tile.button.onClick.AddListener(() =>
            {
                SetPending("gallery:" + card.Key, card.cardImg, $"Picked {card.DisplayName}'s portrait.");
                if (nameInput.text.Trim().Length == 0) nameInput.text = card.DisplayName;
                galleryPanel.SetActive(false);
            });
        }
        pageText.text = filtered.Count == 0 ? "No match" : $"Page {page + 1} / {MaxPage + 1}   ({filtered.Count} portraits)";
        prevPageButton.interactable = page > 0;
        nextPageButton.interactable = page < MaxPage;
    }

    // ---------------------------------------------------------------- save

    void SaveCharacter()
    {
        string source = pendingSource ?? (PlayerProfile.HasCharacter ? PlayerProfile.Data.characterSource : null);
        if (string.IsNullOrEmpty(source)) { Message("<color=#FF6B6B>Choose a picture first: import one from your PC or pick one from the gallery.</color>"); return; }
        if (pendingSource == "file" && !CharacterPortrait.CommitPending()) { Message("<color=#FF6B6B>Could not save the picture. Try importing it again.</color>"); return; }
        string err = PlayerProfile.SetCharacter(nameInput.text, source);
        if (err != null) { Message($"<color=#FF6B6B>{err}.</color>"); return; }
        pendingSource = null;
        pendingSprite = null;
        SfxSynth.Play("win", 0.7f);
        UIFx.BurstAtPointer(UIFactory.Accent2, 1f);
        SfxSynth.Play("sparkle", 0.6f);
        if (firstTime) GameSession.LoadScene(SceneNames.MainMenu);
        else { Message("<color=#7CFC6A>Saved! Your cards now show the new name and picture (cards with their own name or picture keep it).</color>"); RefreshPreview(); }
    }

    // ---------------------------------------------------------------- layout

    public static CharacterScreen BuildLayout(RectTransform canvas)
    {
        var screen = canvas.gameObject.AddComponent<CharacterScreen>();
        AnimeBackground.Build(canvas, "menu", new Color(1f, 0.55f, 0.8f, 0.7f), new Color(0.12f, 0.05f, 0.2f), out _);
        UIFactory.CreateTopBar(canvas, "YOUR CHARACTER", out screen.backButton, out screen.titleText, out Text info);
        info.gameObject.SetActive(false);

        screen.preview = CardView.Build(canvas, "Preview");
        var pv = (RectTransform)screen.preview.transform;
        UIFactory.Place(pv, new Vector2(0f, 0.5f), new Vector2(150, -40), new Vector2(CardView.Width, CardView.Height));
        pv.localScale = Vector3.one * 1.15f;

        var panel = UIFactory.CreatePanel(canvas, "Form", new Color(0.06f, 0.05f, 0.12f, 0.95f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-90, -40), new Vector2(980, 820));

        screen.subtitleText = UIFactory.CreateText(panel.transform, "Subtitle", "", 24, UIFactory.TextDim, TextAnchor.UpperLeft);
        UIFactory.TopStrip(screen.subtitleText.rectTransform, 30, 70, 40, 40);

        var nameLabel = UIFactory.CreateText(panel.transform, "NameLabel", "NAME", 26, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopStrip(nameLabel.rectTransform, 120, 36, 40, 40);
        screen.nameInput = UIFactory.CreateInputField(panel.transform, "NameInput", "Type your hero's name (max 20)", 30);
        UIFactory.TopStrip((RectTransform)screen.nameInput.transform, 160, 72, 40, 40);

        var picLabel = UIFactory.CreateText(panel.transform, "PictureLabel", "PICTURE", 26, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopStrip(picLabel.rectTransform, 270, 36, 40, 40);
        screen.importButton = UIFactory.CreateButton(panel.transform, "Import", "IMPORT FROM PC", UIFactory.Accent, 26);
        UIFactory.TopLeft((RectTransform)screen.importButton.transform, 40, 312, 440, 80);
        screen.galleryButton = UIFactory.CreateButton(panel.transform, "Gallery", "ANIME GALLERY", UIFactory.Accent2, 26);
        UIFactory.TopLeft((RectTransform)screen.galleryButton.transform, 500, 312, 440, 80);

        var pathLabel = UIFactory.CreateText(panel.transform, "PathLabel", "or paste a picture path / image URL", 20, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.TopStrip(pathLabel.rectTransform, 412, 30, 40, 40);
        screen.pathInput = UIFactory.CreateInputField(panel.transform, "PathInput", "C:\\Pictures\\hero.png  or  https://...", 22);
        UIFactory.TopLeft((RectTransform)screen.pathInput.transform, 40, 446, 720, 64);
        screen.loadPathButton = UIFactory.CreateButton(panel.transform, "LoadPath", "LOAD", UIFactory.PanelLight, 24);
        UIFactory.TopLeft((RectTransform)screen.loadPathButton.transform, 780, 446, 160, 64);

        var tip = UIFactory.CreateText(panel.transform, "Tip", "PNG or JPG. The picture is cropped to the card shape automatically.", 19, UIFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
        UIFactory.TopStrip(tip.rectTransform, 520, 30, 40, 40);

        screen.messageText = UIFactory.CreateText(panel.transform, "Message", "", 22, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(screen.messageText.rectTransform, 566, 60, 40, 40);

        screen.saveButton = UIFactory.CreateButton(panel.transform, "Save", "START ADVENTURE", new Color(0.95f, 0.35f, 0.6f), 34);
        UIFactory.Place((RectTransform)screen.saveButton.transform, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(600, 100));

        BuildGallery(canvas, screen);
        return screen;
    }

    static void BuildGallery(RectTransform canvas, CharacterScreen screen)
    {
        var dim = UIFactory.CreateImage(canvas, "Gallery", new Color(0, 0, 0, 0.85f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        screen.galleryPanel = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.08f, 0.06f, 0.15f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640, 960));
        var title = UIFactory.CreateText(panel.transform, "Title", "ANIME GALLERY", 40, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, 40, 20, 500, 60);
        screen.searchInput = UIFactory.CreateInputField(panel.transform, "Search", "Search a character or anime...", 26);
        UIFactory.TopLeft((RectTransform)screen.searchInput.transform, 560, 22, 760, 60);
        screen.galleryCloseButton = UIFactory.CreateButton(panel.transform, "Close", "CLOSE", UIFactory.PanelLight, 24);
        UIFactory.TopLeft((RectTransform)screen.galleryCloseButton.transform, 1400, 22, 200, 60);

        screen.galleryGrid = UIFactory.CreateRect("Grid", panel.transform);
        UIFactory.Stretch(screen.galleryGrid, 40, 100, 40, 110);
        var grid = screen.galleryGrid.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150, 210);
        grid.spacing = new Vector2(46, 22);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.childAlignment = TextAnchor.UpperCenter;

        screen.prevPageButton = UIFactory.CreateButton(panel.transform, "Prev", "< PREV", UIFactory.PanelLight, 24);
        UIFactory.Place((RectTransform)screen.prevPageButton.transform, new Vector2(0.5f, 0f), new Vector2(-420, 24), new Vector2(200, 64));
        screen.pageText = UIFactory.CreateText(panel.transform, "Page", "", 22, UIFactory.TextDim);
        UIFactory.Place(screen.pageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(560, 64));
        screen.nextPageButton = UIFactory.CreateButton(panel.transform, "Next", "NEXT >", UIFactory.PanelLight, 24);
        UIFactory.Place((RectTransform)screen.nextPageButton.transform, new Vector2(0.5f, 0f), new Vector2(420, 24), new Vector2(200, 64));
        dim.gameObject.SetActive(false);
    }
}
