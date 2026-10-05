using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Runtime;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class Dd1ResourceTests
{
    [Fact]
    public void FontsRequestedBeforeContentReadinessRecoverTheirActualGlyphMetrics()
    {
        var previous = Session.Current;
        try
        {
            Session.Current = null;
            Texture2D.ApiThreads.Clear();
            string[] names = { "ubuntu", "ubuntu_m", "dwarvenaxe-m", "dwarvenaxe-l" };
            foreach (string name in names) Assert.Null(Dd1Font.Get(name));
            Assert.Empty(Texture2D.ApiThreads);
            Session.Current = new Session { Dd1 = Dd1Install.Find() };
            foreach (string name in names)
            {
                var font = Dd1Font.Get(name);
                Assert.NotNull(font);
                Assert.Same(font, Dd1Font.Get(name));
                Assert.True(font.Measure("Watch Intro Cinematic", 30).x > 100);
                Assert.Equal(30, font.Measure("Watch Intro Cinematic", 30).y);
                Assert.True(font.Wrap("Watch Intro Cinematic", 30, 100).Count > 1);
            }
            Assert.All(Texture2D.ApiThreads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
        }
        finally { Session.Current = previous; }
    }

    [Fact]
    public void TextTableRequestedBeforeContentReadinessLoadsItsNativeLabelLater()
    {
        var previous = Session.Current;
        try
        {
            Session.Current = null;
            Assert.Null(Dd1Text.Get("miscellaneous", "menu_base_element_watch_intro"));
            Session.Current = new Session();
            Assert.Null(Dd1Text.Get("miscellaneous", "menu_base_element_watch_intro"));
            Session.Current.Dd1 = Dd1Install.Find();
            Assert.Equal("Watch Intro Cinematic", Dd1Text.Get("miscellaneous", "menu_base_element_watch_intro"));
            Assert.Null(Dd1Text.Get("miscellaneous", "unknown_native_label"));
        }
        finally { Session.Current = previous; }
    }

    [Fact]
    public void GenuineMissingFilesStillCacheAfterReadiness()
    {
        var previous = Session.Current;
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_resources_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        string name = "missing_" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "scripts"));
            Directory.CreateDirectory(Path.Combine(fixture, "dungeons"));
            Directory.CreateDirectory(Path.Combine(fixture, "localization"));
            File.WriteAllText(Path.Combine(fixture, "scripts", "map_generator.darkest"), "");
            Session.Current = new Session { Dd1 = Dd1Install.Find(fixture) };
            Assert.Equal(fixture, Session.Current.Dd1.Root);
            Assert.Null(Dd1Font.Get(name));
            Assert.Null(Dd1Text.Get(name, "label"));
            File.WriteAllText(Path.Combine(fixture, "localization", name + ".string_table.xml"),
                "<language id=\"english\"><entry id=\"label\"><![CDATA[Created later]]></entry></language>");
            Assert.Null(Dd1Text.Get(name, "label"));
            Assert.Null(Dd1Font.Get(name));
        }
        finally
        {
            Session.Current = previous;
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }
}
