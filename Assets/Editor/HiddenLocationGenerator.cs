using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > AniGame > Hidden Location > Generate: builds the 3v3 Hidden Location.
/// 20 anime (one per LOC, rarity fixed per anime), 9 characters each (the 9 most popular on AniList: floor 3 = top 3),
/// 3 floors of 3 enemies. Names and pictures come from AniList (needs internet the first time; the list is then kept in
/// Assets/Data/hidden_locations.csv). Talents and elements are random but fixed per character name.
/// Stats: the highest stat of the rarity x (100 + LOC)%.
/// </summary>
public static class HiddenLocationGenerator
{
    public const string SheetPath = "Assets/Data/hidden_locations.csv";
    public const string Folder = "Assets/HiddenLocation";
    const string DatabasePath = "Assets/Resources/CardDatabase.asset";

    public struct AnimeDef
    {
        public int loc; public string name; public int anilistId; public string search; public string[] keys; public CardRarity rarity;
        public AnimeDef(int loc, string name, int id, string search, CardRarity rarity, params string[] keys)
        { this.loc = loc; this.name = name; anilistId = id; this.search = search; this.rarity = rarity; this.keys = keys; }
    }

    public static readonly AnimeDef[] Anime =
    {
        new AnimeDef(1, "Attack on Titan", 16498, "Shingeki no Kyojin", CardRarity.Common, "titan", "kyojin"),
        new AnimeDef(2, "Fullmetal Alchemist: Brotherhood", 5114, "Fullmetal Alchemist: Brotherhood", CardRarity.Common, "fullmetal", "hagane"),
        new AnimeDef(3, "Frieren: Beyond Journey's End", 154587, "Sousou no Frieren", CardRarity.Uncommon, "frieren"),
        new AnimeDef(4, "Steins;Gate", 9253, "Steins;Gate", CardRarity.Uncommon, "steins"),
        new AnimeDef(5, "One Piece", 21, "ONE PIECE", CardRarity.Rare, "one piece"),
        new AnimeDef(6, "Hunter x Hunter", 11061, "HUNTER x HUNTER (2011)", CardRarity.Rare, "hunter"),
        new AnimeDef(7, "Death Note", 1535, "DEATH NOTE", CardRarity.SuperRare, "death note"),
        new AnimeDef(8, "Cowboy Bebop", 1, "Cowboy Bebop", CardRarity.SuperRare, "cowboy bebop"),
        new AnimeDef(9, "Vinland Saga", 101348, "VINLAND SAGA", CardRarity.UltraRare, "vinland"),
        new AnimeDef(10, "Naruto: Shippuden", 1735, "Naruto: Shippuuden", CardRarity.UltraRare, "naruto"),
        new AnimeDef(11, "Demon Slayer: Kimetsu no Yaiba", 101922, "Kimetsu no Yaiba", CardRarity.Epic, "kimetsu", "demon slayer"),
        new AnimeDef(12, "Jujutsu Kaisen", 113415, "Jujutsu Kaisen", CardRarity.Epic, "jujutsu"),
        new AnimeDef(13, "Code Geass: Lelouch of the Rebellion", 1575, "Code Geass: Hangyaku no Lelouch", CardRarity.Legendary, "code geass"),
        new AnimeDef(14, "Monster", 19, "MONSTER", CardRarity.Legendary, "monster"),
        new AnimeDef(15, "Gintama", 918, "Gintama", CardRarity.Mythic, "gintama", "gintam"),
        new AnimeDef(16, "Mob Psycho 100", 21507, "Mob Psycho 100", CardRarity.Mythic, "mob psycho"),
        new AnimeDef(17, "Cyberpunk: Edgerunners", 120377, "Cyberpunk: Edgerunners", CardRarity.God, "cyberpunk", "edgerunners"),
        new AnimeDef(18, "Chainsaw Man", 127230, "Chainsaw Man", CardRarity.God, "chainsaw"),
        new AnimeDef(19, "Dragon Ball Z", 813, "Dragon Ball Z", CardRarity.God, "dragon ball z"),
        new AnimeDef(20, "Haikyuu!!", 20464, "Haikyuu!!", CardRarity.God, "haikyu"),
    };

    public class Row { public int loc, floor, slot; public string anime, card, image, rarity; }

    // ---- AniList JSON (JsonUtility)
    [Serializable] class Resp { public RespData data; }
    [Serializable] class RespData { public Media Media; }
    [Serializable] class Media { public int id; public Title title; public Chars characters; }
    [Serializable] class Title { public string romaji; public string english; }
    [Serializable] class Chars { public CharNode[] nodes; }
    [Serializable] class CharNode { public int id; public CharName name; public CharImage image; }
    [Serializable] class CharName { public string full; }
    [Serializable] class CharImage { public string large; }

    [MenuItem("Tools/AniGame/Hidden Location/Generate")]
    static void Menu() => Debug.Log("[AniGame] " + Generate(false));

    [MenuItem("Tools/AniGame/Hidden Location/Fetch characters again (AniList)")]
    static void MenuRefetch() => Debug.Log("[AniGame] " + Generate(true));

    public static string Generate(bool refetch)
    {
        var log = new StringBuilder();
        try
        {
            var rows = (!refetch && File.Exists(SheetPath)) ? ReadSheet() : null;
            if (rows == null || rows.Count < GameRules.HiddenLocationCount * 9)
            {
                rows = Fetch(log);
                if (rows.Count == 0) return "Hidden Location: could not get any characters from AniList (no internet?). " + log;
                WriteSheet(rows);
                AssetDatabase.ImportAsset(SheetPath);
            }
            int downloaded = DownloadImages(rows, log);
            int cards = BuildAssets(rows, log);
            return $"Hidden Location: {rows.Select(r => r.loc).Distinct().Count()} LOCs, {cards} enemy cards, {downloaded} pictures downloaded. {log}";
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return "Hidden Location failed: " + ex.Message + " " + log;
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ---------------------------------------------------------------- 1. characters from AniList

    static List<Row> Fetch(StringBuilder log)
    {
        var rows = new List<Row>();
        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AniGameHiddenLocation/1.0 (Unity)");
            foreach (var a in Anime)
            {
                EditorUtility.DisplayProgressBar("AniGame - Hidden Location", $"Characters of {a.name}...", a.loc / 21f * 0.5f);
                var media = Query(http, a.anilistId, null);
                if (media == null || !Matches(media, a)) media = Query(http, 0, a.search) ?? media;
                if (media == null || media.characters == null || media.characters.nodes == null) { log.Append($"[{a.name}: not found] "); continue; }
                var chars = media.characters.nodes
                    .Where(n => n != null && n.name != null && !string.IsNullOrWhiteSpace(n.name.full) && n.image != null && !string.IsNullOrEmpty(n.image.large) && !n.image.large.Contains("default"))
                    .Where(n => !NotCharacters.Contains(n.name.full.Trim().ToLowerInvariant()))
                    .Take(9).ToList();
                if (chars.Count < 9) log.Append($"[{a.name}: only {chars.Count} characters] ");
                for (int i = 0; i < chars.Count; i++)
                {
                    // most popular first: floor 3 gets the top 3, floor 1 the last 3
                    int floor = 3 - i / 3, slot = i % 3;
                    rows.Add(new Row { loc = a.loc, floor = floor, slot = slot, anime = a.name, card = chars[i].name.full.Trim(), image = chars[i].image.large, rarity = a.rarity.ToString() });
                }
                Thread.Sleep(800); // AniList allows ~90 requests a minute
            }
        }
        return rows;
    }

    /// <summary>AniList entries that are not fighters.</summary>
    static readonly HashSet<string> NotCharacters = new HashSet<string> { "narrator", "the narrator" };

    static bool Matches(Media m, AnimeDef a)
    {
        string t = ((m.title?.english ?? "") + " " + (m.title?.romaji ?? "")).ToLowerInvariant();
        return a.keys.Any(k => t.Contains(k));
    }

    static Media Query(HttpClient http, int id, string search)
    {
        const string q = "query($id:Int,$search:String){Media(id:$id,search:$search,type:ANIME){id title{romaji english} characters(sort:[FAVOURITES_DESC],perPage:15){nodes{id name{full} image{large}}}}}";
        string vars = id > 0 ? $"{{\"id\":{id}}}" : $"{{\"search\":\"{search.Replace("\"", "\\\"")}\"}}";
        string body = $"{{\"query\":\"{q.Replace("\"", "\\\"")}\",\"variables\":{vars}}}";
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = http.PostAsync("https://graphql.anilist.co", content).GetAwaiter().GetResult();
                string json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if ((int)resp.StatusCode == 429) { Thread.Sleep(3000); continue; }
                if (!resp.IsSuccessStatusCode) return null;
                return JsonUtility.FromJson<Resp>(json)?.data?.Media;
            }
            catch (Exception) { Thread.Sleep(1000); }
        }
        return null;
    }

    // ---------------------------------------------------------------- 2. sheet (so it can be rebuilt offline)

    static string Csv(string s) => (s ?? "").IndexOfAny(new[] { ',', '"' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : (s ?? "");

    static void WriteSheet(List<Row> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SheetPath));
        var sb = new StringBuilder("LOC,FLOOR,SLOT,ANIME,CARD,IMAGE,RARITY\n");
        foreach (var r in rows.OrderBy(r => r.loc).ThenBy(r => r.floor).ThenBy(r => r.slot))
            sb.Append(r.loc).Append(',').Append(r.floor).Append(',').Append(r.slot + 1).Append(',').Append(Csv(r.anime)).Append(',')
              .Append(Csv(r.card)).Append(',').Append(Csv(r.image)).Append(',').Append(r.rarity).Append('\n');
        File.WriteAllText(SheetPath, sb.ToString(), new UTF8Encoding(false));
    }

    static List<Row> ReadSheet()
    {
        var rows = new List<Row>();
        var lines = File.ReadAllLines(SheetPath);
        for (int i = 1; i < lines.Length; i++)
        {
            var f = SplitCsv(lines[i]);
            if (f.Count < 7 || !int.TryParse(f[0], out int loc)) continue;
            rows.Add(new Row { loc = loc, floor = int.Parse(f[1]), slot = int.Parse(f[2]) - 1, anime = f[3], card = f[4], image = f[5], rarity = f[6] });
        }
        return rows;
    }

    static List<string> SplitCsv(string line)
    {
        var res = new List<string>();
        var sb = new StringBuilder();
        bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (q) { if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = false; } else sb.Append(c); }
            else if (c == '"') q = true;
            else if (c == ',') { res.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        res.Add(sb.ToString());
        return res;
    }

    // ---------------------------------------------------------------- 3. pictures (same folder and cache as the campaign)

    static int DownloadImages(List<Row> rows, StringBuilder log)
    {
        CardImporter.CreateFolderRecursive(CampaignGenerator.ImageFolder);
        var todo = rows.Select(r => r.image).Where(u => !string.IsNullOrEmpty(u) && u.StartsWith("http")).Distinct()
                       .Where(u => { var p = CampaignGenerator.LocalImagePath(u); return p != null && !(File.Exists(p) && new FileInfo(p).Length > 100); }).ToList();
        int ok = 0, failed = 0;
        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AniGameHiddenLocation/1.0 (Unity)");
            for (int i = 0; i < todo.Count; i++)
            {
                EditorUtility.DisplayProgressBar("AniGame - Hidden Location", $"Pictures {i + 1}/{todo.Count}", 0.5f + 0.4f * i / Mathf.Max(1, todo.Count));
                try
                {
                    var bytes = http.GetByteArrayAsync(todo[i]).GetAwaiter().GetResult();
                    if (bytes == null || bytes.Length < 100) throw new Exception("empty");
                    File.WriteAllBytes(CampaignGenerator.LocalImagePath(todo[i]), bytes);
                    ok++;
                }
                catch (Exception) { failed++; }
            }
        }
        if (failed > 0) log.Append($"[{failed} pictures failed: they load from the internet in game] ");
        AssetDatabase.Refresh();
        return ok;
    }

    static Sprite LoadSprite(string url)
    {
        string path = CampaignGenerator.LocalImagePath(url);
        if (path == null || !File.Exists(path)) return null;
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite sp) return sp;
        return null;
    }

    // ---------------------------------------------------------------- 4. card assets + database

    /// <summary>Rebuilds the enemy cards from the sheet (talents/elements fixed per name, stats from the current rules).</summary>
    public static int BuildAssets(List<Row> rows = null, StringBuilder log = null)
    {
        rows = rows ?? (File.Exists(SheetPath) ? ReadSheet() : new List<Row>());
        var pool = CampaignGenerator.TalentPool();
        CardImporter.CreateFolderRecursive(Folder);
        var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
        if (db == null) { log?.Append("[no CardDatabase] "); return 0; }
        db.hiddenLocations.Clear();
        int count = 0;
        foreach (var a in Anime)
        {
            var locRows = rows.Where(r => r.loc == a.loc).ToList();
            if (locRows.Count == 0) continue;
            string folder = $"{Folder}/LOC {a.loc}";
            CardImporter.CreateFolderRecursive(folder);
            var data = new CardDatabase.HiddenLocationData { number = a.loc, anime = a.name, rarity = a.rarity };
            for (int f = 1; f <= GameRules.HiddenFloorsPerLocation; f++)
            {
                var floor = new CardDatabase.HiddenFloor();
                foreach (var r in locRows.Where(r => r.floor == f).OrderBy(r => r.slot))
                {
                    string path = $"{folder}/F{f}-{r.slot + 1}.asset";
                    var card = AssetDatabase.LoadAssetAtPath<Card>(path);
                    bool isNew = card == null;
                    if (isNew) card = ScriptableObject.CreateInstance<Card>();
                    card.cardName = r.card;
                    card.anime = a.name;
                    card.rarity = a.rarity;
                    card.hidden = true;
                    card.location = a.loc;
                    card.floor = f;
                    card.teamSlot = r.slot;
                    card.floorCode = $"H{a.loc}-{f}{(char)('A' + r.slot)}";
                    int stat = GameRules.HiddenEnemyStat(a.rarity, a.loc);
                    card.health = stat * 10; // stored x10, scaled by the HP multiplier in battle
                    card.attack = stat;
                    card.defense = stat;
                    card.speed = stat;
                    card.power = stat * 4;
                    card.critRate = 5;
                    card.critDamage = 135;
                    card.evasion = 5;
                    card.mana = 0;
                    card.maxMana = 100;
                    card.element = ElementSystem.FromName(r.card);
                    CampaignGenerator.AssignTalents(card, r.card, pool);
                    card.imageUrl = r.image;
                    card.characterId = CampaignGenerator.CharacterId(r.image);
                    var sprite = LoadSprite(r.image);
                    if (sprite != null) card.cardImg = sprite;
                    if (isNew) AssetDatabase.CreateAsset(card, path); else EditorUtility.SetDirty(card);
                    floor.enemies.Add(card);
                    count++;
                }
                data.floors.Add(floor);
            }
            db.hiddenLocations.Add(data);
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        return count;
    }
}
