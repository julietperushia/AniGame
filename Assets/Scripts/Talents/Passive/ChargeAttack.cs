using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/ChargeAttack")]
public class ChargeAttack : PassiveTalent
{
    [Tooltip("You store attacks while your HP is above this (0.3 = 30%).")]
    public float hpThreshold = 0.3f;
    [Tooltip("Most attacks stored before they are released (keeps two charging cards from stalling forever).")]
    public int maxStored = 4;
    [Tooltip("The release adds this much of the stored damage (1.3 = 130%).")]
    public float releaseMultiplier = 1.3f;

    public override string DefaultName => "Charge Attack";
    public override string DefaultDescription =>
        $"While your HP is above {P(hpThreshold)}, your normal attacks are stored instead of hitting (up to {maxStored}). When {maxStored} are stored, " +
        $"or once your HP is {P(hpThreshold)} or lower, your next attack releases them: its normal damage plus {P(releaseMultiplier)} of all the stored damage.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.chargeAttackPassive = true;
        self.chargeAttackThreshold = hpThreshold;
        self.chargeAttackMax = Mathf.Max(1, maxStored);
        self.chargeAttackRelease = releaseMultiplier;
    }
}
