using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BOSS: one location with many gates, each a boss (a character from the game with boss stats) that fights the player's
/// 3 build slots alone and acts several times a round. Gates open in order. Unlocked with 1 God card.
/// Bosses and rewards are edited in Test Mode > BOSS EDITOR. Built at runtime.
/// </summary>
public class BossScreen : MonoBehaviour
{
    RectTransform root, listContent, detail, teamRow;
    Text teamText, message;
    readonly List<Button> gateButtons = new List<Button>();
    int selected = 1;

    public static void BuildLayout(RectTransform canvas) => canvas.gameObject.AddComponent<BossScreen>();

    void Start()
    {
        root = (RectTransform)transform;
        BossConfig.Reload(); // pick up edits made in the boss editor
        UIFactory.CreateBackground(root, new Color(0.45f, 0.06f, 0.1f, 0.9f), UIFactory.BgDark, out _);
        UIFactory.CreateTopBarWithCurrency(root, "BOSS", out Button back, out _);
        back.onClick.AddListener(() => GameSession.LoadScene(SceneNames.MainMenu));

        var cfg = BossConfig.Current;
        if (!PlayerProfile.BossUnlocked)
        {
            Center($"The Boss gates are sealed.\nOwn {PlayerProfile.BossUnlockGodCards} God card to enter (you have {PlayerProfile.GodCards}).", new Color(1f, 0.7f, 0.7f));
            return;
        }
        if (cfg.gates.Count == 0)
        {
            Center("No bosses yet.", new Color(1f, 0.7f, 0.7f));
            return;
        }

        var listBox = UIFactory.CreatePanel(root, "GateList", new Color(0.08f, 0.02f, 0.04f, 0.9f), true);
        UIFactory.TopLeft(listBox.rectTransform, 30, 120, 460, 930);
        var scroll = UIFactory.CreateScroll(listBox.transform, "Scroll", true, out listContent);
        UIFactory.Stretch((RectTransform)scroll.transform, 10, 10, 10, 10);
        var v = UIFactory.AddVertical(listContent, 6, 0);
        v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;
        for (int i = 1; i <= cfg.gates.Count; i++)
        {
            int n = i;
            var b = UIFactory.CreateButton(listContent, $"Gate{n}", "", UIFactory.PanelLight, 18);
            UIFactory.SetLayoutSize(b, 0, 70);
            var label = UIFactory.ButtonLabel(b);
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(16, 0);
            b.onClick.AddListener(() => Select(n));
            gateButtons.Add(b);
        }

        detail = UIFactory.CreateRect("Detail", root);
        UIFactory.TopLeft(detail, 520, 116, 1370, 590);

        var teamBox = UIFactory.CreatePanel(root, "TeamBox", new Color(0.08f, 0.02f, 0.04f, 0.9f), true);
        UIFactory.TopLeft(teamBox.rectTransform, 520, 720, 1370, 330);
        var teamTitle = UIFactory.CreateText(teamBox.transform, "TeamTitle", "YOUR TEAM  (build slots 1-3)", 24, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(teamTitle.rectTransform, 24, 12, 700, 40);
        teamRow = UIFactory.CreateRect("TeamRow", teamBox.transform);
        UIFactory.TopLeft(teamRow, 24, 60, 520, 250);
        teamText = UIFactory.CreateText(teamBox.transform, "TeamText", "", 20, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopLeft(teamText.rectTransform, 580, 60, 760, 200);
        teamText.horizontalOverflow = HorizontalWrapMode.Wrap;
        message = UIFactory.CreateText(teamBox.transform, "Message", "", 20, new Color(1f, 0.75f, 0.45f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(message.rectTransform, 580, 268, 760, 44);
        var editTeam = UIFactory.CreateButton(teamBox.transform, "EditTeam", "CHANGE TEAM", UIFactory.Accent, 20);
        UIFactory.TopLeft((RectTransform)editTeam.transform, 1110, 12, 236, 44);
        editTeam.onClick.AddListener(() => GameSession.LoadScene(SceneNames.Collection));

        BuildTeam();
        int start = GameSession.SelectedBossGate > 0 && GameSession.SelectedBossGate <= cfg.gates.Count ? GameSession.SelectedBossGate : PlayerProfile.NextBossGate;
        Select(start);
    }

    void Center(string text, Color color)
    {
        var t = UIFactory.CreateText(root, "Info", text, 32, color, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(t.rectTransform, 100, 200, 100, 200);
    }

    void RefreshList()
    {
        var cfg = BossConfig.Current;
        for (int i = 0; i < gateButtons.Count && i < cfg.gates.Count; i++)
        {
            int n = i + 1;
            var g = cfg.gates[i];
            bool cleared = PlayerProfile.IsBossCleared(n), open = PlayerProfile.IsBossOpen(n);
            string rc = ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(g.rarity));
            string state = cleared ? "<color=#FFD54F>DEFEATED</color>" : open ? "<color=#FF9E9E>NEXT</color>" : "<color=#6A6F80>SEALED</color>";
            UIFactory.ButtonLabel(gateButtons[i]).text = open
                ? $"<b>GATE {n}</b>  {state}  <size=15><color=#{rc}>{CardStats.RarityName(g.rarity).ToUpperInvariant()}</color></size>\n<size=17>{g.name}</size>"
                : $"<color=#6A6F80><b>GATE {n}</b>  SEALED\n<size=17>???</size></color>";
            gateButtons[i].image.color = n == selected ? new Color(0.6f, 0.15f, 0.22f) : open ? UIFactory.PanelLight : new Color(0.1f, 0.1f, 0.14f);
        }
    }

    void Select(int n)
    {
        var cfg = BossConfig.Current;
        var g = cfg.Gate(n);
        if (g == null) return;
        selected = n;
        GameSession.SelectedBossGate = n;
        RefreshList();
        UIFactory.Clear(detail);
        bool open = PlayerProfile.IsBossOpen(n), cleared = PlayerProfile.IsBossCleared(n);
        var m = PlayerProfile.BossMember(g);

        var tile = CardTile.Build(detail, "BossTile", 330f, 460f, true);
        UIFactory.TopLeft((RectTransform)tile.transform, 0, 10, 330, 460);
        var card = m.art;
        tile.Set(card, g.rarity, g.element, CardStats.RarityName(g.rarity), $"x{m.actionsPerRound}");
        var db = CardDatabase.Instance;
        tile.SetTalent(db != null ? db.FindTalent(g.talent) : null, db != null ? db.FindTalent(g.talent2) : null);
        if (!string.IsNullOrWhiteSpace(g.name)) tile.nameText.text = g.name;
        if (!open) tile.SetLocked(true, "SEALED");

        var head = UIFactory.CreateText(detail, "Head", $"GATE {n}  -  {(open ? g.name : "???")}", 40, Color.Lerp(CardStats.RarityColor(g.rarity), Color.white, 0.3f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(head.rectTransform, 360, 4, 1000, 56);
        UIFactory.BestFit(head, 20);

        var sb = new StringBuilder();
        if (!open) sb.Append("Defeat the boss of the gate before to break this seal.\n\n");
        else
        {
            sb.Append($"{CardStats.RarityName(g.rarity)}  -  {ElementSystem.DisplayName(g.element)}  -  acts <b>{m.actionsPerRound}x</b> per round\n");
            sb.Append($"<b>HP {m.hp:N0}   ATK {m.atk}   DEF {m.def}   SPD {m.baseSpeed}</b>   <color=#9AA4B5>crit {m.critRate}%, evasion {m.evasion}</color>\n");
            sb.Append("<size=17><color=#9AA4B5>The boss fights your whole team alone: it has a team's worth of HP, ATK and DEF and takes several turns each round.</color></size>\n\n");
            foreach (var t in m.talents)
                if (t != null) sb.Append($"<color=#8FD3FF><b>{t.DisplayName}</b></color>  <size=17>{t.DescriptionFor(g.rarity)}</size>\n");
        }
        sb.Append($"\n<color=#FFD54F><b>{(cleared ? "Replay" : "First clear")}:</b> {RewardText(g, cleared ? cfg.replayRewardPercent : 100)}</color>");
        if (!cleared) sb.Append($"\n<size=17><color=#9AA4B5>Replays: {cfg.replayRewardPercent}% of it.</color></size>");
        var info = UIFactory.CreateText(detail, "Info", sb.ToString(), 20, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopLeft(info.rectTransform, 360, 66, 1000, 430);
        info.horizontalOverflow = HorizontalWrapMode.Wrap;
        info.verticalOverflow = VerticalWrapMode.Truncate;

        var fight = UIFactory.CreateButton(detail, "Fight", cleared ? "REPLAY" : "FIGHT", cleared ? UIFactory.PanelLight : new Color(0.9f, 0.2f, 0.3f), 30);
        UIFactory.TopLeft((RectTransform)fight.transform, 360, 506, 340, 74);
        fight.interactable = open;
        fight.onClick.AddListener(() => Fight(n));
        var cost = UIFactory.CreateText(detail, "Cost", $"{cfg.staminaCost} stamina per fight", 20, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(cost.rectTransform, 720, 506, 600, 74);
    }

    public static string RewardText(BossGate g, int percent)
    {
        var parts = new List<string>();
        string P(int n) => percent >= 100 ? n.ToString("N0") : $"{n * percent / 100f:0.##}";
        if (g.godOrbs > 0) parts.Add($"{P(g.godOrbs)} God Orb{(g.godOrbs == 1 && percent >= 100 ? "" : "s")}");
        if (g.materials > 0) parts.Add($"{P(g.materials)} {PlayerProfile.MaterialName(g.materialTier)}{(g.materials == 1 && percent >= 100 ? "" : "s")}");
        if (g.gold > 0) parts.Add($"{P(g.gold)} Gold");
        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }

    void BuildTeam()
    {
        UIFactory.Clear(teamRow);
        var lines = new List<string>();
        int hp = 0, atk = 0, def = 0;
        for (int i = 0; i < 3; i++)
        {
            var tile = CardTile.Build(teamRow, $"Slot{i}", 160f, 224f, false);
            UIFactory.TopLeft((RectTransform)tile.transform, i * 172, 0, 160, 224);
            var slot = PlayerProfile.GetSlot(i);
            var card = slot != null && slot.HasCard ? PlayerProfile.FindCard(slot.cardUid) : null;
            if (card == null) { tile.SetEmpty(slot != null && slot.unlocked ? $"SLOT {i + 1}\nempty" : $"SLOT {i + 1}\nlocked"); continue; }
            tile.SetInstance(card, slot.build);
            var m = PlayerProfile.SlotMember(i);
            if (m == null) continue;
            hp += m.hp; atk += m.atk; def += m.def;
            lines.Add($"{m.name}: SPD {m.baseSpeed}, {m.TalentNames}");
        }
        teamText.text = $"<b>Team HP {hp:N0}   ATK {atk}   DEF {def}</b>\n" + string.Join("\n", lines);
        string problem = PlayerProfile.BossTeamProblem();
        message.text = PlayerProfile.HiddenTestMode ? "3v3 TEST MODE: every gate open, empty slots use test God cards, no stamina, no rewards."
                     : problem ?? $"Each boss fight costs {BossConfig.Current.staminaCost} stamina.";
    }

    void Fight(int gate)
    {
        string problem = PlayerProfile.BossTeamProblem();
        if (problem != null) { message.text = problem; SfxSynth.Play("fail", 0.6f); return; }
        if (!PlayerProfile.IsBossOpen(gate)) { message.text = "Defeat the bosses before this one first."; return; }
        int cost = BossConfig.Current.staminaCost;
        if (!PlayerProfile.HiddenTestMode && !PlayerProfile.TrySpendStamina(cost))
        {
            message.text = $"Not enough stamina (each boss fight costs {cost}).";
            SfxSynth.Play("fail", 0.6f);
            return;
        }
        SfxSynth.Play("click");
        GameSession.StartBossBattle(gate);
    }
}
