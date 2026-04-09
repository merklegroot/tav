namespace Tav;

public static class KnownManipulativeIds
{
    public static readonly string Apple = "apple";
    public static readonly string Torch = "torch";
    public static readonly string BoneShard = "bone_shard";

    /// <summary>Human-readable label for UI. Unknown ids fall back to spaced text.</summary>
    public static string DisplayName(string manipulativeId)
    {
        if (string.Equals(manipulativeId, Apple, StringComparison.OrdinalIgnoreCase))
            return "Apple";
        if (string.Equals(manipulativeId, Torch, StringComparison.OrdinalIgnoreCase))
            return "Torch";
        if (string.Equals(manipulativeId, BoneShard, StringComparison.OrdinalIgnoreCase))
            return "Bone shard";
        return manipulativeId.Replace('_', ' ');
    }
}