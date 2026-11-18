using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Element chart from the design doc (section 5), from the attacker's side:
///  - 5-cycle Fire > Tree > Rock > Lightning > Water > Fire: +25% on the element you beat, -25% on the one that beats you
///  - Dark and Light: +25% against each other, both ways
///  - same element: -12.5%
///  - Non-Neutral attacking: +12.5% against everything, except -12.5% against Neutral (and Non-Neutral)
///  - Neutral: unaffected (0%), except Neutral attacking Non-Neutral = +25%
/// Element Orb odds: each cycle element 13%, Dark 11%, Light 11%, Neutral 8%, Non-Neutral 5%.
/// </summary>
public static class ElementSystem
{
    public const float Advantage = 1.25f;
    public const float Disadvantage = 0.75f;
    public const float SameElement = 0.875f;
    public const float NonNeutralBonus = 1.125f;
    public const float Neutral = 1.0f;

    static readonly CardElement[] Cycle = { CardElement.Fire, CardElement.Tree, CardElement.Rock, CardElement.Lightning, CardElement.Water };

    /// <summary>Display order used by the UI (same as the doc's table).</summary>
    public static readonly CardElement[] All =
    {
        CardElement.Fire, CardElement.Tree, CardElement.Rock, CardElement.Lightning, CardElement.Water,
        CardElement.Dark, CardElement.Light, CardElement.NonNeutral, CardElement.Neutral
    };

    /// <summary>Element Orb odds in percent (same order as <see cref="All"/>).</summary>
    public static readonly int[] OrbWeights = { 13, 13, 13, 13, 13, 11, 11, 5, 8 };

    static int CycleIndex(CardElement e) => Array.IndexOf(Cycle, e);

    public static float GetElementMultiplier(CardElement attacker, CardElement defender)
    {
        if (attacker == CardElement.NonNeutral)
            return defender == CardElement.Neutral || defender == CardElement.NonNeutral ? SameElement : NonNeutralBonus;
        if (attacker == CardElement.Neutral)
            return defender == CardElement.NonNeutral ? Advantage : Neutral;
        if (defender == CardElement.NonNeutral || defender == CardElement.Neutral)
            return Neutral;
        if (attacker == defender)
            return SameElement;
        if ((attacker == CardElement.Dark && defender == CardElement.Light) || (attacker == CardElement.Light && defender == CardElement.Dark))
            return Advantage;

        int a = CycleIndex(attacker), d = CycleIndex(defender);
        if (a >= 0 && d >= 0)
        {
            if ((a + 1) % Cycle.Length == d) return Advantage;
            if ((d + 1) % Cycle.Length == a) return Disadvantage;
        }
        return Neutral;
    }

    /// <summary>Elements this one deals bonus damage to.</summary>
    public static List<CardElement> StrongAgainst(CardElement element)
    {
        var list = new List<CardElement>();
        foreach (var e in All) if (GetElementMultiplier(element, e) > 1.01f) list.Add(e);
        return list;
    }

    /// <summary>Kept for older callers: the first element this one beats, or null.</summary>
    public static CardElement? Beats(CardElement element)
    {
        var list = StrongAgainst(element);
        return list.Count > 0 ? list[0] : (CardElement?)null;
    }

    public static string DisplayName(CardElement element) => element == CardElement.NonNeutral ? "Non-Neutral" : element.ToString();

    public static string ShortName(CardElement element)
    {
        switch (element)
        {
            case CardElement.Fire: return "FIRE";
            case CardElement.Tree: return "TREE";
            case CardElement.Rock: return "ROCK";
            case CardElement.Lightning: return "BOLT";
            case CardElement.Water: return "WATER";
            case CardElement.Dark: return "DARK";
            case CardElement.Light: return "LIGHT";
            case CardElement.NonNeutral: return "NON-N";
            default: return "NEUT";
        }
    }

    public static Color GetColor(CardElement element)
    {
        switch (element)
        {
            case CardElement.Fire: return new Color(1.00f, 0.38f, 0.22f);
            case CardElement.Tree: return new Color(0.40f, 0.85f, 0.35f);
            case CardElement.Rock: return new Color(0.72f, 0.56f, 0.38f);
            case CardElement.Lightning: return new Color(1.00f, 0.86f, 0.20f);
            case CardElement.Water: return new Color(0.25f, 0.62f, 1.00f);
            case CardElement.Dark: return new Color(0.60f, 0.38f, 0.95f);
            case CardElement.Light: return new Color(1.00f, 0.96f, 0.72f);
            case CardElement.NonNeutral: return new Color(1.00f, 0.45f, 0.85f);
            default: return new Color(0.75f, 0.78f, 0.82f);
        }
    }

    /// <summary>"+25%", "-12.5%", "0%".</summary>
    public static string Describe(float multiplier)
    {
        float pct = (multiplier - 1f) * 100f;
        if (Mathf.Abs(pct) < 0.01f) return "0%";
        return (pct > 0 ? "+" : "") + pct.ToString("0.#") + "%";
    }

    /// <summary>Random element with the Element Orb odds.</summary>
    public static CardElement RollRandom()
    {
        return Pick(UnityEngine.Random.Range(0, 100));
    }

    static CardElement Pick(int roll)
    {
        int acc = 0;
        for (int i = 0; i < All.Length; i++)
        {
            acc += OrbWeights[i];
            if (roll < acc) return All[i];
        }
        return CardElement.Neutral;
    }

    /// <summary>Deterministic element per character name, distributed with the Element Orb odds (enemy floors).</summary>
    public static CardElement FromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return CardElement.Neutral;
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in name.Trim().ToLowerInvariant())
            {
                hash ^= c;
                hash *= 16777619;
            }
            return Pick((int)(hash % 100));
        }
    }
}
