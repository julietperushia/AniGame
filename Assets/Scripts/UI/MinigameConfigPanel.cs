using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Test Mode > MINIGAME PRIZES: the shared cooldown and every game's prize lines (reach the score, win the prize;
/// roulette: wheel segments picked by weight). SAVE writes Assets/Resources/Config/minigames.json. Built at runtime.
/// </summary>
public class MinigameConfigPanel : MonoBehaviour
{
    static readonly Dictionary<string, string> ScoreHelp = new Dictionary<string, string>
    {
        { MinigameConfig.Flappy, "Score = pipes passed. The best line reached pays." },
        { MinigameConfig.Quiz, "Score = right answers (0-5). The best line reached pays." },
        { MinigameConfig.Slots, "Score: 1 = a pair, 2 = three of a kind, 3 = three 7s (jackpot)." },
        { MinigameConfig.Roulette, "Each line is a wheel segment; WEIGHT = how likely it is (MIN SCORE is not used)." },
        { MinigameConfig.HigherLower, "Score = right guesses in a row (0-10). The best line reached pays." },
        { MinigameConfig.Memory, "Score = pairs found (0-6). The best line reached pays." },
    };

    MinigameConfig cfg;
    string selected = MinigameConfig.Flappy;
    RectTransform tabs, rowsContent;
    Text status, help;
    InputField cooldownIn;
    Button enabledButton;
    static MinigameConfigPanel instance;

    public static void Open(Transform canvas)
    {
        if (instance == null) instance = Build(canvas);
        instance.gameObject.SetActive(true);
        instance.transform.SetAsLastSibling();
        MinigameConfig.Reload();
        instance.cfg = MinigameConfig.Current;
        instance.cooldownIn.text = instance.cfg.cooldownMinutes.ToString();
        instance.BuildTabs();
        instance.Select(instance.selected);
        instance.status.text = $"Cooldown left now: {PlayerProfile.CooldownText(PlayerProfile.MinigameCooldownLeft)}";
    }

    static MinigameConfigPanel Build(Transform canvas)
    {
        var dim = UIFactory.CreateImage(canvas, "MinigameConfig", new Color(0, 0, 0, 0.85f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        var p = dim.gameObject.AddComponent<MinigameConfigPanel>();
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.04f, 0.08f, 0.12f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1720, 1000));
        UIFactory.AddOutline(panel, new Color(0.4f, 0.75f, 1f, 0.7f), 3f);
        var pt = panel.transform;
        var title = UIFactory.CreateText(pt, "Title", "MINIGAME PRIZES  <size=22><color=#9AA4B5>(Test Mode)</color></size>", 38, new Color(0.55f, 0.85f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, 30, 14, 900, 56);

        var cdl = UIFactory.CreateText(pt, "CooldownL", "Cooldown (minutes):", 22, UIFactory.TextDim, TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.TopLeft(cdl.rectTransform, 900, 16, 260, 52);
        p.cooldownIn = UIFactory.CreateInputField(pt, "Cooldown", "5", 22);
        UIFactory.TopLeft((RectTransform)p.cooldownIn.transform, 1170, 16, 120, 52);
        p.cooldownIn.contentType = InputField.ContentType.IntegerNumber;
        p.cooldownIn.onEndEdit.AddListener(s => { if (int.TryParse(s, out int n)) { p.cfg.cooldownMinutes = Mathf.Max(0, n); p.status.text = "Changed (not saved yet)."; } });
        Btn(pt, "RESET COOLDOWN", new Color(0.35f, 0.3f, 0.45f), 1310, 16, 380, () => { PlayerProfile.ResetMinigameCooldown(); p.status.text = "Cooldown reset: you can play now."; }, 52);

        p.tabs = UIFactory.CreateRect("Tabs", pt);
        UIFactory.TopLeft(p.tabs, 30, 84, 1660, 60);
        p.help = UIFactory.CreateText(pt, "Help", "", 20, UIFactory.Gold, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(p.help.rectTransform, 30, 152, 1280, 40);
        p.enabledButton = Btn(pt, "ENABLED", new Color(0.25f, 0.6f, 0.4f), 1430, 148, 260, () => p.ToggleEnabled(), 48);

        string[] heads = { "LABEL", "MIN SCORE", "WEIGHT", "PRIZE", "AMOUNT", "RARITY / PACK", "" };
        float[] xs = { 0, 330, 470, 610, 880, 1050, 1300 };
        for (int i = 0; i < heads.Length; i++)
        {
            var h = UIFactory.CreateText(pt, "H" + i, heads[i], 18, UIFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.TopLeft(h.rectTransform, 40 + xs[i], 200, 260, 30);
        }
        var box = UIFactory.CreatePanel(pt, "Rows", new Color(0.02f, 0.04f, 0.07f, 0.95f), true);
        UIFactory.TopLeft(box.rectTransform, 30, 234, 1660, 620);
        var scroll = UIFactory.CreateScroll(box.transform, "Scroll", true, out p.rowsContent);
        UIFactory.Stretch((RectTransform)scroll.transform, 10, 10, 10, 10);
        var v = UIFactory.AddVertical(p.rowsContent, 8, 0);
        v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;

        Btn(pt, "ADD PRIZE LINE", new Color(0.25f, 0.5f, 0.85f), 30, 870, 300, () => p.AddLine());
        Btn(pt, "RESET THIS GAME", new Color(0.35f, 0.3f, 0.45f), 350, 870, 300, () => p.ResetGame());
        Btn(pt, "SAVE", new Color(0.25f, 0.6f, 0.4f), 1150, 870, 260, () => p.SaveNow());
        Btn(pt, "CLOSE", UIFactory.PanelLight, 1430, 870, 260, () => { p.SaveNow(); dim.gameObject.SetActive(false); });
        p.status = UIFactory.CreateText(pt, "Status", "", 19, new Color(0.55f, 1f, 0.6f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(p.status.rectTransform, 30, 940, 1660, 44);
        UIFactory.BestFit(p.status, 11);
        return p;
    }

    static Button Btn(Transform parent, string label, Color c, float x, float y, float w, Action click, float h = 60)
    {
        var b = UIFactory.CreateButton(parent, label, label, c, 20);
        UIFactory.TopLeft((RectTransform)b.transform, x, y, w, h);
        b.onClick.AddListener(() => click());
        return b;
    }

    void BuildTabs()
    {
        UIFactory.Clear(tabs);
        float w = 1660f / Mathf.Max(1, cfg.games.Count) - 8f;
        for (int i = 0; i < cfg.games.Count; i++)
        {
            var g = cfg.games[i];
            string id = g.id;
            var b = UIFactory.CreateButton(tabs, "Tab_" + id, g.title, id == selected ? new Color(0.25f, 0.5f, 0.85f) : UIFactory.PanelLight, 20);
            UIFactory.TopLeft((RectTransform)b.transform, i * (w + 8f), 0, w, 56);
            b.onClick.AddListener(() => Select(id));
        }
    }

    MinigameDef Game => cfg.Find(selected);

    void Select(string id)
    {
        selected = id;
        BuildTabs();
        var g = Game;
        help.text = ScoreHelp.TryGetValue(id, out var h) ? h : "";
        RefreshEnabled();
        UIFactory.Clear(rowsContent);
        if (g == null) return;
        for (int i = 0; i < g.tiers.Count; i++) AddRow(g, g.tiers[i]);
    }

    void RefreshEnabled()
    {
        var g = Game;
        bool on = g == null || g.enabled;
        UIFactory.ButtonLabel(enabledButton).text = on ? "GAME ON" : "GAME OFF";
        enabledButton.image.color = on ? new Color(0.25f, 0.6f, 0.4f) : new Color(0.6f, 0.25f, 0.3f);
    }

    void ToggleEnabled()
    {
        var g = Game;
        if (g == null) return;
        g.enabled = !g.enabled;
        RefreshEnabled();
        status.text = $"{g.title} is now {(g.enabled ? "on" : "off")} (not saved yet).";
    }

    void AddRow(MinigameDef g, PrizeTier t)
    {
        var row = UIFactory.CreateRect("Row", rowsContent);
        UIFactory.SetLayoutSize(row, 0, 54);
        InputField Field(float x, float w, string value, bool number, Action<string> set)
        {
            var f = UIFactory.CreateInputField(row, "F", "", 20);
            UIFactory.TopLeft((RectTransform)f.transform, x, 0, w, 52);
            if (number) f.contentType = InputField.ContentType.IntegerNumber;
            f.text = value;
            f.onEndEdit.AddListener(s => { set(s); status.text = "Changed (not saved yet)."; });
            return f;
        }
        Field(0, 320, t.label, false, s => t.label = s);
        Field(330, 130, t.minScore.ToString(), true, s => { if (int.TryParse(s, out int n)) t.minScore = n; });
        Field(470, 130, t.weight.ToString(), true, s => { if (int.TryParse(s, out int n)) t.weight = Mathf.Max(0, n); });
        var types = Enum.GetValues(typeof(PrizeType)).Cast<PrizeType>().ToList();
        var typeDd = UIFactory.CreateDropdown(row, "Type", 18, 380f);
        UIFactory.TopLeft((RectTransform)typeDd.transform, 610, 0, 260, 52);
        typeDd.options = types.Select(x => new Dropdown.OptionData(Prize.TypeName(x))).ToList();
        typeDd.value = types.IndexOf(t.prize.type);
        typeDd.RefreshShownValue();
        Field(880, 160, t.prize.amount.ToString(), true, s => { if (int.TryParse(s, out int n)) t.prize.amount = Mathf.Max(0, n); });
        // last column: material rarity, or which pack (up to Ultra Rare) when the prize is a Card Pack
        var packs = Prize.PackChoices;
        var tierDd = UIFactory.CreateDropdown(row, "Tier", 18, 380f);
        UIFactory.TopLeft((RectTransform)tierDd.transform, 1050, 0, 240, 52);
        bool filling = false;
        void FillTier()
        {
            filling = true;
            bool pack = t.prize.type == PrizeType.Pack;
            if (pack)
            {
                tierDd.options = packs.Select(x => new Dropdown.OptionData(x.name)).ToList();
                int at = Array.FindIndex(packs, x => x.id == t.prize.packId);
                if (at < 0) { at = 0; t.prize.packId = packs[0].id; }
                tierDd.value = at;
            }
            else
            {
                tierDd.options = Enumerable.Range(0, 9).Select(i => new Dropdown.OptionData(CardStats.RarityName((CardRarity)i))).ToList();
                tierDd.value = Mathf.Clamp(t.prize.materialTier, 0, 8);
            }
            tierDd.interactable = pack || t.prize.type == PrizeType.Material;
            tierDd.RefreshShownValue();
            filling = false;
        }
        FillTier();
        tierDd.onValueChanged.AddListener(i =>
        {
            if (filling) return;
            if (t.prize.type == PrizeType.Pack) t.prize.packId = packs[Mathf.Clamp(i, 0, packs.Length - 1)].id;
            else t.prize.materialTier = i;
            status.text = "Changed (not saved yet).";
        });
        TooltipTrigger.Add(tierDd.targetGraphic, "Materials: which rarity. Card Pack: which pack (opened for free when won, up to the Ultra Rare Pack).");
        typeDd.onValueChanged.AddListener(i => { t.prize.type = types[i]; FillTier(); status.text = "Changed (not saved yet)."; });
        var del = UIFactory.CreateButton(row, "Delete", "DELETE", new Color(0.6f, 0.2f, 0.25f), 18);
        UIFactory.TopLeft((RectTransform)del.transform, 1300, 0, 150, 52);
        del.onClick.AddListener(() => { g.tiers.Remove(t); Select(selected); status.text = "Line removed (not saved yet)."; });
    }

    void AddLine()
    {
        var g = Game;
        if (g == null) return;
        int next = g.tiers.Count == 0 ? 1 : g.tiers.Max(t => t.minScore) + 1;
        g.tiers.Add(new PrizeTier(g.id == MinigameConfig.Roulette ? "New prize" : $"{next}+", g.id == MinigameConfig.Roulette ? 0 : next, new Prize(PrizeType.Gold, 500), 1));
        Select(selected);
        status.text = "Line added (not saved yet).";
    }

    void ResetGame()
    {
        var def = MinigameConfig.Defaults().Find(selected);
        var g = Game;
        if (def == null || g == null) return;
        g.tiers = def.tiers;
        g.enabled = true;
        Select(selected);
        status.text = $"{g.title} prizes reset to the defaults (not saved yet).";
    }

    void SaveNow()
    {
        string where = MinigameConfig.SaveCurrent();
        status.text = where.StartsWith("Error") ? where : $"Saved to {where}";
        SfxSynth.Play("buff", 0.5f);
    }
}
