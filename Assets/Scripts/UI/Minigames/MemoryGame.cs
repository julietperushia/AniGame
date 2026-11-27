using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Memory Match: find the 6 pairs of characters before the time runs out. Score = pairs found.</summary>
public class MemoryGame : MinigameBase
{
    const int Pairs = 6;
    const float TimeLimit = 45f;
    readonly List<CardTile> tiles = new List<CardTile>();
    readonly List<Card> faces = new List<Card>();
    readonly HashSet<int> found = new HashSet<int>();
    int first = -1;
    bool busy;
    float timeLeft = TimeLimit;
    Text timerText, pairsText;

    public override string Id => MinigameConfig.Memory;
    public override string Help => $"Flip two cards at a time and find all {Pairs} pairs in {TimeLimit:0} seconds.";

    protected override void Build()
    {
        var pics = PictureCards().GroupBy(c => c.DisplayName).Select(g => g.First()).Take(Pairs).ToList();
        foreach (var c in pics) { faces.Add(c); faces.Add(c); }
        Shuffle(faces);
        timerText = AddText("Timer", "", 30, UIFactory.Gold, new Vector2(-W / 2 + 160, H / 2 - 30), new Vector2(300, 44), TextAnchor.MiddleLeft);
        pairsText = AddText("Pairs", "", 30, UIFactory.Good, new Vector2(W / 2 - 160, H / 2 - 30), new Vector2(300, 44), TextAnchor.MiddleRight);
        const float w = 150f, h = 210f;
        int cols = 6;
        for (int i = 0; i < faces.Count; i++)
        {
            int index = i;
            var t = CardTile.Build(area, "Tile" + i, w, h, false);
            float x = (i % cols - (cols - 1) / 2f) * (w + 18);
            float y = 120 - (i / cols) * (h + 24);
            UIFactory.Place((RectTransform)t.transform, new Vector2(0.5f, 0.5f), new Vector2(x, y - 20), new Vector2(w, h));
            t.button.onClick.AddListener(() => Flip(index));
            tiles.Add(t);
            Hide(i);
        }
        if (faces.Count < 4) { pairsText.text = "Not enough pictures."; Finish(0); return; }
        Refresh();
    }

    void Hide(int i)
    {
        tiles[i].SetEmpty("");          // no picture while face down (no peeking through the overlay)
        tiles[i].SetLocked(true, "?");
    }

    void Show(int i)
    {
        tiles[i].Set(faces[i], "", "");
        tiles[i].SetLocked(false, null);
    }

    void Refresh()
    {
        timerText.text = $"TIME {Mathf.CeilToInt(Mathf.Max(0f, timeLeft))}";
        pairsText.text = $"PAIRS {found.Count / 2}/{faces.Count / 2}";
    }

    void Update()
    {
        if (over) return;
        timeLeft -= Time.deltaTime;
        Refresh();
        if (timeLeft <= 0f) Finish(found.Count / 2);
    }

    void Flip(int i)
    {
        if (over || busy || found.Contains(i) || i == first) return;
        Show(i);
        SfxSynth.Play("click", 0.4f);
        if (first < 0) { first = i; return; }
        int a = first;
        first = -1;
        if (faces[a] == faces[i])
        {
            found.Add(a); found.Add(i);
            score = found.Count / 2;
            SfxSynth.Play("coin", 0.5f);
            Refresh();
            if (found.Count >= faces.Count) StartCoroutine(Done());
        }
        else StartCoroutine(Unflip(a, i));
    }

    IEnumerator Unflip(int a, int b)
    {
        busy = true;
        yield return new WaitForSeconds(0.7f);
        if (!over) { Hide(a); Hide(b); }
        busy = false;
    }

    IEnumerator Done()
    {
        UIFx.BurstAtPointer(UIFactory.Gold, 1.2f);
        yield return new WaitForSeconds(0.8f);
        Finish(found.Count / 2);
    }
}
