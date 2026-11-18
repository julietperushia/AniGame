using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Big card display: art, rarity frame, element, level, HP/mana bars, stats, talent and status line.
/// Used in battle (bound to a CardRuntime) and as a static preview on the map / collection screens.
/// Built at a fixed 400 x 620 size; scale the GameObject to resize.
/// </summary>
public class CardView : MonoBehaviour
{
    public const float Width = 400f;
    public const float Height = 620f;

    [Header("Parts")]
    public RectTransform body;
    public Image glow;
    public Image frame;
    public Image artBackground;
    public Image art;
    public Text initials;
    public Image rarityChip;
    public Text rarityText;
    public Image elementChip;
    public Text elementText;
    public Text levelText;
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

    /// <summary>Static preview of a card at a given level / evolution.</summary>
    public void Show(Card card, int level, int evolution)
    {
        Runtime = null;
        if (card == null)
        {
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);
        var rt = new CardRuntime(card, level, evolution);
        ApplyIdentity(card, rt.level, rt.evolution);
        hpBar.Set(rt.maxHealth, rt.maxHealth, true);
        manaBar.Set(rt.mana, rt.maxMana, true);
        SetStats(rt);
        statusText.text = $"<color=#9AA4B5>Power {CardStats.Power(card, rt.level, rt.evolution)}   Crit {rt.critRate}%   Evasion {rt.evasion}%</color>";
        SetDefeated(false);
    }

    /// <summary>Binds the view to a fighter for the battle.</summary>
    public void Bind(CardRuntime runtime)
    {
        Runtime = runtime;
        if (runtime == null) return;
        gameObject.SetActive(true);
        ApplyIdentity(runtime.baseCard, runtime.level, runtime.evolution);
        SetVitals(runtime.health, runtime.maxHealth, runtime.mana, runtime.maxMana, true);
        RefreshLive();
        SetDefeated(false);
        body.anchoredPosition = Vector2.zero;
        body.localScale = Vector3.one;
    }

    void ApplyIdentity(Card card, int level, int evolution)
    {
        Color rarity = CardStats.RarityColor(card.rarity);
        Color element = ElementSystem.GetColor(card.element);

        frame.color = rarity;
        glow.color = UIFactory.WithAlpha(rarity, 0.55f);
        artBackground.color = Color.Lerp(element, Color.black, 0.55f);

        shownCard = card;
        art.sprite = card.cardImg;
        art.enabled = card.cardImg != null;
        initials.text = card.cardImg == null ? Initials(card.DisplayName) : "";
        initials.color = UIFactory.WithAlpha(element, 0.85f);
        if (card.cardImg == null && !string.IsNullOrEmpty(card.imageUrl))
        {
            RemoteImageCache.Request(card.imageUrl, sprite =>
            {
                if (this == null || shownCard != card || sprite == null) return;
                art.sprite = sprite;
                art.enabled = true;
                initials.text = "";
            });
        }

        if (animeText != null)
        {
            bool hasAnime = !string.IsNullOrEmpty(card.anime);
            animeText.text = hasAnime ? card.anime : "";
            if (animeChip != null) animeChip.gameObject.SetActive(hasAnime);
        }

        rarityChip.color = rarity;
        rarityText.text = CardStats.RarityName(card.rarity).ToUpperInvariant();
        rarityText.color = card.rarity >= CardRarity.Legendary ? new Color(0.1f, 0.08f, 0.02f) : Color.white;

        elementChip.color = element;
        elementText.text = card.element.ToString().ToUpperInvariant();

        levelText.text = evolution > 0 ? $"Lv {level}  <color=#FFD54F>EVO {evolution}</color>" : $"Lv {level}";
        nameText.text = card.DisplayName;

        if (card.talent != null)
        {
            string kind = card.talent.IsActive ? "<color=#6FD3FF>ACTIVE</color>" : "<color=#FFB23D>PASSIVE</color>";
            talentText.text = $"{card.talent.DisplayName}  <size=16>{kind}</size>";
        }
        else
        {
            talentText.text = "<color=#9AA4B5>No talent</color>";
        }
    }

    static string Initials(string name)
    {
        if (string.IsNullOrEmpty(name)) return "?";
        var parts = name.Split(' ');
        string s = "";
        foreach (var p in parts)
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
            float offset = Mathf.Sin(k * Mathf.PI) * 70f * direction;
            body.anchoredPosition = new Vector2(offset, Mathf.Sin(k * Mathf.PI) * 12f);
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
            float a = Mathf.Lerp(color.a, 0f, Mathf.Clamp01(t / duration));
            flash.color = new Color(color.r, color.g, color.b, a);
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
        Color baseGlow = glow.color;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            body.localScale = Vector3.one * (1f + amount * k);
            glow.color = new Color(baseGlow.r, baseGlow.g, baseGlow.b, Mathf.Lerp(baseGlow.a, 1f, k));
            yield return null;
        }
        body.localScale = Vector3.one;
        glow.color = baseGlow;
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
        UIFactory.Stretch(view.glow.rectTransform, -60, -60, -60, -60);

        view.frame = UIFactory.CreatePanel(body, "Frame", Color.white, false);
        UIFactory.Stretch(view.frame.rectTransform);

        var inner = UIFactory.CreatePanel(body, "Inner", new Color(0.07f, 0.08f, 0.13f, 1f), false);
        UIFactory.Stretch(inner.rectTransform, 6, 6, 6, 6);
        var innerRt = inner.rectTransform;

        // Art
        view.artBackground = UIFactory.CreatePanel(innerRt, "ArtBackground", Color.gray, false);
        UIFactory.TopStrip(view.artBackground.rectTransform, 8, 330, 8, 8);
        if (theme != null && theme.gradient != null)
        {
            var shine = UIFactory.CreateImage(view.artBackground.transform, "Shine", new Color(1, 1, 1, 0.18f), theme.gradient);
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
        var ec = view.elementChip.rectTransform;
        UIFactory.Place(ec, new Vector2(1, 1), new Vector2(-8, -8), new Vector2(110, 34));
        view.elementText = UIFactory.CreateText(view.elementChip.transform, "Text", "FIRE", 18, new Color(0.08f, 0.08f, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(view.elementText.rectTransform);

        var levelChip = UIFactory.CreateChip(view.artBackground.transform, "LevelChip", new Color(0, 0, 0, 0.7f));
        UIFactory.Place(levelChip.rectTransform, new Vector2(0, 0), new Vector2(8, 8), new Vector2(170, 34));
        view.levelText = UIFactory.CreateText(levelChip.transform, "Text", "Lv 1", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.Stretch(view.levelText.rectTransform, 4, 0, 4, 0);

        view.animeChip = UIFactory.CreateChip(view.artBackground.transform, "AnimeChip", new Color(0, 0, 0, 0.7f));
        UIFactory.Place(view.animeChip.rectTransform, new Vector2(1, 0), new Vector2(-8, 8), new Vector2(180, 34));
        view.animeText = UIFactory.CreateText(view.animeChip.transform, "Text", "", 16, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleCenter, FontStyle.Italic);
        UIFactory.Stretch(view.animeText.rectTransform, 6, 0, 6, 0);
        UIFactory.BestFit(view.animeText, 9);
        view.animeChip.gameObject.SetActive(false);

        // Name + talent
        view.nameText = UIFactory.CreateText(innerRt, "Name", "Card", 30, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(view.nameText.rectTransform, 342, 42, 12, 12);
        UIFactory.BestFit(view.nameText, 14);
        UIFactory.AddShadow(view.nameText, UIFactory.ShadowColor, 2f);

        view.talentText = UIFactory.CreateText(innerRt, "Talent", "", 21, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.TopStrip(view.talentText.rectTransform, 384, 30, 12, 12);
        UIFactory.BestFit(view.talentText, 12);

        // Bars
        view.hpBar = StatBar.Build(innerRt, "HPBar", UIFactory.HpColor, 18);
        UIFactory.TopStrip((RectTransform)view.hpBar.transform, 420, 32, 14, 14);
        view.hpBar.format = "HP {0} / {1}";

        view.manaBar = StatBar.Build(innerRt, "ManaBar", UIFactory.ManaColor, 14);
        UIFactory.TopStrip((RectTransform)view.manaBar.transform, 456, 20, 14, 14);
        view.manaBar.format = "MANA {0} / {1}";

        // Stats row
        var stats = UIFactory.CreateRect("Stats", innerRt);
        UIFactory.TopStrip(stats, 482, 34, 10, 10);
        view.atkText = UIFactory.CreateText(stats, "ATK", "ATK 0", 22, UIFactory.TextMain);
        UIFactory.Region(view.atkText.rectTransform, new Vector2(0f, 0f), new Vector2(0.333f, 1f));
        view.defText = UIFactory.CreateText(stats, "DEF", "DEF 0", 22, UIFactory.TextMain);
        UIFactory.Region(view.defText.rectTransform, new Vector2(0.333f, 0f), new Vector2(0.666f, 1f));
        view.spdText = UIFactory.CreateText(stats, "SPD", "SPD 0", 22, UIFactory.TextMain);
        UIFactory.Region(view.spdText.rectTransform, new Vector2(0.666f, 0f), new Vector2(1f, 1f));

        // Status line
        view.statusText = UIFactory.CreateText(innerRt, "Status", "", 18, UIFactory.TextMain, TextAnchor.UpperCenter);
        UIFactory.TopStrip(view.statusText.rectTransform, 520, 80, 12, 12);

        view.flash = UIFactory.CreatePanel(body, "Flash", new Color(1, 1, 1, 0f), false);
        UIFactory.Stretch(view.flash.rectTransform);

        return view;
    }
}
