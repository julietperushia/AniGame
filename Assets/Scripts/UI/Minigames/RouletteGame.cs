using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Roulette: one spin of a prize wheel. The segments and how likely each is are the prize lines in Test Mode.</summary>
public class RouletteGame : MinigameBase
{
    static readonly Color[] Palette =
    {
        new Color(0.9f, 0.25f, 0.35f), new Color(0.25f, 0.45f, 0.9f), new Color(0.95f, 0.65f, 0.15f), new Color(0.3f, 0.7f, 0.4f),
        new Color(0.6f, 0.3f, 0.85f), new Color(0.15f, 0.65f, 0.75f), new Color(0.85f, 0.4f, 0.6f), new Color(0.45f, 0.45f, 0.55f),
    };
    RectTransform wheel;
    Button spin;
    Text info;
    List<PrizeTier> segments;
    bool spinning;

    public override string Id => MinigameConfig.Roulette;
    public override string Help => "One spin of the wheel. Whatever it lands on is yours.";

    protected override void Build()
    {
        var def = MinigameConfig.Current.Find(Id);
        segments = def != null ? def.tiers : new List<PrizeTier>();
        int n = Mathf.Max(1, segments.Count);
        float size = Mathf.Min(H - 60, 640f);
        var rim = UIFactory.CreateImage(area, "Rim", UIFactory.Gold, UITheme.Instance != null ? UITheme.Instance.circle : null);
        UIFactory.Place(rim.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-260, -10), new Vector2(size + 24, size + 24));
        wheel = UIFactory.CreateRect("Wheel", area);
        UIFactory.Place(wheel, new Vector2(0.5f, 0.5f), new Vector2(-260, -10), new Vector2(size, size));
        var circle = UITheme.Instance != null ? UITheme.Instance.circle : null;
        for (int i = 0; i < n; i++)
        {
            var seg = UIFactory.CreateImage(wheel, "Seg" + i, Palette[i % Palette.Length], circle);
            UIFactory.Stretch(seg.rectTransform);
            seg.type = Image.Type.Filled;
            seg.fillMethod = Image.FillMethod.Radial360;
            seg.fillOrigin = (int)Image.Origin360.Top;
            seg.fillClockwise = true;
            seg.fillAmount = 1f / n;
            seg.rectTransform.localRotation = Quaternion.Euler(0, 0, -i * 360f / n);
            // label in the middle of the segment, along its radius
            float mid = (i + 0.5f) * 360f / n;
            var holder = UIFactory.CreateRect("LabelHolder" + i, wheel);
            UIFactory.Stretch(holder);
            holder.localRotation = Quaternion.Euler(0, 0, -mid);
            string text = i < segments.Count ? (string.IsNullOrEmpty(segments[i].label) ? segments[i].prize.Label : segments[i].label) : "?";
            var t = UIFactory.CreateText(holder, "Label", text, 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, size * 0.32f), new Vector2(size * 0.3f, 60));
            t.rectTransform.localRotation = Quaternion.Euler(0, 0, 90f);
            UIFactory.BestFit(t, 10);
            UIFactory.AddOutline(t, new Color(0f, 0f, 0f, 0.6f), 2f);
        }
        var hubDot = UIFactory.CreateImage(area, "Hub", new Color(0.1f, 0.08f, 0.15f), circle);
        UIFactory.Place(hubDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-260, -10), new Vector2(90, 90));
        var pointer = UIFactory.CreateImage(area, "Pointer", Color.white);
        UIFactory.Place(pointer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-260, -10 + size / 2 + 6), new Vector2(40, 40));
        pointer.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
        UIFactory.AddOutline(pointer, new Color(0.4f, 0f, 0.1f), 3f);

        var title = AddText("Title", "ROULETTE", 56, UIFactory.Gold, new Vector2(380, 230), new Vector2(560, 80));
        UIFactory.AddOutline(title, new Color(0.3f, 0f, 0.1f), 3f);
        info = AddText("Info", "Spin the wheel!", 30, UIFactory.TextMain, new Vector2(380, 60), new Vector2(560, 160));
        info.horizontalOverflow = HorizontalWrapMode.Wrap;
        spin = AddButton("SPIN!", new Color(1f, 0.35f, 0.62f), new Vector2(380, -140), new Vector2(340, 90), () => StartCoroutine(Spin()), 40);
        if (segments.Count == 0) { info.text = "No segments: add prize lines in Test Mode."; spin.interactable = false; }
    }

    IEnumerator Spin()
    {
        if (spinning) yield break;
        spinning = true;
        spin.interactable = false;
        int n = segments.Count;
        int index = MinigameConfig.Current.PickWeighted(Id);
        float seg = 360f / n;
        float target = 360f * 6f + (index + 0.5f) * seg + Random.Range(-0.35f, 0.35f) * seg;
        float start = wheel.localEulerAngles.z, t = 0f, dur = 3.8f;
        SfxSynth.Play("charge", 0.5f);
        float lastTick = start;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / dur), 3f); // ease out
            float z = Mathf.Lerp(start, target, k);
            wheel.localRotation = Quaternion.Euler(0, 0, z);
            if (z - lastTick >= seg) { lastTick = z; SfxSynth.Play("click", 0.2f, 1.5f); }
            yield return null;
        }
        wheel.localRotation = Quaternion.Euler(0, 0, target);
        var won = segments[index];
        info.text = $"<color=#FFD54F>{(string.IsNullOrEmpty(won.label) ? won.prize.Label : won.label)}!</color>";
        UIFx.BurstAtPointer(UIFactory.Gold, 1.2f);
        SfxSynth.Play("sparkle", 0.6f);
        score = index;
        yield return new WaitForSeconds(1.4f);
        Finish(index);
    }

    public override void Quit()
    {
        if (spinning) return;
        over = true; // closed without spinning: nothing won (the cooldown has started)
        if (hub != null) hub.OnFinished(Id, -1);
    }
}
