using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Pictures chosen in game, named by a source string:
///   "gallery:KEY" = the art of a built-in card (CardDatabase key);
///   "pic:ID"      = a picture the player imported for a card (persistentDataPath/pictures/ID.png, exported with the save);
///   "res:ID"      = a content picture made in Test Mode, e.g. a boss (Assets/Resources/Config/Pictures/ID.png, ships with the game).
/// Imported pictures are cropped to the card art shape (<see cref="CharacterPortrait.Aspect"/>).
/// </summary>
public static class PictureStore
{
    public const string Gallery = "gallery:", Player = "pic:", Content = "res:";
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static string PlayerFolder => Path.Combine(Application.persistentDataPath, "pictures");
    public static string ContentEditorFolder => Path.Combine(Application.dataPath, "Resources", "Config", "Pictures");
    public static string ContentOverrideFolder => Path.Combine(Application.persistentDataPath, "config", "pictures");
    public static string PlayerPath(string id) => Path.Combine(PlayerFolder, id + ".png");

    public static bool IsGallery(string src) => !string.IsNullOrEmpty(src) && src.StartsWith(Gallery);
    public static bool IsPlayer(string src) => !string.IsNullOrEmpty(src) && src.StartsWith(Player);
    public static string IdOf(string src) => string.IsNullOrEmpty(src) ? "" : src.Substring(src.IndexOf(':') + 1);

    /// <summary>The built-in card behind a "gallery:KEY" source (null otherwise).</summary>
    public static Card GalleryCard(string src)
    {
        if (!IsGallery(src)) return null;
        var db = CardDatabase.Instance;
        return db != null ? db.FindByKey(IdOf(src)) : null;
    }

    /// <summary>The picture for a source, or null (unknown / missing file). Gallery cards with only a web image return null: use <see cref="GalleryCard"/>.</summary>
    public static Sprite Load(string source)
    {
        if (string.IsNullOrEmpty(source)) return null;
        if (cache.TryGetValue(source, out var cached) && cached != null) return cached;
        Sprite sprite = null;
        try
        {
            if (IsGallery(source))
            {
                var card = GalleryCard(source);
                sprite = card != null ? card.cardImg : null;
            }
            else if (source.StartsWith(Player)) sprite = FromFile(PlayerPath(IdOf(source)), IdOf(source));
            else if (source.StartsWith(Content))
            {
                string id = IdOf(source);
                if (Application.isEditor) sprite = FromFile(Path.Combine(ContentEditorFolder, id + ".png"), id);
                else
                {
                    string o = Path.Combine(ContentOverrideFolder, id + ".png");
                    if (PlayerProfile.TestModeAvailable && File.Exists(o)) sprite = FromFile(o, id);
                    if (sprite == null)
                    {
                        var tex = Resources.Load<Texture2D>("Config/Pictures/" + id);
                        if (tex != null) sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
        }
        catch (Exception ex) { Debug.LogWarning($"[AniGame] Could not load picture {source}: {ex.Message}"); }
        if (sprite != null) cache[source] = sprite;
        return sprite;
    }

    static Sprite FromFile(string path, string name)
    {
        if (!File.Exists(path)) return null;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(tex); return null; }
        tex.wrapMode = TextureWrapMode.Clamp;
        var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        s.name = name;
        return s;
    }

    static string NewId(string hint)
    {
        string slug = "";
        foreach (char ch in (hint ?? "").ToLowerInvariant()) if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) slug += ch;
        if (slug.Length > 16) slug = slug.Substring(0, 16);
        if (slug.Length == 0) slug = "pic";
        return $"{slug}_{DateTime.Now:yyyyMMddHHmmss}_{UnityEngine.Random.Range(100, 1000)}";
    }

    /// <summary>Saves a cropped PNG for the player's cards. Returns the source ("pic:ID") or null on error.</summary>
    public static string SavePlayer(byte[] png, Sprite preview = null, string hint = "card")
    {
        try
        {
            Directory.CreateDirectory(PlayerFolder);
            string id = NewId(hint);
            File.WriteAllBytes(PlayerPath(id), png);
            string src = Player + id;
            if (preview != null) cache[src] = preview;
            return src;
        }
        catch (Exception ex) { Debug.LogWarning("[AniGame] Could not save the picture: " + ex.Message); return null; }
    }

    /// <summary>Saves a cropped PNG as game content (Test Mode: bosses). Returns the source ("res:ID") or null on error.</summary>
    public static string SaveContent(byte[] png, Sprite preview = null, string hint = "boss")
    {
        try
        {
            string id = NewId(hint);
            string folder = Application.isEditor ? ContentEditorFolder : ContentOverrideFolder;
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, id + ".png"), png);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.ImportAsset("Assets/Resources/Config/Pictures");
            UnityEditor.AssetDatabase.ImportAsset("Assets/Resources/Config/Pictures/" + id + ".png");
#endif
            string src = Content + id;
            if (preview != null) cache[src] = preview;
            return src;
        }
        catch (Exception ex) { Debug.LogWarning("[AniGame] Could not save the picture: " + ex.Message); return null; }
    }

    /// <summary>Puts an imported player picture back (save import). The id is kept so builds still point at it.</summary>
    public static void RestorePlayer(string id, byte[] png)
    {
        if (string.IsNullOrEmpty(id) || png == null || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return;
        Directory.CreateDirectory(PlayerFolder);
        File.WriteAllBytes(PlayerPath(id), png);
        cache.Remove(Player + id);
    }

    public static bool PlayerFileExists(string src) => IsPlayer(src) && File.Exists(PlayerPath(IdOf(src)));
}
