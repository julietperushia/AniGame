using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Test Mode (main menu, editor and development builds only): open every LOC up to 100, get a God card,
/// add resources, unlock every buff and change the HP multiplier on this PC. Built at runtime.
/// </summary>
public class TestModePanel : MonoBehaviour
{
    Text info;
    Text hpText;
    Action onChanged;

    public static void AddButton(Transform canvas, Action onChanged)
    {
        if (!PlayerProfile.TestModeAvailable || canvas == null) return;
        var open = UIFactory.CreateButton(canvas, "TestModeButton", "TEST MODE", new Color(0.75f, 0.2f, 0.25f), 22);
        UIFactory.Place((RectTransform)open.transform, new Vector2(1f, 0f), new Vector2(-140, 40), new Vector2(240, 60));
        TooltipTrigger.Add(open.image, "<b>Test Mode</b>\nOnly in the Unity editor and development builds: open every LOC, get a God card, resources, buffs and change the HP multiplier.");
        TestModePanel panel = null;
        open.onClick.AddListener(() =>
        {
            if (panel == null) panel = Build(canvas, onChanged);
            panel.gameObject.SetActive(true);
            panel.transform.SetAsLastSibling();
            panel.Refresh("");
        });
    }

    static TestModePanel Build(Transform canvas, Action onChanged)
    {
        var dim = UIFactory.CreateImage(canvas, "TestModePanel", new Color(0, 0, 0, 0.8f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        var p = dim.gameObject.AddComponent<TestModePanel>();
        p.onChanged = onChanged;
        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.12f, 0.05f, 0.08f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 960));
        UIFactory.AddOutline(panel, new Color(1f, 0.35f, 0.4f, 0.7f), 3f);
        var title = UIFactory.CreateText(panel.transform, "Title", "TEST MODE", 46, new Color(1f, 0.5f, 0.55f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(title.rectTransform, 14, 60, 20, 20);
        var note = UIFactory.CreateText(panel.transform, "Note", "Only in the Unity editor and development builds. Changes are saved like normal play.", 18, UIFactory.TextDim, TextAnchor.MiddleCenter);
        UIFactory.TopStrip(note.rectTransform, 74, 28, 20, 20);

        Button Btn(string label, Color c, float x, float y, float w, Action click)
        {
            var b = UIFactory.CreateButton(panel.transform, label, label, c, 22);
            UIFactory.TopLeft((RectTransform)b.transform, x, y, w, 64);
            b.onClick.AddListener(() => click());
            return b;
        }

        Btn("UNLOCK ALL 100 LOCs", new Color(0.55f, 0.3f, 0.95f), 40, 120, 440, () =>
        {
            p.Done(PlayerProfile.TestUnlockAllLocations());
        });
        Btn("GIVE ME A GOD CARD", new Color(0.85f, 0.65f, 0.15f), 500, 120, 440, () =>
        {
            var card = PlayerProfile.TestGiveGodCard();
            p.Done(card != null ? $"God card #{card.uid} added and put in your main slot ({card.build.talent} + {card.build.talent2})" : "Could not add a card");
            UIFx.BurstAtPointer(new Color(1f, 0.85f, 0.4f), 1.2f);
        });
        Btn("+ RESOURCES", new Color(0.2f, 0.6f, 0.4f), 40, 200, 440, () =>
        {
            PlayerProfile.TestAddResources();
            p.Done("+1,000,000 gold, +50,000 diamonds, +200 Fusion Orbs, +50 Element/Talent Orbs, +20 God Orbs, full stamina");
        });
        Btn("UNLOCK ALL BUFFS", new Color(0.3f, 0.45f, 0.85f), 500, 200, 440, () =>
        {
            foreach (var b in Buffs.All) if (!PlayerProfile.Data.buffs.Contains(b.id)) PlayerProfile.Data.buffs.Add(b.id);
            PlayerProfile.Save();
            p.Done("Every permanent buff is now active");
        });

        var hpTitle = UIFactory.CreateText(panel.transform, "HpTitle", "HP MULTIPLIER  (HP = HP stat x this; damage stays the same, so battles get longer)", 20, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopLeft(hpTitle.rectTransform, 40, 290, 900, 32);
        p.hpText = UIFactory.CreateText(panel.transform, "HpValue", "", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopLeft(p.hpText.rectTransform, 40, 330, 900, 50);
        int[] presets = { 10, 20, 30, 50, 100 };
        for (int i = 0; i < presets.Length; i++)
        {
            int v = presets[i];
            Btn($"x{v}", UIFactory.PanelLight, 40 + i * 150, 392, 140, () => { GameRules.SetHpMultiplierOverride(v); p.Done($"HP multiplier set to x{v} on this PC"); });
        }
        Btn("-5", UIFactory.PanelLight, 790, 392, 70, () => { GameRules.SetHpMultiplierOverride(Mathf.Max(1, GameRules.HpMultiplier - 5)); p.Done(""); });
        Btn("+5", UIFactory.PanelLight, 870, 392, 70, () => { GameRules.SetHpMultiplierOverride(GameRules.HpMultiplier + 5); p.Done(""); });
        Btn("USE GAMEBALANCE.ASSET HP", new Color(0.35f, 0.3f, 0.45f), 40, 470, 440, () => { GameRules.SetHpMultiplierOverride(0); p.Done("HP multiplier override removed"); });
        var sim = Btn("3v3 SIMULATOR", new Color(0.85f, 0.3f, 0.45f), 500, 470, 440, () => TeamSimPanel.Open(canvas));
        var matrix = Btn("1v1 TALENT MATRIX", new Color(0.85f, 0.45f, 0.2f), 40, 550, 440, () => TalentMatrixPanel.Open(canvas));
        TooltipTrigger.Add(matrix.image, "<b>1v1 Talent Matrix</b>\nEvery talent fights every other talent on identical cards. Shows a ranking, each talent's matchups and the first battle log of every pair, and exports CSV files.");
        var hidden = Btn("HIDDEN LOCATION SIM", new Color(0.55f, 0.25f, 0.75f), 500, 550, 440, () => HiddenSimPanel.Open(canvas));
        Button testHidden = null;
        testHidden = Btn(PlayerProfile.HiddenTestMode ? "3v3 TEST (HIDDEN + BOSS): ON" : "3v3 TEST (HIDDEN + BOSS): OFF", new Color(0.35f, 0.25f, 0.5f), 40, 630, 900, () =>
        {
            PlayerProfile.HiddenTestMode = !PlayerProfile.HiddenTestMode;
            UIFactory.ButtonLabel(testHidden).text = PlayerProfile.HiddenTestMode ? "3v3 TEST (HIDDEN + BOSS): ON" : "3v3 TEST (HIDDEN + BOSS): OFF";
            p.Done(PlayerProfile.HiddenTestMode ? "3v3 test: Hidden Location and Boss open, every floor and gate open, empty slots use test God cards, no stamina, no rewards" : "3v3 test off");
        });
        var bossEd = Btn("BOSS EDITOR", new Color(0.8f, 0.2f, 0.28f), 40, 710, 440, () => BossEditorPanel.Open(canvas));
        TooltipTrigger.Add(bossEd.image, "<b>Boss editor</b>\nAdd, remove and order the boss gates. Each boss takes its name and picture from a character of the game; set its rarity, element, talents, stats and rewards (God Orbs, materials, gold), then SAVE or TEST FIGHT.");
        var mini = Btn("MINIGAME PRIZES", new Color(0.15f, 0.55f, 0.75f), 500, 710, 440, () => MinigameConfigPanel.Open(canvas));
        TooltipTrigger.Add(mini.image, "<b>Minigame prizes</b>\nThe cooldown between games and every game's prize lines. RESET COOLDOWN lets you play again right away.");
        testHidden.name = "HiddenTestToggle";
        TooltipTrigger.Add(testHidden.image, "<b>Hidden Location test</b>\nOpens the Hidden Location and every floor without changing your save. Empty build slots are filled with max-stat God test cards. Test fights cost no stamina and give no rewards or progress.");
        TooltipTrigger.Add(hidden.image, "<b>Hidden Location simulator</b>\nRuns every Hidden Location floor with teams of 3 max-stat God cards and shows how often each floor is cleared.");
        TooltipTrigger.Add(sim.image, "<b>3v3 Simulator</b>\nRun hundreds of headless 3v3 battles with the cards you pick (or random ones) and see which talents win the most.");

        p.info = UIFactory.CreateText(panel.transform, "Info", "", 20, new Color(0.55f, 1f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(p.info.rectTransform, 786, 70, 40, 40);
        UIFactory.BestFit(p.info, 12);
        var close = UIFactory.CreateButton(panel.transform, "Close", "CLOSE", UIFactory.PanelLight, 24);
        UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(260, 60));
        close.onClick.AddListener(() => dim.gameObject.SetActive(false));
        return p;
    }

    void Done(string message)
    {
        Refresh(message);
        SfxSynth.Play("buff", 0.6f);
        onChanged?.Invoke();
    }

    void Refresh(string message)
    {
        if (!string.IsNullOrEmpty(message)) info.text = message;
        else if (info.text == null) info.text = "";
        hpText.text = $"x{GameRules.HpMultiplier}" + (GameRules.HpMultiplierOverridden ? "  <size=20><color=#FFB23D>(test override)</color></size>" : "  <size=20><color=#9AA4B5>(GameBalance.asset)</color></size>") +
                      $"   <size=20><color=#9AA4B5>round limit {GameRules.MaxRounds}, enrage from round {GameRules.EnrageStartRound}</color></size>";
    }
}
