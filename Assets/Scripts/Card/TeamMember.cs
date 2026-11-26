using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One card of a 3v3 team. The team shares one HP / ATK / DEF / mana / evasion pool (a team CardRuntime);
/// each card keeps its own SPD (turn order), element (its attacks), crit and active talent.
/// </summary>
public class TeamMember
{
    public string name = "Card";
    public CardRarity rarity = CardRarity.God;
    public CardElement element = CardElement.Neutral;
    public int baseSpeed = 100;
    public int critRate = 5;
    public int critDamage = 135;
    public int evasion = 5;
    public int hp, atk, def;           // this card's share of the team pool
    public readonly List<Talent> talents = new List<Talent>();
    public int index;                  // 0..2 inside its team
    public Card art;                   // optional (names / pictures)
    internal int nextActive;

    public int Tier => GameRules.Tier(rarity);

    /// <summary>Current SPD: this card's base SPD scaled by the team's SPD changes (Frostbite, Haste...).</summary>
    public int Speed(CardRuntime team)
    {
        if (team == null || team.baseSpeed <= 0) return Mathf.Max(1, baseSpeed);
        return Mathf.Max(1, Mathf.RoundToInt(baseSpeed * team.TotalSpeed / (float)team.baseSpeed));
    }

    /// <summary>Mana this card adds to the team pool: 100, or more when its active talent costs more (Soul Swap 200).</summary>
    public int PoolShare
    {
        get
        {
            int share = 100;
            foreach (var t in talents) if (t is ActiveTalent a) share = Mathf.Max(share, a.BaseCost);
            return share;
        }
    }

    public string TalentNames => string.Join(" + ", talents.ConvertAll(t => t != null ? t.DisplayName : "-"));
}
