using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Lucky 777: one spin of 3 reels. Pair = 1, three of a kind = 2, three 7s = 3 (jackpot).</summary>
public class SlotsGame : MinigameBase
{
    // symbol 0 is the 7; the others are element icons
    static readonly CardElement[] Elements = { CardElement.Fire, CardElement.Water, CardElement.Tree, CardElement.Light, CardElement.Dark };
    static readonly int[] Weights = { 2, 3, 3, 3, 3, 3 };
    readonly Image[] icons = new Image[3];
    readonly Text[] sevens = new Text[3];
    readonly int[] result = new int[3];
    Button spin;
    Text info;

    public override string Id => MinigameConfig.Slots;
    public override string Help => "One spin. A pair, three of a kind, or three 7s for the jackpot!";

    protected override void Build()
    {
        var machine = UIFactory.CreatePanel(area, "Machine", new Color(0.35f, 0.08f, 0.12f, 1f), false);
        UIFactory.Place(machine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(900, 400));
        UIFactory.AddOutline(machine, UIFactory.Gold, 4f);
        for (int i = 0; i < 3; i++)
        {
            var reel = UIFactory.CreatePanel(machine.transform, "Reel" + i, new Color(0.96f, 0.95f, 0.9f, 1f), false);
            UIFactory.Place(reel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 280, 0), new Vector2(250, 300));
            icons[i] = UIFactory.CreateImage(reel.transform, "Icon", Color.white);
            icons[i].preserveAspect = true;
            UIFactory.Place(icons[i].rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 180));
            sevens[i] = UIFactory.CreateText(reel.transform, "Seven", "7", 190, new Color(0.85f, 0.1f, 0.15f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(sevens[i].rectTransform);
            UIFactory.AddOutline(sevens[i], new Color(0.4f, 0f, 0f), 3f);
            ShowSymbol(i, Random.Range(0, 6));
        }
        var title = AddText("Title", "LUCKY 777", 56, UIFactory.Gold, new Vector2(0, 300), new Vector2(800, 80));
        UIFactory.AddOutline(title, new Color(0.4f, 0f, 0.1f), 3f);
        info = AddText("Info", "", 30, UIFactory.TextMain, new Vector2(0, -230), new Vector2(1200, 50));
        spin = AddButton("SPIN!", new Color(1f, 0.35f, 0.62f), new Vector2(0, -310), new Vector2(340, 90), () => StartCoroutine(Spin()), 40);
    }

    void ShowSymbol(int reel, int symbol)
    {
        bool seven = symbol == 0;
        sevens[reel].gameObject.SetActive(seven);
        icons[reel].gameObject.SetActive(!seven);
        if (!seven)
        {
            var e = Elements[symbol - 1];
            var theme = UITheme.Instance;
            var s = theme != null ? theme.ElementIcon(e) : null;
            icons[reel].sprite = s != null ? s : (theme != null ? theme.circle : null);
            icons[reel].color = s != null ? Color.white : ElementSystem.GetColor(e);
        }
    }

    static int Roll()
    {
        int total = 0;
        foreach (var w in Weights) total += w;
        int r = Random.Range(0, total);
        for (int i = 0; i < Weights.Length; i++) { r -= Weights[i]; if (r < 0) return i; }
        return Weights.Length - 1;
    }

    IEnumerator Spin()
    {
        spin.interactable = false;
        for (int i = 0; i < 3; i++) result[i] = Roll();
        SfxSynth.Play("charge", 0.5f);
        float t = 0f;
        float[] stops = { 1.0f, 1.5f, 2.0f };
        var stopped = new bool[3];
        while (t < stops[2] + 0.01f)
        {
            for (int i = 0; i < 3; i++)
            {
                if (stopped[i]) continue;
                if (t >= stops[i]) { stopped[i] = true; ShowSymbol(i, result[i]); SfxSynth.Play("impact", 0.35f, 1.4f); }
                else if (Random.value < 0.5f) ShowSymbol(i, Random.Range(0, 6));
            }
            yield return new WaitForSeconds(0.05f);
            t += 0.05f;
        }
        int s = result[0] == 0 && result[1] == 0 && result[2] == 0 ? 3
              : result[0] == result[1] && result[1] == result[2] ? 2
              : result[0] == result[1] || result[1] == result[2] || result[0] == result[2] ? 1 : 0;
        info.text = s == 3 ? "<color=#FFD54F>777 JACKPOT!!!</color>" : s == 2 ? "THREE OF A KIND!" : s == 1 ? "A PAIR!" : "No luck this time...";
        if (s > 0) UIFx.BurstAtPointer(UIFactory.Gold, s);
        SfxSynth.Play(s > 0 ? "sparkle" : "fail", 0.6f);
        score = s;
        yield return new WaitForSeconds(1.4f);
        Finish(s);
    }

    public override void Quit()
    {
        if (!spin.interactable) return; // let a spin finish
        Finish(0);
    }
}
