using System;

/// <summary>A card's build: its Roll on each stat, its element and its talent (doc sections 3, 5, 6, 10).</summary>
[Serializable]
public class BuildData
{
    public int hp;
    public int atk;
    public int def;
    public int spd;
    public CardElement element;
    public string talent;
    /// <summary>God cards only: the passive talent (talent is then the active one).</summary>
    public string talent2;
    /// <summary>Name given by the player (empty = the character's name). Each saved copy keeps its own.</summary>
    public string name;

    public BuildData Clone() => (BuildData)MemberwiseClone();

    public int TotalRoll => hp + atk + def + spd;
}

/// <summary>One owned card. The character (key) is its art/identity; power comes from rarity + build.</summary>
[Serializable]
public class CardInstance
{
    public int uid;
    public string key;
    public CardRarity rarity;
    public BuildData build = new BuildData();
    public bool locked;
    /// <summary>Successful God Orbs used on this card (+1% to every stat each, God cards only).</summary>
    public int godBonus;

    public int Tier => GameRules.Tier(rarity);
}

/// <summary>A saved build slot (doc section 10): which card, plus its own Roll/element/talent.</summary>
[Serializable]
public class BuildSlot
{
    public bool unlocked;
    public int cardUid;
    public BuildData build = new BuildData();

    public bool HasCard => cardUid > 0;
}
