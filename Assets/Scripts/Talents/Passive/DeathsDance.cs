using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/DeathsDance")]
public class DeathsDance : PassiveTalent
{
    public float storeRate = 0.20f;

    public override string DefaultName => "Death's Dance";
    public override string DefaultDescription => $"{storeRate * 100:0}% of all damage you take is stored instead of hitting your HP. If the stored damage ever reaches your current HP, you fall at once. Each evade removes 30% of the stored damage; each critical hit you land removes 15%.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) => self.damageStoreRate = Mathf.Min(0.6f, Scaled(storeRate, self));

    public override void OnEvade(CardRuntime self, CardRuntime attacker)
    {
        if (self.storedDamage <= 0) return;
        self.storedDamage = Mathf.RoundToInt(self.storedDamage * 0.7f);
        BattleLog.Passive(self, $"<color=#B39DDB>[Death's Dance] Stored damage -30% ({self.storedDamage})</color>");
    }

    public override void OnAfterAttack(CardRuntime self, CardRuntime enemy)
    {
        if (!self.lastAttackWasCrit || self.storedDamage <= 0) return;
        self.storedDamage = Mathf.RoundToInt(self.storedDamage * 0.85f);
        BattleLog.Passive(self, $"<color=#B39DDB>[Death's Dance] Stored damage -15% ({self.storedDamage})</color>");
    }
}
