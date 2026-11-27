using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MINIGAMES: quick games with one shared cooldown (5 minutes by default, set in Test Mode): play one game, then wait
/// before the next. The score of a game pays its best prize line (Test Mode > MINIGAME PRIZES). Built at runtime.
/// </summary>
public class MinigamesScreen : MonoBehaviour
{
    struct Entry { public string id, desc, score; public Color color; }
    static readonly Entry[] Entries =
    {
        new Entry { id = MinigameConfig.Flappy, desc = "Fly your character through the pipes. Space, click or tap to flap.", score = "Score: pipes passed", color = new Color(0.25f, 0.6f, 0.95f) },
        new Entry { id = MinigameConfig.Quiz, desc = "5 quick questions about anime characters and AniGame itself.", score = "Score: right answers (of 5)", color = new Color(0.6f, 0.35f, 0.95f) },
        new Entry { id = MinigameConfig.Slots, desc = "One spin of 3 reels. Pairs pay, three 7s is the jackpot.", score = "1 = pair, 2 = three of a kind, 3 = 777", color = new Color(0.9f, 0.25f, 0.35f) },
        new Entry { id = MinigameConfig.Roulette, desc = "One spin of the prize wheel.", score = "Each segment is a prize", color = new Color(0.95f, 0.6f, 0.15f) },
        new Entry { id = MinigameConfig.HigherLower, desc = "Is the next character's POWER higher or lower?", score = "Score: right guesses in a row (max 10)", color = new Color(0.2f, 0.65f, 0.5f) },
        new Entry { id = MinigameConfig.Memory, desc = "Find the 6 pairs of characters in 45 seconds.", score = "Score: pairs found (of 6)", color = new Color(0.85f, 0.4f, 0.65f) },
    };

    RectTransform root;
    Text banner;
    readonly Dictionary<string, Button> playButtons = new Dictionary<string, Button>();
    GameObject overlay, resultPanel;
    RectTransform gameArea;
    Text overlayTitle, overlayHelp, resultTitle, resultBody;
    Button closeGame;
    MinigameBase game;

    public static void BuildLayout(RectTransform canvas) => canvas.gameObject.AddComponent<MinigamesScreen>();

    void Start()
    {
        root = (RectTransform)transform;
        MinigameConfig.Reload();
        SfxSynth.Volume = PlayerProfile.Data.sfxVolume;
        UIFactory.CreateBackground(root, new Color(0.12f, 0.35f, 0.5f, 0.9f), UIFactory.BgDark, out _);
        UIFactory.CreateTopBarWithCurrency(root, "MINIGAMES", out Button back, out _);
        back.onClick.AddListener(() => GameSession.LoadScene(SceneNames.MainMenu));

        banner = UIFactory.CreateText(root, "Banner", "", 30, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(banner.rectTransform, 110, 50, 40, 40);
        UIFactory.AddShadow(banner, UIFactory.ShadowColor, 2f);

        var cfg = MinigameConfig.Current;
        for (int i = 0; i < Entries.Length; i++)
        {
            var e = Entries[i];
            var def = cfg.Find(e.id);
            float x = 60 + (i % 3) * 610, y = 180 + (i / 3) * 440;
            var card = UIFactory.CreatePanel(root, "Game_" + e.id, new Color(0.05f, 0.06f, 0.12f, 0.92f), true);
            UIFactory.TopLeft(card.rectTransform, x, y, 590, 420);
            UIFactory.AddOutline(card, UIFactory.WithAlpha(e.color, 0.9f), 3f);
            var stripe = UIFactory.CreatePanel(card.transform, "Stripe", e.color, false);
            UIFactory.TopStrip(stripe.rectTransform, 0, 70);
            var title = UIFactory.CreateText(card.transform, "Title", def != null ? def.title.ToUpperInvariant() : e.id, 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.TopStrip(title.rectTransform, 6, 60, 10, 10);
            UIFactory.AddShadow(title, UIFactory.ShadowColor, 2f);
            var desc = UIFactory.CreateText(card.transform, "Desc", $"{e.desc}\n<size=17><color=#9AA4B5>{e.score}</color></size>", 21, UIFactory.TextMain, TextAnchor.UpperCenter);
            UIFactory.TopStrip(desc.rectTransform, 84, 90, 24, 24);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var prizes = UIFactory.CreateText(card.transform, "Prizes", PrizeSummary(def), 19, new Color(1f, 0.85f, 0.4f), TextAnchor.UpperCenter);
            UIFactory.TopStrip(prizes.rectTransform, 178, 140, 24, 24);
            prizes.horizontalOverflow = HorizontalWrapMode.Wrap;
            var play = UIFactory.CreateButton(card.transform, "Play", "PLAY", e.color, 30);
            UIFactory.Place((RectTransform)play.transform, new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(300, 72));
            string id = e.id;
            play.onClick.AddListener(() => Play(id));
            playButtons[id] = play;
        }
        BuildOverlay();
        StartCoroutine(Tick());
    }

    static string PrizeSummary(MinigameDef def)
    {
        if (def == null || def.tiers.Count == 0) return "No prizes set";
        if (!def.enabled) return "<color=#FF9E9E>Turned off in Test Mode</color>";
        if (def.id == MinigameConfig.Roulette)
            return $"{def.tiers.Count} prizes on the wheel:\n" + string.Join(", ", def.tiers.Take(8).Select(t => string.IsNullOrEmpty(t.label) ? t.prize.Label : t.label));
        return string.Join("\n", def.tiers.OrderBy(t => t.minScore).Take(5).Select(t => $"{(string.IsNullOrEmpty(t.label) ? $"{t.minScore}+" : t.label)}: {t.prize.Label}"));
    }

    IEnumerator Tick()
    {
        while (true)
        {
            RefreshCooldown();
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    void RefreshCooldown()
    {
        int left = PlayerProfile.MinigameCooldownLeft;
        int minutes = MinigameConfig.Current.cooldownMinutes;
        banner.text = left <= 0 ? $"READY! Pick a game. After it you wait {minutes} minute{(minutes == 1 ? "" : "s")} for the next one."
                                : $"Next game in <color=#FFFFFF>{PlayerProfile.CooldownText(left)}</color>";
        bool busy = overlay != null && overlay.activeSelf;
        var cfg = MinigameConfig.Current;
        foreach (var kv in playButtons)
        {
            var def = cfg.Find(kv.Key);
            bool on = def == null || def.enabled;
            kv.Value.interactable = left <= 0 && on && !busy;
            UIFactory.ButtonLabel(kv.Value).text = !on ? "OFF" : left <= 0 ? "PLAY" : PlayerProfile.CooldownText(left);
        }
    }

    void Play(string id)
    {
        if (!PlayerProfile.MinigameReady || (overlay != null && overlay.activeSelf)) { SfxSynth.Play("fail", 0.5f); return; }
        var def = MinigameConfig.Current.Find(id);
        if (def != null && !def.enabled) return;
        PlayerProfile.StartMinigameCooldown();
        SfxSynth.Play("click");
        OpenGame(id, def);
    }

    // ---------------------------------------------------------------- game overlay

    void BuildOverlay()
    {
        var dim = UIFactory.CreateImage(root, "GameOverlay", new Color(0, 0, 0, 0.85f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        overlay = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.06f, 0.07f, 0.14f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 1000));
        UIFactory.AddOutline(panel, new Color(0.45f, 0.75f, 1f, 0.8f), 3f);
        overlayTitle = UIFactory.CreateText(panel.transform, "Title", "", 40, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(overlayTitle.rectTransform, 30, 12, 900, 56);
        overlayHelp = UIFactory.CreateText(panel.transform, "Help", "", 20, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(overlayHelp.rectTransform, 30, 66, 1300, 34);
        closeGame = UIFactory.CreateButton(panel.transform, "Close", "CLOSE", new Color(0.6f, 0.25f, 0.35f), 22);
        UIFactory.Place((RectTransform)closeGame.transform, new Vector2(1f, 1f), new Vector2(-120, -46), new Vector2(200, 60));
        closeGame.onClick.AddListener(() => { if (game != null) game.Quit(); });
        TooltipTrigger.Add(closeGame.image, "Ends the game now with the score you have (the cooldown has already started).");
        gameArea = UIFactory.CreateRect("Area", panel.transform);
        UIFactory.Stretch(gameArea, 30, 110, 30, 30);

        var rdim = UIFactory.CreateImage(panel.transform, "Result", new Color(0, 0, 0, 0.75f), null, false, true);
        UIFactory.Stretch(rdim.rectTransform);
        resultPanel = rdim.gameObject;
        var rp = UIFactory.CreatePanel(rdim.transform, "Box", new Color(0.08f, 0.07f, 0.16f, 1f), true);
        UIFactory.Place(rp.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 480));
        UIFactory.AddOutline(rp, UIFactory.Gold, 3f);
        resultTitle = UIFactory.CreateText(rp.transform, "Title", "", 54, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(resultTitle.rectTransform, 24, 80, 20, 20);
        resultBody = UIFactory.CreateText(rp.transform, "Body", "", 28, UIFactory.TextMain, TextAnchor.UpperCenter);
        UIFactory.TopStrip(resultBody.rectTransform, 120, 230, 30, 30);
        resultBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        var ok = UIFactory.CreateButton(rp.transform, "Ok", "OK", UIFactory.Accent, 30);
        UIFactory.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(280, 76));
        ok.onClick.AddListener(CloseOverlay);
        resultPanel.SetActive(false);
        overlay.SetActive(false);
    }

    void OpenGame(string id, MinigameDef def)
    {
        UIFactory.Clear(gameArea);
        resultPanel.SetActive(false);
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        closeGame.interactable = true;
        RefreshCooldown();
        var host = UIFactory.CreateRect("Game", gameArea);
        UIFactory.Stretch(host);
        Canvas.ForceUpdateCanvases(); // the game reads the size of its area
        switch (id)
        {
            case MinigameConfig.Flappy: game = host.gameObject.AddComponent<FlappyGame>(); break;
            case MinigameConfig.Quiz: game = host.gameObject.AddComponent<QuizGame>(); break;
            case MinigameConfig.Slots: game = host.gameObject.AddComponent<SlotsGame>(); break;
            case MinigameConfig.Roulette: game = host.gameObject.AddComponent<RouletteGame>(); break;
            case MinigameConfig.HigherLower: game = host.gameObject.AddComponent<HigherLowerGame>(); break;
            default: game = host.gameObject.AddComponent<MemoryGame>(); break;
        }
        overlayTitle.text = def != null ? def.title.ToUpperInvariant() : id;
        overlayHelp.text = game.Help;
        game.Begin(this, host);
    }

    /// <summary>Called once by the game: pays the prize and shows the result.</summary>
    public void OnFinished(string id, int score)
    {
        closeGame.interactable = false;
        var cfg = MinigameConfig.Current;
        string head, body;
        if (id == MinigameConfig.Roulette)
        {
            var def = cfg.Find(id);
            if (score < 0 || def == null || score >= def.tiers.Count) { head = "NO SPIN"; body = "You closed the wheel without spinning."; }
            else
            {
                var tier = def.tiers[score];
                var given = PlayerProfile.GiveSegmentPrize(id, score);
                head = string.IsNullOrEmpty(tier.label) ? "PRIZE!" : tier.label.ToUpperInvariant() + "!";
                body = given != null && given.amount > 0 ? $"You won <color=#FFD54F>{given.Label}</color>" + PackLine(given) : "Nothing this time.";
            }
        }
        else
        {
            var (tier, given) = PlayerProfile.FinishMinigame(id, score);
            head = $"SCORE {score}";
            body = given != null && given.amount > 0
                ? $"{(string.IsNullOrEmpty(tier.label) ? "" : tier.label + ": ")}you won <color=#FFD54F>{given.Label}</color>" + PackLine(given)
                : "No prize this time.";
            var next = cfg.Find(id)?.tiers.Where(t => t.minScore > score).OrderBy(t => t.minScore).FirstOrDefault();
            if (next != null) body += $"\n<size=22><color=#9AA4B5>Reach {next.minScore} for {next.prize.Label}.</color></size>";
        }
        body += $"\n\n<size=22>Next game in {PlayerProfile.CooldownText(PlayerProfile.MinigameCooldownLeft)}</size>";
        resultTitle.text = head;
        resultBody.text = body;
        resultPanel.SetActive(true);
        resultPanel.transform.SetAsLastSibling();
        SfxSynth.Play(body.Contains("won") ? "buff" : "fail", 0.6f);
    }

    /// <summary>Dev bridge only (ui:MinigamesScreen.PreviewGame:id): opens a game without starting the cooldown.</summary>
    void PreviewGame(string id)
    {
        if (!PlayerProfile.TestModeAvailable) return;
        OpenGame(id, MinigameConfig.Current.Find(id));
    }

    /// <summary>A pack prize is opened at once: list what came out.</summary>
    static string PackLine(Prize given)
    {
        if (given == null || given.type != PrizeType.Pack || PlayerProfile.LastPackItems.Count == 0) return "";
        return "\n<size=20><color=#C9CEDA>Inside: " + PlayerProfile.DescribePackItems(PlayerProfile.LastPackItems) + "</color></size>";
    }

    void CloseOverlay()
    {
        if (game != null) Destroy(game.gameObject);
        game = null;
        overlay.SetActive(false);
        RefreshCooldown();
    }
}
