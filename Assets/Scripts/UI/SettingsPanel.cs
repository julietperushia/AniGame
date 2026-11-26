using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings (main menu > SETTINGS): music and sound effect volume, graphics quality, full screen,
/// and the save data export / import. Built at runtime the first time it opens.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    Slider musicSlider, sfxSlider;
    Text musicValue, sfxValue;
    Button[] qualityButtons;
    Button fullScreenButton;
    Text info;
    Action openSaveData;
    bool dirty;

    public static SettingsPanel Open(Transform canvas, Action openSaveData)
    {
        var p = canvas.GetComponentInChildren<SettingsPanel>(true);
        if (p == null) p = Build(canvas);
        p.openSaveData = openSaveData;
        p.gameObject.SetActive(true);
        p.transform.SetAsLastSibling();
        p.Refresh();
        return p;
    }

    static SettingsPanel Build(Transform canvas)
    {
        var dim = UIFactory.CreateImage(canvas, "SettingsPanel", new Color(0f, 0f, 0f, 0.8f), null, false, true);
        UIFactory.Stretch(dim.rectTransform);
        var p = dim.gameObject.AddComponent<SettingsPanel>();
        dim.gameObject.AddComponent<PromptKeys>().onEscape = p.Close;

        var panel = UIFactory.CreatePanel(dim.transform, "Panel", new Color(0.07f, 0.08f, 0.15f, 1f), true);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 800));
        UIFactory.AddOutline(panel, UIFactory.WithAlpha(UIFactory.Accent, 0.8f), 3f);
        var pt = panel.transform;

        var title = UIFactory.CreateText(pt, "Title", "SETTINGS", 48, UIFactory.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.TopStrip(title.rectTransform, 22, 64, 44, 44);
        UIFactory.AddShadow(title, UIFactory.ShadowColor, 3f);
        var x = UIFactory.CreateButton(pt, "CloseX", "X", new Color(0.75f, 0.2f, 0.3f), 30);
        UIFactory.Place((RectTransform)x.transform, new Vector2(1f, 1f), new Vector2(-28, -28), new Vector2(64, 64));
        x.onClick.AddListener(p.Close);

        Text Header(string text, float y)
        {
            var t = UIFactory.CreateText(pt, text, text, 24, new Color(0.6f, 0.75f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.TopLeft(t.rectTransform, 44, y, 900, 36);
            return t;
        }
        Text Label(string text, float y)
        {
            var t = UIFactory.CreateText(pt, text, text, 22, UIFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.TopLeft(t.rectTransform, 60, y, 230, 44);
            return t;
        }
        Button Btn(string label, Color c, float bx, float by, float w, float h, Action click, int size = 22)
        {
            var b = UIFactory.CreateButton(pt, label, label, c, size);
            UIFactory.TopLeft((RectTransform)b.transform, bx, by, w, h);
            b.onClick.AddListener(() => click());
            return b;
        }

        // ---- sound
        Header("SOUND", 104);
        Label("Music", 150);
        p.musicSlider = MakeSlider(pt, 290, 150, 520);
        p.musicValue = UIFactory.CreateText(pt, "MusicValue", "", 22, UIFactory.TextMain, TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.TopLeft(p.musicValue.rectTransform, 830, 150, 110, 44);
        Label("Sound effects", 206);
        p.sfxSlider = MakeSlider(pt, 290, 206, 520);
        p.sfxValue = UIFactory.CreateText(pt, "SfxValue", "", 22, UIFactory.TextMain, TextAnchor.MiddleRight, FontStyle.Bold);
        UIFactory.TopLeft(p.sfxValue.rectTransform, 830, 206, 110, 44);
        p.musicSlider.onValueChanged.AddListener(v => { GameSettings.MusicVolume = v; p.dirty = true; p.RefreshValues(); });
        p.sfxSlider.onValueChanged.AddListener(v => { GameSettings.SfxVolume = v; p.dirty = true; p.RefreshValues(); });
        var test = Btn("TEST SOUND", UIFactory.PanelLight, 60, 262, 220, 50, () => SfxSynth.Play("coin"), 18);
        TooltipTrigger.Add(test.image, "Plays a sound at the current volume.");

        // ---- graphics
        Header("GRAPHICS", 336);
        Label("Quality", 382);
        var names = QualitySettings.names;
        p.qualityButtons = new Button[names.Length];
        float bw = Mathf.Min(150f, (900f - 230f) / Mathf.Max(1, names.Length) - 8f);
        for (int i = 0; i < names.Length; i++)
        {
            int q = i;
            p.qualityButtons[i] = Btn(names[i].ToUpperInvariant(), UIFactory.PanelLight, 290 + i * (bw + 8), 382, bw, 50, () => { GameSettings.Quality = q; p.Refresh(); SfxSynth.Play("click", 0.5f); }, 16);
        }
        Label("Full screen", 448);
        p.fullScreenButton = Btn("FULL SCREEN", UIFactory.PanelLight, 290, 448, 260, 50, () => { GameSettings.FullScreen = !GameSettings.FullScreen; p.Invoke(nameof(Refresh), 0.1f); }, 18);
        var gfxNote = UIFactory.CreateText(pt, "GfxNote",
            "Higher quality keeps every picture at full resolution." +
            (Application.isEditor ? " In the Unity editor the Game view also matters: untick \"Low Resolution Aspect Ratios\" and keep Scale at 1x." : ""),
            16, UIFactory.TextDim, TextAnchor.UpperLeft);
        UIFactory.TopLeft(gfxNote.rectTransform, 60, 510, 880, 48);

        // ---- save data
        Header("SAVE DATA", 580);
        Btn("EXPORT / IMPORT SAVE", new Color(0.2f, 0.45f, 0.4f), 60, 626, 400, 60, () => p.openSaveData?.Invoke(), 22);
        var saveNote = UIFactory.CreateText(pt, "SaveNote", "Move your progress to another PC: export one file here, import it there.", 17, UIFactory.TextDim, TextAnchor.MiddleLeft);
        UIFactory.TopLeft(saveNote.rectTransform, 480, 626, 470, 60);

        p.info = UIFactory.CreateText(pt, "Info", "", 18, new Color(0.55f, 1f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(p.info.rectTransform, 700, 30, 40, 40);
        var close = Btn("CLOSE", UIFactory.Accent, 370, 730, 260, 56, p.Close, 24);
        _ = close;
        return p;
    }

    /// <summary>A plain horizontal slider (0..1).</summary>
    static Slider MakeSlider(Transform parent, float x, float y, float w)
    {
        var root = UIFactory.CreateRect("Slider", parent);
        UIFactory.TopLeft(root, x, y, w, 44);
        var bg = UIFactory.CreatePanel(root, "Background", new Color(0.03f, 0.03f, 0.08f, 1f), true);
        UIFactory.Stretch(bg.rectTransform, 0, 14, 0, 14);
        var fillArea = UIFactory.CreateRect("Fill Area", root);
        UIFactory.Stretch(fillArea, 0, 14, 0, 14);
        var fill = UIFactory.CreatePanel(fillArea, "Fill", UIFactory.Accent, false);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        var handleArea = UIFactory.CreateRect("Handle Slide Area", root);
        UIFactory.Stretch(handleArea, 14, 0, 14, 0);
        var theme = UIFactory.Theme;
        var handle = UIFactory.CreateImage(handleArea, "Handle", Color.white, theme != null ? theme.circle : null, false, true);
        handle.rectTransform.anchorMin = Vector2.zero;
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(30f, 0f);
        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }

    void Refresh()
    {
        musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        RefreshValues();
        int q = GameSettings.Quality;
        for (int i = 0; i < qualityButtons.Length; i++)
        {
            bool on = i == q;
            qualityButtons[i].image.color = on ? UIFactory.Accent : UIFactory.PanelLight;
        }
        UIFactory.ButtonLabel(fullScreenButton).text = GameSettings.FullScreen ? "FULL SCREEN: ON" : "FULL SCREEN: OFF";
        fullScreenButton.image.color = GameSettings.FullScreen ? new Color(0.15f, 0.4f, 0.28f) : UIFactory.PanelLight;
        info.text = Application.isEditor ? "<color=#9AA4B5>Full screen only changes in a build.</color>" : "";
    }

    void RefreshValues()
    {
        musicValue.text = $"{Mathf.RoundToInt(GameSettings.MusicVolume * 100f)}%";
        sfxValue.text = $"{Mathf.RoundToInt(GameSettings.SfxVolume * 100f)}%";
    }

    void Close()
    {
        if (dirty) { PlayerProfile.Save(); dirty = false; }
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (dirty) { PlayerProfile.Save(); dirty = false; }
    }
}
