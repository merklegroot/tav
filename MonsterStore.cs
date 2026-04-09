namespace Tav;

internal static class MonsterStore
{
    public static List<Monster> LoadAll() =>
        EmbeddedJsonResource.DeserializeList<Monster>("monsters.json", "res/monsters.json");
}
