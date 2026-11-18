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
    public Button resetButton;
    public Button quitButton;
    public RectTransform showcase;

    bool confirmReset;
    float confirmUntil;

    void Start()
    {
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
            $"<size=28><b>Main card:</b></size> {main}\n" +
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
        BuildShowcase();
        RefreshProfile();
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
        UIFactory.TopStrip((RectTransform)screen.cardsButton.transform, 112, 80);
        screen.gachaButton = UIFactory.CreateButton(menu, "GachaButton", "GACHA", new Color(1f, 0.62f, 0.15f), 30);
        UIFactory.TopLeft((RectTransform)screen.gachaButton.transform, 0, 208, 255, 74);
        screen.shopButton = UIFactory.CreateButton(menu, "ShopButton", "SHOP", new Color(0.2f, 0.65f, 0.55f), 30);
        UIFactory.TopLeft((RectTransform)screen.shopButton.transform, 265, 208, 255, 74);
        screen.resetButton = UIFactory.CreateButton(menu, "ResetButton", "RESET SAVE", UIFactory.PanelLight, 24);
        UIFactory.TopLeft((RectTransform)screen.resetButton.transform, 0, 300, 255, 64);
        screen.quitButton = UIFactory.CreateButton(menu, "QuitButton", "QUIT", UIFactory.PanelLight, 24);
        UIFactory.TopLeft((RectTransform)screen.quitButton.transform, 265, 300, 255, 64);

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
        return screen;
    }
}
