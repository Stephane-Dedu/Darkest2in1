using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class Dd1TestInstallTests
{
    [Fact]
    public void AnExplicitDataInstallTakesPrecedenceOverSteamDiscovery()
    {
        string root = Path.Combine(Path.GetTempPath(), "dd3-test-install-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "dungeons"));
        File.WriteAllText(Path.Combine(root, "scripts", "map_generator.darkest"), "");
        try { Assert.Equal(Path.GetFullPath(root), Dd1TestInstall.From(root).Root); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void AnInvalidExplicitPathCannotSilentlyFallBackToAnotherInstall()
    {
        string missing = Path.Combine(Path.GetTempPath(), "dd3-missing-test-install-" + Guid.NewGuid());
        Assert.Throws<DirectoryNotFoundException>(() => Dd1TestInstall.From(missing));
    }
}
