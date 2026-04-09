namespace Tav;

internal static class RoomStore
{
    public static List<Room> LoadAll() =>
        EmbeddedJsonResource.DeserializeList<Room>("rooms.json", "rooms.json");
}
