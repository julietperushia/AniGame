using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/ManaReaver")]
public class ManaReaver : ActiveTalent
{
    public int manaStealPercent = 20;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int manaToSteal = Mathf.RoundToInt(enemy.mana * manaStealPercent / 100f);
        enemy.mana -= manaToSteal;
        self.mana += manaToSteal;
        
        int damage = manaToSteal * 10;
        enemy.TakeTrueDamage(damage);
        self.lastDamageDealt = damage;
        
        Debug.Log($"<color=purple>[ManaReaver] Stole {manaToSteal} mana, dealt {damage} true damage!</color>");
    }
}
