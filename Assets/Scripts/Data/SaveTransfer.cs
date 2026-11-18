using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Everything one player needs to continue on another PC, in one file.</summary>
[Serializable]
public class SaveExport
{
    public string format = SaveTransfer.Format;
    public int formatVersion = SaveTransfer.FormatVersion;
    public int saveVersion;
    public string gameVersion;
    public string dataVersion;
    public string exportedAt;      // UTC, ISO 8601
    public string characterName;
    public int cardCount;
    public int highestLocation;
    public SaveData save;          // cards, builds, slots, buffs, currencies, orbs, stamina, progress, stats, settings, character
    public string portraitPng;     // the imported character picture (base64 PNG), empty when using a gallery portrait
    public string hash;            // HMAC of everything above: edited files are refused
}

/// <summary>
/// Export / import of the whole save as a single .anisave file (JSON): move your progress to another PC and it plays
/// exactly as before. Importing backs up the current save first.
/// </summary>
public static class SaveTransfer
{
    public const string Format = "AniGameSave";
    public const int FormatVersion = 2; // 2 = signed
    public const string Extension = "anisave";

    public static string ExportFolder => Path.Combine(Application.persistentDataPath, "Exports");

    public static string DefaultFileName
    {
        get
        {
            string name = new string(PlayerProfile.CharacterName.Where(char.IsLetterOrDigit).ToArray());
            if (name.Length == 0) name = "Hero";
            return $"AniGame_{name}_{DateTime.Now:yyyyMMdd_HHmm}.{Extension}";
        }
    }

    /// <summary>Writes the save to <paramref name="path"/> (or the Exports folder when null). Returns an error or null.</summary>
    public static string Export(string path, out string writtenPath)
    {
        writtenPath = null;
        try
        {
            PlayerProfile.Save();
            var d = PlayerProfile.Data;
            var export = new SaveExport
            {
                saveVersion = PlayerProfile.SaveVersion,
                gameVersion = Application.version,
                dataVersion = d.dataVersion,
                exportedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                characterName = PlayerProfile.CharacterName,
                cardCount = d.cards.Count,
                highestLocation = PlayerProfile.HighestUnlockedLocation,
                save = d,
                portraitPng = d.characterSource == "file" && File.Exists(PlayerProfile.PortraitPath)
                    ? Convert.ToBase64String(File.ReadAllBytes(PlayerProfile.PortraitPath)) : ""
            };
            if (string.IsNullOrEmpty(path))
            {
                Directory.CreateDirectory(ExportFolder);
                path = Path.Combine(ExportFolder, DefaultFileName);
            }
            if (!path.EndsWith("." + Extension, StringComparison.OrdinalIgnoreCase)) path += "." + Extension;
            export.hash = "";
            export.hash = PlayerProfile.Hash(JsonUtility.ToJson(export, true));
            File.WriteAllText(path, JsonUtility.ToJson(export, true));
            writtenPath = path;
            return null;
        }
        catch (Exception ex)
        {
            return "Export failed: " + ex.Message;
        }
    }

    /// <summary>Reads and checks a save file without applying it.</summary>
    public static string Peek(string path, out SaveExport export)
    {
        export = null;
        path = (path ?? "").Trim().Trim('"');
        if (path.Length == 0) return "No file chosen";
        if (!File.Exists(path)) return "File not found: " + path;
        try { export = JsonUtility.FromJson<SaveExport>(File.ReadAllText(path)); }
        catch (Exception ex) { return "Not an AniGame save file (" + ex.Message + ")"; }
        if (export == null || export.format != Format || export.save == null) return "Not an AniGame save file";
        if (export.formatVersion > FormatVersion) return "This save comes from a newer version of the game. Update the game first";
        if (export.formatVersion < 2 || string.IsNullOrEmpty(export.hash)) return "This file was exported before save protection was added. Export it again from the old PC";
        string stored = export.hash;
        export.hash = "";
        bool valid = PlayerProfile.Hash(JsonUtility.ToJson(export, true)) == stored;
        export.hash = stored;
        if (!valid) return "This save file was edited or damaged, so it can't be imported";
        if (export.saveVersion != PlayerProfile.SaveVersion || export.save.version != PlayerProfile.SaveVersion)
            return $"This save uses an old save format (v{export.saveVersion}); it can't be loaded by this version";
        return null;
    }

    /// <summary>One-line description of a save file for the confirmation step.</summary>
    public static string Describe(SaveExport e)
    {
        if (e == null || e.save == null) return "";
        var s = e.save;
        string date = DateTime.TryParse(e.exportedAt, out var dt) ? dt.ToLocalTime().ToString("dd MMM yyyy HH:mm") : e.exportedAt;
        return $"<b>{e.characterName}</b>  |  {e.cardCount} cards  |  LOC {e.highestLocation}  |  {s.gold:N0} gold, {s.diamonds:N0} diamonds  |  {s.buffs?.Count ?? 0} buffs  |  exported {date}";
    }

    /// <summary>Replaces the current save with the file. The current save is backed up first. Returns an error or null; <paramref name="warning"/> may be set.</summary>
    public static string Import(string path, out string warning)
    {
        warning = null;
        string err = Peek(path, out var export);
        if (err != null) return err;
        try
        {
            PlayerProfile.BackupCurrentSave("before_import");
            var save = export.save;
            var db = CardDatabase.Instance;
            string current = db != null ? db.dataVersion : null;
            if (!string.IsNullOrEmpty(current) && save.dataVersion != current)
            {
                warning = "This save was made with a different campaign sheet; your cards and buffs are kept, but floor progress may not match the new LOCs.";
                save.dataVersion = current;
            }

            if (!string.IsNullOrEmpty(export.portraitPng))
                File.WriteAllBytes(PlayerProfile.PortraitPath, Convert.FromBase64String(export.portraitPng));
            else if (save.characterSource == "file")
                warning = (warning != null ? warning + " " : "") + "The character picture was not in the file; pick it again on the Character screen.";

            PlayerProfile.ApplyImportedSave(save); // validates, re-signs and saves
            return null;
        }
        catch (Exception ex)
        {
            return "Import failed: " + ex.Message;
        }
    }

    public static void OpenSaveFolder()
    {
        Directory.CreateDirectory(ExportFolder);
        Application.OpenURL("file:///" + Application.persistentDataPath.Replace('\\', '/'));
    }
}
