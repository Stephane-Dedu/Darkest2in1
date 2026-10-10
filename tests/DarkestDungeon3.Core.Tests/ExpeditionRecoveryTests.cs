using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ExpeditionRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveSavedRoomOrHallNeverRepeatsEmbarkOrAbandonsItsState(bool hall)
    {
        var save = Save();
        if (hall) { save.Expedition.RoomId = -1; save.Expedition.CorridorId = 0; save.Expedition.TileIndex = 1; save.Expedition.HeadingRoomId = 1; }
        save.Expedition.Camp = new CampState { Ate = true, RespiteLeft = 5 };
        save.Expedition.PendingSpoils = new BattleSpoils { Kind = "camp", LeftBehind = { new LootDrop { Type = "gold", Amount = 100 } } };
        string before = save.ToJson();
        for (int load = 0; load < 3; load++)
        {
            save = SaveFile.FromJson(save.ToJson());
            Assert.Equal(ExpeditionLoadRoute.Resume, ExpeditionRecovery.Inspect(save));
            Assert.False(ExpeditionFight.RestoreParty(save.Expedition));
            var hero = ExpeditionParty.HeroForRestore(save.Estate.Hero("a"), save.Expedition.PartyStates["a"]);
            Assert.Equal(7, hero.Stress); Assert.False(hero.IsDead);
            var crawl = new Crawl(save.Expedition, new CrawlRules(), new FakeParty("a"));
            Assert.Single(crawl.Resume());
            Assert.Equal(before, save.ToJson());
            Assert.Equal(9, save.Estate.Week); Assert.Equal(510, save.Estate.Get(Currency.Gold));
            Assert.False(save.Expedition.Retreated); Assert.False(save.Expedition.Ended);
        }
    }

    [Fact]
    public void MidFightDeathsUsePreFightEvidenceBeforeDecidingWhetherToResume()
    {
        var save = Save();
        Assert.True(ExpeditionFight.Record(save.Expedition, new FightPlan(), "battle", "arena", false, true));
        save.Expedition.PartyStates["a"].Outcome.Died = true;
        Assert.Equal(ExpeditionLoadRoute.Resume, ExpeditionRecovery.Inspect(save));
        Assert.True(ExpeditionFight.RestoreParty(save.Expedition));
        Assert.False(save.Expedition.PartyStates["a"].Outcome.Died);
        save.Expedition.FightCheckpoint = null;
        save.Expedition.PartyStates["a"].Outcome.Died = true;
        Assert.Equal(ExpeditionLoadRoute.Results, ExpeditionRecovery.Inspect(save));
    }

    [Theory]
    [InlineData("no-quest")]
    [InlineData("no-map")]
    [InlineData("room")]
    [InlineData("missing-hero")]
    [InlineData("duplicates")]
    [InlineData("empty-party")]
    public void InvalidSavedRaidIsRetainedWithoutCampaignOrOutcomeChanges(string invalid)
    {
        var save = Save();
        if (invalid == "no-quest") save.Expedition.Quest = null;
        else if (invalid == "no-map") save.Expedition.Map = null;
        else if (invalid == "room") save.Expedition.RoomId = 999;
        else if (invalid == "missing-hero") save.Estate.Roster.Clear();
        else if (invalid == "duplicates") save.Expedition.Party.Add("a");
        else save.Expedition.Party.Clear();
        string before = save.ToJson();
        Assert.Equal(ExpeditionLoadRoute.Invalid, ExpeditionRecovery.Inspect(save));
        Assert.Equal(before, save.ToJson());
    }

    [Fact]
    public void TownNeverLeftAndTerminalRoutesStayDistinct()
    {
        var save = Save();
        save.Expedition.Started = false;
        Assert.Equal(ExpeditionLoadRoute.Refund, ExpeditionRecovery.Inspect(save));
        save.Expedition.Ended = true;
        Assert.Equal(ExpeditionLoadRoute.Results, ExpeditionRecovery.Inspect(save));
        save.Expedition = null;
        Assert.Equal(ExpeditionLoadRoute.Town, ExpeditionRecovery.Inspect(save));
        Assert.Equal(ExpeditionLoadRoute.Town, ExpeditionRecovery.Inspect(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalResultsRetainSuccessOrRetreatAndCannotPayAgainAfterSaveClear(bool retreat)
    {
        var save = Save(); save.Expedition.Ended = true; save.Expedition.QuestComplete = !retreat; save.Expedition.Retreated = retreat;
        save.Expedition.Quest.Rewards.Add(new Reward { Type = Currency.Gold, Amount = 100 });
        var native = Dd1Campaign.Load(Dd1Install.Find());
        Assert.Equal(ExpeditionLoadRoute.Results, ExpeditionRecovery.Inspect(save));
        var report = Homecoming.Report(save.Estate, native, save.Expedition, ExpeditionParty.Outcomes(save.Expedition, save.Estate));
        Assert.Equal(retreat ? "retreat" : "complete", report.Result);
        Assert.Equal(retreat ? 510 : 610, save.Estate.Get(Currency.Gold));
        Assert.Equal(retreat ? 9 : 7, save.Estate.Hero("a").Stress);
        save.Expedition = null;
        var reloaded = SaveFile.FromJson(save.ToJson());
        Assert.Equal(ExpeditionLoadRoute.Town, ExpeditionRecovery.Inspect(reloaded));
        Assert.Equal(save.Estate.Get(Currency.Gold), reloaded.Estate.Get(Currency.Gold));
        Assert.Equal(9, reloaded.Estate.Week);
    }

    private static SaveFile Save() => new()
    {
        Estate = new Estate { Week = 9, Currencies = { [Currency.Gold] = 510 }, Roster = { new HeroRecord { Id = "a", ClassId = "highwayman", Name = "Dismas", Stress = 1 } } },
        Expedition = new ExpeditionState
        {
            Started = true, Seed = 77, RandomCounter = 24, RoomId = 1, Light = 39, StepsTaken = 22, Party = { "a" },
            Quest = new QuestOffer { Dungeon = "dd2_city", Type = "explore", Difficulty = 1, Length = 1 },
            PartyStates = { ["a"] = new ExpeditionHeroState { Hp = 0, HpMax = 30, Stress = 7, Outcome = new HeroOutcome { HeroId = "a" } } },
            Map = new DungeonMap
            {
                Rooms = { new Room { Id = 0, CorridorIds = { 0 } }, new Room { Id = 1, CorridorIds = { 0 } } },
                Corridors = { new Corridor { Id = 0, RoomA = 0, RoomB = 1, Tiles = { new HallTile { Index = 0 }, new HallTile { Index = 1 } } } }
            }
        }
    };
}
