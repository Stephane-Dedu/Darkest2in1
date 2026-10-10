using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class FadedMemoryCheckpointTests
{
    private static SaveFile Ready(string location = "room")
    {
        var save = new SaveFile();
        save.Estate.Roster.Add(new HeroRecord
        {
            Id = "a", ClassId = "highwayman", Name = "Dismas", WeaponRank = 2,
            EquippedSkills = { "hwm_wicked_slice", "hwm_pistol_shot" }, MasteredSkills = { "hwm_pistol_shot" }
        });
        var map = new DungeonMap
        {
            Seed = 42, Dungeon = "dd2_city", EntranceRoomId = 0,
            Rooms =
            {
                new Room { Id = 0, Content = RoomContent.Entrance, CorridorIds = { 0 } },
                new Room { Id = 1, Content = RoomContent.Empty, CorridorIds = { 0 } },
                new Room { Id = 2, Content = RoomContent.Curio, IsSecret = true, Scouted = true }
            },
            Corridors =
            {
                new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles =
                {
                    new HallTile { Index = 0, SecretRoomId = 2 }, new HallTile { Index = 1 }
                } }
            }
        };
        var state = save.Expedition = new ExpeditionState
        {
            Quest = new QuestOffer { Id = "boss-sprawl", Dungeon = "dd2_city", Type = "kill_boss", MapSeed = 42 },
            Map = map, Seed = 42, RoomId = 0, Party = { "a" },
            PartyStates = { ["a"] = new ExpeditionHeroState { Hp = 30, HpMax = 30, Stress = 2, Outcome = new HeroOutcome { HeroId = "a" } } }
        };
        state.Pack.Add(Supply.Key, 2);
        var crawl = new Crawl(state, new CrawlRules(), new FakeParty("a"));
        crawl.Begin();
        if (location != "room")
        {
            // Retain the prior room-entry square as the ordinary traversal code does.
            state.CameFromCorridorId = 0; state.CameFromTileIndex = 1;
            crawl.Travel(1);
            if (location == "secret") crawl.EnterSecretRoom();
            if (location == "reverse") state.HeadingRoomId = 0;
        }
        return save;
    }

    private static JObject WithoutPosition(ExpeditionState state)
    {
        var json = JObject.FromObject(state);
        foreach (var key in new[] { "RoomId", "CorridorId", "TileIndex", "HeadingRoomId", "CameFromCorridorId",
            "CameFromTileIndex", "SecretReturnCorridorId", "SecretReturnTileIndex", "SecretReturnHeadingRoomId", "InRoom" })
            json.Remove(key);
        return json;
    }

    [Theory]
    [InlineData("room", false)]
    [InlineData("hall", false)]
    [InlineData("reverse", false)]
    [InlineData("secret", false)]
    [InlineData("hall", true)]
    public void ReloadedCheckpointRestoresTravelButKeepsCombatConsequencesAndLoadout(string location, bool died)
    {
        var save = Ready(location);
        var state = save.Expedition;
        var checkpoint = MemoryReturnPosition.Capture(state);
        Assert.NotNull(checkpoint);
        var expectedPosition = JObject.FromObject(checkpoint);
        state.FadedMemory = new FadedMemoryEncounter
        {
            Id = "memory-42", Dungeon = "crypts", BossId = "necromancer_A", Difficulty = 1,
            HeroSprites = { ["a"] = "highwayman" }, ReturnPosition = checkpoint
        };
        // The memory fight has consequences; returning must never restore a pre-fight party or pack.
        state.PartyStates["a"].Hp = 7; state.PartyStates["a"].Stress = 8;
        state.PartyStates["a"].Outcome.Died = died;
        state.PartyStates["a"].Outcome.Quirks = new List<string> { "memory_quirk" };
        state.Pack.TryUse(Supply.Key); state.Pack.Add("dd1+ancestors_pistol", 1);
        state.GoalProgress = 2; state.QuestComplete = true; state.RandomCounter += 9;
        state.RoomId = 1; state.CorridorId = state.TileIndex = state.HeadingRoomId = -1;
        state.CameFromCorridorId = state.CameFromTileIndex = -1;
        state.SecretReturnCorridorId = state.SecretReturnTileIndex = state.SecretReturnHeadingRoomId = -1;
        save = SaveFile.FromJson(save.ToJson());
        state = save.Expedition;
        var consequences = WithoutPosition(state);
        Assert.True(state.FadedMemory.ReturnPosition.TryRestore(state));
        Assert.True(JToken.DeepEquals(expectedPosition, JObject.FromObject(MemoryReturnPosition.Capture(state))));
        Assert.True(JToken.DeepEquals(consequences, WithoutPosition(state)));
        Assert.Equal(died ? ExpeditionLoadRoute.Results : ExpeditionLoadRoute.Resume, ExpeditionRecovery.Inspect(save));
        var hero = ExpeditionParty.HeroForRestore(save.Estate.Hero("a"), state.PartyStates["a"]);
        if (died) Assert.Null(hero);
        else
        {
            Assert.Equal(new[] { "hwm_wicked_slice", "hwm_pistol_shot" }, hero.EquippedSkills);
            Assert.Contains("hwm_pistol_shot", hero.MasteredSkills);
            Assert.Equal(2, hero.WeaponRank);
        }
        string returned = save.ToJson();
        Assert.True(state.FadedMemory.ReturnPosition.TryRestore(state));
        Assert.Equal(returned, save.ToJson());
        if (location == "secret")
        {
            var crawl = new Crawl(state, new CrawlRules(), new FakeParty("a"));
            Assert.Contains(crawl.ExitSecretRoom(), e => e.Type == CrawlEventType.EnteredTile);
            Assert.Equal(0, state.CorridorId); Assert.Equal(0, state.TileIndex); Assert.Equal(1, state.HeadingRoomId);
        }
    }

    [Theory]
    [InlineData("quest")]
    [InlineData("zone")]
    [InlineData("seed")]
    [InlineData("ended")]
    [InlineData("not-started")]
    [InlineData("map")]
    [InlineData("room")]
    [InlineData("hall")]
    [InlineData("heading")]
    [InlineData("came-from")]
    [InlineData("secret-exit")]
    public void ForeignOrInvalidReturnIsRejectedWithoutChangingTheRaid(string invalid)
    {
        var save = Ready("secret");
        var state = save.Expedition;
        var checkpoint = MemoryReturnPosition.Capture(state);
        if (invalid == "quest") state.Quest.Id = "other";
        if (invalid == "zone") state.Quest.Dungeon = "dd2_coast";
        if (invalid == "seed") state.Seed++;
        if (invalid == "ended") state.Ended = true;
        if (invalid == "not-started") state.Started = false;
        if (invalid == "map") state.Map = null;
        if (invalid == "room") checkpoint.RoomId = 99;
        if (invalid == "hall") { checkpoint.RoomId = -1; checkpoint.CorridorId = 99; }
        if (invalid == "heading") checkpoint.SecretReturnHeadingRoomId = 99;
        if (invalid == "came-from") checkpoint.CameFromTileIndex = 99;
        if (invalid == "secret-exit") state.Map.Corridor(0).Tiles[0].SecretRoomId = -1;
        string before = save.ToJson();
        Assert.False(checkpoint.TryRestore(state));
        Assert.Equal(before, save.ToJson());
    }

    [Theory]
    [InlineData("quest")]
    [InlineData("room")]
    [InlineData("not-started")]
    [InlineData("ended")]
    public void InvalidSourceCannotBeCaptured(string invalid)
    {
        var save = Ready(); var state = save.Expedition;
        if (invalid == "quest") state.Quest.Id = null;
        if (invalid == "room") state.RoomId = 99;
        if (invalid == "not-started") state.Started = false;
        if (invalid == "ended") state.Ended = true;
        string before = save.ToJson();
        Assert.Null(MemoryReturnPosition.Capture(state));
        Assert.Equal(before, save.ToJson());
    }

    [Fact]
    public void LegacyExpeditionDoesNotAcquireOrSerializeAMemory()
    {
        var save = Ready(); string before = save.ToJson();
        Assert.DoesNotContain("FadedMemory", before);
        save = SaveFile.FromJson(before);
        Assert.Null(save.Expedition.FadedMemory);
        Assert.Equal(ExpeditionLoadRoute.Resume, ExpeditionRecovery.Inspect(save));
        Assert.Equal(before, save.ToJson());
    }
}
