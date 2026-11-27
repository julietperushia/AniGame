using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Higher or Lower: will the next character's POWER be higher or lower? Score = right guesses in a row (max 10).</summary>
public class HigherLowerGame : MinigameBase
{
    const int MaxStreak = 10;
    List<Card> deck;
    Card current, next;
    CardTile left, right;
    Text streakText, info;
    Button higher, lower;
    bool busy;

    public override string Id => MinigameConfig.HigherLower;
    public override string Help => $"Guess if the next character's POWER is higher or lower. One mistake ends the run (max {MaxStreak}).";

    protected override void Build()
    {
        deck = PictureCards().Where(c => c.power > 0).ToList();
        if (deck.Count < 2) deck = (CardDatabase.Instance != null ? CardDatabase.Instance.AllCharacterCards() : new List<Card>()).Where(c => c.power > 0).OrderBy(_ => Random.value).ToList();
        left = CardTile.Build(area, "Current", 300f, 420f, true);
        UIFactory.Place((RectTransform)left.transform, new Vector2(0.5f, 0.5f), new Vector2(-330, 10), new Vector2(300, 420));
        right = CardTile.Build(area, "Next", 300f, 420f, true);
        UIFactory.Place((RectTransform)right.transform, new Vector2(0.5f, 0.5f), new Vector2(330, 10), new Vector2(300, 420));
        streakText = AddText("Streak", "", 34, UIFactory.Gold, new Vector2(0, H / 2 - 40), new Vector2(800, 50));
        info = AddText("Info", "", 28, UIFactory.TextMain, new Vector2(0, 160), new Vector2(320, 120));
        info.horizontalOverflow = HorizontalWrapMode.Wrap;
        higher = AddButton("HIGHER", new Color(0.25f, 0.65f, 0.4f), new Vector2(0, 30), new Vector2(300, 90), () => Guess(true), 34);
        lower = AddButton("LOWER", new Color(0.8f, 0.3f, 0.35f), new Vector2(0, -80), new Vector2(300, 90), () => Guess(false), 34);
        if (deck.Count < 2) { info.text = "Not enough cards."; Finish(0); return; }
        current = Draw(null);
        next = Draw(current);
        ShowCards(false);
    }

    Card Draw(Card notLike)
    {
        for (int i = 0; i < 40; i++)
        {
            var c = Pick(deck);
            if (notLike == null || (c != notLike && c.power != notLike.power)) return c;
        }
        return deck.First(c => c != notLike);
    }

    void ShowCards(bool reveal)
    {
        left.Set(current, CardStats.RarityName(current.rarity), $"POWER {current.power}");
        if (reveal) { right.Set(next, CardStats.RarityName(next.rarity), $"POWER {next.power}"); right.SetLocked(false, null); }
        else { right.SetEmpty("NEXT"); right.SetLocked(true, "?"); }
        streakText.text = $"STREAK {score}/{MaxStreak}";
        info.text = reveal ? "" : $"<b>{current.DisplayName}</b>\nPOWER <color=#FFD54F>{current.power}</color>";
    }

    void Guess(bool up)
    {
        if (busy || over) return;
        busy = true;
        bool ok = up ? next.power > current.power : next.power < current.power;
        ShowCards(true);
        info.text = ok ? "<color=#3DDC84>RIGHT!</color>" : $"<color=#FF5A5A>WRONG</color>\n{next.DisplayName}: {next.power}";
        SfxSynth.Play(ok ? "coin" : "fail", 0.5f);
        if (ok) score++;
        streakText.text = $"STREAK {score}/{MaxStreak}";
        StartCoroutine(After(ok));
    }

    IEnumerator After(bool ok)
    {
        yield return new WaitForSeconds(ok ? 0.9f : 1.4f);
        if (!ok || score >= MaxStreak) { Finish(score); yield break; }
        current = next;
        next = Draw(current);
        ShowCards(false);
        busy = false;
    }
}
