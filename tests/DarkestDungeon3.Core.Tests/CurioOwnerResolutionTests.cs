using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json;

namespace DarkestDungeon3.Core.Tests;

public sealed class CurioOwnerResolutionTests : IDisposable
{
    private const string Cc = "580100_crimson_court", Com = "735730_color_of_madness";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd3-curio-owner-" + Guid.NewGuid().ToString("N"));

    public CurioOwnerResolutionTests()
    {
        Write("scripts/map_generator.darkest", "");
        Directory.CreateDirectory(Path.Combine(_root, "dungeons"));
        Write("curios/curio_type_library.csv", Sheet("cc_named_base"));
        Write($"dlc/{Cc}/curios/cc_curio_type_library.csv", Sheet("ordinary_cache"));
        Write($"dlc/{Com}/curios/com_curio_type_library.csv", Sheet("another_cache"));
        Write("curios/curio_props.csv", "Prop,Sprite\ncc_named_base,shared_sprite\n");
        Write($"dlc/{Cc}/curios/cc_curio_props.csv", "Prop,Sprite\nordinary_cache,shared_sprite\n");
        WriteTables("loot/loot.json", Table("A", Item(1)), Table("NESTED", Link("A")), Table("SECOND", Item(2)));
        WriteTables($"dlc/{Cc}/features/crimson_court/loot/common_overrides.loot.json", Table("A", Item(7)));
        WriteTables($"dlc/{Com}/loot/color_of_madness.loot.json", Table("SECOND", Item(4)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("skeleton_key")]
    public void OneResolverAlternatesOwnersWithoutChangingReportsSuppliesOrRandomSteps(string? supply)
    {
        var install = Dd1Install.Find(_root)!;
        var library = CurioLibrary.Load(install, includeDlc: true);
        var original = LootTables.Load(install);
        var scopes = Scopes(install);
        var resolver = new CurioResolver(library, null, original, scopes);
        // Mutating the caller's registration map cannot remove validated content from this resolver.
        var cc = scopes[Cc]; var com = scopes[Com]; scopes.Clear();
        var party = new FakeParty("hero");
        foreach (var (id, expectedLoot, amount) in new[] {
                     ("cc_named_base", original, 4), ("ordinary_cache", cc, 16),
                     ("another_cache", com, 6), ("cc_named_base", original, 4) })
        {
            var expectedState = State(); var actualState = State();
            var expectedRandom = new Rng(871); var actualRandom = new Rng(871);
            var expected = new CurioResolver(library, null, expectedLoot)
                .Resolve(id, "hero", supply, expectedState, new FakeParty("hero"), expectedRandom);
            var actual = resolver.Resolve(id, "hero", supply, actualState, party, actualRandom);
            Assert.Equal(2, actual.Loot.Count); // Separate result rows retain their separate drops.
            Assert.Equal(amount, actual.Loot.Sum(d => d.Amount));
            Assert.Equal(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(actual));
            Assert.Equal(JsonConvert.SerializeObject(expectedState), JsonConvert.SerializeObject(actualState));
            Assert.Equal(actualRandom.NextDouble(), expectedRandom.NextDouble());
            Assert.Equal(supply == null ? 1 : 0, actualState.Pack.Count("skeleton_key"));
        }
        Assert.Equal("shared_sprite", resolver.SpriteOf("ordinary_cache"));
        Assert.Equal("base", library.ContentSourceOf("cc_named_base"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrNullRequiredScopeFailsBeforeAnyInteraction(bool nullScope)
    {
        var install = Dd1Install.Find(_root)!;
        var library = CurioLibrary.Load(install, includeDlc: true);
        var scopes = Scopes(install);
        if (nullScope) scopes[Cc] = null!; else scopes.Remove(Cc);
        var state = State(); var random = new Rng(42);
        string before = JsonConvert.SerializeObject(state);
        Assert.Throws<InvalidOperationException>(() => new CurioResolver(library, null, LootTables.Load(install), scopes));
        Assert.Equal(before, JsonConvert.SerializeObject(state));
        Assert.Equal(new Rng(42).NextDouble(), random.NextDouble());
        Assert.Equal(1, state.Pack.Count("skeleton_key"));
    }

    [Fact]
    public void StandaloneParsedOwnershipIsNotGuessedFromAnIdPrefix()
    {
        var install = Dd1Install.Find(_root)!;
        var library = CurioLibrary.Parse(Sheet("cc_unannotated").Split('\n'));
        var original = LootTables.Load(install);
        Assert.Throws<InvalidOperationException>(() => new CurioResolver(library, null, original, Scopes(install)));
        // The original constructor retains its standalone-parser behavior.
        var report = new CurioResolver(library, null, original)
            .Resolve("cc_unannotated", "hero", "skeleton_key", State(), new FakeParty("hero"), new Rng(42));
        Assert.Equal(new[] { 2, 2 }, report.Loot.Select(d => d.Amount));
    }

    private static ExpeditionState State()
    {
        var state = new ExpeditionState { Quest = new QuestOffer { Dungeon = "crypts", Difficulty = 1 } };
        state.Pack.Add("skeleton_key", 1);
        return state;
    }
    private static Dictionary<string, LootTables> Scopes(Dd1Install install) => new()
    { [Cc] = LootTables.LoadForContent(install, Cc), [Com] = LootTables.LoadForContent(install, Com) };
    private static string Sheet(string id) =>
        $",1,Cache,,Good\n,,ID STRING,,RESULT TYPES\n,,{id},,Loot,1,,NESTED,2,,SECOND,1\n,,REGION FOUND\n,,crypts\n,,Item Interactions,,ITEM,RESULT TYPE\n,,,,skeleton_key,Loot,,NESTED,2,,SECOND,1\n";
    private static object Item(int amount) => new { chances = 1, type = "item", data = new { type = "gold", amount } };
    private static object Link(string id) => new { chances = 1, type = "table", data = new { table = id } };
    private static object Table(string id, params object[] entries) => new { id, difficulty = 0, dungeon = "", entries };
    private void WriteTables(string path, params object[] tables) => Write(path, JsonConvert.SerializeObject(new { loot_tables = tables }));
    private void Write(string relative, string text)
    {
        string path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
    public void Dispose()
    {
        string full = Path.GetFullPath(_root);
        string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (Path.GetDirectoryName(full) != parent || !Path.GetFileName(full).StartsWith("dd3-curio-owner-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing cleanup outside the owned temporary fixture");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
