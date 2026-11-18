using UnityEngine;

[CreateAssetMenu(fileName = "Card", menuName = "Scriptable Objects/Card")]
public class Card : ScriptableObject
{
    public string cardName;
    public Sprite cardImg;
    public CardElement element;
    public CardRarity rarity;

    public Talent talent;  

    public int health;
    public int defense;
    public int attack;
    public int speed;
    public int critRate = 5;
    public int critDamage = 135;
    public int evasion = 5;
    public int mana;
    public int maxMana = 100;
    public int power;
}
