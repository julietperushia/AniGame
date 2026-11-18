using UnityEngine;

/// <summary>
/// Handles element interactions for damage multipliers.
/// Fire > Earth > Air > Water > Fire (circular)
/// Light <-> Dark (mutual advantage)
/// 1.5x advantage, 0.5x disadvantage, 0.75x same element, 1.0x neutral.
/// (The live Anigame bot uses 8 elements: fire, grass, ground, electric, water, light, dark, neutral.
///  This project keeps its own 6-element wheel - see the design doc.)
/// </summary>
public static class ElementSystem
{
    public const float Advantage = 1.5f;
    public const float Disadvantage = 0.5f;
    public const float SameElement = 0.75f;
    public const float Neutral = 1.0f;

    /// <summary>
    /// Calculates the element multiplier based on attacker and defender elements.
    /// </summary>
    public static float GetElementMultiplier(CardElement attacker, CardElement defender)
    {
        if (attacker == defender)
        {
            return SameElement;
        }

        switch (attacker)
        {
            case CardElement.Fire:
                if (defender == CardElement.Earth) return Advantage;   // Fire > Earth
                if (defender == CardElement.Water) return Disadvantage; // Fire < Water
                break;

            case CardElement.Earth:
                if (defender == CardElement.Air) return Advantage;     // Earth > Air
                if (defender == CardElement.Fire) return Disadvantage;  // Earth < Fire
                break;

            case CardElement.Air:
                if (defender == CardElement.Water) return Advantage;   // Air > Water
                if (defender == CardElement.Earth) return Disadvantage; // Air < Earth
                break;

            case CardElement.Water:
                if (defender == CardElement.Fire) return Advantage;    // Water > Fire
                if (defender == CardElement.Air) return Disadvantage;   // Water < Air
                break;

            case CardElement.Light:
                if (defender == CardElement.Dark) return Advantage;    // Light > Dark
                break;

            case CardElement.Dark:
                if (defender == CardElement.Light) return Advantage;   // Dark > Light
                break;
        }

        return Neutral;
    }

    /// <summary>The element this one beats (or null for none).</summary>
    public static CardElement? Beats(CardElement element)
    {
        switch (element)
        {
            case CardElement.Fire: return CardElement.Earth;
            case CardElement.Earth: return CardElement.Air;
            case CardElement.Air: return CardElement.Water;
            case CardElement.Water: return CardElement.Fire;
            case CardElement.Light: return CardElement.Dark;
            case CardElement.Dark: return CardElement.Light;
        }
        return null;
    }

    public static Color GetColor(CardElement element)
    {
        switch (element)
        {
            case CardElement.Fire: return new Color(1.00f, 0.42f, 0.25f);
            case CardElement.Water: return new Color(0.25f, 0.62f, 1.00f);
            case CardElement.Earth: return new Color(0.62f, 0.80f, 0.30f);
            case CardElement.Air: return new Color(0.55f, 0.95f, 0.90f);
            case CardElement.Light: return new Color(1.00f, 0.92f, 0.55f);
            case CardElement.Dark: return new Color(0.62f, 0.40f, 0.95f);
        }
        return Color.white;
    }

    /// <summary>Short label for the multiplier, e.g. "ADVANTAGE x1.5".</summary>
    public static string Describe(float multiplier)
    {
        if (multiplier > 1.01f) return $"ADVANTAGE x{multiplier:0.##}";
        if (multiplier < 0.51f) return $"WEAK x{multiplier:0.##}";
        if (multiplier < 0.99f) return $"RESIST x{multiplier:0.##}";
        return "NEUTRAL x1";
    }

    /// <summary>Deterministic element for cards whose data has no element column (stable per character name).</summary>
    public static CardElement FromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return CardElement.Fire;
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in name.Trim().ToLowerInvariant())
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (CardElement)(hash % 6);
        }
    }
}
