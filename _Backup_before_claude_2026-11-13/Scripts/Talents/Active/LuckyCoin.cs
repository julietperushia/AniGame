using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/LuckyCoin")]
public class LuckyCoin : ActiveTalent
{
    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int roll = Random.Range(1, 21);
        int bonus = roll * 3;
        int statChoice = Random.Range(0, 4);
        
        switch (statChoice)
        {
            case 0:
                self.maxHealth += bonus;
                self.health += bonus;
                Debug.Log($"<color=green>[LuckyCoin] Rolled {roll}! +{bonus} Max HP!</color>");
                break;
                
            case 1:
                self.attack += bonus;
                Debug.Log($"<color=green>[LuckyCoin] Rolled {roll}! +{bonus} ATK!</color>");
                break;
                
            case 2:
                self.defense += bonus;
                Debug.Log($"<color=green>[LuckyCoin] Rolled {roll}! +{bonus} DEF!</color>");
                break;
                
            case 3:
                self.speed += bonus;
                Debug.Log($"<color=green>[LuckyCoin] Rolled {roll}! +{bonus} SPD!</color>");
                break;
        }
    }
}
