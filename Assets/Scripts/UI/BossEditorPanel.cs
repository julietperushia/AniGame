using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Test Mode > BOSS EDITOR: add, remove and order the gates of the Boss location. Each boss takes its name and picture
/// from a character of the game; its rarity, element, talents, stats and rewards are set here. SAVE writes
/// Assets/Resources/Config/bosses.json (in a build: an override in persistentDataPath). Built at runtime.
/// </summary>
public class BossEditorPanel : MonoBehaviour
{
    BossConfig cfg;
    int sel;
    bool loading;
    RectTransform listContent, pickerContent;
    readonly List<Button> gateButtons = new List<Button>();
    Text status, previewText, pickLabel, pictureLabel;
    CardTile preview;
    InputField nameIn, hpIn, atkIn, defIn, spdIn, critIn, evaIn, orbsIn, goldIn, matIn, staminaIn, replayIn, pickerSearch;
    Dropdown rarityDd, elementDd, talentDd, talent2Dd, actionsDd, matTierDd;
    readonly List<string> actives = new List<string>(), passives = new List<string>();
    GameObject picker, newCharForm;
    List<Card> allCards;
    // new character form
    InputField ncName, ncAnime;
    Dropdown ncElement;
    CardTile ncPreview;
    string ncPicture;
    Text ncStatus;

    static BossEditorPanel instance;

    public static void Open(Transform canvas)
    {
        if (instance == null) instance = Build(canvas);
        instance.gameObject.SetActive(true);
        instance.transform.SetAsLastSibling();
        BossConfig.Reload();
        instance.cfg = BossConfig.Current;
        instance.sel = Mathf.Clamp(instance.sel, 0, Mathf.Max(0, instance.cfg.gates.Count - 1));
        instance.RebuildList();
        instance.LoadFields();
        instance.status.text = "Edit a gate, then SAVE. Bosses fight the player's 3 build slots alone.";
    }

    BossGate Gate => cfg != null && sel >= 0 && sel < cfg.gates.Count ? cfg.gates[sel] : null;

    // ---------------------------------------------------------------- layout

    static BossEditorPanel Build(Transform canvas)
    {
        var dim = UIFactory.CreateImage(canvas, "BossEditor", new Color(0, 0, 0, 0.85f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        var p = dim.gameObject.AddComponent<BossEditorPanel>();
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.1f, 0.04f, 0.06f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1820, 1010));
        UIFactory.AddOutline(panel, new Color(1f, 0.35f, 0.4f, 0.7f), 3f);
        var pt = panel.transform;
        var title = UIFactory.CreateText(pt, "Title", "BOSS EDITOR  <size=22><color=#9AA4B5>(Test Mode)  -  one location, the gates are fought in order</color></size>", 38, new Color(1f, 0.55f, 0.55f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, 30, 14, 1700, 56);

        // gate list
        var listBox = UIFactory.CreatePanel(pt, "List", new Color(0.05f, 0.02f, 0.03f, 0.95f), true);
        UIFactory.TopLeft(listBox.rectTransform, 30, 84, 370, 760);
        var scroll = UIFactory.CreateScroll(listBox.transform, "Scroll", true, out p.listContent);
        UIFactory.Stretch((RectTransform)scroll.transform, 8, 8, 8, 8);
        var v = UIFactory.AddVertical(p.listContent, 6, 0);
        v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;
        Btn(pt, "ADD GATE", new Color(0.25f, 0.6f, 0.4f), 30, 856, 180, () => p.AddGate());
        Btn(pt, "DELETE", new Color(0.65f, 0.2f, 0.25f), 220, 856, 180, () => p.DeleteGate());
        Btn(pt, "MOVE UP", UIFactory.PanelLight, 30, 926, 180, () => p.Move(-1));
        Btn(pt, "MOVE DOWN", UIFactory.PanelLight, 220, 926, 180, () => p.Move(1));

        // form
        const float lx = 430, cx = 660, row = 62, y0 = 84;
        int r = 0;
        Text Label(string s) { var t = UIFactory.CreateText(pt, "L_" + s, s, 20, UIFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold); UIFactory.TopLeft(t.rectTransform, lx, y0 + r * row, 220, 52); return t; }
        InputField Num(float x, float w, Action<int> set)
        {
            var f = UIFactory.CreateInputField(pt, "In", "0", 22);
            UIFactory.TopLeft((RectTransform)f.transform, x, y0 + r * row, w, 52);
            f.contentType = InputField.ContentType.IntegerNumber;
            f.onEndEdit.AddListener(s => { if (p.loading) return; if (int.TryParse(s, out int n)) set(n); p.Changed(); });
            return f;
        }
        Dropdown Drop(float x, float w, IEnumerable<string> options, Action<int> set)
        {
            var d = UIFactory.CreateDropdown(pt, "Dd", 20, 420f);
            UIFactory.TopLeft((RectTransform)d.transform, x, y0 + r * row, w, 52);
            d.options = options.Select(o => new Dropdown.OptionData(o)).ToList();
            d.onValueChanged.AddListener(i => { if (p.loading) return; set(i); p.Changed(); });
            return d;
        }

        Label("Character");
        var pick = Btn(pt, "PICK CHARACTER", new Color(0.55f, 0.3f, 0.95f), cx, y0 + r * row, 250, () => p.OpenPicker(), 52);
        p.pickLabel = UIFactory.CreateText(pt, "PickLabel", "", 18, UIFactory.TextMain, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(p.pickLabel.rectTransform, cx + 262, y0 + r * row, 280, 52);
        UIFactory.BestFit(p.pickLabel, 11);
        TooltipTrigger.Add(pick.image, "The boss uses this character's picture (and name, unless you type another). ADD CHARACTER in the picker makes a new one from any picture.");
        r++;
        Label("Name");
        p.nameIn = UIFactory.CreateInputField(pt, "Name", "Boss name", 22);
        UIFactory.TopLeft((RectTransform)p.nameIn.transform, cx, y0 + r * row, 540, 52);
        p.nameIn.onEndEdit.AddListener(s => { if (p.loading || p.Gate == null) return; p.Gate.name = s.Trim(); p.Changed(); });
        r++;
        Label("Rarity");
        var rarities = Enum.GetValues(typeof(CardRarity)).Cast<CardRarity>().ToList();
        p.rarityDd = Drop(cx, 540, rarities.Select(CardStats.RarityName), i => p.Gate.rarity = rarities[i]);
        r++;
        Label("Element");
        var elements = ElementSystem.All.ToList();
        p.elementDd = Drop(cx, 540, elements.Select(ElementSystem.DisplayName), i => p.Gate.element = elements[i]);
        r++;
        var db = CardDatabase.Instance;
        p.actives.Add(""); p.passives.Add("");
        if (db != null)
        {
            p.actives.AddRange(db.talents.Where(t => t != null && t.IsActive).Select(t => t.DisplayName).OrderBy(n => n));
            p.passives.AddRange(db.talents.Where(t => t != null && !t.IsActive).Select(t => t.DisplayName).OrderBy(n => n));
        }
        Label("Active talent");
        p.talentDd = Drop(cx, 540, p.actives.Select(n => n == "" ? "(none)" : n), i => p.Gate.talent = p.actives[i]);
        r++;
        Label("Passive talent");
        p.talent2Dd = Drop(cx, 540, p.passives.Select(n => n == "" ? "(none)" : n), i => p.Gate.talent2 = p.passives[i]);
        r++;
        Label("HP % / ATK %");
        p.hpIn = Num(cx, 265, n => p.Gate.hpPercent = Mathf.Max(1, n));
        p.atkIn = Num(cx + 275, 265, n => p.Gate.atkPercent = Mathf.Max(1, n));
        r++;
        Label("DEF % / SPD %");
        p.defIn = Num(cx, 265, n => p.Gate.defPercent = Mathf.Max(1, n));
        p.spdIn = Num(cx + 275, 265, n => p.Gate.spdPercent = Mathf.Max(1, n));
        r++;
        Label("Turns / round, crit %");
        p.actionsDd = Drop(cx, 265, new[] { "1 turn", "2 turns", "3 turns", "4 turns", "5 turns" }, i => p.Gate.actionsPerRound = i + 1);
        p.critIn = Num(cx + 275, 265, n => p.Gate.critRate = Mathf.Clamp(n, 0, 100));
        r++;
        Label("Evasion rating");
        p.evaIn = Num(cx, 265, n => p.Gate.evasion = Mathf.Max(0, n));
        r++;
        Label("God Orbs / Gold");
        p.orbsIn = Num(cx, 265, n => p.Gate.godOrbs = Mathf.Max(0, n));
        p.goldIn = Num(cx + 275, 265, n => p.Gate.gold = Mathf.Max(0, n));
        r++;
        Label("Materials");
        p.matTierDd = Drop(cx, 265, Enumerable.Range(0, 9).Select(PlayerProfile.MaterialName), i => p.Gate.materialTier = i);
        p.matIn = Num(cx + 275, 265, n => p.Gate.materials = Mathf.Max(0, n));
        r++;
        var all = UIFactory.CreateText(pt, "Global", "ALL GATES:", 20, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(all.rectTransform, lx, y0 + r * row + 6, 220, 52);
        r++;
        Label("Stamina / replay %");
        p.staminaIn = Num(cx, 265, n => p.cfg.staminaCost = Mathf.Max(0, n));
        p.replayIn = Num(cx + 275, 265, n => p.cfg.replayRewardPercent = Mathf.Clamp(n, 0, 100));
        TooltipTrigger.Add(p.replayIn.image, "Replaying a defeated gate gives this % of its rewards.");

        // preview + actions
        p.preview = CardTile.Build(pt, "Preview", 300f, 420f, true);
        UIFactory.TopLeft((RectTransform)p.preview.transform, 1230, 84, 300, 420);
        var cp = Btn(pt, "CHANGE PICTURE", new Color(0.55f, 0.3f, 0.95f), 1545, 84, 245, () => p.ChangePicture(), 64);
        TooltipTrigger.Add(cp.image, "Give this boss its own picture: from the anime gallery or imported from your PC. It is saved in Assets/Resources/Config/Pictures so it ships with the game.");
        Btn(pt, "CHARACTER'S PICTURE", new Color(0.35f, 0.3f, 0.45f), 1545, 158, 245, () => p.ResetPicture(), 64);
        p.pictureLabel = UIFactory.CreateText(pt, "PictureLabel", "", 17, UIFactory.TextDim, TextAnchor.UpperLeft);
        UIFactory.TopLeft(p.pictureLabel.rectTransform, 1545, 234, 245, 200);
        p.pictureLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        p.previewText = UIFactory.CreateText(pt, "PreviewText", "", 18, UIFactory.TextMain, TextAnchor.UpperLeft);
        UIFactory.TopLeft(p.previewText.rectTransform, 1230, 516, 560, 330);
        p.previewText.horizontalOverflow = HorizontalWrapMode.Wrap;
        p.previewText.verticalOverflow = VerticalWrapMode.Truncate;
        Btn(pt, "SAVE", new Color(0.25f, 0.6f, 0.4f), 1230, 856, 270, () => p.SaveNow());
        Btn(pt, "TEST FIGHT", new Color(0.9f, 0.25f, 0.35f), 1520, 856, 270, () => p.TestFight());
        Btn(pt, "RESET TO DEFAULTS", new Color(0.35f, 0.3f, 0.45f), 1230, 926, 270, () => p.ResetDefaults());
        Btn(pt, "CLOSE", UIFactory.PanelLight, 1520, 926, 270, () => { p.SaveNow(); dim.gameObject.SetActive(false); });
        p.status = UIFactory.CreateText(pt, "Status", "", 19, new Color(0.55f, 1f, 0.6f), TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(p.status.rectTransform, 430, 940, 780, 50);
        UIFactory.BestFit(p.status, 11);

        p.BuildPicker(dim.transform);
        return p;
    }

    static Button Btn(Transform parent, string label, Color c, float x, float y, float w, Action click, float h = 60)
    {
        var b = UIFactory.CreateButton(parent, label, label, c, 20);
        UIFactory.TopLeft((RectTransform)b.transform, x, y, w, h);
        b.onClick.AddListener(() => click());
        return b;
    }

    // ---------------------------------------------------------------- gates

    void RebuildList()
    {
        UIFactory.Clear(listContent);
        gateButtons.Clear();
        for (int i = 0; i < cfg.gates.Count; i++)
        {
            int index = i;
            var b = UIFactory.CreateButton(listContent, $"Gate{i + 1}", "", UIFactory.PanelLight, 18);
            UIFactory.SetLayoutSize(b, 0, 62);
            var label = UIFactory.ButtonLabel(b);
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(14, 0);
            b.onClick.AddListener(() => { sel = index; RefreshList(); LoadFields(); });
            gateButtons.Add(b);
        }
        RefreshList();
    }

    void RefreshList()
    {
        for (int i = 0; i < gateButtons.Count && i < cfg.gates.Count; i++)
        {
            var g = cfg.gates[i];
            string rc = ColorUtility.ToHtmlStringRGB(CardStats.RarityColor(g.rarity));
            UIFactory.ButtonLabel(gateButtons[i]).text = $"<b>GATE {i + 1}</b>  <size=14><color=#{rc}>{CardStats.RarityName(g.rarity).ToUpperInvariant()}</color></size>\n<size=16>{g.name}</size>";
            gateButtons[i].image.color = i == sel ? new Color(0.6f, 0.15f, 0.22f) : UIFactory.PanelLight;
        }
    }

    void AddGate()
    {
        var copy = Gate != null ? Gate.Clone() : new BossGate();
        copy.hpPercent += 100; copy.atkPercent += 15; copy.defPercent += 15;
        cfg.gates.Insert(Mathf.Min(sel + 1, cfg.gates.Count), copy);
        sel = Mathf.Min(sel + 1, cfg.gates.Count - 1);
        RebuildList();
        LoadFields();
        Changed($"Gate {sel + 1} added (a copy of the selected gate, a bit stronger). Pick its character and SAVE.");
    }

    void DeleteGate()
    {
        if (Gate == null) return;
        if (cfg.gates.Count <= 1) { status.text = "Keep at least one gate."; return; }
        string name = Gate.name;
        cfg.gates.RemoveAt(sel);
        sel = Mathf.Clamp(sel, 0, cfg.gates.Count - 1);
        RebuildList();
        LoadFields();
        Changed($"{name} removed. SAVE to keep it (players keep their cleared count).");
    }

    void Move(int dir)
    {
        int to = sel + dir;
        if (Gate == null || to < 0 || to >= cfg.gates.Count) return;
        var g = cfg.gates[sel];
        cfg.gates[sel] = cfg.gates[to];
        cfg.gates[to] = g;
        sel = to;
        RebuildList();
        LoadFields();
        Changed($"Moved to gate {sel + 1}.");
    }

    void LoadFields()
    {
        var g = Gate;
        if (g == null) return;
        loading = true;
        nameIn.text = g.name;
        rarityDd.value = (int)g.rarity;
        elementDd.value = Mathf.Max(0, Array.IndexOf(ElementSystem.All, g.element));
        talentDd.value = Mathf.Max(0, actives.IndexOf(g.talent ?? ""));
        talent2Dd.value = Mathf.Max(0, passives.IndexOf(g.talent2 ?? ""));
        hpIn.text = g.hpPercent.ToString(); atkIn.text = g.atkPercent.ToString();
        defIn.text = g.defPercent.ToString(); spdIn.text = g.spdPercent.ToString();
        actionsDd.value = Mathf.Clamp(g.actionsPerRound - 1, 0, 4);
        critIn.text = g.critRate.ToString(); evaIn.text = g.evasion.ToString();
        orbsIn.text = g.godOrbs.ToString(); goldIn.text = g.gold.ToString();
        matTierDd.value = Mathf.Clamp(g.materialTier, 0, 8); matIn.text = g.materials.ToString();
        staminaIn.text = cfg.staminaCost.ToString(); replayIn.text = cfg.replayRewardPercent.ToString();
        foreach (var d in new[] { rarityDd, elementDd, talentDd, talent2Dd, actionsDd, matTierDd }) d.RefreshShownValue();
        loading = false;
        RefreshPreview();
    }

    void Changed(string message = null)
    {
        RefreshList();
        RefreshPreview();
        status.text = message ?? "Changed (not saved yet).";
    }

    void RefreshPreview()
    {
        var g = Gate;
        if (g == null) return;
        var m = PlayerProfile.BossMember(g);
        var card = m.art;
        var bcx = cfg.FindCharacter(g.cardKey);
        var gameCard = bcx == null && CardDatabase.Instance != null && !string.IsNullOrEmpty(g.cardKey) ? CardDatabase.Instance.FindByKey(g.cardKey) : null;
        pickLabel.text = bcx != null ? $"{bcx.name}  <color=#9AA4B5>(custom{(string.IsNullOrEmpty(bcx.anime) ? "" : ", " + bcx.anime)})</color>"
                       : gameCard != null ? $"{gameCard.DisplayName}  <color=#9AA4B5>({(string.IsNullOrEmpty(gameCard.anime) ? gameCard.Key : gameCard.anime)})</color>"
                       : "<color=#FF9E9E>no character picked</color>";
        var gp = PictureStore.GalleryCard(g.picture);
        pictureLabel.text = string.IsNullOrEmpty(g.picture) ? "Picture: the character's."
                          : gp != null ? $"Own picture:\n<color=#FFFFFF>{gp.DisplayName}</color> (gallery)"
                          : PictureStore.Load(g.picture) != null ? "Own picture:\n<color=#FFFFFF>imported</color>"
                          : "<color=#FF9E9E>Own picture missing</color> (file not found): the character's is shown.";
        preview.Set(card, g.rarity, g.element, CardStats.RarityName(g.rarity), $"x{m.actionsPerRound}");
        var db = CardDatabase.Instance;
        preview.SetTalent(db != null ? db.FindTalent(g.talent) : null, db != null ? db.FindTalent(g.talent2) : null);
        preview.nameText.text = m.name;
        int max = GameRules.MaxStat(GameRules.Tier(g.rarity));
        previewText.text =
            $"<b>{m.name}</b>  ({CardStats.RarityName(g.rarity)}, {ElementSystem.DisplayName(g.element)})\n" +
            $"<b>HP {m.hp:N0}   ATK {m.atk}   DEF {m.def}   SPD {m.baseSpeed}</b>\n" +
            $"<color=#9AA4B5>(% of the best {CardStats.RarityName(g.rarity)} stat {max}; HP x{GameRules.HpMultiplier}. A team of 3 max cards = 300%.)</color>\n" +
            $"{m.actionsPerRound} turns per round, crit {m.critRate}%, evasion {m.evasion}\n" +
            $"Talents: {(m.talents.Count == 0 ? "none" : m.TalentNames)}\n\n" +
            $"<color=#FFD54F>First clear: {BossScreen.RewardText(g, 100)}\nReplay: {BossScreen.RewardText(g, cfg.replayRewardPercent)}</color>\n" +
            $"<color=#9AA4B5>{cfg.staminaCost} stamina per fight. Your team: {TeamLine()}</color>";
    }

    static string TeamLine()
    {
        int hp = 0, atk = 0, def = 0, n = 0;
        for (int i = 0; i < 3; i++) { var m = PlayerProfile.SlotMember(i); if (m == null) continue; hp += m.hp; atk += m.atk; def += m.def; n++; }
        return n == 0 ? "no cards in your build slots" : $"HP {hp:N0}, ATK {atk}, DEF {def} ({n}/3 slots)";
    }

    void SaveNow()
    {
        string where = BossConfig.SaveCurrent();
        status.text = where.StartsWith("Error") ? where : $"Saved {cfg.gates.Count} gates to {where}";
        SfxSynth.Play("buff", 0.5f);
    }

    void TestFight()
    {
        if (Gate == null) return;
        SaveNow();
        GameSession.StartBossBattle(sel + 1, true);
    }

    void ResetDefaults()
    {
        cfg.gates = BossConfig.Defaults().gates;
        sel = 0;
        RebuildList();
        LoadFields();
        Changed("Gates reset to the defaults (the top Mythic and God Hidden Location characters). SAVE to keep them.");
    }

    // ---------------------------------------------------------------- character picker

    void BuildPicker(Transform parent)
    {
        var dim = UIFactory.CreateImage(parent, "Picker", new Color(0, 0, 0, 0.75f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        picker = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.07f, 0.06f, 0.13f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 900));
        UIFactory.AddOutline(panel, new Color(0.7f, 0.45f, 1f, 0.8f), 3f);
        var title = UIFactory.CreateText(panel.transform, "Title", "PICK THE BOSS CHARACTER", 30, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(title.rectTransform, 30, 16, 470, 50);
        var add = Btn(panel.transform, "+ ADD CHARACTER", new Color(0.25f, 0.6f, 0.4f), 510, 16, 260, () => OpenNewCharacter(), 54);
        TooltipTrigger.Add(add.image, "Make a new boss character from any picture (gallery or your PC): name, anime and element.");
        pickerSearch = UIFactory.CreateInputField(panel.transform, "Search", "Search a name or an anime...", 24);
        UIFactory.TopLeft((RectTransform)pickerSearch.transform, 790, 16, 470, 54);
        pickerSearch.onValueChanged.AddListener(_ => FillPicker());
        Btn(panel.transform, "CLOSE", UIFactory.PanelLight, 1280, 16, 190, () => picker.SetActive(false), 54);
        var scroll = UIFactory.CreateScroll(panel.transform, "Scroll", true, out pickerContent);
        UIFactory.Stretch((RectTransform)scroll.transform, 20, 86, 20, 20);
        UIFactory.AddGrid(pickerContent, new Vector2(150, 210), new Vector2(10, 10), 6);
        BuildNewCharacterForm(dim.transform);
        picker.SetActive(false);
    }

    void BuildNewCharacterForm(Transform parent)
    {
        var dim = UIFactory.CreateImage(parent, "NewCharacter", new Color(0, 0, 0, 0.7f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        newCharForm = dim.gameObject;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.08f, 0.07f, 0.14f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 640));
        UIFactory.AddOutline(panel, new Color(0.4f, 0.9f, 0.6f, 0.8f), 3f);
        var pt = panel.transform;
        var title = UIFactory.CreateText(pt, "Title", "ADD A BOSS CHARACTER", 32, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(title.rectTransform, 16, 50, 20, 20);
        ncPreview = CardTile.Build(pt, "Preview", 300f, 420f, true);
        UIFactory.TopLeft((RectTransform)ncPreview.transform, 40, 90, 300, 420);
        const float x = 380;
        Text L(string t, float y) { var l = UIFactory.CreateText(pt, "L_" + t, t, 20, UIFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold); UIFactory.TopLeft(l.rectTransform, x, y, 180, 52); return l; }
        L("Name", 90);
        ncName = UIFactory.CreateInputField(pt, "Name", "Boss name", 24);
        UIFactory.TopLeft((RectTransform)ncName.transform, x + 190, 90, 490, 52);
        ncName.characterLimit = 24;
        ncName.onValueChanged.AddListener(_ => RefreshNewCharacter());
        L("Anime", 156);
        ncAnime = UIFactory.CreateInputField(pt, "Anime", "Series (optional)", 24);
        UIFactory.TopLeft((RectTransform)ncAnime.transform, x + 190, 156, 490, 52);
        L("Element", 222);
        var elements = ElementSystem.All.ToList();
        ncElement = UIFactory.CreateDropdown(pt, "Element", 20, 420f);
        UIFactory.TopLeft((RectTransform)ncElement.transform, x + 190, 222, 490, 52);
        ncElement.options = elements.Select(e => new Dropdown.OptionData(ElementSystem.DisplayName(e))).ToList();
        ncElement.onValueChanged.AddListener(_ => RefreshNewCharacter());
        L("Picture", 288);
        Btn(pt, "CHOOSE PICTURE", new Color(0.55f, 0.3f, 0.95f), x + 190, 288, 490, () => PickNewCharacterPicture(), 60);
        ncStatus = UIFactory.CreateText(pt, "Status", "", 19, UIFactory.TextDim, TextAnchor.UpperLeft);
        UIFactory.TopLeft(ncStatus.rectTransform, x, 366, 680, 140);
        ncStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
        Btn(pt, "ADD CHARACTER", new Color(0.25f, 0.6f, 0.4f), 290, 556, 250, () => SaveNewCharacter(), 64);
        Btn(pt, "CANCEL", UIFactory.PanelLight, 560, 556, 250, () => newCharForm.SetActive(false), 64);
        newCharForm.SetActive(false);
    }

    void OpenNewCharacter()
    {
        ncName.text = (pickerSearch.text ?? "").Trim();
        ncAnime.text = "";
        ncElement.value = 0;
        ncElement.RefreshShownValue();
        ncPicture = "";
        newCharForm.SetActive(true);
        newCharForm.transform.SetAsLastSibling();
        RefreshNewCharacter();
    }

    void PickNewCharacterPicture()
    {
        PicturePicker.Show(transform, "BOSS CHARACTER PICTURE", choice =>
        {
            if (this == null || choice == null) return;
            if (choice.galleryCard != null)
            {
                ncPicture = choice.GallerySource;
                if (ncName.text.Trim().Length == 0) ncName.text = choice.galleryCard.DisplayName;
                if (ncAnime.text.Trim().Length == 0) ncAnime.text = choice.galleryCard.anime ?? "";
            }
            else if (choice.png != null)
            {
                ncPicture = PictureStore.SaveContent(choice.png, choice.preview, ncName.text) ?? "";
                if (ncPicture.Length == 0) { ncStatus.text = "<color=#FF6B6B>Could not save the picture.</color>"; return; }
            }
            RefreshNewCharacter();
        });
    }

    void RefreshNewCharacter()
    {
        if (ncPreview == null) return;
        var el = ElementSystem.All[Mathf.Clamp(ncElement.value, 0, ElementSystem.All.Length - 1)];
        string name = ncName.text.Trim().Length > 0 ? ncName.text.Trim() : "New boss";
        var art = PlayerProfile.PictureCard("newchar", ncPicture, name, ncAnime.text, CardRarity.God, el);
        if (art != null) ncPreview.Set(art, CardRarity.God, el, "", "");
        else { ncPreview.SetEmpty("No picture"); }
        ncPreview.nameText.text = name;
        ncStatus.text = string.IsNullOrEmpty(ncPicture)
            ? "Choose a picture: the anime gallery or a PNG / JPG from your PC (cropped to the card shape)."
            : "The character is added to the picker (listed first). Gates you pick it for use its name, picture and element. SAVE the boss editor to keep it.";
    }

    void SaveNewCharacter()
    {
        string name = ncName.text.Trim();
        if (name.Length == 0) { ncStatus.text = "<color=#FF6B6B>Type a name.</color>"; return; }
        if (string.IsNullOrEmpty(ncPicture)) { ncStatus.text = "<color=#FF6B6B>Choose a picture first.</color>"; return; }
        if (cfg.characters == null) cfg.characters = new List<BossCharacter>();
        var bc = new BossCharacter
        {
            key = "c" + DateTime.Now.ToString("yyyyMMddHHmmss"),
            name = name,
            anime = ncAnime.text.Trim(),
            element = ElementSystem.All[Mathf.Clamp(ncElement.value, 0, ElementSystem.All.Length - 1)],
            picture = ncPicture,
        };
        cfg.characters.Insert(0, bc);
        newCharForm.SetActive(false);
        Pick(bc);
    }

    void DeleteCharacter(BossCharacter bc)
    {
        if (bc == null) return;
        cfg.characters.Remove(bc);
        foreach (var g in cfg.gates) if (g.cardKey == bc.GateKey && string.IsNullOrEmpty(g.picture)) g.picture = bc.picture; // gates keep the picture
        FillPicker();
        LoadFields();
        Changed($"{bc.name} removed from the picker (gates using it keep its picture). SAVE to keep it.");
    }

    // ---------------------------------------------------------------- picture

    /// <summary>Name of the gate's character (to know whether the boss name was left at the character's name).</summary>
    string CharacterName(BossGate g)
    {
        var bc = cfg.FindCharacter(g.cardKey);
        if (bc != null) return bc.name;
        var c = CardDatabase.Instance != null && !string.IsNullOrEmpty(g.cardKey) ? CardDatabase.Instance.FindByKey(g.cardKey) : null;
        return c != null ? c.DisplayName : null;
    }

    void ChangePicture()
    {
        var g = Gate;
        if (g == null) return;
        PicturePicker.Show(transform, "BOSS PICTURE", choice =>
        {
            if (this == null || choice == null || g == null) return;
            if (choice.galleryCard != null) g.picture = choice.GallerySource;
            else if (choice.png != null)
            {
                string src = PictureStore.SaveContent(choice.png, choice.preview, g.name);
                if (src == null) { status.text = "Could not save the picture."; return; }
                g.picture = src;
            }
            LoadFields();
            Changed("New picture for this boss. SAVE to keep it.");
        });
    }

    void ResetPicture()
    {
        var g = Gate;
        if (g == null) return;
        var bc = cfg.FindCharacter(g.cardKey);
        g.picture = bc != null ? bc.picture : "";
        LoadFields();
        Changed("The boss uses its character's picture again. SAVE to keep it.");
    }

    void OpenPicker()
    {
        if (Gate == null) return;
        picker.SetActive(true);
        picker.transform.SetAsLastSibling();
        FillPicker();
    }

    void FillPicker()
    {
        if (allCards == null)
        {
            var db = CardDatabase.Instance;
            allCards = db != null ? db.AllCharacterCards().OrderByDescending(c => c.rarity).ThenBy(c => c.DisplayName).ToList() : new List<Card>();
        }
        UIFactory.Clear(pickerContent);
        string q = (pickerSearch.text ?? "").Trim().ToLowerInvariant();
        int customShown = 0;
        foreach (var bc in (cfg.characters ?? new List<BossCharacter>()).ToList())
        {
            if (bc == null || (q.Length > 0 && !(bc.name ?? "").ToLowerInvariant().Contains(q) && !(bc.anime ?? "").ToLowerInvariant().Contains(q))) continue;
            var ch = bc;
            var art = PlayerProfile.PictureCard("char|" + bc.key, bc.picture, bc.name, bc.anime, CardRarity.God, bc.element);
            var tile = CardTile.Build(pickerContent, "Custom_" + bc.key, 150f, 210f, false);
            if (art != null) tile.Set(art, CardRarity.God, bc.element, "CUSTOM", "");
            else tile.SetEmpty("No picture");
            tile.nameText.text = bc.name;
            var b0 = tile.GetComponent<Button>();
            if (b0 == null) b0 = tile.gameObject.AddComponent<Button>();
            b0.onClick.AddListener(() => Pick(ch));
            var x = UIFactory.CreateButton(tile.transform, "Remove", "X", new Color(0.7f, 0.2f, 0.25f), 18);
            UIFactory.Place((RectTransform)x.transform, new Vector2(1f, 1f), new Vector2(-20, -20), new Vector2(36, 36));
            x.onClick.AddListener(() => DeleteCharacter(ch));
            TooltipTrigger.Add(x.image, "Remove this character from the picker (gates using it keep its picture).");
            TooltipTrigger.Add(tile.frame, $"<b>{bc.name}</b>\n{bc.anime}\nMade in the boss editor ({ElementSystem.DisplayName(bc.element)})");
            customShown++;
        }
        var matches = allCards.Where(c => q.Length == 0 || c.DisplayName.ToLowerInvariant().Contains(q) || (c.anime ?? "").ToLowerInvariant().Contains(q)).Take(120).ToList();
        foreach (var c in matches)
        {
            var card = c;
            var tile = CardTile.Build(pickerContent, c.Key, 150f, 210f, false);
            tile.Set(c, CardStats.RarityName(c.rarity), c.power.ToString());
            var b = tile.GetComponent<Button>();
            if (b == null) b = tile.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => Pick(card));
            TooltipTrigger.Add(tile.frame, $"<b>{c.DisplayName}</b>\n{c.anime}\n{CardStats.RarityName(c.rarity)} {ElementSystem.DisplayName(c.element)}");
        }
        if (matches.Count == 0 && customShown == 0)
        {
            var t = UIFactory.CreateText(pickerContent, "None", "No character matches.", 24, UIFactory.TextDim, TextAnchor.MiddleCenter);
            UIFactory.SetLayoutSize(t, 600, 80);
        }
    }

    void Pick(Card c)
    {
        var g = Gate;
        if (g == null || c == null) return;
        string old = CharacterName(g);
        if (string.IsNullOrWhiteSpace(g.name) || g.name == "Boss" || (old != null && g.name == old)) g.name = c.DisplayName;
        g.cardKey = c.Key;
        g.picture = "";
        g.element = c.element;
        picker.SetActive(false);
        LoadFields();
        Changed($"{c.DisplayName} picked (name, picture and element). SAVE to keep it.");
    }

    void Pick(BossCharacter bc)
    {
        var g = Gate;
        if (g == null || bc == null) return;
        string old = CharacterName(g);
        if (string.IsNullOrWhiteSpace(g.name) || g.name == "Boss" || (old != null && g.name == old)) g.name = bc.name;
        g.cardKey = bc.GateKey;
        g.picture = "";
        g.element = bc.element;
        picker.SetActive(false);
        LoadFields();
        Changed($"{bc.name} picked (name, picture and element). SAVE to keep it.");
    }
}
