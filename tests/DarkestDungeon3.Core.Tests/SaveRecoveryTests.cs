using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class SaveRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd3-save-recovery-" + Guid.NewGuid().ToString("N"));
    private string SavePath => Path.Combine(_root, "synthetic.json");
    public SaveRecoveryTests() => Directory.CreateDirectory(_root);
    public void Dispose()
    {
        string resolved = Path.GetFullPath(_root);
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(resolved));
        Assert.StartsWith("dd3-save-recovery-", Path.GetFileName(resolved));
        Directory.Delete(resolved, true);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{unfinished")]
    [InlineData("null")]
    [InlineData("{\"Estate\":null}")]
    [InlineData("{\"Expedition\":{}}")]
    public void MissingOrInvalidMainRecoversExactCompleteBackupWithoutWriting(string main)
    {
        var expected = Save(); string backup = expected.ToJson();
        File.WriteAllText(SavePath + ".bak", backup);
        if (main != null) File.WriteAllText(SavePath, main);
        var recovered = SaveFile.Load(SavePath);
        Assert.True(recovered.RecoveredFromBackup); Assert.Equal(backup, recovered.ToJson());
        Assert.Equal(23, recovered.Expedition.RandomCounter); Assert.Equal(-2, recovered.Expedition.PartyStates["a"].Hp);
        Assert.Equal(37, Assert.Single(recovered.Expedition.PendingSpoils.LeftBehind).Amount);
        Assert.Equal(main != null, File.Exists(SavePath));
        if (main != null) Assert.Equal(main, File.ReadAllText(SavePath));
        Assert.Equal(backup, File.ReadAllText(SavePath + ".bak"));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        Assert.DoesNotContain("RecoveredFromBackup", recovered.ToJson());
    }

    [Fact]
    public void ValidMainIsPreferredEvenWhenBackupIsNewerOrCorrupt()
    {
        var current = Save(); string json = current.ToJson(); File.WriteAllText(SavePath, json);
        var other = Save(); other.Estate.Week = 99;
        foreach (var backup in new[] { other.ToJson(), "{broken" })
        {
            File.WriteAllText(SavePath + ".bak", backup);
            var loaded = SaveFile.Load(SavePath);
            Assert.False(loaded.RecoveredFromBackup); Assert.Equal(json, loaded.ToJson());
            Assert.Equal(backup, File.ReadAllText(SavePath + ".bak"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingUnrecoverablePayloadNeverLooksLikeANewEstate(bool backupOnly)
    {
        if (!backupOnly) File.WriteAllText(SavePath, "{broken-main");
        File.WriteAllText(SavePath + ".bak", "{broken-backup");
        Assert.Throws<InvalidDataException>(() => SaveFile.Load(SavePath));
        if (!backupOnly) Assert.Equal("{broken-main", File.ReadAllText(SavePath));
        Assert.Equal("{broken-backup", File.ReadAllText(SavePath + ".bak"));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void OnlyAnAbsentMainAndBackupMeansANewEstate()
    {
        Assert.Null(SaveFile.Load(SavePath));
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public void WindowsLockedMainPropagatesItsIoFailureRatherThanUsingOlderBackup()
    {
        if (!OperatingSystem.IsWindows()) return;
        string json = Save().ToJson(); File.WriteAllText(SavePath, json); File.WriteAllText(SavePath + ".bak", json);
        using (var locked = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => SaveFile.Load(SavePath));
        Assert.Equal(json, File.ReadAllText(SavePath)); Assert.Equal(json, File.ReadAllText(SavePath + ".bak"));
    }

    private static SaveFile Save() => new()
    {
        Estate = new Estate { Name = "Synthetic", Week = 8, Roster = { new HeroRecord { Id = "a", ClassId = "highwayman" } } },
        Expedition = new ExpeditionState
        {
            Started = true, RandomCounter = 23, Party = { "a" },
            PartyStates = { ["a"] = new ExpeditionHeroState { Hp = -2, HpMax = 30, Stress = 6.5f, Outcome = new HeroOutcome { HeroId = "a" } } },
            PendingSpoils = new BattleSpoils { Kind = "room", LeftBehind = { new LootDrop { Type = "gold", Amount = 37 } } }
        }
    };
}
