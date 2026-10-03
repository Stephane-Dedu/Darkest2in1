using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class BestiaryTests
{
    private static readonly string Json = Path.GetFullPath(Path.Combine(
        System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "monsters.json"));
    private static readonly Dd1Bestiary Bestiary = Dd1Bestiary.Load(Json);
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Dd1 = Dd1Campaign.Load(Install);
    private static readonly CrawlContent Content = CrawlContent.Load(Install);

    // DD2 sizes of the two-rank stand-ins (as DD2's ActorDataClass m_Size says); everything else is one rank.
    private static readonly HashSet<string> Large = new()
    {
        "lost_battalion_knight", "lost_battalion_knight_b", "cave_swine_brute", "cave_swine_brute_b", "coastal_docker",
        "fanatic_pit_fighter", "plague_eater_lord", "shared_ghoul", "shared_shambler", "shared_lost_soul_yeoman",
    };
    private static int Size(string dd2) => Large.Contains(dd2) ? 2 : 1;

    [Fact]
    public void TranslatesInRankOrder()
    {
        var lineUp = Bestiary.Translate(new[] { "skeleton_common_A", "skeleton_common_A", "skeleton_arbalist_A", "skeleton_courtier_A" }, new Rng(1), Size);
        Assert.Equal(new[] { "lost_battalion_foot_soldier", "lost_battalion_foot_soldier", "lost_battalion_arbalist", "lost_battalion_drummer" }, lineUp);
    }

    [Fact]
    public void ChampionsTakeTheStrongerForm()
    {
        var lineUp = Bestiary.Translate(new[] { "skeleton_arbalist_C", "swinetaur_C" }, new Rng(1), Size);
        Assert.Equal(new[] { "lost_battalion_arbalist_b", "cave_swine_brute_b" }, lineUp);
    }

    [Fact]
    public void BossPiecesFallBackToTheZoneBattle()
    {
        Assert.Null(Bestiary.Translate(new[] { "necromancer_A", "skeleton_common_A" }, new Rng(1), Size));
        Assert.Null(Bestiary.Translate(new string[0], new Rng(1), Size));
    }

    [Fact]
    public void LineUpFitsFourRanks()
    {
        // Two size-2 stand-ins fill the four ranks; the rest is dropped from the back.
        var lineUp = Bestiary.Translate(new[] { "skeleton_captain_A", "ghoul_A", "skeleton_arbalist_A" }, new Rng(1), Size);
        Assert.Equal(new[] { "lost_battalion_knight", "shared_ghoul" }, lineUp);
    }

    [Fact]
    public void EveryRegularDd1MonsterHasAStandIn()
    {
        var bosses = new HashSet<string> { "necromancer", "prophet", "pew_small", "pew_medium", "pew_large", "hag", "cauldron_empty", "nest", "crow",
            "brigand_cannon", "brigand_fuseman", "swine_prince", "swine_piglet", "siren", "drowned_captain", "collector" };
        var missing = new SortedSet<string>();
        foreach (var zone in new[] { "crypts", "weald", "warrens", "cove" })
            foreach (int difficulty in new[] { 1, 3, 5 })
                foreach (var kind in new[] { "hall", "room" })
                    for (int seed = 0; seed < 60; seed++)
                        foreach (var m in Content.Battles.RollEncounter(zone, difficulty, kind, new Rng(seed)))
                            if (!Bestiary.Knows(m) && !bosses.Contains(Dd1Bestiary.Split(m).Family)) missing.Add(m);
        Assert.Empty(missing);
    }

    [Fact]
    public void TheFightAfterARetreatIsTheSameGroupAndItsLootIsTheirs()
    {
        var quest = new QuestOffer { Dungeon = "crypts", Type = "explore", Length = 1, Difficulty = 1, MapSeed = 9 };
        var map = MapGenerator.Generate(Dd1.MapGen.Find("crypts", "short", "explore"), 9, Dd1.Props("crypts"));
        var state = new ExpeditionState { Quest = quest, Map = map, Seed = 9, Party = { "a", "b", "c", "d" } };
        var crawl = new Crawl(state, CrawlRules.FromDd1(Dd1.Rules), new FakeParty("a", "b", "c", "d"), Content);
        crawl.Begin();
        var first = crawl.FightMonsters("room");
        Assert.NotEmpty(first);
        Assert.Same(first, crawl.FightMonsters("room"));          // still waiting there (after a retreat)
        crawl.ResolveBattle();
        Assert.Equal(first, crawl.LastSpoils.Dd1Monsters);
        Assert.Null(state.FightMonsters);
    }
}

public class MonsterNameTests
{
    [Fact]
    public void Dd1NamesMonstersAndTheirSkills()
    {
        var lore = Dd1Lore.Load(Dd1Install.Find());
        Assert.Equal("Swine Drummer", lore.MonsterNames["swine_drummer_A"]);
        Assert.True(lore.MonsterSkillNames.ContainsKey("crossbow_shot"));
        Assert.True(lore.MonsterNames.Count > 100);
        Assert.Equal("Cloister", lore.Text("upgrade_tree_name_abbey.meditation"));
        Assert.Equal("Improves the meditation facilities.", lore.Text("upgrade_tree_tooltip_description_abbey.meditation"));
    }
}
