using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Flappy Hero: your character flies between pipes. Space / click / tap to flap. Score = pipes passed.</summary>
public class FlappyGame : MinigameBase
{
    class Pipe { public RectTransform top, bottom; public float x, gapY; public bool scored; }

    const float Gravity = -2300f, FlapSpeed = 760f, PipeWidth = 130f, BirdSize = 76f;
    RectTransform bird;
    Text scoreText, hint;
    readonly List<Pipe> pipes = new List<Pipe>();
    float y, vy, spawnTimer, birdX;
    bool started;

    public override string Id => MinigameConfig.Flappy;
    public override string Help => "SPACE, click or tap to flap. Fly through the gaps: every pipe passed is a point.";

    protected override void Build()
    {
        area.gameObject.AddComponent<RectMask2D>();
        var sky = UIFactory.CreateImage(area, "Sky", new Color(0.35f, 0.62f, 0.95f, 1f));
        UIFactory.Stretch(sky.rectTransform);
        var ground = UIFactory.CreateImage(area, "Ground", new Color(0.3f, 0.65f, 0.3f, 1f));
        UIFactory.Place(ground.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(4000, 20));
        birdX = -W * 0.25f;
        var img = UIFactory.CreateImage(area, "Bird", Color.white, PlayerProfile.CharacterSprite);
        if (img.sprite == null) { img.sprite = UITheme.Instance != null ? UITheme.Instance.circle : null; img.color = new Color(1f, 0.85f, 0.2f); }
        img.preserveAspect = true;
        UIFactory.AddOutline(img, new Color(0f, 0f, 0f, 0.6f), 3f);
        bird = img.rectTransform;
        UIFactory.Place(bird, new Vector2(0.5f, 0.5f), new Vector2(birdX, 0), new Vector2(BirdSize, BirdSize));
        scoreText = AddText("Score", "0", 64, Color.white, new Vector2(0, H / 2 - 60), new Vector2(400, 90));
        UIFactory.AddOutline(scoreText, new Color(0f, 0f, 0f, 0.7f), 3f);
        hint = AddText("Hint", "PRESS SPACE / CLICK TO START", 36, Color.white, new Vector2(0, -120), new Vector2(1200, 60));
        UIFactory.AddOutline(hint, new Color(0f, 0f, 0f, 0.7f), 2f);
    }

    float Gap => Mathf.Lerp(290f, 205f, Mathf.Clamp01(score / 25f));
    float Speed => 330f + 7f * score;

    void Update()
    {
        if (over || bird == null) return;
        if (!started)
        {
            bird.anchoredPosition = new Vector2(birdX, Mathf.Sin(Time.time * 4f) * 12f);
            if (Pressed()) { started = true; hint.gameObject.SetActive(false); vy = FlapSpeed; SfxSynth.Play("whoosh", 0.4f); }
            return;
        }
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (Pressed()) { vy = FlapSpeed; SfxSynth.Play("swoosh", 0.25f, 1.3f); }
        vy += Gravity * dt;
        y += vy * dt;
        bird.anchoredPosition = new Vector2(birdX, y);
        bird.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(vy / 18f, -60f, 30f));

        spawnTimer -= dt;
        if (spawnTimer <= 0f) { Spawn(); spawnTimer = 1.55f; }
        float r = BirdSize * 0.38f;
        for (int i = pipes.Count - 1; i >= 0; i--)
        {
            var p = pipes[i];
            p.x -= Speed * dt;
            p.top.anchoredPosition = new Vector2(p.x, p.top.anchoredPosition.y);
            p.bottom.anchoredPosition = new Vector2(p.x, p.bottom.anchoredPosition.y);
            if (!p.scored && p.x + PipeWidth / 2 < birdX - r)
            {
                p.scored = true;
                score++;
                scoreText.text = score.ToString();
                SfxSynth.Play("coin", 0.4f);
            }
            if (Mathf.Abs(p.x - birdX) < PipeWidth / 2 + r && (y + r > p.gapY + Gap / 2 || y - r < p.gapY - Gap / 2)) { Crash(); return; }
            if (p.x < -W / 2 - PipeWidth)
            {
                Destroy(p.top.gameObject);
                Destroy(p.bottom.gameObject);
                pipes.RemoveAt(i);
            }
        }
        if (y - r < -H / 2 + 20 || y + r > H / 2) Crash();
    }

    void Spawn()
    {
        float half = H / 2, gap = Gap;
        float gapY = Random.Range(-half + 60 + gap / 2, half - 60 - gap / 2);
        var color = new Color(0.25f, 0.75f, 0.3f);
        var top = UIFactory.CreatePanel(area, "PipeTop", color, false).rectTransform;
        float topH = half - (gapY + gap / 2);
        UIFactory.Place(top, new Vector2(0.5f, 0.5f), new Vector2(W / 2 + PipeWidth, gapY + gap / 2 + topH / 2), new Vector2(PipeWidth, topH + 40));
        var bottom = UIFactory.CreatePanel(area, "PipeBottom", color, false).rectTransform;
        float bottomH = (gapY - gap / 2) + half;
        UIFactory.Place(bottom, new Vector2(0.5f, 0.5f), new Vector2(W / 2 + PipeWidth, gapY - gap / 2 - bottomH / 2), new Vector2(PipeWidth, bottomH + 40));
        bird.SetAsLastSibling();
        scoreText.transform.SetAsLastSibling();
        pipes.Add(new Pipe { top = top, bottom = bottom, x = W / 2 + PipeWidth, gapY = gapY });
    }

    void Crash()
    {
        if (over) return;
        SfxSynth.Play("impact", 0.7f);
        StartCoroutine(CrashThenFinish());
    }

    IEnumerator CrashThenFinish()
    {
        enabled = false;
        float t = 0f;
        while (t < 0.6f) { t += Time.deltaTime; bird.anchoredPosition += new Vector2(0, -900f * Time.deltaTime); bird.localRotation = Quaternion.Euler(0, 0, -90f); yield return null; }
        Finish(score);
    }
}
