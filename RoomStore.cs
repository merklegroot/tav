namespace Tav;

public static class RoomStore
{
    public static List<Room> LoadAll() =>
        EmbeddedJsonResource.DeserializeList<Room>("rooms.json", "res/rooms.json");
}
