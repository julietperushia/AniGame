using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Anime Quiz: 5 quick questions mixing anime characters (whose anime, who is it) and the game (talents, elements, rarities).</summary>
public class QuizGame : MinigameBase
{
    class Question { public string text; public Card picture; public List<string> answers = new List<string>(); public int correct; }

    const int Count = 5;
    const float TimePerQuestion = 12f;
    readonly List<Question> questions = new List<Question>();
    readonly List<Button> answerButtons = new List<Button>();
    int index, right;
    float timeLeft;
    bool answering;
    Text counter, questionText, scoreText;
    Image timerFill;
    CardTile picture;

    public override string Id => MinigameConfig.Quiz;
    public override string Help => $"{Count} questions, {TimePerQuestion:0} seconds each. Anime characters and AniGame itself.";

    protected override void Build()
    {
        MakeQuestions();
        counter = AddText("Counter", "", 26, UIFactory.Gold, new Vector2(-W / 2 + 200, H / 2 - 30), new Vector2(380, 44), TextAnchor.MiddleLeft);
        scoreText = AddText("Score", "", 26, UIFactory.Good, new Vector2(W / 2 - 200, H / 2 - 30), new Vector2(380, 44), TextAnchor.MiddleRight);
        var timerBg = UIFactory.CreatePanel(area, "TimerBg", new Color(0f, 0f, 0f, 0.5f), false);
        UIFactory.Place(timerBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, H / 2 - 70), new Vector2(W - 80, 14));
        timerFill = UIFactory.CreateImage(timerBg.transform, "Fill", UIFactory.Accent2);
        UIFactory.Stretch(timerFill.rectTransform);
        picture = CardTile.Build(area, "Picture", 270f, 378f, true);
        UIFactory.Place((RectTransform)picture.transform, new Vector2(0.5f, 0.5f), new Vector2(-W / 2 + 190, -30), new Vector2(270, 378));
        questionText = AddText("Question", "", 32, UIFactory.TextMain, new Vector2(140, H / 2 - 160), new Vector2(W - 420, 130));
        questionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIFactory.BestFit(questionText, 16);
        for (int i = 0; i < 4; i++)
        {
            int n = i;
            var b = AddButton("Answer" + i, UIFactory.PanelLight, new Vector2(140 + (i % 2 == 0 ? -265 : 265), -40 - (i / 2) * 120), new Vector2(510, 100), () => Answer(n), 24);
            var label = UIFactory.ButtonLabel(b);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.BestFit(label, 12);
            answerButtons.Add(b);
        }
        Show();
    }

    void Show()
    {
        if (index >= questions.Count) { Finish(right); return; }
        var q = questions[index];
        counter.text = $"QUESTION {index + 1}/{questions.Count}";
        scoreText.text = $"{right} right";
        questionText.text = q.text;
        bool pic = q.picture != null;
        picture.gameObject.SetActive(pic);
        if (pic) { picture.Set(q.picture, "", ""); picture.nameText.text = "?"; picture.SetTalent(null); }
        questionText.rectTransform.anchoredPosition = new Vector2(pic ? 140 : 0, H / 2 - 160);
        for (int i = 0; i < 4; i++)
        {
            var b = answerButtons[i];
            bool has = i < q.answers.Count;
            b.gameObject.SetActive(has);
            if (!has) continue;
            UIFactory.ButtonLabel(b).text = q.answers[i];
            b.image.color = UIFactory.PanelLight;
            b.interactable = true;
            ((RectTransform)b.transform).anchoredPosition = new Vector2((pic ? 140 : 0) + (i % 2 == 0 ? -265 : 265), -40 - (i / 2) * 120);
        }
        timeLeft = TimePerQuestion;
        answering = true;
    }

    void Update()
    {
        if (over || !answering) return;
        timeLeft -= Time.deltaTime;
        timerFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(timeLeft / TimePerQuestion), 1f);
        if (timeLeft <= 0f) Answer(-1);
    }

    void Answer(int chosen)
    {
        if (!answering || over) return;
        answering = false;
        var q = questions[index];
        bool ok = chosen == q.correct;
        if (ok) right++;
        for (int i = 0; i < answerButtons.Count; i++) answerButtons[i].interactable = false;
        answerButtons[q.correct].image.color = UIFactory.Good;
        if (!ok && chosen >= 0) answerButtons[chosen].image.color = UIFactory.Bad;
        if (q.picture != null) picture.nameText.text = q.picture.DisplayName;
        scoreText.text = $"{right} right";
        SfxSynth.Play(ok ? "coin" : "fail", 0.5f);
        score = right;
        StartCoroutine(Next());
    }

    IEnumerator Next()
    {
        yield return new WaitForSeconds(1.1f);
        index++;
        Show();
    }

    // ---------------------------------------------------------------- questions

    void MakeQuestions()
    {
        var cards = PictureCards().Where(c => !string.IsNullOrEmpty(c.anime)).ToList();
        var animes = cards.Select(c => c.anime).Distinct().ToList();
        var kinds = new List<int> { 0, 0, 1, 2, 2, 3, 4 };
        int guard = 0;
        while (questions.Count < Count && guard++ < 60)
        {
            Question q = null;
            switch (Pick(kinds))
            {
                case 0: q = AnimeQuestion(cards, animes); break;
                case 1: q = NameQuestion(cards); break;
                case 2: q = TalentQuestion(); break;
                case 3: q = ElementQuestion(); break;
                case 4: q = RarityQuestion(); break;
            }
            if (q != null && q.answers.Count >= 2 && !questions.Any(o => o.text == q.text && o.picture == q.picture)) questions.Add(q);
        }
    }

    static Question Make(string text, Card picture, string correct, IEnumerable<string> wrong)
    {
        var q = new Question { text = text, picture = picture };
        var list = wrong.Where(w => !string.IsNullOrEmpty(w) && w != correct).Distinct().Take(3).ToList();
        list.Add(correct);
        Shuffle(list);
        q.answers = list;
        q.correct = list.IndexOf(correct);
        return q;
    }

    static Question AnimeQuestion(List<Card> cards, List<string> animes)
    {
        if (cards.Count == 0 || animes.Count < 4) return null;
        var c = Pick(cards);
        return Make("Which anime is this character from?", c, c.anime, animes.Where(a => a != c.anime).OrderBy(_ => Random.value));
    }

    static Question NameQuestion(List<Card> cards)
    {
        if (cards.Count < 4) return null;
        var c = Pick(cards);
        var same = cards.Where(o => o.anime == c.anime && o.DisplayName != c.DisplayName).Select(o => o.DisplayName).Distinct().ToList();
        var others = same.Count >= 3 ? same : same.Concat(cards.Select(o => o.DisplayName)).ToList();
        return Make($"Who is this? <size=22><color=#9AA4B5>({c.anime})</color></size>", c, c.DisplayName, others.Where(n => n != c.DisplayName).OrderBy(_ => Random.value));
    }

    static Question TalentQuestion()
    {
        var db = CardDatabase.Instance;
        if (db == null) return null;
        var talents = db.talents.Where(t => t != null).ToList();
        if (talents.Count < 4) return null;
        var t = Pick(talents);
        string desc = t.DescriptionFor(CardRarity.Common).Replace(t.DisplayName, "???");
        if (desc.Length > 260) desc = desc.Substring(0, 257) + "...";
        return Make($"Which talent is this?\n<size=21><color=#C9CEDA>{desc}</color></size>", null, t.DisplayName,
                    talents.Where(o => o != t && o.IsActive == t.IsActive).Select(o => o.DisplayName).OrderBy(_ => Random.value));
    }

    static Question ElementQuestion()
    {
        var all = ElementSystem.All.Where(e => e != CardElement.Neutral && e != CardElement.NonNeutral).ToList();
        if (all.Count < 4) return null;
        var target = Pick(all);
        var strong = all.Where(a => ElementSystem.GetElementMultiplier(a, target) > 1.001f).ToList();
        var weak = all.Where(a => ElementSystem.GetElementMultiplier(a, target) <= 1.001f).ToList();
        if (strong.Count == 0 || weak.Count < 3) return null;
        var answer = Pick(strong);
        return Make($"Which element deals MORE damage to a <b>{ElementSystem.DisplayName(target)}</b> card?", null, ElementSystem.DisplayName(answer),
                    weak.OrderBy(_ => Random.value).Select(ElementSystem.DisplayName));
    }

    static Question RarityQuestion()
    {
        var rarities = System.Enum.GetValues(typeof(CardRarity)).Cast<CardRarity>().OrderBy(_ => Random.value).Take(4).ToList();
        var best = rarities.Max();
        return Make("Which rarity is the HIGHEST?", null, CardStats.RarityName(best), rarities.Where(r => r != best).Select(CardStats.RarityName));
    }
}
