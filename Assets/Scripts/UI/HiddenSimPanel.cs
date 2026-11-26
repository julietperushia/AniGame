using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Test Mode > HIDDEN LOCATION SIM: every Hidden Location floor against random teams of 3 max-stat God cards.</summary>
public class HiddenSimPanel : MonoBehaviour
{
    static readonly int[] Presets = { 20, 50, 100, 200 };
    HiddenSimSettings settings = new HiddenSimSettings();
    HiddenSimResult result;
    InputField battlesInput;
    Button runButton;
    Text progress;
    RectTransform content;
    bool running;

    public static HiddenSimPanel Open(Transform canvas)
    {
        var p = canvas.GetComponentInChildren<HiddenSimPanel>(true) ?? Build(canvas);
        p.gameObject.SetActive(true);
        p.transform.SetAsLastSibling();
        return p;
    }

    static HiddenSimPanel Build(Transform canvas)
    {
        var dim = UIFactory.CreateImage(canvas, "HiddenSimPanel", new Color(0, 0, 0, 0.85f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        var p = dim.gameObject.AddComponent<HiddenSimPanel>();
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.07f, 0.06f, 0.13f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800, 980));
        UIFactory.AddOutline(panel, new Color(0.75f, 0.45f, 1f, 0.8f), 3f);
        var pt = panel.transform;
        var title = UIFactory.CreateText(pt, "Title", "HIDDEN LOCATION SIMULATOR", 38, new Color(0.85f, 0.65f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, 30, 18, 700, 56);
        var sub = UIFactory.CreateText(pt, "Sub", $"Every floor of the 20 Hidden LOCs against random teams of 3 God cards with the best stats a player can reach (every stat {GameRules.MaxStat(GameRules.MaxTier)}: max Roll + {GameRules.GodOrbMaxStacks} God Orbs). Each battle draws a new team: random elements and a random active + passive talent per card.", 17, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(sub.rectTransform, 740, 18, 1030, 60);
        sub.horizontalOverflow = HorizontalWrapMode.Wrap;

        Button Btn(string label, Color c, float x, float y, float w, float h, Action click, int size = 20)
        {
            var b = UIFactory.CreateButton(pt, label, label, c, size);
            UIFactory.TopLeft((RectTransform)b.transform, x, y, w, h);
            b.onClick.AddListener(() => click());
            return b;
        }
        var lab = UIFactory.CreateText(pt, "BattlesLabel", "BATTLES PER FLOOR", 18, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(lab.rectTransform, 30, 90, 220, 46);
        p.battlesInput = UIFactory.CreateInputField(pt, "Battles", "100", 22);
        UIFactory.TopLeft((RectTransform)p.battlesInput.transform, 250, 90, 120, 46);
        p.battlesInput.contentType = InputField.ContentType.IntegerNumber;
        p.battlesInput.text = p.settings.battlesPerFloor.ToString();
        for (int i = 0; i < Presets.Length; i++) { int v = Presets[i]; Btn(v.ToString(), UIFactory.PanelLight, 384 + i * 84, 90, 76, 46, () => p.battlesInput.text = v.ToString(), 18); }
        p.runButton = Btn("RUN", new Color(0.2f, 0.65f, 0.4f), 760, 86, 240, 54, () => p.Run(), 26);
        Btn("EXPORT CSV", new Color(0.3f, 0.35f, 0.55f), 1020, 86, 240, 54, () => p.Export(), 20);
        Btn("CLOSE", UIFactory.PanelLight, 1280, 86, 490, 54, () => { if (!p.running) dim.gameObject.SetActive(false); }, 22);
        p.progress = UIFactory.CreateText(pt, "Progress", "", 20, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(p.progress.rectTransform, 30, 146, 1740, 34);
        var box = UIFactory.CreatePanel(pt, "ResultBox", new Color(0.03f, 0.03f, 0.07f, 1f), true);
        UIFactory.TopLeft(box.rectTransform, 30, 190, 1740, 760);
        var scroll = UIFactory.CreateScroll(box.transform, "Results", true, out p.content);
        UIFactory.Stretch((RectTransform)scroll.transform, 14, 10, 14, 10);
        var v2 = UIFactory.AddVertical(p.content, 0, 0);
        v2.childControlHeight = true; v2.childControlWidth = true; v2.childForceExpandHeight = false;
        p.Show("<color=#9AA4B5>Press RUN. 100 battles per floor = 6,000 battles.</color>");
        return p;
    }

    void Run()
    {
        if (running) return;
        if (int.TryParse(battlesInput.text, out int n)) settings.battlesPerFloor = Mathf.Clamp(n, 1, 5000);
        settings.seed = Environment.TickCount & 0x7fffffff;
        running = true;
        runButton.interactable = false;
        UIFactory.ButtonLabel(runButton).text = "RUNNING...";
        StartCoroutine(HiddenSimulator.RunAsync(settings, (d, t) => progress.text = $"Floor {d} / {t}", r =>
        {
            running = false;
            runButton.interactable = true;
            UIFactory.ButtonLabel(runButton).text = "RUN";
            result = r;
            if (r == null || r.floors.Count == 0) { progress.text = "No Hidden Location data."; return; }
            progress.text = $"Done: {r.battles:N0} battles in {r.seconds:0}s";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>FLOOR            PLAYER WIN   VERDICT     ROUNDS   ENEMIES</b>");
            foreach (var f in r.floors)
            {
                string c = f.WinRate >= 0.8f ? "#7CFC6A" : f.WinRate >= 0.5f ? "#D7F57A" : f.WinRate >= 0.2f ? "#FFB23D" : "#FF6B6B";
                sb.AppendLine($"LOC {f.loc,2} - {f.floor}   <color={c}>{f.WinRate * 100f,5:0.0}%  {f.Verdict}</color>   {f.AvgRounds:0.0}   <color=#9AA4B5>{CardStats.RarityName(f.rarity)} {f.enemyStat} · {f.anime}</color>");
            }
            sb.AppendLine();
            sb.AppendLine("<b>TALENTS ON THE PLAYER TEAM (win rate of teams that had it, all floors)</b>");
            foreach (var kv in r.talents.OrderByDescending(k => k.Value[1] / (float)Mathf.Max(1, k.Value[0])))
                sb.AppendLine($"  {100f * kv.Value[1] / Mathf.Max(1, kv.Value[0]),5:0.0}%  {kv.Key}  ({kv.Value[0]} battles)");
            Show(sb.ToString());
        }));
    }

    void Show(string text)
    {
        UIFactory.Clear(content);
        var lines = text.Split('\n');
        for (int s = 0; s < lines.Length; s += 50)
        {
            var t = UIFactory.CreateText(content, "Block", string.Join("\n", lines.Skip(s).Take(50)), 17, UIFactory.TextMain, TextAnchor.UpperLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        content.anchoredPosition = Vector2.zero;
    }

    void Export()
    {
        if (result == null) { progress.text = "Run the simulation first."; return; }
        try
        {
            string folder = Application.isEditor ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/HiddenSim"))
                                                 : System.IO.Path.Combine(Application.persistentDataPath, "HiddenSim");
            System.IO.Directory.CreateDirectory(folder);
            var utf8 = new System.Text.UTF8Encoding(true);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "hidden_floors.csv"), result.FloorsCsv(), utf8);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "hidden_talents.csv"), result.TalentsCsv(), utf8);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "hidden_samples.csv"), result.SamplesCsv(), utf8);
            progress.text = $"Saved hidden_floors.csv, hidden_talents.csv and hidden_samples.csv to {folder}";
        }
        catch (Exception ex) { progress.text = "Export failed: " + ex.Message; }
    }

    void OnDisable()
    {
        if (!running) return;
        running = false;
        if (runButton != null) { runButton.interactable = true; UIFactory.ButtonLabel(runButton).text = "RUN"; }
    }
}
