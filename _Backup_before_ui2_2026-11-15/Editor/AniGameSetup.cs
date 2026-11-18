using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// One-click project setup: Tools > AniGame > Build Everything.
///  1. Repairs talent assets (creates missing ones, fills empty names/descriptions)
///  2. Campaign Generator: Assets/Data/campaign.csv -> Assets/Campaign/LOC n/Floor k + portraits
///     (falls back to the legacy Assets/Data/cards.csv -> Assets/Cards importer when there is no campaign sheet)
///  3. Builds Assets/Resources/CardDatabase.asset
///  4. Generates UI sprites + anime placeholder art (overridable from Assets/Art/UserArt) + Assets/Resources/UITheme.asset
///  5. Builds MainMenu / LocationMap / Collection (My Cards) / Gacha / Shop / Battle scenes and the build settings
/// Every step can be run on its own and is safe to re-run.
/// </summary>
public static class AniGameSetup
{
    const string Menu = "Tools/AniGame/";
    const string ScenesFolder = "Assets/Scenes";
    const string ResourcesFolder = "Assets/Resources";
    const string ArtFolder = "Assets/Art/UI";
    const string TalentFolder = "Assets/Scriptable Objects/Talent";
    public const string ReportPath = "Logs/AniGame_Report.txt";

    static readonly string[] SceneOrder = { SceneNames.MainMenu, SceneNames.Character, SceneNames.LocationMap, SceneNames.Collection, SceneNames.Gacha, SceneNames.Shop, SceneNames.Buffs, SceneNames.Library, SceneNames.Battle };

    // ------------------------------------------------------------------ menu

    [MenuItem(Menu + "Build Everything", priority = 0)]
    public static void BuildEverythingMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string report = BuildEverything();
        EditorUtility.DisplayDialog("AniGame", report + "\n\nPress Play in the MainMenu scene to start.", "Let's go");
    }

    /// <summary>Runs every step. Returns a short report (also written to Logs/AniGame_Report.txt).</summary>
    public static string BuildEverything()
    {
        var sb = new StringBuilder();
        try
        {
            EditorUtility.DisplayProgressBar("AniGame", "Repairing talents...", 0.05f);
            sb.AppendLine(RepairTalents());

            if (CampaignGenerator.HasSheet)
            {
                // New pipeline: Assets/Data/campaign.csv -> Assets/Campaign (+ portraits + database)
                var campaign = CampaignGenerator.Generate(new CampaignGenerator.Options());
                sb.AppendLine(campaign.ToString());
            }
            else
            {
                // Legacy pipeline: Assets/Data/cards.csv -> Assets/Cards
                EditorUtility.DisplayProgressBar("AniGame", "Updating cards from CSV...", 0.15f);
                sb.AppendLine(RepairCards());
                EditorUtility.DisplayProgressBar("AniGame", "Building card database...", 0.6f);
                sb.AppendLine(BuildCardDatabase());
            }

            EditorUtility.DisplayProgressBar("AniGame", "Generating UI art...", 0.7f);
            sb.AppendLine(GenerateUiArt());

            EditorUtility.DisplayProgressBar("AniGame", "Building scenes...", 0.85f);
            sb.AppendLine(BuildScenes());
            sb.AppendLine(ApplyProjectSettings());
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAILED: " + ex);
            Debug.LogException(ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        string report = sb.ToString().Trim();
        WriteReport("Build Everything", report);
        Debug.Log("[AniGame] " + report);
        return report;
    }

    [MenuItem(Menu + "Steps/1. Repair Talent Assets", priority = 20)]
    static void RepairTalentsMenu() => Debug.Log("[AniGame] " + RepairTalents());

    [MenuItem(Menu + "Steps/2. Update Cards From Legacy CSV (cards.csv)", priority = 21)]
    static void RepairCardsMenu() => Debug.Log("[AniGame] " + RepairCards());

    [MenuItem(Menu + "Steps/3. Build Card Database", priority = 22)]
    static void BuildDatabaseMenu() => Debug.Log("[AniGame] " + BuildCardDatabase());

    [MenuItem(Menu + "Steps/4. Generate UI Art", priority = 23)]
    static void GenerateArtMenu() => Debug.Log("[AniGame] " + GenerateUiArt());

    [MenuItem(Menu + "Steps/5. Build Scenes", priority = 24)]
    static void BuildScenesMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log("[AniGame] " + BuildScenes());
    }

    [MenuItem(Menu + "Simulate Balance", priority = 40)]
    static void SimulateMenu()
    {
        string report = SimulateBalance();
        Debug.Log(report);
        EditorUtility.DisplayDialog("AniGame balance", report.Length > 1500 ? report.Substring(0, 1500) + "\n..." : report, "OK");
    }

    [MenuItem(Menu + "Open Main Menu Scene", priority = 60)]
    static void OpenMainMenu()
    {
        string path = $"{ScenesFolder}/{SceneNames.MainMenu}.unity";
        if (File.Exists(path) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
    }

    [MenuItem(Menu + "Save Data/Delete Save", priority = 80)]
    static void DeleteSave()
    {
        if (!EditorUtility.DisplayDialog("AniGame", "Delete the save file?\n" + PlayerProfile.SavePath, "Delete", "Cancel")) return;
        PlayerProfile.DeleteSave();
        Debug.Log("[AniGame] Save deleted.");
    }

    [MenuItem(Menu + "Save Data/Refill Stamina + 10,000 Gold (cheat)", priority = 81)]
    static void Cheat()
    {
        PlayerProfile.Data.gold += 10000;
        PlayerProfile.RefillStamina();
        Debug.Log("[AniGame] Stamina refilled, +10,000 gold.");
    }

    [MenuItem(Menu + "Save Data/Export Save File...", priority = 83)]
    static void ExportSaveMenu()
    {
        string path = EditorUtility.SaveFilePanel("Export AniGame save", "", SaveTransfer.DefaultFileName, SaveTransfer.Extension);
        if (string.IsNullOrEmpty(path)) return;
        string err = SaveTransfer.Export(path, out string written);
        EditorUtility.DisplayDialog("AniGame", err ?? "Save exported to\n" + written, "OK");
    }

    [MenuItem(Menu + "Save Data/Import Save File...", priority = 84)]
    static void ImportSaveMenu()
    {
        string path = EditorUtility.OpenFilePanel("Import AniGame save", "", SaveTransfer.Extension);
        if (string.IsNullOrEmpty(path)) return;
        string err = SaveTransfer.Peek(path, out var info);
        if (err != null) { EditorUtility.DisplayDialog("AniGame", err, "OK"); return; }
        if (!EditorUtility.DisplayDialog("AniGame", "Replace the current save with this one? (a backup is kept)\n\n" + System.Text.RegularExpressions.Regex.Replace(SaveTransfer.Describe(info), "<.*?>", ""), "Import", "Cancel")) return;
        err = SaveTransfer.Import(path, out string warning);
        EditorUtility.DisplayDialog("AniGame", err ?? ("Save imported." + (warning != null ? "\n\n" + warning : "")), "OK");
    }

    [MenuItem(Menu + "Save Data/Show Save File", priority = 82)]
    static void ShowSave()
    {
        if (File.Exists(PlayerProfile.SavePath)) EditorUtility.RevealInFinder(PlayerProfile.SavePath);
        else Debug.Log("[AniGame] No save yet: " + PlayerProfile.SavePath);
    }

    /// <summary>Auto-battles should keep running when the window loses focus (also in the editor's Play mode).</summary>
    public static string ApplyProjectSettings()
    {
        PlayerSettings.runInBackground = true;
        if (string.IsNullOrEmpty(PlayerSettings.productName) || PlayerSettings.productName == "New Unity Project")
            PlayerSettings.productName = "AniGame";
        AssetDatabase.SaveAssets();
        return "Project settings: Run In Background enabled.";
    }

    // ------------------------------------------------------------------ 1. talents

    public static string RepairTalents()
    {
        var types = TypeCache.GetTypesDerivedFrom<Talent>().Where(t => !t.IsAbstract && !t.IsGenericType).ToList();
        var existing = new List<Talent>();
        foreach (string guid in AssetDatabase.FindAssets("t:Talent"))
        {
            var t = AssetDatabase.LoadAssetAtPath<Talent>(AssetDatabase.GUIDToAssetPath(guid));
            if (t != null) existing.Add(t);
        }

        int created = 0, filled = 0;
        foreach (var type in types)
        {
            if (existing.Any(t => t.GetType() == type)) continue;
            var talent = (Talent)ScriptableObject.CreateInstance(type);
            string folder = $"{TalentFolder}/{(talent.IsActive ? "Active" : "Passive")}";
            CardImporter.CreateFolderRecursive(folder);
            talent.talentName = talent.DefaultName;
            talent.description = talent.DefaultDescription;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{talent.DefaultName}.asset");
            AssetDatabase.CreateAsset(talent, path);
            existing.Add(talent);
            created++;
        }

        foreach (var t in existing)
        {
            bool dirty = false;
            if (string.IsNullOrEmpty(t.talentName)) { t.talentName = t.DefaultName; dirty = true; }
            if (string.IsNullOrEmpty(t.description)) { t.description = t.DefaultDescription; dirty = true; }
            if (dirty) { EditorUtility.SetDirty(t); filled++; }
        }
        AssetDatabase.SaveAssets();
        return $"Talents: {existing.Count} assets ({created} created, {filled} filled in).";
    }

    // ------------------------------------------------------------------ 2. cards

    public static string RepairCards()
    {
        if (!File.Exists(CardImporter.DefaultCsvPath)) return $"Cards: {CardImporter.DefaultCsvPath} not found, skipped.";
        var r = CardImporter.Import(CardImporter.DefaultCsvPath, CardImporter.DefaultOutputFolder, CardImporter.DefaultImageFolder, true, false);
        return "Cards: " + r;
    }

    // ------------------------------------------------------------------ 3. database

    public static string BuildCardDatabase()
    {
        if (AssetDatabase.IsValidFolder(CampaignGenerator.CampaignFolder)) return CampaignGenerator.RebuildDatabaseFromAssets();
        CardImporter.CreateFolderRecursive(ResourcesFolder);
        string path = $"{ResourcesFolder}/CardDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(path);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<CardDatabase>();
            AssetDatabase.CreateAsset(db, path);
        }

        var byLocation = new Dictionary<int, List<Card>>();
        foreach (string guid in AssetDatabase.FindAssets("t:Card", new[] { CardImporter.DefaultOutputFolder }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var card = AssetDatabase.LoadAssetAtPath<Card>(assetPath);
            if (card == null) continue;
            int loc = card.location, floor = card.floor;
            if (loc <= 0 && !TryParsePlacement(assetPath, out loc, out floor)) continue;
            if (card.location <= 0)
            {
                card.location = loc;
                card.floor = floor;
                EditorUtility.SetDirty(card);
            }
            if (!byLocation.TryGetValue(loc, out var list)) byLocation[loc] = list = new List<Card>();
            list.Add(card);
        }

        db.locations.Clear();
        int cards = 0;
        for (int loc = 1; byLocation.ContainsKey(loc); loc++)
        {
            var floors = byLocation[loc].OrderBy(c => c.floor).ToList();
            db.locations.Add(new CardDatabase.LocationData { number = loc, displayName = $"LOC {loc}", floors = floors });
            cards += floors.Count;
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        return $"Database: {db.locations.Count} locations, {cards} cards.";
    }

    static bool TryParsePlacement(string assetPath, out int loc, out int floor)
    {
        loc = floor = 0;
        string dir = Path.GetFileName(Path.GetDirectoryName(assetPath) ?? "");
        string file = Path.GetFileNameWithoutExtension(assetPath);
        return dir.StartsWith("LOC ") && file.StartsWith("Floor ")
               && int.TryParse(dir.Substring(4), out loc) && int.TryParse(file.Substring(6), out floor);
    }

    // ------------------------------------------------------------------ 4. UI art

    public static string GenerateUiArt()
    {
        CardImporter.CreateFolderRecursive(ArtFolder);
        CardImporter.CreateFolderRecursive(ResourcesFolder);

        var rounded = MakeSprite("ui_rounded.png", RoundedRect(64, 22f), 26);
        var roundedSmall = MakeSprite("ui_rounded_small.png", RoundedRect(32, 8f), 10);
        var circle = MakeSprite("ui_circle.png", Circle(128), 0);
        var glow = MakeSprite("ui_glow.png", Glow(256), 0);
        var gradient = MakeSprite("ui_gradient.png", VerticalGradient(8, 256), 0);
        var vignette = MakeSprite("ui_vignette.png", Vignette(256), 0);

        string path = $"{ResourcesFolder}/UITheme.asset";
        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(path);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UITheme>();
            AssetDatabase.CreateAsset(theme, path);
        }
        theme.rounded = rounded;
        theme.roundedSmall = roundedSmall;
        theme.circle = circle;
        theme.glow = glow;
        theme.gradient = gradient;
        theme.vignette = vignette;
        string anime = AnimeArtGenerator.Generate(theme);
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        return "UI art: 6 base sprites + UITheme generated. " + anime;
    }

    static Sprite MakeSprite(string fileName, Texture2D texture, int border)
    {
        string path = $"{ArtFolder}/{fileName}";
        File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Texture2D NewTexture(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

    static Texture2D RoundedRect(int size, float radius)
    {
        var tex = NewTexture(size, size);
        var px = new Color[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f));
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Texture2D Circle(int size)
    {
        var tex = NewTexture(size, size);
        var px = new Color[size * size];
        float r = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f));
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Texture2D Glow(int size)
    {
        var tex = NewTexture(size, size);
        var px = new Color[size * size];
        float r = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color(1, 1, 1, a * a * (3f - 2f * a));
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Texture2D VerticalGradient(int w, int h)
    {
        var tex = NewTexture(w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float a = (float)y / (h - 1); // bottom transparent -> top opaque
            a = a * a;
            for (int x = 0; x < w; x++) px[y * w + x] = new Color(1, 1, 1, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Texture2D Vignette(int size)
    {
        var tex = NewTexture(size, size);
        var px = new Color[size * size];
        float r = size / 2f;
        float maxD = Mathf.Sqrt(2f) * r;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / maxD;
                float a = Mathf.Clamp01((d - 0.45f) / 0.55f);
                px[y * size + x] = new Color(0, 0, 0, a * a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------ 5. scenes

    public static string BuildScenes()
    {
        if (UITheme.Instance == null) GenerateUiArt();
        CardImporter.CreateFolderRecursive(ScenesFolder);

        BuildScene(SceneNames.MainMenu, c => MainMenuScreen.BuildLayout(c));
        BuildScene(SceneNames.LocationMap, c => LocationMapScreen.BuildLayout(c));
        BuildScene(SceneNames.Collection, c => CollectionScreen.BuildLayout(c));
        BuildScene(SceneNames.Gacha, c => GachaScreen.BuildLayout(c));
        BuildScene(SceneNames.Shop, c => ShopScreen.BuildLayout(c));
        BuildScene(SceneNames.Character, c => CharacterScreen.BuildLayout(c));
        BuildScene(SceneNames.Buffs, c => BuffsScreen.BuildLayout(c));
        BuildScene(SceneNames.Library, c => LibraryScreen.BuildLayout(c));
        BuildScene(SceneNames.Battle, c => BattleScreen.BuildLayout(c));

        // Build settings: our scenes first, keep any other scenes that were already listed.
        var list = new List<EditorBuildSettingsScene>();
        foreach (var name in SceneOrder) list.Add(new EditorBuildSettingsScene($"{ScenesFolder}/{name}.unity", true));
        foreach (var s in EditorBuildSettings.scenes)
            if (!list.Any(x => x.path == s.path)) list.Add(new EditorBuildSettingsScene(s.path, false));
        EditorBuildSettings.scenes = list.ToArray();

        EditorSceneManager.OpenScene($"{ScenesFolder}/{SceneNames.MainMenu}.unity");
        return $"Scenes: {string.Join(", ", SceneOrder)} built and added to Build Settings.";
    }

    static void BuildScene(string name, Action<RectTransform> build)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = UIFactory.BgDark;
        cam.orthographic = true;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = 5;
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        build((RectTransform)canvasGo.transform);

        new GameObject("SFX").AddComponent<SfxSynth>();

        EditorSceneManager.SaveScene(scene, $"{ScenesFolder}/{name}.unity");
    }

    // ------------------------------------------------------------------ balance simulation

    /// <summary>Headless battles to sanity-check battle length, talent strength and the starter team.</summary>
    public static string SimulateBalance()
    {
        var db = CardDatabase.Instance;
        if (db == null || db.LocationCount == 0) return "Build the card database first.";
        var rng = new System.Random(42);
        UnityEngine.Random.InitState(42);
        var sb = new StringBuilder();
        sb.AppendLine("=== AniGame balance simulation (single upgradable card system) ===");

        var allCards = db.locations.SelectMany(l => l.floors).Where(c => c != null).ToList();
        CardRuntime PlayerFighter(int tier, string talent = null, CardElement? element = null, bool averageRoll = false)
        {
            var art = allCards[rng.Next(allCards.Count)];
            var rarity = (CardRarity)tier;
            var inst = new CardInstance { uid = 0, key = art.Key, rarity = rarity };
            inst.build = PlayerProfile.RandomBuild(rarity, talent, element);
            if (averageRoll) inst.build.hp = inst.build.atk = inst.build.def = inst.build.spd = 0;
            return CardRuntime.ForBuild(inst, inst.build);
        }

        // 1) Player power by rarity
        sb.AppendLine("\nPlayer card power (Base 100 + Roll) x (1 + 5% per tier), Power = HP/10 + ATK + DEF + SPD:");
        for (int t = 0; t <= GameRules.MaxTier; t++)
        {
            var r = (CardRarity)t;
            var zero = new BuildData();
            var best = new BuildData { hp = GameRules.MaxDeviation(t), atk = GameRules.MaxDeviation(t), def = GameRules.MaxDeviation(t), spd = GameRules.MaxDeviation(t) };
            var worst = new BuildData { hp = -GameRules.MaxDeviation(t), atk = -GameRules.MaxDeviation(t), def = -GameRules.MaxDeviation(t), spd = -GameRules.MaxDeviation(t) };
            sb.AppendLine($"  {CardStats.RarityName(r),-11} power {CardStats.Power(r, worst),4} .. {CardStats.Power(r, zero),4} .. {CardStats.Power(r, best),4}   talent +{(GameRules.TalentPower(t) - 1f) * 100f:0}%");
        }

        // 2) Win rate of each player rarity vs the floor enemies of a location (sheet stats, scale x{EnemyStatScale})
        sb.AppendLine($"\nWin rate: player card (random Roll/element/talent) vs that LOC's floor enemies (sheet stats x{GameRules.EnemyStatScale}), 40 battles per cell:");
        sb.Append("  LOC  enemy power      ");
        for (int t = 0; t <= GameRules.MaxTier; t++) sb.Append($"{CardStats.RarityShort((CardRarity)t),5}");
        sb.AppendLine("   needs");
        var locs = new List<int> { 1, 2, 3, 5, 8, 10, 15, 20, 25, 30, 40, 50, 60, 70, 80, 90, 100 };
        float totalRounds = 0; int totalBattles = 0, draws = 0;
        foreach (int loc in locs)
        {
            var l = db.GetLocation(loc);
            if (l == null || l.floors.Count == 0) continue;
            var powers = l.floors.Select(CardStats.EnemyPower).ToList();
            sb.Append($"  {loc,3}  {powers.Min(),4}-{powers.Max(),-4} avg {powers.Average(),4:0} ");
            int needs = -1;
            for (int t = 0; t <= GameRules.MaxTier; t++)
            {
                int won = 0;
                const int n = 40;
                for (int i = 0; i < n; i++)
                {
                    var enemy = new CardRuntime(l.floors[rng.Next(l.floors.Count)]);
                    var e = new BattleEngine(PlayerFighter(t), enemy, rng.Next(1, int.MaxValue));
                    var state = e.RunToEnd();
                    if (state == BattleState.PlayerWon) won++;
                    if (state == BattleState.Draw) draws++;
                    totalRounds += e.Round;
                    totalBattles++;
                }
                float pct = won * 100f / n;
                if (needs < 0 && pct >= 50f) needs = t;
                sb.Append($"{pct,5:0}");
            }
            sb.AppendLine(needs >= 0 ? $"   {CardStats.RarityName((CardRarity)needs)}" : "   NOT BEATABLE");
        }
        sb.AppendLine($"  Average battle length {totalRounds / Mathf.Max(1, totalBattles):0.0} rounds, draws {draws * 100f / Mathf.Max(1, totalBattles):0.0}%");

        // 3) Talent strength: same rarity (Ultra Rare), average Roll, same element; A gets talent T, B a random talent
        var talents = db.talents.Where(t => t != null).ToList();
        if (talents.Count > 0)
        {
            var results = new List<KeyValuePair<string, float>>();
            foreach (var talent in talents)
            {
                int won = 0;
                const int n = 160;
                for (int i = 0; i < n; i++)
                {
                    var a = PlayerFighter(4, talent.DisplayName, CardElement.Neutral, true);
                    var b = PlayerFighter(4, null, CardElement.Neutral, true);
                    var e = new BattleEngine(a, b, rng.Next(1, int.MaxValue));
                    if (e.RunToEnd() == BattleState.PlayerWon) won++;
                }
                results.Add(new KeyValuePair<string, float>(talent.DisplayName, won * 100f / n));
            }
            sb.AppendLine($"\nTalent win rate ({talents.Count} talents; Ultra Rare, average Roll, Neutral vs Neutral, vs a random talent, 160 battles each):");
            foreach (var kv in results.OrderByDescending(k => k.Value)) sb.AppendLine($"  {kv.Key,-24} {kv.Value,5:0.0}%");
        }
        else sb.AppendLine("\nNo talents in the database (run the campaign generator).");

        // 4) Element chart as implemented (attacker row vs defender column)
        sb.AppendLine("\nElement damage multiplier (row attacks column):");
        sb.Append("           ");
        foreach (var d in ElementSystem.All) sb.Append($"{ElementSystem.ShortName(d),7}");
        sb.AppendLine();
        foreach (var a in ElementSystem.All)
        {
            sb.Append($"  {ElementSystem.ShortName(a),-8} ");
            foreach (var d in ElementSystem.All) sb.Append($"{ElementSystem.GetElementMultiplier(a, d),7:0.###}");
            sb.AppendLine();
        }
        var elementCounts = allCards.GroupBy(c => c.element).OrderByDescending(g => g.Count()).Select(g => $"{ElementSystem.DisplayName(g.Key)} {g.Count() * 100f / allCards.Count:0.0}%");
        sb.AppendLine("  Floor enemy elements: " + string.Join(", ", elementCounts));

        // 5) Packs and economy sanity checks
        sb.AppendLine("\nPacks (chances of items 2-5 must total 100%):");
        foreach (var pack in Packs.All)
            sb.AppendLine($"  {pack.name,-18} {pack.price,6:N0} {pack.currency,-8} total {pack.TotalChance,6:0.##}%  avg diamonds {pack.ExpectedDiamonds:0.0}");
        sb.AppendLine("\nGold per floor (20 x LOC + 5 x floor), full LOC clear:");
        foreach (int loc in new[] { 1, 10, 50, 100 })
        {
            var l = db.GetLocation(loc);
            if (l == null) continue;
            int gold = 0;
            for (int f = 1; f <= l.floors.Count; f++) gold += GameRules.FloorGold(loc, f);
            sb.AppendLine($"  LOC {loc,3}: {gold:N0} gold over {l.floors.Count} floors (= {gold / 1000f:0.0} Fusion Orbs)");
        }

        string report = sb.ToString();
        WriteReport("Simulate Balance", report);
        return report;
    }

    static void WriteReport(string title, string body)
    {
        try
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}\n{body}\n");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AniGame] Could not write report: " + ex.Message);
        }
    }
}
