using Tav.Models;

namespace Tav.Store;

public interface IMonsterStore
{
    List<Monster> LoadAll();
}

public class MonsterStore : IMonsterStore
{
    public List<Monster> LoadAll() =>
        EmbeddedJsonResource.DeserializeList<Monster>("monsters.json", "res/monsters.json");
}
