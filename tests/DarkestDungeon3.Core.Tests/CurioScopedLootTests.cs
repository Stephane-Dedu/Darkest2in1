using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json;

namespace DarkestDungeon3.Core.Tests;

public sealed class CurioScopedLootTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd3-curio-loot-" + Guid.NewGuid().ToString("N"));

    public CurioScopedLootTests()
    {
        Write("scripts/map_generator.darkest", "");
        Directory.CreateDirectory(Path.Combine(_root, "dungeons"));
        WriteTables("loot/loot.json", Table("A", Item(1)), Table("NESTED", Link("A")));
    }

    [Fact]
    public void NestedContentDrawUsesLocalOverrideWithoutMutatingBaseOrRandomSteps()
    {
        WriteTables("dlc/580100_crimson_court/features/crimson_court/loot/crimson_court.loot.json",
            Table("A", Item(3)), Table("CC_ONLY", Link("A")));
        WriteTables("dlc/580100_crimson_court/features/crimson_court/loot/common_overrides.loot.json", Table("A", Item(7)));
        WriteTables("dlc/580100_crimson_court/features/districts/loot/blueprint_overrides.loot.json", Table("A", Item(99)));
        var install = Dd1Install.Find(_root)!;
        var original = LootTables.Load(install);
        var scoped = LootTables.LoadForContent(install, "580100_crimson_court");
        var beforeRandom = new Rng(42);
        var scopedRandom = new Rng(42);
        Assert.Equal(1, Assert.Single(original.Roll("NESTED", 1, 1, "crypts", beforeRandom)).Amount);
        Assert.Equal(7, Assert.Single(scoped.Roll("CC_ONLY", 1, 1, "crypts", scopedRandom)).Amount);
        Assert.Equal(beforeRandom.NextDouble(), scopedRandom.NextDouble());
        Assert.False(original.Has("CC_ONLY"));
        Assert.Equal(1, Draw(original, "A"));
        Assert.Equal(1, Draw(LootTables.Load(install), "A"));
    }

    [Fact]
    public void ColorOfMadnessScopeKeepsBaseFallbackAndExcludesOtherContent()
    {
        WriteTables("dlc/735730_color_of_madness/loot/color_of_madness.loot.json", Table("COM_ONLY", Link("A")));
        WriteTables("dlc/580100_crimson_court/features/crimson_court/loot/common_overrides.loot.json",
            Table("A", Item(9)), Table("CC_ONLY", Item(9)));
        var install = Dd1Install.Find(_root)!;
        var scoped = LootTables.LoadForContent(install, "735730_color_of_madness");
        Assert.Equal(1, Draw(scoped, "COM_ONLY"));
        Assert.False(scoped.Has("CC_ONLY"));
        Assert.Equal(1, Draw(LootTables.LoadForContent(install, "base"), "A"));
    }

    [Fact]
    public void UnknownOrIncompleteContentFailsInsteadOfPretendingBaseLootIsDlc()
    {
        var install = Dd1Install.Find(_root)!;
        Assert.Throws<ArgumentException>(() => LootTables.LoadForContent(install, "../other"));
        Assert.Throws<DirectoryNotFoundException>(() => LootTables.LoadForContent(install, "580100_crimson_court"));
        Directory.CreateDirectory(Path.Combine(_root, "dlc/580100_crimson_court/features/crimson_court/loot"));
        Assert.Throws<InvalidDataException>(() => LootTables.LoadForContent(install, "580100_crimson_court"));
    }

    private static int Draw(LootTables tables, string id) => Assert.Single(tables.Roll(id, 1, 1, "crypts", new Rng(42))).Amount;
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
        if (Path.GetDirectoryName(full) != parent || !Path.GetFileName(full).StartsWith("dd3-curio-loot-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing cleanup outside the owned temporary fixture");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
