using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Tests;

public class TrinketResolveTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private static readonly Dd1Campaign Campaign = Dd1Campaign.Load(Install);
    private static readonly Dd1Trinkets Trinkets = Dd1Trinkets.LoadBase(Install);
    private const string Portrait = "dd3_dd1_ancestors_portrait";

    private static (Estate, ExpeditionState) Fixture(bool worn = true)
    {
        var estate = new Estate();
        estate.Roster.Add(new HeroRecord { Id = "h", ClassId = "highwayman", Trinkets = worn ? new() { Portrait } : new() });
        var expedition = new ExpeditionState { QuestComplete = true,
            Quest = new QuestOffer { Id = "q", Dungeon = "dd2_city", Difficulty = 1, Length = 2, ResolveXp = 4 } };
        return (estate, expedition);
    }

    private static HomecomingReport Return(Estate estate, ExpeditionState expedition, HeroOutcome outcome) =>
        Homecoming.Report(estate, Campaign, expedition, new[] { outcome }, equipmentBuffs: id =>
            estate.Hero(id)?.WornTrinkets.Where(t => t.StartsWith(FadedMemory.TrinketPrefix))
                .Select(t => Trinkets.Get(t.Substring(FadedMemory.TrinketPrefix.Length))).Where(t => t != null)
                .SelectMany(t => t.BuffIds).Select(Campaign.Buffs.Get));

    [Fact]
    public void InstalledPortraitAwardsItsActualFiftyPercentBonusAfterReload()
    {
        var (estate, expedition) = Fixture();
        var loaded = SaveFile.FromJson(new SaveFile { Estate = estate, Expedition = expedition }.ToJson());
        var report = Return(loaded.Estate, loaded.Expedition, new HeroOutcome { HeroId = "h" });
        Assert.Equal(6, Assert.Single(report.Heroes).XpGained);
        Assert.Equal(6, loaded.Estate.Hero("h").ResolveXp);
        Assert.Contains(Portrait, loaded.Estate.Hero("h").WornTrinkets);
    }

    [Fact]
    public void CarriedOrStashedPortraitGivesNoEquipmentBonus()
    {
        var (estate, expedition) = Fixture(false);
        estate.Trinkets.Add(Portrait); expedition.Pack.Add(Portrait, 1);
        Assert.Equal(4, Assert.Single(Return(estate, expedition, new HeroOutcome { HeroId = "h" }).Heroes).XpGained);
    }

    [Fact]
    public void FinalNativeEquipmentRemovalTakesPrecedenceOverStartingLoadout()
    {
        var (estate, expedition) = Fixture();
        Assert.Equal(4, Assert.Single(Return(estate, expedition, new HeroOutcome { HeroId = "h", Trinkets = new() }).Heroes).XpGained);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void RetreatOrDeathCannotReceivePortraitXp(bool retreated, bool died)
    {
        var (estate, expedition) = Fixture(); expedition.Retreated = retreated;
        Assert.Equal(0, Assert.Single(Return(estate, expedition, new HeroOutcome { HeroId = "h", Died = died }).Heroes).XpGained);
    }

    [Fact]
    public void EquipmentBonusStacksWithTownBonusWithoutConsumingTheTrinket()
    {
        var (estate, expedition) = Fixture();
        string buff = Trinkets.Get("ancestors_portrait").BuffIds.First(id => Campaign.Buffs.Get(id).Stat == "resolve_xp_bonus_percent");
        expedition.PendingBuffs["h"] = new() { buff };
        Assert.Equal(8, Assert.Single(Return(estate, expedition, new HeroOutcome { HeroId = "h" }).Heroes).XpGained);
        Assert.Contains(Portrait, estate.Hero("h").WornTrinkets);
    }

    [Fact]
    public void UnsupportedConditionalXpNeverBecomesUnconditional()
    {
        var (estate, expedition) = Fixture();
        var report = Homecoming.Report(estate, Campaign, expedition, new[] { new HeroOutcome { HeroId = "h" } },
            equipmentBuffs: _ => new[] { new Dd1Buff { Stat = "resolve_xp_bonus_percent", Amount = 2, Rule = "future_rule" } });
        Assert.Equal(4, Assert.Single(report.Heroes).XpGained);
    }
}
