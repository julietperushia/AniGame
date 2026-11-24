using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Big card display: art, rarity frame (with effects for high rarity), element, power, HP/mana bars, stats,
/// talent and status line. Shows an owned card + build, a floor enemy, or a live fighter in battle.
/// Built at a fixed 400 x 620 size; scale the GameObject to resize.
/// </summary>
public class CardView : MonoBehaviour
{
    public const float Width = 400f;
    public const float Height = 620f;

    [Header("Parts")]
    public RectTransform body;
    public Image glow;
    public Image frame;          // background panel tinted by rarity
    public CardFrame frameFx;    // rarity frame overlay + effects
    public Image artBackground;
    public Image art;
    public Text initials;
    public Image rarityChip;
    public Text rarityText;
    public Image elementChip;
    public Image elementIcon;
    public Text elementText;
    public Text levelText;       // shows POWER
    public Image animeChip;
    public Text animeText;
    public Text nameText;
    public Text talentText;      // rarity bonus under the talent icon ("+15%")
    public Image talentIcon;
    public Image talentIcon2;    // God cards: the passive talent
    public StatBar hpBar;
    public StatBar manaBar;
    public Text atkText;
    public Text defText;
    public Text spdText;
    public Text statusText;
    public Image flash;
    public CanvasGroup group;

    public CardRuntime Runtime { get; private set; }
    Card shownCard;

    Coroutine flashRoutine;
    Coroutine moveRoutine;
    Coroutine pulseRoutine;

    // ---------------------------------------------------------------- data

    /// <summary>An owned card with one of its builds (card's own build or a slot build).</summary>
    public void ShowInstance(CardInstance card, BuildData build)
    {
        Runtime = null;
        if (card == null || build == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);
        var rt = CardRuntime.ForBuild(card, build);
        ApplyIdentity(rt.baseCard, card.rarity, build.element, rt.talent, $"POWER {CardStats.Power(card, build)}", rt.talents.Count > 1 ? rt.talents[1] : null);
        hpBar.Set(rt.maxHealth, rt.maxHealth, true);
        manaBar.Set(0, rt.maxMana, true);
        SetStats(rt);
        statusText.text = $"<color=#9AA4B5>Roll  HP {Signed(build.hp)}  ATK {Signed(build.atk)}  DEF {Signed(build.def)}  SPD {Signed(build.spd)}   (max ±{GameRules.MaxDeviation(card.Tier)})</color>" +
                          (card.godBonus > 0 ? $"\n<color=#FFE9A3>God Orb +{card.godBonus}% all stats</color>" : "");
        SetDefeated(false);
    }

    /// <summary>A floor enemy (sheet stats).</summary>
    public void ShowEnemy(Card card)
    {
        Runtime = null;
        if (card == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);
        var rt = new CardRuntime(card);
        ApplyIdentity(card, card.rarity, card.element, card.talent, $"POWER {CardStats.EnemyPower(card)}", card.talent2);
        hpBar.Set(rt.maxHealth, rt.maxHealth, true);
        manaBar.Set(rt.mana, rt.maxMana, true);
        SetStats(rt);
        statusText.text = $"<color=#9AA4B5>Crit {rt.critRate}%   Evasion {GameRules.EvasionLabel(rt.evasion)}</color>";
        SetDefeated(false);
    }

    /// <summary>Binds the view to a fighter for the battle.</summary>
    public void Bind(CardRuntime runtime)
    {
        Runtime = runtime;
        if (runtime == null) return;
        gameObject.SetActive(true);
        int power = runtime.instance != null ? CardStats.Power(runtime.instance, runtime.build) : CardStats.EnemyPower(runtime.baseCard);
        ApplyIdentity(runtime.baseCard, runtime.rarity, runtime.element, runtime.talent, $"POWER {power}", runtime.talents.Count > 1 ? runtime.talents[1] : null);
        SetVitals(runtime.health, runtime.maxHealth, runtime.mana, runtime.maxMana, true);
        RefreshLive();
        SetDefeated(false);
        ResetPose();
    }

    static string Signed(int v) => v > 0 ? $"<color=#7CFC6A>+{v}</color>" : v < 0 ? $"<color=#FF6B6B>{v}</color>" : "0";

    void ApplyIdentity(Card art, CardRarity rarity, CardElement element, Talent talent, string bottomLabel, Talent talent2 = null)
    {
        Color rarityColor = CardStats.RarityColor(rarity);
        Color elementColor = ElementSystem.GetColor(element);
        var theme = UITheme.Instance;

        frame.color = CardStats.RarityColorDark(rarity);
        glow.color = UIFactory.WithAlpha(rarityColor, 0.5f);
        if (frameFx != null) frameFx.SetRarity(rarity);
        artBackground.color = Color.Lerp(elementColor, Color.black, 0.6f);

        shownCard = art;
        string name = art != null ? art.DisplayName : "Unknown";
        Sprite sprite = art != null ? art.cardImg : null;
        this.art.sprite = sprite;
        this.art.enabled = sprite != null;
        initials.text = sprite == null ? Initials(name) : "";
        initials.color = UIFactory.WithAlpha(elementColor, 0.85f);
        if (art != null && sprite == null && !string.IsNullOrEmpty(art.imageUrl))
        {
            var wanted = art;
            RemoteImageCache.Request(art.imageUrl, s =>
            {
                if (this == null || shownCard != wanted || s == null) return;
                this.art.sprite = s;
                this.art.enabled = true;
                initials.text = "";
            });
        }

        rarityChip.color = rarityColor;
        rarityText.text = CardStats.RarityName(rarity).ToUpperInvariant();
        rarityText.color = rarity >= CardRarity.Legendary ? new Color(0.1f, 0.08f, 0.02f) : Color.white;

        // Element and talent are shown as pictures (names in the tooltip)
        var elementSprite = theme != null ? theme.ElementIcon(element) : null;
        elementChip.color = elementSprite != null ? Color.clear : UIFactory.WithAlpha(elementColor, 0.95f);
        elementText.text = elementSprite != null ? "" : ElementSystem.ShortName(element);
        if (elementIcon != null)
        {
            elementIcon.sprite = elementSprite;
            elementIcon.enabled = elementSprite != null;
            TooltipTrigger.Add(elementIcon, IconInfo.Element(element));
        }

        levelText.text = bottomLabel;
        nameText.text = name;

        if (animeText != null)
        {
            string anime = art != null ? art.anime : null;
            animeText.text = string.IsNullOrEmpty(anime) ? "" : anime;
            if (animeChip != null) animeChip.gameObject.SetActive(!string.IsNullOrEmpty(anime));
        }

        var talentSprite = theme != null ? theme.TalentIcon(talent) : null;
        if (talentIcon != null)
        {
            talentIcon.sprite = talentSprite;
            talentIcon.enabled = talentSprite != null;
            talentIcon.gameObject.SetActive(talent != null);
            TooltipTrigger.Add(talentIcon, IconInfo.Talent(talent, rarity));
        }
        // God cards: second (passive) talent icon under the first one, created on demand
        if (talent2 != null && talentIcon2 == null && talentIcon != null)
        {
            talentIcon2 = UIFactory.CreateImage(talentIcon.transform.parent, "TalentIcon2", Color.white);
            talentIcon2.preserveAspect = true;
            var r1 = talentIcon.rectTransform;
            var r2 = talentIcon2.rectTransform;
            r2.anchorMin = r1.anchorMin; r2.anchorMax = r1.anchorMax; r2.pivot = r1.pivot;
            r2.sizeDelta = r1.sizeDelta;
            r2.anchoredPosition = r1.anchoredPosition + new Vector2(0f, -r1.sizeDelta.y - 24f);
        }
        if (talentIcon2 != null)
        {
            var s2 = talent2 != null && theme != null ? theme.TalentIcon(talent2) : null;
            talentIcon2.sprite = s2;
            talentIcon2.enabled = s2 != null;
            talentIcon2.gameObject.SetActive(talent2 != null);
            if (talent2 != null) TooltipTrigger.Add(talentIcon2, IconInfo.Talent(talent2, rarity));
        }
        int bonus = Mathf.RoundToInt((GameRules.TalentPower(GameRules.Tier(rarity)) - 1f) * 100f);
        if (talent == null) talentText.text = "";
        else if (talentSprite == null) talentText.text = talent.DisplayName; // no icon yet: fall back to the name
        else talentText.text = bonus > 0 ? $"+{bonus}%" : "";
    }

    static string Initials(string name)
    {
        if (string.IsNullOrEmpty(name)) return "?";
        string s = "";
        foreach (var p in name.Split(' '))
        {
            if (p.Length > 0) s += char.ToUpperInvariant(p[0]);
            if (s.Length >= 2) break;
        }
        return s;
    }

    public void SetVitals(int hp, int maxHp, int mana, int maxMana, bool instant)
    {
        hpBar.Set(Mathf.Max(0, hp), Mathf.Max(1, maxHp), instant);
        float pct = maxHp > 0 ? (float)hp / maxHp : 0f;
        hpBar.SetColor(pct > 0.5f ? UIFactory.HpColor : pct > 0.25f ? new Color(1f, 0.8f, 0.25f) : UIFactory.Bad);
        manaBar.Set(Mathf.Max(0, mana), Mathf.Max(1, maxMana), instant);
        manaBar.SetColor(mana >= maxMana ? new Color(0.55f, 0.85f, 1f) : UIFactory.ManaColor);
    }

    /// <summary>Refreshes stats and statuses from the bound runtime (bars come from event snapshots).</summary>
    public void RefreshLive()
    {
        if (Runtime == null) return;
        SetStats(Runtime);
        string status = Runtime.StatusSummary();
        string combat = $"<color=#9AA4B5>Crit {Runtime.critRate}%   Evasion {GameRules.EvasionLabel(Runtime.evasion)}</color>";
        statusText.text = combat + "\n" + (string.IsNullOrEmpty(status) ? "<color=#5A6475>No effects</color>" : status);
    }

    void SetStats(CardRuntime rt)
    {
        atkText.text = StatLabel("ATK", rt.TotalAttack, rt.baseAttack);
        defText.text = StatLabel("DEF", rt.TotalDefense, rt.baseDefense);
        spdText.text = StatLabel("SPD", rt.TotalSpeed, rt.baseSpeed);
    }

    static string StatLabel(string label, int value, int baseValue)
    {
        string color = value > baseValue ? "#7CFC6A" : value < baseValue ? "#FF6B6B" : "#EEF1F7";
        return $"<color=#9AA4B5>{label}</color> <color={color}>{value}</color>";
    }

    public void SetDefeated(bool defeated)
    {
        if (group != null) group.alpha = defeated ? 0.45f : 1f;
        art.color = defeated ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
    }

    // ---------------------------------------------------------------- animation
    // The body is posed from layers that combine: a move (attack, dodge, knock-back, entrance),
    // a shake, a pulse scale, a tilt and an optional idle float. Card moves run on UIFx.Dt, so they
    // follow the battle speed (x1/x2/x4) and slow motion.

    Vector2 moveOffset, shakeOffset;
    float tilt;
    Vector2 squash = Vector2.one;
    float pulseScale = 1f;
    Coroutine shakeRoutine;
    bool knockedOut;
    float idlePhase;

    /// <summary>Gentle floating while waiting (battle).</summary>
    [System.NonSerialized] public bool idle;

    static float Dt => UIFx.Dt;

    void Awake() => idlePhase = Random.Range(0f, 6.3f);

    void LateUpdate()
    {
        if (body == null) return;
        Vector2 bob = idle && !knockedOut ? new Vector2(0f, Mathf.Sin(Time.unscaledTime * 1.8f + idlePhase) * 6f) : Vector2.zero;
        body.anchoredPosition = moveOffset + shakeOffset + bob;
        body.localRotation = Quaternion.Euler(0f, 0f, tilt);
        body.localScale = new Vector3(squash.x * pulseScale, squash.y * pulseScale, 1f);
    }

    /// <summary>Stops every move and puts the card back in its resting pose.</summary>
    public void ResetPose()
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        moveRoutine = shakeRoutine = pulseRoutine = null;
        moveOffset = shakeOffset = Vector2.zero;
        tilt = 0f;
        squash = Vector2.one;
        pulseScale = 1f;
        knockedOut = false;
        if (body != null)
        {
            body.anchoredPosition = Vector2.zero;
            body.localRotation = Quaternion.identity;
            body.localScale = Vector3.one;
        }
    }

    bool CanAnimate => isActiveAndEnabled && body != null;

    void Run(IEnumerator sequence)
    {
        if (!CanAnimate) return;
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(sequence);
    }

    /// <summary>Tweens the move layer to a pose.</summary>
    IEnumerator Pose(Vector2 offset, float toTilt, Vector2 toSquash, float duration, System.Func<float, float> ease, System.Action<float> onStep = null)
    {
        Vector2 fromOffset = moveOffset, fromSquash = squash;
        float fromTilt = tilt;
        float t = 0f;
        while (t < duration)
        {
            t += Dt;
            float k = Mathf.Clamp01(t / duration);
            float e = ease(k);
            moveOffset = Vector2.LerpUnclamped(fromOffset, offset, e);
            tilt = Mathf.LerpUnclamped(fromTilt, toTilt, e);
            squash = Vector2.LerpUnclamped(fromSquash, toSquash, e);
            onStep?.Invoke(k);
            yield return null;
        }
        moveOffset = offset;
        tilt = toTilt;
        squash = toSquash;
        onStep?.Invoke(1f);
    }

    IEnumerator Hold(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Dt; yield return null; }
    }

    /// <summary>Simple forward-and-back hop (kept for older callers).</summary>
    public void Lunge(float direction, float duration)
    {
        Run(LungeRoutine(direction, duration));
    }

    IEnumerator LungeRoutine(float direction, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Dt;
            float k = Mathf.Clamp01(t / duration);
            moveOffset = new Vector2(Mathf.Sin(k * Mathf.PI) * 70f * direction, Mathf.Sin(k * Mathf.PI) * 12f);
            yield return null;
        }
        moveOffset = Vector2.zero;
    }

    /// <summary>Pulls back and leans away before an attack (bigger for a crit). dir: +1 attacks right, -1 left.</summary>
    public void WindUp(float dir, bool heavy)
    {
        Run(Pose(new Vector2(-dir * (heavy ? 50f : 28f), heavy ? 16f : 6f), dir * (heavy ? 8f : 4f),
                 heavy ? new Vector2(0.93f, 1.07f) : new Vector2(0.97f, 1.03f), heavy ? 0.26f : 0.13f, UIFx.EaseOutQuad));
    }

    /// <summary>Fast dash toward the enemy, leaving afterimages. Stays forward until ReturnHome / HitReact.</summary>
    public void Dash(float dir, float distance, Color ghostTint, int ghosts = 3)
    {
        int spawned = 0;
        Run(Pose(new Vector2(dir * distance, 0f), -dir * 6f, new Vector2(1.1f, 0.92f), 0.09f, UIFx.EaseInQuad, k =>
        {
            while (spawned < ghosts && k >= spawned / (float)ghosts)
            {
                spawned++;
                UIFx.Ghost(this, ghostTint, 0.3f, 0.42f);
            }
        }));
    }

    /// <summary>Eases back to the resting spot.</summary>
    public void ReturnHome(float duration = 0.24f)
    {
        if (knockedOut) return;
        Run(Pose(Vector2.zero, 0f, Vector2.one, duration, UIFx.EaseOutBack));
    }

    /// <summary>Knock-back + shake when hit. dir = direction of the push (the attacker's dir).</summary>
    public void HitReact(float dir, bool crit)
    {
        Run(HitSequence(dir, crit));
        Shake(crit ? 0.42f : 0.24f, crit ? 18f : 9f);
    }

    IEnumerator HitSequence(float dir, bool crit)
    {
        yield return Pose(new Vector2(dir * (crit ? 80f : 36f), crit ? 14f : 4f), -dir * (crit ? 11f : 5f),
                          crit ? new Vector2(0.88f, 1.1f) : new Vector2(0.94f, 1.05f), crit ? 0.08f : 0.06f, UIFx.EaseOutQuad);
        if (crit) yield return Hold(0.06f);
        yield return Pose(Vector2.zero, 0f, Vector2.one, crit ? 0.4f : 0.26f, UIFx.EaseOutBack);
    }

    /// <summary>Side-hop away from an attack. dir = direction away from the attacker.</summary>
    public void Dodge(float dir)
    {
        Run(DodgeSequence(dir));
    }

    IEnumerator DodgeSequence(float dir)
    {
        yield return Pose(new Vector2(dir * 95f, 64f), -dir * 13f, new Vector2(0.92f, 1.07f), 0.1f, UIFx.EaseOutQuad);
        yield return Hold(0.14f);
        yield return Pose(Vector2.zero, 0f, Vector2.one, 0.24f, UIFx.EaseOutBack);
    }

    /// <summary>Defeat: staggers, tips over and drops. Stays down until the next Bind/ResetPose.</summary>
    public void KnockOut(float dir)
    {
        knockedOut = true;
        Run(KnockOutSequence(dir));
    }

    IEnumerator KnockOutSequence(float dir)
    {
        yield return Pose(new Vector2(dir * 40f, 24f), -dir * 6f, new Vector2(0.92f, 1.08f), 0.12f, UIFx.EaseOutQuad);
        yield return Pose(new Vector2(dir * 80f, -56f), -dir * 17f, new Vector2(1.02f, 0.96f), 0.5f, UIFx.EaseOutBounce);
    }

    /// <summary>Slides in from off-screen (fromDir -1 = from the left) and lands with a squash.</summary>
    public void EnterFrom(float fromDir, float duration = 0.42f)
    {
        if (!CanAnimate) return;
        moveOffset = new Vector2(fromDir * 1150f, 0f);
        tilt = fromDir * 8f;
        Run(EnterSequence(fromDir, duration));
    }

    IEnumerator EnterSequence(float fromDir, float duration)
    {
        int ghosts = 0;
        yield return Pose(new Vector2(-fromDir * 22f, 0f), fromDir * 5f, new Vector2(1.08f, 0.94f), duration, UIFx.EaseOutCubic, k =>
        {
            if (ghosts < 3 && k >= 0.15f + ghosts * 0.2f) { ghosts++; UIFx.Ghost(this, Color.white, 0.25f, 0.3f, 0.35f); }
        });
        yield return Pose(Vector2.zero, 0f, new Vector2(1.04f, 0.95f), 0.08f, UIFx.EaseOutQuad);
        yield return Pose(Vector2.zero, 0f, Vector2.one, 0.2f, UIFx.EaseOutBack);
    }

    /// <summary>Two happy hops (winner).</summary>
    public void Victory()
    {
        if (knockedOut) return;
        Run(VictorySequence());
    }

    IEnumerator VictorySequence()
    {
        for (int i = 0; i < 2; i++)
        {
            yield return Pose(Vector2.zero, 0f, new Vector2(1.07f, 0.92f), 0.08f, UIFx.EaseOutQuad);
            yield return Pose(new Vector2(0f, 48f), i == 0 ? 3f : -3f, new Vector2(0.95f, 1.06f), 0.16f, UIFx.EaseOutQuad);
            yield return Pose(Vector2.zero, 0f, Vector2.one, 0.16f, UIFx.EaseInQuad);
        }
    }

    public void Shake(float duration, float strength = 14f)
    {
        if (!CanAnimate) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, strength));
    }

    IEnumerator ShakeRoutine(float duration, float strength)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Dt;
            float falloff = 1f - Mathf.Clamp01(t / duration);
            shakeOffset = Random.insideUnitCircle * strength * falloff;
            yield return null;
        }
        shakeOffset = Vector2.zero;
    }

    public void Flash(Color color, float duration)
    {
        if (!CanAnimate || flash == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(color, duration));
    }

    IEnumerator FlashRoutine(Color color, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            flash.color = new Color(color.r, color.g, color.b, Mathf.Lerp(color.a, 0f, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        flash.color = new Color(color.r, color.g, color.b, 0f);
    }

    public void Pulse(float duration, float amount = 0.06f)
    {
        if (!CanAnimate) return;
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseRoutine(duration, amount));
    }

    IEnumerator PulseRoutine(float duration, float amount)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Dt;
            pulseScale = 1f + amount * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            yield return null;
        }
        pulseScale = 1f;
    }

    // ---------------------------------------------------------------- construction

    public static CardView Build(Transform parent, string name)
    {
        var root = UIFactory.CreateRect(name, parent);
        root.sizeDelta = new Vector2(Width, Height);
        var view = root.gameObject.AddComponent<CardView>();
        view.group = root.gameObject.AddComponent<CanvasGroup>();

        var body = UIFactory.CreateRect("Body", root);
        UIFactory.Stretch(body);
        view.body = body;

        var theme = UIFactory.Theme;
        view.glow = UIFactory.CreateImage(body, "Glow", new Color(1, 1, 1, 0.5f), theme != null ? theme.glow : null);
        UIFactory.Stretch(view.glow.rectTransform, -70, -70, -70, -70);

        view.frame = UIFactory.CreatePanel(body, "Background", Color.gray, false);
        UIFactory.Stretch(view.frame.rectTransform);

        var inner = UIFactory.CreatePanel(body, "Inner", new Color(0.06f, 0.07f, 0.11f, 1f), false);
        UIFactory.Stretch(inner.rectTransform, 10, 10, 10, 10);
        var innerRt = inner.rectTransform;

        // Art
        view.artBackground = UIFactory.CreatePanel(innerRt, "ArtBackground", Color.gray, false);
        UIFactory.TopStrip(view.artBackground.rectTransform, 8, 326, 8, 8);
        if (theme != null && theme.gradient != null)
        {
            var shine = UIFactory.CreateImage(view.artBackground.transform, "Shade", new Color(1, 1, 1, 0.18f), theme.gradient);
            UIFactory.Stretch(shine.rectTransform);
        }
        view.initials = UIFactory.CreateText(view.artBackground.transform, "Initials", "", 120, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(view.initials.rectTransform);
        view.art = UIFactory.CreateImage(view.artBackground.transform, "Art", Color.white);
        view.art.preserveAspect = true;
        UIFactory.Stretch(view.art.rectTransform, 4, 4, 4, 4);

        // Chips on the art
        view.rarityChip = UIFactory.CreateChip(view.artBackground.transform, "Rarity", Color.white);
        UIFactory.TopLeft(view.rarityChip.rectTransform, 8, 8, 150, 34);
        view.rarityText = UIFactory.CreateText(view.rarityChip.transform, "Text", "RARE", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(view.rarityText.rectTransform, 4, 0, 4, 0);
        UIFactory.BestFit(view.rarityText, 10);

        view.elementChip = UIFactory.CreateChip(view.artBackground.transform, "Element", Color.clear);
        UIFactory.Place(view.elementChip.rectTransform, new Vector2(1, 1), new Vector2(-6, -6), new Vector2(62, 62));
        view.elementIcon = UIFactory.CreateImage(view.elementChip.transform, "Icon", Color.white);
        view.elementIcon.preserveAspect = true;
        UIFactory.Stretch(view.elementIcon.rectTransform);
        view.elementIcon.enabled = false;
        view.elementText = UIFactory.CreateText(view.elementChip.transform, "Text", "", 16, new Color(0.08f, 0.08f, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(view.elementText.rectTransform, 2, 0, 2, 0);
        UIFactory.BestFit(view.elementText, 9);

        view.talentIcon = UIFactory.CreateImage(view.artBackground.transform, "TalentIcon", Color.white);
        view.talentIcon.preserveAspect = true;
        UIFactory.Place(view.talentIcon.rectTransform, new Vector2(1, 1), new Vector2(-6, -74), new Vector2(62, 62));
        view.talentText = UIFactory.CreateText(view.talentIcon.transform, "Bonus", "", 17, new Color(0.55f, 1f, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Place(view.talentText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, -18), new Vector2(110, 24));
        UIFactory.AddOutline(view.talentText, new Color(0, 0, 0, 0.95f), 1.5f);
        UIFactory.BestFit(view.talentText, 9);

        var levelChip = UIFactory.CreateChip(view.artBackground.transform, "PowerChip", new Color(0, 0, 0, 0.72f));
        UIFactory.Place(levelChip.rectTransform, new Vector2(0, 0), new Vector2(8, 8), new Vector2(170, 34));
        view.levelText = UIFactory.CreateText(levelChip.transform, "Text", "POWER 0", 19, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(view.levelText.rectTransform, 4, 0, 4, 0);
        UIFactory.BestFit(view.levelText, 10);

        view.animeChip = UIFactory.CreateChip(view.artBackground.transform, "AnimeChip", new Color(0, 0, 0, 0.72f));
        UIFactory.Place(view.animeChip.rectTransform, new Vector2(1, 0), new Vector2(-8, 8), new Vector2(176, 34));
        view.animeText = UIFactory.CreateText(view.animeChip.transform, "Text", "", 16, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleCenter, FontStyle.Italic);
        UIFactory.Stretch(view.animeText.rectTransform, 6, 0, 6, 0);
        UIFactory.BestFit(view.animeText, 9);
        view.animeChip.gameObject.SetActive(false);

        // Name + talent
        view.nameText = UIFactory.CreateText(innerRt, "Name", "Card", 30, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(view.nameText.rectTransform, 338, 42, 12, 12);
        UIFactory.BestFit(view.nameText, 14);
        UIFactory.AddShadow(view.nameText, UIFactory.ShadowColor, 2f);

        // Bars
        view.hpBar = StatBar.Build(innerRt, "HPBar", UIFactory.HpColor, 18);
        UIFactory.TopStrip((RectTransform)view.hpBar.transform, 388, 32, 14, 14);
        view.hpBar.format = "HP {0} / {1}";

        view.manaBar = StatBar.Build(innerRt, "ManaBar", UIFactory.ManaColor, 14);
        UIFactory.TopStrip((RectTransform)view.manaBar.transform, 426, 20, 14, 14);
        view.manaBar.format = "MANA {0} / {1}";

        var stats = UIFactory.CreateRect("Stats", innerRt);
        UIFactory.TopStrip(stats, 456, 34, 10, 10);
        view.atkText = UIFactory.CreateText(stats, "ATK", "ATK 0", 22, UIFactory.TextMain);
        UIFactory.Region(view.atkText.rectTransform, new Vector2(0f, 0f), new Vector2(0.333f, 1f));
        view.defText = UIFactory.CreateText(stats, "DEF", "DEF 0", 22, UIFactory.TextMain);
        UIFactory.Region(view.defText.rectTransform, new Vector2(0.333f, 0f), new Vector2(0.666f, 1f));
        view.spdText = UIFactory.CreateText(stats, "SPD", "SPD 0", 22, UIFactory.TextMain);
        UIFactory.Region(view.spdText.rectTransform, new Vector2(0.666f, 0f), new Vector2(1f, 1f));

        view.statusText = UIFactory.CreateText(innerRt, "Status", "", 17, UIFactory.TextMain, TextAnchor.UpperCenter);
        UIFactory.TopStrip(view.statusText.rectTransform, 496, 92, 14, 14);

        // Rarity frame overlay (+ shine over the art, sparkles)
        view.frameFx = CardFrame.Attach(body, view.glow, view.artBackground.rectTransform, 2f);

        view.flash = UIFactory.CreatePanel(body, "Flash", new Color(1, 1, 1, 0f), false);
        UIFactory.Stretch(view.flash.rectTransform);
        return view;
    }
}
