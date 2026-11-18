using UnityEngine;

/// <summary>
/// Handles element interactions for damage multipliers (Anigame formula)
/// Fire > Earth > Air > Water > Fire (circular)
/// Light <-> Dark (mutual advantage)
/// </summary>
public static class ElementSystem
{
    /// <summary>
    /// Calculates the element multiplier based on attacker and defender elements
    /// </summary>
    /// <returns>
    /// 1.5f for advantage, 0.5f for disadvantage, 0.75f for same element, 1.0f for neutral
    /// </returns>
    public static float GetElementMultiplier(CardElement attacker, CardElement defender)
    {
        // Same element = 0.75x
        if (attacker == defender)
        {
            return 0.75f;
        }

        // Check advantages
        switch (attacker)
        {
            case CardElement.Fire:
                if (defender == CardElement.Earth) return 1.5f;  // Fire > Earth
                if (defender == CardElement.Water) return 0.5f;  // Fire < Water
                break;

            case CardElement.Earth:
                if (defender == CardElement.Air) return 1.5f;    // Earth > Air
                if (defender == CardElement.Fire) return 0.5f;   // Earth < Fire
                break;

            case CardElement.Air:
                if (defender == CardElement.Water) return 1.5f;  // Air > Water
                if (defender == CardElement.Earth) return 0.5f;  // Air < Earth
                break;

            case CardElement.Water:
                if (defender == CardElement.Fire) return 1.5f;   // Water > Fire
                if (defender == CardElement.Air) return 0.5f;    // Water < Air
                break;

            case CardElement.Light:
                if (defender == CardElement.Dark) return 1.5f;   // Light > Dark
                break;

            case CardElement.Dark:
                if (defender == CardElement.Light) return 1.5f;  // Dark > Light
                break;
        }

        // Neutral matchup
        return 1.0f;
    }
}
