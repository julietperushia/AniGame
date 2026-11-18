using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/TrickRoomATK")]
public class TrickRoomATK : ActiveTalent
{
    public int swapPercent = 60;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        if (enemy.TotalAttack > self.TotalAttack)
        {
            int difference = enemy.TotalAttack - self.TotalAttack;
            int swapAmount = Mathf.RoundToInt(difference * swapPercent / 100f);
            
            self.attack += swapAmount;
            enemy.attack -= swapAmount;
            
            Debug.Log($"<color=cyan>[TrickRoom ATK] Swapped {swapAmount} ATK! (You: {self.TotalAttack}, Enemy: {enemy.TotalAttack})</color>");
        }
        else
        {
            Debug.Log($"<color=cyan>[TrickRoom ATK] No swap - your ATK is already higher!</color>");
        }
    }
}
