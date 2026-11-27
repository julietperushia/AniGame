using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Base of every minigame. The hub (MinigamesScreen) gives it a play area; the game builds its own UI there and calls
/// Finish(score) once. The prize lines in MinigameConfig turn the score into a prize. Closing a game early keeps the score so far.
/// </summary>
public abstract class MinigameBase : MonoBehaviour
{
    protected RectTransform area;
    protected MinigamesScreen hub;
    protected int score;
    protected bool over;

    public abstract string Id { get; }
    /// <summary>One line shown under the title.</summary>
    public abstract string Help { get; }

    public void Begin(MinigamesScreen hub, RectTransform area)
    {
        this.hub = hub;
        this.area = area;
        Build();
    }

    protected abstract void Build();

    protected void Finish(int finalScore)
    {
        if (over) return;
        over = true;
        score = finalScore;
        if (hub != null) hub.OnFinished(Id, score);
    }

    /// <summary>The close button: the game ends with the score reached so far.</summary>
    public virtual void Quit() => Finish(score);

    protected float W => area.rect.width;
    protected float H => area.rect.height;

    protected Text AddText(string name, string text, int size, Color color, Vector2 pos, Vector2 sizeDelta, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
    {
        var t = UIFactory.CreateText(area, name, text, size, color, anchor, style);
        UIFactory.Place(t.rectTransform, new Vector2(0.5f, 0.5f), pos, sizeDelta);
        return t;
    }

    protected Button AddButton(string label, Color color, Vector2 pos, Vector2 size, System.Action click, int fontSize = 26)
    {
        var b = UIFactory.CreateButton(area, label, label, color, fontSize);
        UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), pos, size);
        b.onClick.AddListener(() => click());
        return b;
    }

    /// <summary>Space, a mouse click or a tap this frame.</summary>
    protected static bool Pressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) return true;
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        var touch = Touchscreen.current;
        return touch != null && touch.primaryTouch.press.wasPressedThisFrame;
    }

    /// <summary>Character cards of the game that have a picture (or a picture link), shuffled.</summary>
    protected static List<Card> PictureCards()
    {
        var db = CardDatabase.Instance;
        if (db == null) return new List<Card>();
        return db.AllCharacterCards().Where(c => c.cardImg != null || !string.IsNullOrEmpty(c.imageUrl)).OrderBy(_ => Random.value).ToList();
    }

    protected static T Pick<T>(IList<T> list) => list[Random.Range(0, list.Count)];

    protected static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }
}
