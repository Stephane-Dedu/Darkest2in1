using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class RecruitClassesTests
{
    /// <summary>A catalog whose stagecoach has DD2's DLC Crusader.</summary>
    private sealed class DlcCatalog : FakeCatalog, IHeroCatalog
    {
        IReadOnlyList<string> IHeroCatalog.RecruitableClasses => RecruitClasses.For(ownsCrusader: true);
    }

    [Fact]
    public void TheBountyHunterIsAlwaysRecruitable()
    {
        Assert.Contains(RecruitClasses.BountyHunter, RecruitClasses.For(ownsCrusader: false));
        Assert.Contains(RecruitClasses.BountyHunter, RecruitClasses.For(ownsCrusader: true));
        Assert.Equal(RecruitClasses.Base.Count + 1, RecruitClasses.For(ownsCrusader: false).Distinct().Count());
    }

    [Fact]
    public void TheCrusaderNeedsDd2sDlc()
    {
        Assert.DoesNotContain(RecruitClasses.Crusader, RecruitClasses.For(ownsCrusader: false));
        Assert.Contains(RecruitClasses.Crusader, RecruitClasses.For(ownsCrusader: true));
        Assert.DoesNotContain("duelist", RecruitClasses.For(ownsCrusader: true));   // the DLC's other hero was not asked for
    }

    [Fact]
    public void ReynauldIsACrusaderWhenDd2HasOne()
    {
        var install = Dd1Install.Find();
        var dd1 = Dd1Campaign.Load(install);
        var buildings = Buildings.Load(install);
        var withDlc = Hamlet.NewEstate(7, dd1, buildings, new DlcCatalog());
        Assert.Equal("crusader", withDlc.Roster.Single(h => h.Name == "Reynauld").ClassId);
        var without = Hamlet.NewEstate(7, dd1, buildings, new FakeCatalog());
        Assert.Equal("man_at_arms", without.Roster.Single(h => h.Name == "Reynauld").ClassId);   // the stand-in
    }
}
