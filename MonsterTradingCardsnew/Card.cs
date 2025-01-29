namespace MonsterTradingCardsnew;

public class Card : ICard
{
    public string Name { get; set; }
    public float Damage { get; set; }
    public Element ElementType { get; set; }

    public string CardType { get; set; }
    
    public string? MonsterType { get; set; } 

    public Card(string name, float damage, Element elementType, string card_type, string? monsterType = null)
    {
        Name = name;
        Damage = damage;
        ElementType = elementType;
        CardType = card_type;
        MonsterType = (card_type == "Monster") ? monsterType : null;
    }
}