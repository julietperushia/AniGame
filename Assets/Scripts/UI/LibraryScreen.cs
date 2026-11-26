using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Searchable in-game guide: element counters (as pictures), every item, stats and rarity, fusion, rerolls,
/// every talent, buffs, stamina, gacha and battle rules. Content is generated from the live rules, so it never goes stale.
/// </summary>
public class LibraryScreen : MonoBehaviour
{
    public Button backButton;
    public Text titleText;
    public InputField searchInput;
    public RectTransform categoryList;
    public RectTransform content;
    public ScrollRect scroll;
    public Text countText;

    static readonly string[] Categories = { "All", "Elements", "Items", "Stats & Rarity", "Fusion", "Rerolls", "Talents", "Buffs", "Stamina & Rewards", "Gacha", "Battle", "Save Data" };

    class Entry
    {
        public string category;
        public string title;
        public Sprite icon;
        public string body;
        public Action<RectTransform> visual;
        public float visualHeight;
        public string search;
        public GameObject go;
        public int talentType = -1; // 0 active, 1 passive (talent entries only)
        public int talentCat = -1;  // (int)TalentCategory
    }

    // Talent filter (shown on the TALENTS page)
    int typeFilter = -1;      // -1 all, 0 active, 1 passive
    int categoryFilter = -1;  // -1 all, else (int)TalentCategory
    RectTransform talentFilterBar;
    readonly List<Button> typeButtons = new List<Button>();
    Dropdown categoryDropdown;

    readonly List<Entry> entries = new List<Entry>();
    readonly List<Button> categoryButtons = new List<Button>();
    string category = "All";

    void Start()
    {
        backButton.onClick.AddListener(() => GameSession.LoadScene(SceneNames.MainMenu));
        searchInput.onValueChanged.AddListener(_ => ApplyFilter());
        foreach (var c in Categories)
        {
            string cat = c;
            var b = UIFactory.CreateButton(categoryList, cat, cat.ToUpperInvariant(), UIFactory.PanelLight, 20);
            UIFactory.SetLayoutSize(b, -1, 58f);
            b.onClick.AddListener(() => { category = cat; ApplyFilter(); });
            categoryButtons.Add(b);
        }
        BuildEntries();
        BuildTalentFilter();
        foreach (var e in entries) e.go = BuildEntry(e);
        ApplyFilter();
    }

    /// <summary>Opens a category page (also used by the editor bridge for testing).</summary>
    public void ShowCategory(string name)
    {
        if (Array.IndexOf(Categories, name) < 0) return;
        category = name;
        ApplyFilter();
    }

    void ApplyFilter()
    {
        string q = (searchInput.text ?? "").Trim().ToLowerInvariant();
        int shown = 0;
        foreach (var e in entries)
        {
            bool show = (category == "All" || e.category == category) && (q.Length == 0 || e.search.Contains(q));
            if (show && category == "Talents" && e.talentType >= 0)
                show = (typeFilter < 0 || e.talentType == typeFilter) && (categoryFilter < 0 || e.talentCat == categoryFilter);
            e.go.SetActive(show);
            if (show) shown++;
        }
        for (int i = 0; i < categoryButtons.Count; i++)
            categoryButtons[i].image.color = Categories[i] == category ? UIFactory.Accent : UIFactory.PanelLight;
        if (talentFilterBar != null)
        {
            talentFilterBar.gameObject.SetActive(category == "Talents");
            for (int i = 0; i < typeButtons.Count; i++)
                typeButtons[i].image.color = typeFilter == i - 1 ? UIFactory.Accent : UIFactory.PanelLight;
        }
        countText.text = shown == 0 ? "Nothing found. Try another word (e.g. fire, orb, fusion, poison)." : $"{shown} topic{(shown == 1 ? "" : "s")}";
        scroll.verticalNormalizedPosition = 1f;
    }

    // ---------------------------------------------------------------- content

    /// <summary>Filter row at the top of the TALENTS page: type (all / active / passive) and group.</summary>
    void BuildTalentFilter()
    {
        talentFilterBar = UIFactory.CreateRect("TalentFilter", content);
        talentFilterBar.SetAsFirstSibling();
        UIFactory.SetLayoutSize(talentFilterBar, -1, 64f);
        string[] types = { "ALL TYPES", "ACTIVE", "PASSIVE" };
        for (int i = 0; i < types.Length; i++)
        {
            int value = i - 1;
            var b = UIFactory.CreateButton(talentFilterBar, types[i], types[i], UIFactory.PanelLight, 19);
            UIFactory.TopLeft((RectTransform)b.transform, 8 + i * 168, 6, 160, 52);
            b.onClick.AddListener(() => { typeFilter = value; ApplyFilter(); });
            typeButtons.Add(b);
        }
        categoryDropdown = UIFactory.CreateDropdown(talentFilterBar, "Group", 20, 420f);
        UIFactory.TopLeft((RectTransform)categoryDropdown.transform, 530, 6, 300, 52);
        var opts = new List<Dropdown.OptionData> { new Dropdown.OptionData("All groups") };
        foreach (TalentCategory c in Enum.GetValues(typeof(TalentCategory))) opts.Add(new Dropdown.OptionData(TalentCatalog.Name(c)));
        categoryDropdown.ClearOptions();
        categoryDropdown.AddOptions(opts);
        categoryDropdown.onValueChanged.AddListener(v => { categoryFilter = v - 1; ApplyFilter(); });
    }

    void Add(string cat, string title, Sprite icon, string body, Action<RectTransform> visual = null, float visualHeight = 0f, string keywords = "")
    {
        entries.Add(new Entry
        {
            category = cat, title = title, icon = icon, body = body, visual = visual, visualHeight = visualHeight,
            search = (cat + " " + title + " " + System.Text.RegularExpressions.Regex.Replace(body ?? "", "<.*?>", "") + " " + keywords).ToLowerInvariant()
        });
    }

    static string Pct(float f) => $"{f * 100f:0.#}%";
    static string RColor(CardRarity r) => ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(r));

    void BuildEntries()
    {
        var theme = UITheme.Instance;
        Sprite El(CardElement e) => theme != null ? theme.ElementIcon(e) : null;

        // ---- Elements
        Add("Elements", "Element cycle", El(CardElement.Fire),
            "Each card has one of 9 elements. Attacking an element you beat deals <color=#7CFC6A>+25%</color> damage; attacking one that beats you deals <color=#FF6B6B>-25%</color>. Arrows point from the winner to the element it beats. Hover an icon for its name.",
            ElementCycle, 470f, "counter advantage weakness fire tree rock lightning water dark light neutral non-neutral");
        Add("Elements", "Element damage chart", El(CardElement.Neutral),
            "Row = your element, column = the enemy's element. Green = you deal more damage, red = less.",
            ElementMatrix, 560f, "matrix table multiplier");
        Add("Elements", "Element reroll odds", theme != null ? theme.elementOrbIcon : null,
            "An Element Orb rerolls a card's element: Fire, Tree, Rock, Lightning and Water 13% each, Dark and Light 11% each, Neutral 8%, Non-Neutral 5%. Repeats are possible.",
            null, 0f, "orb odds chance");

        // ---- Items
        Add("Items", "Gold", theme != null ? theme.goldIcon : null,
            $"Earned from every floor win ({GameRules.FloorGold(1, 1)} at LOC 1 floor 1, 20 x LOC + 5 x floor), gold packs and dismantling. Spent on gold packs, the orb shop, stat rerolls, element/talent rerolls without an orb, and economy/stamina buffs.");
        Add("Items", "Diamonds", theme != null ? theme.diamondIcon : null,
            "Drop from gold packs (Common, Rare, Super Rare) and Treasure Hunter loot. Spent on diamond packs, build slots 2 and 3, and fusion/collection/combat buffs. They can't be bought.");
        Add("Items", "Fusion Orb", theme != null ? theme.fusionOrbIcon : null,
            $"Needed for fusion: 1 per attempt at Common/Uncommon up to 20 at Mythic. {GameRules.FusionOrbPrice:N0} gold in the shop; 5% drop from every floor win; also in packs.");
        Add("Items", "Element Orb", theme != null ? theme.elementOrbIcon : null,
            $"Rerolls a card's element. Used first when you have one; without one the reroll costs {GameRules.ElementOrbPrice:N0} gold. Shop price {GameRules.ElementOrbPrice:N0} gold.");
        Add("Items", "Talent Orb", theme != null ? theme.talentOrbIcon : null,
            $"Rerolls a card's talent (any talent can appear on any rarity). Used first when you have one; without one the reroll costs {GameRules.TalentOrbPrice:N0} gold. Shop price {GameRules.TalentOrbPrice:N0} gold.");
        Add("Items", "God Orb", theme != null ? theme.godOrbIcon : null,
            $"Only works on God cards: {Pct(GameRules.GodOrbSuccess)} chance to add +1% to every stat (max +{GameRules.GodOrbMaxStacks}%); a failed orb is used up. Never sold: {Pct(GameRules.GodOrbEnemyDrop)} drop from God-rarity floor enemies, and a bonus chance in diamond packs (Ultra Rare 1%, Epic 3%, Legendary 6%).");
        Add("Items", "Stamina", theme != null ? theme.staminaIcon : null,
            $"Every fight costs {GameRules.StaminaCost} stamina. Max {GameRules.BaseMaxStamina} (+20 with Potion of Stamina). You get it back: +1 every 5 minutes, +{GameRules.StaminaNewFloor} for beating a new floor, and a full refill when a new LOC unlocks.");

        // ---- Stats & rarity
        var rows = new List<string[]>();
        for (int t = 0; t <= GameRules.MaxTier; t++)
        {
            var r = (CardRarity)t;
            var zero = new BuildData();
            rows.Add(new[] { CardStats.RarityName(r), $"x{GameRules.RarityMultiplier(t):0.00}", $"±{GameRules.MaxDeviation(t)}", $"+{(GameRules.TalentPower(t) - 1f) * 100f:0}%", CardStats.Power(r, zero).ToString(), t == GameRules.MaxTier ? "-" : Pct(GameRules.CardDropChance(r)) });
        }
        Add("Stats & Rarity", "Card stats", theme != null ? theme.Frame(CardRarity.Legendary) : null,
            $"Every card is your character, so cards only differ by rarity and build. Each stat = (100 + Roll) x rarity multiplier. HP = HP stat x {GameRules.HpMultiplier}. Power = HP stat + ATK + DEF + SPD.",
            rt => Table(rt, new[] { "Rarity", "Stats", "Roll", "Talent", "Power (Roll 0)", "Floor drop" }, rows, new[] { 210f, 130f, 110f, 120f, 200f, 160f }, true), 46f * 10 + 10f, "power hp atk def spd roll multiplier rarity tier");
        Add("Stats & Rarity", "Rarity order", theme != null ? theme.Frame(CardRarity.God) : null,
            string.Join("  >  ", Enumerable.Range(0, GameRules.MaxTier + 1).Select(t => $"<color=#{RColor((CardRarity)t)}>{CardStats.RarityName((CardRarity)t)}</color>")) +
            "\nGOD is locked until you buy the New Generation buff.", null, 0f, "common uncommon rare super ultra epic legendary mythic god");
        Add("Stats & Rarity", "Build slots", null,
            $"3 slots (free / {GameRules.SlotUnlockCost(1)} / {GameRules.SlotUnlockCost(2)} diamonds). Each slot keeps a card with its own Roll, element and talent, so you can keep different builds of the same card. Pick the slot on the map before FIGHT. Slotted cards are locked automatically and can't be used as fodder.",
            null, 0f, "slot lock");

        // ---- Fusion
        var frows = new List<string[]>();
        for (int t = 0; t < GameRules.MaxTier; t++)
            frows.Add(new[] { $"{CardStats.RarityName((CardRarity)t)} > {CardStats.RarityName((CardRarity)(t + 1))}", Pct(GameRules.FusionBaseRate(t)), GameRules.FusionOrbCost(t).ToString(), "2" });
        Add("Fusion", "Fusion", theme != null ? theme.fusionOrbIcon : null,
            "Main card + 2 fodder cards of the same rarity + Fusion Orbs. Success: the main card goes up one rarity and keeps its Roll, element and talent (every slot using it ranks up too). Fail: the 2 fodder and the orbs are lost, the main card stays. Locked or slotted cards can't be fodder. Mythic > God needs New Generation.",
            rt => Table(rt, new[] { "Fusion", "Success", "Fusion Orbs", "Fodder" }, frows, new[] { 380f, 180f, 200f, 150f }, false), 46f * 9 + 10f, "fodder orb success fail rank up");
        Add("Fusion", "Auto merge", theme != null ? theme.fusionOrbIcon : null,
            "Collection > AUTO MERGE fuses for you: pick the highest rarity you want and the most Fusion Orbs you are willing to spend. It shows the most it could cost before you confirm, then fuses lowest rarity first (slotted and locked cards are only ever upgraded, never used as fodder) and reports successes, fails, the cards you got and what your buffs saved.",
            null, 0f, "automatic merge fuse bulk");
        Add("Fusion", "Fusion buffs", theme != null ? theme.BuffIcon(Buffs.FusionFortune) : null,
            "Fusion Fortune: +3% success. Fusion Protection: a failed fusion costs only 1 fodder. Fusion Blessing: 3% chance to jump one more rarity (never into Mythic or God). Lucky Fusion: 5% + 5% per orb used chance to get 1 Fusion Orb back.");

        // ---- Rerolls
        var rrows = new List<string[]>();
        for (int t = 0; t <= GameRules.MaxTier; t++) rrows.Add(new[] { CardStats.RarityName((CardRarity)t), $"{GameRules.StatRerollCost(t):N0}", $"{GameRules.DismantleGold(t):N0}" });
        Add("Rerolls", "Stat reroll", theme != null ? theme.goldIcon : null,
            "Rerolls all 4 Rolls (HP, ATK, DEF, SPD) at once for gold. You see the old and new build side by side, then save the new one to a slot or discard it. Efficient Upgrader: -10% gold.",
            rt => Table(rt, new[] { "Rarity", "Stat reroll (gold)", "Dismantle (gold)" }, rrows, new[] { 260f, 300f, 300f }, true), 46f * 10 + 10f, "roll cost gold dismantle salvage");
        Add("Rerolls", "Element and talent rerolls", theme != null ? theme.talentOrbIcon : null,
            $"No pop-up: the new roll shows on the card, so you can keep rerolling, then press SAVE TO SLOT. Uses 1 Element / Talent Orb if you have one (no gold). With no orb, it costs the orb's price in gold ({GameRules.ElementOrbPrice:N0} / {GameRules.TalentOrbPrice:N0}). Preview, then save to a slot or discard.", null, 0f, "orb element talent");
        Add("Fusion", "Card materials", theme != null ? theme.diamondIcon : null,
            "Collection > MATERIALS / EXCHANGE turns cards you don't need into materials: 1 card = 1 material of its rarity (Common Material, SR Material...). Materials take no space and count as fodder cards of their rarity: fusion and auto merge use them first. Exchange the selected card, every unlocked card up to a rarity, or switch on AUTO EXCHANGE so new cards from packs and drops become materials right away (also on the Gacha screen).",
            null, 0f, "material exchange space fodder auto exchange common sr");
        Add("Rerolls", "Dismantling", theme != null ? theme.BuffIcon(Buffs.SalvageExpert) : null,
            "Unlocked by the Salvage Expert buff. Turns a card that is not locked or slotted into gold: half the stat reroll cost of its rarity (table above).", null, 0f, "salvage gold");

        // ---- Talents
        var db = CardDatabase.Instance;
        if (db != null)
        {
            Add("Talents", "God cards: two talents", theme != null ? theme.Frame(CardRarity.God) : null,
                "A God card has two talents: one ACTIVE and one PASSIVE. A card that reaches God by fusion keeps its talent and gets one of the other kind. On a God card, REROLL TALENT rerolls both at once (one Talent Orb). God floor enemies also have two talents.",
                null, 0f, "god two talents active passive reroll");
            foreach (var t in db.talents.Where(x => x != null).OrderBy(x => x.DisplayName))
            {
                Add("Talents", t.DisplayName, theme != null ? theme.TalentIcon(t) : null,
                    $"{(t.IsActive ? "<color=#6FD3FF>ACTIVE</color>" : "<color=#FFB23D>PASSIVE</color>")}  <color=#9AA4B5>Group: {TalentCatalog.Name(t.Category)}</color>\n{t.Description}\n<color=#9AA4B5>Any rarity can have it. A number with (Max ...) grows with rarity: +2.5% per tier, up to the Max value at God. Your cards show their own numbers.</color>",
                    null, 0f, (t.IsActive ? "active" : "passive") + " " + TalentCatalog.Name(t.Category));
                entries[entries.Count - 1].talentType = t.IsActive ? 0 : 1;
                entries[entries.Count - 1].talentCat = (int)t.Category;
            }
        }

        // ---- Buffs
        foreach (var b in Buffs.All)
            Add("Buffs", b.name, theme != null ? theme.BuffIcon(b.id) : null,
                $"{b.description}\n<color=#9AA4B5>{Buffs.GroupName(b.group)}  |  {(b.currency == BuffCurrency.Gold ? $"{b.price:N0} gold" : $"{b.price:N0} diamonds")}  |  permanent</color>" +
                (PlayerProfile.Has(b.id) ? "  <color=#7CFC6A>OWNED</color>" : ""));

        // ---- Stamina & rewards
        Add("Stamina & Rewards", "Stamina rules", theme != null ? theme.staminaIcon : null,
            $"Fight cost: {GameRules.StaminaCost}. Max: {GameRules.BaseMaxStamina}. Regen: +1 every 5 minutes. Beating a floor for the first time: +{GameRules.StaminaNewFloor}. Unlocking a new LOC: full refill. Nothing else gives stamina, except buffs: Stamina Master (2 per 5 minutes), Elixir (+2 per win), Potion of Stamina (+20 max), Returning Heroes (+1 per defeat).");
        Add("Stamina & Rewards", "Floor rewards", theme != null ? theme.goldIcon : null,
            "Every win: 20 x LOC + 5 x floor gold, a 5% Fusion Orb drop, and a chance to get a card of YOUR character at the enemy's rarity (Common 12%, -2% per rarity, min 1%). God enemies drop no cards but may drop a God Orb. Buffs: Golden Touch +10% gold, Hunter's Instinct +10% card chance, Rare Encounter 3% one rarity higher, Treasure Hunter +5% rare loot.", null, 0f, "gold drop loot card");

        // ---- Gacha
        foreach (var pack in Packs.All)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"{(pack.currency == PackCurrency.Gold ? $"{pack.price:N0} gold" : $"{pack.price:N0} diamonds")}. Item 1: a guaranteed <color=#{RColor(pack.guaranteed)}>{CardStats.RarityName(pack.guaranteed)}</color> card of your character. Items 2-5: ");
            sb.Append(string.Join(", ", pack.entries.OrderByDescending(e => e.chance).Select(e => $"{GachaScreen.ItemLabel(e.type, e.amount, e.rarity)} {e.chance:0.##}%")));
            float god = GameRules.GodOrbPackChance(pack.id);
            if (god > 0f) sb.Append($". Bonus God Orb: {Pct(god)}");
            Add("Gacha", pack.name, theme != null ? theme.PackArt(Array.IndexOf(Packs.All, pack)) ?? theme.Frame(pack.guaranteed) : null, sb.ToString() + ".", null, 0f, "pack odds summon");
        }

        // ---- Battle
        Add("Battle", "How a battle works", theme != null ? theme.Frame(CardRarity.Epic) : null,
            $"One card against one enemy. The faster card acts first each round. Damage = (100 x ATK x current ATK + 638,000) / (87 x DEF) x element bonus. Base crit 5% (x1.35 damage), base evasion rating 5 (4.8% evade chance). Mana fills every turn; an active talent fires when it is full. From round {GameRules.EnrageStartRound} both fighters get +{GameRules.EnragePercent}% ATK per round (enrage). After {GameRules.MaxRounds} rounds the battle is a draw.",
            null, 0f, "damage crit evasion mana enrage round formula");
        Add("Save Data", "Move your save to another PC", null,
            $"Main menu > SETTINGS > EXPORT / IMPORT SAVE > EXPORT SAVE FILE writes one .{SaveTransfer.Extension} file with everything: cards and builds, slots, buffs, currencies, orbs, stamina, LOC progress, stats, settings and your character's name and picture. Copy it to the other PC (USB, cloud drive, email), then SETTINGS > EXPORT / IMPORT SAVE > IMPORT SAVE FILE there. Your old save on that PC is backed up first. Stamina keeps regenerating for the time in between.",
            null, 0f, "export import backup transfer pc computer load file");
        Add("Save Data", "Save protection", theme != null ? theme.lockIcon : null,
            "Saves and exported files are signed. If a save file is edited outside the game, it is refused: the last valid save is restored (or a new game starts) and the edited file is kept as a backup. Edited .anisave files can't be imported.",
            null, 0f, "cheat hash tamper edit security");
        Add("Battle", "Evasion rating", theme != null ? theme.BuffIcon(Buffs.TheFlash) : null,
            "Evasion is a rating with no limit. Your real chance to evade a normal attack = 75% x (1 - e^(-rating / 75)), so each extra point is worth a bit less and the chance never passes 75%.\n" +
            "Rating 10 = 9.4%   20 = 17.6%   50 = 36.5%   100 = 55%   200 = 70%   300 = 73.6%   1000 = 75%.\n" +
            "Talents and skills can't be evaded, only normal attacks. A card that has cast Precision can't have its critical hits evaded.",
            null, 0f, "evasion evade dodge rating chance cap diminishing returns precision");
        Add("Battle", "Reading the animations", theme != null ? theme.sparkle : null,
            "Normal hit: the attacker winds up, dashes in with afterimages and a white slash hits. CRITICAL: a longer golden wind-up, a double gold slash, light burst, screen flash, a short slow-motion and a CRITICAL! slam. EVADE: the defender hops aside leaving a blue afterimage and wind streaks. Talents light up the card with rays; heals rise in green, burn in orange sparks, poison in green bubbles. SPEED x1/x2/x4 speeds everything up, SKIP jumps to the result.",
            null, 0f, "animation crit critical evade dodge attack effect speed skip");
        Add("Battle", "Combat buffs", theme != null ? theme.BuffIcon(Buffs.StrongestHero) : null,
            "The Flash: +2 evasion rating. Head Hunter: +2% crit. Strongest Hero: 1% chance the enemy starts at half HP. Second Wind: 1% chance to survive a lethal hit with 1 HP (once per battle).");
    }

    // ---------------------------------------------------------------- entry UI

    GameObject BuildEntry(Entry e)
    {
        var panel = UIFactory.CreatePanel(content, e.title, new Color(0.07f, 0.06f, 0.14f, 0.96f), false);
        var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(22, 22, 16, 18);
        v.spacing = 10f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        var header = UIFactory.CreateRect("Header", panel.transform);
        UIFactory.SetLayoutSize(header, -1, 64f);
        if (e.icon != null)
        {
            var icon = UIFactory.CreateImage(header, "Icon", Color.white, e.icon);
            icon.preserveAspect = true;
            UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(64, 64));
        }
        var title = UIFactory.CreateText(header, "Title", e.title.ToUpperInvariant(), 30, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Stretch(title.rectTransform, e.icon != null ? 82 : 0, 0, 0, 0);
        var cat = UIFactory.CreateText(header, "Category", e.category.ToUpperInvariant(), 17, UIFactory.TextDim, TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.Stretch(cat.rectTransform);

        if (!string.IsNullOrEmpty(e.body))
        {
            var body = UIFactory.CreateText(panel.transform, "Body", e.body, 22, new Color(0.86f, 0.88f, 0.94f), TextAnchor.UpperLeft);
            body.verticalOverflow = VerticalWrapMode.Overflow;
        }
        if (e.visual != null)
        {
            var area = UIFactory.CreateRect("Visual", panel.transform);
            UIFactory.SetLayoutSize(area, -1, e.visualHeight);
            e.visual(area);
        }
        return panel.gameObject;
    }

    static void Table(RectTransform area, string[] headers, List<string[]> rows, float[] widths, bool rarityColumn)
    {
        const float h = 46f;
        float x0 = 0f;
        for (int r = -1; r < rows.Count; r++)
        {
            float y = -(r + 1) * h;
            var bg = UIFactory.CreateImage(area, "Row", r < 0 ? new Color(0.2f, 0.16f, 0.35f, 1f) : (r % 2 == 0 ? new Color(1f, 1f, 1f, 0.04f) : new Color(1f, 1f, 1f, 0.08f)));
            var brt = bg.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(x0, y);
            brt.sizeDelta = new Vector2(widths.Sum(), h - 2f);
            float x = x0;
            string[] cells = r < 0 ? headers : rows[r];
            for (int c = 0; c < cells.Length && c < widths.Length; c++)
            {
                Color col = r < 0 ? UIFactory.Gold : UIFactory.TextMain;
                if (r >= 0 && c == 0 && rarityColumn) col = CardStats.RarityColor((CardRarity)Mathf.Clamp(r, 0, GameRules.MaxTier));
                var t = UIFactory.CreateText(area, "Cell", cells[c], 20, col, c == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, r < 0 || c == 0 ? FontStyle.Bold : FontStyle.Normal);
                var rt = t.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(x + (c == 0 ? 14f : 0f), y);
                rt.sizeDelta = new Vector2(widths[c] - (c == 0 ? 14f : 0f), h - 2f);
                UIFactory.BestFit(t, 12);
                x += widths[c];
            }
        }
    }

    static Image ElementImage(RectTransform parent, CardElement e, Vector2 pos, float size)
    {
        var theme = UITheme.Instance;
        var sprite = theme != null ? theme.ElementIcon(e) : null;
        var img = UIFactory.CreateImage(parent, ElementSystem.DisplayName(e), sprite != null ? Color.white : ElementSystem.GetColor(e), sprite != null ? sprite : (theme != null ? theme.circle : null));
        img.preserveAspect = true;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        TooltipTrigger.Add(img, IconInfo.Element(e));
        return img;
    }

    static void Arrow(RectTransform parent, Vector2 from, Vector2 to, Color color, float trim)
    {
        var theme = UITheme.Instance;
        var dir = to - from;
        float len = dir.magnitude - 2f * trim;
        if (len <= 4f) return;
        var mid = (from + to) * 0.5f;
        var img = UIFactory.CreateImage(parent, "Arrow", color, theme != null ? theme.arrow : null);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = mid;
        rt.sizeDelta = new Vector2(len, 26f);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    static Text Label(RectTransform parent, string text, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        var t = UIFactory.CreateText(parent, "Label", text, fontSize, UIFactory.TextMain, anchor, FontStyle.Bold);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return t;
    }

    static void ElementCycle(RectTransform area)
    {
        var cycle = new[] { CardElement.Fire, CardElement.Tree, CardElement.Rock, CardElement.Lightning, CardElement.Water };
        var center = new Vector2(260f, -235f);
        const float radius = 165f;
        var pos = new Vector2[5];
        for (int i = 0; i < 5; i++)
        {
            float a = (90f - i * 72f) * Mathf.Deg2Rad;
            pos[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        for (int i = 0; i < 5; i++) Arrow(area, pos[i], pos[(i + 1) % 5], new Color(0.55f, 1f, 0.45f, 0.9f), 56f);
        for (int i = 0; i < 5; i++) ElementImage(area, cycle[i], pos[i], 92f);
        Label(area, "+25%", center + new Vector2(-38f, 0f), new Vector2(100, 40), 30).color = new Color(0.55f, 1f, 0.45f);

        float x = 600f;
        // Dark <-> Light
        ElementImage(area, CardElement.Dark, new Vector2(x, -60f), 76f);
        Arrow(area, new Vector2(x, -60f), new Vector2(x + 170f, -60f), new Color(0.55f, 1f, 0.45f, 0.9f), 46f);
        Arrow(area, new Vector2(x + 170f, -60f), new Vector2(x, -60f), new Color(0.55f, 1f, 0.45f, 0.9f), 46f);
        ElementImage(area, CardElement.Light, new Vector2(x + 170f, -60f), 76f);
        Label(area, "+25% both ways", new Vector2(x + 230f, -60f), new Vector2(420, 40), 24);
        // Same element
        ElementImage(area, CardElement.Water, new Vector2(x, -170f), 76f);
        Label(area, "=", new Vector2(x + 70f, -170f), new Vector2(40, 40), 34, TextAnchor.MiddleCenter);
        ElementImage(area, CardElement.Water, new Vector2(x + 170f, -170f), 76f);
        Label(area, "same element: -12.5%", new Vector2(x + 230f, -170f), new Vector2(420, 40), 24).color = new Color(1f, 0.55f, 0.55f);
        // Non-Neutral
        ElementImage(area, CardElement.NonNeutral, new Vector2(x, -280f), 76f);
        Arrow(area, new Vector2(x, -280f), new Vector2(x + 170f, -280f), new Color(0.55f, 1f, 0.45f, 0.9f), 46f);
        var all = UIFactory.CreateText(area, "All", "ALL", 24, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        var art = all.rectTransform; art.anchorMin = art.anchorMax = new Vector2(0f, 1f); art.anchoredPosition = new Vector2(x + 170f, -280f); art.sizeDelta = new Vector2(80, 40);
        Label(area, "+12.5% vs every element\n(-12.5% vs Neutral)", new Vector2(x + 230f, -280f), new Vector2(420, 70), 22);
        // Neutral -> Non-Neutral
        ElementImage(area, CardElement.Neutral, new Vector2(x, -390f), 76f);
        Arrow(area, new Vector2(x, -390f), new Vector2(x + 170f, -390f), new Color(0.55f, 1f, 0.45f, 0.9f), 46f);
        ElementImage(area, CardElement.NonNeutral, new Vector2(x + 170f, -390f), 76f);
        Label(area, "+25% vs Non-Neutral", new Vector2(x + 230f, -390f), new Vector2(420, 40), 24);
    }

    static void ElementMatrix(RectTransform area)
    {
        var all = ElementSystem.All;
        const float cell = 52f;
        float x0 = 20f, y0 = -10f;
        for (int i = 0; i < all.Length; i++)
        {
            ElementImage(area, all[i], new Vector2(x0 + (i + 1) * cell + cell / 2f, y0 - cell / 2f), cell - 8f);
            ElementImage(area, all[i], new Vector2(x0 + cell / 2f, y0 - (i + 1) * cell - cell / 2f), cell - 8f);
        }
        for (int r = 0; r < all.Length; r++)
            for (int c = 0; c < all.Length; c++)
            {
                float m = ElementSystem.GetElementMultiplier(all[r], all[c]);
                Color bg = m > 1f ? new Color(0.2f, 0.65f, 0.3f, 0.85f) : m < 1f ? new Color(0.75f, 0.2f, 0.25f, 0.85f) : new Color(1f, 1f, 1f, 0.06f);
                var img = UIFactory.CreateImage(area, "Cell", bg, UIFactory.Theme != null ? UIFactory.Theme.roundedSmall : null, true);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(x0 + (c + 1) * cell + 2f, y0 - (r + 1) * cell - 2f);
                rt.sizeDelta = new Vector2(cell - 4f, cell - 4f);
                if (Mathf.Abs(m - 1f) > 0.001f)
                {
                    var t = UIFactory.CreateText(img.transform, "Value", ElementSystem.Describe(m), 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                    UIFactory.Stretch(t.rectTransform);
                    UIFactory.BestFit(t, 9);
                }
            }
        var note = UIFactory.CreateText(area, "Note", "Hover any icon to see the element's name.", 20, UIFactory.TextDim, TextAnchor.UpperLeft, FontStyle.Italic);
        var nrt = note.rectTransform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(0f, 1f);
        nrt.pivot = new Vector2(0f, 1f);
        nrt.anchoredPosition = new Vector2(x0 + 11 * cell, y0 - cell);
        nrt.sizeDelta = new Vector2(500, 120);
    }

    // ---------------------------------------------------------------- layout

    public static LibraryScreen BuildLayout(RectTransform canvas)
    {
        var screen = canvas.gameObject.AddComponent<LibraryScreen>();
        AnimeBackground.Build(canvas, "map", new Color(0.4f, 0.55f, 1f, 0.6f), new Color(0.05f, 0.06f, 0.15f), out _);
        UIFactory.CreateTopBarWithCurrency(canvas, "LIBRARY", out screen.backButton, out screen.titleText);

        var side = UIFactory.CreatePanel(canvas, "Categories", new Color(0.05f, 0.04f, 0.11f, 0.95f), true);
        UIFactory.Region(side.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), 24, 120, 0, 24);
        side.rectTransform.sizeDelta = new Vector2(300, side.rectTransform.sizeDelta.y);
        side.rectTransform.anchoredPosition = new Vector2(174, side.rectTransform.anchoredPosition.y);
        screen.categoryList = UIFactory.CreateRect("List", side.transform);
        UIFactory.Stretch(screen.categoryList, 14, 14, 14, 14);
        var v = UIFactory.AddVertical(screen.categoryList, 8f, 0);
        v.childControlHeight = true;

        var main = UIFactory.CreatePanel(canvas, "Main", new Color(0.04f, 0.03f, 0.09f, 0.9f), true);
        UIFactory.Region(main.rectTransform, Vector2.zero, Vector2.one, 344, 120, 24, 24);
        screen.searchInput = UIFactory.CreateInputField(main.transform, "Search", "Search: element, fusion, orb, poison, stamina...", 26);
        UIFactory.TopStrip((RectTransform)screen.searchInput.transform, 16, 64, 20, 300);
        screen.countText = UIFactory.CreateText(main.transform, "Count", "", 20, UIFactory.TextDim, TextAnchor.MiddleRight);
        UIFactory.Place(screen.countText.rectTransform, new Vector2(1f, 1f), new Vector2(-20, -16), new Vector2(270, 64));
        UIFactory.BestFit(screen.countText, 12);
        screen.scroll = UIFactory.CreateScroll(main.transform, "Entries", true, out RectTransform content);
        UIFactory.Stretch((RectTransform)screen.scroll.transform, 12, 92, 12, 12);
        UIFactory.AddVertical(content, 16f, 8);
        screen.content = content;
        return screen;
    }
}
