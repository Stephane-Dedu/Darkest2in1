using DarkestDungeon3.Core.Campaign;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class CampaignSaveSlotTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd3-slot-selection-" + Guid.NewGuid().ToString("N"));
    public CampaignSaveSlotTests() => Directory.CreateDirectory(_root);
    public void Dispose()
    {
        string resolved = Path.GetFullPath(_root);
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(resolved));
        Assert.StartsWith("dd3-slot-selection-", Path.GetFileName(resolved)); Directory.Delete(resolved, true);
    }

    [Fact]
    public void FailedCrossSlotReadClearsPriorEstateAndDestinationWithoutWritingEitherFile()
    {
        string first = Path.Combine(_root, "first.json"), second = Path.Combine(_root, "second.json");
        var save = new SaveFile { Estate = new Estate { Name = "First", Week = 7 } }; save.Save(first);
        File.WriteAllText(second, "{broken");
        var selected = new CampaignSaveSlot(); selected.Select(first);
        Assert.Equal(first, selected.Path); Assert.Equal("First", selected.Save.Estate.Name);
        Assert.Throws<InvalidDataException>(() => selected.Select(second));
        Assert.Null(selected.Save); Assert.Null(selected.Path);
        Assert.Equal(save.ToJson(), File.ReadAllText(first)); Assert.Equal("{broken", File.ReadAllText(second));
        Assert.Equal(2, Directory.GetFiles(_root).Length);
    }

    [Fact]
    public void NewRecoveredAndSuccessfulRetrySelectionsAssignOnlyTheirOwnSavePathPair()
    {
        string path = Path.Combine(_root, "selected.json");
        var selected = new CampaignSaveSlot(); selected.Select(path);
        Assert.Null(selected.Save); Assert.Equal(path, selected.Path); Assert.Empty(Directory.GetFiles(_root));
        var expected = new SaveFile { Estate = new Estate { Name = "Recovered" } };
        File.WriteAllText(path + ".bak", expected.ToJson()); selected.Select(path);
        Assert.True(selected.Save.RecoveredFromBackup); Assert.Equal(path, selected.Path);
        File.WriteAllText(path, "{broken"); File.WriteAllText(path + ".bak", "{broken");
        Assert.Throws<InvalidDataException>(() => selected.Select(path)); Assert.Null(selected.Save); Assert.Null(selected.Path);
        File.WriteAllText(path, expected.ToJson()); selected.Select(path);
        Assert.False(selected.Save.RecoveredFromBackup); Assert.Equal(path, selected.Path);
        Assert.Equal("Recovered", selected.Save.Estate.Name);
    }
}
