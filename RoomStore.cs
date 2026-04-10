namespace Tav;

public interface IRoomStore
{
    List<Room> LoadAll();
}

public class RoomStore : IRoomStore
{
    public List<Room> LoadAll() =>
        EmbeddedJsonResource.DeserializeList<Room>("rooms.json", "res/rooms.json");
}
