using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/DeathsDance")]
public class DeathsDance : PassiveTalent
{
    [Tooltip("Part of all damage taken that is stored instead (0.2 = 20%).")]
    public float storeRate = 0.20f;
    [Tooltip("The store rate can't go above this, even with rarity bonus (0.6 = 60%).")]
    public float maxStoreRate = 0.6f;
    [Tooltip("Part of the stored damage removed when you evade (0.3 = 30%).")]
    public float evadeClear = 0.3f;
    [Tooltip("Part of the stored damage removed by each of your attacks that hits (0.1 = 10%).")]
    public float attackClear = 0.1f;
    [Tooltip("Extra part of the stored damage removed when that attack is a critical hit (0.15 = 15%).")]
    public float critClear = 0.15f;

    public override string DefaultName => "Death's Dance";
    public override string DefaultDescription =>
        $"{PctF(storeRate, maxStoreRate)} of all damage you take is stored instead of hitting your HP. If the stored damage ever reaches your current HP, you fall at once. " +
        $"Each evade removes {P(evadeClear)} of the stored damage; each of your attacks that hits removes {P(attackClear)}, and a critical hit removes another {P(critClear)}.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) => self.damageStoreRate = Mathf.Min(maxStoreRate, Scaled(storeRate, self));

    public override void OnEvade(CardRuntime self, CardRuntime attacker)
    {
        if (self.storedDamage <= 0) return;
        self.storedDamage = Mathf.RoundToInt(self.storedDamage * (1f - Mathf.Clamp01(evadeClear)));
        BattleLog.Passive(self, $"<color=#B39DDB>[Death's Dance] Stored damage -{evadeClear * 100f:0}% ({self.storedDamage})</color>");
    }

    public override void OnAfterAttack(CardRuntime self, CardRuntime enemy)
    {
        if (self.storedDamage <= 0) return;
        float keep = 1f - Mathf.Clamp01(attackClear);
        if (self.lastAttackWasCrit) keep *= 1f - Mathf.Clamp01(critClear);
        if (keep >= 1f) return;
        self.storedDamage = Mathf.RoundToInt(self.storedDamage * keep);
        BattleLog.Passive(self, $"<color=#B39DDB>[Death's Dance] Stored damage -{(1f - keep) * 100f:0.#}% ({self.storedDamage})</color>");
    }
}
