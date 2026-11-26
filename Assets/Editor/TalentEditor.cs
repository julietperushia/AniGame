using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for every talent asset: the numbers first, then the description with its sync status
/// (green = generated from the numbers and updating live, orange = custom text that doesn't follow them).
/// </summary>
[CustomEditor(typeof(Talent), true)]
[CanEditMultipleObjects]
public class TalentEditor : Editor
{
    static readonly string[] Hidden = { "m_Script", "talentName", "autoDescription", "description" };
    GUIStyle wrapped;
    bool showGenerated;
    static CardRarity previewRarity = CardRarity.God;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var t = (Talent)target;
        if (wrapped == null) wrapped = new GUIStyle(EditorStyles.textArea) { wordWrap = true, fontSize = 12 };

        // ---- header
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(t.DisplayName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(t.IsActive ? "ACTIVE" : "PASSIVE", EditorStyles.miniBoldLabel);
            GUILayout.Label(TalentCatalog.Name(t.Category), EditorStyles.miniLabel);
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("talentName"), new GUIContent("Name"));

        // ---- numbers
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Numbers", EditorStyles.boldLabel);
        var it = serializedObject.GetIterator();
        bool any = false;
        for (bool enter = true; it.NextVisible(enter); enter = false)
        {
            if (Hidden.Contains(it.name)) continue;
            EditorGUILayout.PropertyField(it, true);
            any = true;
        }
        if (!any) EditorGUILayout.LabelField("This talent has no numbers.", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Some numbers grow with the card's rarity (x1.00 at Common up to x1.20 at God): the description shows them as \"base (Max God value)\". The others never change.",
                                   EditorStyles.wordWrappedMiniLabel);

        // ---- description
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
        var auto = serializedObject.FindProperty("autoDescription");
        var desc = serializedObject.FindProperty("description");
        EditorGUILayout.PropertyField(auto, new GUIContent("Auto description", auto.tooltip));
        bool applied = serializedObject.ApplyModifiedProperties(); // OnValidate rewrites the text from the numbers
        if (applied) serializedObject.Update();

        if (targets.Length > 1)
        {
            EditorGUILayout.HelpBox("Several talents selected: select one to see its description.", MessageType.None);
            return;
        }

        if (t.autoDescription)
        {
            var old = GUI.backgroundColor;
            EditorGUILayout.HelpBox(t.DescriptionInSync
                ? "SYNCED - this text is written from the numbers above and changes as soon as you edit one. The Library shows exactly this; a card shows the same text with its own numbers for its rarity (preview below)."
                : "Will sync on the next change or save (the text below is the live version).", MessageType.Info);
            GUI.backgroundColor = new Color(0.75f, 1f, 0.78f);
            EditorGUILayout.SelectableLabel(t.GeneratedDescription, wrapped, GUILayout.MinHeight(70), GUILayout.ExpandHeight(false),
                                            GUILayout.Height(wrapped.CalcHeight(new GUIContent(t.GeneratedDescription), EditorGUIUtility.currentViewWidth - 40) + 8));
            GUI.backgroundColor = old;
            previewRarity = (CardRarity)EditorGUILayout.EnumPopup("Card of rarity", previewRarity);
            EditorGUILayout.HelpBox(t.DescriptionFor(previewRarity), MessageType.None);
            if (!t.DescriptionInSync && GUILayout.Button("Sync now"))
            {
                Undo.RecordObject(t, "Sync talent description");
                t.SyncDescription();
                EditorUtility.SetDirty(t);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("CUSTOM TEXT - NOT synced. Changing the numbers above will NOT change this text, and cards show it as it is at every rarity. Turn Auto description on to make it follow the numbers again.",
                                    MessageType.Warning);
            EditorGUI.BeginChangeCheck();
            string text = EditorGUILayout.TextArea(desc.stringValue, wrapped, GUILayout.MinHeight(70));
            if (EditorGUI.EndChangeCheck()) { desc.stringValue = text; serializedObject.ApplyModifiedProperties(); }
            showGenerated = EditorGUILayout.Foldout(showGenerated, "What the automatic text would say", true);
            if (showGenerated) EditorGUILayout.HelpBox(t.GeneratedDescription, MessageType.None);
            if (GUILayout.Button("Use the automatic description"))
            {
                Undo.RecordObject(t, "Auto talent description");
                t.autoDescription = true;
                t.SyncDescription();
                EditorUtility.SetDirty(t);
            }
        }
    }
}

/// <summary>Tools > AniGame > Sync Talent Descriptions: writes the automatic text into every talent asset and reports any that aren't synced.</summary>
public static class TalentDescriptionSync
{
    [MenuItem("Tools/AniGame/Sync Talent Descriptions")]
    static void Menu() => Debug.Log(SyncAll());

    public static string SyncAll()
    {
        int total = 0, auto = 0, custom = 0;
        var customNames = new System.Collections.Generic.List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Talent"))
        {
            var t = AssetDatabase.LoadAssetAtPath<Talent>(AssetDatabase.GUIDToAssetPath(guid));
            if (t == null) continue;
            total++;
            if (!t.autoDescription) { custom++; customNames.Add(t.DisplayName); continue; }
            t.SyncDescription();
            EditorUtility.SetDirty(t); // always save: text changed in memory on load isn't marked dirty by Unity
            auto++;
        }
        AssetDatabase.SaveAssets();
        return $"Talent descriptions: {total} talents, {auto} synced with their numbers, {custom} with custom text" +
               (custom > 0 ? $" ({string.Join(", ", customNames)})" : "") + ".";
    }
}
