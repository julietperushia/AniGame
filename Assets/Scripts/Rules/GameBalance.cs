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
