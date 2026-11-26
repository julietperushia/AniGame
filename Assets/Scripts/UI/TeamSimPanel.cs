using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Test Mode > 3v3 SIMULATOR: pick rarity / element / talents for the 6 cards (anything left on Random is rolled
/// again for every battle; rarity left unset = God), choose how many battles, run them and read the win rates.
/// Built at runtime. The same simulator is in the editor under Tools > AniGame > 3v3 Simulator.
/// </summary>
public class TeamSimPanel : MonoBehaviour
{
    public const string PrefsKey = "AniGame.TeamSimSettings";

    static readonly CardElement[] ElementOptions =
        { CardElement.Fire, CardElement.Water, CardElement.Tree, CardElement.Lightning, CardElement.Light, CardElement.Dark, CardElement.Rock, CardElement.Neutral };
    static readonly float[] ScaleOptions = { 0f, 0.25f, 0.33f, 0.5f, 0.75f, 1f };

    class SlotRow
    {
        public Dropdown rarity, element, talent, talent2;
        public List<string> talentNames = new List<string>(), talent2Names = new List<string>();
    }

    TeamSimSettings settings;
    readonly SlotRow[] rows = new SlotRow[6];
    InputField battlesInput;
    Button rollsButton, runButton;
    Dropdown scaleDropdown;
    Text progress;
    RectTransform resultContent;
    ScrollRect resultScroll;
    TeamSimReport lastReport;
    bool showLog;
    bool running;
    bool building;

    public static TeamSimPanel Open(Transform canvas)
    {
        var existing = canvas.GetComponentInChildren<TeamSimPanel>(true);
        var p = existing != null ? existing : Build(canvas);
        p.gameObject.SetActive(true);
        p.transform.SetAsLastSibling();
        return p;
    }

    static TeamSimSettings LoadSettings()
    {
        try
        {
            var json = PlayerPrefs.GetString(PrefsKey, "");
            var s = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<TeamSimSettings>(json);
            if (s != null && s.slots != null && s.slots.Length == 6) return s;
        }
        catch (Exception) { }
        return new TeamSimSettings();
    }

    void SaveSettings()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(settings));
        PlayerPrefs.Save();
    }

    static TeamSimPanel Build(Transform canvas)
    {
        var dim = UIFactory.CreateImage(canvas, "TeamSimPanel", new Color(0, 0, 0, 0.85f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        var p = dim.gameObject.AddComponent<TeamSimPanel>();
        p.building = true;
        p.settings = LoadSettings();

        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.07f, 0.08f, 0.14f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800, 980));
        UIFactory.AddOutline(panel, new Color(1f, 0.35f, 0.4f, 0.7f), 3f);
        var pt = panel.transform;

        var title = UIFactory.CreateText(pt, "Title", "3v3 SIMULATOR", 40, new Color(1f, 0.55f, 0.6f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, 30, 18, 600, 56);
        var sub = UIFactory.CreateText(pt, "Sub", "Anything left on Random is rolled again for every battle. Rarity not set = God.", 18, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(sub.rectTransform, 360, 26, 700, 40);

        Text Label(string text, float x, float y, float w, int size = 20, Color? c = null)
        {
            var t = UIFactory.CreateText(pt, text, text, size, c ?? UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.TopLeft(t.rectTransform, x, y, w, 46);
            return t;
        }

        Dropdown Drop(string name, float x, float y, float w)
        {
            var d = UIFactory.CreateDropdown(pt, name, 18, 420f);
            UIFactory.TopLeft((RectTransform)d.transform, x, y, w, 46);
            var label = d.captionText.rectTransform;
            label.offsetMin = new Vector2(12, label.offsetMin.y);
            d.itemText.rectTransform.offsetMin = new Vector2(30, d.itemText.rectTransform.offsetMin.y);
            return d;
        }

        Button Btn(string label, Color c, float x, float y, float w, float h, Action click, int size = 20)
        {
            var b = UIFactory.CreateButton(pt, label, label, c, size);
            UIFactory.TopLeft((RectTransform)b.transform, x, y, w, h);
            b.onClick.AddListener(() => click());
            return b;
        }

        // ---- the 6 cards
        Label("RARITY", 90, 84, 200, 16, UIFactory.TextDim);
        Label("ELEMENT", 300, 84, 180, 16, UIFactory.TextDim);
        Label("TALENT  (God: active)", 490, 84, 270, 16, UIFactory.TextDim);
        Label("GOD PASSIVE", 760, 84, 260, 16, UIFactory.TextDim);
        for (int i = 0; i < 6; i++)
        {
            int idx = i;
            float y = 124 + i * 58 + (i >= 3 ? 20 : 0);
            Label((i < 3 ? "A" : "B") + (i % 3 + 1), 30, y, 56, 22, i < 3 ? new Color(0.45f, 0.8f, 1f) : new Color(1f, 0.55f, 0.5f));
            var row = new SlotRow
            {
                rarity = Drop("Rarity" + i, 90, y, 200),
                element = Drop("Element" + i, 300, y, 180),
                talent = Drop("Talent" + i, 490, y, 260),
                talent2 = Drop("Talent2" + i, 760, y, 260),
            };
            p.rows[i] = row;
            row.rarity.options = new List<Dropdown.OptionData> { new Dropdown.OptionData("God (not set)") };
            foreach (CardRarity r in Enum.GetValues(typeof(CardRarity))) row.rarity.options.Add(new Dropdown.OptionData(CardStats.RarityName(r)));
            row.element.options = new List<Dropdown.OptionData> { new Dropdown.OptionData("Random") };
            foreach (var e in ElementOptions) row.element.options.Add(new Dropdown.OptionData(e.ToString()));
            row.rarity.onValueChanged.AddListener(_ => p.OnSlotChanged(idx, true));
            row.element.onValueChanged.AddListener(_ => p.OnSlotChanged(idx, false));
            row.talent.onValueChanged.AddListener(_ => p.OnSlotChanged(idx, false));
            row.talent2.onValueChanged.AddListener(_ => p.OnSlotChanged(idx, false));
        }

        // ---- battle settings
        float sy = 520;
        Label("BATTLES", 30, sy, 110);
        p.battlesInput = UIFactory.CreateInputField(pt, "Battles", "200", 22);
        UIFactory.TopLeft((RectTransform)p.battlesInput.transform, 140, sy, 130, 46);
        p.battlesInput.contentType = InputField.ContentType.IntegerNumber;
        p.battlesInput.onEndEdit.AddListener(_ => p.ReadBattles());
        int[] presets = { 100, 500, 1000, 5000 };
        for (int i = 0; i < presets.Length; i++)
        {
            int v = presets[i];
            Btn(v.ToString(), UIFactory.PanelLight, 286 + i * 112, sy, 102, 46, () => { p.settings.battles = v; p.battlesInput.text = v.ToString(); p.SaveSettings(); }, 18);
        }
        p.rollsButton = Btn("ROLLS", new Color(0.3f, 0.35f, 0.55f), 760, sy, 260, 46, () =>
        {
            p.settings.averageRolls = !p.settings.averageRolls;
            p.SaveSettings();
            p.RefreshRolls();
        }, 18);
        TooltipTrigger.Add(p.rollsButton.image, "<b>Stat rolls</b>\nRandom: every card rolls its HP/ATK/DEF/SPD like a new card.\nAverage: every Roll is 0, so only rarity, element and talents differ.");

        float sy2 = sy + 62;
        Label("ATTACK SCALE", 30, sy2, 180);
        p.scaleDropdown = Drop("Scale", 210, sy2, 300);
        p.scaleDropdown.options = new List<Dropdown.OptionData>();
        foreach (var s in ScaleOptions)
            p.scaleDropdown.options.Add(new Dropdown.OptionData(s <= 0f ? $"GameBalance ({GameBalance.Current.teamAttackScale:0.##})" : s >= 1f ? "1 (no scaling)" : s.ToString("0.##")));
        p.scaleDropdown.onValueChanged.AddListener(v => { p.settings.attackScale = ScaleOptions[Mathf.Clamp(v, 0, ScaleOptions.Length - 1)]; p.SaveSettings(); });
        TooltipTrigger.Add((Graphic)p.scaleDropdown.targetGraphic, "<b>Attack scale</b>\nTeam ATK and DEF are the totals of 3 cards, which makes every normal attack about 3x a 1v1 hit, and 3 cards attack each round. " +
                                                                    "This multiplies 3v3 normal attack damage (and Slayer) so battles last about as long as 1v1. Default: GameBalance.asset (0.33).");
        Btn("RESET ALL TO RANDOM", new Color(0.35f, 0.3f, 0.45f), 760, sy2, 260, 46, () => p.ResetSlots(), 18);

        var rules = UIFactory.CreateText(pt, "Rules",
            "<b>3v3 rules</b>  HP / ATK / DEF are the team totals and shared.  Every card has its own SPD: each round all 6 cards act once, fastest first.  " +
            "One mana pool (100 per card, 200 for a Soul Swap card); every card gains mana on its turn, actives can't be used in round 1 and cost their own mana.  " +
            "Passives and debuffs are global (they hit the whole enemy team).  Crit rate / crit damage are per card; evasion is shared.  " +
            "Stun / Freeze / Sleep skip only the next card of that team.  Element: the attacker's element against all 3 defenders, averaged.",
            16, UIFactory.TextDim, TextAnchor.UpperLeft);
        UIFactory.TopLeft(rules.rectTransform, 30, sy2 + 66, 990, 130);
        rules.horizontalOverflow = HorizontalWrapMode.Wrap;

        p.progress = UIFactory.CreateText(pt, "Progress", "", 22, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(p.progress.rectTransform, 30, 806, 990, 40);
        p.runButton = Btn("RUN", new Color(0.2f, 0.65f, 0.4f), 30, 860, 560, 84, () => p.Run(), 32);
        Btn("CLOSE", UIFactory.PanelLight, 610, 860, 410, 84, () => { if (!p.running) dim.gameObject.SetActive(false); }, 26);

        // ---- results
        var tabs = new[] { "RESULTS", "FIRST BATTLE LOG" };
        for (int i = 0; i < tabs.Length; i++)
        {
            bool log = i == 1;
            Btn(tabs[i], new Color(0.25f, 0.28f, 0.42f), 1060 + i * 250, 84, 240, 50, () => { p.showLog = log; p.ShowReport(); }, 18);
        }
        Btn("COPY", new Color(0.3f, 0.3f, 0.36f), 1560, 84, 210, 50, () =>
        {
            if (p.lastReport == null) return;
            GUIUtility.systemCopyBuffer = System.Text.RegularExpressions.Regex.Replace(p.lastReport.Summary() + "\n" + p.lastReport.sampleLog, "<.*?>", "");
            p.progress.text = "Results copied to the clipboard";
        }, 18);
        var box = UIFactory.CreatePanel(pt, "ResultBox", new Color(0.03f, 0.03f, 0.07f, 1f), true);
        UIFactory.TopLeft(box.rectTransform, 1060, 150, 710, 800);
        p.resultScroll = UIFactory.CreateScroll(box.transform, "Results", true, out p.resultContent);
        UIFactory.Stretch((RectTransform)p.resultScroll.transform, 14, 10, 14, 10);
        var vlg = UIFactory.AddVertical(p.resultContent, 0, 0);
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;

        p.LoadIntoUi();
        p.building = false;
        p.ShowReport();
        return p;
    }

    // ------------------------------------------------------------------ slots

    static string TalentLabel(Talent t) => $"{t.DisplayName} [{(t.IsActive ? "A" : "P")}]";

    void FillTalentOptions(int i)
    {
        var slot = settings.slots[i];
        var row = rows[i];
        row.talentNames.Clear();
        row.talent2Names.Clear();
        row.talent.options = new List<Dropdown.OptionData>();
        row.talent2.options = new List<Dropdown.OptionData>();
        if (slot.IsGod)
        {
            row.talent.options.Add(new Dropdown.OptionData("Random active"));
            row.talentNames.Add("");
            foreach (var t in TeamSimulator.Actives()) { row.talent.options.Add(new Dropdown.OptionData(t.DisplayName)); row.talentNames.Add(t.DisplayName); }
            row.talent2.options.Add(new Dropdown.OptionData("Random passive"));
            row.talent2Names.Add("");
            foreach (var t in TeamSimulator.Passives()) { row.talent2.options.Add(new Dropdown.OptionData(t.DisplayName)); row.talent2Names.Add(t.DisplayName); }
        }
        else
        {
            row.talent.options.Add(new Dropdown.OptionData("Random talent"));
            row.talentNames.Add("");
            foreach (var t in TeamSimulator.AllTalents()) { row.talent.options.Add(new Dropdown.OptionData(TalentLabel(t))); row.talentNames.Add(t.DisplayName); }
        }
        // keep the chosen talent when it still fits
        int a = row.talentNames.IndexOf(slot.talent ?? "");
        if (a < 0) { a = 0; slot.talent = ""; }
        int b = row.talent2Names.IndexOf(slot.talent2 ?? "");
        if (b < 0) { b = 0; slot.talent2 = ""; }
        row.talent.SetValueWithoutNotify(a);
        row.talent.RefreshShownValue();
        row.talent2.SetValueWithoutNotify(b);
        row.talent2.RefreshShownValue();
        row.talent2.gameObject.SetActive(slot.IsGod);
    }

    void LoadIntoUi()
    {
        building = true;
        for (int i = 0; i < 6; i++)
        {
            var slot = settings.slots[i];
            var row = rows[i];
            row.rarity.SetValueWithoutNotify(slot.setRarity ? (int)slot.rarity + 1 : 0);
            row.rarity.RefreshShownValue();
            int e = slot.setElement ? Array.IndexOf(ElementOptions, slot.element) + 1 : 0;
            row.element.SetValueWithoutNotify(Mathf.Max(0, e));
            row.element.RefreshShownValue();
            FillTalentOptions(i);
        }
        battlesInput.text = settings.battles.ToString();
        int si = Array.IndexOf(ScaleOptions, settings.attackScale);
        scaleDropdown.SetValueWithoutNotify(si < 0 ? 0 : si);
        scaleDropdown.RefreshShownValue();
        RefreshRolls();
        building = false;
    }

    void OnSlotChanged(int i, bool rarityChanged)
    {
        if (building) return;
        var slot = settings.slots[i];
        var row = rows[i];
        int r = row.rarity.value;
        slot.setRarity = r > 0;
        if (r > 0) slot.rarity = (CardRarity)(r - 1);
        int e = row.element.value;
        slot.setElement = e > 0;
        if (e > 0) slot.element = ElementOptions[e - 1];
        if (rarityChanged) FillTalentOptions(i);
        else
        {
            slot.talent = row.talent.value < row.talentNames.Count ? row.talentNames[row.talent.value] : "";
            slot.talent2 = slot.IsGod && row.talent2.value < row.talent2Names.Count ? row.talent2Names[row.talent2.value] : "";
        }
        SaveSettings();
    }

    void ResetSlots()
    {
        if (running) return;
        for (int i = 0; i < 6; i++) settings.slots[i] = new TeamSimSlot();
        SaveSettings();
        LoadIntoUi();
        progress.text = "Every card is random again (God rarity)";
    }

    void RefreshRolls()
    {
        UIFactory.ButtonLabel(rollsButton).text = settings.averageRolls ? "ROLLS: AVERAGE" : "ROLLS: RANDOM";
    }

    void ReadBattles()
    {
        if (int.TryParse(battlesInput.text, out int n)) settings.battles = Mathf.Clamp(n, 1, 100000);
        battlesInput.text = settings.battles.ToString();
        SaveSettings();
    }

    // ------------------------------------------------------------------ run

    void Run()
    {
        if (running) return;
        ReadBattles();
        settings.seed = Environment.TickCount & 0x7fffffff;
        running = true;
        UIFactory.ButtonLabel(runButton).text = "RUNNING...";
        runButton.interactable = false;
        SfxSynth.Play("click", 0.5f);
        StartCoroutine(TeamSimulator.RunAsync(settings,
            (done, total) => progress.text = $"Battle {done} / {total}",
            report =>
            {
                running = false;
                runButton.interactable = true;
                UIFactory.ButtonLabel(runButton).text = "RUN";
                lastReport = report;
                showLog = false;
                progress.text = report == null ? "Simulation failed" : $"Done: {report.battles} battles, avg {report.AvgRounds:0.0} rounds";
                ShowReport();
                SfxSynth.Play("buff", 0.6f);
            }));
    }

    void OnDisable()
    {
        if (!running) return;
        running = false;
        if (runButton != null)
        {
            runButton.interactable = true;
            UIFactory.ButtonLabel(runButton).text = "RUN";
        }
    }

    void ShowReport()
    {
        if (resultContent == null) return;
        UIFactory.Clear(resultContent);
        string text;
        if (lastReport == null)
            text = "<color=#9AA4B5>Set the cards (or leave them random), choose the number of battles and press RUN.\n\n" +
                   "The results list every talent with the win rate of the teams that had it. With random teams a talent near 50% is fair; " +
                   "<color=#FF6B6B>STRONG</color> marks 62% or more, <color=#6FA8FF>weak</color> 38% or less.\n\n" +
                   "To test one talent, set it on one card of Team A and leave everything else random. To test a full team, set all of Team A.</color>";
        else text = showLog ? lastReport.sampleLog : lastReport.Summary();

        // Unity Text has a vertex limit: one Text per block of lines
        var lines = (text ?? "").Split('\n');
        for (int start = 0; start < lines.Length; start += 50)
        {
            var chunk = string.Join("\n", lines.Skip(start).Take(50));
            var t = UIFactory.CreateText(resultContent, "Block", chunk, 17, UIFactory.TextMain, TextAnchor.UpperLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(resultContent);
        resultContent.anchoredPosition = Vector2.zero; // back to the top
        resultScroll.velocity = Vector2.zero;
    }
}
