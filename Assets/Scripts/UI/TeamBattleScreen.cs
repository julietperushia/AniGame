using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3v3 battle of the Hidden Location. Each side is one team (shared HP, ATK, DEF, mana pool and statuses) of 3 cards
/// that act on their own SPD. The engine runs the battle; this screen replays its events. Built at runtime.
/// </summary>
public class TeamBattleScreen : MonoBehaviour
{
    static readonly int[] Speeds = { 1, 2, 4 };

    RectTransform root, arena, fxLayer;
    StatBar playerHp, playerMana, enemyHp, enemyMana;
    Text title, roundText, orderText, playerStatus, enemyStatus, logText;
    Button speedButton, skipButton;
    GameObject resultPanel;
    Text resultTitle, resultBody;
    Button nextButton, retryButton, mapButton;
    readonly List<string> logLines = new List<string>();
    readonly Dictionary<TeamMember, CardView> views = new Dictionary<TeamMember, CardView>();
    readonly List<CardView> playerViews = new List<CardView>(), enemyViews = new List<CardView>();
    CardRuntime player, enemy;
    int location, floor, speedIndex;
    bool skipping, finished;

    float Speed => Speeds[Mathf.Clamp(speedIndex, 0, Speeds.Length - 1)] * 0.6f;

    public static void BuildLayout(RectTransform canvas)
    {
        canvas.gameObject.AddComponent<TeamBattleScreen>();
    }

    void Start()
    {
        root = (RectTransform)transform;
        if (GameSession.HasPendingHiddenBattle)
        {
            location = GameSession.PendingHiddenLocation;
            floor = GameSession.PendingHiddenFloor;
            GameSession.HasPendingHiddenBattle = false;
        }
        else
        {
            var next = PlayerProfile.NextHiddenFloor(); // scene opened directly in the editor
            location = next.loc;
            floor = next.floor;
        }
        var db = CardDatabase.Instance;
        var loc = db != null ? db.GetHidden(location) : null;
        Color tint = loc != null ? CardStats.RarityColor(loc.rarity) : UIFactory.Accent;
        UIFactory.CreateBackground(root, UIFactory.WithAlpha(Color.Lerp(tint, Color.black, 0.45f), 0.85f), UIFactory.BgDark, out _);
        arena = UIFactory.CreateRect("Arena", root);
        UIFactory.Stretch(arena);
        BuildHud(loc);

        speedIndex = Mathf.Max(0, System.Array.IndexOf(Speeds, PlayerProfile.Data.battleSpeed));
        UpdateSpeed();
        SfxSynth.Volume = PlayerProfile.Data.sfxVolume;

        player = PlayerProfile.HiddenTeamRuntime();
        enemy = PlayerProfile.HiddenEnemyRuntime(location, floor);
        if (player == null || enemy == null)
        {
            ShowResult(false, player == null ? (PlayerProfile.HiddenTeamProblem() ?? "Your team is not ready.") : "This floor could not be loaded.", null, true);
            return;
        }
        BuildTeam(player, playerViews, true);
        BuildTeam(enemy, enemyViews, false);
        StartCoroutine(RunBattle());
    }

    // ---------------------------------------------------------------- layout

    void BuildHud(CardDatabase.HiddenLocationData loc)
    {
        var bar = UIFactory.CreateTopBar(root, "", out Button back, out title, out Text info);
        info.gameObject.SetActive(false);
        back.onClick.AddListener(() => GameSession.LoadScene(SceneNames.HiddenLocation));
        title.text = $"HIDDEN LOC {location}  -  FLOOR {floor}/{GameRules.HiddenFloorsPerLocation}" + (loc != null ? $"   <size=24><color=#C9CEDA>{loc.anime}</color></size>" : "");
        title.rectTransform.sizeDelta = new Vector2(1100, 80);
        UIFactory.BestFit(title, 20);

        speedButton = UIFactory.CreateButton(bar, "Speed", "SPEED x1", UIFactory.PanelLight, 22);
        UIFactory.Place((RectTransform)speedButton.transform, new Vector2(1f, 0.5f), new Vector2(-330, 0), new Vector2(180, 60));
        speedButton.onClick.AddListener(() => { speedIndex = (speedIndex + 1) % Speeds.Length; PlayerProfile.Data.battleSpeed = Speeds[speedIndex]; PlayerProfile.Save(); UpdateSpeed(); });
        skipButton = UIFactory.CreateButton(bar, "Skip", "SKIP", new Color(0.6f, 0.25f, 0.35f), 22);
        UIFactory.Place((RectTransform)skipButton.transform, new Vector2(1f, 0.5f), new Vector2(-130, 0), new Vector2(180, 60));
        skipButton.onClick.AddListener(() => skipping = true);

        roundText = UIFactory.CreateText(root, "Round", "ROUND 1", 34, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(roundText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(400, 50));
        orderText = UIFactory.CreateText(root, "Order", "", 18, UIFactory.TextDim, TextAnchor.MiddleCenter);
        UIFactory.Place(orderText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(1800, 34));
        UIFactory.BestFit(orderText, 12);

        playerHp = TeamBar("PlayerHP", new Vector2(-480, -220), UIFactory.HpColor, 20);
        playerMana = TeamBar("PlayerMana", new Vector2(-480, -256), UIFactory.ManaColor, 15, 24);
        enemyHp = TeamBar("EnemyHP", new Vector2(480, -220), UIFactory.HpColor, 20);
        enemyMana = TeamBar("EnemyMana", new Vector2(480, -256), UIFactory.ManaColor, 15, 24);
        var pl = UIFactory.CreateText(root, "PlayerLabel", "YOUR TEAM", 24, new Color(0.55f, 0.85f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Place(pl.rectTransform, new Vector2(0.5f, 1f), new Vector2(-480, -188), new Vector2(820, 30));
        var el = UIFactory.CreateText(root, "EnemyLabel", "ENEMY TEAM", 24, new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.Place(el.rectTransform, new Vector2(0.5f, 1f), new Vector2(480, -188), new Vector2(820, 30));
        playerStatus = UIFactory.CreateText(root, "PlayerStatus", "", 16, UIFactory.TextMain, TextAnchor.MiddleLeft);
        UIFactory.Place(playerStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(-480, -288), new Vector2(820, 28));
        enemyStatus = UIFactory.CreateText(root, "EnemyStatus", "", 16, UIFactory.TextMain, TextAnchor.MiddleRight);
        UIFactory.Place(enemyStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(480, -288), new Vector2(820, 28));

        var logBox = UIFactory.CreatePanel(root, "LogBox", new Color(0.03f, 0.03f, 0.07f, 0.85f), true);
        UIFactory.Place(logBox.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(1860, 200));
        logText = UIFactory.CreateText(logBox.transform, "Log", "", 17, UIFactory.TextMain, TextAnchor.LowerLeft);
        UIFactory.Stretch(logText.rectTransform, 16, 8, 16, 8);
        logText.verticalOverflow = VerticalWrapMode.Truncate;

        fxLayer = UIFactory.CreateRect("Fx", root);
        UIFactory.Stretch(fxLayer);
    }

    StatBar TeamBar(string name, Vector2 pos, Color color, int labelSize, float height = 32)
    {
        var b = StatBar.Build(root, name, color, labelSize);
        UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), pos, new Vector2(820, height));
        return b;
    }

    void BuildTeam(CardRuntime team, List<CardView> list, bool isPlayer)
    {
        for (int i = 0; i < team.members.Count; i++)
        {
            var m = team.members[i];
            var view = CardView.Build(arena, (isPlayer ? "P" : "E") + i);
            var rt = (RectTransform)view.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one * 0.6f;
            float x = (isPlayer ? -480f : 480f) + (i - 1) * 272f;
            rt.anchoredPosition = new Vector2(x, 0f);
            view.ShowMember(m, team);
            if (view.hpBar != null) view.hpBar.gameObject.SetActive(false);   // HP and mana are the team's: shown above
            if (view.manaBar != null) view.manaBar.gameObject.SetActive(false);
            view.idle = true;
            list.Add(view);
            views[m] = view;
        }
    }

    void UpdateSpeed()
    {
        var label = UIFactory.ButtonLabel(speedButton);
        if (label != null) label.text = $"SPEED x{Speeds[speedIndex]}";
        DamagePopup.SpeedMultiplier = Speed;
        UIFx.Speed = Speed;
    }

    void OnDestroy()
    {
        UIFx.Speed = 1f;
        DamagePopup.SpeedMultiplier = 1f;
    }

    // ---------------------------------------------------------------- battle

    IEnumerator RunBattle()
    {
        var engine = new BattleEngine(player, enemy, System.Environment.TickCount);
        engine.Begin();
        ApplySnapshot(engine.Events.Count > 0 ? engine.Events[0] : null);
        int processed = 0, guard = 0;
        while (guard++ < 20000)
        {
            while (processed < engine.Events.Count)
            {
                var e = engine.Events[processed++];
                if (skipping) PlayInstant(e);
                else yield return PlayEvent(e, engine);
            }
            if (engine.IsOver) break;
            engine.Step();
        }
        finished = true;
        foreach (var v in playerViews.Concat(enemyViews)) { v.ResetPose(); v.RefreshLive(); }
        var state = engine.State;
        bool victory = state == BattleState.PlayerWon || (state == BattleState.Draw && !enemy.IsAlive);
        foreach (var v in victory ? enemyViews : playerViews) v.SetDefeated(true);
        if (victory)
        {
            foreach (var v in playerViews) v.Victory();
            var rewards = PlayerProfile.ApplyHiddenVictory(location, floor);
            ShowResult(true, $"You beat Hidden LOC {location} floor {floor} in {engine.Round} rounds.", rewards, false);
        }
        else
        {
            int back = PlayerProfile.ApplyDefeat();
            string why = state == BattleState.Draw ? $"Time is up after {engine.Round} rounds." : $"Your team fell in round {engine.Round}.";
            ShowResult(false, why + (back > 0 ? $"\nReturning Heroes: +{back} stamina." : "") + "\nTry other builds in your 3 slots: SPD decides who acts first, and talents work for the whole team.", null, false);
        }
    }

    float Wait(float s) => s / Mathf.Max(0.1f, Speed);

    CardView ViewOf(BattleEvent e) => e.member != null && views.TryGetValue(e.member, out var v) ? v : null;

    List<CardView> SideOf(CardRuntime team) => team == player ? playerViews : enemyViews;

    void ApplySnapshot(BattleEvent e)
    {
        if (e == null) return;
        playerHp.Set(Mathf.Max(0, e.playerHp), Mathf.Max(1, e.playerMaxHp), Mathf.Max(0, e.playerShield), skipping);
        enemyHp.Set(Mathf.Max(0, e.enemyHp), Mathf.Max(1, e.enemyMaxHp), Mathf.Max(0, e.enemyShield), skipping);
        playerMana.Set(Mathf.Max(0, e.playerMana), Mathf.Max(1, e.playerMaxMana), skipping);
        enemyMana.Set(Mathf.Max(0, e.enemyMana), Mathf.Max(1, e.enemyMaxMana), skipping);
        float p = e.playerMaxHp > 0 ? (float)e.playerHp / e.playerMaxHp : 0f, q = e.enemyMaxHp > 0 ? (float)e.enemyHp / e.enemyMaxHp : 0f;
        playerHp.SetColor(p > 0.5f ? UIFactory.HpColor : p > 0.25f ? new Color(1f, 0.8f, 0.25f) : UIFactory.Bad);
        enemyHp.SetColor(q > 0.5f ? UIFactory.HpColor : q > 0.25f ? new Color(1f, 0.8f, 0.25f) : UIFactory.Bad);
    }

    void RefreshTeams()
    {
        foreach (var v in playerViews) v.RefreshLive();
        foreach (var v in enemyViews) v.RefreshLive();
        string ps = player.StatusSummary(), es = enemy.StatusSummary();
        playerStatus.text = string.IsNullOrEmpty(ps) ? "" : ps;
        enemyStatus.text = string.IsNullOrEmpty(es) ? "" : es;
    }

    void Log(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        logLines.Add(line);
        if (logLines.Count > 9) logLines.RemoveAt(0);
        logText.text = string.Join("\n", logLines);
    }

    void PlayInstant(BattleEvent e)
    {
        if (e.type != BattleEventType.TurnStart) Log(e.text);
        ApplySnapshot(e);
        if (e.type == BattleEventType.RoundStart) roundText.text = $"ROUND {e.round}";
        if (e.type == BattleEventType.RoundStart || e.type == BattleEventType.BattleEnd) RefreshTeams();
    }

    Vector2 PopupPos(CardRuntime team, CardView view)
    {
        var target = view != null ? (RectTransform)view.transform : null;
        if (target == null)
        {
            var side = SideOf(team);
            target = side.Count > 1 ? (RectTransform)side[1].transform : null;
        }
        if (target == null) return Vector2.zero;
        Vector3 world = target.TransformPoint(new Vector3(0, CardView.Height * 0.2f, 0));
        return fxLayer.InverseTransformPoint(world);
    }

    IEnumerator PlayEvent(BattleEvent e, BattleEngine engine)
    {
        ApplySnapshot(e);
        var actorView = ViewOf(e);
        float dir = e.actor == player ? 1f : -1f;
        switch (e.type)
        {
            case BattleEventType.RoundStart:
                Log(e.text);
                roundText.text = $"ROUND {e.round}";
                orderText.text = "ORDER: " + string.Join("  >  ", engine.TeamTurnOrder().Select(m => $"{(player.members.Contains(m) ? "<color=#8FD3FF>" : "<color=#FF9E9E>")}{m.name} {m.Speed(player.members.Contains(m) ? player : enemy)}</color>"));
                RefreshTeams();
                yield return new WaitForSeconds(Wait(0.35f));
                break;
            case BattleEventType.TurnStart:
                if (actorView != null) { actorView.Pulse(Wait(0.3f), 0.08f); actorView.Flash(new Color(1f, 1f, 1f, 0.35f), Wait(0.25f)); }
                yield return new WaitForSeconds(Wait(0.18f));
                break;
            case BattleEventType.SkillActivated:
                Log(e.text);
                if (actorView != null) actorView.Flash(new Color(0.45f, 0.85f, 1f, 0.7f), Wait(0.5f));
                DamagePopup.Spawn(fxLayer, PopupPos(e.actor, actorView), e.skill != null ? e.skill.DisplayName.ToUpperInvariant() : "SKILL", new Color(0.55f, 0.9f, 1f), 34, 1.2f);
                SfxSynth.Play("skill", 0.6f);
                yield return new WaitForSeconds(Wait(0.55f));
                RefreshTeams();
                break;
            case BattleEventType.NormalAttack:
                Log(e.text);
                if (actorView != null) actorView.Lunge(dir, Wait(0.3f));
                SfxSynth.Play("impact", e.crit ? 0.8f : 0.5f);
                yield return new WaitForSeconds(Wait(0.22f));
                break;
            case BattleEventType.Damage:
            {
                Log(e.text);
                bool crit = e.kind == DamageKind.Critical;
                var side = SideOf(e.target);
                foreach (var v in side) v.Shake(Wait(0.2f), crit ? 12f : 6f);
                DamagePopup.Spawn(fxLayer, PopupPos(e.target, null) + new Vector2(Random.Range(-60f, 60f), 0), (crit ? "CRIT " : "") + e.amount.ToString("N0"),
                                  crit ? new Color(1f, 0.75f, 0.2f) : e.kind == DamageKind.True ? new Color(0.8f, 0.6f, 1f) : e.kind == DamageKind.Burn ? new Color(1f, 0.55f, 0.2f) : e.kind == DamageKind.Poison ? new Color(0.5f, 1f, 0.4f) : Color.white,
                                  crit ? 46 : 36);
                yield return new WaitForSeconds(Wait(0.2f));
                break;
            }
            case BattleEventType.Heal:
                Log(e.text);
                DamagePopup.Spawn(fxLayer, PopupPos(e.target, null) + new Vector2(0, 40), "+" + e.amount.ToString("N0"), new Color(0.4f, 1f, 0.6f), 32);
                yield return new WaitForSeconds(Wait(0.14f));
                break;
            case BattleEventType.Evaded:
                Log(e.text);
                DamagePopup.Spawn(fxLayer, PopupPos(e.target, null), e.miss ? "MISS" : "EVADE", new Color(0.7f, 0.85f, 1f), 34);
                SfxSynth.Play("miss", 0.5f);
                yield return new WaitForSeconds(Wait(0.25f));
                break;
            case BattleEventType.TurnSkipped:
                Log(e.text);
                if (actorView != null) actorView.Shake(Wait(0.3f), 8f);
                DamagePopup.Spawn(fxLayer, PopupPos(e.actor, actorView), "SKIP", new Color(1f, 0.9f, 0.3f), 32);
                yield return new WaitForSeconds(Wait(0.35f));
                break;
            case BattleEventType.BattleEnd:
                Log(e.text);
                RefreshTeams();
                yield return new WaitForSeconds(Wait(0.4f));
                break;
            default:
                Log(e.text);
                if (e.type == BattleEventType.Status || e.type == BattleEventType.PassiveTriggered) RefreshTeams();
                yield return new WaitForSeconds(Wait(e.type == BattleEventType.BattleStart ? 0.4f : 0.08f));
                break;
        }
    }

    // ---------------------------------------------------------------- result

    void ShowResult(bool victory, string body, BattleRewards rewards, bool fatal)
    {
        finished = true;
        if (resultPanel == null)
        {
            var dim = UIFactory.CreateImage(root, "Result", new Color(0, 0, 0, 0.7f), null, false, true);
            UIFactory.Stretch(dim.rectTransform);
            resultPanel = dim.gameObject;
            var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.08f, 0.07f, 0.14f, 0.98f), true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 560));
            UIFactory.AddOutline(panel, new Color(0.75f, 0.5f, 1f, 0.8f), 3f);
            resultTitle = UIFactory.CreateText(panel.transform, "Title", "", 60, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.TopStrip(resultTitle.rectTransform, 24, 80, 20, 20);
            resultBody = UIFactory.CreateText(panel.transform, "Body", "", 24, UIFactory.TextMain, TextAnchor.UpperCenter);
            UIFactory.TopStrip(resultBody.rectTransform, 120, 300, 40, 40);
            resultBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            nextButton = UIFactory.CreateButton(panel.transform, "Next", "NEXT FLOOR", new Color(1f, 0.35f, 0.62f), 26);
            UIFactory.Place((RectTransform)nextButton.transform, new Vector2(0.5f, 0f), new Vector2(-290, 30), new Vector2(260, 70));
            retryButton = UIFactory.CreateButton(panel.transform, "Retry", "RETRY", UIFactory.Accent, 26);
            UIFactory.Place((RectTransform)retryButton.transform, new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(260, 70));
            mapButton = UIFactory.CreateButton(panel.transform, "Map", "MAP", UIFactory.PanelLight, 26);
            UIFactory.Place((RectTransform)mapButton.transform, new Vector2(0.5f, 0f), new Vector2(290, 30), new Vector2(260, 70));
            mapButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.HiddenLocation));
        }
        resultPanel.SetActive(true);
        resultPanel.transform.SetAsLastSibling();
        resultTitle.text = fatal ? "CAN'T START" : victory ? "VICTORY!" : "DEFEAT";
        resultTitle.color = fatal ? UIFactory.Bad : victory ? UIFactory.Gold : new Color(1f, 0.45f, 0.45f);
        var sb = new System.Text.StringBuilder(body);
        if (rewards != null)
        {
            sb.Append("\n\n");
            foreach (var l in rewards.lines) sb.Append("<color=#FFD54F>").Append(l).Append("</color>\n");
            foreach (var n in rewards.notes) sb.Append("<color=#C9A2FF>").Append(n).Append("</color>\n");
        }
        sb.Append($"\n<size=18><color=#9AA4B5>Stamina {PlayerProfile.Data.stamina}/{PlayerProfile.MaxStamina} (each fight costs {PlayerProfile.StaminaCost})</color></size>");
        resultBody.text = sb.ToString();

        int nextLoc = floor < GameRules.HiddenFloorsPerLocation ? location : location + 1;
        int nextFloor = floor < GameRules.HiddenFloorsPerLocation ? floor + 1 : 1;
        bool hasNext = victory && !fatal && nextLoc <= GameRules.HiddenLocationCount && CardDatabase.Instance != null && CardDatabase.Instance.GetHidden(nextLoc) != null;
        nextButton.gameObject.SetActive(hasNext);
        retryButton.gameObject.SetActive(!fatal);
        nextButton.onClick.RemoveAllListeners();
        retryButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(() => TryStart(nextLoc, nextFloor));
        retryButton.onClick.AddListener(() => TryStart(location, floor));
        SfxSynth.Play(victory ? "buff" : "lose", 0.7f);
    }

    void TryStart(int loc, int fl)
    {
        string problem = PlayerProfile.HiddenTeamProblem();
        if (problem != null) { resultBody.text = problem; return; }
        if (!PlayerProfile.HiddenTestMode && !PlayerProfile.TrySpendStamina(PlayerProfile.StaminaCost))
        {
            resultBody.text = $"Not enough stamina: each fight costs {PlayerProfile.StaminaCost} (you have {PlayerProfile.Data.stamina}).";
            SfxSynth.Play("fail", 0.6f);
            return;
        }
        GameSession.StartHiddenBattle(loc, fl);
    }

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || finished) return;
        if (kb.spaceKey.wasPressedThisFrame) skipping = true;
    }
}
