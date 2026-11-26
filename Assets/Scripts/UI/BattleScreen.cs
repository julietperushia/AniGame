using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public ScrollRect logScroll;
    public RectTransform reserveRow;
    public Text playerTalentText;
    public Text enemyTalentText;

    [Header("Result")]
    public GameObject resultPanel;
    public Text resultTitle;
    public Text resultBody;
    public Image resultCardArt;
    public Text resultCardLabel;
    public Text resultSubtitle;
    public RectTransform rewardRow;
    public CardView resultDropView;
    public Text resultNotes;
    public Button nextButton;
    public Button retryButton;
    public Button mapButton;
    public Text resultHint;
    public Button saveLogButton;

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
    RectTransform arena;      // everything but the result panel (camera shake / zoom)
    RectTransform backLayer;  // effects behind the cards (auras, light bursts)
    CardView meleeActor;      // attacker that dashed forward and waits for its hit
    float meleeDir;
    Color dropColor = Color.white;
    int dropTier = -1;

    /// <summary>x1 plays at 60% of the old pace (it felt too fast); x2 / x4 scale from there.</summary>
    public const float PaceScale = 0.6f;
    int SpeedLabel => speeds[Mathf.Clamp(speedIndex, 0, speeds.Length - 1)];
    float Speed => SpeedLabel * PaceScale;
    readonly List<string> fullLog = new List<string>();
    BattleState finalState;
    int shownTalentCount = -1;

    void Start()
    {
        resultPanel.SetActive(false);
        bannerGroup.alpha = 0f;
        logText.text = "";
        SetupArena();

        backButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.LocationMap));
        speedButton.onClick.AddListener(CycleSpeed);
        skipButton.onClick.AddListener(() => skipping = true);
        mapButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.LocationMap));

        var db = CardDatabase.Instance;
        if (db == null || db.LocationCount == 0)
        {
            ShowFatal("The game data could not be loaded.\nPlease reinstall the game.");
            return;
        }

        var data = PlayerProfile.Data;
        SfxSynth.Volume = data.sfxVolume;
        speedIndex = Mathf.Max(0, System.Array.IndexOf(speeds, data.battleSpeed));
        if (saveLogButton != null) saveLogButton.onClick.AddListener(SaveBattleLog);
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
            ShowFatal("No card in your build slot. Open Collection and save a card to a slot.");
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
        if (label != null) label.text = $"SPEED x{SpeedLabel}";
        DamagePopup.SpeedMultiplier = Speed;
        UIFx.Speed = Speed;
    }

    void OnDestroy()
    {
        UIFx.Speed = 1f;
        DamagePopup.SpeedMultiplier = 1f;
    }

    // ---------------------------------------------------------------- battle flow

    IEnumerator RunBattle(CardRuntime player)
    {
        var enemy = new CardRuntime(enemyCard);
        ApplyTestTalents(player, GameSession.TestPlayerTalents);
        ApplyTestTalents(enemy, GameSession.TestEnemyTalents);
        GameSession.TestPlayerTalents = GameSession.TestEnemyTalents = null;
        enemyView.Bind(enemy);
        playerView.Bind(player);
        playerView.idle = enemyView.idle = true;
        RefreshTalentPanels();
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

        if (skipping)
        {
            meleeActor = null;
            playerView.ResetPose();
            enemyView.ResetPose();
            MarkDefeated();
        }
        battleFinished = true;
        var state = engine.State;
        finalState = state;
        bool victory = state == BattleState.PlayerWon || (state == BattleState.Draw && !enemy.IsAlive);
        ShowResult(victory, state == BattleState.Draw && enemy.IsAlive);
    }

    /// <summary>Test battles only: replaces a fighter's talents with the named ones.</summary>
    static void ApplyTestTalents(CardRuntime rt, string names)
    {
        var db = CardDatabase.Instance;
        if (string.IsNullOrEmpty(names) || db == null) return;
        rt.talents.Clear();
        rt.talent = null;
        foreach (var n in names.Split(',')) rt.AddTalent(db.FindTalent(n.Trim()));
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
        playerView.SetVitals(e.playerHp, e.playerMaxHp, e.playerMana, e.playerMaxMana, instant, e.playerShield);
        enemyView.SetVitals(e.enemyHp, e.enemyMaxHp, e.enemyMana, e.enemyMaxMana, instant, e.enemyShield);
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
        float dir = actorView == enemyView ? -1f : 1f;

        switch (e.type)
        {
            case BattleEventType.BattleStart:
                Log(e.text);
                ApplySnapshot(e, true);
                yield return IntroRun();
                break;

            case BattleEventType.RoundStart:
                EndMelee();
                roundText.text = $"ROUND {e.round}";
                UIFx.PopScale(roundText.rectTransform, 1.5f, 0.3f);
                Log(e.text);
                yield return Wait(0.15f);
                break;

            case BattleEventType.TurnStart:
                EndMelee();
                if (actorView != null)
                {
                    actorView.Pulse(0.22f, 0.035f);
                    UIFx.GlowFlash(backLayer, FxPos(actorView, backLayer), UIFactory.WithAlpha(SideColor(actorView), 0.35f), 640f, 0.35f);
                }
                yield return Wait(0.12f);
                break;

            case BattleEventType.NormalAttack:
                yield return AttackRun(actorView, dir, e.crit);
                break;

            case BattleEventType.Evaded:
                Log(e.text);
                yield return EvadeRun(actorView, targetView, dir, e.miss);
                break;

            case BattleEventType.Damage:
                Log(e.text);
                ApplySnapshot(e, false);
                yield return DamageRun(e, targetView);
                break;

            case BattleEventType.Heal:
                Log(e.text);
                ApplySnapshot(e, false);
                if (targetView != null)
                {
                    targetView.Flash(new Color(0.4f, 1f, 0.6f, 0.45f), 0.35f);
                    var at = FxPos(targetView, popupLayer);
                    UIFx.Rise(popupLayer, at, new Color(0.5f, 1f, 0.65f, 0.95f), 14, 300f, 30f, 0.9f);
                    UIFx.Ring(popupLayer, at, new Color(0.45f, 1f, 0.6f, 0.6f), 120f, 520f, 0.5f);
                }
                Popup(targetView, $"+{e.amount}", new Color(0.45f, 1f, 0.6f), 44);
                SfxSynth.Play("heal", 0.7f);
                yield return Wait(0.32f);
                break;

            case BattleEventType.SkillActivated:
                Log(e.text);
                ApplySnapshot(e, false);
                if (actorView != null)
                {
                    var color = SideColor(actorView);
                    var back = FxPos(actorView, backLayer);
                    var front = FxPos(actorView, popupLayer);
                    actorView.Pulse(0.6f, 0.08f);
                    actorView.Flash(new Color(0.45f, 0.8f, 1f, 0.5f), 0.5f);
                    UIFx.Starburst(backLayer, back, UIFactory.WithAlpha(color, 0.55f), 900f, 0.9f, 60f);
                    UIFx.GlowFlash(backLayer, back, UIFactory.WithAlpha(color, 0.6f), 760f, 0.6f);
                    UIFx.Ring(popupLayer, front, new Color(0.6f, 0.9f, 1f, 0.85f), 100f, 640f, 0.55f);
                    UIFx.Rise(popupLayer, front, new Color(0.7f, 0.92f, 1f, 1f), 16, 360f, 32f, 0.9f);
                }
                string skillName = e.skill != null ? e.skill.DisplayName : e.actor != null && e.actor.talent != null ? e.actor.talent.DisplayName : "Talent";
                ShowBanner(skillName.ToUpperInvariant() + "!", e.actor != null && e.actor.isPlayer ? UIFactory.Accent : UIFactory.Accent2);
                SfxSynth.Play("skill");
                yield return Wait(0.85f);
                break;

            case BattleEventType.PassiveTriggered:
                Log(e.text);
                ApplySnapshot(e, false);
                if (actorView != null)
                {
                    actorView.Pulse(0.4f, 0.05f);
                    var at = FxPos(actorView, popupLayer);
                    UIFx.Ring(popupLayer, at, new Color(1f, 0.85f, 0.4f, 0.6f), 120f, 480f, 0.4f);
                    UIFx.Rise(popupLayer, at, new Color(1f, 0.9f, 0.5f, 1f), 8, 260f, 26f, 0.7f);
                }
                SfxSynth.Play("buff", 0.6f);
                yield return Wait(0.45f);
                break;

            case BattleEventType.Status:
                Log(e.text);
                ApplySnapshot(e, false);
                if (EventFx(e, actorView, targetView)) { yield return Wait(0.35f); break; }
                if (e.text != null && e.text.Contains("stunned"))
                {
                    SfxSynth.Play("stun");
                    if (targetView != null) StunStars(targetView);
                }
                yield return Wait(0.3f);
                break;

            case BattleEventType.TurnSkipped:
                Log(e.text);
                string why = e.text != null && e.text.Contains("frozen") ? "FROZEN" : e.text != null && e.text.Contains("asleep") ? "ZZZ..." : "STUNNED";
                Popup(actorView, why, why == "FROZEN" ? new Color(0.6f, 0.9f, 1f) : why == "ZZZ..." ? new Color(0.7f, 0.75f, 1f) : new Color(1f, 0.88f, 0.3f), 44);
                if (actorView != null) { StunStars(actorView); actorView.Shake(0.3f, 6f); }
                SfxSynth.Play("stun");
                yield return Wait(0.5f);
                break;

            case BattleEventType.Info:
                Log(e.text);
                ApplySnapshot(e, false);
                yield return Wait(EventFx(e, actorView, targetView) ? 0.4f : 0.18f);
                break;

            case BattleEventType.BattleEnd:
                EndMelee();
                Log(e.text);
                ApplySnapshot(e, false);
                yield return FinishRun();
                break;
        }

        playerView.RefreshLive();
        enemyView.RefreshLive();
        int count = (playerView.Runtime?.talents.Count ?? 0) * 100 + (enemyView.Runtime?.talents.Count ?? 0);
        if (count != shownTalentCount) RefreshTalentPanels();
    }

    // ---------------------------------------------------------------- talent panels (always visible)

    void RefreshTalentPanels()
    {
        shownTalentCount = (playerView.Runtime?.talents.Count ?? 0) * 100 + (enemyView.Runtime?.talents.Count ?? 0);
        if (playerTalentText != null) playerTalentText.text = TalentSheet(playerView.Runtime);
        if (enemyTalentText != null) enemyTalentText.text = TalentSheet(enemyView.Runtime);
    }

    static string TalentSheet(CardRuntime rt)
    {
        if (rt == null || rt.talents.Count == 0) return "<color=#9AA4B5>No talent</color>";
        var sb = new System.Text.StringBuilder();
        int bonus = Mathf.RoundToInt((GameRules.TalentPower(rt.Tier) - 1f) * 100f);
        foreach (var t in rt.talents)
        {
            if (sb.Length > 0) sb.Append("\n");
            string type = t.IsActive ? "<color=#6FD3FF>ACTIVE</color>" : "<color=#FFB23D>PASSIVE</color>";
            float scale = rt.ScaleOf(t);
            string extra = scale < 0.99f ? $" <color=#9AA4B5>({scale * 100:0}% from Gamble)</color>" : bonus > 0 ? $" <color=#7CFC6A>+{bonus}%</color>" : "";
            sb.Append($"<b>{t.DisplayName}</b>  {type}{extra}\n<color=#C9CEDA>{t.DescriptionFor(rt.PowerOf(t))}</color>");
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- battle log (scrollable, saveable)

    void SaveBattleLog()
    {
        if (fullLog.Count == 0) { resultHint.text = "<color=#FF6B6B>The log is empty.</color>"; return; }
        string result = finalState == BattleState.PlayerWon ? "VICTORY" : finalState == BattleState.EnemyWon ? "DEFEAT" : "DRAW";
        string stamp = System.DateTime.Now.ToString("yyyy-MM-dd_HHmm");
        string defaultName = $"AniGame_battle_LOC{location}_F{floor}_{result}_{stamp}";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"AniGame battle log - {System.DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"LOC {location} - Floor {floor} - {result}");
        sb.AppendLine($"You:   {Describe(playerView.Runtime)}");
        sb.AppendLine($"Enemy: {Describe(enemyView.Runtime)}");
        sb.AppendLine(new string('-', 60));
        foreach (var line in fullLog) sb.AppendLine(Strip(line));
        string path = FileDialogs.Supported ? FileDialogs.SaveFile("Save battle log", "Text file", "txt", defaultName) : null;
        if (path == null && FileDialogs.Supported) return; // cancelled
        try
        {
            if (path == null)
            {
                string dir = System.IO.Path.Combine(Application.persistentDataPath, "BattleLogs");
                System.IO.Directory.CreateDirectory(dir);
                path = System.IO.Path.Combine(dir, defaultName + ".txt");
            }
            System.IO.File.WriteAllText(path, sb.ToString());
            resultHint.text = $"<color=#7CFC6A>Battle log saved: {System.IO.Path.GetFileName(path)}</color>";
            SfxSynth.Play("buff", 0.6f);
        }
        catch (System.Exception ex)
        {
            resultHint.text = $"<color=#FF6B6B>Could not save the log: {ex.Message}</color>";
        }
    }

    static string Strip(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<.*?>", "");

    static string Describe(CardRuntime rt)
    {
        if (rt == null) return "?";
        string talents = string.Join(" + ", rt.talents.Select(t => t.DisplayName));
        return $"{rt.DisplayName} ({CardStats.RarityName(rt.rarity)}, {ElementSystem.DisplayName(rt.Element)}) HP {rt.baseMaxHealth} ATK {rt.baseAttack} DEF {rt.baseDefense} SPD {rt.baseSpeed} - {talents}";
    }

    // ---------------------------------------------------------------- animation runs

    /// <summary>Puts every top-level element (except the result panel) in an "Arena" so the whole
    /// battle can shake / zoom like a camera, and adds a layer behind the cards for auras.</summary>
    void SetupArena()
    {
        var canvas = (RectTransform)transform;
        arena = UIFactory.CreateRect("Arena", canvas);
        UIFactory.Stretch(arena);
        var move = new List<Transform>();
        foreach (Transform child in canvas)
            if (child != arena && (resultPanel == null || child != resultPanel.transform)) move.Add(child);
        arena.SetSiblingIndex(0);
        foreach (var c in move) c.SetParent(arena, false);
        if (resultPanel != null) resultPanel.transform.SetAsLastSibling();

        backLayer = UIFactory.CreateRect("BackFx", arena);
        UIFactory.Stretch(backLayer);
        int index = 0;
        for (int i = 0; i < arena.childCount; i++)
            if (arena.GetChild(i).GetComponent<AnimeBackground>() != null) { index = i + 1; break; }
        backLayer.SetSiblingIndex(index);
    }

    Vector2 FxPos(CardView view, RectTransform layer)
    {
        if (view == null) return Vector2.zero;
        return UIFx.LocalPos(layer, view.body != null ? view.body : (RectTransform)view.transform);
    }

    Color SideColor(CardView view) => view == playerView ? UIFactory.Accent : UIFactory.Accent2;

    void EndMelee()
    {
        if (meleeActor != null) meleeActor.ReturnHome(0.22f);
        meleeActor = null;
    }

    IEnumerator IntroRun()
    {
        if (skipping) yield break;
        playerView.EnterFrom(-1f);
        enemyView.EnterFrom(1f);
        SfxSynth.Play("whoosh", 0.8f);
        yield return Wait(0.42f);
        UIFx.Shake(arena, 0.22f, 9f);
        if (vsText != null)
        {
            UIFx.PopScale(vsText.rectTransform, 2.6f, 0.4f);
            var vsPos = UIFx.LocalPos(popupLayer, vsText.rectTransform);
            UIFx.Ring(popupLayer, vsPos, UIFactory.WithAlpha(UIFactory.Accent2, 0.9f), 60f, 560f, 0.5f);
            UIFx.Sparks(popupLayer, vsPos, UIFactory.Gold, 16, 700f, 30f, 0.55f);
        }
        SfxSynth.Play("impact", 0.6f, 1.2f);
        ShowBanner("FIGHT!", UIFactory.Accent2);
        yield return Wait(0.75f);
    }

    IEnumerator AttackRun(CardView attacker, float dir, bool crit)
    {
        if (attacker == null) yield break;
        attacker.WindUp(dir, crit);
        if (crit)
        {
            var at = FxPos(attacker, popupLayer);
            attacker.Flash(new Color(1f, 0.75f, 0.2f, 0.6f), 0.35f);
            UIFx.GlowFlash(backLayer, FxPos(attacker, backLayer), new Color(1f, 0.55f, 0.1f, 0.8f), 700f, 0.4f);
            UIFx.Ring(popupLayer, at, new Color(1f, 0.8f, 0.3f, 0.9f), 620f, 160f, 0.28f);
            UIFx.Sparks(popupLayer, at + new Vector2(0f, 180f), new Color(1f, 0.85f, 0.35f), 6, 220f, 46f, 0.35f, 0f);
            SfxSynth.Play("charge", 0.85f);
            yield return Wait(0.3f);
        }
        else yield return Wait(0.13f);

        Color ghost = crit ? new Color(1f, 0.75f, 0.25f) : new Color(0.65f, 0.75f, 1f);
        attacker.Dash(dir, crit ? 240f : 200f, ghost);
        UIFx.Streaks(popupLayer, FxPos(attacker, popupLayer), dir, new Color(1f, 1f, 1f, 0.55f), crit ? 8 : 5, 420f, 0.3f);
        SfxSynth.Play("swing", crit ? 0.9f : 0.6f, crit ? 0.85f : 1f);
        meleeActor = attacker;
        meleeDir = dir;
        yield return Wait(0.09f);
    }

    IEnumerator EvadeRun(CardView attacker, CardView dodger, float dir, bool miss = false)
    {
        if (attacker != null)
        {
            attacker.WindUp(dir, false);
            yield return Wait(0.12f);
            attacker.Dash(dir, 250f, new Color(0.65f, 0.75f, 1f));
            SfxSynth.Play("swing", 0.6f);
        }
        if (dodger != null)
        {
            yield return Wait(0.03f);
            UIFx.Ghost(dodger, new Color(0.45f, 0.9f, 1f), 0.45f, 0.6f);
            dodger.Dodge(dir);
            var at = FxPos(dodger, popupLayer);
            UIFx.Streaks(popupLayer, at, dir, new Color(0.75f, 0.95f, 1f, 0.85f), 8, 480f, 0.36f);
            UIFx.TextSlam(popupLayer, at + new Vector2(0f, 200f), miss ? "MISS!" : "EVADE!", miss ? new Color(0.8f, 0.85f, 0.9f) : new Color(0.55f, 0.95f, 1f), 76, 0.85f, dir * 6f);
            SfxSynth.Play("whoosh", 0.9f, 1.1f);
            SfxSynth.Play("miss", 0.45f);
        }
        yield return Wait(0.2f);
        if (attacker != null) attacker.ReturnHome(0.28f);
        yield return Wait(0.34f);
    }

    IEnumerator DamageRun(BattleEvent e, CardView target)
    {
        if (target == null) { yield return Wait(0.2f); yield break; }
        bool melee = meleeActor != null && target != meleeActor && (e.kind == DamageKind.Normal || e.kind == DamageKind.Critical);
        var at = FxPos(target, popupLayer);

        if (melee)
        {
            float d = meleeDir;
            bool crit = e.crit;
            var hit = at + new Vector2(-d * 70f, 40f);
            float angle = d > 0f ? -32f : 212f;
            if (crit)
            {
                UIFx.ScreenFlash(popupLayer, new Color(1f, 0.93f, 0.75f, 0.45f), 0.32f);
                UIFx.Starburst(backLayer, FxPos(target, backLayer), new Color(1f, 0.65f, 0.2f, 0.85f), 1100f, 0.8f, 80f);
                UIFx.Slash(popupLayer, hit, angle, new Color(1f, 0.6f, 0.15f, 1f), 620f, 90f, 0.34f);
                UIFx.Slash(popupLayer, hit, d > 0f ? 32f : 148f, new Color(1f, 0.85f, 0.3f, 1f), 620f, 90f, 0.34f, 0.05f);
                UIFx.Sparks(popupLayer, hit, new Color(1f, 0.78f, 0.25f), 34, 1250f, 40f, 0.7f, 700f);
                UIFx.Ring(popupLayer, hit, new Color(1f, 0.85f, 0.4f, 0.95f), 80f, 700f, 0.45f);
                UIFx.Ring(popupLayer, hit, new Color(1f, 1f, 1f, 0.8f), 40f, 420f, 0.3f, 0.06f);
                UIFx.TextSlam(popupLayer, new Vector2(at.x * 0.4f, 40f), "CRITICAL!", new Color(1f, 0.8f, 0.2f), 112, 1.05f, d > 0f ? -6f : 6f);
                UIFx.Shake(arena, 0.45f, 22f);
                UIFx.Punch(arena, 0.045f, 0.38f);
                UIFx.SlowMo(0.3f, 0.3f);
                target.Flash(new Color(1f, 0.55f, 0.15f, 0.65f), 0.4f);
                SfxSynth.Play("impact", 1f);
                SfxSynth.Play("slash", 0.8f);
            }
            else
            {
                UIFx.Slash(popupLayer, hit, angle, new Color(0.85f, 0.92f, 1f, 0.95f), 460f, 60f, 0.26f);
                UIFx.Sparks(popupLayer, hit, new Color(1f, 0.95f, 0.75f), 14, 800f, 30f, 0.45f, 700f);
                UIFx.Ring(popupLayer, hit, new Color(1f, 1f, 1f, 0.7f), 50f, 380f, 0.3f);
                UIFx.Shake(arena, 0.16f, 7f);
                target.Flash(DamageFlash(e.kind), 0.3f);
                SfxSynth.Play("slash", 0.5f, Random.Range(0.95f, 1.1f));
            }
            // Hit-stop: both cards freeze for a beat, then the target is knocked back.
            yield return Wait(crit ? 0.12f : 0.05f);
            target.HitReact(d, crit);
            meleeActor.ReturnHome(crit ? 0.34f : 0.24f);
            meleeActor = null;
        }
        else
        {
            target.Shake(e.crit ? 0.35f : 0.22f, e.crit ? 22f : 12f);
            target.Flash(DamageFlash(e.kind), 0.3f);
            KindFx(e.kind, at);
        }

        Popup(target, DamageLabel(e), DamageColor(e.kind), e.crit ? 64 : 50);
        SfxSynth.Play(DamageSfx(e.kind), e.crit ? 1f : 0.8f, Random.Range(0.92f, 1.08f));
        yield return Wait(e.crit ? 0.62f : 0.34f);
    }

    /// <summary>Animations for talent effects that arrive as Info / Status events (so the cards never look frozen).
    /// Returns true when it played something.</summary>
    bool EventFx(BattleEvent e, CardView actor, CardView target)
    {
        if (skipping) return false;
        string t = e.text ?? "";
        var who = target ?? actor;
        switch (e.tag)
        {
            case "charge":
                if (actor == null) return false;
                actor.Pulse(0.4f, 0.05f);
                actor.Flash(new Color(1f, 0.7f, 0.15f, 0.45f), 0.4f);
                UIFx.GlowFlash(backLayer, FxPos(actor, backLayer), new Color(1f, 0.6f, 0.1f, 0.7f), 620f, 0.5f);
                UIFx.Ring(popupLayer, FxPos(actor, popupLayer), new Color(1f, 0.75f, 0.2f, 0.9f), 520f, 120f, 0.4f);
                Popup(actor, "CHARGING", new Color(1f, 0.75f, 0.2f), 40);
                SfxSynth.Play("charge", 0.6f);
                return true;
            case "release":
                if (actor == null) return false;
                UIFx.Starburst(backLayer, FxPos(actor, backLayer), new Color(1f, 0.6f, 0.1f, 0.8f), 1000f, 0.8f);
                UIFx.TextSlam(popupLayer, FxPos(actor, popupLayer) + new Vector2(0f, 220f), "RELEASE!", new Color(1f, 0.7f, 0.2f), 70, 0.8f);
                SfxSynth.Play("impact", 0.7f, 1.2f);
                return true;
            case "haste":
                if (actor == null) return false;
                UIFx.Streaks(popupLayer, FxPos(actor, popupLayer), actor == playerView ? 1f : -1f, new Color(0.6f, 1f, 1f, 0.8f), 7, 420f, 0.3f);
                Popup(actor, "HASTE!", new Color(0.4f, 1f, 1f), 40);
                SfxSynth.Play("whoosh", 0.6f, 1.2f);
                return true;
            case "counter":
                if (actor == null) return false;
                Popup(actor, "COUNTER!", new Color(1f, 0.75f, 0.2f), 42);
                return true;
            case "hypno":
                if (actor == null) return false;
                UIFx.Ring(popupLayer, FxPos(actor, popupLayer), new Color(0.75f, 0.4f, 0.9f, 0.9f), 80f, 520f, 0.5f);
                Popup(actor, "HYPNOTIZED", new Color(0.8f, 0.5f, 1f), 40);
                actor.Shake(0.3f, 8f);
                return true;
        }
        if (who == null) return false;
        var at = FxPos(who, popupLayer);
        if (t.Contains("barrier absorbs"))
        {
            bool broke = t.Contains("breaks");
            UIFx.Ring(popupLayer, at, new Color(0.45f, 0.85f, 1f, 0.9f), 380f, 460f, 0.35f);
            UIFx.GlowFlash(popupLayer, at, new Color(0.4f, 0.8f, 1f, 0.35f), 520f, 0.35f);
            var num = System.Text.RegularExpressions.Regex.Match(t, @"absorbs (\d+)");
            string absorbed = num.Success ? num.Groups[1].Value : "";
            // Only a hit the barrier fully holds says BLOCKED; a broken barrier lets the rest through to the HP
            Popup(who, broke ? $"SHIELD -{absorbed}  BROKEN!" : $"BLOCKED {absorbed}", new Color(0.5f, 0.9f, 1f), 38);
            if (broke) who.Shake(0.25f, 8f);
            return true;
        }
        if (t.Contains("barrier explodes"))
        {
            UIFx.Burst(popupLayer, at, new Color(0.45f, 0.85f, 1f), 1.2f);
            SfxSynth.Play("impact", 0.6f, 1.3f);
            return true;
        }
        if (t.Contains("raises a") && t.Contains("barrier"))
        {
            UIFx.Ring(popupLayer, at, new Color(0.45f, 0.85f, 1f, 0.9f), 120f, 520f, 0.5f);
            UIFx.GlowFlash(backLayer, FxPos(who, backLayer), new Color(0.4f, 0.8f, 1f, 0.6f), 700f, 0.6f);
            return true;
        }
        if (t.Contains("[Guts]"))
        {
            UIFx.TextSlam(popupLayer, at + new Vector2(0f, 220f), "GUTS!", new Color(1f, 0.45f, 0.3f), 76, 0.9f);
            who.Shake(0.4f, 12f);
            return true;
        }
        if (t.Contains("[Death's Dance]") && t.Contains("catches up"))
        {
            UIFx.Puff(popupLayer, at, new Color(0.6f, 0.5f, 0.8f), 1.4f);
            return true;
        }
        if (t.Contains("is frozen"))
        {
            UIFx.Sparks(popupLayer, at, new Color(0.7f, 0.95f, 1f), 18, 500f, 30f, 0.6f, 200f);
            who.Flash(new Color(0.6f, 0.9f, 1f, 0.55f), 0.5f);
            return true;
        }
        if (t.Contains("falls asleep"))
        {
            UIFx.Rise(popupLayer, at, new Color(0.7f, 0.75f, 1f, 0.9f), 8, 220f, 34f, 1f);
            Popup(who, "ZZZ", new Color(0.7f, 0.75f, 1f), 44);
            return true;
        }
        if (t.Contains("blinded"))
        {
            who.Flash(new Color(0.9f, 0.9f, 0.9f, 0.6f), 0.4f);
            Popup(who, "BLIND", new Color(0.8f, 0.85f, 0.9f), 40);
            return true;
        }
        if (t.Contains("hexed"))
        {
            UIFx.Ring(popupLayer, at, new Color(0.8f, 0.5f, 0.85f, 0.9f), 80f, 480f, 0.45f);
            Popup(who, "HEX: ATK <> DEF", new Color(0.85f, 0.6f, 0.9f), 36);
            return true;
        }
        if (t.Contains("[Mimic]"))
        {
            var src = actor ?? who;
            UIFx.Ring(popupLayer, FxPos(src, popupLayer), new Color(0.8f, 0.6f, 0.9f, 0.9f), 500f, 100f, 0.4f);
            return true;
        }
        if (t.Contains("[Purify]") || t.Contains("immune"))
        {
            UIFx.Rise(popupLayer, at, new Color(0.85f, 1f, 1f, 1f), 14, 300f, 30f, 0.9f);
            return true;
        }
        return false;
    }

    void KindFx(DamageKind kind, Vector2 at)
    {
        switch (kind)
        {
            case DamageKind.Burn:
                UIFx.Rise(popupLayer, at, new Color(1f, 0.55f, 0.15f, 1f), 16, 260f, 36f, 0.7f, UIFx.DotSprite);
                UIFx.GlowFlash(popupLayer, at, new Color(1f, 0.4f, 0.1f, 0.45f), 420f, 0.4f);
                break;
            case DamageKind.Poison:
                UIFx.Rise(popupLayer, at, new Color(0.5f, 1f, 0.35f, 0.9f), 14, 260f, 30f, 0.8f, UIFx.RingSprite);
                break;
            case DamageKind.Skill:
                UIFx.Ring(popupLayer, at, new Color(0.5f, 0.85f, 1f, 0.9f), 60f, 520f, 0.4f);
                UIFx.Sparks(popupLayer, at, new Color(0.55f, 0.85f, 1f), 18, 800f, 32f, 0.5f, 400f);
                UIFx.Shake(arena, 0.18f, 8f);
                break;
            case DamageKind.True:
                UIFx.Ring(popupLayer, at, new Color(0.8f, 0.55f, 1f, 0.9f), 60f, 480f, 0.4f);
                UIFx.Sparks(popupLayer, at, new Color(0.8f, 0.6f, 1f), 16, 700f, 30f, 0.5f, 400f);
                break;
            case DamageKind.Reflect:
                UIFx.Ring(popupLayer, at, new Color(0.45f, 0.85f, 1f, 0.9f), 400f, 80f, 0.3f);
                UIFx.Sparks(popupLayer, at, new Color(0.5f, 0.9f, 1f), 10, 600f, 26f, 0.4f);
                break;
            case DamageKind.Recoil:
                UIFx.Sparks(popupLayer, at, new Color(1f, 0.5f, 0.5f), 10, 500f, 26f, 0.45f);
                break;
            default:
                UIFx.Sparks(popupLayer, at, Color.white, 10, 600f, 26f, 0.4f);
                break;
        }
    }

    void StunStars(CardView view)
    {
        var at = FxPos(view, popupLayer) + new Vector2(0f, 250f);
        UIFx.Sparks(popupLayer, at, new Color(1f, 0.9f, 0.3f), 8, 260f, 38f, 0.8f, -40f, null, 90f, 200f);
    }

    IEnumerator FinishRun()
    {
        if (skipping) { MarkDefeated(); yield break; }
        bool playerDown = playerView.Runtime != null && !playerView.Runtime.IsAlive;
        bool enemyDown = enemyView.Runtime != null && !enemyView.Runtime.IsAlive;
        if (playerDown || enemyDown)
        {
            UIFx.Shake(arena, 0.35f, 14f);
            SfxSynth.Play("impact", 0.8f, 0.8f);
        }
        if (playerDown) { playerView.KnockOut(-1f); UIFx.Puff(popupLayer, FxPos(playerView, popupLayer), new Color(0.6f, 0.6f, 0.7f)); }
        else if (enemyDown) playerView.Victory();
        if (enemyDown) { enemyView.KnockOut(1f); UIFx.Puff(popupLayer, FxPos(enemyView, popupLayer), new Color(0.6f, 0.6f, 0.7f)); }
        else if (playerDown) enemyView.Victory();
        if (enemyDown && !playerDown)
            UIFx.Rise(popupLayer, FxPos(playerView, popupLayer), new Color(1f, 0.9f, 0.45f, 1f), 18, 380f, 34f, 1.1f);
        yield return Wait(0.35f);
        MarkDefeated();
        yield return Wait(0.6f);
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
        fullLog.Add(line);
        logLines.Add(line);
        while (logLines.Count > 250) logLines.RemoveAt(0); // the screen keeps the last 250 lines (the saved log has all)
        bool atBottom = logScroll == null || logScroll.verticalNormalizedPosition <= 0.02f || logScroll.content.rect.height <= logScroll.viewport.rect.height + 1f;
        logText.text = string.Join("\n", logLines);
        if (logScroll != null && atBottom)
        {
            Canvas.ForceUpdateCanvases();
            logScroll.verticalNormalizedPosition = 0f;
        }
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
        resultBody.text = "";
        resultNotes.text = "";
        resultDropView.gameObject.SetActive(false);
        UIFactory.Clear(rewardRow);
        resultSubtitle.text = $"LOC {location}  -  Floor {floor}  |  {enemyCard.DisplayName}";

        if (victory)
        {
            SfxSynth.Play("win");
            var r = PlayerProfile.ApplyVictory(location, floor);
            resultTitle.text = r.firstClear ? "FLOOR CLEARED!" : "VICTORY!";
            resultTitle.color = UIFactory.Gold;

            var chips = new List<(ItemKind kind, int amount)> { (ItemKind.Gold, r.gold) };
            if (r.stamina > 0) chips.Add((ItemKind.Stamina, r.stamina));
            if (r.fusionOrbs > 0) chips.Add((ItemKind.FusionOrb, r.fusionOrbs));
            if (r.elementOrbs > 0) chips.Add((ItemKind.ElementOrb, r.elementOrbs));
            if (r.talentOrbs > 0) chips.Add((ItemKind.TalentOrb, r.talentOrbs));
            if (r.diamonds > 0) chips.Add((ItemKind.Diamonds, r.diamonds));
            if (r.godOrbs > 0) chips.Add((ItemKind.GodOrb, r.godOrbs));
            LayoutRewardChips(chips);

            dropTier = -1;
            if (r.droppedCard != null)
            {
                dropTier = r.droppedCard.Tier;
                dropColor = CardStats.RarityColor(r.droppedCard.rarity);
                resultDropView.gameObject.SetActive(true);
                resultDropView.ShowInstance(r.droppedCard, r.droppedCard.build);
                resultCardLabel.text = $"NEW CARD!  <color=#{ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(r.droppedCard.rarity))}>{CardStats.RarityName(r.droppedCard.rarity).ToUpperInvariant()}</color>";
                SfxSynth.Play("buff", 0.8f);
            }
            else resultCardLabel.text = $"<color=#9AA4B5>No card this time ({PlayerProfile.CardDropChance(enemyCard) * 100f:0.#}% chance)</color>";
            resultNotes.text = string.Join("\n", r.notes);
            SetupNextButton();
        }
        else
        {
            SfxSynth.Play("lose");
            int staminaBack = PlayerProfile.ApplyDefeat();
            resultTitle.text = timedOut ? "TIME UP" : "DEFEAT";
            resultTitle.color = UIFactory.Bad;
            if (staminaBack > 0) LayoutRewardChips(new List<(ItemKind, int)> { (ItemKind.Stamina, staminaBack) });
            string counter = CounterElementHint(enemyCard.element);
            resultBody.text =
                $"{enemyCard.DisplayName} was too strong.\n\n" +
                $"- {counter} (Element Orb in Collection)\n" +
                "- Reroll stats or talent, or fuse to raise rarity\n" +
                "- Save other builds to slots 2-3 and pick one on the map" +
                (staminaBack > 0 ? "\n\n<color=#7CFC6A>Returning Heroes gave you stamina back.</color>" : "");
        }

        retryButton.onClick.RemoveAllListeners();
        retryButton.onClick.AddListener(() => TryStart(location, floor));
        StartCoroutine(ResultFx(victory));
    }

    /// <summary>Result panel entrance: pop-in, spinning rays, title punch, confetti (win) or a heavy drop (loss).</summary>
    IEnumerator ResultFx(bool victory)
    {
        var layer = (RectTransform)resultPanel.transform;
        var panel = layer.Find("Panel") as RectTransform;
        if (panel != null)
        {
            UIFx.PopIn(panel, 0f, 0.32f, 0.6f);
            var rays = panel.Find("Rays") as RectTransform;
            if (rays != null) UIFx.Spin(rays, 12f);
        }
        var title = resultTitle.rectTransform;
        if (victory)
        {
            UIFx.PopScale(title, 2.8f, 0.45f, 0.12f);
            yield return new WaitForSecondsRealtime(0.28f);
            if (panel != null)
            {
                var halo = UIFx.Starburst(panel, UIFx.LocalPos(panel, title), UIFactory.WithAlpha(UIFactory.Gold, 0.55f), 900f, 1.2f, 40f);
                if (halo != null) halo.transform.SetSiblingIndex(title.GetSiblingIndex());
            }
            UIFx.Burst(layer, UIFx.LocalPos(layer, title), UIFactory.Gold, 1.3f, false);
            UIFx.Confetti(layer, 90);
            SfxSynth.Play("sparkle", 0.7f);
            if (dropTier >= 0 && resultDropView.gameObject.activeSelf && panel != null)
            {
                var drt = (RectTransform)resultDropView.transform;
                UIFx.PopIn(drt, 0.15f, 0.4f);
                yield return new WaitForSecondsRealtime(0.3f);
                var rays = UIFx.Starburst(panel, UIFx.LocalPos(panel, drt), UIFactory.WithAlpha(dropColor, 0.7f), 480f + 50f * dropTier, 1.4f, 50f);
                if (rays != null) rays.transform.SetSiblingIndex(drt.GetSiblingIndex());
                UIFx.Burst(layer, UIFx.LocalPos(layer, drt), dropColor, 0.7f + 0.1f * dropTier, false);
                if (dropTier >= 5) SfxSynth.Play("impact", 0.5f, 1.3f);
            }
        }
        else
        {
            UIFx.DropIn(title, 260f, 0.7f, 0.1f);
            UIFx.ScreenFlash(layer, new Color(0.6f, 0f, 0.05f, 0.35f), 0.5f);
            yield return new WaitForSecondsRealtime(0.55f);
            if (panel != null) UIFx.Shake(panel, 0.25f, 8f);
        }
    }

    /// <summary>Dev helper for the editor bridge ("ui:BattleScreen.DebugFx:crit"): replays one animation.
    /// Kinds: hit, crit, evade, skill, heal, intro, ko (add "2" for the enemy side, e.g. crit2).</summary>
    void DebugFx(string kind)
    {
        if (playerView.Runtime == null || enemyView.Runtime == null) return;
        resultPanel.SetActive(false);
        foreach (var v in new[] { playerView, enemyView }) { v.ResetPose(); v.SetDefeated(false); v.idle = true; }
        StartCoroutine(DebugRun(kind ?? "hit"));
    }

    IEnumerator DebugRun(string kind)
    {
        bool enemySide = kind.EndsWith("2");
        kind = kind.TrimEnd('2');
        var a = enemySide ? enemyView : playerView;
        var t = enemySide ? playerView : enemyView;
        float d = enemySide ? -1f : 1f;
        var ev = new BattleEvent { type = BattleEventType.Damage, amount = 1234, actor = a.Runtime, target = t.Runtime };
        switch (kind)
        {
            case "crit": ev.crit = true; ev.kind = DamageKind.Critical; yield return AttackRun(a, d, true); yield return DamageRun(ev, t); break;
            case "evade": yield return EvadeRun(a, t, d); break;
            case "intro": yield return IntroRun(); break;
            case "ko": t.KnockOut(d); a.Victory(); UIFx.Shake(arena, 0.35f, 14f); break;
            case "heal": UIFx.Rise(popupLayer, FxPos(a, popupLayer), new Color(0.5f, 1f, 0.65f, 0.95f), 14, 300f, 30f, 0.9f); UIFx.Ring(popupLayer, FxPos(a, popupLayer), new Color(0.45f, 1f, 0.6f, 0.6f), 120f, 520f, 0.5f); break;
            case "skill":
                UIFx.Starburst(backLayer, FxPos(a, backLayer), UIFactory.WithAlpha(SideColor(a), 0.55f), 900f, 0.9f, 60f);
                UIFx.GlowFlash(backLayer, FxPos(a, backLayer), UIFactory.WithAlpha(SideColor(a), 0.6f), 760f, 0.6f);
                UIFx.Ring(popupLayer, FxPos(a, popupLayer), new Color(0.6f, 0.9f, 1f, 0.85f), 100f, 640f, 0.55f);
                UIFx.Rise(popupLayer, FxPos(a, popupLayer), new Color(0.7f, 0.92f, 1f, 1f), 16, 360f, 32f, 0.9f);
                a.Pulse(0.6f, 0.08f);
                ShowBanner("SKILL!", SideColor(a));
                break;
            default: ev.kind = DamageKind.Normal; yield return AttackRun(a, d, false); yield return DamageRun(ev, t); break;
        }
    }

    IEnumerator ChipSparkle(RectTransform chip, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (chip == null) yield break;
        var layer = (RectTransform)resultPanel.transform;
        UIFx.Sparks(layer, UIFx.LocalPos(layer, chip), UIFactory.Gold, 10, 420f, 24f, 0.5f, 300f);
        SfxSynth.Play("coin", 0.35f, 1f + delay * 0.3f);
    }

    void LayoutRewardChips(List<(ItemKind kind, int amount)> chips)
    {
        const float size = 150f, gap = 22f;
        float total = chips.Count * size + (chips.Count - 1) * gap;
        for (int i = 0; i < chips.Count; i++)
        {
            var chip = ItemInfo.Chip(rewardRow, chips[i].kind, $"+{chips[i].amount:N0}", size);
            chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0.5f);
            chip.pivot = new Vector2(0.5f, 0.5f);
            chip.anchoredPosition = new Vector2(-total / 2f + size / 2f + i * (size + gap), 0f);
            StartCoroutine(PopIn(chip, 0.3f + 0.08f * i));
            StartCoroutine(ChipSparkle(chip, 0.45f + 0.08f * i));
        }
    }

    IEnumerator PopIn(RectTransform rt, float delay)
    {
        rt.localScale = Vector3.zero;
        float t = -delay;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.25f;
            float k = Mathf.Clamp01(t);
            rt.localScale = Vector3.one * (k < 0.7f ? Mathf.Lerp(0f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f));
            yield return null;
        }
        rt.localScale = Vector3.one;
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

    static Text TalentPanel(RectTransform canvas, string name, float x, Color accent)
    {
        var panel = UIFactory.CreatePanel(canvas, name, new Color(0.04f, 0.04f, 0.09f, 0.92f), false);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(x, 12), new Vector2(CardView.Width + 40, 232));
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(accent, 0.55f), 2f);
        var title = UIFactory.CreateText(panel.transform, "Title", "TALENTS", 15, UIFactory.WithAlpha(accent, 1f), TextAnchor.UpperLeft, FontStyle.Bold);
        UIFactory.TopStrip(title.rectTransform, 6, 20, 14, 14);
        var text = UIFactory.CreateText(panel.transform, "Text", "", 19, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.Stretch(text.rectTransform, 14, 26, 14, 8);
        UIFactory.BestFit(text, 10);
        return text;
    }

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
        UIFactory.Place((RectTransform)screen.playerView.transform, new Vector2(0.5f, 0.5f), new Vector2(-500, 36), new Vector2(CardView.Width, CardView.Height));
        var youLabel = UIFactory.CreateText(screen.playerView.transform, "YouLabel", "YOU", 26, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(youLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 40), new Vector2(200, 36));

        screen.enemyView = CardView.Build(canvas, "EnemyCard");
        UIFactory.Place((RectTransform)screen.enemyView.transform, new Vector2(0.5f, 0.5f), new Vector2(500, 36), new Vector2(CardView.Width, CardView.Height));
        var enemyLabel = UIFactory.CreateText(screen.enemyView.transform, "EnemyLabel", "ENEMY", 26, UIFactory.Accent2, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(enemyLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 40), new Vector2(200, 36));

        // Talent panels under each card (always visible: name, type and full description)
        screen.playerTalentText = TalentPanel(canvas, "PlayerTalents", -500, UIFactory.Accent);
        screen.enemyTalentText = TalentPanel(canvas, "EnemyTalents", 500, UIFactory.Accent2);

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
        var logHint = UIFactory.CreateText(logPanel.transform, "Hint", "scroll for more", 15, UIFactory.TextDim, TextAnchor.UpperRight, FontStyle.Italic);
        UIFactory.TopStrip(logHint.rectTransform, 12, 22, 16, 16);
        screen.logScroll = UIFactory.CreateScroll(logPanel.transform, "LogScroll", true, out RectTransform logContent);
        UIFactory.Stretch((RectTransform)screen.logScroll.transform, 12, 40, 12, 10);
        UIFactory.AddVertical(logContent, 0f, 4);
        screen.logText = UIFactory.CreateText(logContent, "Log", "", 19, UIFactory.TextMain, TextAnchor.UpperLeft);
        screen.logText.verticalOverflow = VerticalWrapMode.Overflow;
        screen.logScroll.scrollSensitivity = 30f;

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
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.06f, 0.05f, 0.13f, 0.98f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240, 880));
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(UIFactory.Gold, 0.6f), 3f);
        var resultTheme = UIFactory.Theme;
        if (resultTheme != null && resultTheme.lightRays != null)
        {
            var rays = UIFactory.CreateImage(panel.transform, "Rays", new Color(1f, 0.9f, 0.6f, 0.08f), resultTheme.lightRays);
            UIFactory.Place(rays.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 300), new Vector2(1400, 1400));
            rays.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
        screen.resultTitle = UIFactory.CreateText(panel.transform, "Title", "VICTORY!", 80, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(screen.resultTitle.rectTransform, 20, 100, 20, 20);
        UIFactory.AddOutline(screen.resultTitle, new Color(0.35f, 0.15f, 0.05f, 0.9f), 3f);
        UIFactory.AddShadow(screen.resultTitle, UIFactory.ShadowColor, 5f);
        screen.resultSubtitle = UIFactory.CreateText(panel.transform, "Subtitle", "", 24, UIFactory.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(screen.resultSubtitle.rectTransform, 118, 34, 20, 20);

        screen.rewardRow = UIFactory.CreateRect("Rewards", panel.transform);
        UIFactory.TopStrip(screen.rewardRow, 160, 186, 20, 20);

        screen.resultCardLabel = UIFactory.CreateText(panel.transform, "NewCardLabel", "", 28, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(screen.resultCardLabel.rectTransform, 356, 40, 20, 20);
        UIFactory.AddShadow(screen.resultCardLabel, UIFactory.ShadowColor, 2f);
        screen.resultDropView = CardView.Build(panel.transform, "DropCard");
        var drt = (RectTransform)screen.resultDropView.transform;
        UIFactory.Place(drt, new Vector2(0.5f, 1f), new Vector2(0, -400), new Vector2(CardView.Width, CardView.Height));
        drt.localScale = Vector3.one * 0.42f;
        screen.resultDropView.gameObject.SetActive(false);

        screen.resultBody = UIFactory.CreateText(panel.transform, "Body", "", 26, UIFactory.TextMain, TextAnchor.UpperCenter);
        UIFactory.TopStrip(screen.resultBody.rectTransform, 360, 300, 120, 120);
        screen.resultNotes = UIFactory.CreateText(panel.transform, "Notes", "", 22, new Color(0.55f, 1f, 0.5f), TextAnchor.UpperCenter, FontStyle.Bold);
        UIFactory.TopStrip(screen.resultNotes.rectTransform, 668, 70, 40, 40);
        UIFactory.BestFit(screen.resultNotes, 12);
        screen.resultCardArt = UIFactory.CreateImage(panel.transform, "NewCardArt", Color.white);
        screen.resultCardArt.gameObject.SetActive(false);
        screen.resultHint = UIFactory.CreateText(panel.transform, "Hint", "", 22, UIFactory.TextMain);
        UIFactory.TopStrip(screen.resultHint.rectTransform, 736, 30, 20, 20);

        screen.mapButton = UIFactory.CreateButton(panel.transform, "MapButton", "MAP", UIFactory.PanelLight, 30);
        UIFactory.Place((RectTransform)screen.mapButton.transform, new Vector2(0.5f, 0f), new Vector2(-300, 40), new Vector2(260, 80));
        screen.retryButton = UIFactory.CreateButton(panel.transform, "RetryButton", "RETRY", UIFactory.Accent2, 30);
        UIFactory.Place((RectTransform)screen.retryButton.transform, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(260, 80));
        screen.saveLogButton = UIFactory.CreateButton(panel.transform, "SaveLog", "SAVE BATTLE LOG", new Color(0.25f, 0.3f, 0.45f), 18);
        UIFactory.Place((RectTransform)screen.saveLogButton.transform, new Vector2(1f, 1f), new Vector2(-24, -24), new Vector2(220, 46));
        screen.nextButton = UIFactory.CreateButton(panel.transform, "NextButton", "NEXT FLOOR >", UIFactory.Accent, 30);
        UIFactory.Place((RectTransform)screen.nextButton.transform, new Vector2(0.5f, 0f), new Vector2(300, 40), new Vector2(260, 80));

        dim.gameObject.SetActive(false);
        return screen;
    }
}
