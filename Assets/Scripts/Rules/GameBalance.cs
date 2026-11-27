using UnityEngine;

/// <summary>
/// Tunable battle numbers, edited in the Inspector (Assets/Resources/GameBalance.asset).
/// HP multiplier: HP = HP stat x this. Damage does not change, so a higher multiplier means longer battles.
/// Test Mode in the main menu can override it on this PC (saved in PlayerPrefs, not in the save file).
/// </summary>
[CreateAssetMenu(fileName = "GameBalance", menuName = "AniGame/Game Balance")]
public class GameBalance : ScriptableObject
{
    [Tooltip("HP = HP stat x this (enemies and your cards). 10 = the original Anigame scale; 20 = battles about twice as long.")]
    [Range(1, 200)] public int hpMultiplier = 10;

    [Tooltip("Round limit and enrage start grow with the HP multiplier (x multiplier / 10), so long battles don't end in draws.")]
    public bool scaleRoundsWithHp = true;
    [Tooltip("Rounds before a draw at HP multiplier 10.")]
    public int maxRounds = 50;
    [Tooltip("Round when both fighters start to enrage at HP multiplier 10.")]
    public int enrageStartRound = 20;
    [Tooltip("ATK gained per round of enrage (% of base ATK).")]
    public int enragePercent = 10;

    [Header("Healing fatigue (every battle)")]
    [Tooltip("After this round, all healing received drops a little more every round (0 = off).")]
    public int healingFatigueAfterRound = 10;
    [Tooltip("Healing lost per round after that, in %. 5 = round 11: -5%, round 12: -10% ... round 30: no healing at all.")]
    public int healingFatiguePerRound = 5;

    [Header("3v3 team battles")]
    [Tooltip("Normal attack damage x this in 3v3. Team ATK and DEF are the totals of 3 cards, which makes every hit about 3x a 1v1 hit, " +
             "and 3 cards attack per round. 0.33 keeps 3v3 battles about as long as 1v1; 1 = no change (much shorter battles).")]
    [Range(0.05f, 1f)] public float teamAttackScale = 0.33f;

    [Header("Hidden Location (3v3)")]
    [Tooltip("Cards of Mythic rarity or higher needed to unlock the Hidden Location.")]
    public int hiddenUnlockCards = 3;
    [Tooltip("Gold per win = this x LOC + Gold per floor x floor (first clear of a floor).")]
    public int hiddenGoldPerLoc = 1500;
    public int hiddenGoldPerFloor = 500;
    [Tooltip("Replaying a floor you already cleared gives this % of the gold and no diamonds.")]
    public int hiddenReplayGoldPercent = 25;
    [Tooltip("Diamonds for the first clear of a floor = this x LOC + Diamonds per floor x floor.")]
    public int hiddenDiamondsPerLoc = 20;
    public int hiddenDiamondsPerFloor = 10;

    static GameBalance current;

    public static GameBalance Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<GameBalance>("GameBalance");
                if (current == null) current = CreateInstance<GameBalance>();
            }
            return current;
        }
    }
}
