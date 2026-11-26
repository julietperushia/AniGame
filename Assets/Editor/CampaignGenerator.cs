using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Campaign Generator: turns the campaign sheet (Assets/Data/campaign.csv) into locations and floors.
///
/// Sheet columns (header names, any order):
///   required  LOC, FLOOR, CARD, RARITY, HP, ATK, DEF, SPEED
///   optional  ANIME, IMAGE, TALENT, POWER, ELEMENT, CRIT, CRITDMG, EVASION
/// FLOOR codes are letter + tier (A1, B1 ... A2, B2 ...): floors are ordered by tier, then letter.
///
/// Output:
///   Assets/Campaign/LOC n/Floor k.asset   one Card per row (updated in place on re-generation, GUIDs kept)
///   Assets/Campaign/Images/&lt;id&gt;.png|jpg  portraits downloaded from the IMAGE urls (skipped when already there)
///   Assets/Resources/CardDatabase.asset    LOC -> floors index used by the game (with the anime per LOC)
/// Re-run it whenever the sheet changes: stale floors / locations inside Assets/Campaign are removed.
/// </summary>
public static class CampaignGenerator
{
    public const string DefaultSheetPath = "Assets/Data/campaign.csv";
    public const string CampaignFolder = "Assets/Campaign";
    public const string ImageFolder = "Assets/Campaign/Images";
    const string DatabasePath = "Assets/Resources/CardDatabase.asset";
    const int MaxParallelDownloads = 6;

    public class Options
    {
        public string sheetPath = DefaultSheetPath;
        public bool downloadImages = true;
        public bool removeStale = true;
        public bool validateOnly;
    }

    public class Row
    {
        public int line;
        public int loc;
        public string floorCode;
        public string letter;
        public int tier;
        public string anime;
        public string card;
        public string image;
        public string rarity;
        public string talent;
        public string element;
        public int hp, atk, def, spd, power;
        public int crit = -1, critDmg = -1, evasion = -1;
    }

    public class Report
    {
        public int rows, locations, created, updated, removed, anime;
        public int imagesTotal, imagesDownloaded, imagesCached, imagesFailed, cardsWithoutImage, cardsWithoutTalent;
        public string dataVersion;
        public bool cancelled;
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();

        public bool Ok => errors.Count == 0;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Campaign: {rows} rows -> {locations} locations, {anime} anime (data version {dataVersion})");
            sb.AppendLine($"Cards: {created} created, {updated} updated, {removed} stale removed, {cardsWithoutTalent} without talent, {cardsWithoutImage} without image");
            sb.AppendLine($"Images: {imagesTotal} unique, {imagesDownloaded} downloaded, {imagesCached} already present, {imagesFailed} failed");
            if (cancelled) sb.AppendLine("Image download was cancelled.");
            if (errors.Count > 0) sb.AppendLine($"Errors ({errors.Count}):\n  " + string.Join("\n  ", errors.Take(25)));
            if (warnings.Count > 0) sb.AppendLine($"Warnings ({warnings.Count}):\n  " + string.Join("\n  ", warnings.Take(25)));
            return sb.ToString().TrimEnd();
        }
    }

    public static bool HasSheet => File.Exists(DefaultSheetPath);

    // ------------------------------------------------------------------ menu

    [MenuItem("Tools/AniGame/Campaign/Generate From Sheet", priority = 1)]
    static void GenerateMenu() => RunWithDialog(new Options());

    [MenuItem("Tools/AniGame/Campaign/Generate Without Downloading Images", priority = 2)]
    static void GenerateNoDownloadMenu() => RunWithDialog(new Options { downloadImages = false });

    [MenuItem("Tools/AniGame/Campaign/Validate Sheet Only", priority = 3)]
    static void ValidateMenu() => RunWithDialog(new Options { validateOnly = true });

    static void RunWithDialog(Options options)
    {
        if (!File.Exists(options.sheetPath))
        {
            string picked = EditorUtility.OpenFilePanel("Campaign sheet (CSV)", Application.dataPath, "csv");
            if (string.IsNullOrEmpty(picked)) return;
            options.sheetPath = picked;
        }
        var report = Generate(options);
        string text = report.ToString();
        Debug.Log("[AniGame] " + text);
        EditorUtility.DisplayDialog("AniGame - Campaign Generator", text.Length > 1800 ? text.Substring(0, 1800) + "\n..." : text, "OK");
    }

    // ------------------------------------------------------------------ pipeline

    public static Report Generate(Options options)
    {
        var report = new Report();
        try
        {
            EditorUtility.DisplayProgressBar("AniGame - Campaign", "Reading sheet...", 0.02f);
            var rows = ReadSheet(options.sheetPath, report);
            if (rows.Count == 0)
            {
                report.errors.Add("No usable rows in the sheet.");
                return report;
            }
            report.dataVersion = HashFile(options.sheetPath);
            Validate(rows, report);
            if (options.validateOnly) return report;

            AniGameSetup.RepairTalents(); // makes sure every talent class has an asset (Berserk Strike, ...)

            if (options.downloadImages) DownloadImages(rows, report);

            var cards = CreateCardAssets(rows, options.removeStale, report);
            BuildDatabase(cards, rows, report.dataVersion, options.sheetPath);
        }
        catch (Exception ex)
        {
            report.errors.Add("FAILED: " + ex.Message);
            Debug.LogException(ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            WriteReport(report);
        }
        return report;
    }

    // ------------------------------------------------------------------ 1. read

    public static List<Row> ReadSheet(string path, Report report)
    {
        var rows = new List<Row>();
        string text = File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿');
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length < 2) return rows;

        var header = CardImporter.ParseCsvLine(lines[0]);
        var col = new Dictionary<string, int>();
        for (int i = 0; i < header.Count; i++)
        {
            string key = header[i].Trim().Trim('﻿').ToUpperInvariant().Replace(" ", "").Replace("_", "");
            if (!col.ContainsKey(key)) col[key] = i;
        }

        string[] required = { "LOC", "FLOOR", "CARD", "RARITY", "HP", "ATK", "DEF", "SPEED" };
        foreach (var r in required)
            if (!col.ContainsKey(r)) report.errors.Add($"Missing required column '{r}'. Found: {string.Join(", ", header)}");
        if (report.errors.Count > 0) return rows;

        int Col(params string[] names)
        {
            foreach (var n in names) if (col.TryGetValue(n, out int idx)) return idx;
            return -1;
        }
        int cLoc = Col("LOC"), cFloor = Col("FLOOR"), cCard = Col("CARD", "NAME"), cRarity = Col("RARITY");
        int cHp = Col("HP"), cAtk = Col("ATK"), cDef = Col("DEF"), cSpd = Col("SPEED", "SPD");
        int cAnime = Col("ANIME", "SERIES"), cImage = Col("IMAGE", "IMG"), cTalent = Col("TALENT"), cPower = Col("POWER");
        int cElement = Col("ELEMENT"), cCrit = Col("CRIT", "CRITRATE"), cCritDmg = Col("CRITDMG", "CRITDAMAGE"), cEva = Col("EVASION", "EVA");

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = CardImporter.ParseCsvLine(line);
            string Get(int c) => c >= 0 && c < f.Count ? f[c].Trim() : "";

            var row = new Row { line = i + 1 };
            if (!int.TryParse(Get(cLoc), out row.loc) || row.loc <= 0)
            {
                report.errors.Add($"Line {row.line}: LOC '{Get(cLoc)}' is not a positive number");
                continue;
            }
            row.floorCode = Get(cFloor);
            row.card = Get(cCard);
            if (string.IsNullOrEmpty(row.card))
            {
                report.errors.Add($"Line {row.line}: empty CARD name");
                continue;
            }
            row.rarity = Get(cRarity);
            row.anime = Get(cAnime);
            row.image = Get(cImage);
            row.talent = Get(cTalent);
            row.element = Get(cElement);

            bool numbersOk = TryInt(Get(cHp), out row.hp) & TryInt(Get(cAtk), out row.atk) &
                             TryInt(Get(cDef), out row.def) & TryInt(Get(cSpd), out row.spd);
            if (!numbersOk)
            {
                report.errors.Add($"Line {row.line} ({row.card}): HP/ATK/DEF/SPEED must be whole numbers");
                continue;
            }
            if (!TryInt(Get(cPower), out row.power)) row.power = row.hp + row.atk + row.def + row.spd;
            if (TryInt(Get(cCrit), out int crit)) row.crit = crit;
            if (TryInt(Get(cCritDmg), out int critDmg)) row.critDmg = critDmg;
            if (TryInt(Get(cEva), out int eva)) row.evasion = eva;

            var m = Regex.Match(row.floorCode ?? "", @"^\s*([A-Za-z]+)\s*(\d+)\s*$");
            if (m.Success)
            {
                row.letter = m.Groups[1].Value.ToUpperInvariant();
                row.tier = int.Parse(m.Groups[2].Value);
            }
            else
            {
                row.letter = "";
                row.tier = int.MaxValue;
                report.warnings.Add($"Line {row.line}: FLOOR '{row.floorCode}' is not letter+number; kept in sheet order");
            }
            rows.Add(row);
        }
        report.rows = rows.Count;
        return rows;
    }

    static bool TryInt(string s, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (int.TryParse(s.Trim(), out value)) return true;
        if (float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv))
        {
            value = Mathf.RoundToInt(fv);
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ 2. validate

    static void Validate(List<Row> rows, Report report)
    {
        var talents = CardImporter.BuildTalentCache();
        var locs = rows.Select(r => r.loc).Distinct().OrderBy(n => n).ToList();
        report.locations = locs.Count;
        report.anime = rows.Select(r => r.anime).Where(a => !string.IsNullOrEmpty(a)).Distinct().Count();

        for (int n = 1; n <= locs.Last(); n++)
            if (!locs.Contains(n)) report.warnings.Add($"LOC {n} has no rows (the campaign will stop at LOC {n - 1})");

        foreach (var g in rows.GroupBy(r => r.loc))
        {
            foreach (var d in g.GroupBy(r => r.floorCode.ToUpperInvariant()).Where(x => x.Count() > 1))
                report.errors.Add($"LOC {g.Key}: floor code {d.Key} appears {d.Count()} times (lines {string.Join(", ", d.Select(r => r.line))})");
            var animeNames = g.Select(r => r.anime).Where(a => !string.IsNullOrEmpty(a)).Distinct().ToList();
            if (animeNames.Count > 1) report.warnings.Add($"LOC {g.Key} mixes anime: {string.Join(" / ", animeNames)} (the most common one names the location)");
        }

        var validRarities = Enum.GetNames(typeof(CardRarity)).Select(CardImporter.NormalizeName).ToList();
        foreach (var r in rows)
        {
            if (!validRarities.Contains(CardImporter.NormalizeName(r.rarity)))
                report.warnings.Add($"Line {r.line} ({r.card}): unknown rarity '{r.rarity}' -> Common");
            if (!string.IsNullOrEmpty(r.talent) && CardImporter.FindTalent(talents, r.talent) == null)
                report.warnings.Add($"Line {r.line} ({r.card}): unknown talent '{r.talent}' (it will be created once a Talent class with that name exists)");
            if (!string.IsNullOrEmpty(r.element) && CardImporter.ParseElement(r.element) == null)
                report.warnings.Add($"Line {r.line} ({r.card}): unknown element '{r.element}'");
            if (string.IsNullOrEmpty(r.image)) report.warnings.Add($"Line {r.line} ({r.card}): no IMAGE url");
        }
    }

    // ------------------------------------------------------------------ 3. images

    /// <summary>Character id = file name of the image url without extension (AniList: "6240", "b455-J0I8nu81FYvC").</summary>
    public static string CharacterId(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        try
        {
            string file = Path.GetFileNameWithoutExtension(new Uri(url).AbsolutePath);
            return Regex.Replace(file, @"[^A-Za-z0-9_\-]", "_");
        }
        catch
        {
            return "";
        }
    }

    static string ImageExtension(string url)
    {
        try
        {
            string ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" ? ext : ".png";
        }
        catch
        {
            return ".png";
        }
    }

    public static string LocalImagePath(string url)
    {
        string id = CharacterId(url);
        return string.IsNullOrEmpty(id) ? null : $"{ImageFolder}/{id}{ImageExtension(url)}";
    }

    static void DownloadImages(List<Row> rows, Report report)
    {
        CardImporter.CreateFolderRecursive(ImageFolder);
        var urls = rows.Select(r => r.image).Where(u => !string.IsNullOrEmpty(u) && u.StartsWith("http")).Distinct().ToList();
        report.imagesTotal = urls.Count;

        var todo = new List<string>();
        foreach (var url in urls)
        {
            string local = LocalImagePath(url);
            if (local == null) continue;
            var info = new FileInfo(local);
            if (info.Exists && info.Length > 100) report.imagesCached++;
            else todo.Add(url);
        }
        if (todo.Count == 0) return;

        int done = 0, failed = 0;
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        var cts = new CancellationTokenSource();
        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
        using (var gate = new SemaphoreSlim(MaxParallelDownloads))
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AniGameCampaignGenerator/1.0 (Unity)");
            var tasks = todo.Select(url => Task.Run(async () =>
            {
                await gate.WaitAsync(cts.Token).ConfigureAwait(false);
                try
                {
                    byte[] bytes = null;
                    for (int attempt = 0; attempt < 3 && bytes == null && !cts.IsCancellationRequested; attempt++)
                    {
                        try
                        {
                            var data = await http.GetByteArrayAsync(url).ConfigureAwait(false);
                            if (LooksLikeImage(data)) bytes = data;
                        }
                        catch (Exception) when (attempt < 2)
                        {
                            await Task.Delay(500 * (attempt + 1)).ConfigureAwait(false);
                        }
                    }
                    if (bytes == null) throw new Exception("not an image or no response");
                    string local = LocalImagePath(url);
                    string tmp = local + ".download";
                    File.WriteAllBytes(tmp, bytes);
                    if (File.Exists(local)) File.Delete(local);
                    File.Move(tmp, local);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    failures.Add($"{url}: {ex.Message}");
                }
                finally
                {
                    Interlocked.Increment(ref done);
                    gate.Release();
                }
            }, cts.Token)).ToArray();

            while (!Task.WaitAll(tasks, 150))
            {
                int d = Volatile.Read(ref done);
                if (EditorUtility.DisplayCancelableProgressBar("AniGame - Campaign", $"Downloading portraits {d}/{todo.Count}...", 0.05f + 0.6f * d / todo.Count))
                {
                    cts.Cancel();
                    report.cancelled = true;
                    try { Task.WaitAll(tasks, 5000); } catch { }
                    break;
                }
            }
        }

        report.imagesFailed = failed;
        report.imagesDownloaded = Math.Max(0, done - failed);
        foreach (var f in failures.Take(10)) report.warnings.Add("Image download failed: " + f);

        EditorUtility.DisplayProgressBar("AniGame - Campaign", "Importing portraits...", 0.68f);
        AssetDatabase.Refresh();
    }

    static bool LooksLikeImage(byte[] b)
    {
        if (b == null || b.Length < 100) return false;
        bool png = b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
        bool jpg = b[0] == 0xFF && b[1] == 0xD8;
        return png || jpg;
    }

    static Sprite LoadSprite(string url)
    {
        string path = LocalImagePath(url);
        if (path == null || !File.Exists(path)) return null;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s) return s;
        return null;
    }

    // ------------------------------------------------------------------ talents by character name

    /// <summary>Every talent asset, one per name, in a stable order (sorted by class name).</summary>
    public static List<Talent> TalentPool()
    {
        var seen = new HashSet<string>();
        var list = new List<Talent>();
        foreach (string guid in AssetDatabase.FindAssets("t:Talent"))
        {
            var t = AssetDatabase.LoadAssetAtPath<Talent>(AssetDatabase.GUIDToAssetPath(guid));
            if (t != null && seen.Add(t.GetType().Name)) list.Add(t);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.GetType().Name, b.GetType().Name));
        return list;
    }

    /// <summary>FNV-1a hash: stable across runs and machines (string.GetHashCode is not).</summary>
    static uint StableHash(string text)
    {
        uint h = 2166136261;
        foreach (char c in (text ?? "").Trim().ToLowerInvariant()) { h ^= c; h *= 16777619; }
        return h;
    }

    /// <summary>Same name = same talent. God: that talent plus one of the other kind (active + passive).</summary>
    public static void AssignTalents(Card card, string name, List<Talent> pool)
    {
        card.talent = null;
        card.talent2 = null;
        if (pool == null || pool.Count == 0) return;
        uint h = StableHash(name);
        var main = pool[(int)(h % (uint)pool.Count)];
        card.talent = main;
        if (card.rarity != CardRarity.God) return;
        var other = pool.Where(t => t.IsActive != main.IsActive).ToList();
        if (other.Count == 0) return;
        var second = other[(int)(StableHash(name + "#2") % (uint)other.Count)];
        if (main.IsActive) card.talent2 = second;
        else { card.talent = second; card.talent2 = main; }
    }

    // ------------------------------------------------------------------ 4. card assets

    static List<Card> CreateCardAssets(List<Row> rows, bool removeStale, Report report)
    {
        var talents = CardImporter.BuildTalentCache();
        var talentPool = TalentPool();
        var result = new List<Card>();
        var byLoc = rows.GroupBy(r => r.loc).OrderBy(g => g.Key).ToList();
        CardImporter.CreateFolderRecursive(CampaignFolder);

        int index = 0;
        AssetDatabase.StartAssetEditing(); // batch the imports: creating 2,000+ assets one by one takes many minutes
        try
        {
        foreach (var group in byLoc)
        {
            int loc = group.Key;
            string folder = $"{CampaignFolder}/LOC {loc}";
            CardImporter.CreateFolderRecursive(folder);
            var ordered = group.OrderBy(r => r.tier).ThenBy(r => r.letter.Length).ThenBy(r => r.letter, StringComparer.Ordinal).ThenBy(r => r.line).ToList();

            for (int k = 0; k < ordered.Count; k++)
            {
                var row = ordered[k];
                index++;
                if (index % 40 == 0)
                    EditorUtility.DisplayProgressBar("AniGame - Campaign", $"LOC {loc}: {row.card}", 0.7f + 0.25f * index / rows.Count);

                string path = $"{folder}/Floor {k + 1}.asset";
                var card = AssetDatabase.LoadAssetAtPath<Card>(path);
                bool isNew = card == null;
                if (isNew) card = ScriptableObject.CreateInstance<Card>();

                card.cardName = row.card;
                card.anime = row.anime;
                card.location = loc;
                card.floor = k + 1;
                card.floorCode = row.floorCode;
                card.rarity = CardImporter.ParseRarity(row.rarity);
                card.health = row.hp * 10;
                card.attack = row.atk;
                card.defense = row.def;
                card.speed = row.spd;
                card.power = row.power;
                card.mana = 0;
                card.maxMana = 100;
                card.critRate = row.crit >= 0 ? row.crit : (card.critRate > 0 ? card.critRate : 5);
                card.critDamage = row.critDmg >= 0 ? row.critDmg : (card.critDamage > 0 ? card.critDamage : 135);
                card.evasion = row.evasion >= 0 ? row.evasion : (card.evasion > 0 ? card.evasion : 5);

                var parsedElement = string.IsNullOrEmpty(row.element) ? null : CardImporter.ParseElement(row.element);
                if (parsedElement.HasValue) card.element = parsedElement.Value;
                else card.element = ElementSystem.FromName(row.card); // no ELEMENT column: deterministic roll with the Element Orb odds

                // Talents: random from every talent (old + new), but the same character name always gets the same
                // talent(s). God cards get an active + a passive talent. (The sheet's TALENT column is ignored.)
                AssignTalents(card, row.card, talentPool);
                if (card.talent == null) report.cardsWithoutTalent++;

                card.imageUrl = row.image;
                card.characterId = CharacterId(row.image);
                var sprite = LoadSprite(row.image);
                if (sprite != null) card.cardImg = sprite;
                if (card.cardImg == null) report.cardsWithoutImage++;

                if (isNew)
                {
                    AssetDatabase.CreateAsset(card, path);
                    report.created++;
                }
                else
                {
                    EditorUtility.SetDirty(card);
                    report.updated++;
                }
                result.Add(card);
            }

            if (removeStale)
            {
                // Floors beyond the sheet's count for this location (the sheet got shorter)
                foreach (string guid in AssetDatabase.FindAssets("t:Card", new[] { folder }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetDirectoryName(p).Replace('\\', '/') != folder) continue;
                    string file = Path.GetFileNameWithoutExtension(p);
                    if (file.StartsWith("Floor ") && int.TryParse(file.Substring(6), out int fl) && fl > ordered.Count)
                    {
                        AssetDatabase.DeleteAsset(p);
                        report.removed++;
                    }
                }
            }
        }

        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        if (removeStale)
        {
            // Whole locations that are no longer in the sheet (only generator-owned "LOC n" folders)
            var keep = new HashSet<int>(byLoc.Select(g => g.Key));
            foreach (string dir in AssetDatabase.GetSubFolders(CampaignFolder))
            {
                string name = Path.GetFileName(dir);
                if (name.StartsWith("LOC ") && int.TryParse(name.Substring(4), out int n) && !keep.Contains(n))
                {
                    AssetDatabase.DeleteAsset(dir);
                    report.removed++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    // ------------------------------------------------------------------ 5. database

    static void BuildDatabase(List<Card> cards, List<Row> rows, string dataVersion, string sheetPath)
    {
        CardImporter.CreateFolderRecursive("Assets/Resources");
        var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<CardDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
        }

        var animeByLoc = rows.GroupBy(r => r.loc).ToDictionary(
            g => g.Key,
            g => g.Select(r => r.anime).Where(a => !string.IsNullOrEmpty(a)).GroupBy(a => a).OrderByDescending(a => a.Count()).Select(a => a.Key).FirstOrDefault() ?? "");

        db.locations.Clear();
        var byLoc = cards.GroupBy(c => c.location).ToDictionary(g => g.Key, g => g.OrderBy(c => c.floor).ToList());
        for (int loc = 1; byLoc.ContainsKey(loc); loc++)
        {
            db.locations.Add(new CardDatabase.LocationData
            {
                number = loc,
                displayName = $"LOC {loc}",
                anime = animeByLoc.TryGetValue(loc, out string a) ? a : "",
                floors = byLoc[loc]
            });
        }
        // Every talent can roll on every rarity (talent rerolls, pack cards): one asset per talent name.
        db.talents.Clear();
        var seen = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Talent"))
        {
            var t = AssetDatabase.LoadAssetAtPath<Talent>(AssetDatabase.GUIDToAssetPath(guid));
            if (t == null || string.IsNullOrEmpty(t.DisplayName) || !seen.Add(t.DisplayName.Trim().ToLowerInvariant())) continue;
            db.talents.Add(t);
        }
        db.talents.Sort((x, y) => string.Compare(x.DisplayName, y.DisplayName, StringComparison.OrdinalIgnoreCase));

        db.dataVersion = dataVersion;
        db.sourceFile = sheetPath;
        db.generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    /// <summary>Rebuilds the database from the generated assets (used by Tools > AniGame > Steps > Build Card Database).</summary>
    public static string RebuildDatabaseFromAssets()
    {
        var cards = new List<Card>();
        foreach (string guid in AssetDatabase.FindAssets("t:Card", new[] { CampaignFolder }))
        {
            var c = AssetDatabase.LoadAssetAtPath<Card>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && c.location > 0) cards.Add(c);
        }
        var fakeRows = cards.Select(c => new Row { loc = c.location, anime = c.anime }).ToList();
        string version = File.Exists(DefaultSheetPath) ? HashFile(DefaultSheetPath) : "assets";
        BuildDatabase(cards, fakeRows, version, DefaultSheetPath);
        var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
        return $"Database: {db.locations.Count} locations, {db.CardCount} cards, {db.AnimeCount} anime (from {CampaignFolder}).";
    }

    // ------------------------------------------------------------------ helpers

    static string HashFile(string path)
    {
        using (var md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(File.ReadAllBytes(path));
            return BitConverter.ToString(hash, 0, 5).Replace("-", "").ToLowerInvariant();
        }
    }

    static void WriteReport(Report report)
    {
        try
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/AniGame_Campaign_Report.txt",
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Campaign Generator\n{report}\n\nAll errors:\n{string.Join("\n", report.errors)}\n\nAll warnings:\n{string.Join("\n", report.warnings)}\n");
        }
        catch { }
    }
}
