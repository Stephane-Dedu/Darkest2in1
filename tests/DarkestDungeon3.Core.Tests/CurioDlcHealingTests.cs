using DarkestDungeon3.Core;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Tests;

public sealed class CurioDlcHealingTests : IDisposable
{
    private const string Com = "735730_color_of_madness";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd3-curio-heal-" + Guid.NewGuid().ToString("N"));

    public CurioDlcHealingTests()
    {
        Write("scripts/map_generator.darkest", "");
        Directory.CreateDirectory(Path.Combine(_root, "dungeons"));
        Write("curios/curio_type_library.csv", Sheet("base_heal"));
        Write($"dlc/{Com}/curios/com_curio_type_library.csv", Sheet("content_heal"));
        Write("loot/loot.json", "{\"loot_tables\":[]}");
        Write($"dlc/{Com}/loot/color_of_madness.loot.json", "{\"loot_tables\":[]}");
    }

    [Theory]
    [InlineData("performer", "heal_percent", "1.0", 1f, .2f, 9f, 8f)]
    [InlineData("performer_group", "heal_percent", "0.70", .8f, .9f, 9f, 8f)]
    [InlineData("performer", "healstress", "100", .1f, .2f, -1f, 8f)]
    [InlineData("performer_group", "healstress", "60", .1f, .2f, 3f, 2f)]
    public void CanonicalHealingHonorsPercentageOrGroupTargetsAndSkipsDeadHeroes(
        string target, string field, string value, float frontHp, float rearHp, float frontStress, float rearStress)
    {
        var resolver = Resolver(target, field, value);
        var party = Party(); var rng = new Rng(321);
        var report = resolver.Resolve("content_heal", "front", null, new ExpeditionState(), party, rng);
        Assert.Equal(frontHp, party.Hp["front"], 5);
        Assert.Equal(rearHp, party.Hp["rear"], 5);
        Assert.Equal(frontStress, party.Stress["front"], 5);
        Assert.Equal(rearStress, party.Stress["rear"], 5);
        Assert.Equal(0, party.Hp["dead"]);
        Assert.Equal(7, party.Stress["dead"]);
        Assert.Equal(new[] { "restore" }, report.Effects);
        // One ordinary outcome and one effect choice; recipients do not add random draws.
        var expected = new Rng(321); expected.NextDouble(); expected.NextDouble();
        Assert.Equal(expected.NextDouble(), rng.NextDouble());
    }

    [Fact]
    public void GroupHealingChecksEachRecipientsResolvePermission()
    {
        var party = new RefusingParty(); Initialize(party);
        Resolver("performer_group", "heal_percent", "0.70")
            .Resolve("content_heal", "front", null, new ExpeditionState(), party, new Rng(1));
        Assert.Equal(.8f, party.Hp["front"], 5);
        Assert.Equal(.2f, party.Hp["rear"], 5);
    }

    [Theory]
    [InlineData("heal_percent", "0.70", .1f, 9f)]
    [InlineData("healstress", "60", .1f, 3f)]
    public void BaseCurioAndPublicEffectEntryPointRetainOriginalSingleHeroBehavior(
        string field, string value, float hp, float stress)
    {
        var resolver = Resolver("performer_group", field, value);
        var baseParty = Party(); var directParty = Party();
        resolver.Resolve("base_heal", "front", null, new ExpeditionState(), baseParty, new Rng(42));
        resolver.ApplyEffect("restore", "front", directParty, new Rng(42));
        foreach (var party in new[] { baseParty, directParty })
        {
            Assert.Equal(hp, party.Hp["front"], 5); Assert.Equal(stress, party.Stress["front"], 5);
            Assert.Equal(.2f, party.Hp["rear"], 5); Assert.Equal(8, party.Stress["rear"]);
        }
    }

    private CurioResolver Resolver(string target, string field, string value)
    {
        var install = Dd1Install.Find(_root)!;
        var effects = new EffectLibrary();
        effects.Effects.Add("restore", DarkestFile.Parse($"effect: .name restore .target {target} .chance 100% .{field} {value}").Single());
        return new CurioResolver(CurioLibrary.Load(install, true), effects, LootTables.Load(install),
            new Dictionary<string, LootTables> { [Com] = LootTables.LoadForContent(install, Com) });
    }
    private static FakeParty Party() { var party = new FakeParty("front", "rear", "dead"); Initialize(party); return party; }
    private static void Initialize(FakeParty party)
    {
        party.Hp["front"] = .1f; party.Hp["rear"] = .2f; party.Hp["dead"] = 0;
        party.Stress["front"] = 9; party.Stress["rear"] = 8; party.Stress["dead"] = 7;
    }
    private sealed class RefusingParty : FakeParty, IResolveParty
    {
        public RefusingParty() : base("front", "rear", "dead") { }
        public bool Allows(string heroId, string reaction) => heroId != "rear";
    }
    private static string Sheet(string id) => $",1,Restoration,,Good\n,,ID STRING,,RESULT TYPES\n,,{id},,Effect,1,,restore,1\n";
    private void Write(string relative, string text)
    {
        string path = Path.Combine(_root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text);
    }
    public void Dispose()
    {
        string full = Path.GetFullPath(_root); string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (Path.GetDirectoryName(full) != parent || !Path.GetFileName(full).StartsWith("dd3-curio-heal-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing cleanup outside the owned temporary fixture");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
