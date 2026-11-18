using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Paralysis")]
public class Paralysis : ActiveTalent
{
    public int stunChance = 40;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int roll = Random.Range(0, 100);
        
        if (roll < stunChance)
        {
            StatusEffects.ApplyStun(enemy);
            Debug.Log($"<color=yellow>[Paralysis] Enemy is STUNNED! (Skip turn, -10% DEF permanently)</color>");
        }
        else
        {
            Debug.Log($"<color=yellow>[Paralysis] Stun failed! ({roll}/{stunChance})</color>");
        }
    }
}
