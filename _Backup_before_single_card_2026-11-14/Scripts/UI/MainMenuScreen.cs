using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Title screen: play, collection, reset save, quit. Shows player info and the team.</summary>
public class MainMenuScreen : MonoBehaviour
{
    public Text titleText;
    public Text subtitleText;
    public Text profileText;
    public Text versionText;
    public Button playButton;
    public Button collectionButton;
    public Button resetButton;
    public Button quitButton;
    public RectTransform showcase;

    bool confirmReset;
    float confirmUntil;

    void Start()
    {
        playButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.LocationMap));
        collectionButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Collection));
        resetButton.onClick.AddListener(OnReset);
        quitButton.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        var db = CardDatabase.Instance;
        if (db == null || db.LocationCount == 0)
        {
            subtitleText.text = "<color=#FF6B6B>Card database missing - run Tools > AniGame > Build Everything</color>";
            playButton.interactable = false;
            collectionButton.interactable = false;
        }
        else
        {
            int anime = db.AnimeCount;
            subtitleText.text = $"Anime card battler  |  {db.LocationCount} locations  |  {db.CardCount:N0} cards" + (anime > 0 ? $"  |  {anime} anime" : "");
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
            float t = Time.unscaledTime;
            rt.anchoredPosition = basePos + new Vector2(0f, Mathf.Sin(t * 1.2f) * 6f);
            yield return null;
        }
    }

    void RefreshProfile()
    {
        var d = PlayerProfile.Data;
        int loc = PlayerProfile.HighestUnlockedLocation;
        profileText.text =
            $"<size=30><b>Player Lv {d.playerLevel}</b></size>   <color=#9AA4B5>XP {d.playerXp}/{PlayerProfile.PlayerXpToNext(d.playerLevel)}</color>\n" +
            UIFactory.CurrencyLine() + "\n" +
            $"<color=#9AA4B5>Cards owned</color> {d.cards.Count}    <color=#9AA4B5>Current location</color> LOC {loc}    " +
            $"<color=#9AA4B5>Wins</color> {d.battlesWon}  <color=#9AA4B5>Losses</color> {d.battlesLost}";
    }

    void BuildShowcase()
    {
        if (showcase == null) return;
        UIFactory.Clear(showcase);
        var team = PlayerProfile.GetTeam();
        for (int i = 0; i < team.Count; i++)
        {
            var card = PlayerProfile.Resolve(team[i]);
            var tile = CardTile.Build(showcase, $"Team{i}", 230, 320);
            var rt = (RectTransform)tile.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float offset = (i - (team.Count - 1) / 2f);
            rt.anchoredPosition = new Vector2(offset * 190f, -Mathf.Abs(offset) * 30f);
            rt.localRotation = Quaternion.Euler(0, 0, -offset * 9f);
            tile.Set(card, $"Lv {team[i].level}", card != null ? CardStats.RarityShort(card.rarity) : "");
            tile.button.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Collection));
            if (i == 0) tile.transform.SetAsLastSibling();
        }
        // Keep the middle card on top
        if (team.Count == 3) showcase.GetChild(1).SetAsLastSibling();
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
        UIFactory.CreateBackground(canvas, new Color(0.45f, 0.25f, 0.85f, 0.8f), UIFactory.BgDark, out _);

        var glowSprite = UIFactory.Theme != null ? UIFactory.Theme.glow : null;
        var glowA = UIFactory.CreateImage(canvas, "GlowA", new Color(1f, 0.3f, 0.55f, 0.35f), glowSprite);
        UIFactory.Place(glowA.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-520, 240), new Vector2(900, 900));
        var glowB = UIFactory.CreateImage(canvas, "GlowB", new Color(0.3f, 0.6f, 1f, 0.3f), glowSprite);
        UIFactory.Place(glowB.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(560, -160), new Vector2(1000, 1000));

        screen.titleText = UIFactory.CreateText(canvas, "Title", "ANIGAME", 150, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Place(screen.titleText.rectTransform, new Vector2(0f, 1f), new Vector2(110, -110), new Vector2(1000, 180));
        UIFactory.AddOutline(screen.titleText, UIFactory.Accent, 4f);
        UIFactory.AddShadow(screen.titleText, new Color(0.9f, 0.2f, 0.5f, 0.8f), 9f);

        screen.subtitleText = UIFactory.CreateText(canvas, "Subtitle", "Anime card battler", 30, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.Place(screen.subtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(118, -290), new Vector2(1100, 50));

        var menu = UIFactory.CreateRect("Menu", canvas);
        UIFactory.Place(menu, new Vector2(0f, 0.5f), new Vector2(110, -40), new Vector2(520, 420));
        screen.playButton = UIFactory.CreateButton(menu, "PlayButton", "PLAY", UIFactory.Accent, 40);
        UIFactory.TopStrip((RectTransform)screen.playButton.transform, 0, 100);
        screen.collectionButton = UIFactory.CreateButton(menu, "CollectionButton", "COLLECTION", UIFactory.Accent2, 34);
        UIFactory.TopStrip((RectTransform)screen.collectionButton.transform, 120, 86);
        screen.resetButton = UIFactory.CreateButton(menu, "ResetButton", "RESET SAVE", UIFactory.PanelLight, 26);
        UIFactory.TopStrip((RectTransform)screen.resetButton.transform, 226, 70);
        screen.quitButton = UIFactory.CreateButton(menu, "QuitButton", "QUIT", UIFactory.PanelLight, 26);
        UIFactory.TopStrip((RectTransform)screen.quitButton.transform, 316, 70);

        var profilePanel = UIFactory.CreatePanel(canvas, "ProfilePanel", new Color(0.04f, 0.05f, 0.1f, 0.8f), false);
        UIFactory.Place(profilePanel.rectTransform, new Vector2(0f, 0f), new Vector2(110, 60), new Vector2(1180, 150));
        screen.profileText = UIFactory.CreateText(profilePanel.transform, "Profile", "", 24, UIFactory.TextMain, TextAnchor.MiddleLeft);
        UIFactory.Stretch(screen.profileText.rectTransform, 28, 12, 28, 12);

        screen.showcase = UIFactory.CreateRect("TeamShowcase", canvas);
        UIFactory.Place(screen.showcase, new Vector2(1f, 0.5f), new Vector2(-180, 60), new Vector2(700, 520));
        var teamLabel = UIFactory.CreateText(canvas, "TeamLabel", "YOUR TEAM", 26, UIFactory.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(teamLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-355, 380), new Vector2(350, 40));

        screen.versionText = UIFactory.CreateText(canvas, "Version", "AniGame prototype  |  Unity 6", 20, UIFactory.TextDim, TextAnchor.LowerRight);
        UIFactory.Place(screen.versionText.rectTransform, new Vector2(1f, 0f), new Vector2(-24, 16), new Vector2(600, 30));
        return screen;
    }
}
