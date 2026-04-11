using Tav;

namespace Tav.Store;

public interface IMonsterImageStore
{
    /// <summary>Lines of the portrait file, or empty when missing.</summary>
    IEnumerable<string> Lines(string monsterId);
}

public class MonsterImageStore : IMonsterImageStore
{
    public IEnumerable<string> Lines(string monsterId) => EmbeddedImgTxtResource.ReadLines(monsterId);
}
