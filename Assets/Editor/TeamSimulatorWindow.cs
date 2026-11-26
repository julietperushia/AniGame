using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Tools > AniGame > 3v3 Simulator: the editor version of Test Mode's 3v3 SIMULATOR.</summary>
public class TeamSimulatorWindow : EditorWindow
{
    const string PrefsKey = "AniGame.TeamSimSettings";
    static readonly CardElement[] ElementOptions =
        { CardElement.Fire, CardElement.Water, CardElement.Tree, CardElement.Lightning, CardElement.Light, CardElement.Dark, CardElement.Rock, CardElement.Neutral };

    TeamSimSettings settings;
    TeamSimReport report;
    bool randomSeed = true;
    int tab;
    Vector2 scroll, resultScroll;
    string[] rarityNames, elementNames, allNames, activeNames, passiveNames;
    GUIStyle resultStyle;

    [MenuItem("Tools/AniGame/3v3 Simulator")]
    static void Open()
    {
        var w = GetWindow<TeamSimulatorWindow>("3v3 Simulator");
        w.minSize = new Vector2(760, 560);
        w.Show();
    }

    void OnEnable()
    {
        try { settings = JsonUtility.FromJson<TeamSimSettings>(EditorPrefs.GetString(PrefsKey, "")); } catch (Exception) { settings = null; }
        if (settings == null || settings.slots == null || settings.slots.Length != 6) settings = new TeamSimSettings();
        rarityNames = new[] { "God (not set)" }.Concat(Enum.GetValues(typeof(CardRarity)).Cast<CardRarity>().Select(CardStats.RarityName)).ToArray();
        elementNames = new[] { "Random" }.Concat(ElementOptions.Select(e => e.ToString())).ToArray();
        RefreshTalents();
    }

    void RefreshTalents()
    {
        allNames = new[] { "Random talent" }.Concat(TeamSimulator.AllTalents().Select(t => t.DisplayName)).ToArray();
        activeNames = new[] { "Random active" }.Concat(TeamSimulator.Actives().Select(t => t.DisplayName)).ToArray();
        passiveNames = new[] { "Random passive" }.Concat(TeamSimulator.Passives().Select(t => t.DisplayName)).ToArray();
    }

    void Save() => EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(settings));

    static string Pick(string current, string[] names, params GUILayoutOption[] opts)
    {
        int i = Array.IndexOf(names, current);
        if (i < 0) i = 0;
        int n = EditorGUILayout.Popup(i, names, opts);
        return n <= 0 ? "" : names[n];
    }

    void OnGUI()
    {
        if (resultStyle == null) resultStyle = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true, fontSize = 12 };
        if (allNames == null || allNames.Length <= 1) RefreshTalents();
        EditorGUI.BeginChangeCheck();

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(position.height * 0.5f, 330)));
        EditorGUILayout.HelpBox("Anything left on Random is rolled again for every battle. Rarity not set = God (1 active + 1 passive).", MessageType.None);
        for (int i = 0; i < 6; i++)
        {
            if (i == 0) EditorGUILayout.LabelField("Team A", EditorStyles.boldLabel);
            if (i == 3) EditorGUILayout.LabelField("Team B", EditorStyles.boldLabel);
            var s = settings.slots[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label((i < 3 ? "A" : "B") + (i % 3 + 1), GUILayout.Width(24));
                int r = EditorGUILayout.Popup(s.setRarity ? (int)s.rarity + 1 : 0, rarityNames, GUILayout.Width(120));
                s.setRarity = r > 0;
                if (r > 0) s.rarity = (CardRarity)(r - 1);
                int e = EditorGUILayout.Popup(s.setElement ? Array.IndexOf(ElementOptions, s.element) + 1 : 0, elementNames, GUILayout.Width(100));
                s.setElement = e > 0;
                if (e > 0) s.element = ElementOptions[e - 1];
                if (s.IsGod)
                {
                    s.talent = Pick(s.talent, activeNames, GUILayout.MinWidth(140));
                    s.talent2 = Pick(s.talent2, passiveNames, GUILayout.MinWidth(140));
                }
                else
                {
                    s.talent = Pick(s.talent, allNames, GUILayout.MinWidth(140));
                    s.talent2 = "";
                }
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUILayout.HorizontalScope())
        {
            settings.battles = Mathf.Clamp(EditorGUILayout.IntField("Battles", settings.battles, GUILayout.Width(260)), 1, 100000);
            foreach (var v in new[] { 100, 500, 1000, 5000 })
                if (GUILayout.Button(v.ToString(), GUILayout.Width(52))) settings.battles = v;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            settings.averageRolls = EditorGUILayout.ToggleLeft("Average rolls (Roll 0, only talents / element / rarity differ)", settings.averageRolls);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            bool useAsset = settings.attackScale <= 0f;
            useAsset = EditorGUILayout.ToggleLeft($"Attack scale from GameBalance ({GameBalance.Current.teamAttackScale:0.##})", useAsset, GUILayout.Width(300));
            if (useAsset) settings.attackScale = 0f;
            else settings.attackScale = EditorGUILayout.Slider(settings.attackScale <= 0f ? 0.33f : settings.attackScale, 0.05f, 1f);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            randomSeed = EditorGUILayout.ToggleLeft("Random seed", randomSeed, GUILayout.Width(110));
            using (new EditorGUI.DisabledScope(randomSeed)) settings.seed = EditorGUILayout.IntField("Seed", settings.seed);
        }
        if (EditorGUI.EndChangeCheck()) Save();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("RUN", GUILayout.Height(32))) Run();
            if (GUILayout.Button("Reset all to random", GUILayout.Height(32), GUILayout.Width(150)))
            {
                for (int i = 0; i < 6; i++) settings.slots[i] = new TeamSimSlot();
                Save();
            }
            using (new EditorGUI.DisabledScope(report == null))
                if (GUILayout.Button("Copy", GUILayout.Height(32), GUILayout.Width(70)))
                    EditorGUIUtility.systemCopyBuffer = System.Text.RegularExpressions.Regex.Replace(report.Summary() + "\n" + report.sampleLog, "<.*?>", "");
        }

        tab = GUILayout.Toolbar(tab, new[] { "Results", "First battle log" });
        resultScroll = EditorGUILayout.BeginScrollView(resultScroll);
        string text = report == null
            ? "Press RUN. Every talent gets the win rate of the teams that had it (draws count half). With random teams ~50% is fair; STRONG = 62%+, weak = 38%-.\nTo test one talent, set it on A1 and leave the rest random."
            : tab == 0 ? report.Summary() : report.sampleLog;
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i += 60)
            GUILayout.Label(string.Join("\n", lines.Skip(i).Take(60)), resultStyle);
        EditorGUILayout.EndScrollView();
    }

    void Run()
    {
        if (CardDatabase.Instance == null) { ShowNotification(new GUIContent("No CardDatabase found")); return; }
        if (randomSeed) settings.seed = Environment.TickCount & 0x7fffffff;
        try
        {
            report = TeamSimulator.Run(settings, (done, total) =>
            {
                if (done % 25 == 0 && EditorUtility.DisplayCancelableProgressBar("3v3 Simulator", $"Battle {done} / {total}", done / (float)total))
                    throw new OperationCanceledException();
            });
        }
        catch (OperationCanceledException) { ShowNotification(new GUIContent("Cancelled")); }
        finally { EditorUtility.ClearProgressBar(); }
        tab = 0;
        resultScroll = Vector2.zero;
        Repaint();
    }
}
