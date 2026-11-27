using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Game content edited in Test Mode (bosses, minigame prizes), stored as JSON.
/// In the Unity editor the file is Assets/Resources/Config/NAME.json (so it ships with the game);
/// in a development build Test Mode writes an override in persistentDataPath/config; release builds read Resources only.
/// </summary>
public static class JsonConfig
{
    public static string EditorPath(string name) => Path.Combine(Application.dataPath, "Resources", "Config", name + ".json");
    public static string OverridePath(string name) => Path.Combine(Application.persistentDataPath, "config", name + ".json");

    public static T Load<T>(string name, Func<T> defaults) where T : class
    {
        try
        {
            string json = null;
            if (Application.isEditor)
            {
                string p = EditorPath(name);
                if (File.Exists(p)) json = File.ReadAllText(p);
            }
            else
            {
                string o = OverridePath(name);
                if (PlayerProfile.TestModeAvailable && File.Exists(o)) json = File.ReadAllText(o);
                if (json == null)
                {
                    var asset = Resources.Load<TextAsset>("Config/" + name);
                    if (asset != null) json = asset.text;
                }
            }
            if (!string.IsNullOrEmpty(json))
            {
                var value = JsonUtility.FromJson<T>(json);
                if (value != null) return value;
            }
        }
        catch (Exception ex) { Debug.LogWarning($"[AniGame] Could not read config {name}: {ex.Message}"); }
        return defaults();
    }

    /// <summary>Saves the config. Returns where it went (or an error message starting with "Error").</summary>
    public static string Save<T>(string name, T value)
    {
        try
        {
            string json = JsonUtility.ToJson(value, true);
            string path = Application.isEditor ? EditorPath(name) : OverridePath(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.ImportAsset("Assets/Resources/Config/" + name + ".json");
#endif
            return path;
        }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }
}
