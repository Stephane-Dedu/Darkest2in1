using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class ExpeditionPartyTests
{
    [Fact]
    public void CaptureOwnsItsListsAndOutcomeProjectionCannotChangeSavedCondition()
    {
        var state = new ExpeditionState { Party = { "a" }, RandomCounter = 27 };
        var snapshot = Snapshot("a", 3.5f);
        ExpeditionParty.Capture(state, new[] { snapshot });
        snapshot.Outcome.Quirks.Add("later_quirk"); snapshot.Outcome.Trinkets.Clear(); snapshot.Hp = 1;
        Assert.Equal(7.5f, state.PartyStates["a"].Hp);
        Assert.Equal(new[] { "saved_quirk" }, state.PartyStates["a"].Outcome.Quirks);
        Assert.Equal(new[] { "saved_ring" }, state.PartyStates["a"].Outcome.Trinkets);
        string before = Json(state);
        var outcomes = ExpeditionParty.Outcomes(state, new Estate());
        var outcome = Assert.Single(outcomes);
        Assert.Equal(4, outcome.Stress);
        outcome.Quirks.Clear(); outcome.Trinkets.Add("later_ring"); outcome.Died = true;
        Assert.Equal(before, Json(state));
        Assert.Equal(27, state.RandomCounter);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-3f)]
    [InlineData(4f)]
    public void LivingHealthNeverInventsADeathAndRawConditionSurvivesReload(float health)
    {
        var state = new ExpeditionState { Party = { "a" } };
        var snapshot = Snapshot("a", 6.25f); snapshot.Hp = health;
        ExpeditionParty.Capture(state, new[] { snapshot });
        var loaded = SaveFile.FromJson(Json(state)).Expedition;
        var actual = loaded.PartyStates["a"];
        Assert.Equal(health, actual.Hp); Assert.Equal(30, actual.HpMax);
        Assert.Equal(6.25f, actual.Stress); Assert.Equal(0.2f, actual.WoundPercent);
        Assert.False(actual.Outcome.Died);
        Assert.False(Assert.Single(ExpeditionParty.Outcomes(loaded, new Estate())).Died);
        Assert.Equal(Json(state), Json(loaded));
    }

    [Theory]
    [InlineData("health")]
    [InlineData("maximum")]
    [InlineData("stress")]
    [InlineData("wound")]
    [InlineData("foreign")]
    public void UnavailableForeignOrInvalidSamplesPreservePriorSavedCondition(string invalid)
    {
        var state = new ExpeditionState { Party = { "a" } };
        ExpeditionParty.Capture(state, new[] { Snapshot("a", 2) });
        string before = Json(state);
        var snapshot = Snapshot("a", 7);
        if (invalid == "health") snapshot.Hp = float.NaN;
        else if (invalid == "maximum") snapshot.HpMax = 0;
        else if (invalid == "stress") snapshot.Stress = float.PositiveInfinity;
        else if (invalid == "wound") snapshot.WoundPercent = 1.1f;
        else snapshot.Outcome.HeroId = "foreign";
        ExpeditionParty.Capture(state, new[] { snapshot, null });
        ExpeditionParty.Capture(state, Array.Empty<ExpeditionHeroState>());
        Assert.Equal(before, Json(state));
    }

    [Fact]
    public void RecordedDeathAndChangedLoadoutReachActualHomecomingAfterReload()
    {
        var dd1 = Dd1Campaign.Load(Dd1Install.Find());
        var estate = new Estate { Seed = 91 };
        estate.Roster.Add(new HeroRecord { Id = "a", ClassId = "highwayman", Name = "Survivor", Stress = 1, Quirks = { "stale_quirk" } });
        estate.Roster.Add(new HeroRecord { Id = "b", ClassId = "plague_doctor", Name = "Fallen" });
        var state = new ExpeditionState
        {
            Party = { "a", "b" }, Retreated = true, Ended = true,
            Quest = new QuestOffer { Dungeon = "dd2_city", Type = "explore", Difficulty = 1, Length = 1 }
        };
        var dead = Snapshot("b", 9); dead.Outcome.Died = true; dead.Outcome.CauseOfDeath = "recorded dungeon death";
        ExpeditionParty.Capture(state, new[] { Snapshot("a", 5), dead });
        var save = SaveFile.FromJson(new SaveFile { Estate = estate, Expedition = state }.ToJson());
        var outcomes = ExpeditionParty.Outcomes(save.Expedition, save.Estate);
        Assert.Equal(new[] { "a", "b" }, outcomes.Select(o => o.HeroId));
        Homecoming.Apply(save.Estate, dd1, save.Expedition, outcomes);
        var fallen = Assert.Single(save.Estate.Graveyard);
        Assert.Equal("b", fallen.Id); Assert.Equal("recorded dungeon death", fallen.CauseOfDeath);
        var survivor = Assert.Single(save.Estate.Roster);
        Assert.Equal("a", survivor.Id); Assert.Equal(7, survivor.Stress); // saved 5 plus the existing abandonment penalty
        Assert.Equal(new[] { "saved_quirk" }, survivor.Quirks);
        Assert.Equal(new[] { "saved_ring" }, survivor.WornTrinkets);
    }

    [Fact]
    public void LegacyFallbackUsesOnlyEstateEvidenceWithoutChangingIt()
    {
        var save = SaveFile.FromJson("{\"Estate\":{\"Roster\":[{\"Id\":\"a\",\"Stress\":7},{\"Id\":\"b\",\"IsDead\":true,\"CauseOfDeath\":\"old recorded death\"}]},\"Expedition\":{\"Party\":[\"a\",\"b\"],\"RandomCounter\":34}}");
        string before = save.ToJson();
        var outcomes = ExpeditionParty.Outcomes(save.Expedition, save.Estate);
        Assert.Equal(7, outcomes[0].Stress); Assert.False(outcomes[0].Died);
        Assert.True(outcomes[1].Died); Assert.Equal("old recorded death", outcomes[1].CauseOfDeath);
        Assert.All(outcomes, o => { Assert.Null(o.Quirks); Assert.Null(o.Trinkets); });
        Assert.Empty(save.Expedition.PartyStates);
        Assert.Equal(before, save.ToJson());
    }

    private static ExpeditionHeroState Snapshot(string id, float stress) => new()
    {
        Hp = 7.5f, HpMax = 30, Stress = stress, WoundPercent = 0.2f,
        Outcome = new HeroOutcome { HeroId = id, Quirks = new List<string> { "saved_quirk" }, Trinkets = new List<string> { "saved_ring" } }
    };
    private static string Json(ExpeditionState state) => new SaveFile { Expedition = state }.ToJson();
}
