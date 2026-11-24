using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public enum OrbType { Fusion, Element, Talent, God }

/// <summary>Everything saved to disk (JSON in Application.persistentDataPath).</summary>
[Serializable]
public class SaveData
{
    public int version = PlayerProfile.SaveVersion;
    /// <summary>CardDatabase.dataVersion this save was made with (campaign sheet hash).</summary>
    public string dataVersion;

    // The player's own character: the only character they can collect (user rule, 15 Nov 2026)
    public bool hasCharacter;
    public string characterName = "";
    /// <summary>"" = none yet, "file" = imported picture (persistentDataPath/character_portrait.png), "gallery:KEY" = a built-in portrait.</summary>
    public string characterSource = "";

    public int gold = GameRules.StartGold;
    public int diamonds;
    public int fusionOrbs = GameRules.StartFusionOrbs;
    public int elementOrbs;
    public int talentOrbs;
    public int godOrbs;

    public int stamina = GameRules.BaseMaxStamina;
    public long staminaTimestamp;

    public int nextUid = 1;
    public List<CardInstance> cards = new List<CardInstance>();
    public List<BuildSlot> slots = new List<BuildSlot>();
    public int activeSlot;
    public List<string> buffs = new List<string>();

    /// <summary>Cleared floor count per LOC (index 0 = LOC 1). Floors are cleared in order.</summary>
    public List<int> clearedFloors = new List<int>();

    public int battlesWon;
    public int battlesLost;
    public int packsOpened;
    public int fusionsSucceeded;
    public int fusionsFailed;
    public int goldSpent;
    public int cashbackEarned;

    public int battleSpeed = 1;
    public float sfxVolume = 0.7f;

    /// <summary>Card materials per rarity tier (Common .. God). Used as fusion fodder.</summary>
    public int[] materials = new int[9];
    /// <summary>Exchange new cards (packs and drops) up to this rarity for materials automatically.</summary>
    public bool autoExchange;
    public int autoExchangeMaxTier = 1;

    /// <summary>HMAC of everything above: a save edited outside the game is detected and rejected.</summary>
    public string integrity = "";
}

public class BattleRewards
{
    public bool victory;
    public bool firstClear;
    public bool locationCleared;
    public int gold;
    public int stamina;
    public int fusionOrbs;
    public int elementOrbs;
    public int talentOrbs;
    public int diamonds;
    public int godOrbs;
    public CardInstance droppedCard;
    public bool rareEncounter;
    /// <summary>Text lines (kept for logs); the result screen shows the numbers above as pictures.</summary>
    public readonly List<string> lines = new List<string>();
    /// <summary>Short notes shown under the reward pictures (LOC cleared, buffs that triggered...).</summary>
    public readonly List<string> notes = new List<string>();
}

public class FusionResult
{
    public bool success;
    public bool blessed;      // Fusion Blessing jumped one extra rarity
    public float rate;
    public CardRarity from;
    public CardRarity to;
    public int orbsSpent;
    public int fodderLost;
    public int fodderSaved;   // Fusion Protection kept 1 fodder on a fail
    public bool orbRefund;    // Lucky Fusion gave 1 orb back
}

/// <summary>Result of an auto merge run.</summary>
public class AutoMergeResult
{
    public int attempts, successes, fails, blessed;
    public int orbsSpent, orbsRefunded, fodderUsed, fodderSaved;
    public string stopReason = "";
    /// <summary>Main cards that ranked up: uid -> (starting rarity, final rarity).</summary>
    public readonly Dictionary<int, (CardRarity from, CardRarity to)> upgraded = new Dictionary<int, (CardRarity, CardRarity)>();
}

/// <summary>
/// Save data + every rule that changes it: the player's character, build slots, rerolls, fusion, gacha, shop,
/// permanent buffs, stamina, dismantling and floor rewards (design doc + user changes of 15 Nov 2026).
/// </summary>
public static class PlayerProfile
{
    public const int SaveVersion = 2;
    public const int StaminaCost = GameRules.StaminaCost;
    public const int StaminaRegenSeconds = GameRules.StaminaRegenSeconds;
    public const string PlayerCardKey = "player";

    static SaveData data;

    /// <summary>Raised after every Save() so UI can refresh.</summary>
    public static event Action Changed;

    public static string SavePath => Path.Combine(Application.persistentDataPath, "anigame_save.json");
    public static string PortraitPath => Path.Combine(Application.persistentDataPath, "character_portrait.png");
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    // ================================================================== save / load

    /// <summary>Set when the last Load had to reject or repair the save (shown once on the main menu).</summary>
    public static string LoadWarning;

    public static void Load()
    {
        data = null;
        if (File.Exists(SavePath))
        {
            var loaded = ReadVerified(SavePath, out bool tampered);
            if (tampered)
            {
                BackupSave("tampered");
                bool backupTampered = true;
                SaveData backup = File.Exists(BackupPath) ? ReadVerified(BackupPath, out backupTampered) : null;
                if (backup != null && !backupTampered)
                {
                    loaded = backup;
                    LoadWarning = "Your save file was changed outside the game, so the last valid save was restored.";
                }
                else
                {
                    loaded = null;
                    LoadWarning = "Your save file was changed outside the game and could not be trusted, so a new game was started (the file was kept as a backup).";
                }
                Debug.LogWarning("[AniGame] " + LoadWarning);
            }
            data = loaded;
        }

        string current = CurrentDataVersion;
        if (data != null && (data.version != SaveVersion || (!string.IsNullOrEmpty(current) && data.dataVersion != current)))
        {
            BackupSave(data.version != SaveVersion ? $"v{data.version}" : (data.dataVersion ?? "legacy"));
            Debug.Log("[AniGame] Save made with an older game version or campaign sheet; starting a new save (old one backed up).");
            data = null;
        }

        if (data == null) NewGame();
        else Validate();
        UpdateStamina();
        ResetCharacterCache();
    }

    public static void Save()
    {
        if (data == null) return;
        try
        {
            string json = Sign(data);
            File.WriteAllText(SavePath, json);
            File.WriteAllText(BackupPath, json);
            PlayerPrefs.SetString(SignedPrefKey, data.integrity);
            PlayerPrefs.Save();
        }
        catch (Exception ex) { Debug.LogError($"[AniGame] Failed to save: {ex.Message}"); }
        Changed?.Invoke();
    }

    // ================================================================== save integrity (anti-cheat)

    public static string BackupPath => SavePath + ".bak";
    const string SignedPrefKey = "AniGame.SignedSave";
    const string IntegrityKey = "AniGame|save-integrity|9d41c7e2b6a0|Juli-and-friends|v1";

    /// <summary>HMAC-SHA256 (hex) of a text with the game's secret key.</summary>
    public static string Hash(string text)
    {
        using (var h = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(IntegrityKey)))
        {
            var bytes = h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text ?? ""));
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    /// <summary>Fills save.integrity and returns the signed JSON.</summary>
    public static string Sign(SaveData save)
    {
        save.integrity = "";
        save.integrity = Hash(JsonUtility.ToJson(save, true));
        return JsonUtility.ToJson(save, true);
    }

    /// <summary>
    /// Checks a signed JSON text exactly as it was written: the hash was taken over the same text with the hash
    /// field empty. Unlike re-serializing the loaded object, this still works after new fields are added to the
    /// save format in an update (old saves keep verifying).
    /// </summary>
    public static bool IsValidText(string json, string field, string stored)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(stored)) return false;
        string signedPart = $"\"{field}\": \"{stored}\"";
        if (!json.Contains(signedPart)) return false;
        return Hash(json.Replace(signedPart, $"\"{field}\": \"\"")) == stored;
    }

    public static bool IsValid(SaveData save)
    {
        if (save == null || string.IsNullOrEmpty(save.integrity)) return false;
        string stored = save.integrity;
        save.integrity = "";
        bool ok = Hash(JsonUtility.ToJson(save, true)) == stored;
        save.integrity = stored;
        return ok;
    }

    /// <summary>Reads a save file. tampered = the hash does not match (or an unsigned file where a signed save existed).</summary>
    static SaveData ReadVerified(string path, out bool tampered)
    {
        tampered = false;
        SaveData save;
        string text = null;
        try { text = File.ReadAllText(path); save = JsonUtility.FromJson<SaveData>(text); }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AniGame] Could not read {Path.GetFileName(path)}: {ex.Message}");
            tampered = true;
            return null;
        }
        if (save == null) { tampered = true; return null; }
        if (string.IsNullOrEmpty(save.integrity))
        {
            // Saves made before protection existed are accepted once (then signed); after that an unsigned file means editing.
            tampered = PlayerPrefs.HasKey(SignedPrefKey);
            return tampered ? null : save;
        }
        tampered = !(IsValidText(text, "integrity", save.integrity) || IsValid(save));
        return tampered ? null : save;
    }

    /// <summary>Replaces the whole save (used by import) and signs it.</summary>
    public static void ApplyImportedSave(SaveData save)
    {
        data = save;
        Validate();
        UpdateStamina();
        ResetCharacterCache();
        Save();
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(BackupPath)) File.Delete(BackupPath);
        data = null;
        ResetCharacterCache();
    }

    static string CurrentDataVersion
    {
        get
        {
            var db = CardDatabase.Instance;
            return db != null ? db.dataVersion : null;
        }
    }

    /// <summary>Copies the save file to a timestamped backup (used before importing another save).</summary>
    public static void BackupCurrentSave(string tag) => BackupSave(tag);

    static void BackupSave(string tag)
    {
        try
        {
            if (!File.Exists(SavePath)) return;
            string safe = string.IsNullOrEmpty(tag) ? "old" : tag.Replace('/', '_').Replace('\\', '_');
            File.Copy(SavePath, Path.Combine(Application.persistentDataPath, $"anigame_save_backup_{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.json"), true);
        }
        catch (Exception ex) { Debug.LogWarning($"[AniGame] Could not back up the old save: {ex.Message}"); }
    }

    static void NewGame()
    {
        data = new SaveData { staminaTimestamp = Now, dataVersion = CurrentDataVersion };
        EnsureSlots();
        GiveStarterCard();
        Save();
    }

    /// <summary>Starter: one Common card of the player's character with a random build, placed in slot 1.</summary>
    static void GiveStarterCard()
    {
        var card = AddCard(CardRarity.Common, RandomBuild(CardRarity.Common));
        data.slots[0].cardUid = card.uid;
        data.slots[0].build = card.build.Clone();
        data.activeSlot = 0;
    }

    static void EnsureSlots()
    {
        if (data.slots == null) data.slots = new List<BuildSlot>();
        while (data.slots.Count < GameRules.SlotCount) data.slots.Add(new BuildSlot());
        data.slots[0].unlocked = true; // slot 1 is free
    }

    static void Validate()
    {
        if (data.cards == null) data.cards = new List<CardInstance>();
        if (data.clearedFloors == null) data.clearedFloors = new List<int>();
        if (data.buffs == null) data.buffs = new List<string>();
        if (data.characterName == null) data.characterName = "";
        if (data.characterSource == null) data.characterSource = "";
        EnsureSlots();
        foreach (var c in data.cards)
        {
            if (c.build == null) c.build = RandomBuild(c.rarity);
            if (c.uid >= data.nextUid) data.nextUid = c.uid + 1;
            c.key = PlayerCardKey; // every card is the player's character
        }
        foreach (var s in data.slots)
            if (s.HasCard && FindCard(s.cardUid) == null) s.cardUid = 0;
        if (data.cards.Count == 0) GiveStarterCard();
        if (!data.slots.Any(s => s.HasCard) && data.cards.Count > 0)
        {
            data.slots[0].cardUid = data.cards[0].uid;
            data.slots[0].build = data.cards[0].build.Clone();
        }
        data.activeSlot = ValidActiveSlot(data.activeSlot);
        if (data.stamina > MaxStamina) data.stamina = MaxStamina;
        if (data.materials == null || data.materials.Length != GameRules.MaxTier + 1)
        {
            var m = new int[GameRules.MaxTier + 1];
            if (data.materials != null) System.Array.Copy(data.materials, m, Mathf.Min(m.Length, data.materials.Length));
            data.materials = m;
        }
        data.autoExchangeMaxTier = Mathf.Clamp(data.autoExchangeMaxTier, 0, GameRules.MaxTier);
        // God cards carry two talents (one active, one passive)
        foreach (var c in data.cards)
            if (c.Tier >= GameRules.MaxTier) EnsureGodTalents(c.build);
        foreach (var s in data.slots)
        {
            var c = s.HasCard ? FindCard(s.cardUid) : null;
            if (c != null && c.Tier >= GameRules.MaxTier && s.build != null) EnsureGodTalents(s.build);
        }
    }

    // ================================================================== the player's character

    static Card characterCard;
    static Sprite characterSprite;
    static Texture2D characterTexture;

    public static bool HasCharacter => Data.hasCharacter && !string.IsNullOrEmpty(Data.characterName);

    /// <summary>Set by the character screen while editing so card previews show the unsaved name / picture.</summary>
    public static string PreviewName;
    public static Sprite PreviewSprite;
    public static string CharacterName => string.IsNullOrEmpty(Data.characterName) ? "Hero" : Data.characterName;

    /// <summary>The player's character as a runtime Card (name + portrait). Every player card uses it as its art.</summary>
    public static Card CharacterCard
    {
        get
        {
            if (characterCard == null)
            {
                characterCard = ScriptableObject.CreateInstance<Card>();
                characterCard.hideFlags = HideFlags.HideAndDontSave;
                characterCard.name = PlayerCardKey;
            }
            characterCard.cardName = PreviewName ?? CharacterName;
            characterCard.cardImg = PreviewSprite != null ? PreviewSprite : CharacterSprite;
            characterCard.anime = "";
            return characterCard;
        }
    }

    public static Sprite CharacterSprite
    {
        get
        {
            if (characterSprite != null) return characterSprite;
            string src = Data.characterSource ?? "";
            if (src.StartsWith("gallery:"))
            {
                var db = CardDatabase.Instance;
                var card = db != null ? db.FindByKey(src.Substring(8)) : null;
                characterSprite = card != null ? card.cardImg : null;
            }
            else if (src == "file" && File.Exists(PortraitPath))
            {
                try
                {
                    characterTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (characterTexture.LoadImage(File.ReadAllBytes(PortraitPath)))
                    {
                        characterTexture.wrapMode = TextureWrapMode.Clamp;
                        characterSprite = Sprite.Create(characterTexture, new Rect(0, 0, characterTexture.width, characterTexture.height), new Vector2(0.5f, 0.5f), 100f);
                        characterSprite.name = "CharacterPortrait";
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[AniGame] Could not load the character picture: " + ex.Message); }
            }
            return characterSprite;
        }
    }

    public static void ResetCharacterCache()
    {
        characterSprite = null;
        if (characterTexture != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(characterTexture); else UnityEngine.Object.DestroyImmediate(characterTexture); }
        characterTexture = null;
    }

    /// <summary>Sets the character name and picture source ("file" or "gallery:KEY"). Returns an error or null.</summary>
    public static string SetCharacter(string name, string source)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return "Type a name for your character";
        if (name.Length > 20) return "Names can be at most 20 characters";
        if (string.IsNullOrEmpty(source)) return "Choose a picture for your character";
        Data.characterName = name;
        Data.characterSource = source;
        Data.hasCharacter = true;
        ResetCharacterCache();
        Save();
        return null;
    }

    // ================================================================== permanent buffs

    public static bool Has(string buffId) => Data.buffs != null && Data.buffs.Contains(buffId);
    public static bool GodUnlocked => Has(Buffs.NewGeneration);

    public static string CanBuyBuff(BuffDefinition b)
    {
        if (b == null) return "Unknown buff";
        if (Has(b.id)) return "Already owned";
        if (b.currency == BuffCurrency.Gold && Data.gold < b.price) return $"Needs {b.price:N0} gold";
        if (b.currency == BuffCurrency.Diamonds && Data.diamonds < b.price) return $"Needs {b.price:N0} diamonds";
        return null;
    }

    public static string BuyBuff(string id)
    {
        var b = Buffs.Find(id);
        string err = CanBuyBuff(b);
        if (err != null) return err;
        if (b.currency == BuffCurrency.Gold) SpendGold(b.price);
        else Data.diamonds -= b.price;
        Data.buffs.Add(b.id);
        if (b.id == Buffs.PotionOfStamina) Data.stamina = Mathf.Min(MaxStamina, Data.stamina + Buffs.PotionMaxStamina);
        Save();
        return null;
    }

    // ================================================================== gold (cashback / golden touch)

    /// <summary>Spends gold; Gold Cashback returns 15%. Returns the cashback. Caller checks the balance first.</summary>
    public static int SpendGold(int cost)
    {
        if (cost <= 0) return 0;
        Data.gold -= cost;
        Data.goldSpent += cost;
        int back = Has(Buffs.GoldCashback) ? Mathf.FloorToInt(cost * Buffs.GoldCashbackRate) : 0;
        Data.gold += back;
        Data.cashbackEarned += back;
        return back;
    }

    /// <summary>Adds earned gold; Golden Touch gives +10%. Returns what was added.</summary>
    public static int EarnGold(int amount)
    {
        if (amount <= 0) return 0;
        int total = Has(Buffs.GoldenTouch) ? Mathf.RoundToInt(amount * (1f + Buffs.GoldenTouchBonus)) : amount;
        Data.gold += total;
        return total;
    }

    // ================================================================== currencies & orbs

    public static int GetOrbs(OrbType type)
    {
        switch (type)
        {
            case OrbType.Fusion: return Data.fusionOrbs;
            case OrbType.Element: return Data.elementOrbs;
            case OrbType.Talent: return Data.talentOrbs;
            default: return Data.godOrbs;
        }
    }

    public static void AddOrbs(OrbType type, int amount)
    {
        switch (type)
        {
            case OrbType.Fusion: Data.fusionOrbs = Mathf.Max(0, Data.fusionOrbs + amount); break;
            case OrbType.Element: Data.elementOrbs = Mathf.Max(0, Data.elementOrbs + amount); break;
            case OrbType.Talent: Data.talentOrbs = Mathf.Max(0, Data.talentOrbs + amount); break;
            default: Data.godOrbs = Mathf.Max(0, Data.godOrbs + amount); break;
        }
    }

    public static string OrbName(OrbType type) =>
        type == OrbType.Fusion ? "Fusion Orb" : type == OrbType.Element ? "Element Orb" : type == OrbType.Talent ? "Talent Orb" : "God Orb";

    /// <summary>Base shop price (God Orbs are not sold).</summary>
    public static int OrbPrice(OrbType type) =>
        type == OrbType.Fusion ? GameRules.FusionOrbPrice : type == OrbType.Element ? GameRules.ElementOrbPrice : type == OrbType.Talent ? GameRules.TalentOrbPrice : 0;

    /// <summary>Shop price after Merchant's Favor.</summary>
    public static int OrbShopPrice(OrbType type)
    {
        int p = OrbPrice(type);
        return Has(Buffs.MerchantsFavor) ? Mathf.RoundToInt(p * (1f - Buffs.MerchantsFavorDiscount)) : p;
    }

    // ================================================================== cards

    /// <summary>Every card is the player's character: its art is always <see cref="CharacterCard"/>.</summary>
    public static Card Art(CardInstance card) => card == null ? null : CharacterCard;

    public static CardInstance FindCard(int uid) => uid <= 0 ? null : Data.cards.FirstOrDefault(c => c.uid == uid);

    public static int RandomRoll(int tier)
    {
        int dev = GameRules.MaxDeviation(tier);
        return UnityEngine.Random.Range(-dev, dev + 1);
    }

    /// <summary>A fresh build: each stat rolled within +-MaxDeviation, element by Element Orb odds, a random talent.</summary>
    public static BuildData RandomBuild(CardRarity rarity, string talent = null, CardElement? element = null, string talent2 = null)
    {
        int tier = GameRules.Tier(rarity);
        var db = CardDatabase.Instance;
        if (string.IsNullOrEmpty(talent))
        {
            var t = db != null ? db.RandomTalent() : null;
            talent = t != null ? t.DisplayName : "";
        }
        var build = new BuildData
        {
            hp = RandomRoll(tier),
            atk = RandomRoll(tier),
            def = RandomRoll(tier),
            spd = RandomRoll(tier),
            element = element ?? ElementSystem.RollRandom(),
            talent = talent,
            talent2 = talent2 ?? ""
        };
        if (tier >= GameRules.MaxTier) EnsureGodTalents(build);
        else build.talent2 = "";
        return build;
    }

    /// <summary>God builds: talent = an active talent, talent2 = a passive one (keeps whatever already fits).</summary>
    public static void EnsureGodTalents(BuildData b)
    {
        var db = CardDatabase.Instance;
        if (b == null || db == null) return;
        var t1 = db.FindTalent(b.talent);
        var t2 = db.FindTalent(b.talent2);
        if (t1 != null && !t1.IsActive && (t2 == null || t2.IsActive)) { var tmp = t1; t1 = t2; t2 = tmp; }
        if (t1 == null || !t1.IsActive) t1 = db.RandomActive();
        if (t2 == null || t2.IsActive) t2 = db.RandomPassive();
        b.talent = t1 != null ? t1.DisplayName : b.talent;
        b.talent2 = t2 != null ? t2.DisplayName : "";
    }

    public static CardInstance AddCard(CardRarity rarity, BuildData build)
    {
        var d = data ?? Data;
        var card = new CardInstance
        {
            uid = d.nextUid++,
            key = PlayerCardKey,
            rarity = rarity,
            build = build ?? RandomBuild(rarity)
        };
        d.cards.Add(card);
        return card;
    }

    /// <summary>Kept for older callers: the art is ignored, every card is the player's character.</summary>
    public static CardInstance AddCard(Card art, CardRarity rarity, BuildData build) => AddCard(rarity, build);

    /// <summary>Rare Encounter: 3% chance an obtained card is one rarity higher, up to Ultra Rare.</summary>
    static CardRarity ObtainedRarity(CardRarity rarity, out bool upgraded)
    {
        upgraded = false;
        if (Has(Buffs.RareEncounter) && rarity < Buffs.RareEncounterMax && UnityEngine.Random.value < Buffs.RareEncounterChance)
        {
            upgraded = true;
            return rarity + 1;
        }
        return rarity;
    }

    static void RemoveCard(CardInstance card)
    {
        if (card == null) return;
        Data.cards.Remove(card);
        foreach (var s in Data.slots) if (s.cardUid == card.uid) s.cardUid = 0;
    }

    public static bool IsInSlot(int uid) => Data.slots.Any(s => s.unlocked && s.cardUid == uid);

    /// <summary>Locked by the player, or automatically because a build slot uses it.</summary>
    public static bool IsProtected(CardInstance card) => card != null && (card.locked || IsInSlot(card.uid));

    public static void ToggleLock(CardInstance card)
    {
        if (card == null) return;
        card.locked = !card.locked;
        Save();
    }

    // ================================================================== dismantle (Salvage Expert)

    public static int DismantleValue(CardInstance card) => card == null ? 0 : GameRules.DismantleGold(card.Tier);

    public static string CanDismantle(CardInstance card)
    {
        if (!Has(Buffs.SalvageExpert)) return "Buy the Salvage Expert buff to dismantle cards";
        if (card == null) return "No card selected";
        if (IsProtected(card)) return "Locked or slotted cards can't be dismantled";
        return null;
    }

    public static string Dismantle(CardInstance card, out int gold)
    {
        gold = 0;
        string err = CanDismantle(card);
        if (err != null) return err;
        RemoveCard(card);
        gold = EarnGold(DismantleValue(card));
        Save();
        return null;
    }

    // ================================================================== God Orb

    public static string CanUseGodOrb(CardInstance card)
    {
        if (card == null) return "No card selected";
        if (card.rarity != CardRarity.God) return "God Orbs only work on God cards";
        if (card.godBonus >= GameRules.GodOrbMaxStacks) return $"This card already has the max +{GameRules.GodOrbMaxStacks}%";
        if (Data.godOrbs < 1) return "Needs 1 God Orb (God floor drops and diamond packs)";
        return null;
    }

    public static string UseGodOrb(CardInstance card, out bool success)
    {
        success = false;
        string err = CanUseGodOrb(card);
        if (err != null) return err;
        Data.godOrbs--;
        success = UnityEngine.Random.value < GameRules.GodOrbSuccess;
        if (success) card.godBonus++;
        Save();
        return null;
    }

    // ================================================================== build slots

    public static BuildSlot GetSlot(int index) => index >= 0 && index < Data.slots.Count ? Data.slots[index] : null;

    /// <summary>Diamond cost to unlock a slot, after Berserker.</summary>
    public static int SlotUnlockPrice(int index)
    {
        int cost = GameRules.SlotUnlockCost(index);
        return Has(Buffs.Berserker) ? Mathf.RoundToInt(cost * (1f - Buffs.BerserkerDiscount)) : cost;
    }

    public static string UnlockSlot(int index)
    {
        var slot = GetSlot(index);
        if (slot == null) return "No such slot";
        if (slot.unlocked) return null;
        int cost = SlotUnlockPrice(index);
        if (Data.diamonds < cost) return $"Needs {cost} diamonds";
        Data.diamonds -= cost;
        slot.unlocked = true;
        Save();
        return null;
    }

    public static string SaveToSlot(int index, CardInstance card, BuildData build)
    {
        var slot = GetSlot(index);
        if (slot == null || card == null || build == null) return "Nothing to save";
        if (!slot.unlocked) return $"Slot {index + 1} is locked";
        slot.cardUid = card.uid;
        slot.build = build.Clone();
        var active = GetSlot(Data.activeSlot);
        if (active == null || !active.unlocked || !active.HasCard) Data.activeSlot = index;
        Save();
        return null;
    }

    public static int ActiveSlot
    {
        get => ValidActiveSlot(Data.activeSlot);
        set
        {
            Data.activeSlot = ValidActiveSlot(value);
            Save();
        }
    }

    static int ValidActiveSlot(int index)
    {
        var slots = (data ?? Data).slots;
        if (index >= 0 && index < slots.Count && slots[index].unlocked && slots[index].HasCard) return index;
        for (int i = 0; i < slots.Count; i++) if (slots[i].unlocked && slots[i].HasCard) return i;
        return 0;
    }

    /// <summary>The fighter built from a slot (null if the slot is empty), with the combat buffs applied.</summary>
    public static CardRuntime SlotRuntime(int index)
    {
        var slot = GetSlot(index);
        if (slot == null || !slot.unlocked || !slot.HasCard) return null;
        var card = FindCard(slot.cardUid);
        if (card == null) return null;
        var rt = CardRuntime.ForBuild(card, slot.build);
        ApplyCombatBuffs(rt);
        return rt;
    }

    public static void ApplyCombatBuffs(CardRuntime rt)
    {
        if (rt == null) return;
        if (Has(Buffs.TheFlash)) rt.evasion += Buffs.FlashEvasion;
        if (Has(Buffs.HeadHunter)) rt.critRate += Buffs.HeadHunterCrit;
        rt.secondWindChance = Has(Buffs.SecondWind) ? Buffs.SecondWindChance : 0f;
        rt.strongestHeroChance = Has(Buffs.StrongestHero) ? Buffs.StrongestHeroChance : 0f;
    }

    // ================================================================== rerolls (orb first, then gold; preview, then save to a slot)

    public static int StatRerollPrice(int tier)
    {
        int cost = GameRules.StatRerollCost(tier);
        return Has(Buffs.EfficientUpgrader) ? Mathf.RoundToInt(cost * (1f - Buffs.EfficientUpgraderDiscount)) : cost;
    }

    /// <summary>Gold paid for an element/talent reroll when you have no orb (the orb's price, -10% with Efficient Upgrader).</summary>
    public static int OrbRerollGoldPrice(OrbType type)
    {
        int cost = OrbPrice(type);
        return Has(Buffs.EfficientUpgrader) ? Mathf.RoundToInt(cost * (1f - Buffs.EfficientUpgraderDiscount)) : cost;
    }

    /// <summary>Spends gold and returns a copy of <paramref name="from"/> with all 4 stats re-rolled. Null reason = ok.</summary>
    public static string RerollStats(CardInstance card, BuildData from, out BuildData result)
    {
        result = null;
        if (card == null || from == null) return "No card selected";
        int cost = StatRerollPrice(card.Tier);
        if (Data.gold < cost) return $"Needs {cost:N0} gold";
        SpendGold(cost);
        result = from.Clone();
        result.hp = RandomRoll(card.Tier);
        result.atk = RandomRoll(card.Tier);
        result.def = RandomRoll(card.Tier);
        result.spd = RandomRoll(card.Tier);
        Save();
        return null;
    }

    /// <summary>Uses 1 orb if you have one (no gold), otherwise pays the orb's gold price.</summary>
    static string PayOrbOrGold(OrbType type)
    {
        if (GetOrbs(type) > 0)
        {
            AddOrbs(type, -1);
            return null;
        }
        int cost = OrbRerollGoldPrice(type);
        if (Data.gold < cost) return $"Needs 1 {OrbName(type)} or {cost:N0} gold";
        SpendGold(cost);
        return null;
    }

    public static string RerollElement(CardInstance card, BuildData from, out BuildData result)
    {
        result = null;
        if (card == null || from == null) return "No card selected";
        string err = PayOrbOrGold(OrbType.Element);
        if (err != null) return err;
        result = from.Clone();
        result.element = ElementSystem.RollRandom();
        Save();
        return null;
    }

    public static string RerollTalent(CardInstance card, BuildData from, out BuildData result)
    {
        result = null;
        if (card == null || from == null) return "No card selected";
        if (CardDatabase.Instance == null || CardDatabase.Instance.RandomTalent() == null) return "No talents in the database";
        string err = PayOrbOrGold(OrbType.Talent);
        if (err != null) return err;
        result = from.Clone();
        if (card.Tier >= GameRules.MaxTier)
        {
            // God: one reroll gives a new active AND a new passive
            result.talent = CardDatabase.Instance.RandomActive()?.DisplayName ?? result.talent;
            result.talent2 = CardDatabase.Instance.RandomPassive()?.DisplayName ?? result.talent2;
        }
        else result.talent = CardDatabase.Instance.RandomTalent().DisplayName;
        Save();
        return null;
    }

    // ================================================================== fusion

    public static float FusionRate(int tier) => GameRules.FusionSuccessRate(tier, Has(Buffs.FusionFortune) ? Buffs.FusionFortuneBonus : 0f);

    /// <summary>Lucky Fusion: chance to get 1 orb back = 5% + 5% per orb used.</summary>
    public static float LuckyFusionChance(int orbsUsed) => Has(Buffs.LuckyFusion) ? Mathf.Min(1f, Buffs.LuckyFusionBase + Buffs.LuckyFusionPerOrb * orbsUsed) : 0f;

    /// <summary>Cards that can be fodder for <paramref name="main"/>: same rarity, not the main card, not locked / in a slot.</summary>
    public static List<CardInstance> FodderCandidates(CardInstance main)
    {
        if (main == null) return new List<CardInstance>();
        return Data.cards.Where(c => c != main && c.rarity == main.rarity && !IsProtected(c))
                         .OrderBy(c => c.build.TotalRoll).ToList();
    }

    public static string CanFuse(CardInstance main, IList<CardInstance> fodder, int materials = 0)
    {
        if (main == null) return "Pick a main card";
        if (!GameRules.CanFuseFrom(main.Tier)) return "God is the maximum rarity";
        if (main.rarity == CardRarity.Mythic && !GodUnlocked) return "GOD rarity is locked: buy the New Generation buff";
        fodder = fodder ?? new List<CardInstance>();
        if (materials < 0 || Materials(main.Tier) < materials) return $"Not enough {MaterialName(main.Tier)}s";
        if (fodder.Count + materials != GameRules.FusionFodderCount) return $"Needs {GameRules.FusionFodderCount} fodder (cards or {MaterialName(main.Tier)}s)";
        if (fodder.Distinct().Count() != fodder.Count) return "Pick two different fodder cards";
        foreach (var f in fodder)
        {
            if (f == null || f == main) return "Invalid fodder";
            if (f.rarity != main.rarity) return "Fodder must be the same rarity";
            if (IsProtected(f)) return "Locked or slotted cards can't be fodder";
        }
        int orbs = GameRules.FusionOrbCost(main.Tier);
        if (Data.fusionOrbs < orbs) return $"Needs {orbs} Fusion Orbs";
        return null;
    }

    /// <summary>Success: main card +1 rarity (Fusion Blessing: 3% for +2, never into Mythic/God); Roll, element, talent kept.
    /// Failure: both fodder and the orbs are lost (Fusion Protection: only 1 fodder). Lucky Fusion may refund 1 orb.</summary>
    public static FusionResult Fuse(CardInstance main, IList<CardInstance> fodder, int materials = 0)
    {
        fodder = fodder ?? new List<CardInstance>();
        if (CanFuse(main, fodder, materials) != null) return null;
        int tier = main.Tier;
        var result = new FusionResult
        {
            from = main.rarity,
            rate = FusionRate(main.Tier),
            orbsSpent = GameRules.FusionOrbCost(main.Tier)
        };
        Data.fusionOrbs -= result.orbsSpent;
        result.success = UnityEngine.Random.value < result.rate;
        var ordered = fodder.OrderBy(f => f.build.TotalRoll).ToList(); // the weakest goes first
        if (result.success)
        {
            foreach (var f in ordered) RemoveCard(f);
            Data.materials[tier] -= materials;
            result.fodderLost = ordered.Count + materials;
            int target = main.Tier + 1;
            if (Has(Buffs.FusionBlessing) && target + 1 <= (int)CardRarity.Legendary && UnityEngine.Random.value < Buffs.FusionBlessingChance)
            {
                target++;
                result.blessed = true;
            }
            main.rarity = (CardRarity)Mathf.Min(GameRules.MaxTier, target);
            if (main.Tier >= GameRules.MaxTier)
            {
                EnsureGodTalents(main.build);
                foreach (var s in Data.slots)
                    if (s.cardUid == main.uid && s.build != null) EnsureGodTalents(s.build);
            }
            Data.fusionsSucceeded++;
        }
        else
        {
            int total = ordered.Count + materials;
            int lose = Has(Buffs.FusionProtection) ? 1 : total;
            // Materials are lost first, then the weakest cards
            int matLost = Mathf.Min(materials, lose);
            Data.materials[tier] -= matLost;
            for (int i = 0; i < lose - matLost && i < ordered.Count; i++) RemoveCard(ordered[i]);
            result.fodderLost = lose;
            result.fodderSaved = total - lose;
            Data.fusionsFailed++;
        }
        if (UnityEngine.Random.value < LuckyFusionChance(result.orbsSpent))
        {
            Data.fusionOrbs++;
            result.orbRefund = true;
        }
        result.to = main.rarity;
        Save();
        return result;
    }

    // ================================================================== auto merge

    /// <summary>Fusion main for a tier: slotted/locked cards first (they rank up), then the best Roll.</summary>
    static CardInstance PickMain(int tier) =>
        Data.cards.Where(c => c.Tier == tier).OrderByDescending(c => IsInSlot(c.uid)).ThenByDescending(c => c.locked)
            .ThenByDescending(c => c.build.TotalRoll).FirstOrDefault();

    /// <summary>Upper bound if every fusion succeeds: (fusions, orbs, cards used as fodder) for the target and orb budget.</summary>
    public static (int fusions, int orbs, int cards) AutoMergeEstimate(CardRarity target, int maxOrbs)
    {
        int top = Mathf.Min((int)target, GodUnlocked ? GameRules.MaxTier : GameRules.MaxTier - 1);
        var free = new int[GameRules.MaxTier + 1];
        var guarded = new int[GameRules.MaxTier + 1];
        foreach (var c in Data.cards) { if (IsProtected(c)) guarded[c.Tier]++; else free[c.Tier]++; }
        var mats = (int[])Data.materials.Clone();
        int budget = Mathf.Min(Mathf.Max(0, maxOrbs), Data.fusionOrbs);
        int fusions = 0, orbs = 0, cards = 0;
        for (int t = 0; t < top; t++)
        {
            int cost = GameRules.FusionOrbCost(t);
            while (budget >= cost)
            {
                bool guardedMain = guarded[t] >= 1;
                int mainCost = guardedMain ? 0 : 1;
                if (free[t] - mainCost < 0) break;
                int fromMats = Mathf.Min(2, mats[t]);
                int fromCards = 2 - fromMats;
                if (free[t] - mainCost < fromCards) break;
                if (guardedMain) { guarded[t]--; guarded[t + 1]++; } else { free[t]--; free[t + 1]++; }
                free[t] -= fromCards;
                mats[t] -= fromMats;
                budget -= cost; orbs += cost; fusions++; cards += 2;
            }
        }
        return (fusions, orbs, cards);
    }

    /// <summary>
    /// Fuses automatically, lowest rarity first, until no card can reach the next rarity up to <paramref name="target"/>,
    /// the orb budget is used, or orbs run out. Locked and slotted cards are only ever the main card, never fodder.
    /// </summary>
    public static AutoMergeResult AutoMerge(CardRarity target, int maxOrbs)
    {
        var r = new AutoMergeResult();
        int top = Mathf.Min((int)target, GodUnlocked ? GameRules.MaxTier : GameRules.MaxTier - 1);
        bool progressed = true;
        while (progressed && r.attempts < 1000)
        {
            progressed = false;
            for (int t = 0; t < top; t++)
            {
                while (r.attempts < 1000)
                {
                    int cost = GameRules.FusionOrbCost(t);
                    if (r.orbsSpent - r.orbsRefunded + cost > maxOrbs) { r.stopReason = "orb budget reached"; break; }
                    if (Data.fusionOrbs < cost) { r.stopReason = "out of Fusion Orbs"; break; }
                    var main = PickMain(t);
                    if (main == null) break;
                    int mats = Mathf.Min(GameRules.FusionFodderCount, Materials(t)); // materials first, then cards
                    var fodder = FodderCandidates(main).Take(GameRules.FusionFodderCount - mats).ToList();
                    if (fodder.Count + mats < GameRules.FusionFodderCount) break;
                    var startRarity = main.rarity;
                    var res = Fuse(main, fodder, mats);
                    if (res == null) break;
                    r.attempts++;
                    r.orbsSpent += res.orbsSpent;
                    r.fodderUsed += res.fodderLost;
                    r.fodderSaved += res.fodderSaved;
                    if (res.orbRefund) r.orbsRefunded++;
                    if (res.success)
                    {
                        r.successes++;
                        if (res.blessed) r.blessed++;
                        r.upgraded[main.uid] = r.upgraded.TryGetValue(main.uid, out var prev) ? (prev.from, main.rarity) : (startRarity, main.rarity);
                    }
                    else r.fails++;
                    progressed = true;
                }
            }
        }
        if (string.IsNullOrEmpty(r.stopReason)) r.stopReason = r.attempts == 0 ? "no cards to merge" : "nothing left to merge";
        Save();
        return r;
    }

    // ================================================================== gacha

    public static string CanOpenPack(PackDefinition pack)
    {
        if (pack == null) return "Unknown pack";
        if (pack.currency == PackCurrency.Gold && Data.gold < pack.price) return $"Needs {pack.price:N0} gold";
        if (pack.currency == PackCurrency.Diamonds && Data.diamonds < pack.price) return $"Needs {pack.price:N0} diamonds";
        return null;
    }

    public static List<PackItem> OpenPack(PackDefinition pack)
    {
        if (CanOpenPack(pack) != null) return null;
        if (pack.currency == PackCurrency.Gold) SpendGold(pack.price);
        else Data.diamonds -= pack.price;

        var items = new List<PackItem> { GiveCardItem(pack.guaranteed) };
        for (int i = 1; i < PackDefinition.ItemCount; i++)
        {
            var e = Packs.RollEntry(pack);
            if (e.type == RewardType.Card) items.Add(GiveCardItem(e.rarity));
            else
            {
                int amount = GiveCurrency(e.type, e.amount);
                items.Add(new PackItem { type = e.type, amount = amount });
            }
        }
        if (UnityEngine.Random.value < GameRules.GodOrbPackChance(pack.id))
        {
            Data.godOrbs++;
            items.Add(new PackItem { type = RewardType.GodOrb, amount = 1 });
        }
        Data.packsOpened++;
        Save();
        return items;
    }

    static PackItem GiveCardItem(CardRarity rarity)
    {
        rarity = ObtainedRarity(rarity, out _);
        var card = AddCard(rarity, RandomBuild(rarity));
        var item = new PackItem { type = RewardType.Card, amount = 1, rarity = rarity, card = card };
        if (ShouldAutoExchange(card)) { ExchangeCard(card, false); item.exchanged = true; }
        return item;
    }

    static int GiveCurrency(RewardType type, int amount)
    {
        switch (type)
        {
            case RewardType.Gold: return EarnGold(amount);
            case RewardType.Diamonds: Data.diamonds += amount; break;
            case RewardType.FusionOrb: Data.fusionOrbs += amount; break;
            case RewardType.ElementOrb: Data.elementOrbs += amount; break;
            case RewardType.TalentOrb: Data.talentOrbs += amount; break;
            case RewardType.GodOrb: Data.godOrbs += amount; break;
        }
        return amount;
    }

    // ================================================================== shop

    public static string BuyOrbs(OrbType type, int quantity)
    {
        if (type == OrbType.God) return "God Orbs can't be bought";
        quantity = Mathf.Max(1, quantity);
        int cost = OrbShopPrice(type) * quantity;
        if (Data.gold < cost) return $"Needs {cost:N0} gold";
        SpendGold(cost);
        AddOrbs(type, quantity);
        Save();
        return null;
    }

    // ================================================================== stamina (max 80, +1 per 5 min, 5 per fight)

    public static int MaxStamina => GameRules.BaseMaxStamina + (Has(Buffs.PotionOfStamina) ? Buffs.PotionMaxStamina : 0);
    public static int StaminaPerTick => 1 + (Has(Buffs.StaminaMaster) ? Buffs.StaminaMasterRegen : 0);

    public static void UpdateStamina()
    {
        var d = data;
        if (d == null) return;
        long now = Now;
        int max = MaxStamina;
        if (d.staminaTimestamp <= 0 || d.staminaTimestamp > now) d.staminaTimestamp = now;
        if (d.stamina >= max) { d.staminaTimestamp = now; return; }
        long ticks = (now - d.staminaTimestamp) / StaminaRegenSeconds;
        if (ticks > 0)
        {
            d.stamina = (int)Math.Min(max, d.stamina + ticks * StaminaPerTick);
            d.staminaTimestamp += ticks * StaminaRegenSeconds;
            if (d.stamina >= max) d.staminaTimestamp = now;
        }
    }

    public static int SecondsToNextStamina
    {
        get
        {
            UpdateStamina();
            if (Data.stamina >= MaxStamina) return 0;
            return (int)Math.Max(0, StaminaRegenSeconds - (Now - Data.staminaTimestamp));
        }
    }

    public static bool TrySpendStamina(int amount)
    {
        UpdateStamina();
        if (Data.stamina < amount) return false;
        if (Data.stamina >= MaxStamina) Data.staminaTimestamp = Now;
        Data.stamina -= amount;
        Save();
        return true;
    }

    /// <summary>Adds stamina up to the max. Returns what was actually added.</summary>
    public static int AddStamina(int amount)
    {
        UpdateStamina();
        int before = Data.stamina;
        Data.stamina = Mathf.Min(MaxStamina, Data.stamina + Mathf.Max(0, amount));
        return Data.stamina - before;
    }

    public static void RefillStamina()
    {
        Data.stamina = Mathf.Max(Data.stamina, MaxStamina);
        Data.staminaTimestamp = Now;
        Save();
    }

    // ================================================================== materials (card exchange)

    static readonly string[] materialShort = { "Common", "Uncommon", "Rare", "SR", "UR", "Epic", "Legendary", "Mythic", "God" };

    public static string MaterialName(int tier) => $"{materialShort[Mathf.Clamp(tier, 0, materialShort.Length - 1)]} Material";
    public static int Materials(int tier) => Data.materials != null && tier >= 0 && tier < Data.materials.Length ? Data.materials[tier] : 0;
    public static int TotalMaterials => Data.materials != null ? Data.materials.Sum() : 0;

    public static string CanExchange(CardInstance card)
    {
        if (card == null) return "No card selected";
        if (card.locked) return "Unlock the card first";
        if (IsInSlot(card.uid)) return "This card is in a build slot";
        if (Data.cards.Count <= 1) return "You need to keep at least one card";
        return null;
    }

    /// <summary>Turns a card into 1 material of its rarity (fusion fodder that takes no collection space).</summary>
    public static string ExchangeCard(CardInstance card, bool save = true)
    {
        string err = CanExchange(card);
        if (err != null) return err;
        RemoveCard(card);
        Data.materials[card.Tier]++;
        if (save) Save();
        return null;
    }

    /// <summary>Cards that "exchange all up to maxTier" would turn into materials.</summary>
    public static List<CardInstance> ExchangeableCards(int maxTier) =>
        Data.cards.Where(c => c.Tier <= maxTier && !IsProtected(c)).ToList();

    /// <summary>Exchanges every unlocked, unslotted card up to maxTier. Returns materials gained per tier.</summary>
    public static int[] ExchangeAll(int maxTier)
    {
        var gained = new int[GameRules.MaxTier + 1];
        foreach (var c in ExchangeableCards(maxTier))
        {
            if (Data.cards.Count <= 1) break;
            RemoveCard(c);
            Data.materials[c.Tier]++;
            gained[c.Tier]++;
        }
        Save();
        return gained;
    }

    static bool ShouldAutoExchange(CardInstance card) =>
        card != null && Data.autoExchange && card.Tier <= Data.autoExchangeMaxTier && Data.cards.Count > 1;

    public static void SetAutoExchange(bool on, int maxTier)
    {
        Data.autoExchange = on;
        Data.autoExchangeMaxTier = Mathf.Clamp(maxTier, 0, GameRules.MaxTier);
        Save();
    }

    // ================================================================== test mode (editor / development builds)

    public static bool TestModeAvailable => Application.isEditor || Debug.isDebugBuild;

    /// <summary>Clears every floor of every LOC before the last one, so all 100 LOCs are open.</summary>
    public static string TestUnlockAllLocations()
    {
        var db = CardDatabase.Instance;
        if (db == null) return "No card database";
        int count = db.LocationCount;
        for (int loc = 1; loc < count; loc++) SetClearedFloors(loc, db.FloorCount(loc));
        GameSession.SelectedLocation = count;
        Save();
        return $"LOC 1-{count - 1} cleared: every LOC up to {count} is open";
    }

    /// <summary>Adds a God card (active + passive talent, good Roll) and puts it in the main slot.</summary>
    public static CardInstance TestGiveGodCard()
    {
        var build = RandomBuild(CardRarity.God);
        int dev = GameRules.MaxDeviation(GameRules.MaxTier);
        build.hp = build.atk = build.def = build.spd = dev / 2;
        var card = AddCard(CardRarity.God, build);
        if (!Data.buffs.Contains(Buffs.NewGeneration)) Data.buffs.Add(Buffs.NewGeneration);
        var slot = GetSlot(ActiveSlot);
        if (slot != null && slot.unlocked) { slot.cardUid = card.uid; slot.build = card.build.Clone(); }
        Save();
        return card;
    }

    public static void TestAddResources()
    {
        Data.gold += 1000000;
        Data.diamonds += 50000;
        Data.fusionOrbs += 200;
        Data.elementOrbs += 50;
        Data.talentOrbs += 50;
        Data.godOrbs += 20;
        Data.stamina = MaxStamina;
        Save();
    }

    // ================================================================== campaign progress

    public static int ClearedFloors(int location)
    {
        int i = location - 1;
        return i >= 0 && i < Data.clearedFloors.Count ? Data.clearedFloors[i] : 0;
    }

    static void SetClearedFloors(int location, int count)
    {
        while (Data.clearedFloors.Count < location) Data.clearedFloors.Add(0);
        Data.clearedFloors[location - 1] = count;
    }

    public static bool IsLocationCleared(int location)
    {
        var db = CardDatabase.Instance;
        int floors = db != null ? db.FloorCount(location) : 0;
        return floors > 0 && ClearedFloors(location) >= floors;
    }

    public static bool IsLocationUnlocked(int location) => location <= 1 || IsLocationCleared(location - 1);

    public static int HighestUnlockedLocation
    {
        get
        {
            var db = CardDatabase.Instance;
            int count = db != null ? db.LocationCount : 1;
            int loc = 1;
            while (loc < count && IsLocationCleared(loc)) loc++;
            return loc;
        }
    }

    public static bool IsFloorUnlocked(int location, int floor) => IsLocationUnlocked(location) && floor <= ClearedFloors(location) + 1;
    public static bool IsFloorCleared(int location, int floor) => floor <= ClearedFloors(location);

    public static int NextFloor(int location)
    {
        var db = CardDatabase.Instance;
        int floors = db != null ? db.FloorCount(location) : 1;
        return Mathf.Clamp(ClearedFloors(location) + 1, 1, Mathf.Max(1, floors));
    }

    // ================================================================== rewards

    public static float CardDropChance(Card enemy) =>
        enemy == null ? 0f : Mathf.Clamp01(GameRules.CardDropChance(enemy.rarity) + (Has(Buffs.HuntersInstinct) ? Buffs.HuntersInstinctBonus : 0f));

    public static BattleRewards ApplyVictory(int location, int floor)
    {
        var r = new BattleRewards { victory = true };
        var db = CardDatabase.Instance;
        Card enemy = db != null ? db.GetFloorCard(location, floor) : null;

        r.firstClear = !IsFloorCleared(location, floor);
        if (r.firstClear)
        {
            SetClearedFloors(location, Mathf.Max(ClearedFloors(location), floor));
            r.locationCleared = IsLocationCleared(location);
        }

        r.gold = EarnGold(GameRules.FloorGold(location, floor));
        r.lines.Add($"+{r.gold:N0} Gold" + (Has(Buffs.GoldenTouch) ? " (Golden Touch)" : ""));
        if (Has(Buffs.GoldenTouch)) r.notes.Add("Golden Touch: +10% gold");

        // Card drop: always the player's own character, at the defeated enemy's rarity (with its talent and element)
        if (enemy != null && enemy.rarity != CardRarity.God && UnityEngine.Random.value < CardDropChance(enemy))
        {
            string talent = enemy.talent != null ? enemy.talent.DisplayName : null;
            var rarity = ObtainedRarity(enemy.rarity, out bool upgraded);
            r.droppedCard = AddCard(rarity, RandomBuild(rarity, talent, enemy.element, enemy.talent2 != null ? enemy.talent2.DisplayName : null));
            r.rareEncounter = upgraded;
            if (ShouldAutoExchange(r.droppedCard))
            {
                ExchangeCard(r.droppedCard, false);
                r.notes.Add($"Auto exchange: the card became 1 {MaterialName(r.droppedCard.Tier)}");
            }
            r.lines.Add($"CARD DROP: {CharacterName} ({CardStats.RarityName(rarity)})" + (upgraded ? " - Rare Encounter!" : ""));
            if (upgraded) r.notes.Add("Rare Encounter: the card came one rarity higher!");
        }
        if (UnityEngine.Random.value < GameRules.FusionOrbDropChance)
        {
            r.fusionOrbs = 1;
            Data.fusionOrbs += 1;
            r.lines.Add("+1 Fusion Orb");
        }
        if (Has(Buffs.TreasureHunter) && UnityEngine.Random.value < Buffs.TreasureHunterChance)
        {
            float roll = UnityEngine.Random.value;
            if (roll < 0.40f) { Data.fusionOrbs++; r.fusionOrbs++; r.lines.Add("Treasure: +1 Fusion Orb"); }
            else if (roll < 0.65f) { Data.elementOrbs++; r.elementOrbs++; r.lines.Add("Treasure: +1 Element Orb"); }
            else if (roll < 0.80f) { Data.talentOrbs++; r.talentOrbs++; r.lines.Add("Treasure: +1 Talent Orb"); }
            else { Data.diamonds += 10; r.diamonds += 10; r.lines.Add("Treasure: +10 Diamonds"); }
            r.notes.Add("Treasure Hunter found rare loot!");
        }
        if (enemy != null && enemy.rarity == CardRarity.God && UnityEngine.Random.value < GameRules.GodOrbEnemyDrop)
        {
            Data.godOrbs++;
            r.godOrbs++;
            r.lines.Add("+1 GOD ORB!");
        }

        // Stamina: +2 for a new floor, full refill when a new LOC opens, +2 Elixir
        if (r.firstClear) r.stamina += AddStamina(GameRules.StaminaNewFloor);
        if (Has(Buffs.Elixir)) { int e = AddStamina(Buffs.ElixirStamina); r.stamina += e; if (e > 0) r.notes.Add("Elixir: +2 stamina"); }
        if (r.locationCleared)
        {
            int before = Data.stamina;
            Data.stamina = Mathf.Max(Data.stamina, MaxStamina);
            Data.staminaTimestamp = Now;
            r.stamina += Data.stamina - before;
            r.lines.Add($"LOC {location} CLEARED! LOC {location + 1} unlocked - stamina refilled");
            r.notes.Add($"LOC {location} CLEARED! LOC {location + 1} unlocked and stamina refilled");
        }
        if (r.stamina > 0) r.lines.Add($"+{r.stamina} Stamina");

        Data.battlesWon++;
        Save();
        return r;
    }

    /// <summary>Returning Heroes: +1 stamina when defeated. Returns the stamina gained.</summary>
    public static int ApplyDefeat()
    {
        Data.battlesLost++;
        int gained = Has(Buffs.ReturningHeroes) ? AddStamina(Buffs.ReturningHeroesStamina) : 0;
        Save();
        return gained;
    }
}
