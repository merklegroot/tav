using Tav;

namespace Tav.Store;

public interface IManipulativeImageStore
{
    /// <summary>Lines from embedded <c>{imageStem}.ans</c>, or empty when missing.</summary>
    IEnumerable<string> Lines(string imageStem);
}

public class ManipulativeImageStore : IManipulativeImageStore
{
    public IEnumerable<string> Lines(string imageStem) => EmbeddedImgTxtResource.ReadLines(imageStem);
}
