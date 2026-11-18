using UnityEngine;
using UnityEditor;
using System.IO;
using System.Net;
using System.Collections.Generic;

public class CardImporter : EditorWindow
{
    private TextAsset csvFile;
    private string outputFolder = "Assets/Cards";
    private string imageFolder = "Assets/Cards/Images";
    private bool organizeBySection = true;

    // Caches to avoid re-searching every row
    private Dictionary<string, Talent> talentCache = new Dictionary<string, Talent>();
    private Dictionary<string, Sprite> downloadedSpriteCache = new Dictionary<string, Sprite>();
    private Dictionary<string, int> nameCounter = new Dictionary<string, int>();

    [MenuItem("Tools/Card Importer")]
    public static void ShowWindow()
    {
        GetWindow<CardImporter>("Card Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Card CSV Importer", EditorStyles.boldLabel);

        csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvFile, typeof(TextAsset), false);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        imageFolder = EditorGUILayout.TextField("Image Folder", imageFolder);
        organizeBySection = EditorGUILayout.Toggle("Organize by LOC/Floor", organizeBySection);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("CSV Format: LOC, FLOOR, CARD, IMAGE (URL), RARITY, Talent, HP, ATK, DEF, SPEED, POWER", MessageType.Info);

        if (GUILayout.Button("Import Cards", GUILayout.Height(30)))
        {
            ImportCards();
        }
    }

    private void ImportCards()
    {
        if (csvFile == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a CSV file!", "OK");
            return;
        }

        BuildTalentCache();
        nameCounter.Clear();

        // Ensure image folder exists
        if (!AssetDatabase.IsValidFolder(imageFolder))
        {
            CreateFolderRecursive(imageFolder);
        }

        string[] lines = csvFile.text.Split('\n');
        int totalLines = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(lines[i].Trim())) totalLines++;
        }

        int successCount = 0;
        int errorCount = 0;
        int processed = 0;
        List<string> errors = new List<string>();

        // --- Phase 2: Create card assets in batch ---
        AssetDatabase.StartAssetEditing();

        try
        {
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                processed++;

                bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                    "Creating Cards",
                    $"Processing card {processed}/{totalLines}...",
                    (float)processed / totalLines
                );

                if (cancelled)
                {
                    Debug.LogWarning("Card import cancelled by user.");
                    break;
                }

                string[] data = line.Split(',');

                if (data.Length < 11)
                {
                    errors.Add($"Line {i + 1}: Not enough columns ({data.Length}/11)");
                    errorCount++;
                    continue;
                }

                try
                {
                    string loc = data[0].Trim();          // A: LOC
                    string floor = data[1].Trim();         // B: FLOOR
                    string cardName = data[2].Trim();      // C: CARD
                    string imageUrl = data[3].Trim();      // D: IMAGE (URL)
                    string rarity = data[4].Trim();        // E: RARITY
                    string talentName = data[5].Trim();    // F: Talent
                    int hp = int.Parse(data[6].Trim()) * 10;    // G: HP
                    int atk = int.Parse(data[7].Trim());   // H: ATK
                    int def = int.Parse(data[8].Trim());   // I: DEF
                    int speed = int.Parse(data[9].Trim()); // J: SPEED
                    int power = int.Parse(data[10].Trim());// K: POWER

                    Card card = CreateCard(loc, floor, cardName, imageUrl, rarity, talentName, hp, atk, def, speed, power);

                    if (card != null)
                    {
                        successCount++;
                    }
                    else
                    {
                        errors.Add($"Line {i + 1}: Failed to create card '{cardName}'");
                        errorCount++;
                    }
                }
                catch (System.Exception e)
                {
                    errors.Add($"Line {i + 1}: {e.Message}");
                    errorCount++;
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

        string message = $"Import Complete!\n\nCards: {successCount}\nErrors: {errorCount}";
        if (errors.Count > 0)
        {
            int maxShow = Mathf.Min(errors.Count, 20);
            message += $"\n\nFirst {maxShow} errors:\n" + string.Join("\n", errors.GetRange(0, maxShow).ToArray());
            if (errors.Count > maxShow)
                message += $"\n... and {errors.Count - maxShow} more errors (see Console)";

            foreach (string err in errors)
                Debug.LogError($"[CardImporter] {err}");
        }

        EditorUtility.DisplayDialog("Import Results", message, "OK");
    }

    private Sprite FindExistingSprite(string cardName)
    {
        string safeName = GetSafeFileName(cardName);
        string[] extensions = { ".png", ".jpg", ".jpeg", ".webp" };

        foreach (string ext in extensions)
        {
            string path = $"{imageFolder}/{safeName}{ext}";
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite)
                {
                    return sprite;
                }
            }
        }

        return null;
    }

    private string GetSafeFileName(string name)
    {
        return name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
    }

    private string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        return name.Replace(" ", "").Replace("-", "").Replace("_", "").ToLower();
    }

    private void BuildTalentCache()
    {
        talentCache.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Talent");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Talent talent = AssetDatabase.LoadAssetAtPath<Talent>(path);
            if (talent != null)
            {
                string normalized = NormalizeName(talent.name);
                if (!talentCache.ContainsKey(normalized))
                {
                    talentCache[normalized] = talent;
                }
            }
        }
        Debug.Log($"[CardImporter] Cached {talentCache.Count} talents.");
    }

    private CardRarity ParseRarity(string rarityStr)
    {
        if (string.IsNullOrEmpty(rarityStr)) return CardRarity.Common;
        string normalized = rarityStr.Replace(" ", "").ToLower();
        foreach (CardRarity r in System.Enum.GetValues(typeof(CardRarity)))
        {
            if (r.ToString().ToLower() == normalized) return r;
        }
        Debug.LogWarning($"[CardImporter] Unknown rarity: {rarityStr}. Defaulting to Common.");
        return CardRarity.Common;
    }

    private Card CreateCard(string loc, string floor, string cardName, string imageUrl, string rarity,
                           string talentName, int hp, int atk, int def, int speed, int power)
    {
        // Lookup talent from cache
        Talent talent = null;
        if (!string.IsNullOrEmpty(talentName))
        {
            string key = NormalizeName(talentName);
            if (!talentCache.TryGetValue(key, out talent))
            {
                Debug.LogWarning($"Talent '{talentName}' not found for card '{cardName}'.");
            }
        }

        // Lookup sprite locally
        Sprite sprite = FindExistingSprite(cardName);

        Card card = ScriptableObject.CreateInstance<Card>();
        card.cardName = cardName;
        card.cardImg = sprite;
        card.rarity = ParseRarity(rarity);
        card.talent = talent;
        card.health = hp;
        card.attack = atk;
        card.defense = def;
        card.speed = speed;
        card.mana = 0; // Initial mana 0
        card.maxMana = 100;
        card.power = power;

        // Use sequential 'Floor X' naming within each LOC folder
        string locKey = loc.ToLower();
        if (!nameCounter.ContainsKey(locKey))
            nameCounter[locKey] = 0;
        nameCounter[locKey]++;
        string assetFileName = $"Floor {nameCounter[locKey]}";

        // Determine save folder: Map LOC to 1-100 range
        string folderPath;
        if (organizeBySection)
        {
            int locValue;
            if (int.TryParse(loc, out locValue))
            {
                // Force wrap into 1-100 range if it's a numeric ID exceeding 100
                int mappedLoc = (locValue - 1) % 100 + 1;
                folderPath = $"{outputFolder}/LOC {mappedLoc}";
            }
            else
            {
                folderPath = $"{outputFolder}/LOC {loc}";
            }
        }
        else
        {
            folderPath = outputFolder;
        }

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            CreateFolderRecursive(folderPath);
        }

        string assetPath = $"{folderPath}/{assetFileName}.asset";

        AssetDatabase.CreateAsset(card, assetPath);

        return card;
    }

    private void CreateFolderRecursive(string path)
    {
        string[] folders = path.Split('/');
        string currentPath = folders[0];

        for (int i = 1; i < folders.Length; i++)
        {
            string newPath = currentPath + "/" + folders[i];
            if (!AssetDatabase.IsValidFolder(newPath))
            {
                AssetDatabase.CreateFolder(currentPath, folders[i]);
            }
            currentPath = newPath;
        }
    }
}
