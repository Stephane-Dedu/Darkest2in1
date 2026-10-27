using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;

namespace DarkestDungeon3.Core.Tests;

public sealed class CurioExpansionLoadTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd3-curio-dlc-" + Guid.NewGuid().ToString("N"));

    public CurioExpansionLoadTests()
    {
        Write("scripts/map_generator.darkest", "");
        Directory.CreateDirectory(Path.Combine(_root, "dungeons"));
        Write("curios/curio_type_library.csv", Sheet("base_cache", "Original cache"));
        Write("curios/curio_props.csv", "Prop,Sprite\nbase_cache,original_sprite\n");
    }

    [Fact]
    public void DlcDefinitionsAndSpriteAliasesRequireExplicitOptIn()
    {
        Write("dlc/100_campaign/curios/campaign_curio_type_library.csv", Sheet("extra_cache", "Extra cache"));
        Write("dlc/100_campaign/curios/campaign_curio_props.csv", "Prop,Sprite\nextra_cache,extra_sprite\n");
        var install = Dd1Install.Find(_root)!; // Valid explicit fixture is returned before any discovery paths.
        var original = CurioLibrary.Load(install);
        Assert.Single(original.Curios);
        Assert.Null(original.Get("extra_cache"));
        Assert.Equal("extra_cache", original.SpriteOf("extra_cache"));
        Assert.Equal("base", original.ContentSourceOf("base_cache"));
        Assert.False(CurioLibrary.LoadPropSprites(install).ContainsKey("extra_cache"));

        var expanded = CurioLibrary.Load(install, includeDlc: true);
        Assert.Equal(2, expanded.Curios.Count);
        Assert.Equal("Extra cache", expanded.Get("extra_cache").Name);
        Assert.Equal("extra_sprite", expanded.SpriteOf("extra_cache"));
        Assert.Equal("100_campaign", expanded.ContentSourceOf("extra_cache"));
        Assert.Null(expanded.ContentSourceOf("missing"));
        Assert.Null(CurioLibrary.Parse(Sheet("unannotated", "Standalone").Split('\n')).ContentSourceOf("unannotated"));
    }

    [Fact]
    public void BaseOwnershipAndStableDlcOrderSurviveCollisionsAndArenaIsExcluded()
    {
        Write("dlc/200_campaign/curios/b_curio_type_library.csv", Sheet("extra_cache", "Later cache"));
        Write("dlc/100_campaign/curios/a_curio_type_library.csv",
            Sheet("base_cache", "Forbidden override") + Sheet("extra_cache", "First cache"));
        Write("dlc/100_campaign/curios/a_curio_props.csv", "Prop,Sprite\nbase_cache,wrong_sprite\nextra_cache,first_sprite\n");
        Write("dlc/200_campaign/curios/b_curio_props.csv", "Prop,Sprite\nextra_cache,later_sprite\n");
        Write("dlc/000_arena/curios/arena_curio_type_library.csv", Sheet("arena_cache", "Arena cache"));
        Write("dlc/000_arena/curios/arena_curio_props.csv", "Prop,Sprite\narena_cache,arena_sprite\n");
        var expanded = CurioLibrary.Load(Dd1Install.Find(_root)!, includeDlc: true);
        Assert.Equal(2, expanded.Curios.Count);
        Assert.Equal("Original cache", expanded.Get("base_cache").Name);
        Assert.Equal("original_sprite", expanded.SpriteOf("base_cache"));
        Assert.Equal("base", expanded.ContentSourceOf("base_cache"));
        Assert.Equal("First cache", expanded.Get("extra_cache").Name);
        Assert.Equal("first_sprite", expanded.SpriteOf("extra_cache"));
        Assert.Equal("100_campaign", expanded.ContentSourceOf("extra_cache"));
        Assert.Null(expanded.Get("arena_cache"));
        Assert.Equal("arena_cache", expanded.SpriteOf("arena_cache"));
    }

    private static string Sheet(string id, string name) =>
        $",1,{name},,Good\n,,ID STRING,,RESULT TYPES\n,,{id},,Nothing,1\n";

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
        if (Path.GetDirectoryName(full) != parent || !Path.GetFileName(full).StartsWith("dd3-curio-dlc-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing cleanup outside the owned temporary fixture");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
