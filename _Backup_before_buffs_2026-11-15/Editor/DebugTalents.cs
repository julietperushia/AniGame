using UnityEngine;
using UnityEditor;

public class DebugTalents
{
    [MenuItem("Tools/Debug Talents")]
    public static void DebugT()
    {
        string[] guids = AssetDatabase.FindAssets("t:Talent");
        Debug.Log($"Found {guids.Length} talents");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Talent talent = AssetDatabase.LoadAssetAtPath<Talent>(path);
            if (talent != null)
            {
                Debug.Log($"Name: {talent.name}, Normalized: {NormalizeName(talent.name)}");
            }
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        return name.Replace(" ", "").Replace("-", "").Replace("_", "").ToLower();
    }
}
