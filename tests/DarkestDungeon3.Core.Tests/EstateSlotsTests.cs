using DarkestDungeon3.Core.Campaign;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public sealed class EstateSlotsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd3-estate-list-" + Guid.NewGuid().ToString("N"));
    public EstateSlotsTests() => Directory.CreateDirectory(_root);
    public void Dispose()
    {
        string resolved = Path.GetFullPath(_root);
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(resolved));
        Assert.StartsWith("dd3-estate-list-", Path.GetFileName(resolved));
        Directory.Delete(resolved, true);
    }

    [Fact]
    public void DiscoveryIncludesEstatesBeyondThreeAndBackupOnlyWithoutParsingOrChangingFiles()
    {
        foreach (string name in new[] { "estate_9.json", "estate_1.json", "estate_1.json.bak", "estate_4.json.bak", "estate_0.json", "estate_03.json", "estate_7.json.tmp", "estate_999999999999.json", "other.json" })
            File.WriteAllText(Path.Combine(_root, name), "deliberately unreadable");
        var before = Directory.GetFiles(_root).ToDictionary(path => path, File.ReadAllText);
        Assert.Equal(new[] { 1, 4, 9 }, EstateSlots.List(_root));
        Assert.Equal(10, EstateSlots.Next(_root));
        Assert.Equal(before.Count, Directory.GetFiles(_root).Length);
        foreach (var pair in before) Assert.Equal(pair.Value, File.ReadAllText(pair.Key));
    }

    [Fact]
    public void ANewSlotDoesNotOverwriteExistingCampaignsAndDeletionCannotRecoverItsBackup()
    {
        var first = new SaveFile { Estate = new Estate { Name = "First" } };
        first.Save(EstateSlots.PathFor(_root, 1));
        var next = new SaveFile { Estate = new Estate { Name = "Second" } };
        int slot = EstateSlots.Next(_root);
        string path = EstateSlots.PathFor(_root, slot);
        next.Save(path); next.Estate.Week = 5; next.Save(path);
        Assert.True(File.Exists(path + ".bak"));
        EstateSlots.Delete(_root, slot);
        Assert.Null(SaveFile.Load(path));
        Assert.False(File.Exists(path + ".bak"));
        Assert.Equal(first.ToJson(), File.ReadAllText(EstateSlots.PathFor(_root, 1)));
        Assert.Equal(new[] { 1 }, EstateSlots.List(_root));
        EstateSlots.Delete(_root, slot); // repeat after a refresh is harmless
    }

    [Fact]
    public void MissingDirectoryIsEmptyAndDeletingABackupOnlySlotLeavesUnrelatedFiles()
    {
        string absent = Path.Combine(_root, "absent");
        Assert.Empty(EstateSlots.List(absent)); Assert.Equal(1, EstateSlots.Next(absent));
        Assert.False(Directory.Exists(absent));
        File.WriteAllText(EstateSlots.PathFor(_root, 20) + ".bak", "broken save can still be deleted");
        File.WriteAllText(Path.Combine(_root, "keep.json"), "keep");
        EstateSlots.Delete(_root, 20);
        Assert.Equal("keep", File.ReadAllText(Path.Combine(_root, "keep.json")));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidIdsCannotTargetOtherPaths(int slot)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EstateSlots.PathFor(_root, slot));
        Assert.Throws<ArgumentOutOfRangeException>(() => EstateSlots.Delete(_root, slot));
    }

    [Fact]
    public void ClearingAnActiveSlotDropsItsSaveAndDestination()
    {
        string path = EstateSlots.PathFor(_root, 2);
        new SaveFile { Estate = new Estate { Name = "Selected" } }.Save(path);
        var active = new CampaignSaveSlot(); active.Select(path); active.Clear();
        Assert.Null(active.Save); Assert.Null(active.Path);
        Assert.Equal("Selected", SaveFile.Load(path).Estate.Name);
    }
}
