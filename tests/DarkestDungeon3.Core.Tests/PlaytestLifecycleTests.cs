using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Tests;

/// <summary>
/// Saved system integration, with a tiny deterministic map and a simulated combat result.
/// Native combat, frame time, controls and visual acceptance remain manual runbook steps.
/// </summary>
public class PlaytestLifecycleTests
{
    [Theory]
    [InlineData("dd2_city")]
    [InlineData("dd2_farm")]
    [InlineData("dd2_forest")]
    [InlineData("dd2_coast")]
    public void ProvisionWalkFightCampReturnAndReloadPreserveRegionalCampaignState(string region)
    {
        var install = Dd1TestInstall.Find();
        var dd1 = Dd1Campaign.Load(install);
        var estate = new Estate { Seed = 2026, Week = 4, QuestsCompleted = 4 };
        estate.Add(Currency.Gold, 1200);
        string[] classes = { "crusader", "highwayman", "plague_doctor", "vestal" };
        foreach (string cls in classes)
            estate.Roster.Add(new HeroRecord { Id = cls, ClassId = cls, Name = cls });
        var quest = new QuestOffer { Id = "qa-" + region, Dungeon = region, Type = "explore", Length = 2,
            Difficulty = 1, MapSeed = 2026, CanRetreat = true, Rewards = { new Reward { Type = Currency.Gold, Amount = 500 } } };
        estate.Quests.Add(quest);
        var bought = new Inventory(); bought.Add(Supply.Food, 12); bought.Add(Supply.Torch, 4);
        var state = Embark.Create(dd1, quest, estate.Roster, bought, Provisioner.Load(install, ItemCatalog.Load(install)));
        Assert.NotEmpty(state.Map.Rooms); // Real region map config and provision pack were loaded.
        Assert.Equal(classes, state.Party);
        Assert.True(state.Pack.Count(Supply.Firewood) > 0);
        // Fixed map isolates the persisted phase sequence from procedural event distribution.
        state.Map = new DungeonMap
        {
            Rooms = { new Room { Id = 0, Content = RoomContent.Entrance, CorridorIds = { 0 } },
                      new Room { Id = 1, Content = RoomContent.Empty, CorridorIds = { 0 } } },
            Corridors = { new Corridor { Id = 0, RoomA = 0, RoomB = 1,
                Tiles = { new HallTile { Index = 0 }, new HallTile { Index = 1, Content = HallContent.Battle } } } }
        };
        state.Goal = new QuestGoal { Type = "explore_room", Amount = 2 };
        var party = new FakeParty(classes);
        var rules = new CrawlRules { AmbushCampChance = 0 };
        var save = new SaveFile { Estate = estate, Expedition = state };
        string root = Path.Combine(Path.GetTempPath(), "dd3-playtest-loop-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "synthetic.json");
        try
        {
            Crawl Reload()
            {
                ExpeditionParty.Capture(save.Expedition, classes.Select(id => new ExpeditionHeroState
                {
                    Hp = party.Hp[id] * 30, HpMax = 30, Stress = party.Stress[id], Outcome = new HeroOutcome { HeroId = id }
                }));
                string before = save.ToJson();
                save.Save(path);
                save = SaveFile.Load(path);
                Assert.Equal(before, save.ToJson());
                Assert.Equal(region, save.Expedition.Quest.Dungeon);
                Assert.Equal(classes, save.Expedition.Party);
                var resumed = new Crawl(save.Expedition, rules, party);
                string phase = save.ToJson();
                resumed.Resume();
                Assert.Equal(phase, save.ToJson()); // No duplicate steps, RNG, supplies or phase resolution.
                return resumed;
            }
            var crawl = new Crawl(state, rules, party);
            crawl.Begin();
            crawl.Travel(1);
            crawl = Reload();
            Assert.Equal(0, save.Expedition.TileIndex);
            crawl.Step(true);
            Assert.NotNull(save.Expedition.PendingEncounter);
            crawl = Reload();
            Assert.True(crawl.IsBlocked);
            crawl.ResolveBattle(); // Explicit combat seam, not a native DD2 skill fight.
            if (crawl.LastSpoils != null) Assert.True(crawl.DismissSpoils(crawl.LastSpoils));
            Assert.True(crawl.CanEnterDoor(1));
            crawl.EnterDoor(1);
            Assert.True(save.Expedition.QuestComplete);
            Assert.False(save.Expedition.Ended);
            Assert.True(crawl.CanCamp);
            int wood = save.Expedition.Pack.Count(Supply.Firewood);
            crawl.MakeCamp();
            Assert.True(crawl.EatMeal(Meal.Full));
            int food = save.Expedition.Pack.Count(Supply.Food);
            crawl = Reload();
            Assert.False(crawl.EatMeal(Meal.Full));
            Assert.Equal(food, save.Expedition.Pack.Count(Supply.Food));
            Assert.Equal(wood - 1, save.Expedition.Pack.Count(Supply.Firewood));
            crawl.BreakCamp();
            save.Expedition.Pack.Add(Currency.Gold, 175);
            Assert.True(crawl.TryLeave());
            Assert.False(crawl.TryLeave());
            crawl = Reload();
            Assert.Equal(ExpeditionLoadRoute.Results, ExpeditionRecovery.Inspect(save));
            var report = Homecoming.Report(save.Estate, dd1, save.Expedition, ExpeditionParty.Outcomes(save.Expedition, save.Estate));
            Assert.Equal("complete", report.Result);
            Assert.Equal(1875, save.Estate.Get(Currency.Gold));
            Assert.Equal(5, save.Estate.QuestsCompleted);
            Assert.True(save.Estate.ZoneXp[region] > 0);
            Assert.All(save.Estate.Roster, h => Assert.Equal(Homecoming.ResolveXp(quest), h.ResolveXp));
            Assert.DoesNotContain(save.Estate.Quests, q => q.Id == quest.Id);
            // The runtime clears the expedition when it publishes homecoming. Exercise that save contract.
            save.Expedition = null;
            save.Save(path);
            string town = save.ToJson();
            for (int i = 0; i < 3; i++)
            {
                save = SaveFile.Load(path);
                Assert.Equal(ExpeditionLoadRoute.Town, ExpeditionRecovery.Inspect(save));
                Assert.Equal(town, save.ToJson());
                Assert.Equal(1875, save.Estate.Get(Currency.Gold));
                save.Save(path);
            }
        }
        finally { Directory.Delete(root, true); } // Unique synthetic temp folder, never a real estate.
    }
}
