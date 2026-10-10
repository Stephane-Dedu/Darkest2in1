using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Runtime;

// Only Unity's timed fade/audio and secret transition wrapper are replaced. The linked production
// navigation methods and real Core crawl own destination selection, route state and secret access.
internal sealed partial class Driver
{
    public Crawl Crawl { get; init; }
    public ExpeditionState Expedition => Crawl.State;
    private int _travelTo = -1;
    public int PendingTravel => _travelTo;
    private void BeginTravel(int roomId) => _travelTo = roomId;
    public bool EnterSecretRoom()
    {
        int before = Expedition.RoomId;
        Crawl.EnterSecretRoom();
        return Expedition.RoomId != before;
    }
    public bool ExitSecretRoom()
    {
        int before = Expedition.RoomId;
        Crawl.ExitSecretRoom();
        return Expedition.RoomId != before;
    }
}
