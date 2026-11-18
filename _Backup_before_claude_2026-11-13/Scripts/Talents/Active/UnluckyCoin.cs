using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/UnluckyCoin")]
public class UnluckyCoin : ActiveTalent
{
    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int roll = Random.Range(1, 21);
        int reduction = roll * 2;
        int statChoice = Random.Range(0, 4);
        
        switch (statChoice)
        {
            case 0:
                enemy.maxHealth -= reduction;
                enemy.health = Mathf.Min(enemy.health, enemy.maxHealth);
                Debug.Log($"<color=red>[UnluckyCoin] Rolled {roll}! Enemy -{reduction} Max HP!</color>");
                break;
                
            case 1:
                enemy.attack -= reduction;
                Debug.Log($"<color=red>[UnluckyCoin] Rolled {roll}! Enemy -{reduction} ATK!</color>");
                break;
                
            case 2:
                enemy.defense -= reduction;
                Debug.Log($"<color=red>[UnluckyCoin] Rolled {roll}! Enemy -{reduction} DEF!</color>");
                break;
                
            case 3:
                enemy.speed -= reduction;
                Debug.Log($"<color=red>[UnluckyCoin] Rolled {roll}! Enemy -{reduction} SPD!</color>");
                break;
        }
    }
}
