using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Core.Campaign;

namespace DarkestDungeon3.Core.Tests;

public sealed class ExpeditionFightTests
{
    private static ExpeditionState Ready()
    {
        var state = new ExpeditionState { Started = true, Party = { "a" }, Light = 73 };
        state.PartyStates["a"] = new ExpeditionHeroState { Hp = 12, HpMax = 40, Stress = 3,
            Outcome = new() { HeroId = "a" } };
        state.Pack.Add(Supply.Bandage, 2);
        state.Pack.Layout.AddRange(new string[] { null!, Supply.Bandage, null!, Supply.Torch });
        state.TravelEffects.Add(new TravelEffect { HeroId = "a", Kind = "bleed", DamageFraction = .05f, TurnsLeft = 3, StepProgress = .5f });
        return state;
    }

    [Fact]
    public void Restarting_an_unfinished_fight_rewinds_consumption_cures_and_health_together_after_reload()
    {
        var state = Ready();
        Assert.True(ExpeditionFight.Record(state, new FightPlan(), "battle", "arena", false, false));
        var ledger = new CombatSupplyLedger(state.Pack);
        ledger.Select(42, Supply.Bandage, 1, 4);
        Assert.True(ledger.ConfirmUse(42, "bandages", 1, 0));
        state.TravelEffects.Clear(); // the native item cured the bleed
        state.PartyStates["a"].Hp = 17;
        state.Light = 25;
        var loaded = SaveFile.FromJson(new SaveFile { Expedition = state }.ToJson()).Expedition;

        Assert.True(ExpeditionFight.RestoreParty(loaded));

        Assert.Equal(12, loaded.PartyStates["a"].Hp);
        Assert.Equal(73, loaded.Light);
        Assert.Equal(2, loaded.Pack.Count(Supply.Bandage));
        Assert.Equal(new string[] { null!, Supply.Bandage, null!, Supply.Torch }, loaded.Pack.Layout);
        var effect = Assert.Single(loaded.TravelEffects);
        Assert.Equal("bleed", effect.Kind);
        Assert.Equal(.05f, effect.DamageFraction);
        Assert.Equal(3, effect.TurnsLeft);
        Assert.Equal(.5f, effect.StepProgress);
    }

    [Fact]
    public void Repeated_restores_copy_owned_pack_and_status_snapshots_without_aliasing()
    {
        var state = Ready();
        Assert.True(ExpeditionFight.Record(state, new FightPlan(), "battle", null, false, false));
        state.Pack.Add(Supply.Bandage, -1);
        state.Pack.Layout[1] = Supply.Herbs;
        state.TravelEffects[0].DamageFraction = .9f;
        state.TravelEffects[0].TurnsLeft = 1;
        Assert.True(ExpeditionFight.RestoreParty(state));
        Assert.Equal(2, state.Pack.Count(Supply.Bandage));
        Assert.Equal(Supply.Bandage, state.Pack.Layout[1]);
        Assert.Equal(.05f, state.TravelEffects[0].DamageFraction);
        state.Pack.Items.Clear();
        state.Pack.Layout.Clear();
        state.TravelEffects[0].StepProgress = 0;
        state.TravelEffects.Clear();
        Assert.True(ExpeditionFight.RestoreParty(state));
        Assert.Equal(2, state.Pack.Count(Supply.Bandage));
        Assert.Equal(4, state.Pack.Layout.Count);
        Assert.Equal(.5f, Assert.Single(state.TravelEffects).StepProgress);
    }

    [Fact]
    public void Empty_starting_statuses_remove_later_enemy_statuses_when_fight_restarts()
    {
        var state = Ready();
        state.TravelEffects.Clear();
        Assert.True(ExpeditionFight.Record(state, new FightPlan(), "battle", null, false, false));
        state.TravelEffects.Add(new TravelEffect { HeroId = "a", Kind = "blight", DamageFraction = .1f, TurnsLeft = 2 });
        Assert.True(ExpeditionFight.RestoreParty(state));
        Assert.Empty(state.TravelEffects);
    }

    [Fact]
    public void Legacy_checkpoints_without_supply_snapshots_preserve_existing_pack_and_statuses()
    {
        var state = Ready();
        Assert.True(ExpeditionFight.Record(state, new FightPlan(), "battle", null, false, false));
        var json = Newtonsoft.Json.Linq.JObject.Parse(new SaveFile { Expedition = state }.ToJson());
        var checkpoint = (Newtonsoft.Json.Linq.JObject)json["Expedition"]!["FightCheckpoint"]!;
        checkpoint.Remove("Pack");
        checkpoint.Remove("TravelEffects");
        var loaded = SaveFile.FromJson(json.ToString()).Expedition;
        loaded.Pack.TryUse(Supply.Bandage);
        loaded.TravelEffects[0].TurnsLeft = 2;
        var pack = loaded.Pack;
        var effects = loaded.TravelEffects;

        Assert.True(ExpeditionFight.RestoreParty(loaded));

        Assert.Same(pack, loaded.Pack);
        Assert.Same(effects, loaded.TravelEffects);
        Assert.Equal(1, loaded.Pack.Count(Supply.Bandage));
        Assert.Equal(2, loaded.TravelEffects[0].TurnsLeft);
        Assert.Equal(12, loaded.PartyStates["a"].Hp);
    }
}
