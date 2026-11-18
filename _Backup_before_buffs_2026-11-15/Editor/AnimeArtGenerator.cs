using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Anime-style placeholder art, drawn procedurally, plus user art overrides.
/// Every sprite first looks for your own file in Assets/Art/UserArt (see README.txt there); if it is missing,
/// a generated placeholder is written to Assets/Art/UI/Generated and used instead.
/// </summary>
public static class AnimeArtGenerator
{
    public const string GeneratedFolder = "Assets/Art/UI/Generated";
    public const string UserFolder = "Assets/Art/UserArt";
    public const int FrameSize = 192;
    public const int FrameBorder = 64;

    static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".psd", ".tga" };
    static readonly string[] AudioExtensions = { ".ogg", ".mp3", ".wav" };
    public static readonly string[] RarityFileNames = { "common", "uncommon", "rare", "superrare", "ultrarare", "epic", "legendary", "mythic", "god" };
    public static readonly string[] ElementFileNames = { "fire", "water", "tree", "lightning", "light", "dark", "rock", "neutral", "nonneutral" }; // index = (int)CardElement
    public static readonly string[] BackgroundKeys = { "menu", "map", "battle", "cards", "gacha", "shop" };

    static int userCount, generatedCount;
    static readonly List<string> userFiles = new List<string>();

    // ================================================================== entry point

    public static string Generate(UITheme theme)
    {
        userCount = generatedCount = 0;
        userFiles.Clear();
        CardImporter.CreateFolderRecursive(GeneratedFolder);
        EnsureUserFolders();

        theme.sakuraPetal = Pick("Effects/sakura_petal", () => Save("fx_sakura_petal", SakuraPetal(), 0));
        theme.sparkle = Pick("Effects/sparkle", () => Save("fx_sparkle", Sparkle(), 0));
        theme.halftone = Pick("Effects/halftone", () => Save("fx_halftone", Halftone(), 0, true));
        theme.lightRays = Pick("Effects/light_rays", () => Save("fx_light_rays", LightRays(), 0));
        theme.speedLines = Pick("Effects/speed_lines", () => Save("fx_speed_lines", SpeedLines(), 0));
        theme.frameShine = Pick("Effects/frame_shine", () => Save("fx_frame_shine", FrameShine(), 0));

        if (theme.rarityFrames == null || theme.rarityFrames.Length != 9) theme.rarityFrames = new Sprite[9];
        for (int i = 0; i < 9; i++)
        {
            int tier = i;
            theme.rarityFrames[i] = Pick("Frames/frame_" + RarityFileNames[i], () => Save("frame_" + RarityFileNames[tier], Frame(tier), FrameBorder), true);
        }

        if (theme.elementIcons == null || theme.elementIcons.Length != 9) theme.elementIcons = new Sprite[9];
        for (int i = 0; i < 9; i++)
        {
            var element = (CardElement)i;
            theme.elementIcons[i] = Pick("Elements/element_" + ElementFileNames[i], () => Save("element_" + ElementFileNames[(int)element], ElementIcon(element), 0));
        }

        theme.goldIcon = Pick("Icons/gold", () => Save("icon_gold", GoldCoin(), 0));
        theme.diamondIcon = Pick("Icons/diamond", () => Save("icon_diamond", Diamond(), 0));
        theme.fusionOrbIcon = Pick("Icons/fusion_orb", () => Save("icon_fusion_orb", Orb(Hex("B07CFF"), 0), 0));
        theme.elementOrbIcon = Pick("Icons/element_orb", () => Save("icon_element_orb", Orb(Hex("5BE36A"), 1), 0));
        theme.talentOrbIcon = Pick("Icons/talent_orb", () => Save("icon_talent_orb", Orb(Hex("FFA62B"), 2), 0));
        theme.staminaIcon = Pick("Icons/stamina", () => Save("icon_stamina", StaminaBolt(), 0));
        theme.lockIcon = Pick("Icons/lock", () => Save("icon_lock", Lock(), 0));

        theme.menuBackground = Pick("Backgrounds/menu", () => Save("bg_menu", Background("menu"), 0));
        theme.mapBackground = Pick("Backgrounds/map", () => Save("bg_map", Background("map"), 0));
        theme.battleBackground = Pick("Backgrounds/battle", () => Save("bg_battle", Background("battle"), 0));
        theme.cardsBackground = Pick("Backgrounds/cards", () => Save("bg_cards", Background("cards"), 0));
        theme.gachaBackground = Pick("Backgrounds/gacha", () => Save("bg_gacha", Background("gacha"), 0));
        theme.shopBackground = Pick("Backgrounds/shop", () => Save("bg_shop", Background("shop"), 0));

        theme.logo = Pick("logo", () => null);
        if (theme.packArt == null || theme.packArt.Length != 6) theme.packArt = new Sprite[6];
        for (int i = 0; i < 6 && i < Packs.All.Length; i++) theme.packArt[i] = Pick("Packs/pack_" + Packs.All[i].id, () => null);

        theme.menuMusic = PickAudio("Audio/menu_music");
        theme.battleMusic = PickAudio("Audio/battle_music");
        theme.gachaMusic = PickAudio("Audio/gacha_music");

        string users = userFiles.Count > 0 ? " Your art: " + string.Join(", ", userFiles) + "." : " No user art found yet (see Assets/Art/UserArt/README.txt).";
        return $"Anime art: {generatedCount} generated placeholders, {userCount} user files.{users}";
    }

    // ================================================================== user overrides

    static void EnsureUserFolders()
    {
        foreach (var sub in new[] { "Backgrounds", "Frames", "Elements", "Icons", "Packs", "Effects", "Audio" })
            CardImporter.CreateFolderRecursive($"{UserFolder}/{sub}");
        string readme = Path.GetFullPath($"{UserFolder}/README.txt");
        File.WriteAllText(readme, Readme());
    }

    static string FindUserFile(string relNoExt, string[] extensions)
    {
        foreach (var ext in extensions)
        {
            string path = $"{UserFolder}/{relNoExt}{ext}";
            if (File.Exists(Path.GetFullPath(path))) return path;
        }
        return null;
    }

    static Sprite Pick(string relNoExt, Func<Sprite> generate, bool isFrame = false)
    {
        string path = FindUserFile(relNoExt, ImageExtensions);
        if (path == null)
        {
            var s = generate();
            if (s != null) generatedCount++;
            return s;
        }
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; dirty = true; }
            if (isFrame && importer.spriteBorder == Vector4.zero)
            {
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                float b = Mathf.Round(Mathf.Min(w, h) * 0.25f);
                importer.spriteBorder = new Vector4(b, b, b, b);
                dirty = true;
            }
            if (dirty) importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) { userCount++; userFiles.Add(relNoExt); }
        return sprite;
    }

    static AudioClip PickAudio(string relNoExt)
    {
        string path = FindUserFile(relNoExt, AudioExtensions);
        if (path == null) return null;
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip != null) { userCount++; userFiles.Add(relNoExt); }
        return clip;
    }

    static Sprite Save(string name, Texture2D tex, int border, bool repeat = false)
    {
        string path = $"{GeneratedFolder}/{name}.png";
        File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ================================================================== painter

    sealed class Paint
    {
        public readonly int W, H;
        public readonly Color[] Px;

        public Paint(int w, int h)
        {
            W = w; H = h;
            Px = new Color[w * h];
        }

        public void Blend(int i, Color c, float cov)
        {
            float sa = Mathf.Clamp01(c.a * cov);
            if (sa <= 0f) return;
            var d = Px[i];
            float k = d.a * (1f - sa);
            float oa = sa + k;
            Px[i] = new Color((c.r * sa + d.r * k) / oa, (c.g * sa + d.g * k) / oa, (c.b * sa + d.b * k) / oa, oa);
        }

        public void Draw(Func<Vector2, float> sdf, Func<Vector2, Color> color, float x0 = 0f, float y0 = 0f, float x1 = -1f, float y1 = -1f, float aa = 1f)
        {
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0)), iy0 = Mathf.Max(0, Mathf.FloorToInt(y0));
            int ix1 = x1 < 0f ? W : Mathf.Min(W, Mathf.CeilToInt(x1)), iy1 = y1 < 0f ? H : Mathf.Min(H, Mathf.CeilToInt(y1));
            for (int y = iy0; y < iy1; y++)
                for (int x = ix0; x < ix1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float cov = Mathf.Clamp01(0.5f - sdf(p) / aa);
                    if (cov <= 0f) continue;
                    Blend(y * W + x, color(p), cov);
                }
        }

        public void Draw(Func<Vector2, float> sdf, Color color, float x0 = 0f, float y0 = 0f, float x1 = -1f, float y1 = -1f, float aa = 1f) =>
            Draw(sdf, _ => color, x0, y0, x1, y1, aa);

        /// <summary>Draws a shape with a dark outline (and optional soft drop shadow) underneath.</summary>
        public void Outlined(Func<Vector2, float> sdf, Func<Vector2, Color> color, Color outline, float width, float x0 = 0f, float y0 = 0f, float x1 = -1f, float y1 = -1f)
        {
            Draw(p => sdf(p) - width, outline, x0 - width, y0 - width, x1 < 0 ? -1 : x1 + width, y1 < 0 ? -1 : y1 + width);
            Draw(sdf, color, x0, y0, x1, y1);
        }

        public void Glow(Vector2 c, float radius, Color color, float power = 2f)
        {
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(c.x - radius)), ix1 = Mathf.Min(W, Mathf.CeilToInt(c.x + radius));
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(c.y - radius)), iy1 = Mathf.Min(H, Mathf.CeilToInt(c.y + radius));
            for (int y = iy0; y < iy1; y++)
                for (int x = ix0; x < ix1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / radius;
                    if (d >= 1f) continue;
                    Blend(y * W + x, color, Mathf.Pow(1f - d, power));
                }
        }

        public void Each(Func<int, int, Color, Color> f)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    Px[y * W + x] = f(x, y, Px[y * W + x]);
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels(Px);
            tex.Apply();
            return tex;
        }
    }

    // ================================================================== SDF helpers (pixel units, y up)

    static float SdCircle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

    static float SdRoundBox(Vector2 p, Vector2 c, Vector2 half, float r)
    {
        var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    static float SdBox(Vector2 p, Vector2 c, Vector2 half) => SdRoundBox(p, c, half, 0f);

    static float SdSegment(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-5f, Vector2.Dot(ba, ba)));
        return (pa - ba * h).magnitude - r;
    }

    static float SdPolygon(Vector2 p, Vector2[] v)
    {
        float d = Vector2.Dot(p - v[0], p - v[0]);
        float s = 1f;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            Vector2 e = v[j] - v[i];
            Vector2 w = p - v[i];
            Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            d = Mathf.Min(d, Vector2.Dot(b, b));
            bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
        }
        return s * Mathf.Sqrt(d);
    }

    /// <summary>Region between depth a and b inside a shape (a may be negative to extend outside).</summary>
    static float Band(float d, float a, float b) => Mathf.Max(d + a, -d - b);

    static Vector2[] Star(Vector2 c, int points, float r1, float r2, float rotDeg = 0f)
    {
        var v = new Vector2[points * 2];
        for (int i = 0; i < v.Length; i++)
        {
            float a = (90f + rotDeg + i * 180f / points) * Mathf.Deg2Rad;
            float r = i % 2 == 0 ? r1 : r2;
            v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return v;
    }

    static Vector2[] Offset(Vector2[] pts, Vector2 offset, float scale = 1f, Vector2 pivot = default)
    {
        var v = new Vector2[pts.Length];
        for (int i = 0; i < pts.Length; i++) v[i] = pivot + (pts[i] - pivot) * scale + offset;
        return v;
    }

    static Vector2 Rotate(Vector2 p, Vector2 c, float deg)
    {
        float a = deg * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
        var d = p - c;
        return c + new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
    }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
    static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
    static Color Mul(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

    static float Hash(int n)
    {
        unchecked
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f; // 0..1
        }
    }

    static float Noise1(float x, int seed)
    {
        int i = Mathf.FloorToInt(x);
        float f = x - i, u = f * f * (3f - 2f * f);
        return Mathf.Lerp(Hash(i + seed * 7919), Hash(i + 1 + seed * 7919), u);
    }

    static float Fbm(float x, int seed)
    {
        float s = 0f, amp = 0.5f, fr = 1f;
        for (int o = 0; o < 5; o++) { s += amp * Noise1(x * fr, seed + o * 31); amp *= 0.5f; fr *= 2.03f; }
        return s; // ~0..1
    }

    // ================================================================== effects

    static Texture2D SakuraPetal()
    {
        var p = new Paint(64, 64);
        Vector2 c1 = new Vector2(18f, 30f), c2 = new Vector2(46f, 30f);
        Func<Vector2, float> petal = q => Mathf.Max(Mathf.Max(SdCircle(q, c1, 26f), SdCircle(q, c2, 26f)), -SdCircle(q, new Vector2(32f, 55.5f), 6.5f));
        p.Draw(q => petal(q) - 1f, A(Hex("F06A9A"), 0.55f));
        p.Draw(petal, q => Color.Lerp(Hex("FFF1F6"), Hex("FF9CC2"), Mathf.Clamp01((q.y - 10f) / 44f)));
        p.Draw(q => SdSegment(q, new Vector2(32f, 14f), new Vector2(32f, 34f), 1.2f), A(Hex("FF7FAE"), 0.45f));
        return p.ToTexture();
    }

    static Texture2D Sparkle()
    {
        var p = new Paint(64, 64);
        var c = new Vector2(32f, 32f);
        p.Glow(c, 30f, A(Color.white, 0.55f), 2.2f);
        var big = Star(c, 4, 31f, 4.2f);
        var small = Star(c, 4, 15f, 3f, 45f);
        p.Draw(q => SdPolygon(q, small), A(Color.white, 0.75f));
        p.Draw(q => SdPolygon(q, big), Color.white);
        return p.ToTexture();
    }

    static Texture2D Halftone()
    {
        var p = new Paint(24, 24);
        p.Draw(q => Mathf.Min(SdCircle(q, new Vector2(12f, 12f), 4.5f),
                    Mathf.Min(Mathf.Min(SdCircle(q, Vector2.zero, 4.5f), SdCircle(q, new Vector2(24f, 0f), 4.5f)),
                              Mathf.Min(SdCircle(q, new Vector2(0f, 24f), 4.5f), SdCircle(q, new Vector2(24f, 24f), 4.5f)))), Color.white);
        return p.ToTexture();
    }

    static Texture2D LightRays()
    {
        const int S = 512;
        var p = new Paint(S, S);
        var c = new Vector2(S / 2f, S / 2f);
        p.Each((x, y, _) =>
        {
            var d = new Vector2(x + 0.5f, y + 0.5f) - c;
            float r = d.magnitude / (S / 2f);
            float a = Mathf.Atan2(d.y, d.x);
            float v = 0.5f + 0.5f * Mathf.Cos(a * 14f);
            float rays = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.9f, v));
            float fall = Mathf.Pow(Mathf.Clamp01(1f - r), 0.8f) * Mathf.Clamp01(r * 8f);
            return new Color(1f, 1f, 1f, rays * fall);
        });
        return p.ToTexture();
    }

    static Texture2D SpeedLines()
    {
        const int S = 768;
        var p = new Paint(S, S);
        var c = new Vector2(S / 2f, S / 2f);
        const int lines = 220;
        p.Each((x, y, _) =>
        {
            var d = new Vector2(x + 0.5f, y + 0.5f) - c;
            float r = d.magnitude / (S * 0.7071f);
            float t = (Mathf.Atan2(d.y, d.x) / (2f * Mathf.PI) + 0.5f) * lines;
            int bucket = Mathf.FloorToInt(t) % lines;
            float f = t - Mathf.Floor(t);
            float h = Hash(bucket * 17 + 3);
            if (h < 0.45f) return new Color(1, 1, 1, 0);
            float start = 0.32f + 0.3f * Hash(bucket * 31 + 7);
            float width = (0.08f + 0.3f * Hash(bucket * 13 + 11)) * Mathf.Clamp01((r - start) / (1f - start));
            float line = Mathf.Clamp01((width - Mathf.Abs(f - 0.5f)) * 6f);
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, start + 0.25f, r));
            return new Color(1f, 1f, 1f, line * fade);
        });
        return p.ToTexture();
    }

    static Texture2D FrameShine()
    {
        var p = new Paint(64, 256);
        p.Each((x, y, _) =>
        {
            float k = 1f - Mathf.Abs((x + 0.5f) / 32f - 1f);
            return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(k), 2.2f) * 0.95f);
        });
        return p.ToTexture();
    }

    // ================================================================== rarity frames (9-sliced, border 64)

    static Texture2D Frame(int tier)
    {
        const int S = FrameSize;
        const float inset = 3f, radius = 22f;
        var paint = new Paint(S, S);
        var center = new Vector2(S / 2f, S / 2f);
        var half = new Vector2(S / 2f - inset, S / 2f - inset);
        var col = CardStats.RarityColor((CardRarity)tier);
        var light = Color.Lerp(col, Color.white, 0.55f);
        var dark = Color.Lerp(col, Color.black, 0.55f);
        float[] thicknessByTier = { 6f, 7f, 8f, 9f, 10f, 11f, 13f, 14f, 15f };
        float thick = thicknessByTier[tier];
        Func<Vector2, float> outer = q => SdRoundBox(q, center, half, radius);

        Color BandColor(Vector2 q)
        {
            float t = Mathf.Clamp01(-outer(q) / thick);
            Color baseCol = col, hi = light, lo = dark;
            if (tier == 7)
            {
                float h = Mathf.Clamp01((q.x + q.y) / (2f * S));
                baseCol = Color.Lerp(Hex("38F3F3"), Hex("FF5BD8"), h);
                hi = Color.Lerp(baseCol, Color.white, 0.6f);
                lo = Color.Lerp(baseCol, Color.black, 0.5f);
            }
            else if (tier == 8)
            {
                baseCol = Hex("FFF6D8"); hi = Color.white; lo = Hex("E6C474");
            }
            Color c = tier >= 6
                ? Color.Lerp(hi, lo, t) + Color.white * (0.45f * Mathf.Max(0f, 1f - Mathf.Abs(t - 0.32f) / 0.14f))
                : Color.Lerp(Color.Lerp(baseCol, hi, 0.35f), Color.Lerp(baseCol, lo, 0.35f), t);
            c.a = 1f;
            return c;
        }

        // Outer dark stroke, main band, inner lines
        paint.Draw(q => Band(outer(q), -1.2f, thick + 1.5f), new Color(0f, 0f, 0f, 0.6f));
        paint.Draw(q => Band(outer(q), 0f, thick), BandColor);
        if (tier >= 2) paint.Draw(q => Band(outer(q), thick + 3f, thick + 5f), A(tier == 8 ? Hex("FFE7A0") : light, 0.95f));
        if (tier >= 5) paint.Draw(q => Band(outer(q), thick + 7f, thick + 8.2f), A(tier >= 7 ? Color.white : light, 0.7f));
        if (tier >= 6) paint.Draw(q => Band(outer(q), thick * 0.45f, thick * 0.55f), A(Color.white, 0.35f));

        // Corner ornaments (stay inside the 64 px slice so they never stretch)
        float ornament = tier switch { 3 => 6f, 4 => 11f, 5 => 13f, 6 => 20f, 7 => 25f, 8 => 30f, _ => 0f };
        if (ornament <= 0f) return paint.ToTexture();
        float o = Mathf.Max(inset + thick * 0.5f + radius * 0.3f, ornament + 2f);
        var outline = new Color(0f, 0f, 0f, 0.65f);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                var c = new Vector2(sx < 0 ? o : S - o, sy < 0 ? o : S - o);
                float bx0 = c.x - ornament - 4f, by0 = c.y - ornament - 4f, bx1 = c.x + ornament + 4f, by1 = c.y + ornament + 4f;
                switch (tier)
                {
                    case 3:
                        paint.Outlined(q => SdCircle(q, c, ornament), q => Color.Lerp(light, col, (c.y + ornament - q.y) / (2f * ornament)), outline, 1.5f, bx0, by0, bx1, by1);
                        break;
                    case 4:
                    {
                        var diamond = Star(c, 2, ornament, ornament * 0.62f, 0f);
                        paint.Outlined(q => SdPolygon(q, diamond), q => Color.Lerp(light, col, (c.y + ornament - q.y) / (2f * ornament)), outline, 1.5f, bx0, by0, bx1, by1);
                        break;
                    }
                    case 5:
                    {
                        // L bracket pointing into the card + a diamond on the corner
                        var inner = new Vector2(sx < 0 ? o + 10f : S - o - 10f, sy < 0 ? o + 10f : S - o - 10f);
                        var ax = inner + new Vector2(-sx * 24f, 0f);
                        var ay = inner + new Vector2(0f, -sy * 24f);
                        paint.Draw(q => Mathf.Min(SdSegment(q, inner, ax, 2.4f), SdSegment(q, inner, ay, 2.4f)), light);
                        var diamond = Star(c, 2, ornament * 0.75f, ornament * 0.5f, 0f);
                        paint.Outlined(q => SdPolygon(q, diamond), q => Color.Lerp(light, col, (c.y + ornament - q.y) / (2f * ornament)), outline, 1.5f, bx0, by0, bx1, by1);
                        break;
                    }
                    case 6:
                    {
                        var star = Star(c, 4, ornament, ornament * 0.36f, 45f);
                        paint.Outlined(q => SdPolygon(q, star), q => Color.Lerp(Hex("FFF4B8"), Hex("C8901A"), (c.y + ornament - q.y) / (2f * ornament)), outline, 1.6f, bx0, by0, bx1, by1);
                        paint.Outlined(q => SdCircle(q, c, ornament * 0.42f), q => Color.Lerp(Hex("FF7A93"), Hex("8A0F2E"), (c.y + ornament - q.y) / (2f * ornament)), A(Hex("5A3A08"), 0.9f), 1.6f, bx0, by0, bx1, by1);
                        paint.Draw(q => SdCircle(q, c + new Vector2(-2.5f, 2.5f), ornament * 0.14f), A(Color.white, 0.85f), bx0, by0, bx1, by1);
                        break;
                    }
                    case 7:
                    {
                        paint.Glow(c, ornament * 1.3f, A(Hex("9FF8FF"), 0.45f));
                        var star = Star(c, 4, ornament, ornament * 0.25f, 0f);
                        var small = Star(c, 4, ornament * 0.55f, ornament * 0.18f, 45f);
                        paint.Outlined(q => SdPolygon(q, small), _ => Hex("FF8BE3"), outline, 1.2f, bx0, by0, bx1, by1);
                        paint.Outlined(q => SdPolygon(q, star), q => Color.Lerp(Color.white, Hex("6CF6F6"), (q - c).magnitude / ornament), outline, 1.4f, bx0, by0, bx1, by1);
                        paint.Draw(q => SdCircle(q, c, ornament * 0.16f), Hex("FF5BD8"), bx0, by0, bx1, by1);
                        break;
                    }
                    default:
                    {
                        paint.Glow(c, ornament * 1.25f, A(Color.white, 0.75f));
                        var star = Star(c, 8, ornament, ornament * 0.42f, 0f);
                        var small = Star(c, 4, ornament * 0.62f, ornament * 0.14f, 0f);
                        paint.Outlined(q => SdPolygon(q, star), q => Color.Lerp(Color.white, Hex("F3D27A"), (q - c).magnitude / ornament), A(Hex("7A5A10"), 0.7f), 1.5f, bx0, by0, bx1, by1);
                        paint.Draw(q => SdPolygon(q, small), Color.white, bx0, by0, bx1, by1);
                        paint.Outlined(q => SdCircle(q, c, ornament * 0.17f), _ => Hex("FFD54F"), A(Hex("7A5A10"), 0.8f), 1.2f, bx0, by0, bx1, by1);
                        break;
                    }
                }
            }
        return paint.ToTexture();
    }

    // ================================================================== element icons (128)

    static readonly Vector2 C128 = new Vector2(64f, 64f);

    static Vector2[] BoltShape() => new[]
    {
        new Vector2(60f, 116f), new Vector2(92f, 116f), new Vector2(73f, 75f), new Vector2(94f, 75f),
        new Vector2(44f, 10f), new Vector2(57f, 58f), new Vector2(36f, 58f)
    };

    static Texture2D ElementIcon(CardElement element)
    {
        var p = new Paint(128, 128);
        var col = ElementSystem.GetColor(element);
        var bgTop = Color.Lerp(col, Color.white, 0.15f);
        var bgBottom = Color.Lerp(col, Color.black, 0.5f);
        p.Draw(q => SdCircle(q, C128, 61.5f), new Color(0f, 0f, 0f, 0.6f));
        p.Draw(q => SdCircle(q, C128, 58f), q => Color.Lerp(bgBottom, bgTop, q.y / 128f));
        p.Draw(q => Band(SdCircle(q, C128, 58f), 2f, 4.5f), A(Color.white, 0.35f));

        Func<Vector2, float> glyph;
        Func<Vector2, float> detail = null;
        Color detailColor = Color.Lerp(col, Color.black, 0.25f);
        switch (element)
        {
            case CardElement.Fire:
            {
                var tri = new[] { new Vector2(40f, 54f), new Vector2(88f, 54f), new Vector2(70f, 112f) };
                var tri2 = new[] { new Vector2(53f, 47f), new Vector2(75f, 47f), new Vector2(67f, 80f) };
                glyph = q => Mathf.Min(SdCircle(q, new Vector2(64f, 48f), 25f), SdPolygon(q, tri));
                detail = q => Mathf.Min(SdCircle(q, new Vector2(64f, 43f), 11.5f), SdPolygon(q, tri2));
                detailColor = Hex("FF8A3C");
                break;
            }
            case CardElement.Water:
            {
                var tri = new[] { new Vector2(40f, 60f), new Vector2(88f, 60f), new Vector2(64f, 112f) };
                glyph = q => Mathf.Min(SdCircle(q, new Vector2(64f, 50f), 26f), SdPolygon(q, tri));
                detail = q => SdSegment(q, new Vector2(51f, 44f), new Vector2(56f, 60f), 4f);
                detailColor = A(Hex("8CC8FF"), 0.9f);
                break;
            }
            case CardElement.Tree:
            {
                glyph = q =>
                {
                    var r = Rotate(q, C128, 38f);
                    float leaf = Mathf.Max(SdCircle(r, new Vector2(44f, 64f), 36f), SdCircle(r, new Vector2(84f, 64f), 36f));
                    float stem = SdSegment(r, new Vector2(64f, 22f), new Vector2(64f, 40f), 3.5f);
                    return Mathf.Min(leaf, stem);
                };
                detail = q =>
                {
                    var r = Rotate(q, C128, 38f);
                    return Mathf.Min(SdSegment(r, new Vector2(64f, 40f), new Vector2(64f, 88f), 2.2f),
                           Mathf.Min(SdSegment(r, new Vector2(64f, 60f), new Vector2(76f, 72f), 1.8f), SdSegment(r, new Vector2(64f, 52f), new Vector2(53f, 63f), 1.8f)));
                };
                break;
            }
            case CardElement.Rock:
            {
                var rock = new[] { new Vector2(28f, 42f), new Vector2(44f, 28f), new Vector2(86f, 28f), new Vector2(102f, 48f),
                                   new Vector2(96f, 78f), new Vector2(74f, 98f), new Vector2(46f, 94f), new Vector2(26f, 70f) };
                glyph = q => SdPolygon(q, rock);
                detail = q => Mathf.Min(Mathf.Min(SdSegment(q, new Vector2(44f, 28f), new Vector2(56f, 62f), 2f), SdSegment(q, new Vector2(56f, 62f), new Vector2(96f, 78f), 2f)),
                                        Mathf.Min(SdSegment(q, new Vector2(56f, 62f), new Vector2(26f, 70f), 2f), SdSegment(q, new Vector2(56f, 62f), new Vector2(74f, 98f), 2f)));
                break;
            }
            case CardElement.Lightning:
            {
                var bolt = Offset(BoltShape(), Vector2.zero, 0.86f, C128);
                glyph = q => SdPolygon(q, bolt);
                break;
            }
            case CardElement.Light:
            {
                var rays = Star(C128, 8, 47f, 24f, 0f);
                glyph = q => Mathf.Min(SdPolygon(q, rays), SdCircle(q, C128, 21f));
                detail = q => Band(SdCircle(q, C128, 21f), -1f, 2.5f);
                detailColor = Hex("F2C94C");
                break;
            }
            case CardElement.Dark:
            {
                var star = Star(new Vector2(88f, 42f), 4, 10f, 2.6f);
                glyph = q => Mathf.Min(Mathf.Max(SdCircle(q, C128, 38f), -SdCircle(q, new Vector2(83f, 76f), 31f)), SdPolygon(q, star));
                break;
            }
            case CardElement.NonNeutral:
                glyph = q => Mathf.Min(Mathf.Abs(SdCircle(q, C128, 30f)) - 6.5f, SdSegment(q, new Vector2(42f, 42f), new Vector2(86f, 86f), 6f));
                break;
            default: // Neutral
                glyph = q => Mathf.Min(Mathf.Abs(SdCircle(q, C128, 30f)) - 6.5f, SdCircle(q, C128, 9f));
                break;
        }

        p.Draw(q => glyph(q + new Vector2(0f, 2.5f)) - 1f, new Color(0f, 0f, 0f, 0.35f)); // drop shadow
        p.Draw(glyph, q => Color.Lerp(Hex("FFFFFF"), Hex("E8ECF5"), 1f - q.y / 128f));
        if (detail != null) p.Draw(detail, detailColor);
        return p.ToTexture();
    }

    // ================================================================== currency / item icons (128)

    static Texture2D GoldCoin()
    {
        var p = new Paint(128, 128);
        p.Draw(q => SdCircle(q, C128, 58f), Hex("8A5A08"));
        p.Draw(q => SdCircle(q, C128, 54f), q => Color.Lerp(Hex("D9961A"), Hex("FFE58A"), q.y / 128f));
        p.Draw(q => Band(SdCircle(q, C128, 44f), -1.5f, 1.5f), A(Hex("A8700E"), 0.9f));
        var star = Star(C128 + new Vector2(0f, -1f), 5, 27f, 11.5f);
        p.Outlined(q => SdPolygon(q, star), q => Color.Lerp(Hex("F3B732"), Hex("FFF5C4"), q.y / 128f), A(Hex("9A650C"), 0.9f), 2f);
        p.Draw(q => SdSegment(q, new Vector2(34f, 84f), new Vector2(46f, 98f), 5f), A(Color.white, 0.45f));
        return p.ToTexture();
    }

    static Texture2D Diamond()
    {
        var p = new Paint(128, 128);
        var gem = new[] { new Vector2(22f, 82f), new Vector2(42f, 104f), new Vector2(86f, 104f), new Vector2(106f, 82f), new Vector2(64f, 18f) };
        p.Glow(C128, 62f, A(Hex("7FDFFF"), 0.35f));
        p.Outlined(q => SdPolygon(q, gem), q => Color.Lerp(Hex("2D9BEA"), Hex("C9F6FF"), q.y / 110f), A(Hex("0E4F80"), 0.95f), 2.5f);
        Func<Vector2, float> facets = q => Mathf.Min(Mathf.Min(SdSegment(q, new Vector2(22f, 82f), new Vector2(106f, 82f), 1.3f), SdSegment(q, new Vector2(42f, 104f), new Vector2(54f, 82f), 1.3f)),
                                            Mathf.Min(Mathf.Min(SdSegment(q, new Vector2(86f, 104f), new Vector2(74f, 82f), 1.3f), SdSegment(q, new Vector2(54f, 82f), new Vector2(64f, 18f), 1.3f)),
                                                      SdSegment(q, new Vector2(74f, 82f), new Vector2(64f, 18f), 1.3f)));
        p.Draw(facets, A(Color.white, 0.6f));
        var shine = new[] { new Vector2(44f, 100f), new Vector2(54f, 86f), new Vector2(36f, 86f) };
        p.Draw(q => SdPolygon(q, shine), A(Color.white, 0.7f));
        return p.ToTexture();
    }

    /// <summary>Glossy orb. glyph: 0 = up arrow (fusion), 1 = diamond star (element), 2 = star (talent).</summary>
    static Texture2D Orb(Color col, int glyph)
    {
        var p = new Paint(128, 128);
        var light = new Vector3(-0.45f, 0.6f, 0.66f).normalized;
        p.Glow(C128, 63f, A(col, 0.45f));
        p.Draw(q => SdCircle(q, C128, 53f), A(Color.Lerp(col, Color.black, 0.65f), 1f));
        p.Draw(q => SdCircle(q, C128, 50f), q =>
        {
            var n2 = (q - C128) / 50f;
            float z = Mathf.Sqrt(Mathf.Max(0f, 1f - n2.sqrMagnitude));
            float lam = Mathf.Max(0f, Vector3.Dot(new Vector3(n2.x, n2.y, z), light));
            float spec = Mathf.Pow(lam, 22f);
            float rim = Mathf.Pow(1f - z, 3f) * 0.6f;
            var c = col * (0.32f + 0.8f * lam) + Color.white * (spec * 0.9f) + Color.Lerp(col, Color.white, 0.5f) * rim;
            c.a = 1f;
            return c;
        });
        Vector2[] shape;
        if (glyph == 0) shape = new[] { new Vector2(64f, 94f), new Vector2(88f, 66f), new Vector2(74f, 66f), new Vector2(74f, 38f), new Vector2(54f, 38f), new Vector2(54f, 66f), new Vector2(40f, 66f) };
        else if (glyph == 1) shape = Star(C128, 4, 28f, 9f);
        else shape = Star(C128 + new Vector2(0f, -2f), 5, 27f, 11f);
        p.Draw(q => SdPolygon(q, shape) - 2f, A(Color.Lerp(col, Color.black, 0.6f), 0.55f));
        p.Draw(q => SdPolygon(q, shape), A(Color.white, 0.92f));
        p.Draw(q => SdCircle(q, new Vector2(46f, 86f), 9f), A(Color.white, 0.55f));
        return p.ToTexture();
    }

    static Texture2D StaminaBolt()
    {
        var p = new Paint(128, 128);
        var bolt = BoltShape();
        p.Glow(C128, 60f, A(Hex("9DFF7A"), 0.3f));
        p.Outlined(q => SdPolygon(q, bolt), q => Color.Lerp(Hex("2FBF5A"), Hex("D8FF7A"), q.y / 128f), A(Hex("135A26"), 0.95f), 3f);
        p.Draw(q => SdSegment(q, new Vector2(68f, 108f), new Vector2(80f, 108f), 2.5f), A(Color.white, 0.6f));
        return p.ToTexture();
    }

    static Texture2D Lock()
    {
        var p = new Paint(128, 128);
        Func<Vector2, float> shackle = q => q.y < 62f ? 999f : Mathf.Abs(SdCircle(q, new Vector2(64f, 70f), 24f)) - 7f;
        Func<Vector2, float> shackleLegs = q => Mathf.Min(SdBox(q, new Vector2(40f, 62f), new Vector2(7f, 10f)), SdBox(q, new Vector2(88f, 62f), new Vector2(7f, 10f)));
        Func<Vector2, float> shape = q => Mathf.Min(Mathf.Min(shackle(q), shackleLegs(q)), SdRoundBox(q, new Vector2(64f, 42f), new Vector2(38f, 28f), 9f));
        p.Draw(q => shape(q) - 3f, A(Hex("2A2F3D"), 0.95f));
        p.Draw(q => Mathf.Min(shackle(q), shackleLegs(q)), q => Color.Lerp(Hex("8E98AB"), Hex("E3E8F2"), (q.y - 40f) / 60f));
        p.Draw(q => SdRoundBox(q, new Vector2(64f, 42f), new Vector2(38f, 28f), 9f), q => Color.Lerp(Hex("C08A1E"), Hex("FFE08A"), (q.y - 14f) / 56f));
        p.Draw(q => Mathf.Min(SdCircle(q, new Vector2(64f, 48f), 7f), SdBox(q, new Vector2(64f, 36f), new Vector2(3f, 9f))), Hex("4A3206"));
        return p.ToTexture();
    }

    // ================================================================== screen backgrounds (960 x 540, anime scenery placeholders)

    const int BgW = 960, BgH = 540;

    static void Sky(Paint p, Color top, Color mid, Color bottom, float midAt = 0.45f)
    {
        p.Each((x, y, _) =>
        {
            float t = (y + 0.5f) / BgH;
            var c = t < midAt ? Color.Lerp(bottom, mid, t / midAt) : Color.Lerp(mid, top, (t - midAt) / (1f - midAt));
            c.a = 1f;
            return c;
        });
    }

    static void Stars(Paint p, int count, int seed, float minY, float brightness = 1f)
    {
        var rng = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            var c = new Vector2((float)rng.NextDouble() * BgW, minY + (float)rng.NextDouble() * (BgH - minY));
            float r = 0.6f + (float)rng.NextDouble() * 1.4f;
            float a = (0.35f + 0.65f * (float)rng.NextDouble()) * brightness;
            if (r > 1.6f)
            {
                var star = Star(c, 4, r * 3.2f, r * 0.6f);
                p.Draw(q => SdPolygon(q, star), A(Color.white, a), c.x - 8, c.y - 8, c.x + 8, c.y + 8);
            }
            else p.Draw(q => SdCircle(q, c, r), A(Color.white, a), c.x - 3, c.y - 3, c.x + 3, c.y + 3);
        }
    }

    static void Moon(Paint p, Vector2 c, float r, Color color, float glow = 0.45f)
    {
        p.Glow(c, r * 3.2f, A(color, glow * 0.6f), 2.5f);
        p.Glow(c, r * 1.6f, A(color, glow), 1.5f);
        p.Draw(q => SdCircle(q, c, r), q => Color.Lerp(color, Color.white, Mathf.Clamp01((q.y - c.y) / r * 0.5f + 0.3f)), c.x - r - 2, c.y - r - 2, c.x + r + 2, c.y + r + 2);
        var rng = new System.Random((int)(c.x * 7 + r));
        for (int i = 0; i < 5; i++)
        {
            var cc = c + new Vector2(((float)rng.NextDouble() - 0.5f) * r * 1.1f, ((float)rng.NextDouble() - 0.5f) * r * 1.1f);
            float cr = r * (0.08f + 0.12f * (float)rng.NextDouble());
            p.Draw(q => Mathf.Max(SdCircle(q, cc, cr), SdCircle(q, c, r - 2f)), A(Color.Lerp(color, Color.black, 0.25f), 0.25f), cc.x - cr - 2, cc.y - cr - 2, cc.x + cr + 2, cc.y + cr + 2);
        }
    }

    static void Mountains(Paint p, float baseY, float amplitude, float frequency, int seed, Color top, Color bottom, bool sharp = false)
    {
        for (int x = 0; x < BgW; x++)
        {
            float n = Fbm(x * frequency, seed);
            if (sharp) n = 1f - Mathf.Abs(n * 2f - 1f);
            float h = baseY + amplitude * n;
            int ih = Mathf.Min(BgH - 1, Mathf.FloorToInt(h));
            for (int y = 0; y <= ih; y++)
            {
                float cov = y == ih ? h - ih : 1f;
                var c = Color.Lerp(bottom, top, Mathf.Clamp01(y / Mathf.Max(1f, h)));
                p.Blend(y * BgW + x, c, cov);
            }
        }
    }

    static void Fog(Paint p, float height, Color color)
    {
        p.Each((x, y, c) =>
        {
            float a = Mathf.Clamp01(1f - y / height) * color.a * (0.75f + 0.25f * Mathf.Sin(x * 0.012f + y * 0.03f));
            return Color.Lerp(c, new Color(color.r, color.g, color.b, 1f), a);
        });
    }

    static void Torii(Paint p, float cx, float groundY, float scale, Color color)
    {
        float s = scale;
        p.Draw(q => Mathf.Min(SdBox(q, new Vector2(cx - 38 * s, groundY + 40 * s), new Vector2(4.5f * s, 40 * s)), SdBox(q, new Vector2(cx + 38 * s, groundY + 40 * s), new Vector2(4.5f * s, 40 * s))), color);
        p.Draw(q => SdBox(q, new Vector2(cx, groundY + 64 * s), new Vector2(50 * s, 3.5f * s)), color);
        var top = new[] { new Vector2(cx - 62 * s, groundY + 82 * s), new Vector2(cx + 62 * s, groundY + 82 * s), new Vector2(cx + 54 * s, groundY + 74 * s), new Vector2(cx - 54 * s, groundY + 74 * s) };
        p.Draw(q => SdPolygon(q, top) - 1.5f * s, color);
        p.Draw(q => SdBox(q, new Vector2(cx, groundY + 71 * s), new Vector2(3f * s, 5 * s)), color);
    }

    static void Skyline(Paint p, float baseY, int seed, Color color, Color windows, float maxHeight, float windowChance)
    {
        var rng = new System.Random(seed);
        float x = -10f;
        while (x < BgW + 10)
        {
            float w = 26f + (float)rng.NextDouble() * 60f;
            float h = maxHeight * (0.3f + 0.7f * (float)rng.NextDouble());
            var c = new Vector2(x + w / 2f, baseY + h / 2f);
            var half = new Vector2(w / 2f, h / 2f);
            p.Draw(q => SdBox(q, c, half), color, x - 1, 0, x + w + 1, baseY + h + 1);
            if (rng.NextDouble() < 0.3) p.Draw(q => SdBox(q, new Vector2(c.x, baseY + h + 8), new Vector2(1.2f, 8f)), color, c.x - 3, baseY + h, c.x + 3, baseY + h + 17);
            for (float wy = baseY + 10; wy < baseY + h - 8; wy += 11)
                for (float wx = x + 6; wx < x + w - 6; wx += 9)
                    if (rng.NextDouble() < windowChance)
                    {
                        var wc = new Vector2(wx + 2, wy + 3);
                        p.Draw(q => SdBox(q, wc, new Vector2(2f, 3f)), A(windows, 0.55f + 0.45f * (float)rng.NextDouble()), wc.x - 3, wc.y - 4, wc.x + 3, wc.y + 4);
                    }
            x += w + (float)rng.NextDouble() * 6f;
        }
    }

    static void Branch(Paint p, System.Random rng, Vector2 start, float angle, float length, float width, int depth, Color wood, Color blossom, Color blossomLight)
    {
        var end = start + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * length;
        float pad = width + 2f;
        p.Draw(q => SdSegment(q, start, end, width), wood, Mathf.Min(start.x, end.x) - pad, Mathf.Min(start.y, end.y) - pad, Mathf.Max(start.x, end.x) + pad, Mathf.Max(start.y, end.y) + pad);
        if (depth <= 0 || length < 12f)
        {
            int n = 6 + rng.Next(6);
            for (int i = 0; i < n; i++)
            {
                var c = end + new Vector2(((float)rng.NextDouble() - 0.5f) * 44f, ((float)rng.NextDouble() - 0.5f) * 36f);
                float r = 6f + (float)rng.NextDouble() * 9f;
                var col = Color.Lerp(blossom, blossomLight, (float)rng.NextDouble());
                p.Draw(q => SdCircle(q, c, r), A(col, 0.92f), c.x - r - 2, c.y - r - 2, c.x + r + 2, c.y + r + 2);
                var flower = Star(c, 5, r * 0.55f, r * 0.3f, (float)rng.NextDouble() * 72f);
                p.Draw(q => SdPolygon(q, flower), A(Color.white, 0.35f), c.x - r, c.y - r, c.x + r, c.y + r);
            }
            return;
        }
        int kids = 2 + (rng.NextDouble() < 0.3 ? 1 : 0);
        for (int k = 0; k < kids; k++)
        {
            float a = angle + ((float)rng.NextDouble() * 2f - 1f) * 42f;
            Branch(p, rng, end, a, length * (0.62f + 0.18f * (float)rng.NextDouble()), width * 0.66f, depth - 1, wood, blossom, blossomLight);
        }
    }

    static void MagicCircle(Paint p, Vector2 c, float r, Color color, float rot)
    {
        p.Draw(q => Band(SdCircle(q, c, r), -1.5f, 1.5f), color, c.x - r - 4, c.y - r - 4, c.x + r + 4, c.y + r + 4);
        p.Draw(q => Band(SdCircle(q, c, r * 0.92f), -0.8f, 0.8f), color, c.x - r, c.y - r, c.x + r, c.y + r);
        p.Draw(q => Band(SdCircle(q, c, r * 0.6f), -1.2f, 1.2f), color, c.x - r, c.y - r, c.x + r, c.y + r);
        var hexA = Star(c, 3, r * 0.9f, r * 0.9f * 0.5f, rot);
        var tri1 = new[] { hexA[0], hexA[2], hexA[4] };
        var tri2 = new[] { Rotate(hexA[0], c, 180f), Rotate(hexA[2], c, 180f), Rotate(hexA[4], c, 180f) };
        p.Draw(q => Mathf.Abs(SdPolygon(q, tri1)) - 1.1f, color, c.x - r, c.y - r, c.x + r, c.y + r);
        p.Draw(q => Mathf.Abs(SdPolygon(q, tri2)) - 1.1f, color, c.x - r, c.y - r, c.x + r, c.y + r);
        for (int i = 0; i < 24; i++)
        {
            float a = (rot + i * 15f) * Mathf.Deg2Rad;
            var d = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.96f;
            float dr = i % 3 == 0 ? 3.2f : 1.6f;
            p.Draw(q => SdCircle(q, d, dr), color, d.x - 5, d.y - 5, d.x + 5, d.y + 5);
        }
    }

    static void Lantern(Paint p, Vector2 c, float s, Color color)
    {
        p.Glow(c, 46f * s, A(color, 0.45f));
        p.Draw(q => SdRoundBox(q, c, new Vector2(11f * s, 15f * s), 8f * s), q => Color.Lerp(Color.Lerp(color, Color.black, 0.3f), Color.Lerp(color, Color.white, 0.4f), (q.y - c.y + 15f * s) / (30f * s)), c.x - 14 * s, c.y - 18 * s, c.x + 14 * s, c.y + 18 * s);
        p.Draw(q => Mathf.Min(SdBox(q, c + new Vector2(0, 15f * s), new Vector2(6f * s, 2f * s)), SdBox(q, c - new Vector2(0, 15f * s), new Vector2(6f * s, 2f * s))), Hex("2A1408"), c.x - 8 * s, c.y - 19 * s, c.x + 8 * s, c.y + 19 * s);
    }

    static Texture2D Background(string key)
    {
        var p = new Paint(BgW, BgH);
        switch (key)
        {
            case "menu":
            {
                Sky(p, Hex("2A1A55"), Hex("B24C8C"), Hex("FFB59E"), 0.38f);
                Stars(p, 90, 11, BgH * 0.55f);
                Moon(p, new Vector2(690f, 370f), 78f, Hex("FFF1D6"), 0.5f);
                Mountains(p, 150f, 120f, 0.006f, 3, Hex("7B4C93"), Hex("A0609A"), true);
                Mountains(p, 90f, 90f, 0.009f, 8, Hex("4A2A6A"), Hex("5E3478"));
                Mountains(p, 40f, 70f, 0.004f, 21, Hex("2A1440"), Hex("341A4E"));
                Torii(p, 250f, 92f, 1.15f, Hex("1E0F30"));
                Fog(p, 120f, A(Hex("FFC3D6"), 0.35f));
                Branch(p, new System.Random(5), new Vector2(-20f, 560f), -30f, 150f, 11f, 4, Hex("2E1830"), Hex("FF8FB8"), Hex("FFD6E6"));
                Branch(p, new System.Random(9), new Vector2(990f, 520f), 205f, 120f, 9f, 3, Hex("2E1830"), Hex("FF8FB8"), Hex("FFD6E6"));
                break;
            }
            case "map":
            {
                Sky(p, Hex("0B1430"), Hex("23396F"), Hex("5E73B8"), 0.4f);
                Stars(p, 180, 23, BgH * 0.35f);
                Moon(p, new Vector2(180f, 420f), 44f, Hex("E8F0FF"), 0.4f);
                Mountains(p, 170f, 140f, 0.005f, 4, Hex("3B4F8C"), Hex("4A62A3"), true);
                Mountains(p, 110f, 100f, 0.008f, 12, Hex("25356A"), Hex("2E417C"), true);
                Mountains(p, 50f, 70f, 0.006f, 19, Hex("141E44"), Hex("1A2652"));
                var rng = new System.Random(4);
                for (int i = 0; i < 18; i++) Lantern(p, new Vector2((float)rng.NextDouble() * BgW, 30f + (float)rng.NextDouble() * 60f), 0.35f, Hex("FFC266"));
                Fog(p, 90f, A(Hex("8FA6FF"), 0.25f));
                break;
            }
            case "battle":
            {
                Sky(p, Hex("2A0618"), Hex("A11F3A"), Hex("FF9A3C"), 0.32f);
                p.Glow(new Vector2(BgW / 2f, 120f), 520f, A(Hex("FFB347"), 0.55f), 1.6f);
                p.Draw(q => SdCircle(q, new Vector2(BgW / 2f, 110f), 120f), q => Color.Lerp(Hex("FFE7A3"), Hex("FF8C3A"), 1f - (q.y - 0f) / 230f));
                Skyline(p, 60f, 31, Hex("5A1426"), Hex("FF9E6B"), 210f, 0.05f);
                Skyline(p, 0f, 77, Hex("1A040C"), Hex("FFC07A"), 170f, 0.12f);
                break;
            }
            case "cards":
            {
                Sky(p, Hex("8C6CD0"), Hex("E6A4D8"), Hex("FFD9E8"), 0.5f);
                p.Glow(new Vector2(260f, 420f), 380f, A(Color.white, 0.35f));
                Mountains(p, 40f, 50f, 0.005f, 7, Hex("C58BC9"), Hex("D79CCD"));
                var tree = new System.Random(17);
                Branch(p, tree, new Vector2(800f, -10f), 95f, 170f, 18f, 5, Hex("4A2A3E"), Hex("F59AC4"), Hex("FFE3EF"));
                Branch(p, new System.Random(2), new Vector2(-30f, 470f), -18f, 140f, 10f, 3, Hex("4A2A3E"), Hex("F59AC4"), Hex("FFE3EF"));
                break;
            }
            case "gacha":
            {
                Sky(p, Hex("10042A"), Hex("3A1270"), Hex("7A2CB0"), 0.4f);
                Stars(p, 160, 41, 0f, 0.9f);
                var c = new Vector2(BgW / 2f, BgH / 2f - 10f);
                p.Glow(c, 360f, A(Hex("C77DFF"), 0.45f), 1.5f);
                MagicCircle(p, c, 240f, A(Hex("FFD68A"), 0.55f), 0f);
                MagicCircle(p, c, 150f, A(Hex("FF9BE0"), 0.5f), 15f);
                Fog(p, 110f, A(Hex("B06CFF"), 0.3f));
                break;
            }
            default: // shop
            {
                Sky(p, Hex("1E0F33"), Hex("7A2C55"), Hex("F28C5C"), 0.42f);
                Stars(p, 60, 53, BgH * 0.6f, 0.8f);
                Skyline(p, 0f, 12, Hex("2A0F22"), Hex("FFB86B"), 260f, 0.22f);
                for (int i = 0; i < 9; i++)
                {
                    float x = 60f + i * 105f;
                    float y = 470f - 26f * Mathf.Sin((i + 0.5f) / 9f * Mathf.PI);
                    Lantern(p, new Vector2(x, y), 1f, i % 2 == 0 ? Hex("FF5A4A") : Hex("FFB23D"));
                }
                p.Draw(q => Mathf.Abs(q.y - (496f - 26f * Mathf.Sin(q.x / BgW * Mathf.PI))) - 1.2f, A(Hex("2A1408"), 0.9f));
                Fog(p, 80f, A(Hex("FF9E7A"), 0.25f));
                break;
            }
        }
        return p.ToTexture();
    }

    // ================================================================== README for user art

    static string Readme()
    {
        var sb = new StringBuilder();
        sb.AppendLine("ANIGAME - YOUR ART GOES HERE");
        sb.AppendLine("==============================");
        sb.AppendLine("Drop files with these exact names (PNG recommended, transparent background unless noted), then run");
        sb.AppendLine("Tools > AniGame > Steps > 4. Generate UI Art (or Build Everything). Missing files use generated placeholders.");
        sb.AppendLine("Colour emoji can't be shown by Unity's legacy Text, so every icon must be a PNG.");
        sb.AppendLine();
        sb.AppendLine("Backgrounds/  1920x1080 (16:9), opaque PNG or JPG");
        foreach (var k in BackgroundKeys) sb.AppendLine($"   {k}.png");
        sb.AppendLine();
        sb.AppendLine("Frames/  card frames, 512x716 PNG, transparent centre, ornaments inside the outer 128 px (9-slice border 128)");
        foreach (var r in RarityFileNames) sb.AppendLine($"   frame_{r}.png");
        sb.AppendLine("   Tip: God frames should be light/white - the game tints them with a moving rainbow.");
        sb.AppendLine();
        sb.AppendLine("Elements/  256x256 PNG, round badge");
        foreach (var e in ElementFileNames) sb.AppendLine($"   element_{e}.png");
        sb.AppendLine();
        sb.AppendLine("Icons/  256x256 PNG");
        foreach (var i in new[] { "gold", "diamond", "fusion_orb", "element_orb", "talent_orb", "stamina", "lock" }) sb.AppendLine($"   {i}.png");
        sb.AppendLine();
        sb.AppendLine("Packs/  512x720 PNG (gacha pack covers)");
        foreach (var pck in Packs.All) sb.AppendLine($"   pack_{pck.id}.png   ({pck.name})");
        sb.AppendLine();
        sb.AppendLine("Effects/  optional");
        sb.AppendLine("   sakura_petal.png 64x64 white/pink petal    sparkle.png 64x64 white 4-point star");
        sb.AppendLine("   halftone.png 24x24 tiling dots              light_rays.png 512x512 white rays on transparent");
        sb.AppendLine("   speed_lines.png 1024x1024 manga speed lines  frame_shine.png 64x256 vertical white band");
        sb.AppendLine();
        sb.AppendLine("logo.png  1200x400 transparent game logo (main menu)");
        sb.AppendLine();
        sb.AppendLine("Audio/  .ogg, .mp3 or .wav, loopable");
        sb.AppendLine("   menu_music   battle_music   gacha_music");
        return sb.ToString();
    }
}
