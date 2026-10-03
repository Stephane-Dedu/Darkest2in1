using System.Collections.Generic;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>DD1's quirk limits: 5 positive, 5 negative, 3 diseases; over the cap a new quirk replaces an unlocked one.</summary>
public class QuirkLimitTests
{
    private static readonly QuirkLimits Limits = QuirkLimits.FromDd1(Dd1Campaign.Load(Dd1Install.Find()).Rules);
    private static bool Positive(string q) => q.StartsWith("pos_");
    private static bool Disease(string q) => q.StartsWith("disease_");

    [Fact]
    public void LimitsComeFromDd1()
    {
        Assert.Equal(5, Limits.MaxPositive);
        Assert.Equal(5, Limits.MaxNegative);
        Assert.Equal(3, Limits.MaxDiseases);
    }

    [Fact]
    public void OverTheCapReplacesAnUnlockedQuirkOfTheSameKind()
    {
        var quirks = new List<string> { "pos_1", "pos_2", "pos_3", "pos_4", "neg_1" };
        Assert.Null(Limits.Apply(quirks, new List<string>(), "pos_5", Positive, Disease, new Rng(1), out bool gained));
        Assert.True(gained);                                   // room for a fifth
        Assert.Equal(6, quirks.Count);

        var locked = new List<string> { "pos_1", "pos_2", "pos_3", "pos_4" };
        string replaced = Limits.Apply(quirks, locked, "pos_6", Positive, Disease, new Rng(1), out gained);
        Assert.True(gained);
        Assert.Equal("pos_5", replaced);                       // the only unlocked positive
        Assert.Contains("pos_6", quirks);
        Assert.DoesNotContain("pos_5", quirks);
        Assert.Contains("neg_1", quirks);                      // other kinds untouched

        locked.Add("pos_6");
        Assert.Null(Limits.Apply(quirks, locked, "pos_7", Positive, Disease, new Rng(1), out gained));
        Assert.False(gained);                                  // all five locked: not gained
        Assert.DoesNotContain("pos_7", quirks);

        Assert.Null(Limits.Apply(quirks, locked, "pos_6", Positive, Disease, new Rng(1), out gained));
        Assert.False(gained);                                  // already has it
    }

    [Fact]
    public void DiseasesCapAtThree()
    {
        var quirks = new List<string> { "disease_a", "disease_b", "disease_c" };
        string replaced = Limits.Apply(quirks, null, "disease_d", Positive, Disease, new Rng(3), out bool gained);
        Assert.True(gained);
        Assert.NotNull(replaced);
        Assert.Equal(3, quirks.Count);
    }
}
