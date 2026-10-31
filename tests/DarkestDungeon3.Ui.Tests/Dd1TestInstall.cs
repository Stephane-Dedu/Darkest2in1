using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Ui.Tests;

/// <summary>Use an explicitly selected test data install; keep ordinary Steam discovery when unset.</summary>
internal static class Dd1TestInstall
{
    internal static Dd1Install Find() => From(Environment.GetEnvironmentVariable("DD3_TEST_DD1"));

    internal static Dd1Install From(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) return Dd1Install.Find();
        string fullPath = Path.GetFullPath(configuredPath);
        if (!Dd1Install.LooksLikeDd1(fullPath))
            throw new DirectoryNotFoundException("DD3_TEST_DD1 must point to a valid DD1 data install: " + fullPath);
        return Dd1Install.Find(fullPath);
    }
}
