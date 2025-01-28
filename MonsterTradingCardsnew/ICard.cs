using System.Security.Cryptography;

namespace MonsterTradingCardsnew;

public interface ICard
{
    public string Name { get; set; }

    public int Damage { get; set; }

    public Element ElementType { get; set; }
}