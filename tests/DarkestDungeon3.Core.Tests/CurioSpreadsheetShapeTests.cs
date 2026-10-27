using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Tests;

public class CurioSpreadsheetShapeTests
{
    [Fact]
    public void UnnumberedBlockDoesNotBecomePreviousCurioItems()
    {
        var library = CurioLibrary.Parse(new[]
        {
            ",1,Old Cache,,Mixed",
            ",,ID STRING,,RESULT TYPES",
            ",,old_cache,,Nothing,1",
            ",,Item Interactions,,ITEM,RESULT TYPE",
            ",,,,shovel,Loot,,OLD_LOOT,1",
            ",,Unnumbered Cache,,Good",
            ",,ID STRING,,RESULT TYPES",
            ",,new_cache,,Nothing,1",
            ",,REGION FOUND,,Loot,2,,NEW_LOOT,3",
            ",,Courtyard,,Quirk",
            ",,Item Interactions,,ITEM,RESULT TYPE",
            ",,,,skeleton_key,Loot,,KEY_LOOT,2"
        });

        Assert.Equal(2, library.Curios.Count);
        var old = library.Get("old_cache");
        Assert.Equal("shovel", Assert.Single(old.Items).Item);
        Assert.Equal("OLD_LOOT", Assert.Single(old.Items[0].Outcome.Results).Name);
        var added = library.Get("new_cache");
        Assert.Equal("Unnumbered Cache", added.Name);
        Assert.Equal("Good", added.Category);
        Assert.Equal("Courtyard", added.Region);
        Assert.Equal("skeleton_key", Assert.Single(added.Items).Item);
        var loot = Assert.Single(added.Outcomes.Where(x => x.Type == "Loot"));
        Assert.Equal(2, loot.Weight);
        Assert.Equal(3, Assert.Single(loot.Results).Weight);
    }

    [Fact]
    public void IdentifierRowDoesNotRequireANothingOutcome()
    {
        var library = CurioLibrary.Parse(new[]
        {
            ",7,Quiet Visitor,,Mixed",
            ",,ID STRING,,RESULT TYPES",
            ",,quiet_visitor,,,,,,,,",
            ",,REGION FOUND,,Loot,1,,VISITOR_GIFT,1",
            ",,COM,,,,",
            ",,Item Interactions,,ITEM,RESULT TYPE"
        });

        var visitor = Assert.Single(library.Curios).Value;
        Assert.Equal("quiet_visitor", visitor.Id);
        Assert.Equal("Quiet Visitor", visitor.Name);
        Assert.Equal("COM", visitor.Region);
        Assert.Empty(visitor.Items);
        var outcome = Assert.Single(visitor.Outcomes);
        Assert.Equal("Loot", outcome.Type);
        Assert.Equal("VISITOR_GIFT", Assert.Single(outcome.Results).Name);
    }

    [Fact]
    public void SummonOutcomeKeepsItsCanonicalEncounterAndWeights()
    {
        var library = CurioLibrary.Parse(new[]
        {
            ",,Infested Object,,Bad",
            ",,ID STRING,,RESULT TYPES",
            ",,infested_object,,Nothing",
            ",,REGION FOUND,,Summon,12,,encounter_object_1,0",
            ",,ALL,,Loot"
        });

        var outcome = Assert.Single(library.Get("infested_object").Outcomes);
        Assert.Equal("Summon", outcome.Type);
        Assert.Equal(12, outcome.Weight);
        var encounter = Assert.Single(outcome.Results);
        Assert.Equal("encounter_object_1", encounter.Name);
        Assert.Equal(0, encounter.Weight);
    }
}
