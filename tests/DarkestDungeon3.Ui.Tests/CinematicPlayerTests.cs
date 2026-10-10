using System.Diagnostics;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Runtime;
using UnityEngine;
using UnityEngine.Video;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class CinematicPlayerTests
{
    [Fact]
    public void ColdVoiceWaitsWithoutStartingAudioThenBeginsSubtitlesAtZero()
    {
        Fixture((fixture, name, source) =>
        {
            CinematicPlayer.Play(name);
            Assert.True(CinematicPlayer.Active);
            Assert.True(Dd1Audio.Hush);
            Assert.Empty(Dd1Audio.StartThreads);
            Assert.Empty(VideoPlayer.Created);
            WaitForVoice(name, source);
            Time.unscaledTime += 10;
            CinematicPlayer.Draw();
            Assert.Single(Dd1Audio.StartThreads);
            Assert.All(Dd1Audio.StartThreads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
            Assert.Contains("Fixture narration", Gui.Texts);
            Gui.Texts.Clear();
            Dd1Audio.Last.IsPlaying = false;
            Time.unscaledTime += 2;
            CinematicPlayer.Draw();
            Assert.False(CinematicPlayer.Active);
            Assert.False(Dd1Audio.Hush);
        });
    }

    [Fact]
    public void SkippingPendingVoiceDoesNotStartItOrLeaveTheCinematicActive()
    {
        Fixture((fixture, name, source) =>
        {
            CinematicPlayer.Play(name);
            Assert.True(CinematicPlayer.Active);
            Event.current = new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape };
            CinematicPlayer.Draw();
            Assert.Equal(EventType.Used, Event.current.type);
            Assert.False(CinematicPlayer.Active);
            Assert.False(Dd1Audio.Hush);
            Assert.Empty(Dd1Audio.StartThreads);
            Assert.Empty(VideoPlayer.Created);
        });
    }

    [Fact]
    public void VideoPreparationWaitsForVoiceAndStartsBothOnTheDrawingThread()
    {
        Fixture((fixture, name, source) =>
        {
            Directory.CreateDirectory(Path.Combine(fixture, "cache"));
            File.WriteAllBytes(Path.Combine(fixture, "cache", "dd1_" + name + ".webm"), Array.Empty<byte>());
            CinematicPlayer.Play(name);
            Assert.Empty(VideoPlayer.Created);
            Assert.Empty(Dd1Audio.StartThreads);
            WaitForVoice(name, source);
            CinematicPlayer.Draw();
            Assert.True(Assert.Single(VideoPlayer.Created).isPlaying);
            Assert.Single(Dd1Audio.StartThreads);
            Assert.All(Dd1Audio.StartThreads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
            Event.current = new Event { type = EventType.MouseDown };
            CinematicPlayer.Draw();
            Assert.False(CinematicPlayer.Active);
            Assert.False(Dd1Audio.Last.IsPlaying);
            Assert.False(VideoPlayer.Created[0].isPlaying);
        });
    }

    private static void WaitForVoice(string name, string source)
    {
        var watch = Stopwatch.StartNew();
        bool complete = false;
        while (!complete && watch.Elapsed.TotalSeconds < 10)
        {
            CinematicCache.Voice(name, source, out complete);
            if (!complete) Thread.Sleep(1);
        }
        Assert.True(complete);
    }

    private static void Fixture(Action<string, string, string> test)
    {
        var previous = Session.Current;
        string previousSaveDir = Session.SaveDir;
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(tempRoot, "dd3_player_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
        string name = "fixture_" + Guid.NewGuid().ToString("N");
        string source = Path.Combine(fixture, "video", name + ".ogv");
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "scripts"));
            Directory.CreateDirectory(Path.Combine(fixture, "dungeons"));
            Directory.CreateDirectory(Path.Combine(fixture, "video"));
            Directory.CreateDirectory(Path.Combine(fixture, "localization"));
            File.WriteAllText(Path.Combine(fixture, "scripts", "map_generator.darkest"), "");
            var page = new byte[35];
            new byte[] { 79, 103, 103, 83 }.CopyTo(page, 0);
            page[5] = 2; page[14] = 1; page[26] = 1; page[27] = 7;
            new byte[] { 1, 118, 111, 114, 98, 105, 115 }.CopyTo(page, 28);
            File.WriteAllBytes(source, page);
            File.WriteAllText(Path.Combine(fixture, "video", name + ".sub"), "0,1000,");
            File.WriteAllText(Path.Combine(fixture, "localization", "miscellaneous.string_table.xml"),
                "<language id=\"english\"><entry id=\"str_vo_" + name + "_0\"><![CDATA[Fixture narration]]></entry></language>");
            Session.Current = new Session { Dd1 = Dd1Install.Find(fixture) };
            Assert.Equal(fixture, Session.Current.Dd1.Root);
            Session.SaveDir = fixture;
            Session.SaveDirReads.Clear();
            Event.current = new Event { type = EventType.Repaint };
            Time.unscaledTime = 1000;
            VideoPlayer.Created.Clear();
            Dd1Audio.StartThreads.Clear();
            Gui.Texts.Clear();
            test(fixture, name, source);
            Assert.All(Session.SaveDirReads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
        }
        finally
        {
            for (int i = 0; CinematicPlayer.Active && i < 3; i++)
            {
                Event.current = new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape };
                CinematicPlayer.Draw();
            }
            WaitForVoice(name, source); // a skipped worker still owns its files until completion
            Session.Current = previous;
            Session.SaveDir = previousSaveDir;
            Assert.Equal(tempRoot, Path.GetDirectoryName(Path.GetFullPath(fixture)));
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
    }
}
