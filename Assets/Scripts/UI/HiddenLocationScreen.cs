using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HIDDEN LOCATION (3v3): 20 LOCs of 3 floors, each floor a team of 3 enemies. The player's team is the 3 build slots.
/// Unlocked by owning 3 cards of Mythic rarity or higher. The whole screen is built at runtime.
/// </summary>
public class HiddenLocationScreen : MonoBehaviour
{
    RectTransform root, listContent, floorsRow, teamRow;
    Text header, subHeader, teamText, message;
    readonly List<Button> locButtons = new List<Button>();
    int selected = 1;

    public static void BuildLayout(RectTransform canvas)
    {
        canvas.gameObject.AddComponent<HiddenLocationScreen>();
    }

    void Start()
    {
        root = (RectTransform)transform;
        UIFactory.CreateBackground(root, new Color(0.32f, 0.12f, 0.45f, 0.9f), UIFactory.BgDark, out _);
        UIFactory.CreateTopBarWithCurrency(root, "HIDDEN LOCATION", out Button back, out _);
        back.onClick.AddListener(() => GameSession.LoadScene(SceneNames.MainMenu));

        var db = CardDatabase.Instance;
        if (db == null || db.hiddenLocations.Count == 0)
        {
            var t = UIFactory.CreateText(root, "Missing", "The Hidden Location could not be loaded. Please reinstall the game.", 30, UIFactory.Bad, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(t.rectTransform, 100, 200, 100, 200);
            return;
        }
        if (!PlayerProfile.HiddenUnlocked)
        {
            var t = UIFactory.CreateText(root, "Locked", $"The Hidden Location is locked.\nOwn {GameRules.HiddenUnlockCards} cards of Mythic rarity or higher to enter (you have {PlayerProfile.HighRarityCards}).", 32, new Color(0.85f, 0.7f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(t.rectTransform, 100, 200, 100, 200);
            return;
        }

        // ---- LOC list (left)
        var listBox = UIFactory.CreatePanel(root, "LocList", new Color(0.05f, 0.04f, 0.1f, 0.9f), true);
        UIFactory.TopLeft(listBox.rectTransform, 30, 120, 460, 930);
        var scroll = UIFactory.CreateScroll(listBox.transform, "Scroll", true, out listContent);
        UIFactory.Stretch((RectTransform)scroll.transform, 10, 10, 10, 10);
        var v = UIFactory.AddVertical(listContent, 6, 0);
        v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;
        foreach (var loc in db.hiddenLocations)
        {
            int n = loc.number;
            var b = UIFactory.CreateButton(listContent, $"Loc{n}", "", UIFactory.PanelLight, 18);
            UIFactory.SetLayoutSize(b, 0, 70);
            var label = UIFactory.ButtonLabel(b);
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(16, 0);
            label.supportRichText = true;
            b.onClick.AddListener(() => Select(n));
            locButtons.Add(b);
        }

        // ---- detail (right)
        header = UIFactory.CreateText(root, "Header", "", 40, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(header.rectTransform, 520, 116, 1360, 56);
        UIFactory.BestFit(header, 20);
        subHeader = UIFactory.CreateText(root, "SubHeader", "", 20, UIFactory.TextDim, TextAnchor.UpperLeft);
        UIFactory.TopLeft(subHeader.rectTransform, 520, 174, 1360, 56);
        floorsRow = UIFactory.CreateRect("Floors", root);
        UIFactory.TopLeft(floorsRow, 520, 234, 1370, 470);

        var teamBox = UIFactory.CreatePanel(root, "TeamBox", new Color(0.05f, 0.04f, 0.1f, 0.9f), true);
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
        TooltipTrigger.Add(editTeam.image, "Your team is your 3 build slots. Change them in Collection with SAVE TO S1 / S2 / S3.");

        BuildTeam();
        var next = PlayerProfile.NextHiddenFloor();
        Select(GameSession.SelectedHiddenLocation > 0 ? GameSession.SelectedHiddenLocation : next.loc);
    }

    static string Pips(int loc)
    {
        string s = "";
        for (int f = 1; f <= GameRules.HiddenFloorsPerLocation; f++)
            s += PlayerProfile.IsHiddenCleared(loc, f) ? "<color=#FFD54F>■</color>" : "<color=#4A4F63>■</color>";
        return s;
    }

    void RefreshList()
    {
        var db = CardDatabase.Instance;
        for (int i = 0; i < locButtons.Count && i < db.hiddenLocations.Count; i++)
        {
            var loc = db.hiddenLocations[i];
            bool open = PlayerProfile.IsHiddenOpen(loc.number, 1);
            string rc = ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(loc.rarity));
            var label = UIFactory.ButtonLabel(locButtons[i]);
            label.text = open
                ? $"<b>LOC {loc.number}</b>  {Pips(loc.number)}  <size=15><color=#{rc}>{CardStats.RarityName(loc.rarity).ToUpperInvariant()}</color></size>\n<size=16>{loc.anime}</size>"
                : $"<color=#6A6F80><b>LOC {loc.number}</b>  LOCKED  <size=15>{CardStats.RarityName(loc.rarity).ToUpperInvariant()}</size>\n<size=16>{loc.anime}</size></color>";
            locButtons[i].image.color = loc.number == selected ? new Color(0.45f, 0.25f, 0.65f) : open ? UIFactory.PanelLight : new Color(0.1f, 0.1f, 0.14f);
        }
    }

    void Select(int number)
    {
        var db = CardDatabase.Instance;
        var loc = db.GetHidden(number);
        if (loc == null) return;
        selected = number;
        GameSession.SelectedHiddenLocation = number;
        RefreshList();
        int stat = GameRules.HiddenEnemyStat(loc.rarity, loc.number);
        header.text = $"LOC {loc.number}  -  {loc.anime}";
        header.color = Color.Lerp(CardStats.RarityColor(loc.rarity), Color.white, 0.35f);
        subHeader.text = $"{CardStats.RarityName(loc.rarity)} enemies: every stat {stat} (the best {CardStats.RarityName(loc.rarity)} stat {GameRules.MaxStat(GameRules.Tier(loc.rarity))} +{loc.number}%), HP {stat * GameRules.HpMultiplier:N0} each.  " +
                         $"3v3: both teams add up their HP, ATK and DEF; every card acts on its own SPD.";

        UIFactory.Clear(floorsRow);
        for (int f = 1; f <= loc.floors.Count; f++)
        {
            int floor = f;
            var col = UIFactory.CreatePanel(floorsRow, $"Floor{f}", new Color(0.07f, 0.06f, 0.13f, 0.95f), true);
            UIFactory.TopLeft(col.rectTransform, (f - 1) * 460, 0, 446, 470);
            bool cleared = PlayerProfile.IsHiddenCleared(number, f);
            bool open = PlayerProfile.IsHiddenOpen(number, f);
            UIFactory.AddOutline(col, cleared ? new Color(1f, 0.84f, 0.3f, 0.8f) : open ? new Color(0.7f, 0.45f, 1f, 0.9f) : new Color(0.3f, 0.3f, 0.4f, 0.6f), 2f);
            var title = UIFactory.CreateText(col.transform, "Title", $"FLOOR {f}" + (cleared ? "  <color=#FFD54F>CLEARED</color>" : open ? "  <color=#C9A2FF>NEXT</color>" : "  <color=#6A6F80>LOCKED</color>"), 26, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.TopStrip(title.rectTransform, 10, 40, 10, 10);
            var enemies = loc.floors[f - 1].enemies;
            for (int k = 0; k < enemies.Count && k < 3; k++)
            {
                var c = enemies[k];
                var tile = CardTile.Build(col.transform, $"Enemy{k}", 136f, 190f, k == 1);
                UIFactory.TopLeft((RectTransform)tile.transform, 12 + k * 144, 60, 136, 190);
                tile.Set(c, CardStats.RarityName(c.rarity), c.power.ToString());
                if (!open) tile.SetLocked(true, "?");
            }
            var info = UIFactory.CreateText(col.transform, "Info", "", 17, UIFactory.TextDim, TextAnchor.UpperCenter);
            UIFactory.TopStrip(info.rectTransform, 262, 110, 14, 14);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            var names = new List<string>();
            foreach (var c in enemies) if (c != null) names.Add(c.DisplayName);
            info.text = (open ? string.Join(", ", names) : "Beat the floor before to see this team.") +
                        $"\n<color=#FFD54F>{(cleared ? "Replay" : "First clear")}: {GameRules.HiddenGold(number, f, !cleared):N0} gold" + (cleared ? "" : $" + {GameRules.HiddenDiamonds(number, f)} diamonds") + "</color>";
            var fight = UIFactory.CreateButton(col.transform, "Fight", cleared ? "REPLAY" : "FIGHT", cleared ? UIFactory.PanelLight : new Color(1f, 0.35f, 0.62f), 26);
            UIFactory.Place((RectTransform)fight.transform, new Vector2(0.5f, 0f), new Vector2(0, 18), new Vector2(300, 64));
            fight.interactable = open;
            fight.onClick.AddListener(() => Fight(number, floor));
        }
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
        string problem = PlayerProfile.HiddenTeamProblem();
        message.text = PlayerProfile.HiddenTestMode ? "TEST MODE: every floor open, empty slots use test God cards, no stamina, no rewards."
                     : problem ?? $"Each fight costs {PlayerProfile.StaminaCost} stamina.";
    }

    void Fight(int loc, int floor)
    {
        string problem = PlayerProfile.HiddenTeamProblem();
        if (problem != null) { message.text = problem; SfxSynth.Play("fail", 0.6f); return; }
        if (!PlayerProfile.IsHiddenOpen(loc, floor)) { message.text = "Beat the floors before this one first."; return; }
        if (!PlayerProfile.HiddenTestMode && !PlayerProfile.TrySpendStamina(PlayerProfile.StaminaCost))
        {
            message.text = $"Not enough stamina (each fight costs {PlayerProfile.StaminaCost}).";
            SfxSynth.Play("fail", 0.6f);
            return;
        }
        SfxSynth.Play("click");
        GameSession.StartHiddenBattle(loc, floor);
    }
}
