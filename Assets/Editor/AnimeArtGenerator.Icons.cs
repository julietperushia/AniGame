using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Talent icons, buff icons, the God Orb and the chart arrow (placeholders; user files in Assets/Art/UserArt override them).</summary>
public static partial class AnimeArtGenerator
{
    public static string Slug(string name) => UITheme.IconKey(name);

    public static List<Talent> TalentAssets()
    {
        var list = new List<Talent>();
        var seen = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Talent"))
        {
            var t = AssetDatabase.LoadAssetAtPath<Talent>(AssetDatabase.GUIDToAssetPath(guid));
            if (t == null || string.IsNullOrEmpty(t.DisplayName) || !seen.Add(Slug(t.DisplayName))) continue;
            list.Add(t);
        }
        return list.OrderBy(t => t.DisplayName).ToList();
    }

    static void GenerateIcons(UITheme theme)
    {
        theme.godOrbIcon = Pick("Icons/god_orb", () => Save("icon_god_orb", GodOrb(), 0));
        theme.arrow = Pick("Effects/arrow", () => Save("fx_arrow", Arrow(), 0));

        var talents = TalentAssets();
        theme.talentIconNames = talents.Select(t => Slug(t.DisplayName)).ToArray();
        theme.talentIcons = new Sprite[talents.Count];
        for (int i = 0; i < talents.Count; i++)
        {
            var t = talents[i];
            string slug = Slug(t.DisplayName);
            theme.talentIcons[i] = Pick("Talents/talent_" + slug, () => Save("talent_" + slug, TalentIcon(slug, t.IsActive), 0));
        }

        theme.buffIconIds = Buffs.All.Select(b => b.id).ToArray();
        theme.buffIcons = new Sprite[Buffs.All.Length];
        for (int i = 0; i < Buffs.All.Length; i++)
        {
            var b = Buffs.All[i];
            theme.buffIcons[i] = Pick("Buffs/buff_" + b.id, () => Save("buff_" + b.id, BuffIcon(b), 0));
        }
    }

    // ================================================================== glyph library (128 px space, white shapes)

    static Func<Vector2, float> At(Func<Vector2, float> glyph, Vector2 center, float scale) =>
        q => glyph(C128 + (q - center) / scale) * scale;

    static Func<Vector2, float> Rot(Func<Vector2, float> glyph, float deg) => q => glyph(Rotate(q, C128, deg));

    static float Union(params float[] d) { float m = d[0]; for (int i = 1; i < d.Length; i++) m = Mathf.Min(m, d[i]); return m; }

    static readonly Vector2[] SwordBlade = { new Vector2(59f, 50f), new Vector2(69f, 50f), new Vector2(69f, 96f), new Vector2(64f, 110f), new Vector2(59f, 96f) };
    static readonly Vector2[] ShieldShape = { new Vector2(34f, 98f), new Vector2(94f, 98f), new Vector2(92f, 60f), new Vector2(64f, 22f), new Vector2(36f, 60f) };
    static readonly Vector2[] Chevron = { new Vector2(36f, 50f), new Vector2(64f, 78f), new Vector2(92f, 50f), new Vector2(92f, 34f), new Vector2(64f, 62f), new Vector2(36f, 34f) };

    static float Sword(Vector2 q) => Union(SdPolygon(q, SwordBlade), SdBox(q, new Vector2(64f, 46f), new Vector2(22f, 4f)),
                                           SdBox(q, new Vector2(64f, 34f), new Vector2(4.5f, 9f)), SdCircle(q, new Vector2(64f, 22f), 6f));
    static float Shield(Vector2 q) => SdPolygon(q, ShieldShape) - 2f;
    static float Heart(Vector2 q) => Union(SdCircle(q, new Vector2(49f, 74f), 19f), SdCircle(q, new Vector2(79f, 74f), 19f),
                                           SdPolygon(q, new[] { new Vector2(31f, 68f), new Vector2(97f, 68f), new Vector2(64f, 26f) }));
    static float Drop(Vector2 q) => Union(SdCircle(q, new Vector2(64f, 50f), 25f), SdPolygon(q, new[] { new Vector2(41f, 60f), new Vector2(87f, 60f), new Vector2(64f, 110f) }));
    static float Flame(Vector2 q) => Union(
        SdCircle(q, new Vector2(64f, 46f), 25f), SdPolygon(q, new[] { new Vector2(40f, 52f), new Vector2(88f, 52f), new Vector2(70f, 116f) }),
        SdCircle(q, new Vector2(40f, 44f), 13f), SdPolygon(q, new[] { new Vector2(27f, 46f), new Vector2(53f, 46f), new Vector2(30f, 86f) }),
        SdCircle(q, new Vector2(88f, 46f), 12f), SdPolygon(q, new[] { new Vector2(76f, 48f), new Vector2(100f, 48f), new Vector2(99f, 82f) }));
    static float Coin(Vector2 q) => SdCircle(q, C128, 38f);
    static float Ring(Vector2 q, float r, float w) => Mathf.Abs(SdCircle(q, C128, r)) - w;
    static float Plus(Vector2 q, Vector2 c, float len, float w) => Mathf.Min(SdBox(q, c, new Vector2(len, w)), SdBox(q, c, new Vector2(w, len)));
    static float Crack(Vector2 q) => Union(SdSegment(q, new Vector2(56f, 104f), new Vector2(70f, 76f), 3f), SdSegment(q, new Vector2(70f, 76f), new Vector2(56f, 56f), 3f),
                                           SdSegment(q, new Vector2(56f, 56f), new Vector2(68f, 24f), 3f));

    /// <summary>Main glyph (white) and optional detail drawn in the badge's dark colour.</summary>
    static void Glyph(string name, out Func<Vector2, float> shape, out Func<Vector2, float> detail)
    {
        detail = null;
        switch (name)
        {
            case "sword": shape = Rot(Sword, 40f); break;
            case "swords": shape = q => Mathf.Min(Rot(Sword, 40f)(q), Rot(Sword, -40f)(q)); break;
            case "swordburst":
            {
                var burst = Star(C128, 8, 50f, 26f, 22.5f);
                shape = q => Mathf.Min(SdPolygon(q, burst), Rot(Sword, 40f)(q) - 1f);
                detail = q => Rot(Sword, 40f)(q) + 2.5f;
                break;
            }
            case "brokensword": shape = q => Mathf.Max(Rot(Sword, 40f)(q), -Rot(Crack, 40f)(q)); break;
            case "shield": shape = Shield; detail = q => Band(SdPolygon(q, ShieldShape), 9f, 12f); break;
            case "shieldplus": shape = Shield; detail = q => Plus(q, new Vector2(64f, 64f), 16f, 5f); break;
            case "brokenshield": shape = q => Mathf.Max(Shield(q), -Crack(q)); break;
            case "flame":
            {
                shape = Flame;
                detail = q => Union(SdCircle(q, new Vector2(64f, 42f), 11f), SdPolygon(q, new[] { new Vector2(54f, 46f), new Vector2(74f, 46f), new Vector2(67f, 80f) }));
                break;
            }
            case "drop": shape = Drop; detail = q => SdSegment(q, new Vector2(52f, 44f), new Vector2(56f, 60f), 4f); break;
            case "poison": shape = Drop; detail = q => Union(SdCircle(q, new Vector2(56f, 54f), 5f), SdCircle(q, new Vector2(72f, 54f), 5f), SdBox(q, new Vector2(64f, 38f), new Vector2(9f, 3f))); break;
            case "bolt": { var bolt = Offset(BoltShape(), Vector2.zero, 0.82f, C128); shape = q => SdPolygon(q, bolt); break; }
            case "star": { var st = Star(C128 + new Vector2(0f, -2f), 5, 42f, 18f); shape = q => SdPolygon(q, st); break; }
            case "sparkle": { var s1 = Star(C128, 4, 44f, 9f); var s2 = Star(C128 + new Vector2(28f, 28f), 4, 14f, 3.5f); shape = q => Mathf.Min(SdPolygon(q, s1), SdPolygon(q, s2)); break; }
            case "heart": shape = Heart; break;
            case "heartplus": shape = Heart; detail = q => Plus(q, new Vector2(64f, 64f), 15f, 4.5f); break;
            case "heartdrop": shape = Heart; detail = q => At(Drop, new Vector2(64f, 62f), 0.34f)(q); break;
            case "brokenheart": shape = q => Mathf.Max(Heart(q), -Crack(q)); break;
            case "crosshair":
                shape = q => Union(Ring(q, 30f, 4.5f), SdBox(q, new Vector2(64f, 104f), new Vector2(3.5f, 12f)), SdBox(q, new Vector2(64f, 24f), new Vector2(3.5f, 12f)),
                                   SdBox(q, new Vector2(24f, 64f), new Vector2(12f, 3.5f)), SdBox(q, new Vector2(104f, 64f), new Vector2(12f, 3.5f)), SdCircle(q, C128, 7f));
                break;
            case "eye":
                shape = q => Mathf.Max(SdCircle(q, new Vector2(64f, 30f), 52f), SdCircle(q, new Vector2(64f, 98f), 52f));
                detail = q => Mathf.Max(SdCircle(q, C128, 16f), -SdCircle(q, C128, 6f));
                break;
            case "crown":
            {
                var crown = new[] { new Vector2(30f, 40f), new Vector2(98f, 40f), new Vector2(104f, 92f), new Vector2(84f, 66f), new Vector2(64f, 98f), new Vector2(44f, 66f), new Vector2(24f, 92f) };
                shape = q => Union(SdPolygon(q, crown), SdBox(q, new Vector2(64f, 32f), new Vector2(34f, 6f)), SdCircle(q, new Vector2(24f, 94f), 6f), SdCircle(q, new Vector2(64f, 100f), 6f), SdCircle(q, new Vector2(104f, 94f), 6f));
                detail = q => SdCircle(q, new Vector2(64f, 54f), 6f);
                break;
            }
            case "coin":
            {
                var st = Star(C128 + new Vector2(0f, -1f), 5, 20f, 8.5f);
                shape = Coin;
                detail = q => Mathf.Min(Ring(q, 30f, 1.8f), SdPolygon(q, st));
                break;
            }
            case "badcoin":
            {
                shape = q => Mathf.Max(Coin(q), -Crack(q));
                detail = q => Ring(q, 30f, 1.8f);
                break;
            }
            case "coincycle":
            {
                var head = new[] { new Vector2(90f, 96f), new Vector2(108f, 74f), new Vector2(84f, 72f) };
                shape = q => Union(SdCircle(q, C128 + new Vector2(-6f, -6f), 28f), Mathf.Max(Ring(q, 44f, 4f), -(q.x - 70f)), SdPolygon(q, head));
                detail = q => Ring(q + new Vector2(6f, 6f), 21f, 1.8f);
                break;
            }
            case "cycle":
            {
                var head = new[] { new Vector2(84f, 104f), new Vector2(108f, 84f), new Vector2(80f, 78f) };
                var head2 = new[] { new Vector2(44f, 24f), new Vector2(20f, 44f), new Vector2(48f, 50f) };
                shape = q => Union(Mathf.Max(Ring(q, 32f, 6f), -Mathf.Min(SdBox(q, new Vector2(96f, 72f), new Vector2(12f, 10f)), SdBox(q, new Vector2(32f, 56f), new Vector2(12f, 10f)))), SdPolygon(q, head), SdPolygon(q, head2));
                break;
            }
            case "lock":
            {
                shape = q => Union(q.y < 62f ? 999f : Mathf.Abs(SdCircle(q, new Vector2(64f, 70f), 20f)) - 6f,
                                   SdBox(q, new Vector2(44f, 62f), new Vector2(6f, 8f)), SdBox(q, new Vector2(84f, 62f), new Vector2(6f, 8f)),
                                   SdRoundBox(q, new Vector2(64f, 44f), new Vector2(32f, 24f), 7f));
                detail = q => Mathf.Min(SdCircle(q, new Vector2(64f, 48f), 6f), SdBox(q, new Vector2(64f, 38f), new Vector2(2.5f, 8f)));
                break;
            }
            case "key":
                shape = q => Union(Ring(q + new Vector2(20f, -18f), 15f, 6f), SdSegment(q, new Vector2(56f, 72f), new Vector2(98f, 30f), 6f),
                                   SdSegment(q, new Vector2(84f, 44f), new Vector2(94f, 54f), 5f), SdSegment(q, new Vector2(74f, 54f), new Vector2(82f, 62f), 5f));
                break;
            case "cube":
            {
                var hex = Star(C128, 3, 42f, 42f, 0f);
                shape = q => SdPolygon(q, hex);
                detail = q => Union(SdSegment(q, C128, new Vector2(64f, 22f), 2.5f), SdSegment(q, C128, new Vector2(100f, 85f), 2.5f), SdSegment(q, C128, new Vector2(28f, 85f), 2.5f));
                break;
            }
            case "fist":
                shape = q => Union(SdRoundBox(q, new Vector2(64f, 58f), new Vector2(30f, 28f), 10f), SdRoundBox(q, new Vector2(38f, 52f), new Vector2(10f, 18f), 7f), SdBox(q, new Vector2(64f, 26f), new Vector2(22f, 8f)));
                detail = q => Union(SdSegment(q, new Vector2(52f, 86f), new Vector2(52f, 66f), 2f), SdSegment(q, new Vector2(66f, 86f), new Vector2(66f, 66f), 2f), SdSegment(q, new Vector2(80f, 86f), new Vector2(80f, 66f), 2f));
                break;
            case "fang":
                shape = q => Union(SdRoundBox(q, new Vector2(64f, 94f), new Vector2(36f, 10f), 8f),
                                   SdPolygon(q, new[] { new Vector2(34f, 88f), new Vector2(56f, 88f), new Vector2(44f, 30f) }),
                                   SdPolygon(q, new[] { new Vector2(72f, 88f), new Vector2(94f, 88f), new Vector2(84f, 30f) }));
                break;
            case "moon":
            {
                var st = Star(new Vector2(92f, 88f), 4, 13f, 3.5f);
                shape = q => Mathf.Min(Mathf.Max(SdCircle(q, C128, 40f), -SdCircle(q, new Vector2(84f, 78f), 33f)), SdPolygon(q, st));
                break;
            }
            case "halo":
            {
                var st = Star(new Vector2(64f, 52f), 4, 30f, 7f);
                shape = q => Mathf.Min(Mathf.Abs(new Vector2(q.x - 64f, (q.y - 96f) * 2.6f).magnitude - 30f) - 5f, SdPolygon(q, st));
                break;
            }
            case "gear":
                shape = q =>
                {
                    float d = SdCircle(q, C128, 28f);
                    for (int i = 0; i < 8; i++) d = Mathf.Min(d, SdBox(Rotate(q, C128, i * 45f), new Vector2(64f, 98f), new Vector2(7f, 10f)));
                    return Mathf.Max(d, -SdCircle(q, C128, 11f));
                };
                break;
            case "wing":
                shape = q =>
                {
                    var r = Rotate(q, C128, -35f);
                    return Mathf.Max(SdCircle(r, new Vector2(40f, 64f), 40f), SdCircle(r, new Vector2(88f, 64f), 40f));
                };
                detail = q => SdSegment(Rotate(q, C128, -35f), new Vector2(64f, 30f), new Vector2(64f, 98f), 2.2f);
                break;
            case "scales":
                shape = q => Union(SdBox(q, new Vector2(64f, 60f), new Vector2(4f, 36f)), SdBox(q, new Vector2(64f, 96f), new Vector2(40f, 4f)), SdBox(q, new Vector2(64f, 24f), new Vector2(20f, 4f)),
                                   Mathf.Max(SdCircle(q, new Vector2(32f, 66f), 16f), q.y - 66f), Mathf.Max(SdCircle(q, new Vector2(96f, 66f), 16f), q.y - 66f),
                                   SdSegment(q, new Vector2(26f, 96f), new Vector2(18f, 66f), 2f), SdSegment(q, new Vector2(38f, 96f), new Vector2(46f, 66f), 2f),
                                   SdSegment(q, new Vector2(90f, 96f), new Vector2(82f, 66f), 2f), SdSegment(q, new Vector2(102f, 96f), new Vector2(110f, 66f), 2f));
                break;
            case "elements":
                shape = q => SdPolygon(q, Star(C128, 5, 22f, 10f));
                break;
            case "arrowsup":
                shape = q => Mathf.Min(SdPolygon(q, Offset(Chevron, new Vector2(0f, 22f))), SdPolygon(q, Offset(Chevron, new Vector2(0f, -6f))));
                break;
            case "skull":
                shape = q => Union(SdCircle(q, new Vector2(64f, 72f), 32f), SdRoundBox(q, new Vector2(64f, 40f), new Vector2(20f, 14f), 5f));
                detail = q => Union(SdCircle(q, new Vector2(51f, 72f), 9f), SdCircle(q, new Vector2(77f, 72f), 9f), SdPolygon(q, new[] { new Vector2(58f, 54f), new Vector2(70f, 54f), new Vector2(64f, 63f) }),
                                    SdBox(q, new Vector2(56f, 34f), new Vector2(1.5f, 7f)), SdBox(q, new Vector2(64f, 34f), new Vector2(1.5f, 7f)), SdBox(q, new Vector2(72f, 34f), new Vector2(1.5f, 7f)));
                break;
            case "hourglass":
                shape = q => Union(SdPolygon(q, new[] { new Vector2(40f, 96f), new Vector2(88f, 96f), new Vector2(64f, 64f) }), SdPolygon(q, new[] { new Vector2(40f, 32f), new Vector2(88f, 32f), new Vector2(64f, 64f) }),
                                   SdBox(q, new Vector2(64f, 102f), new Vector2(30f, 5f)), SdBox(q, new Vector2(64f, 26f), new Vector2(30f, 5f)));
                detail = q => SdPolygon(q, new[] { new Vector2(50f, 36f), new Vector2(78f, 36f), new Vector2(64f, 52f) });
                break;
            case "potion":
                shape = q => Union(SdCircle(q, new Vector2(64f, 50f), 30f), SdBox(q, new Vector2(64f, 86f), new Vector2(10f, 14f)), SdBox(q, new Vector2(64f, 102f), new Vector2(15f, 5f)));
                detail = q => Mathf.Max(SdCircle(q, new Vector2(64f, 50f), 23f), q.y - 52f);
                break;
            case "chest":
                shape = q => Union(SdBox(q, new Vector2(64f, 48f), new Vector2(38f, 22f)), SdRoundBox(q, new Vector2(64f, 76f), new Vector2(38f, 14f), 10f));
                detail = q => Union(SdBox(q, new Vector2(64f, 68f), new Vector2(38f, 2.5f)), SdRoundBox(q, new Vector2(64f, 62f), new Vector2(7f, 9f), 2f));
                break;
            case "tag":
                shape = q => SdPolygon(Rotate(q, C128, 45f), new[] { new Vector2(40f, 34f), new Vector2(88f, 34f), new Vector2(88f, 84f), new Vector2(64f, 104f), new Vector2(40f, 84f) });
                detail = q => SdCircle(q, new Vector2(78f, 78f), 7f);
                break;
            case "hammer":
                shape = q => Mathf.Min(SdBox(Rotate(q, C128, 40f), new Vector2(64f, 92f), new Vector2(30f, 13f)), SdSegment(q, new Vector2(64f, 64f), new Vector2(34f, 24f), 6f));
                break;
            case "clover":
                shape = q => Union(SdCircle(q, new Vector2(64f, 86f), 18f), SdCircle(q, new Vector2(42f, 64f), 18f), SdCircle(q, new Vector2(86f, 64f), 18f), SdCircle(q, new Vector2(64f, 46f), 18f),
                                   SdSegment(q, new Vector2(64f, 46f), new Vector2(80f, 16f), 4f));
                break;
            case "gem":
            {
                var gem = new[] { new Vector2(28f, 78f), new Vector2(44f, 98f), new Vector2(84f, 98f), new Vector2(100f, 78f), new Vector2(64f, 24f) };
                shape = q => SdPolygon(q, gem);
                detail = q => Union(SdSegment(q, new Vector2(28f, 78f), new Vector2(100f, 78f), 2f), SdSegment(q, new Vector2(54f, 78f), new Vector2(64f, 24f), 2f), SdSegment(q, new Vector2(74f, 78f), new Vector2(64f, 24f), 2f));
                break;
            }
            default: { var st = Star(C128, 4, 40f, 12f); shape = q => SdPolygon(q, st); break; }
        }
    }

    // ================================================================== talents

    /// <summary>glyph, badge colour, corner badge (0 none, 1 ATK sword, 2 DEF shield)</summary>
    static void TalentSpec(string slug, out string glyph, out string color, out int corner)
    {
        corner = slug.EndsWith("atk") ? 1 : slug.EndsWith("def") ? 2 : 0;
        string root = corner == 0 ? slug : slug.Substring(0, slug.Length - 3);
        switch (root)
        {
            case "amplifier": glyph = "arrowsup"; color = "2ECC71"; break;
            case "balancingstrike": glyph = "scales"; color = "8FA3B8"; break;
            case "berserkstrike": glyph = "swordburst"; color = "E8463C"; break;
            case "blaze": glyph = "flame"; color = "FF6A3D"; break;
            case "breaker": glyph = corner == 2 ? "brokenshield" : "brokensword"; color = "7F8C8D"; break;
            case "celestialblessing": glyph = "sparkle"; color = "8E7CFF"; break;
            case "dexteritydrive": glyph = "gear"; color = "26C6DA"; break;
            case "doubleedgedstrike": glyph = "swords"; color = "C0392B"; break;
            case "elementalstrike": glyph = "elements"; color = "AB47BC"; break;
            case "endurance": glyph = "shield"; color = "5D8AA8"; break;
            case "evasion": glyph = "wing"; color = "4FC3F7"; break;
            case "luckycoin": glyph = "coin"; color = "E2A400"; break;
            case "unluckycoin": glyph = "badcoin"; color = "6D5D3A"; break;
            case "manareaver": glyph = "drop"; color = "3A7BFF"; break;
            case "paralysis": glyph = "bolt"; color = "D4AC0D"; break;
            case "poison": glyph = "poison"; color = "4CAF50"; break;
            case "precision": glyph = "crosshair"; color = "FF8F00"; break;
            case "regeneration": glyph = "heartplus"; color = "EC407A"; break;
            case "restrictedinstinct": glyph = "lock"; color = "607D8B"; break;
            case "trickroom": glyph = "cube"; color = "9B59B6"; break;
            case "vengeance": glyph = "fist"; color = "D35400"; break;
            case "berserker": glyph = "skull"; color = "FF5722"; break;
            case "bloodsurge": glyph = "drop"; color = "C62828"; break;
            case "bloodthirster": glyph = "fang"; color = "8E0E0E"; break;
            case "celestialinfluence": glyph = "moon"; color = "5C6BC0"; break;
            case "divineblessing": glyph = "halo"; color = "E6B800"; break;
            case "dominance": glyph = "crown"; color = "F39C12"; break;
            case "grievouslimiter": glyph = "brokenheart"; color = "6D4C41"; break;
            case "lethalclarity": glyph = "eye"; color = "16A085"; break;
            case "lifesap": glyph = "heartdrop"; color = "D81B60"; break;
            case "overload": glyph = "bolt"; color = "F1C40F"; break;
            case "protector": glyph = "shieldplus"; color = "3498DB"; break;
            case "reversion": glyph = "cycle"; color = "00ACC1"; break;
            case "purify": glyph = "sparkle"; color = "26A69A"; break;
            case "piercingedge": glyph = "sword"; color = "78909C"; break;
            case "rot": glyph = "skull"; color = "6B8E23"; break;
            case "dispel": glyph = "key"; color = "7E57C2"; break;
            case "burstbarrier": glyph = "shield"; color = "29B6F6"; break;
            case "counterstance": glyph = "swords"; color = "FFB300"; break;
            case "guts": glyph = "fist"; color = "E53935"; break;
            case "thorns": glyph = "fang"; color = "558B2F"; break;
            case "momentum": glyph = "arrowsup"; color = "FF7043"; break;
            case "frostbite": glyph = "gem"; color = "4FC3F7"; break;
            case "haste": glyph = "wing"; color = "00BCD4"; break;
            case "mimic": glyph = "eye"; color = "AB47BC"; break;
            case "silence": glyph = "lock"; color = "9575CD"; break;
            case "infernus": glyph = "flame"; color = "B71C1C"; break;
            case "cursed": glyph = "skull"; color = "4A148C"; break;
            case "doppelganger": glyph = "cube"; color = "5C6BC0"; break;
            case "blackhole": glyph = "moon"; color = "263238"; break;
            case "chaotic": glyph = "elements"; color = "FF4081"; break;
            case "hypnotize": glyph = "eye"; color = "7B1FA2"; break;
            case "gigantify": glyph = "arrowsup"; color = "8D6E63"; break;
            case "leech": glyph = "heartdrop"; color = "880E4F"; break;
            case "greedy": glyph = "coin"; color = "FFC400"; break;
            case "gamble": glyph = "clover"; color = "43A047"; break;
            case "nothingness": glyph = "halo"; color = "9E9E9E"; break;
            case "deathsdance": glyph = "hourglass"; color = "37474F"; break;
            case "slayer": glyph = "swordburst"; color = "D50000"; break;
            case "flurry": glyph = "swords"; color = "F4511E"; break;
            case "assassinate": glyph = "crosshair"; color = "37474F"; break;
            case "finalgambit": glyph = "brokenheart"; color = "BF360C"; break;
            case "chargestrike": glyph = "hammer"; color = "FF6F00"; break;
            case "chargeattack": glyph = "bolt"; color = "FFA000"; break;
            case "blind": glyph = "eye"; color = "546E7A"; break;
            case "sleep": glyph = "moon"; color = "3949AB"; break;
            case "hex": glyph = "potion"; color = "6A1B9A"; break;
            case "truesight": glyph = "eye"; color = "00BFA5"; break;
            case "soulswap": glyph = "cycle"; color = "C2185B"; break;
            default: glyph = "star"; color = "7C5CFF"; break;
        }
    }

    static Texture2D TalentIcon(string slug, bool active)
    {
        TalentSpec(slug, out string glyph, out string hex, out int corner);
        var col = Hex(hex);
        var p = new Paint(128, 128);
        var light = Color.Lerp(col, Color.white, 0.25f);
        var darkCol = Color.Lerp(col, Color.black, 0.55f);
        Func<Vector2, float> badge = active
            ? (Func<Vector2, float>)(q => SdCircle(q, C128, 56f))
            : (q => SdPolygon(q, Star(C128, 3, 58f, 58f, 30f)) - 3f); // passive = hexagon
        p.Draw(q => badge(q) - 4f, new Color(0f, 0f, 0f, 0.65f));
        p.Draw(badge, q => Color.Lerp(darkCol, light, q.y / 128f));
        p.Draw(q => Band(badge(q), 2.5f, 5f), A(Color.white, active ? 0.45f : 0.6f));

        Glyph(glyph, out var shape, out var detail);
        var s = At(shape, C128, 0.8f);
        p.Draw(q => s(q + new Vector2(0f, 2.5f)) - 1.5f, new Color(0f, 0f, 0f, 0.35f));
        p.Draw(s, q => Color.Lerp(Hex("FFFFFF"), Hex("E6EAF4"), 1f - q.y / 128f));
        if (detail != null) p.Draw(At(detail, C128, 0.8f), darkCol);
        if (glyph == "elements")
        {
            string[] dots = { "FF6131", "66D959", "B88F61", "FFDB33", "409EFF" };
            for (int i = 0; i < 5; i++)
            {
                float a = (90f + i * 72f) * Mathf.Deg2Rad;
                var c = C128 + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 32f;
                var cc = Hex(dots[i]);
                p.Outlined(q => SdCircle(q, c, 9f), _ => cc, new Color(0f, 0f, 0f, 0.6f), 1.5f, c.x - 12, c.y - 12, c.x + 12, c.y + 12);
            }
        }
        if (corner != 0)
        {
            var bc = new Vector2(100f, 28f);
            var cornerCol = corner == 1 ? Hex("E53935") : Hex("1E88E5");
            p.Draw(q => SdCircle(q, bc, 22f), new Color(0f, 0f, 0f, 0.7f));
            p.Draw(q => SdCircle(q, bc, 19f), q => Color.Lerp(Color.Lerp(cornerCol, Color.black, 0.4f), cornerCol, (q.y - 9f) / 38f));
            Func<Vector2, float> small = corner == 1 ? (Func<Vector2, float>)(q => Rot(Sword, 40f)(q)) : Shield;
            p.Draw(At(small, bc, 0.28f), Color.white, bc.x - 20, bc.y - 20, bc.x + 20, bc.y + 20);
        }
        return p.ToTexture();
    }

    // ================================================================== buffs

    static Texture2D BuffIcon(BuffDefinition b)
    {
        string glyph;
        switch (b.id)
        {
            case Buffs.FusionProtection: glyph = "shield"; break;
            case Buffs.FusionBlessing: glyph = "halo"; break;
            case Buffs.FusionFortune: glyph = "clover"; break;
            case Buffs.LuckyFusion: glyph = "cycle"; break;
            case Buffs.HuntersInstinct: glyph = "crosshair"; break;
            case Buffs.RareEncounter: glyph = "arrowsup"; break;
            case Buffs.TreasureHunter: glyph = "chest"; break;
            case Buffs.Berserker: glyph = "key"; break;
            case Buffs.GoldCashback: glyph = "coincycle"; break;
            case Buffs.GoldenTouch: glyph = "coin"; break;
            case Buffs.MerchantsFavor: glyph = "tag"; break;
            case Buffs.EfficientUpgrader: glyph = "gear"; break;
            case Buffs.SalvageExpert: glyph = "hammer"; break;
            case Buffs.TheFlash: glyph = "bolt"; break;
            case Buffs.HeadHunter: glyph = "skull"; break;
            case Buffs.StrongestHero: glyph = "fist"; break;
            case Buffs.SecondWind: glyph = "heartplus"; break;
            case Buffs.StaminaMaster: glyph = "hourglass"; break;
            case Buffs.Elixir: glyph = "potion"; break;
            case Buffs.PotionOfStamina: glyph = "potion"; break;
            case Buffs.ReturningHeroes: glyph = "cycle"; break;
            case Buffs.NewGeneration: glyph = "crown"; break;
            default: glyph = "star"; break;
        }
        bool stamina = b.id == Buffs.StaminaMaster || b.id == Buffs.Elixir || b.id == Buffs.PotionOfStamina || b.id == Buffs.ReturningHeroes;
        string hex = b.id == Buffs.NewGeneration ? "F5C542"
                   : stamina ? "34B36B"
                   : b.group == BuffGroup.Fusion ? "8E5CF7"
                   : b.group == BuffGroup.Collection ? "1FA9A0"
                   : b.group == BuffGroup.Economy ? "E0A21E" : "E2483C";
        var col = Hex(hex);
        var light = Color.Lerp(col, Color.white, 0.3f);
        var darkCol = Color.Lerp(col, Color.black, 0.55f);
        var p = new Paint(128, 128);
        Func<Vector2, float> badge = q => SdRoundBox(q, C128, new Vector2(56f, 56f), 20f);
        p.Draw(q => badge(q) - 4f, new Color(0f, 0f, 0f, 0.65f));
        p.Draw(badge, q => Color.Lerp(darkCol, light, q.y / 128f));
        p.Draw(q => Band(badge(q), 3f, 5.5f), A(Color.white, 0.5f));
        if (b.id == Buffs.NewGeneration) p.Glow(C128, 54f, A(Color.white, 0.5f));
        Glyph(glyph, out var shape, out var detail);
        var s = At(shape, C128, 0.78f);
        p.Draw(q => s(q + new Vector2(0f, 2.5f)) - 1.5f, new Color(0f, 0f, 0f, 0.35f));
        p.Draw(s, q => Color.Lerp(Color.white, Hex("E6EAF4"), 1f - q.y / 128f));
        if (detail != null) p.Draw(At(detail, C128, 0.78f), darkCol);
        if (b.id == Buffs.PotionOfStamina) p.Draw(q => Plus(q, new Vector2(100f, 100f), 14f, 5f), Hex("FFFFFF"));
        return p.ToTexture();
    }

    // ================================================================== God Orb + arrow

    static Texture2D GodOrb()
    {
        var p = new Paint(128, 128);
        var light = new Vector3(-0.45f, 0.6f, 0.66f).normalized;
        p.Glow(C128, 63f, A(Hex("FFF1B0"), 0.6f));
        p.Draw(q => SdCircle(q, C128, 53f), Hex("6B4A10"));
        p.Draw(q => SdCircle(q, C128, 50f), q =>
        {
            var n2 = (q - C128) / 50f;
            float z = Mathf.Sqrt(Mathf.Max(0f, 1f - n2.sqrMagnitude));
            float lam = Mathf.Max(0f, Vector3.Dot(new Vector3(n2.x, n2.y, z), light));
            float hue = Mathf.Repeat(Mathf.Atan2(n2.y, n2.x) / (2f * Mathf.PI) + 0.5f, 1f);
            var rainbow = Color.HSVToRGB(hue, 0.35f, 1f);
            var c = Color.Lerp(Hex("FFE9A3"), rainbow, 0.45f) * (0.45f + 0.65f * lam) + Color.white * Mathf.Pow(lam, 20f) * 0.9f;
            c.a = 1f;
            return c;
        });
        var star = Star(C128, 8, 30f, 13f);
        p.Draw(q => SdPolygon(q, star) - 2f, A(Hex("7A5A10"), 0.6f));
        p.Draw(q => SdPolygon(q, star), Color.white);
        p.Draw(q => SdCircle(q, new Vector2(46f, 86f), 9f), A(Color.white, 0.6f));
        return p.ToTexture();
    }

    static Texture2D Arrow()
    {
        var p = new Paint(128, 48);
        var head = new[] { new Vector2(84f, 6f), new Vector2(124f, 24f), new Vector2(84f, 42f) };
        p.Draw(q => Mathf.Min(SdBox(q, new Vector2(46f, 24f), new Vector2(42f, 6f)), SdPolygon(q, head)), Color.white);
        return p.ToTexture();
    }
}
