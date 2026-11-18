using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Imports / updates Card assets from a CSV (LOC, FLOOR, CARD, IMAGE, RARITY, TALENT, HP, ATK, DEF, SPEED, POWER[, ELEMENT]).
/// - Cards go to Assets/Cards/LOC n/Floor k.asset (k = order inside the location).
/// - Existing assets are UPDATED in place (GUIDs and references stay valid).
/// - Talent names are matched by asset name, talentName, class name and aliases (BerserkStrike, DoubleEdgedStrike...).
/// - ELEMENT column is optional; without it each character gets a stable element derived from its name.
/// </summary>
public class CardImporter : EditorWindow
{
    public const string DefaultCsvPath = "Assets/Data/cards.csv";
    public const string DefaultOutputFolder = "Assets/Cards";
    public const string DefaultImageFolder = "Assets/Cards/Images";

    public class Result
    {
        public int created;
        public int updated;
        public int errors;
        public int missingTalents;
        public int missingImages;
        public readonly List<string> messages = new List<string>();
        public override string ToString() =>
            $"Created {created}, updated {updated}, errors {errors}, cards without talent {missingTalents}, without image {missingImages}";
    }

    TextAsset csvFile;
    string outputFolder = DefaultOutputFolder;
    string imageFolder = DefaultImageFolder;
    bool updateExisting = true;

    /// <summary>CSV talent name (normalized) -> other names to try (normalized).</summary>
    static readonly Dictionary<string, string[]> TalentAliases = new Dictionary<string, string[]>
    {
        { "berserkstrike", new[] { "berserkerstrike" } },
        { "doubleedgedstrike", new[] { "doubleedgestrike" } },
    };

    [MenuItem("Tools/Card Importer")]
    public static void ShowWindow()
    {
        GetWindow<CardImporter>("Card Importer");
    }

    void OnEnable()
    {
        if (csvFile == null) csvFile = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultCsvPath);
    }

    void OnGUI()
    {
        GUILayout.Label("Card CSV Importer", EditorStyles.boldLabel);

        csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvFile, typeof(TextAsset), false);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        imageFolder = EditorGUILayout.TextField("Image Folder", imageFolder);
        updateExisting = EditorGUILayout.Toggle("Update existing assets", updateExisting);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("CSV columns: LOC, FLOOR, CARD, IMAGE (URL), RARITY, TALENT, HP, ATK, DEF, SPEED, POWER, optional ELEMENT.\n" +
                                "HP is multiplied by 10. Existing 'LOC n/Floor k' assets are updated in place.", MessageType.Info);

        if (GUILayout.Button("Import Cards", GUILayout.Height(30)))
        {
            if (csvFile == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a CSV file!", "OK");
                return;
            }
            var result = Import(AssetDatabase.GetAssetPath(csvFile), outputFolder, imageFolder, updateExisting, true);
            Debug.Log($"[CardImporter] {result}");
        }
    }

    // ------------------------------------------------------------------ import

    public static Result Import(string csvPath, string outputFolder, string imageFolder, bool updateExisting, bool showDialog)
    {
        var result = new Result();
        if (!File.Exists(csvPath))
        {
            result.errors++;
            result.messages.Add($"CSV not found: {csvPath}");
            return result;
        }

        var talents = BuildTalentCache();
        var spriteCache = new Dictionary<string, Sprite>();
        var floorCounter = new Dictionary<int, int>();

        string[] lines = File.ReadAllText(csvPath).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length < 2)
        {
            result.errors++;
            result.messages.Add("CSV has no data rows");
            return result;
        }

        var header = ParseCsvLine(lines[0]);
        var col = new Dictionary<string, int>();
        for (int i = 0; i < header.Count; i++) col[header[i].Trim().ToUpperInvariant()] = i;
        int Col(string name, int fallback) => col.TryGetValue(name, out int idx) ? idx : fallback;

        int cLoc = Col("LOC", 0), cFloor = Col("FLOOR", 1), cName = Col("CARD", 2), cRarity = Col("RARITY", 4), cTalent = Col("TALENT", 5);
        int cHp = Col("HP", 6), cAtk = Col("ATK", 7), cDef = Col("DEF", 8), cSpd = Col("SPEED", 9), cPower = Col("POWER", 10);
        int cElement = Col("ELEMENT", -1);

        if (!AssetDatabase.IsValidFolder(imageFolder)) CreateFolderRecursive(imageFolder);

        int total = lines.Length - 1;
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("AniGame - Cards", $"Row {i}/{total}", (float)i / total))
                {
                    result.messages.Add("Import cancelled by user.");
                    break;
                }

                var data = ParseCsvLine(line);
                if (data.Count < 11)
                {
                    result.errors++;
                    result.messages.Add($"Line {i + 1}: Not enough columns ({data.Count}/11)");
                    continue;
                }

                try
                {
                    string locText = data[cLoc].Trim();
                    if (!int.TryParse(locText, out int loc))
                    {
                        result.errors++;
                        result.messages.Add($"Line {i + 1}: LOC '{locText}' is not a number");
                        continue;
                    }
                    loc = (loc - 1) % 100 + 1; // keep inside LOC 1-100

                    floorCounter.TryGetValue(loc, out int floorIndex);
                    floorIndex++;
                    floorCounter[loc] = floorIndex;

                    string cardName = data[cName].Trim();
                    string folder = $"{outputFolder}/LOC {loc}";
                    if (!AssetDatabase.IsValidFolder(folder)) CreateFolderRecursive(folder);
                    string assetPath = $"{folder}/Floor {floorIndex}.asset";

                    Card card = AssetDatabase.LoadAssetAtPath<Card>(assetPath);
                    bool isNew = card == null;
                    if (!isNew && !updateExisting) continue;
                    if (isNew) card = ScriptableObject.CreateInstance<Card>();

                    card.cardName = cardName;
                    card.location = loc;
                    card.floor = floorIndex;
                    card.floorCode = data[cFloor].Trim();
                    card.rarity = ParseRarity(data[cRarity]);
                    card.health = int.Parse(data[cHp].Trim()) * 10;
                    card.attack = int.Parse(data[cAtk].Trim());
                    card.defense = int.Parse(data[cDef].Trim());
                    card.speed = int.Parse(data[cSpd].Trim());
                    card.power = int.Parse(data[cPower].Trim());
                    card.mana = 0;
                    card.maxMana = 100;
                    if (card.critRate <= 0) card.critRate = 5;
                    if (card.critDamage <= 0) card.critDamage = 135;
                    if (card.evasion <= 0) card.evasion = 5;

                    // Element: CSV column > existing non-default value > stable hash of the name
                    CardElement? csvElement = cElement >= 0 && cElement < data.Count ? ParseElement(data[cElement]) : null;
                    if (csvElement.HasValue) card.element = csvElement.Value;
                    else if (isNew || card.element == CardElement.Fire) card.element = ElementSystem.FromName(cardName);

                    // Talent
                    string talentName = data[cTalent].Trim();
                    card.talent = FindTalent(talents, talentName);
                    if (card.talent == null && !string.IsNullOrEmpty(talentName))
                    {
                        result.missingTalents++;
                        if (result.missingTalents <= 10) result.messages.Add($"Talent '{talentName}' not found for '{cardName}'");
                    }

                    // Image
                    if (!spriteCache.TryGetValue(cardName, out Sprite sprite))
                    {
                        sprite = FindExistingSprite(imageFolder, cardName);
                        spriteCache[cardName] = sprite;
                    }
                    if (sprite != null) card.cardImg = sprite;
                    if (card.cardImg == null) result.missingImages++;

                    if (isNew)
                    {
                        AssetDatabase.CreateAsset(card, assetPath);
                        result.created++;
                    }
                    else
                    {
                        EditorUtility.SetDirty(card);
                        result.updated++;
                    }
                }
                catch (Exception e)
                {
                    result.errors++;
                    result.messages.Add($"Line {i + 1}: {e.Message}");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

        foreach (var m in result.messages) Debug.LogWarning($"[CardImporter] {m}");
        if (showDialog)
        {
            string msg = result.ToString();
            if (result.messages.Count > 0) msg += "\n\n" + string.Join("\n", result.messages.GetRange(0, Mathf.Min(15, result.messages.Count)));
            EditorUtility.DisplayDialog("Import Results", msg, "OK");
        }
        return result;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Splits a CSV line, honouring double-quoted fields.</summary>
    public static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Length = 0; }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }

    public static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        return name.Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
    }

    public static Dictionary<string, Talent> BuildTalentCache()
    {
        var cache = new Dictionary<string, Talent>();
        foreach (string guid in AssetDatabase.FindAssets("t:Talent"))
        {
            var talent = AssetDatabase.LoadAssetAtPath<Talent>(AssetDatabase.GUIDToAssetPath(guid));
            if (talent == null) continue;
            foreach (string key in new[] { talent.name, talent.talentName, talent.GetType().Name, talent.DefaultName })
            {
                string k = NormalizeName(key);
                if (!string.IsNullOrEmpty(k) && !cache.ContainsKey(k)) cache[k] = talent;
            }
        }
        return cache;
    }

    public static Talent FindTalent(Dictionary<string, Talent> cache, string csvName)
    {
        string key = NormalizeName(csvName);
        if (string.IsNullOrEmpty(key)) return null;
        if (cache.TryGetValue(key, out Talent t)) return t;
        if (TalentAliases.TryGetValue(key, out string[] aliases))
            foreach (var a in aliases)
                if (cache.TryGetValue(a, out t)) return t;
        return null;
    }

    public static CardRarity ParseRarity(string rarityStr)
    {
        if (string.IsNullOrEmpty(rarityStr)) return CardRarity.Common;
        string normalized = NormalizeName(rarityStr);
        foreach (CardRarity r in Enum.GetValues(typeof(CardRarity)))
            if (r.ToString().ToLowerInvariant() == normalized) return r;
        Debug.LogWarning($"[CardImporter] Unknown rarity: {rarityStr}. Defaulting to Common.");
        return CardRarity.Common;
    }

    /// <summary>Accepts this project's six elements plus Anigame names (ground/grass -> Earth, electric -> Air).</summary>
    public static CardElement? ParseElement(string value)
    {
        switch (NormalizeName(value))
        {
            case "fire": return CardElement.Fire;
            case "water": return CardElement.Water;
            case "earth": case "ground": case "grass": return CardElement.Earth;
            case "air": case "wind": case "electric": case "lightning": return CardElement.Air;
            case "light": return CardElement.Light;
            case "dark": return CardElement.Dark;
            default: return null;
        }
    }

    static Sprite FindExistingSprite(string imageFolder, string cardName)
    {
        string safeName = GetSafeFileName(cardName);
        string[] extensions = { ".png", ".jpg", ".jpeg", ".webp" };
        foreach (string ext in extensions)
        {
            string path = $"{imageFolder}/{safeName}{ext}";
            if (!File.Exists(path)) continue;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite) return sprite;
        }
        return null;
    }

    static string GetSafeFileName(string name)
    {
        return name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
    }

    public static void CreateFolderRecursive(string path)
    {
        string[] folders = path.Split('/');
        string currentPath = folders[0];
        for (int i = 1; i < folders.Length; i++)
        {
            string newPath = currentPath + "/" + folders[i];
            if (!AssetDatabase.IsValidFolder(newPath)) AssetDatabase.CreateFolder(currentPath, folders[i]);
            currentPath = newPath;
        }
    }
}
