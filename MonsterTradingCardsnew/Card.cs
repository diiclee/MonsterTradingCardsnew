namespace MonsterTradingCardsnew;

public class Card : ICard
{
    public string Name { get; set; }
    public int Damage { get; set; }
    public Element ElementType { get; set; }
    
    public string CardType { get; set; }

    public Card(string name, int damage,  Element elementType, string card_type)
    {
        Name = name;
        Damage = damage;
        ElementType = elementType;
        CardType = card_type;
    }
    
    
}