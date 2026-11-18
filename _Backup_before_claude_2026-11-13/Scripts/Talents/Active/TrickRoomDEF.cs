using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/TrickRoomDEF")]
public class TrickRoomDEF : ActiveTalent
{
    public int swapPercent = 60;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        if (enemy.TotalDefense > self.TotalDefense)
        {
            int difference = enemy.TotalDefense - self.TotalDefense;
            int swapAmount = Mathf.RoundToInt(difference * swapPercent / 100f);
            
            self.defense += swapAmount;
            enemy.defense -= swapAmount;
            
            Debug.Log($"<color=cyan>[TrickRoom DEF] Swapped {swapAmount} DEF! (You: {self.TotalDefense}, Enemy: {enemy.TotalDefense})</color>");
        }
        else
        {
            Debug.Log($"<color=cyan>[TrickRoom DEF] No swap - your DEF is already higher!</color>");
        }
    }
}
