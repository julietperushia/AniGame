using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Title screen: Play, My Cards, Gacha, Shop, Reset, Quit. Shows the build slots and resources.</summary>
public class MainMenuScreen : MonoBehaviour
{
    public Text titleText;
    public Text subtitleText;
    public Text profileText;
    public Text versionText;
    public Image logo;
    public Button playButton;
    public Button cardsButton;
    public Button gachaButton;
    public Button shopButton;
    public Button buffsButton;
    public Button libraryButton;
    public Button characterButton;
    public Button saveDataButton;
    public Button resetButton;
    public Button quitButton;
    public RectTransform showcase;

    [Header("Save data (export / import)")]
    public GameObject savePanel;
    public Text saveInfoText;
    public Button exportButton;
    public Button importButton;
    public Button openFolderButton;
    public InputField importPathInput;
    public Button importPathButton;
    public Button saveCloseButton;
    public Text saveMessageText;
    string pendingImport;
    float pendingImportUntil;

    bool confirmReset;
    float confirmUntil;

    void Start()
    {
        // First launch: create the character before anything else
        if (!PlayerProfile.HasCharacter)
        {
            GameSession.LoadScene(SceneNames.Character);
            return;
        }
        if (buffsButton != null) buffsButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Buffs));
        if (libraryButton != null) libraryButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Library));
        if (characterButton != null) characterButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Character));
        SetupSavePanel();
        playButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.LocationMap));
        cardsButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Collection));
        gachaButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Gacha));
        shopButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Shop));
        resetButton.onClick.AddListener(OnReset);
        quitButton.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        var theme = UITheme.Instance;
        if (logo != null && theme != null && theme.logo != null)
        {
            logo.sprite = theme.logo;
            logo.enabled = true;
            titleText.enabled = false;
        }

        var db = CardDatabase.Instance;
        if (db == null || db.LocationCount == 0)
        {
            subtitleText.text = "<color=#FF6B6B>Card database missing - run Tools > AniGame > Build Everything</color>";
            playButton.interactable = false;
        }
        else
        {
            subtitleText.text = $"One card. Endless builds.  |  {db.LocationCount} locations  |  {db.AnimeCount} anime  |  {db.talents.Count} talents";
        }

        SfxSynth.Volume = PlayerProfile.Data.sfxVolume;
        BuildShowcase();
        StartCoroutine(RefreshLoop());
        StartCoroutine(TitleAnimation());
    }

    IEnumerator RefreshLoop()
    {
        while (true)
        {
            RefreshProfile();
            if (pendingImport != null && Time.unscaledTime > pendingImportUntil) CancelImport();
            if (confirmReset && Time.unscaledTime > confirmUntil)
            {
                confirmReset = false;
                UIFactory.ButtonLabel(resetButton).text = "RESET SAVE";
            }
            yield return new WaitForSecondsRealtime(1f);
        }
    }

    IEnumerator TitleAnimation()
    {
        var rt = titleText.rectTransform;
        Vector2 basePos = rt.anchoredPosition;
        while (true)
        {
            rt.anchoredPosition = basePos + new Vector2(0f, Mathf.Sin(Time.unscaledTime * 1.2f) * 6f);
            yield return null;
        }
    }

    void RefreshProfile()
    {
        var d = PlayerProfile.Data;
        int loc = PlayerProfile.HighestUnlockedLocation;
        var best = PlayerProfile.SlotRuntime(PlayerProfile.ActiveSlot);
        string main = best != null ? $"{best.DisplayName} <color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(best.rarity))}>{CardStats.RarityName(best.rarity)}</color>" : "none";
        profileText.text =
            $"<size=28><b>{PlayerProfile.CharacterName}</b></size>   <color=#9AA4B5>main build:</color> {main}\n" +
            UIFactory.CurrencyLine() + "\n" +
            $"<color=#9AA4B5>Cards owned</color> {d.cards.Count}    <color=#9AA4B5>Current location</color> LOC {loc}    " +
            $"<color=#9AA4B5>Packs opened</color> {d.packsOpened}    <color=#9AA4B5>Fusions</color> {d.fusionsSucceeded} ok / {d.fusionsFailed} failed";
    }

    void BuildShowcase()
    {
        if (showcase == null) return;
        UIFactory.Clear(showcase);
        int shown = 0;
        for (int i = 0; i < GameRules.SlotCount; i++)
        {
            var slot = PlayerProfile.GetSlot(i);
            if (slot == null || !slot.unlocked || !slot.HasCard) continue;
            var card = PlayerProfile.FindCard(slot.cardUid);
            if (card == null) continue;
            var tile = CardTile.Build(showcase, $"Slot{i}", 230, 320);
            var rt = (RectTransform)tile.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            tile.SetInstance(card, slot.build);
            tile.SetBadge(i == PlayerProfile.ActiveSlot ? $"SLOT {i + 1}  MAIN" : $"SLOT {i + 1}", i == PlayerProfile.ActiveSlot ? UIFactory.Accent2 : UIFactory.Accent);
            tile.button.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Collection));
            shown++;
        }
        for (int i = 0; i < showcase.childCount; i++)
        {
            var rt = (RectTransform)showcase.GetChild(i);
            float offset = i - (shown - 1) / 2f;
            rt.anchoredPosition = new Vector2(offset * 190f, -Mathf.Abs(offset) * 30f);
            rt.localRotation = Quaternion.Euler(0, 0, -offset * 9f);
        }
        if (shown == 3) showcase.GetChild(1).SetAsLastSibling();
    }

    // ---------------------------------------------------------------- save data: export / import (move to another PC)

    void SetupSavePanel()
    {
        if (savePanel == null) return;
        savePanel.SetActive(false);
        saveDataButton.onClick.AddListener(() => { savePanel.SetActive(true); saveMessageText.text = ""; CancelImport(); RefreshSaveInfo(); });
        saveCloseButton.onClick.AddListener(() => savePanel.SetActive(false));
        exportButton.onClick.AddListener(ExportSave);
        importButton.onClick.AddListener(() =>
        {
            if (pendingImport != null && Time.unscaledTime < pendingImportUntil) { DoImport(); return; }
            if (!FileDialogs.Supported) { saveMessageText.text = "Paste the path of a ." + SaveTransfer.Extension + " file below."; return; }
            string path = FileDialogs.OpenFile("Import an AniGame save", "AniGame save", SaveTransfer.Extension);
            if (string.IsNullOrEmpty(path)) { saveMessageText.text = "Import cancelled."; return; }
            AskImport(path);
        });
        importPathButton.onClick.AddListener(() => AskImport(importPathInput.text));
        openFolderButton.onClick.AddListener(SaveTransfer.OpenSaveFolder);
    }

    void RefreshSaveInfo()
    {
        var d = PlayerProfile.Data;
        saveInfoText.text =
            $"<b>{PlayerProfile.CharacterName}</b>  |  {d.cards.Count} cards  |  LOC {PlayerProfile.HighestUnlockedLocation}  |  {d.buffs.Count} buffs  |  {d.battlesWon} wins\n" +
            "<color=#9AA4B5>Export writes ONE file with everything: cards and builds, slots, buffs, gold, diamonds, orbs, stamina, LOC progress, stats, settings, " +
            "your character's name and picture. Copy it to the other PC and import it there to continue exactly where you stopped.</color>\n" +
            $"<size=17><color=#6F7890>Save folder: {Application.persistentDataPath}</color></size>";
    }

    void ExportSave()
    {
        string path = null;
        if (FileDialogs.Supported)
        {
            path = FileDialogs.SaveFile("Export your AniGame save", "AniGame save", SaveTransfer.Extension, SaveTransfer.DefaultFileName);
            if (string.IsNullOrEmpty(path)) { saveMessageText.text = "Export cancelled."; return; }
        }
        string err = SaveTransfer.Export(path, out string written);
        if (err != null) { saveMessageText.text = $"<color=#FF6B6B>{err}</color>"; return; }
        SfxSynth.Play("buff", 0.8f);
        saveMessageText.text = $"<color=#7CFC6A>Saved to:</color> {written}";
    }

    void AskImport(string path)
    {
        string err = SaveTransfer.Peek(path, out var info);
        if (err != null) { saveMessageText.text = $"<color=#FF6B6B>{err}.</color>"; CancelImport(); return; }
        pendingImport = path.Trim().Trim('"');
        pendingImportUntil = Time.unscaledTime + 10f;
        UIFactory.ButtonLabel(importButton).text = "CONFIRM IMPORT";
        importButton.image.color = UIFactory.Accent2;
        saveMessageText.text = SaveTransfer.Describe(info) +
            "\n<color=#FFD54F>This replaces your current progress (a backup is kept). Click CONFIRM IMPORT within 10 seconds.</color>";
    }

    void CancelImport()
    {
        pendingImport = null;
        UIFactory.ButtonLabel(importButton).text = "IMPORT SAVE FILE";
        importButton.image.color = new Color(0.22f, 0.5f, 0.85f);
    }

    void DoImport()
    {
        string path = pendingImport;
        CancelImport();
        string err = SaveTransfer.Import(path, out string warning);
        if (err != null) { saveMessageText.text = $"<color=#FF6B6B>{err}.</color>"; return; }
        SfxSynth.Play("win", 0.8f);
        GameSession.LoadScene(SceneNames.MainMenu); // reload everything with the imported save
        if (warning != null) Debug.LogWarning("[AniGame] " + warning);
    }

    void OnReset()
    {
        var label = UIFactory.ButtonLabel(resetButton);
        if (!confirmReset)
        {
            confirmReset = true;
            confirmUntil = Time.unscaledTime + 3f;
            label.text = "CLICK AGAIN TO CONFIRM";
            return;
        }
        confirmReset = false;
        label.text = "RESET SAVE";
        PlayerProfile.DeleteSave();
        PlayerProfile.Load();
        GameSession.LoadScene(SceneNames.Character);
    }

    // ---------------------------------------------------------------- layout

    public static MainMenuScreen BuildLayout(RectTransform canvas)
    {
        var screen = canvas.gameObject.AddComponent<MainMenuScreen>();
        AnimeBackground.Build(canvas, "menu", new Color(1f, 0.45f, 0.75f, 0.75f), new Color(0.12f, 0.08f, 0.28f), out _);

        var glowSprite = UIFactory.Theme != null ? UIFactory.Theme.glow : null;
        var glowA = UIFactory.CreateImage(canvas, "GlowA", new Color(1f, 0.35f, 0.6f, 0.35f), glowSprite);
        UIFactory.Place(glowA.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-520, 240), new Vector2(900, 900));
        var glowB = UIFactory.CreateImage(canvas, "GlowB", new Color(0.35f, 0.65f, 1f, 0.3f), glowSprite);
        UIFactory.Place(glowB.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(560, -160), new Vector2(1000, 1000));

        screen.logo = UIFactory.CreateImage(canvas, "Logo", Color.white);
        screen.logo.preserveAspect = true;
        UIFactory.Place(screen.logo.rectTransform, new Vector2(0f, 1f), new Vector2(100, -60), new Vector2(900, 260));
        screen.logo.enabled = false;

        screen.titleText = UIFactory.CreateText(canvas, "Title", "ANIGAME", 150, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Place(screen.titleText.rectTransform, new Vector2(0f, 1f), new Vector2(110, -100), new Vector2(1000, 180));
        UIFactory.AddOutline(screen.titleText, new Color(1f, 0.35f, 0.65f), 4f);
        UIFactory.AddShadow(screen.titleText, new Color(0.35f, 0.2f, 0.9f, 0.85f), 9f);

        screen.subtitleText = UIFactory.CreateText(canvas, "Subtitle", "", 28, new Color(1f, 0.92f, 0.97f), TextAnchor.MiddleLeft);
        UIFactory.Place(screen.subtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(118, -280), new Vector2(1200, 50));
        UIFactory.AddShadow(screen.subtitleText, UIFactory.ShadowColor, 2f);

        var menu = UIFactory.CreateRect("Menu", canvas);
        UIFactory.Place(menu, new Vector2(0f, 0.5f), new Vector2(110, -60), new Vector2(520, 520));
        screen.playButton = UIFactory.CreateButton(menu, "PlayButton", "PLAY", new Color(1f, 0.35f, 0.62f), 40);
        UIFactory.TopStrip((RectTransform)screen.playButton.transform, 0, 96);
        screen.cardsButton = UIFactory.CreateButton(menu, "CardsButton", "MY CARDS", UIFactory.Accent, 32);
        UIFactory.TopStrip((RectTransform)screen.cardsButton.transform, 108, 76);
        screen.gachaButton = UIFactory.CreateButton(menu, "GachaButton", "GACHA", new Color(1f, 0.62f, 0.15f), 28);
        UIFactory.TopLeft((RectTransform)screen.gachaButton.transform, 0, 196, 255, 70);
        screen.shopButton = UIFactory.CreateButton(menu, "ShopButton", "SHOP", new Color(0.2f, 0.65f, 0.55f), 28);
        UIFactory.TopLeft((RectTransform)screen.shopButton.transform, 265, 196, 255, 70);
        screen.buffsButton = UIFactory.CreateButton(menu, "BuffsButton", "BUFFS", new Color(0.55f, 0.35f, 0.95f), 28);
        UIFactory.TopLeft((RectTransform)screen.buffsButton.transform, 0, 278, 255, 70);
        screen.libraryButton = UIFactory.CreateButton(menu, "LibraryButton", "LIBRARY", new Color(0.22f, 0.5f, 0.85f), 28);
        UIFactory.TopLeft((RectTransform)screen.libraryButton.transform, 265, 278, 255, 70);
        screen.characterButton = UIFactory.CreateButton(menu, "CharacterButton", "CHARACTER", UIFactory.PanelLight, 21);
        UIFactory.TopLeft((RectTransform)screen.characterButton.transform, 0, 360, 168, 60);
        screen.resetButton = UIFactory.CreateButton(menu, "ResetButton", "RESET SAVE", UIFactory.PanelLight, 21);
        UIFactory.TopLeft((RectTransform)screen.resetButton.transform, 176, 360, 168, 60);
        screen.quitButton = UIFactory.CreateButton(menu, "QuitButton", "QUIT", UIFactory.PanelLight, 21);
        UIFactory.TopLeft((RectTransform)screen.quitButton.transform, 352, 360, 168, 60);
        screen.saveDataButton = UIFactory.CreateButton(menu, "SaveDataButton", "SAVE DATA  (EXPORT / IMPORT)", new Color(0.2f, 0.45f, 0.4f), 21);
        UIFactory.TopLeft((RectTransform)screen.saveDataButton.transform, 0, 430, 520, 56);

        var profilePanel = UIFactory.CreatePanel(canvas, "ProfilePanel", new Color(0.05f, 0.04f, 0.12f, 0.82f), false);
        UIFactory.Place(profilePanel.rectTransform, new Vector2(0f, 0f), new Vector2(110, 50), new Vector2(1240, 150));
        screen.profileText = UIFactory.CreateText(profilePanel.transform, "Profile", "", 22, UIFactory.TextMain, TextAnchor.MiddleLeft);
        UIFactory.Stretch(screen.profileText.rectTransform, 28, 12, 28, 12);

        screen.showcase = UIFactory.CreateRect("SlotShowcase", canvas);
        UIFactory.Place(screen.showcase, new Vector2(1f, 0.5f), new Vector2(-180, 60), new Vector2(700, 520));
        var teamLabel = UIFactory.CreateText(canvas, "SlotsLabel", "YOUR BUILDS", 26, new Color(1f, 0.9f, 0.96f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(teamLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-355, 380), new Vector2(350, 40));
        UIFactory.AddShadow(teamLabel, UIFactory.ShadowColor, 2f);

        screen.versionText = UIFactory.CreateText(canvas, "Version", "AniGame  |  single-card system  |  Unity 6", 20, UIFactory.TextDim, TextAnchor.LowerRight);
        UIFactory.Place(screen.versionText.rectTransform, new Vector2(1f, 0f), new Vector2(-24, 16), new Vector2(700, 30));
        BuildSavePanel(canvas, screen);
        return screen;
    }

    static void BuildSavePanel(RectTransform canvas, MainMenuScreen screen)
    {
        var dim = UIFactory.CreateImage(canvas, "SavePanel", new Color(0f, 0f, 0f, 0.82f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        screen.savePanel = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.07f, 0.06f, 0.14f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240, 760));
        var title = UIFactory.CreateText(panel.transform, "Title", "SAVE DATA", 46, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopStrip(title.rectTransform, 24, 64, 44, 44);
        UIFactory.AddShadow(title, UIFactory.ShadowColor, 3f);
        screen.saveInfoText = UIFactory.CreateText(panel.transform, "Info", "", 22, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopStrip(screen.saveInfoText.rectTransform, 100, 170, 44, 44);
        UIFactory.BestFit(screen.saveInfoText, 13);

        screen.exportButton = UIFactory.CreateButton(panel.transform, "Export", "EXPORT SAVE FILE", new Color(0.25f, 0.65f, 0.4f), 28);
        UIFactory.TopLeft((RectTransform)screen.exportButton.transform, 44, 290, 560, 90);
        screen.importButton = UIFactory.CreateButton(panel.transform, "Import", "IMPORT SAVE FILE", new Color(0.22f, 0.5f, 0.85f), 28);
        UIFactory.TopLeft((RectTransform)screen.importButton.transform, 636, 290, 560, 90);

        var pathLabel = UIFactory.CreateText(panel.transform, "PathLabel", "or paste the path of a ." + SaveTransfer.Extension + " file to import:", 20, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.TopStrip(pathLabel.rectTransform, 400, 30, 44, 44);
        screen.importPathInput = UIFactory.CreateInputField(panel.transform, "ImportPath", "D:\\Backups\\AniGame_Hero.anisave", 22);
        UIFactory.TopLeft((RectTransform)screen.importPathInput.transform, 44, 436, 940, 64);
        screen.importPathButton = UIFactory.CreateButton(panel.transform, "ImportPathButton", "CHECK FILE", UIFactory.PanelLight, 22);
        UIFactory.TopLeft((RectTransform)screen.importPathButton.transform, 1000, 436, 196, 64);

        screen.saveMessageText = UIFactory.CreateText(panel.transform, "Message", "", 21, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopStrip(screen.saveMessageText.rectTransform, 520, 110, 44, 44);
        UIFactory.BestFit(screen.saveMessageText, 12);

        screen.openFolderButton = UIFactory.CreateButton(panel.transform, "OpenFolder", "OPEN SAVE FOLDER", UIFactory.PanelLight, 22);
        UIFactory.Place((RectTransform)screen.openFolderButton.transform, new Vector2(0f, 0f), new Vector2(44, 34), new Vector2(330, 70));
        screen.saveCloseButton = UIFactory.CreateButton(panel.transform, "Close", "CLOSE", UIFactory.Accent, 24);
        UIFactory.Place((RectTransform)screen.saveCloseButton.transform, new Vector2(1f, 0f), new Vector2(-44, 34), new Vector2(240, 70));
        dim.gameObject.SetActive(false);
    }
}
