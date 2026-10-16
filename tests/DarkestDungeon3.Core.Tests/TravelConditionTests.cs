using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class TravelConditionTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly CrawlContent Content = CrawlContent.Load(Install);
    private static readonly CrawlRules Rules = CrawlRules.FromDd1(Dd1Campaign.Load(Install).Rules);
    private static (Crawl, FakeParty) New()
    {
        var map = new DungeonMap { EntranceRoomId = 0 };
        map.Rooms.Add(new Room { Id = 0, Content = RoomContent.Entrance, Cleared = true, CorridorIds = { 0 } });
        map.Rooms.Add(new Room { Id = 1, Content = RoomContent.Empty, CorridorIds = { 0 } });
        map.Corridors.Add(new Corridor { Id = 0, RoomA = 0, RoomB = 1 });
        for (int i = 0; i < 8; i++) map.Corridors[0].Tiles.Add(new HallTile { Index = i });
        var s = new ExpeditionState { Map = map, Party = { "a", "b" }, Quest = new QuestOffer { Type = "explore", Dungeon = "crypts" } };
        s.Pack.Add(Supply.Food, 24);
        s.Pack.Add(Supply.Antivenom, 2);
        var party = new MortalParty("a", "b");
        party.Hp["a"] = 0.1f;
        var c = new Crawl(s, Rules, party, Content);
        c.Begin();
        return (c, party);
    }

    [Fact]
    public void Travel_damage_cannot_leave_a_defeated_party_waiting_for_a_battle()
    {
        var (c, p) = New();
        c.State.Map.Corridors[0].Tiles[1].Content = HallContent.Battle;
        foreach (string id in c.State.Party)
            c.State.TravelEffects.Add(new TravelEffect { HeroId = id, Kind = "bleed", DamageFraction = 1, TurnsLeft = 3 });
        c.Travel(1);
        var events = c.Step(true);
        Assert.Empty(p.Alive);
        Assert.True(c.State.Ended);
        Assert.False(c.CanNavigate);
        Assert.Null(c.State.PendingEncounter);
        Assert.DoesNotContain(events, e => e.Type is CrawlEventType.Battle or CrawlEventType.Ambush);
    }

    private sealed class MortalParty : FakeParty, IParty
    {
        public MortalParty(params string[] heroes) : base(heroes) { }
        public new void Damage(string heroId, float fraction, string cause) => Hp[heroId] = System.Math.Max(0, Hp[heroId] - fraction);
    }

    [Fact]
    public void Four_foods_per_hero_persist_until_combat_reset()
    {
        var (c, p) = New();
        for (int i = 0; i < 4; i++) Assert.NotNull(c.UseSupply("a", Supply.Food));
        Assert.Null(c.UseSupply("a", Supply.Food));
        Assert.Equal(20, c.State.Pack.Count(Supply.Food));
        p.Hp["b"] = 0.2f;
        Assert.NotNull(c.UseSupply("b", Supply.Food));
        var loaded = SaveFile.FromJson(new SaveFile { Expedition = c.State }.ToJson()).Expedition;
        var resumed = new Crawl(loaded, Rules, p, Content);
        Assert.Null(resumed.UseSupply("a", Supply.Food));
        resumed.ResolveBattle();
        Assert.NotNull(resumed.UseSupply("a", Supply.Food));
    }

    [Fact]
    public void Curio_poison_waits_for_travel_and_can_be_treated_after_reload()
    {
        var (c, p) = New();
        Content.Curios.ApplyEffect("Blight 1", "a", p, new Core.Rng(1), state: c.State);
        Assert.Equal(0.1f, p.Hp["a"]);
        Assert.Single(c.State.TravelEffects);
        var loaded = SaveFile.FromJson(new SaveFile { Expedition = c.State }.ToJson()).Expedition;
        var resumed = new Crawl(loaded, Rules, p, Content);
        Assert.NotNull(resumed.UseSupply("a", Supply.Antivenom));
        Assert.Empty(loaded.TravelEffects);
        Assert.Equal(1, loaded.Pack.Count(Supply.Antivenom));
        Assert.Null(resumed.UseSupply("a", Supply.Antivenom));
        Assert.Equal(1, loaded.Pack.Count(Supply.Antivenom));
    }

    [Fact]
    public void Poison_ticks_at_data_cadence_and_resume_does_not_tick_again()
    {
        var (c, p) = New();
        p.Hp["a"] = 1;
        Content.Curios.ApplyEffect("Blight 1", "a", p, new Core.Rng(1), state: c.State);
        c.Travel(1);
        Assert.Equal(1, p.Hp["a"]);
        c.Step(true);
        Assert.InRange(p.Hp["a"], 0.93f, 0.94f);
        var before = p.Hp["a"];
        var loaded = SaveFile.FromJson(new SaveFile { Expedition = c.State }.ToJson()).Expedition;
        new Crawl(loaded, Rules, p, Content).Resume();
        Assert.Equal(before, p.Hp["a"]);
        Assert.Equal(2, loaded.TravelEffects.Single().TurnsLeft);
    }
}
