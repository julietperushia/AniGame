using System.Linq;
using UnityEngine;

/// <summary>Boss location (3v3: the 3 build slots against one boss): unlock, gates in order, boss stats and rewards.</summary>
public static partial class PlayerProfile
{
    public const int BossUnlockGodCards = 1;

    public static int GodCards => Data.cards.Count(c => c != null && c.rarity >= CardRarity.God);

    /// <summary>Open with 1 God card (or with the 3v3 test switch in Test Mode).</summary>
    public static bool BossUnlocked => GodCards >= BossUnlockGodCards || HiddenTestMode;

    public static int BossGateCount => BossConfig.Current.gates.Count;
    public static bool IsBossCleared(int gate) => Data.bossCleared >= gate;
    public static bool IsBossOpen(int gate) => HiddenTestMode || gate <= Data.bossCleared + 1;
    public static int NextBossGate => Mathf.Clamp(Data.bossCleared + 1, 1, Mathf.Max(1, BossGateCount));

    public static string BossTeamProblem()
    {
        if (!BossUnlocked) return $"Own {BossUnlockGodCards} God card to challenge the bosses (you have {GodCards}).";
        if (HiddenTestMode) return null;
        return TeamSlotsProblem();
    }

    /// <summary>A boss as a team member: stats are % of the best stat of its rarity, HP x the HP multiplier.</summary>
    static readonly System.Collections.Generic.Dictionary<string, Card> bossArt = new System.Collections.Generic.Dictionary<string, Card>();

    /// <summary>A runtime card showing a picture source (boss pictures, custom boss characters). Null if the picture is missing.</summary>
    public static Card PictureCard(string cacheKey, string picture, string name, string anime, CardRarity rarity, CardElement element)
    {
        if (string.IsNullOrEmpty(picture)) return null;
        var sprite = PictureStore.Load(picture);
        var gallery = sprite == null ? PictureStore.GalleryCard(picture) : null;
        if (sprite == null && (gallery == null || string.IsNullOrEmpty(gallery.imageUrl))) return null;
        if (!bossArt.TryGetValue(cacheKey, out var art) || art == null)
        {
            art = ScriptableObject.CreateInstance<Card>();
            art.hideFlags = HideFlags.HideAndDontSave;
            art.name = "Boss_" + cacheKey;
            bossArt[cacheKey] = art;
        }
        art.cardName = name;
        art.anime = anime ?? "";
        art.cardImg = sprite;
        art.imageUrl = gallery != null ? gallery.imageUrl : null;
        art.rarity = rarity;
        art.element = element;
        return art;
    }

    /// <summary>The boss's art: its own picture, else its character's (game card or editor-made character).</summary>
    public static Card BossArt(BossGate g)
    {
        if (g == null) return null;
        var db = CardDatabase.Instance;
        var card = db != null && !string.IsNullOrEmpty(g.cardKey) ? db.FindByKey(g.cardKey) : null;
        var custom = BossConfig.Current.FindCharacter(g.cardKey);
        string pic = !string.IsNullOrEmpty(g.picture) ? g.picture : custom != null ? custom.picture : null;
        string name = !string.IsNullOrWhiteSpace(g.name) ? g.name : custom != null ? custom.name : card != null ? card.DisplayName : "Boss";
        var art = PictureCard("gate|" + pic + "|" + name, pic, name, custom != null ? custom.anime : card != null ? card.anime : "", g.rarity, g.element);
        return art != null ? art : card;
    }

    public static TeamMember BossMember(BossGate g)
    {
        if (g == null) return null;
        var db = CardDatabase.Instance;
        var card = BossArt(g);
        int max = GameRules.MaxStat(GameRules.Tier(g.rarity));
        int S(int pct) => Mathf.Max(1, Mathf.RoundToInt(max * Mathf.Max(1, pct) / 100f));
        var m = new TeamMember
        {
            name = !string.IsNullOrWhiteSpace(g.name) ? g.name : card != null ? card.DisplayName : "Boss",
            rarity = g.rarity,
            element = g.element,
            hp = S(g.hpPercent) * GameRules.HpMultiplier,
            atk = S(g.atkPercent),
            def = S(g.defPercent),
            baseSpeed = S(g.spdPercent),
            critRate = Mathf.Clamp(g.critRate, 0, 100),
            critDamage = 150,
            evasion = Mathf.Max(0, g.evasion),
            art = card,
            actionsPerRound = Mathf.Clamp(g.actionsPerRound, 1, 5),
        };
        if (db != null)
        {
            var t1 = db.FindTalent(g.talent);
            var t2 = db.FindTalent(g.talent2);
            if (t1 != null) m.talents.Add(t1);
            if (t2 != null && t2 != t1) m.talents.Add(t2);
        }
        return m;
    }

    public static CardRuntime BossRuntime(int gate)
    {
        var m = BossMember(BossConfig.Current.Gate(gate));
        return m == null ? null : CardRuntime.ForTeam(m.name, new System.Collections.Generic.List<TeamMember> { m });
    }

    /// <summary>Boss win: first clear gives the gate's rewards and opens the next gate; a replay gives the replay % of them.</summary>
    public static BattleRewards ApplyBossVictory(int gate, bool testFight)
    {
        var r = new BattleRewards { victory = true };
        var cfg = BossConfig.Current;
        var g = cfg.Gate(gate);
        if (g == null) return r;
        if (testFight || HiddenTestMode) { r.notes.Add("Test fight: no rewards and no progress."); return r; }
        r.firstClear = !IsBossCleared(gate);
        if (r.firstClear) Data.bossCleared = Mathf.Max(Data.bossCleared, gate);
        float share = r.firstClear ? 1f : Mathf.Clamp01(cfg.replayRewardPercent / 100f);
        void Give(Prize p)
        {
            var given = GivePrize(p, share, false);
            if (given.amount > 0) r.lines.Add("+" + given.Label);
            if (p.type == PrizeType.GodOrbs) r.godOrbs += given.amount;
            if (p.type == PrizeType.Gold) r.gold += given.amount;
        }
        Give(new Prize(PrizeType.GodOrbs, g.godOrbs));
        Give(new Prize(PrizeType.Material, g.materials, g.materialTier));
        Give(new Prize(PrizeType.Gold, g.gold));
        if (r.lines.Count == 0) r.lines.Add("No reward this time");
        if (!r.firstClear) r.notes.Add($"Replay: {cfg.replayRewardPercent}% of the rewards (a leftover part is a chance for one more)");
        else if (gate < cfg.gates.Count) r.notes.Add($"GATE {gate + 1} is open!");
        else r.notes.Add("EVERY BOSS IS DEFEATED!");
        Data.battlesWon++;
        Data.bossWins++;
        Save();
        return r;
    }
}
