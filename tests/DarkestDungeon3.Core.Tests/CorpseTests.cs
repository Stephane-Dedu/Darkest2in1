using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>DD1's corpse rule (death_class in each monster's info.darkest).</summary>
public class CorpseTests
{
    private static readonly Dd1Corpse Corpses = new(Dd1Install.Find());

    [Fact]
    public void OnlyMonstersWithACorpseClassLeaveOne()
    {
        Assert.False(Corpses.LeavesCorpse("maggot_A", crit: false, dot: false));           // no death_class: the ranks close up
        Assert.True(Corpses.LeavesCorpse("brigand_blood_A", crit: false, dot: false));     // corpse_large_A
        Assert.False(Corpses.LeavesCorpse("brigand_blood_A", crit: true, dot: false));     // .is_valid_on_crit False
        Assert.False(Corpses.LeavesCorpse("brigand_blood_A", crit: false, dot: true));     // bleed/blight: not set = no corpse
        Assert.True(Corpses.LeavesCorpse("skeleton_common_A", crit: false, dot: false));
        Assert.Null(Corpses.LeavesCorpse("shared_carrion_eater", crit: false, dot: false)); // not a DD1 monster
    }
}
