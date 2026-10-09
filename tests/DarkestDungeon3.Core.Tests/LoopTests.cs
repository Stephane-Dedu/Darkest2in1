using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

/// <summary>The minimal DD1 loop end to end, without the game: board → embark → crawl → homecoming → save.</summary>
public class LoopTests
{
    private readonly ITestOutputHelper _out;
    public LoopTests(ITestOutputHelper output) => _out = output;

    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Dd1Install.Find());

    private static Estate NewEstate()
    {
        var estate = new Estate { Seed = 2026 };
        estate.Add(Currency.Gold, 5000);
        foreach (var (cls, name) in new[] { ("man_at_arms", "Reynauld"), ("highwayman", "Dismas"), ("plague_doctor", "Paracelsus"), ("vestal", "Junia") })
            estate.Roster.Add(new HeroRecord { Id = Estate.NewId(), ClassId = cls, Name = name });
        estate.Quests = QuestBoard.Generate(estate, Dd1);
        return estate;
    }

    [Fact]
    public void FullLoopUpdatesTheEstateAndSurvivesASaveRoundTrip()
    {
        var estate = NewEstate();
        var quest = estate.Quests.First();
        Assert.All(estate.Roster, h => Assert.True(Homecoming.WillEmbark(h, quest)));

        var map = MapGenerator.Generate(Dd1.MapGen.Find(quest.Dungeon, quest.Size, quest.Type), quest.MapSeed, Dd1.Props(quest.Dungeon));
        var exp = new ExpeditionState { Quest = quest, Map = map, Seed = quest.MapSeed, Party = estate.Roster.Select(h => h.Id).ToList() };
        exp.Pack.Add(Supply.Food, 12);
        exp.Pack.Add(Supply.Torch, 8);
        exp.Pack.Add(Supply.Shovel, 2);
        exp.Pack.Add(Currency.Gold, 900);   // pretend loot
        var party = new FakeParty(exp.Party.ToArray());
        var crawl = new Crawl(exp, CrawlRules.FromDd1(Dd1.Rules), party);
        crawl.Begin();

        // Save mid-dungeon, reload, and keep going on the reloaded state.
        var save = new SaveFile { Estate = estate, Expedition = exp };
        var reloaded = SaveFile.FromJson(save.ToJson());
        Assert.Equal(exp.Map.ToAscii(), reloaded.Expedition.Map.ToAscii());
        Assert.Equal(exp.Party, reloaded.Expedition.Party);
        Assert.Equal(12, reloaded.Expedition.Pack.Count(Supply.Food));

        crawl = new Crawl(reloaded.Expedition, CrawlRules.FromDd1(Dd1.Rules), party);
        estate = reloaded.Estate;
        exp = reloaded.Expedition;
        int guard = 0;
        while (!exp.QuestComplete && guard++ < 3000)
        {
            if (crawl.LastSpoils != null) { Assert.True(crawl.DismissSpoils(crawl.LastSpoils)); continue; }
            if (crawl.IsBlocked)
            {
                if (exp.InRoom || crawl.CurrentTile.Content == HallContent.Battle) crawl.ResolveBattle();
                else if (crawl.CurrentTile?.Content == HallContent.Trap) crawl.DisarmTrap();
                else crawl.ClearObstacle();
            }
            else if (exp.InRoom)
            {
                var next = exp.Map.Neighbours(exp.RoomId).OrderBy(n => exp.Map.Room(n).Visited).First();
                crawl.Travel(next);
            }
            else crawl.Step(forward: true);
        }
        Assert.True(exp.QuestComplete);

        int goldBefore = estate.Get(Currency.Gold);
        var outcomes = exp.Party.Select(id => new HeroOutcome { HeroId = id, Stress = party.Stress[id] }).ToList();
        outcomes[3].Died = true;
        outcomes[3].CauseOfDeath = "a cultist's blade";
        var log = Homecoming.Apply(estate, Dd1, exp, outcomes);
        foreach (var line in log) _out.WriteLine(line);

        Assert.Equal(goldBefore + 900 + quest.Rewards.Where(r => r.Type == Currency.Gold).Sum(r => r.Amount), estate.Get(Currency.Gold));
        Assert.Equal(2, estate.QuestsCompleted);
        Assert.True(estate.ZoneXp[quest.Dungeon] > 0);
        Assert.Equal(3, estate.Roster.Count);
        Assert.Single(estate.Graveyard);
        Assert.All(estate.Roster, h => Assert.Equal(Homecoming.ResolveXp(quest), h.ResolveXp));
        Assert.DoesNotContain(estate.Quests, q => q.Id == quest.Id);

        // And to disk and back.
        string path = Path.Combine(Path.GetTempPath(), "dd3_test_save", "slot1.json");
        new SaveFile { Estate = estate }.Save(path);
        new SaveFile { Estate = estate }.Save(path); // second save makes a .bak
        var fromDisk = SaveFile.Load(path);
        Assert.Equal(estate.Get(Currency.Gold), fromDisk.Estate.Get(Currency.Gold));
        Assert.Equal("Junia", fromDisk.Estate.Graveyard.Single().Name);
        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void OverLevelledHeroesRefuseEasyQuests()
    {
        var hero = new HeroRecord { ResolveLevel = 3 };
        Assert.False(Homecoming.WillEmbark(hero, new QuestOffer { Difficulty = 1 }));
        Assert.True(Homecoming.WillEmbark(hero, new QuestOffer { Difficulty = 3 }));
    }
}
