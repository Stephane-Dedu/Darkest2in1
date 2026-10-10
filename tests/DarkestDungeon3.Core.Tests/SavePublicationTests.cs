using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Expedition;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class SavePublicationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd3-save-publication-" + Guid.NewGuid().ToString("N"));
    private string SavePath => Path.Combine(_root, "synthetic.json");
    public SavePublicationTests() => Directory.CreateDirectory(_root);
    public void Dispose()
    {
        string resolved = Path.GetFullPath(_root);
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(resolved));
        Assert.StartsWith("dd3-save-publication-", Path.GetFileName(resolved));
        Directory.Delete(resolved, true);
    }

    [Fact]
    public void InitialAndRepeatedPublicationRetainExactCurrentAndPreviousCompleteSaves()
    {
        var save = Save();
        save.Save(SavePath);
        Assert.Equal(save.ToJson(), File.ReadAllText(SavePath));
        Assert.False(File.Exists(SavePath + ".bak")); Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        for (int i = 0; i < 3; i++)
        {
            string previous = save.ToJson();
            save.Estate.Week++; save.Expedition.RandomCounter++; save.Expedition.PartyStates["a"].Hp--;
            save.Save(SavePath);
            Assert.Equal(save.ToJson(), File.ReadAllText(SavePath));
            Assert.Equal(previous, File.ReadAllText(SavePath + ".bak"));
            Assert.Equal(save.ToJson(), SaveFile.Load(SavePath).ToJson());
            Assert.Equal(previous, SaveFile.Load(SavePath + ".bak").ToJson());
            Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        }
    }

    [Fact]
    public void WindowsExclusiveLockFailurePreservesDestinationBackupAndLeavesNoTemporaryPayload()
    {
        if (!OperatingSystem.IsWindows()) return; // Windows sharing semantics are the target of this case.
        var save = Save(); save.Save(SavePath);
        save.Estate.Week++; save.Save(SavePath);
        string current = File.ReadAllText(SavePath), backup = File.ReadAllText(SavePath + ".bak");
        save.Estate.Week++;
        using (var locked = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => save.Save(SavePath));
        Assert.Equal(current, File.ReadAllText(SavePath)); Assert.Equal(backup, File.ReadAllText(SavePath + ".bak"));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        save.Save(SavePath); // A retry after releasing the external lock can publish normally.
        Assert.Equal(save.ToJson(), File.ReadAllText(SavePath)); Assert.Equal(current, File.ReadAllText(SavePath + ".bak"));
    }

    [Fact]
    public void FailedFirstPublicationKeepsExistingDirectoryAndCleansItsTemporaryFile()
    {
        Directory.CreateDirectory(SavePath);
        string sentinel = Path.Combine(SavePath, "keep.txt"); File.WriteAllText(sentinel, "unchanged");
        Assert.ThrowsAny<IOException>(() => Save().Save(SavePath));
        Assert.True(Directory.Exists(SavePath)); Assert.Equal("unchanged", File.ReadAllText(sentinel));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp")); Assert.False(File.Exists(SavePath + ".bak"));
    }

    private static SaveFile Save() => new()
    {
        Estate = new Estate { Week = 3, Name = "Synthetic" },
        Expedition = new ExpeditionState
        {
            Started = true, RandomCounter = 17, Party = { "a" },
            PartyStates = { ["a"] = new ExpeditionHeroState { Hp = 12, HpMax = 30, Stress = 4.5f, Outcome = new HeroOutcome { HeroId = "a" } } },
            PendingSpoils = new BattleSpoils { Kind = "hall", LeftBehind = { new LootDrop { Type = "gold", Amount = 150 } } }
        }
    };
}
