using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Tools > AniGame > 1v1 Talent Matrix: every talent against every talent, with CSV export.</summary>
public class TalentMatrixWindow : EditorWindow
{
    TalentMatrixSettings settings = new TalentMatrixSettings();
    TalentMatrixResult result;
    Vector2 scroll, detailScroll;
    int selected = -1, selectedOpponent = -1;
    GUIStyle wrap;

    [MenuItem("Tools/AniGame/1v1 Talent Matrix")]
    static void Open() => GetWindow<TalentMatrixWindow>("1v1 Talent Matrix").minSize = new Vector2(820, 560);

    /// <summary>Writes the matrix, the per-pair records (with first battle logs) and the ranking as CSV files.</summary>
    public static string Export(TalentMatrixResult r, string folder) => TalentMatrix.Export(r, folder);

    void OnGUI()
    {
        if (wrap == null) wrap = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
        EditorGUILayout.HelpBox("Both cards are identical (same rarity, every Roll 0, same element) and carry one talent each. Each pair fights the chosen number of battles; sides swap every battle so the first move is fair.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            settings.rarity = (CardRarity)EditorGUILayout.EnumPopup("Rarity", settings.rarity, GUILayout.Width(300));
            settings.battlesPerPair = Mathf.Clamp(EditorGUILayout.IntField("Battles per pair", settings.battlesPerPair, GUILayout.Width(260)), 1, 1000);
            settings.element = (CardElement)EditorGUILayout.EnumPopup("Element", settings.element, GUILayout.Width(260));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("RUN", GUILayout.Height(30))) Run();
            using (new EditorGUI.DisabledScope(result == null))
                if (GUILayout.Button("Export CSV...", GUILayout.Height(30), GUILayout.Width(140)))
                {
                    string folder = EditorUtility.SaveFolderPanel("Export talent matrix", "", "TalentMatrix");
                    if (!string.IsNullOrEmpty(folder)) { Export(result, folder); EditorUtility.RevealInFinder(Path.Combine(folder, "talent_ranking.csv")); }
                }
        }
        if (result == null) return;

        EditorGUILayout.LabelField(result.SummaryText(0).Split('\n')[0], EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Width(280));
            foreach (int i in result.Ranking())
            {
                var t = result.talents[i];
                if (GUILayout.Toggle(selected == i, $"{result.Overall(i) * 100f:0.0}%   {t.DisplayName} [{(t.IsActive ? "A" : "P")}]", "Button"))
                    if (selected != i) { selected = i; selectedOpponent = -1; }
            }
            EditorGUILayout.EndScrollView();

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            if (selected >= 0)
            {
                EditorGUILayout.LabelField($"{result.talents[selected].DisplayName} against every talent", EditorStyles.boldLabel);
                foreach (int j in Enumerable.Range(0, result.talents.Count).Where(j => j != selected).OrderByDescending(j => result.Rate(selected, j)))
                {
                    float r = result.Rate(selected, j);
                    var old = GUI.color;
                    GUI.color = r >= 0.6f ? new Color(0.6f, 1f, 0.6f) : r <= 0.4f ? new Color(1f, 0.65f, 0.65f) : Color.white;
                    if (GUILayout.Button($"{r * 100f:0.0}%   vs {result.talents[j].DisplayName}", EditorStyles.miniButton)) selectedOpponent = j;
                    GUI.color = old;
                    if (selectedOpponent == j)
                    {
                        var p = result.Pair(selected, j);
                        if (p != null) EditorGUILayout.SelectableLabel(p.firstLog, EditorStyles.textArea, GUILayout.Height(300));
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }

    void Run()
    {
        if (CardDatabase.Instance == null) return;
        settings.seed = Environment.TickCount & 0x7fffffff;
        try
        {
            result = TalentMatrix.Run(settings, (d, t) =>
            {
                if (d % 20 == 0 && EditorUtility.DisplayCancelableProgressBar("1v1 Talent Matrix", $"Pair {d} / {t}", d / (float)t)) throw new OperationCanceledException();
            });
        }
        catch (OperationCanceledException) { ShowNotification(new GUIContent("Cancelled")); }
        finally { EditorUtility.ClearProgressBar(); }
        selected = result != null ? result.Ranking().FirstOrDefault() : -1;
    }
}
