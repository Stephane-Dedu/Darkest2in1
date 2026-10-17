using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Tests;

public class CorridorDoorTests
{
    internal static Crawl Hall(int count=3,bool reverse=false) => new(new ExpeditionState
    {
        Started=true,RoomId=-1,CorridorId=0,TileIndex=reverse ? count-1 : 0,HeadingRoomId=reverse ? 0 : 1,
        Quest=new QuestOffer {Dungeon="crypts",Type="explore"},Party={"a"},
        Map=new DungeonMap {Rooms={new Room {Id=0,CorridorIds={0}},new Room {Id=1,CorridorIds={0}}},
            Corridors={new Corridor {Id=0,RoomA=0,RoomB=1,Tiles=Enumerable.Range(0,count).Select(i=>new HallTile {Index=i}).ToList()}}}
    },new CrawlRules(),new FakeParty("a"));

    [Theory]
    [InlineData(false,true,2)]
    [InlineData(true,true,0)]
    [InlineData(false,false,0)]
    [InlineData(true,false,2)]
    public void WalkingPastEitherHallEndWaitsForAnExplicitDoorAction(bool reverse,bool forward,int tile)
    {
        var crawl=Hall(reverse:reverse);crawl.State.TileIndex=tile;
        string before=new SaveFile {Expedition=crawl.State}.ToJson();
        var events=crawl.Step(forward);
        Assert.False(crawl.State.InRoom);
        Assert.DoesNotContain(events,e=>e.Type==CrawlEventType.EnteredRoom);
        Assert.Equal(before,new SaveFile {Expedition=crawl.State}.ToJson());
    }

    [Theory]
    [InlineData(false,1)]
    [InlineData(true,0)]
    public void DoorEntryVisitsTheCorrectRoomOnceAndRetainsItsRetreatSquare(bool reverse,int destination)
    {
        var crawl=Hall(reverse:reverse);crawl.State.TileIndex=reverse ? 0 : 2;
        var loaded=SaveFile.FromJson(new SaveFile {Expedition=crawl.State}.ToJson()).Expedition;
        crawl=new Crawl(loaded,new CrawlRules(),new FakeParty("a"));
        Assert.True(crawl.CanEnterDoor(destination));
        Assert.Contains(crawl.EnterDoor(destination),e=>e.Type==CrawlEventType.EnteredRoom && e.RoomId==destination);
        Assert.Equal(destination,loaded.RoomId);Assert.Equal(0,loaded.CameFromCorridorId);
        Assert.Equal(reverse ? 0 : 2,loaded.CameFromTileIndex);
        int rng=loaded.RandomCounter;
        Assert.False(crawl.CanEnterDoor(destination));
        Assert.DoesNotContain(crawl.EnterDoor(destination),e=>e.Type==CrawlEventType.EnteredRoom);
        Assert.Equal(rng,loaded.RandomCounter);
    }
    [Fact] public void DoorCannotBypassAnEncounterOrModalAndStillAllowsBackingAwayFromATrap()
    {
        var crawl=Hall(1);var tile=crawl.CurrentTile;
        tile.Content=HallContent.Battle;Assert.False(crawl.CanEnterDoor(0));Assert.False(crawl.CanEnterDoor(1));
        tile.Content=HallContent.Trap;tile.Scouted=true;
        Assert.True(crawl.CanEnterDoor(0));Assert.False(crawl.CanEnterDoor(1));
        crawl.State.PendingSpoils=new BattleSpoils();Assert.False(crawl.CanEnterDoor(0));
        crawl.State.PendingSpoils=null;
        crawl.EnterDoor(0);Assert.False(tile.Resolved);
        Assert.True(crawl.State.InRoom);
    }
}
