using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Ui;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class EstatePickerSlotTests
{
    [Fact]
    public void ActualSummaryDistinguishesNewUnavailableRecoveredAndRefreshedWithoutWriting()
    {
        string root = Path.Combine(Path.GetTempPath(), "dd3-picker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "synthetic.json");
            var empty = EstatePickerSlot.Read(path);
            Assert.True(empty.CanOpen); Assert.Null(empty.Save); Assert.Null(empty.Error);
            Assert.Empty(Directory.GetFiles(root));
            File.WriteAllText(path, "{broken");
            var unavailable = EstatePickerSlot.Read(path);
            Assert.False(unavailable.CanOpen); Assert.Null(unavailable.Save); Assert.NotNull(unavailable.Error);
            var saved = new SaveFile { Estate = new Estate { Name = "Kept estate", Week = 13 } };
            File.WriteAllText(path + ".bak", saved.ToJson());
            var recovered = EstatePickerSlot.Read(path);
            Assert.True(recovered.CanOpen); Assert.True(recovered.Save.RecoveredFromBackup);
            Assert.Equal("Kept estate", recovered.Save.Estate.Name); Assert.Equal("{broken", File.ReadAllText(path));
            Assert.Equal(saved.ToJson(), File.ReadAllText(path + ".bak"));
            File.WriteAllText(path, saved.ToJson());
            var refreshed = EstatePickerSlot.Read(path);
            Assert.True(refreshed.CanOpen); Assert.False(refreshed.Save.RecoveredFromBackup);
            Assert.Null(refreshed.Error); Assert.Equal(saved.ToJson(), refreshed.Save.ToJson());
            Assert.False(unavailable.CanOpen); // a previous failed summary cannot silently become a new estate
        }
        finally
        {
            string resolved = Path.GetFullPath(root);
            Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(resolved));
            Assert.StartsWith("dd3-picker-", Path.GetFileName(resolved)); Directory.Delete(resolved, true);
        }
    }
}
