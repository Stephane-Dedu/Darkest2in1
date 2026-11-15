namespace DarkestDungeon3.Core.Tests;

public class Dd1TestInstallTests
{
    [Fact]
    public void AnExplicitInstallWinsOverSteamDiscovery()
    {
        string root = Path.Combine(Path.GetTempPath(), "dd3-core-data-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "dungeons"));
        File.WriteAllText(Path.Combine(root, "scripts", "map_generator.darkest"), "");
        try { Assert.Equal(Path.GetFullPath(root), Dd1TestInstall.From(root).Root); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void AnInvalidExplicitInstallFailsInsteadOfSelectingAnotherDrive()
    {
        string missing = Path.Combine(Path.GetTempPath(), "dd3-core-missing-" + Guid.NewGuid());
        Assert.Throws<DirectoryNotFoundException>(() => Dd1TestInstall.From(missing));
    }
}
