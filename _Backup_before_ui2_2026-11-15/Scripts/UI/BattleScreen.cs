using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated battle scene. Runs BattleEngine turn by turn and replays its events:
/// lunges, shakes, damage numbers, talent banners, bars and a battle log.
/// Team cards fight one after another (relay) against the floor's enemy.
/// </summary>
public class BattleScreen : MonoBehaviour
{
    [Header("Views")]
    public CardView playerView;
    public CardView enemyView;
    public RectTransform popupLayer;
    public Image backgroundGradient;

    [Header("Top bar")]
    public Text titleText;
    public Text subtitleText;
    public Text roundText;
    public Button backButton;
    public Button speedButton;
    public Button skipButton;

    [Header("Center")]
    public Text matchupText;
    public Image playerElementIcon;
    public Image enemyElementIcon;
    public Text vsText;
    public CanvasGroup bannerGroup;
    public Text bannerText;
    public Image bannerBackground;
    public Text logText;
    public RectTransform reserveRow;

    [Header("Result")]
    public GameObject resultPanel;
    public Text resultTitle;
    public Text resultBody;
    public Image resultCardArt;
    public Text resultCardLabel;
    public Button nextButton;
    public Button retryButton;
    public Button mapButton;
    public Text resultHint;

    [Header("Timing")]
    public float baseDelay = 1f;

    int location;
    int floor;
    int slotIndex;
    Card enemyCard;
    readonly List<string> logLines = new List<string>();
    readonly int[] speeds = { 1, 2, 4 };
    int speedIndex;
    bool skipping;
    bool battleFinished;
    Coroutine bannerRoutine;

    float Speed => speeds[Mathf.Clamp(speedIndex, 0, speeds.Length - 1)];

    void Start()
    {
        resultPanel.SetActive(false);
        bannerGroup.alpha = 0f;
        logText.text = "";

        backButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.LocationMap));
        speedButton.onClick.AddListener(CycleSpeed);
        skipButton.onClick.AddListener(() => skipping = true);
        mapButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.LocationMap));

        var db = CardDatabase.Instance;
        if (db == null || db.LocationCount == 0)
        {
            ShowFatal("Card database not found.\nRun Tools > AniGame > Build Everything in the Unity editor.");
            return;
        }

        var data = PlayerProfile.Data;
        SfxSynth.Volume = data.sfxVolume;
        speedIndex = Mathf.Max(0, System.Array.IndexOf(speeds, data.battleSpeed));
        UpdateSpeedLabel();

        if (GameSession.HasPendingBattle)
        {
            location = GameSession.PendingLocation;
            floor = GameSession.PendingFloor;
            GameSession.HasPendingBattle = false;
        }
        else
        {
            // Scene opened directly in the editor: fight the next uncleared floor for free.
            location = PlayerProfile.HighestUnlockedLocation;
            floor = PlayerProfile.NextFloor(location);
        }

        enemyCard = db.GetFloorCard(location, floor);
        slotIndex = PlayerProfile.ActiveSlot;
        var fighter = PlayerProfile.SlotRuntime(slotIndex);
        if (enemyCard == null)
        {
            ShowFatal($"LOC {location} floor {floor} does not exist.");
            return;
        }
        if (fighter == null)
        {
            ShowFatal("No card in your build slot. Open My Cards and save a card to a slot.");
            return;
        }

        int floors = db.FloorCount(location);
        titleText.text = $"LOC {location}  -  FLOOR {floor}/{floors}";
        string anime = string.IsNullOrEmpty(enemyCard.anime) ? "" : $"{enemyCard.anime}  |  ";
        subtitleText.text = $"{anime}{enemyCard.DisplayName}  |  {CardStats.RarityName(enemyCard.rarity)}  |  Power {CardStats.EnemyPower(enemyCard)}  |  You: slot {slotIndex + 1}";
        if (backgroundGradient != null)
            backgroundGradient.color = UIFactory.WithAlpha(Color.Lerp(ElementSystem.GetColor(enemyCard.element), Color.black, 0.35f), 0.75f);

        StartCoroutine(RunBattle(fighter));
    }

    void ShowFatal(string message)
    {
        battleFinished = true;
        resultPanel.SetActive(true);
        resultTitle.text = "CAN'T START";
        resultTitle.color = UIFactory.Bad;
        resultBody.text = message;
        resultCardArt.gameObject.SetActive(false);
        resultCardLabel.text = "";
        nextButton.gameObject.SetActive(false);
        retryButton.gameObject.SetActive(false);
        resultHint.text = "";
    }

    void CycleSpeed()
    {
        speedIndex = (speedIndex + 1) % speeds.Length;
        PlayerProfile.Data.battleSpeed = speeds[speedIndex];
        PlayerProfile.Save();
        UpdateSpeedLabel();
    }

    void UpdateSpeedLabel()
    {
        var label = UIFactory.ButtonLabel(speedButton);
        if (label != null) label.text = $"SPEED x{Speed}";
        DamagePopup.SpeedMultiplier = Speed;
    }

    // ---------------------------------------------------------------- battle flow

    IEnumerator RunBattle(CardRuntime player)
    {
        var enemy = new CardRuntime(enemyCard);
        enemyView.Bind(enemy);
        playerView.Bind(player);
        UpdateMatchup(player, enemy);
        if (reserveRow != null) reserveRow.gameObject.SetActive(false);

        var engine = new BattleEngine(player, enemy, System.Environment.TickCount);
        engine.Begin();

        int processed = 0;
        int guard = 0;
        while (guard++ < 5000)
        {
            while (processed < engine.Events.Count)
            {
                var e = engine.Events[processed++];
                if (skipping) PlayInstant(e);
                else yield return PlayEvent(e);
            }
            if (engine.IsOver) break;
            engine.Step();
        }

        battleFinished = true;
        var state = engine.State;
        bool victory = state == BattleState.PlayerWon || (state == BattleState.Draw && !enemy.IsAlive);
        ShowResult(victory, state == BattleState.Draw && enemy.IsAlive);
    }

    void UpdateMatchup(CardRuntime player, CardRuntime enemy)
    {
        float give = ElementSystem.GetElementMultiplier(player.Element, enemy.Element);
        float take = ElementSystem.GetElementMultiplier(enemy.Element, player.Element);
        string pc = ColorUtility.ToHtmlStringRGB(ElementSystem.GetColor(player.Element));
        string ec = ColorUtility.ToHtmlStringRGB(ElementSystem.GetColor(enemy.Element));
        var theme = UITheme.Instance;
        SetElementIcon(playerElementIcon, player.Element, theme);
        SetElementIcon(enemyElementIcon, enemy.Element, theme);
        bool icons = playerElementIcon != null && playerElementIcon.enabled && enemyElementIcon != null && enemyElementIcon.enabled;
        string header = icons ? "" :
            $"<color=#{pc}>{ElementSystem.DisplayName(player.Element).ToUpperInvariant()}</color>  vs  <color=#{ec}>{ElementSystem.DisplayName(enemy.Element).ToUpperInvariant()}</color>\n";
        matchupText.text = header +
            $"<size=22>You deal <color={MulColor(give)}>{ElementSystem.Describe(give)}</color>   You take <color={MulColor(take, true)}>{ElementSystem.Describe(take)}</color></size>";
    }

    static void SetElementIcon(Image img, CardElement element, UITheme theme)
    {
        if (img == null) return;
        var sprite = theme != null ? theme.ElementIcon(element) : null;
        img.sprite = sprite;
        img.enabled = sprite != null;
        TooltipTrigger.Add(img, IconInfo.Element(element));
    }

    static string MulColor(float m, bool inverse = false)
    {
        bool good = inverse ? m < 1f : m > 1f;
        bool bad = inverse ? m > 1f : m < 1f;
        return good ? "#7CFC6A" : bad ? "#FF6B6B" : "#EEF1F7";
    }

    object Wait(float seconds)
    {
        if (skipping) return null;
        return new WaitForSecondsRealtime(seconds * baseDelay / Speed);
    }

    CardView ViewFor(CardRuntime rt)
    {
        if (rt == null) return null;
        return rt.isPlayer ? playerView : enemyView;
    }

    void ApplySnapshot(BattleEvent e, bool instant)
    {
        playerView.SetVitals(e.playerHp, e.playerMaxHp, e.playerMana, e.playerMaxMana, instant);
        enemyView.SetVitals(e.enemyHp, e.enemyMaxHp, e.enemyMana, e.enemyMaxMana, instant);
    }

    void PlayInstant(BattleEvent e)
    {
        if (!string.IsNullOrEmpty(e.text) && e.type != BattleEventType.TurnStart) Log(e.text);
        ApplySnapshot(e, true);
        playerView.RefreshLive();
        enemyView.RefreshLive();
        if (e.type == BattleEventType.RoundStart) roundText.text = $"ROUND {e.round}";
        if (e.type == BattleEventType.BattleEnd) MarkDefeated();
    }

    IEnumerator PlayEvent(BattleEvent e)
    {
        var actorView = ViewFor(e.actor);
        var targetView = ViewFor(e.target);

        switch (e.type)
        {
            case BattleEventType.BattleStart:
                Log(e.text);
                ApplySnapshot(e, true);
                ShowBanner("FIGHT!", UIFactory.Accent2);
                yield return Wait(0.8f);
                break;

            case BattleEventType.RoundStart:
                roundText.text = $"ROUND {e.round}";
                Log(e.text);
                yield return Wait(0.15f);
                break;

            case BattleEventType.TurnStart:
                if (actorView != null) actorView.Pulse(0.2f / Speed, 0.03f);
                yield return Wait(0.12f);
                break;

            case BattleEventType.NormalAttack:
                if (actorView != null) actorView.Lunge(actorView == playerView ? 1f : -1f, 0.3f / Speed);
                SfxSynth.Play("swing", 0.6f);
                yield return Wait(0.16f);
                break;

            case BattleEventType.Evaded:
                Log(e.text);
                if (actorView != null) actorView.Lunge(actorView == playerView ? 1f : -1f, 0.3f / Speed);
                yield return Wait(0.15f);
                Popup(targetView, "MISS", new Color(0.8f, 0.85f, 0.9f), 44);
                SfxSynth.Play("miss");
                yield return Wait(0.35f);
                break;

            case BattleEventType.Damage:
                Log(e.text);
                ApplySnapshot(e, false);
                if (targetView != null)
                {
                    targetView.Shake(e.crit ? 0.35f : 0.22f, e.crit ? 22f : 12f);
                    targetView.Flash(DamageFlash(e.kind), 0.3f);
                }
                Popup(targetView, DamageLabel(e), DamageColor(e.kind), e.crit ? 64 : 50);
                SfxSynth.Play(DamageSfx(e.kind), e.crit ? 1f : 0.8f, Random.Range(0.92f, 1.08f));
                yield return Wait(e.crit ? 0.5f : 0.34f);
                break;

            case BattleEventType.Heal:
                Log(e.text);
                ApplySnapshot(e, false);
                if (targetView != null) targetView.Flash(new Color(0.4f, 1f, 0.6f, 0.45f), 0.35f);
                Popup(targetView, $"+{e.amount}", new Color(0.45f, 1f, 0.6f), 44);
                SfxSynth.Play("heal", 0.7f);
                yield return Wait(0.32f);
                break;

            case BattleEventType.SkillActivated:
                Log(e.text);
                ApplySnapshot(e, false);
                if (actorView != null)
                {
                    actorView.Pulse(0.6f / Speed, 0.08f);
                    actorView.Flash(new Color(0.45f, 0.8f, 1f, 0.5f), 0.5f);
                }
                string skillName = e.actor != null && e.actor.talent != null ? e.actor.talent.DisplayName : "Talent";
                ShowBanner(skillName.ToUpperInvariant() + "!", e.actor != null && e.actor.isPlayer ? UIFactory.Accent : UIFactory.Accent2);
                SfxSynth.Play("skill");
                yield return Wait(0.85f);
                break;

            case BattleEventType.PassiveTriggered:
                Log(e.text);
                ApplySnapshot(e, false);
                if (actorView != null) actorView.Pulse(0.4f / Speed, 0.05f);
                SfxSynth.Play("buff", 0.6f);
                yield return Wait(0.45f);
                break;

            case BattleEventType.Status:
                Log(e.text);
                ApplySnapshot(e, false);
                if (e.text != null && e.text.Contains("stunned")) SfxSynth.Play("stun");
                yield return Wait(0.3f);
                break;

            case BattleEventType.TurnSkipped:
                Log(e.text);
                Popup(actorView, "STUNNED", new Color(1f, 0.88f, 0.3f), 44);
                SfxSynth.Play("stun");
                yield return Wait(0.5f);
                break;

            case BattleEventType.Info:
                Log(e.text);
                ApplySnapshot(e, false);
                yield return Wait(0.18f);
                break;

            case BattleEventType.BattleEnd:
                Log(e.text);
                ApplySnapshot(e, false);
                MarkDefeated();
                yield return Wait(0.7f);
                break;
        }

        playerView.RefreshLive();
        enemyView.RefreshLive();
    }

    void MarkDefeated()
    {
        if (playerView.Runtime != null && !playerView.Runtime.IsAlive) playerView.SetDefeated(true);
        if (enemyView.Runtime != null && !enemyView.Runtime.IsAlive) enemyView.SetDefeated(true);
    }

    static string DamageLabel(BattleEvent e)
    {
        switch (e.kind)
        {
            case DamageKind.Critical: return $"CRIT {e.amount}!";
            case DamageKind.Burn: return $"BURN {e.amount}";
            case DamageKind.Poison: return $"POISON {e.amount}";
            case DamageKind.Reflect: return $"REFLECT {e.amount}";
            case DamageKind.Recoil: return $"RECOIL {e.amount}";
            default: return e.amount.ToString();
        }
    }

    static Color DamageColor(DamageKind kind)
    {
        switch (kind)
        {
            case DamageKind.Critical: return new Color(1f, 0.7f, 0.2f);
            case DamageKind.True: return new Color(0.85f, 0.6f, 1f);
            case DamageKind.Skill: return new Color(0.5f, 0.85f, 1f);
            case DamageKind.Burn: return new Color(1f, 0.5f, 0.2f);
            case DamageKind.Poison: return new Color(0.5f, 1f, 0.4f);
            case DamageKind.Reflect: return new Color(0.45f, 0.85f, 1f);
            case DamageKind.Recoil: return new Color(1f, 0.55f, 0.55f);
            default: return Color.white;
        }
    }

    static Color DamageFlash(DamageKind kind)
    {
        switch (kind)
        {
            case DamageKind.Burn: return new Color(1f, 0.45f, 0.1f, 0.5f);
            case DamageKind.Poison: return new Color(0.4f, 1f, 0.3f, 0.45f);
            case DamageKind.True: return new Color(0.75f, 0.45f, 1f, 0.5f);
            default: return new Color(1f, 0.2f, 0.2f, 0.5f);
        }
    }

    static string DamageSfx(DamageKind kind)
    {
        switch (kind)
        {
            case DamageKind.Critical: return "crit";
            case DamageKind.Burn: return "burn";
            case DamageKind.Poison: return "poison";
            default: return "hit";
        }
    }

    void Popup(CardView view, string message, Color color, int size)
    {
        if (view == null || popupLayer == null) return;
        Vector3 world = view.transform.position;
        Vector2 local = popupLayer.InverseTransformPoint(world);
        local += new Vector2(Random.Range(-50f, 50f), Random.Range(40f, 110f));
        DamagePopup.Spawn(popupLayer, local, message, color, size, 1.1f);
    }

    void Log(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        logLines.Add(line);
        while (logLines.Count > 9) logLines.RemoveAt(0);
        logText.text = string.Join("\n", logLines);
    }

    void ShowBanner(string text, Color color)
    {
        if (skipping) return;
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerRoutine(text, color));
    }

    IEnumerator BannerRoutine(string text, Color color)
    {
        bannerText.text = text;
        if (bannerBackground != null) bannerBackground.color = UIFactory.WithAlpha(color, 0.85f);
        var rt = (RectTransform)bannerGroup.transform;
        float duration = 0.9f / Speed;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            bannerGroup.alpha = k < 0.2f ? k / 0.2f : k > 0.75f ? 1f - (k - 0.75f) / 0.25f : 1f;
            rt.localScale = new Vector3(1f, Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(k / 0.2f)), 1f);
            yield return null;
        }
        bannerGroup.alpha = 0f;
    }

    // ---------------------------------------------------------------- result

    void ShowResult(bool victory, bool timedOut)
    {
        resultPanel.SetActive(true);
        nextButton.gameObject.SetActive(false);
        retryButton.gameObject.SetActive(true);
        resultHint.text = "";
        resultCardArt.gameObject.SetActive(false);
        resultCardLabel.text = "";

        if (victory)
        {
            SfxSynth.Play("win");
            var rewards = PlayerProfile.ApplyVictory(location, floor);
            resultTitle.text = rewards.firstClear ? "FLOOR CLEARED!" : "VICTORY!";
            resultTitle.color = UIFactory.Gold;
            resultBody.text = string.Join("\n", rewards.lines);
            var dropArt = rewards.droppedCard != null ? PlayerProfile.Art(rewards.droppedCard) : null;
            if (dropArt != null)
            {
                resultCardArt.gameObject.SetActive(true);
                resultCardArt.sprite = dropArt.cardImg;
                resultCardArt.color = dropArt.cardImg != null ? Color.white : CardStats.RarityColor(rewards.droppedCard.rarity);
                if (dropArt.cardImg == null && !string.IsNullOrEmpty(dropArt.imageUrl))
                {
                    RemoteImageCache.Request(dropArt.imageUrl, sprite =>
                    {
                        if (resultCardArt == null || sprite == null) return;
                        resultCardArt.sprite = sprite;
                        resultCardArt.color = Color.white;
                    });
                }
                resultCardLabel.text = $"<color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(rewards.droppedCard.rarity))}>CARD DROP: {dropArt.DisplayName}</color>";
            }
            SetupNextButton();
        }
        else
        {
            SfxSynth.Play("lose");
            int staminaBack = PlayerProfile.ApplyDefeat();
            resultTitle.text = timedOut ? "TIME UP" : "DEFEAT";
            resultTitle.color = UIFactory.Bad;
            string counter = CounterElementHint(enemyCard.element);
            resultBody.text =
                $"{enemyCard.DisplayName} was too strong.\n\n" +
                "Tips:\n" +
                $"- {counter} (Element Orb in My Cards)\n" +
                "- Reroll stats or talent, or Fuse to raise rarity\n" +
                "- Save other builds to slots 2-3 and pick one on the map" +
                (staminaBack > 0 ? $"\n\n<color=#7CFC6A>+{staminaBack} Stamina (Returning Heroes)</color>" : "");
        }

        retryButton.onClick.RemoveAllListeners();
        retryButton.onClick.AddListener(() => TryStart(location, floor));
    }

    static string CounterElementHint(CardElement enemyElement)
    {
        foreach (CardElement e in System.Enum.GetValues(typeof(CardElement)))
        {
            if (ElementSystem.GetElementMultiplier(e, enemyElement) > 1f)
                return $"{ElementSystem.DisplayName(e)} deals {ElementSystem.Describe(ElementSystem.GetElementMultiplier(e, enemyElement))} to {ElementSystem.DisplayName(enemyElement)}";
        }
        return $"No element has an advantage over {ElementSystem.DisplayName(enemyElement)}";
    }

    void SetupNextButton()
    {
        var db = CardDatabase.Instance;
        int nextLoc = location;
        int nextFloor = floor + 1;
        if (nextFloor > db.FloorCount(location))
        {
            nextLoc = location + 1;
            nextFloor = 1;
        }
        if (db.GetFloorCard(nextLoc, nextFloor) == null || !PlayerProfile.IsFloorUnlocked(nextLoc, nextFloor)) return;

        nextButton.gameObject.SetActive(true);
        var label = UIFactory.ButtonLabel(nextButton);
        if (label != null) label.text = nextLoc != location ? $"LOC {nextLoc} >" : $"NEXT FLOOR >";
        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(() => TryStart(nextLoc, nextFloor));
    }

    void TryStart(int loc, int fl)
    {
        if (!PlayerProfile.TrySpendStamina(PlayerProfile.StaminaCost))
        {
            resultHint.text = $"<color=#FF6B6B>Not enough stamina ({PlayerProfile.Data.stamina}/{PlayerProfile.StaminaCost}). +1 every {PlayerProfile.StaminaRegenSeconds / 60} min.</color>";
            return;
        }
        GameSession.StartBattle(loc, fl);
    }

    void Update()
    {
        // Keyboard-free shortcuts are handled by buttons; keep the skip button hidden once finished.
        if (battleFinished && skipButton.gameObject.activeSelf) skipButton.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- layout (called by the editor scene builder)

    public static BattleScreen BuildLayout(RectTransform canvas)
    {
        var screen = canvas.gameObject.AddComponent<BattleScreen>();

        var bg = AnimeBackground.Build(canvas, "battle", new Color(0.55f, 0.2f, 0.5f, 0.75f), new Color(0.06f, 0.04f, 0.12f), out Image gradient);
        screen.backgroundGradient = gradient;
        bg.petalCount = 8;
        var theme = UIFactory.Theme;
        if (theme != null && theme.speedLines != null)
        {
            var lines = UIFactory.CreateImage(bg.transform, "SpeedLines", new Color(1f, 1f, 1f, 0.08f), theme.speedLines);
            UIFactory.Place(lines.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2400, 2400));
            lines.transform.SetSiblingIndex(3);
        }

        // Top bar
        var bar = UIFactory.CreateTopBar(canvas, "", out Button back, out Text title, out Text info);
        screen.backButton = back;
        UIFactory.ButtonLabel(back).text = "< MAP";
        screen.titleText = title;
        title.fontSize = 34;
        title.rectTransform.anchoredPosition = new Vector2(220, 16);
        screen.subtitleText = UIFactory.CreateText(bar, "Subtitle", "", 22, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.Place(screen.subtitleText.rectTransform, new Vector2(0, 0.5f), new Vector2(220, -22), new Vector2(900, 34));
        screen.roundText = info;
        info.text = "ROUND 1";
        info.fontSize = 30;
        info.fontStyle = FontStyle.Bold;
        info.rectTransform.anchoredPosition = new Vector2(-470, 0);
        info.rectTransform.sizeDelta = new Vector2(300, 80);

        screen.speedButton = UIFactory.CreateButton(bar, "SpeedButton", "SPEED x1", UIFactory.PanelLight, 24);
        UIFactory.Place((RectTransform)screen.speedButton.transform, new Vector2(1, 0.5f), new Vector2(-230, 0), new Vector2(190, 62));
        screen.skipButton = UIFactory.CreateButton(bar, "SkipButton", "SKIP >>", UIFactory.Accent, 24);
        UIFactory.Place((RectTransform)screen.skipButton.transform, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(190, 62));

        // Cards
        screen.playerView = CardView.Build(canvas, "PlayerCard");
        UIFactory.Place((RectTransform)screen.playerView.transform, new Vector2(0.5f, 0.5f), new Vector2(-500, 20), new Vector2(CardView.Width, CardView.Height));
        var youLabel = UIFactory.CreateText(screen.playerView.transform, "YouLabel", "YOU", 26, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(youLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 40), new Vector2(200, 36));

        screen.enemyView = CardView.Build(canvas, "EnemyCard");
        UIFactory.Place((RectTransform)screen.enemyView.transform, new Vector2(0.5f, 0.5f), new Vector2(500, 20), new Vector2(CardView.Width, CardView.Height));
        var enemyLabel = UIFactory.CreateText(screen.enemyView.transform, "EnemyLabel", "ENEMY", 26, UIFactory.Accent2, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(enemyLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 40), new Vector2(200, 36));

        // Reserve row under the player card
        screen.reserveRow = UIFactory.CreateRect("TeamReserve", canvas);
        UIFactory.Place(screen.reserveRow, new Vector2(0.5f, 0f), new Vector2(-500, 18), new Vector2(330, 140));

        // Center column
        screen.vsText = UIFactory.CreateText(canvas, "VS", "VS", 96, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(screen.vsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(300, 120));
        UIFactory.AddOutline(screen.vsText, UIFactory.Accent2, 3f);
        UIFactory.AddShadow(screen.vsText, UIFactory.ShadowColor, 6f);

        screen.playerElementIcon = UIFactory.CreateImage(canvas, "PlayerElement", Color.white);
        screen.playerElementIcon.preserveAspect = true;
        UIFactory.Place(screen.playerElementIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-80, 180), new Vector2(76, 76));
        screen.enemyElementIcon = UIFactory.CreateImage(canvas, "EnemyElement", Color.white);
        screen.enemyElementIcon.preserveAspect = true;
        UIFactory.Place(screen.enemyElementIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(80, 180), new Vector2(76, 76));
        var vsSmall = UIFactory.CreateText(canvas, "MatchupVs", "vs", 26, UIFactory.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(vsSmall.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(60, 40));
        screen.matchupText = UIFactory.CreateText(canvas, "Matchup", "", 28, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(screen.matchupText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(560, 64));
        UIFactory.AddShadow(screen.matchupText, UIFactory.ShadowColor, 2f);

        var logPanel = UIFactory.CreatePanel(canvas, "BattleLog", new Color(0.03f, 0.04f, 0.08f, 0.82f), false);
        UIFactory.Place(logPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(560, 380));
        var logTitle = UIFactory.CreateText(logPanel.transform, "Title", "BATTLE LOG", 18, UIFactory.TextDim, TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopStrip(logTitle.rectTransform, 10, 24, 16, 16);
        screen.logText = UIFactory.CreateText(logPanel.transform, "Log", "", 19, UIFactory.TextMain, TextAnchor.LowerLeft);
        UIFactory.Stretch(screen.logText.rectTransform, 16, 40, 16, 12);
        screen.logText.verticalOverflow = VerticalWrapMode.Truncate;

        // Banner
        var banner = UIFactory.CreateRect("TalentBanner", canvas);
        UIFactory.Place(banner, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1100, 110));
        screen.bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        screen.bannerGroup.blocksRaycasts = false;
        screen.bannerGroup.interactable = false;
        screen.bannerBackground = UIFactory.CreatePanel(banner, "Background", UIFactory.Accent, false);
        UIFactory.Stretch(screen.bannerBackground.rectTransform);
        screen.bannerText = UIFactory.CreateText(banner, "Text", "", 58, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(screen.bannerText.rectTransform, 20, 0, 20, 0);
        UIFactory.BestFit(screen.bannerText, 20);
        UIFactory.AddOutline(screen.bannerText, new Color(0, 0, 0, 0.7f), 2.5f);

        // Popups above everything except the result panel
        screen.popupLayer = UIFactory.CreateRect("PopupLayer", canvas);
        UIFactory.Stretch(screen.popupLayer);

        // Result panel
        var dim = UIFactory.CreateImage(canvas, "ResultPanel", new Color(0, 0, 0, 0.72f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        screen.resultPanel = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", UIFactory.Panel, true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 720));
        screen.resultTitle = UIFactory.CreateText(panel.transform, "Title", "VICTORY!", 72, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(screen.resultTitle.rectTransform, 30, 100, 20, 20);
        UIFactory.AddShadow(screen.resultTitle, UIFactory.ShadowColor, 4f);
        screen.resultBody = UIFactory.CreateText(panel.transform, "Body", "", 26, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopLeft(screen.resultBody.rectTransform, 60, 150, 600, 400);
        screen.resultCardArt = UIFactory.CreateImage(panel.transform, "NewCardArt", Color.white);
        screen.resultCardArt.preserveAspect = true;
        UIFactory.TopLeft(screen.resultCardArt.rectTransform, 700, 150, 240, 340);
        screen.resultCardLabel = UIFactory.CreateText(panel.transform, "NewCardLabel", "", 22, UIFactory.TextMain, TextAnchor.UpperCenter, FontStyle.Bold);
        UIFactory.TopLeft(screen.resultCardLabel.rectTransform, 680, 500, 280, 60);
        screen.resultHint = UIFactory.CreateText(panel.transform, "Hint", "", 22, UIFactory.TextMain);
        UIFactory.TopStrip(screen.resultHint.rectTransform, 560, 40, 20, 20);

        screen.mapButton = UIFactory.CreateButton(panel.transform, "MapButton", "MAP", UIFactory.PanelLight, 30);
        UIFactory.Place((RectTransform)screen.mapButton.transform, new Vector2(0.5f, 0f), new Vector2(-300, 40), new Vector2(260, 80));
        screen.retryButton = UIFactory.CreateButton(panel.transform, "RetryButton", "RETRY", UIFactory.Accent2, 30);
        UIFactory.Place((RectTransform)screen.retryButton.transform, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(260, 80));
        screen.nextButton = UIFactory.CreateButton(panel.transform, "NextButton", "NEXT FLOOR >", UIFactory.Accent, 30);
        UIFactory.Place((RectTransform)screen.nextButton.transform, new Vector2(0.5f, 0f), new Vector2(300, 40), new Vector2(260, 80));

        dim.gameObject.SetActive(false);
        return screen;
    }
}
