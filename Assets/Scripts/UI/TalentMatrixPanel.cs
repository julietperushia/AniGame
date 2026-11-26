using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Test Mode > 1v1 TALENT MATRIX: every talent fights every other talent (identical cards, one talent each),
/// then the ranking, each talent's matchups and the first battle log of any pair. Built at runtime.
/// </summary>
public class TalentMatrixPanel : MonoBehaviour
{
    static readonly int[] Presets = { 10, 20, 50 };

    TalentMatrixSettings settings = new TalentMatrixSettings();
    TalentMatrixResult result;
    Dropdown rarityDrop;
    InputField battlesInput;
    Button runButton;
    Text progress, matchTitle, logTitle;
    RectTransform rankContent, matchContent, logContent;
    ScrollRect logScroll;
    int selected = -1;
    bool running;

    public static TalentMatrixPanel Open(Transform canvas)
    {
        var p = canvas.GetComponentInChildren<TalentMatrixPanel>(true) ?? Build(canvas);
        p.gameObject.SetActive(true);
        p.transform.SetAsLastSibling();
        return p;
    }

    static TalentMatrixPanel Build(Transform canvas)
    {
        var dim = UIFactory.CreateImage(canvas, "TalentMatrixPanel", new Color(0, 0, 0, 0.85f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        var p = dim.gameObject.AddComponent<TalentMatrixPanel>();
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.07f, 0.08f, 0.14f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800, 980));
        UIFactory.AddOutline(panel, new Color(1f, 0.35f, 0.4f, 0.7f), 3f);
        var pt = panel.transform;

        var title = UIFactory.CreateText(pt, "Title", "1v1 TALENT MATRIX", 40, new Color(1f, 0.55f, 0.6f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, 30, 18, 520, 56);
        var sub = UIFactory.CreateText(pt, "Sub", "Every talent against every other talent. Both cards are identical (same rarity, Roll 0, Neutral) with one talent each; sides swap every battle.", 17, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(sub.rectTransform, 540, 22, 1230, 48);
        sub.horizontalOverflow = HorizontalWrapMode.Wrap;

        Button Btn(string label, Color c, float x, float y, float w, float h, Action click, int size = 20)
        {
            var b = UIFactory.CreateButton(pt, label, label, c, size);
            UIFactory.TopLeft((RectTransform)b.transform, x, y, w, h);
            b.onClick.AddListener(() => click());
            return b;
        }
        Text Label(string text, float x, float y, float w, int size = 18)
        {
            var t = UIFactory.CreateText(pt, text, text, size, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.TopLeft(t.rectTransform, x, y, w, 46);
            return t;
        }

        Label("RARITY", 30, 84, 90);
        p.rarityDrop = UIFactory.CreateDropdown(pt, "Rarity", 18, 420f);
        UIFactory.TopLeft((RectTransform)p.rarityDrop.transform, 120, 84, 200, 46);
        p.rarityDrop.options = Enum.GetValues(typeof(CardRarity)).Cast<CardRarity>().Select(r => new Dropdown.OptionData(CardStats.RarityName(r))).ToList();
        p.rarityDrop.SetValueWithoutNotify((int)p.settings.rarity);
        p.rarityDrop.RefreshShownValue();
        p.rarityDrop.onValueChanged.AddListener(v => p.settings.rarity = (CardRarity)v);

        Label("BATTLES PER PAIR", 350, 84, 200);
        p.battlesInput = UIFactory.CreateInputField(pt, "Battles", "20", 22);
        UIFactory.TopLeft((RectTransform)p.battlesInput.transform, 550, 84, 110, 46);
        p.battlesInput.contentType = InputField.ContentType.IntegerNumber;
        p.battlesInput.text = p.settings.battlesPerPair.ToString();
        for (int i = 0; i < Presets.Length; i++)
        {
            int v = Presets[i];
            Btn(v.ToString(), UIFactory.PanelLight, 672 + i * 82, 84, 74, 46, () => p.battlesInput.text = v.ToString(), 18);
        }
        p.runButton = Btn("RUN", new Color(0.2f, 0.65f, 0.4f), 930, 80, 220, 54, () => p.Run(), 26);
        Btn("EXPORT CSV", new Color(0.3f, 0.35f, 0.55f), 1170, 80, 220, 54, () => p.Export(), 20);
        Btn("CLOSE", UIFactory.PanelLight, 1410, 80, 360, 54, () => { if (!p.running) dim.gameObject.SetActive(false); }, 22);
        p.progress = UIFactory.CreateText(pt, "Progress", "", 20, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(p.progress.rectTransform, 30, 140, 1740, 34);

        RectTransform Column(string name, float x, float w, out ScrollRect scroll)
        {
            var box = UIFactory.CreatePanel(pt, name + "Box", new Color(0.03f, 0.03f, 0.07f, 1f), true);
            UIFactory.TopLeft(box.rectTransform, x, 230, w, 720);
            scroll = UIFactory.CreateScroll(box.transform, name, true, out var content);
            UIFactory.Stretch((RectTransform)scroll.transform, 10, 10, 10, 10);
            var v = UIFactory.AddVertical(content, 4, 0);
            v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;
            return content;
        }
        Label("RANKING  (average win rate)", 30, 182, 420, 18);
        p.rankContent = Column("Ranking", 30, 420, out _);
        p.matchTitle = Label("MATCHUPS", 470, 182, 500, 18);
        p.matchContent = Column("Matchups", 470, 500, out _);
        p.logTitle = Label("FIRST BATTLE LOG", 990, 182, 780, 18);
        p.logContent = Column("Log", 990, 780, out p.logScroll);
        p.ShowLog("Press RUN. With 20 battles per pair this is about 54,000 battles and takes a minute or two.");
        return p;
    }

    Button Row(RectTransform parent, string text, Color color, Action click)
    {
        var b = UIFactory.CreateButton(parent, "Row", text, color, 17);
        UIFactory.SetLayoutSize(b, 0, 40);
        var label = UIFactory.ButtonLabel(b);
        if (label != null) { label.alignment = TextAnchor.MiddleLeft; label.rectTransform.offsetMin = new Vector2(14, 0); }
        b.onClick.AddListener(() => click());
        return b;
    }

    static Color RateColor(float r) =>
        r >= 0.6f ? new Color(0.18f, 0.45f, 0.25f) : r <= 0.4f ? new Color(0.5f, 0.18f, 0.2f) : UIFactory.PanelLight;

    void Run()
    {
        if (running) return;
        if (int.TryParse(battlesInput.text, out int n)) settings.battlesPerPair = Mathf.Clamp(n, 1, 500);
        settings.seed = Environment.TickCount & 0x7fffffff;
        running = true;
        runButton.interactable = false;
        UIFactory.ButtonLabel(runButton).text = "RUNNING...";
        StartCoroutine(TalentMatrix.RunAsync(settings, (d, t) => progress.text = $"Pair {d} / {t}", r =>
        {
            running = false;
            runButton.interactable = true;
            UIFactory.ButtonLabel(runButton).text = "RUN";
            result = r;
            if (r == null) { progress.text = "Failed"; return; }
            progress.text = $"Done: {r.pairs.Count} pairs, {r.battles:N0} battles in {r.seconds:0}s. Click a talent to see its matchups, then a matchup for its first battle.";
            ShowRanking();
            Select(r.Ranking().First());
        }));
    }

    void ShowRanking()
    {
        UIFactory.Clear(rankContent);
        int rank = 1;
        foreach (int i in result.Ranking())
        {
            int idx = i;
            var t = result.talents[i];
            float r = result.Overall(i);
            Row(rankContent, $"{rank++,2}.  {r * 100f:0.0}%   {t.DisplayName}  {(t.IsActive ? "<color=#6FD3FF>A</color>" : "<color=#FFB23D>P</color>")}", RateColor(r), () => Select(idx));
        }
    }

    void Select(int i)
    {
        selected = i;
        UIFactory.Clear(matchContent);
        matchTitle.text = $"{result.talents[i].DisplayName.ToUpperInvariant()} AGAINST EVERY TALENT";
        foreach (int j in Enumerable.Range(0, result.talents.Count).Where(j => j != i).OrderByDescending(j => result.Rate(i, j)))
        {
            int opp = j;
            float r = result.Rate(i, j);
            Row(matchContent, $"{r * 100f:0.0}%   vs {result.talents[j].DisplayName}", RateColor(r), () => ShowPair(i, opp));
        }
        ShowPair(i, Enumerable.Range(0, result.talents.Count).Where(j => j != i).OrderByDescending(j => result.Rate(i, j)).First());
    }

    void ShowPair(int i, int j)
    {
        var p = result.Pair(i, j);
        if (p == null) return;
        bool flipped = p.a != result.talents[i];
        int wins = flipped ? p.bWins : p.aWins, losses = flipped ? p.aWins : p.bWins;
        logTitle.text = $"{result.talents[i].DisplayName} vs {result.talents[j].DisplayName}: {wins} wins, {losses} losses, {p.draws} draws, {p.AvgRounds:0.0} rounds";
        ShowLog(p.firstLog);
    }

    void ShowLog(string text)
    {
        UIFactory.Clear(logContent);
        var lines = (text ?? "").Split('\n');
        for (int s = 0; s < lines.Length; s += 50)
        {
            var t = UIFactory.CreateText(logContent, "Block", string.Join("\n", lines.Skip(s).Take(50)), 16, UIFactory.TextMain, TextAnchor.UpperLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(logContent);
        logContent.anchoredPosition = Vector2.zero;
    }

    void Export()
    {
        if (result == null) { progress.text = "Run the matrix first."; return; }
        try
        {
            string folder = TalentMatrix.Export(result, TalentMatrix.DefaultFolder);
            progress.text = $"Saved talent_matrix.csv, talent_pairs.csv and talent_ranking.csv to {folder}";
            GUIUtility.systemCopyBuffer = folder;
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
