using System.Diagnostics;
using System.Security.Cryptography;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class NativeCurioArtTests
{
    private sealed class Pack : IDisposable
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"dd3-curio-"+Guid.NewGuid());
        private readonly string _previous=Plugin.NativeRoomSceneryPath.Value;
        public readonly byte[] Png=new byte[24];
        public Pack(int count=1,int width=64,int height=64)
        {
            Directory.CreateDirectory(Path.Combine(Root,"curios/art"));
            Png[0]=137;Png[1]=80;Png[2]=78;Png[3]=71;
            void Dimension(int at,int value){for(int b=0;b<4;b++)Png[at+b]=(byte)(value>>(24-b*8));}
            Dimension(16,width);Dimension(20,height);
            string hash=Convert.ToHexString(SHA256.HashData(Png)).ToLowerInvariant();
            int cell=(Math.Max(width,height)+255)/256,mw=(width+cell-1)/cell,mh=(height+cell-1)/cell;
            var artifacts=Enumerable.Range(0,count).Select(i=>new {Id="curio"+i,Width=200,PixelWidth=width,PixelHeight=height,
                Padding=16,MaskCellSize=cell,MaskWidth=mw,MaskHeight=mh,Mask=Enumerable.Repeat((byte)255,(mw*mh+7)/8).ToArray(),
                ClosedSha256=hash,UsedSha256=hash}).ToArray();
            var entries=Enumerable.Range(0,count).Select(i=>new {Id="curio"+i,ArtId="curio"+i})
                .Concat(new[]{new{Id="alias",ArtId="curio0"}}).ToArray();
            foreach(var art in artifacts)
                foreach(string state in new[]{"closed","used"})
                    File.WriteAllBytes(Path.Combine(Root,"curios/art",art.Id+"-"+state+".png"),Png);
            File.WriteAllText(Path.Combine(Root,"curios/manifest.json"),JsonConvert.SerializeObject(new{Version=2,Artifacts=artifacts,Entries=entries}));
            NativeCurioArt.Clear();Plugin.NativeRoomSceneryPath.Value=Root;
            Texture2D.ApiThreads.Clear();Texture2D.RoomDecodeCount=0;Texture2D.RoomDecodeResult=null;
        }
        public void Dispose()
        {
            NativeCurioArt.Clear();Plugin.NativeRoomSceneryPath.Value=_previous;
            UnityEngine.Object.ObserveDestroy=null;Texture2D.RoomDecodeResult=null;
            Directory.Delete(Root,true);
        }
    }
    private static ExpeditionState State(int count=1)
    {
        var map=new DungeonMap();
        map.Rooms.Add(new Room{Id=0,CurioId="curio0",CorridorIds={0}});
        map.Rooms.Add(new Room{Id=1,CorridorIds={0}});
        var corridor=new Corridor{Id=0,RoomA=0,RoomB=1};
        for(int i=0;i<count;i++)corridor.Tiles.Add(new HallTile{Index=i,Content=HallContent.Curio,ContentId="curio"+i});
        map.Corridors.Add(corridor);
        return new ExpeditionState{Map=map,RoomId=0,RandomCounter=17};
    }
    private static void Pump(ExpeditionState state,Func<bool> complete)
    {
        var watch=Stopwatch.StartNew();
        while(!complete()&&watch.Elapsed.TotalSeconds<10)
        {
            Time.frameCount++;
            int before=Texture2D.RoomDecodeCount;
            NativeCurioArt.Prepare(state);NativeCurioArt.Prepare(state);
            Assert.InRange(Texture2D.RoomDecodeCount-before,0,1);
            Thread.Sleep(1);
        }
        Assert.True(complete(),"Curio warmup did not finish. Loaded: "+string.Join(",",NativeCurioArt.Definitions
            .Where(d=>NativeCurioArt.TryGet(d.Id,false,out _,out _)).Select(d=>d.Id))+". Warnings: "+string.Join("; ",Plugin.Log.Warnings.TakeLast(5)));
    }

    [Fact]
    public void RealLoaderSharesAliasesKeepsUsedStatesAndUploadsOnlyOnTheCallingThread()
    {
        using var pack=new Pack();var state=State();
        var destroyed=new List<object>();UnityEngine.Object.ObserveDestroy=destroyed.Add;
        int thread=Environment.CurrentManagedThreadId;
        Pump(state,()=>NativeCurioArt.Ready&&NativeCurioArt.TryGet("curio0",false,out _,out _));
        Assert.True(NativeCurioArt.TryGet("curio0",false,out var closed,out var art));
        Assert.True(NativeCurioArt.TryGet("alias",false,out var alias,out var shared));
        Assert.True(NativeCurioArt.TryGet("curio0",true,out var used,out _));
        Assert.Same(closed,alias);Assert.Same(art,shared);Assert.NotSame(closed,used);
        Assert.Equal(2,Texture2D.RoomDecodeCount);Assert.Equal(17,state.RandomCounter);
        Assert.False(state.Map.Rooms[0].CurioTaken);
        Assert.All(Texture2D.ApiThreads,id=>Assert.Equal(thread,id));
        NativeCurioArt.Clear();Assert.Equal(2,destroyed.Count);
    }

    [Fact]
    public void AChangedUsedFileFailsTheWholePairWithoutSubstitutingAnotherObject()
    {
        using var pack=new Pack();var state=State();
        File.WriteAllBytes(Path.Combine(pack.Root,"curios/art/curio0-used.png"),new byte[]{1,2,3});
        NativeCurioArt.Prepare(state);Pump(state,()=>NativeCurioArt.Ready);
        Assert.False(NativeCurioArt.TryGet("curio0",false,out _,out _));
        Assert.False(NativeCurioArt.TryGet("unknown",false,out _,out _));
        Assert.False(NativeCurioArt.TryGet("faded_memory",false,out _,out _));
        Assert.Equal(0,Texture2D.RoomDecodeCount);
    }

    [Fact]
    public void RegionChangesPreloadTheSelectedBodyAndNameWithoutChangingGameplayState()
    {
        using var pack = new Pack(2); var state = State();
        string path = Path.Combine(pack.Root, "curios/manifest.json");
        var document = JObject.Parse(File.ReadAllText(path)); document["Version"] = 3;
        document["Entries"]![0]!["Regions"] = JObject.FromObject(new { dd2_city = new { ArtId = "curio1", DisplayName = "Locked street cache",
            ResultTexts = new Dictionary<string, string> { ["The cabinet is a trap!"] = "The cache is trapped!" } } });
        File.WriteAllText(path, document.ToString());
        state.Quest = new() { Dungeon = "dd2_city" };
        Pump(state, () => NativeCurioArt.Ready && NativeCurioArt.TryGet("curio0", false, out _, out _));
        Assert.True(NativeCurioArt.TryGet("curio0", false, out var closed, out var art));
        Assert.Equal("curio1", art.Id);
        Assert.Equal("Locked street cache", NativeCurioArt.DisplayName("curio0"));
        Assert.Equal("The cache is trapped!", NativeCurioArt.ResultText("curio0", "The cabinet is a trap!"));
        Assert.True(NativeCurioArt.TryGet("curio0", true, out var used, out var usedArt));
        Assert.NotSame(closed, used); Assert.Same(art, usedArt);
        state.Quest.Dungeon = "ruins";
        Pump(state, () => NativeCurioArt.Ready && NativeCurioArt.TryGet("curio0", false, out _, out var current) && current.Id == "curio0");
        Assert.Null(NativeCurioArt.DisplayName("curio0"));
        Assert.Equal("The cabinet is a trap!", NativeCurioArt.ResultText("curio0", "The cabinet is a trap!"));
        Assert.Equal(17, state.RandomCounter);
        Assert.Equal("curio0", state.Map.Rooms[0].CurioId);
        Assert.False(state.Map.Rooms[0].CurioTaken);
    }

    [Fact]
    public void RegionOnlyPairLoadsInSprawlAndReturnsToOriginalArtWithoutStaleNamesOrGameplayChanges()
    {
        using var pack = new Pack(); var state = State();
        string path = Path.Combine(pack.Root, "curios/manifest.json");
        var document = JObject.Parse(File.ReadAllText(path)); document["Version"] = 3;
        var entry = (JObject)document["Entries"]![0]!.DeepClone(); entry.Remove("ArtId");
        entry["Regions"] = JObject.FromObject(new { dd2_city = new { ArtId = "curio0", DisplayName = "Abandoned apothecary trolley" } });
        document["Entries"] = new JArray(entry); File.WriteAllText(path, document.ToString());
        state.Quest = new() { Dungeon = "ruins" };
        NativeCurioArt.Prepare(state); Pump(state, () => NativeCurioArt.Ready);
        Assert.False(NativeCurioArt.TryGet("curio0", false, out _, out _));
        Assert.Equal(0, Texture2D.RoomDecodeCount); Assert.Null(NativeCurioArt.DisplayName("curio0"));
        state.Quest.Dungeon = "dd2_city";
        Pump(state, () => NativeCurioArt.TryGet("curio0", true, out _, out _));
        Assert.True(NativeCurioArt.TryGet("curio0", false, out var closed, out var art));
        Assert.True(NativeCurioArt.TryGet("curio0", true, out var used, out var usedArt));
        Assert.NotSame(closed, used); Assert.Same(art, usedArt);
        Assert.Equal("Abandoned apothecary trolley", NativeCurioArt.DisplayName("curio0"));
        state.Map.Rooms[0].CurioTaken = true;
        foreach (string region in new[] { "ruins", "dd2_forest", "dd2_farm", "dd2_coast" })
        {
            state.Quest.Dungeon = region; NativeCurioArt.Prepare(state);
            Assert.True(NativeCurioArt.Ready);
            Assert.False(NativeCurioArt.TryGet("curio0", false, out _, out _));
            Assert.Null(NativeCurioArt.DisplayName("curio0"));
            Assert.Equal("Original result", NativeCurioArt.ResultText("curio0", "Original result"));
        }
        Assert.Equal(2, Texture2D.RoomDecodeCount); Assert.Equal(17, state.RandomCounter);
        Assert.Equal("curio0", state.Map.Rooms[0].CurioId); Assert.True(state.Map.Rooms[0].CurioTaken);
    }

    [Fact]
    public void NearbyPreloadEvictsOldPairsAndReleasesTexturesWhenLeavingTheExpedition()
    {
        using var pack=new Pack(16);var state=State(16);
        state.RoomId=-1;state.CorridorId=0;
        var destroyed=new List<object>();UnityEngine.Object.ObserveDestroy=destroyed.Add;
        for(int tile=0;tile<16;tile++)
        {
            state.TileIndex=tile;
            NativeCurioArt.Prepare(state);
            string id="curio"+tile;
            Pump(state,()=>NativeCurioArt.TryGet(id,false,out _,out _));
            Assert.InRange(NativeCurioArt.ResidentPairs,1,NativeCurioArt.MaxResidentPairs);
            Assert.InRange(NativeCurioArt.ResidentPixels,1,NativeCurioArt.MaxResidentPixels);
        }
        Assert.NotEmpty(destroyed);
        NativeCurioArt.Prepare(null);Assert.Equal(0,NativeCurioArt.ResidentPairs);
        Assert.Equal(17,state.RandomCounter);Assert.All(state.Map.AllTiles,tile=>Assert.False(tile.Resolved));
    }

    [Fact]
    public void MissingPrivatePackFinishesWarmupWithoutInventingAnyArt()
    {
        using var pack=new Pack();File.Delete(Path.Combine(pack.Root,"curios/manifest.json"));
        var state=State();NativeCurioArt.Prepare(state);Pump(state,()=>NativeCurioArt.Ready);
        Assert.False(NativeCurioArt.TryGet("curio0",false,out _,out _));
        Assert.Equal(0,NativeCurioArt.ResidentPairs);Assert.Equal(0,Texture2D.RoomDecodeCount);
    }

    [Fact]
    public void LargeNearbyPaintingsCannotStarveTheNewCurrentTileOfItsPixelBudget()
    {
        using var pack=new Pack(8,2048,1792);var state=State(8);
        state.RoomId=-1;state.CorridorId=0;state.TileIndex=0;
        NativeCurioArt.Prepare(state);
        Pump(state,()=>NativeCurioArt.Ready&&NativeCurioArt.TryGet("curio0",false,out _,out _));
        Assert.Equal(3,NativeCurioArt.ResidentPairs);
        state.TileIndex=3;
        NativeCurioArt.Prepare(state);
        Assert.False(NativeCurioArt.TryGet("curio0",false,out _,out _));
        Pump(state,()=>NativeCurioArt.Ready&&NativeCurioArt.TryGet("curio3",false,out _,out _));
        Assert.InRange(NativeCurioArt.ResidentPixels,1,NativeCurioArt.MaxResidentPixels);
        Assert.True(NativeCurioArt.TryGet("curio4",false,out _,out _));
        Assert.True(NativeCurioArt.TryGet("curio2",false,out _,out _));
        Assert.False(NativeCurioArt.TryGet("curio0",false,out _,out _));
        Assert.Equal(17,state.RandomCounter);
    }
}
