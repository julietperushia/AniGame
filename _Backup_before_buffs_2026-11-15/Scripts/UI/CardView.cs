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
    public Text talentText;
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
        ApplyIdentity(rt.baseCard, card.rarity, build.element, rt.talent, $"POWER {CardStats.Power(card.rarity, build)}");
        hpBar.Set(rt.maxHealth, rt.maxHealth, true);
        manaBar.Set(0, rt.maxMana, true);
        SetStats(rt);
        statusText.text = $"<color=#9AA4B5>Roll  HP {Signed(build.hp)}  ATK {Signed(build.atk)}  DEF {Signed(build.def)}  SPD {Signed(build.spd)}   (max ±{GameRules.MaxDeviation(card.Tier)})</color>";
        SetDefeated(false);
    }

    /// <summary>A floor enemy (sheet stats).</summary>
    public void ShowEnemy(Card card)
    {
        Runtime = null;
        if (card == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);
        var rt = new CardRuntime(card);
        ApplyIdentity(card, card.rarity, card.element, card.talent, $"POWER {CardStats.EnemyPower(card)}");
        hpBar.Set(rt.maxHealth, rt.maxHealth, true);
        manaBar.Set(rt.mana, rt.maxMana, true);
        SetStats(rt);
        statusText.text = $"<color=#9AA4B5>Crit {rt.critRate}%   Evasion {rt.evasion}%</color>";
        SetDefeated(false);
    }

    /// <summary>Binds the view to a fighter for the battle.</summary>
    public void Bind(CardRuntime runtime)
    {
        Runtime = runtime;
        if (runtime == null) return;
        gameObject.SetActive(true);
        int power = runtime.instance != null ? CardStats.Power(runtime.rarity, runtime.build) : CardStats.EnemyPower(runtime.baseCard);
        ApplyIdentity(runtime.baseCard, runtime.rarity, runtime.element, runtime.talent, $"POWER {power}");
        SetVitals(runtime.health, runtime.maxHealth, runtime.mana, runtime.maxMana, true);
        RefreshLive();
        SetDefeated(false);
        body.anchoredPosition = Vector2.zero;
        body.localScale = Vector3.one;
    }

    static string Signed(int v) => v > 0 ? $"<color=#7CFC6A>+{v}</color>" : v < 0 ? $"<color=#FF6B6B>{v}</color>" : "0";

    void ApplyIdentity(Card art, CardRarity rarity, CardElement element, Talent talent, string bottomLabel)
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

        elementChip.color = UIFactory.WithAlpha(elementColor, 0.95f);
        elementText.text = ElementSystem.ShortName(element);
        if (elementIcon != null)
        {
            var icon = theme != null ? theme.ElementIcon(element) : null;
            elementIcon.sprite = icon;
            elementIcon.enabled = icon != null;
        }

        levelText.text = bottomLabel;
        nameText.text = name;

        if (animeText != null)
        {
            string anime = art != null ? art.anime : null;
            animeText.text = string.IsNullOrEmpty(anime) ? "" : anime;
            if (animeChip != null) animeChip.gameObject.SetActive(!string.IsNullOrEmpty(anime));
        }

        if (talent != null)
        {
            string kind = talent.IsActive ? "<color=#6FD3FF>ACTIVE</color>" : "<color=#FFB23D>PASSIVE</color>";
            int bonus = Mathf.RoundToInt((GameRules.TalentPower(GameRules.Tier(rarity)) - 1f) * 100f);
            string scale = bonus > 0 ? $" <color=#7CFC6A>+{bonus}%</color>" : "";
            talentText.text = $"{talent.DisplayName}{scale}  <size=16>{kind}</size>";
        }
        else talentText.text = "<color=#9AA4B5>No talent</color>";
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
        statusText.text = string.IsNullOrEmpty(status) ? "<color=#5A6475>No effects</color>" : status;
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

    public void Lunge(float direction, float duration)
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(LungeRoutine(direction, duration));
    }

    IEnumerator LungeRoutine(float direction, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            body.anchoredPosition = new Vector2(Mathf.Sin(k * Mathf.PI) * 70f * direction, Mathf.Sin(k * Mathf.PI) * 12f);
            yield return null;
        }
        body.anchoredPosition = Vector2.zero;
    }

    public void Shake(float duration, float strength = 14f)
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(ShakeRoutine(duration, strength));
    }

    IEnumerator ShakeRoutine(float duration, float strength)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float falloff = 1f - Mathf.Clamp01(t / duration);
            body.anchoredPosition = Random.insideUnitCircle * strength * falloff;
            yield return null;
        }
        body.anchoredPosition = Vector2.zero;
    }

    public void Flash(Color color, float duration)
    {
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
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseRoutine(duration, amount));
    }

    IEnumerator PulseRoutine(float duration, float amount)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            body.localScale = Vector3.one * (1f + amount * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI));
            yield return null;
        }
        body.localScale = Vector3.one;
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

        view.elementChip = UIFactory.CreateChip(view.artBackground.transform, "Element", Color.white);
        UIFactory.Place(view.elementChip.rectTransform, new Vector2(1, 1), new Vector2(-8, -8), new Vector2(124, 38));
        view.elementIcon = UIFactory.Icon(view.elementChip.transform, "Icon", null, Color.clear, 30f, new Vector2(4f, 0f));
        view.elementIcon.enabled = false;
        view.elementText = UIFactory.CreateText(view.elementChip.transform, "Text", "FIRE", 18, new Color(0.08f, 0.08f, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(view.elementText.rectTransform, 30, 0, 4, 0);
        UIFactory.BestFit(view.elementText, 10);

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

        view.talentText = UIFactory.CreateText(innerRt, "Talent", "", 21, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(view.talentText.rectTransform, 378, 30, 12, 12);
        UIFactory.BestFit(view.talentText, 12);

        // Bars
        view.hpBar = StatBar.Build(innerRt, "HPBar", UIFactory.HpColor, 18);
        UIFactory.TopStrip((RectTransform)view.hpBar.transform, 412, 32, 14, 14);
        view.hpBar.format = "HP {0} / {1}";

        view.manaBar = StatBar.Build(innerRt, "ManaBar", UIFactory.ManaColor, 14);
        UIFactory.TopStrip((RectTransform)view.manaBar.transform, 448, 20, 14, 14);
        view.manaBar.format = "MANA {0} / {1}";

        var stats = UIFactory.CreateRect("Stats", innerRt);
        UIFactory.TopStrip(stats, 474, 34, 10, 10);
        view.atkText = UIFactory.CreateText(stats, "ATK", "ATK 0", 22, UIFactory.TextMain);
        UIFactory.Region(view.atkText.rectTransform, new Vector2(0f, 0f), new Vector2(0.333f, 1f));
        view.defText = UIFactory.CreateText(stats, "DEF", "DEF 0", 22, UIFactory.TextMain);
        UIFactory.Region(view.defText.rectTransform, new Vector2(0.333f, 0f), new Vector2(0.666f, 1f));
        view.spdText = UIFactory.CreateText(stats, "SPD", "SPD 0", 22, UIFactory.TextMain);
        UIFactory.Region(view.spdText.rectTransform, new Vector2(0.666f, 0f), new Vector2(1f, 1f));

        view.statusText = UIFactory.CreateText(innerRt, "Status", "", 17, UIFactory.TextMain, TextAnchor.UpperCenter);
        UIFactory.TopStrip(view.statusText.rectTransform, 512, 76, 14, 14);

        // Rarity frame overlay (+ shine over the art, sparkles)
        view.frameFx = CardFrame.Attach(body, view.glow, view.artBackground.rectTransform, 2f);

        view.flash = UIFactory.CreatePanel(body, "Flash", new Color(1, 1, 1, 0f), false);
        UIFactory.Stretch(view.flash.rectTransform);
        return view;
    }
}
