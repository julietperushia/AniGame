using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Mimic")]
public class Mimic : ActiveTalent
{
    public float healPercent = 0.10f;
    const string Key = "MimicCopied";

    public override string DefaultName => "Mimic";
    public override string DefaultDescription => $"Copies the enemy's talent. An active talent: you cast it yourself. A passive talent: you keep it for the rest of the battle; once you have it, later casts heal you {healPercent * 100:0}% max HP instead.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        bool did = false;
        var theirActive = enemy.Actives().FirstOrDefault(a => !(a is Mimic));
        if (theirActive != null)
        {
            BattleLog.Info($"<color=#CE93D8>[Mimic] {self.DisplayName} copies {theirActive.DisplayName}!</color>");
            theirActive.Activate(self, enemy);
            did = true;
        }
        var passive = enemy.Passives().FirstOrDefault(p => !self.HasTalent(p));
        if (passive != null && !self.GetTalentState(Key, false))
        {
            self.SetTalentState(Key, true);
            self.AddTalent(passive);
            BattleLog.Info($"<color=#CE93D8>[Mimic] {self.DisplayName} learns {passive.DisplayName} for the rest of the battle!</color>");
            if (passive is Nothingness)
            {
                BattleLog.Info($"<color=#9E9E9E>[Nothingness] Copying nothing has a price...</color>");
                self.TakeTrueDamage(Mathf.RoundToInt(self.maxHealth * 0.3f), DamageKind.True);
            }
            BattleLog.Current?.InitTalent(self, enemy, passive);
            did = true;
        }
        if (!did || (theirActive == null && passive == null))
            self.Heal(Mathf.RoundToInt(self.maxHealth * Scaled(healPercent, self)));
    }
}
