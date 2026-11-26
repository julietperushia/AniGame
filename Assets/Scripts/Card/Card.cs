using UnityEngine;

[CreateAssetMenu(fileName = "Card", menuName = "Scriptable Objects/Card")]
public class Card : ScriptableObject
{
    public string cardName;
    public Sprite cardImg;
    public CardElement element;
    public CardRarity rarity;

    public Talent talent;
    [Tooltip("God cards only: second talent (one active + one passive).")]
    public Talent talent2;

    [Tooltip("Stored already multiplied by 10 (Anigame shows HP stat x10).")]
    public int health;
    public int defense;
    public int attack;
    public int speed;
    public int critRate = 5;
    public int critDamage = 135;
    public int evasion = 5;
    public int mana;
    public int maxMana = 100;
    public int power;

    [Header("Campaign placement (filled by the importer / AniGame setup)")]
    [Tooltip("LOC number (1-100). 0 = not part of the campaign.")]
    public int location;
    [Tooltip("Floor number inside the location (1-based).")]
    public int floor;
    [Tooltip("Original code from the CSV, e.g. A1 (letter = character, number = tier).")]
    public string floorCode;
    [Tooltip("Anime / series the character comes from (sheet column ANIME).")]
    public string anime;
    [Tooltip("Source image URL (sheet column IMAGE). Used by the generator to download art and as a runtime fallback.")]
    public string imageUrl;
    [Tooltip("Character id taken from the image URL; stable across re-generations.")]
    public string characterId;

    [Header("Hidden Location (3v3)")]
    [Tooltip("Enemy of the Hidden Location (3v3) instead of the normal campaign.")]
    public bool hidden;
    [Tooltip("Hidden Location: position in the enemy team (0-2).")]
    public int teamSlot;

    /// <summary>Stable id used by save files. "L12F5" for campaign cards, "H3F2S1" for Hidden Location enemies, asset name otherwise.</summary>
    public string Key => hidden ? $"H{location}F{floor}S{teamSlot}" : location > 0 ? $"L{location}F{floor}" : name;

    public string DisplayName => string.IsNullOrEmpty(cardName) ? name : cardName;

    /// <summary>Sum of the four main stats on the Anigame scale (HP counted /10).</summary>
    public int StatTotal => health / 10 + attack + defense + speed;
}
