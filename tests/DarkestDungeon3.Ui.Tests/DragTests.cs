using DarkestDungeon3.Ui;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class DragTests : IDisposable
{
    private static readonly Rect Source = new(946, 174, 600, 101), Roster = new(1538, 132, 395, 776);
    private sealed record Recruit(string HeroId);

    private static void Send(EventType type, float x, float y)
    {
        Time.frameCount++;
        Event.current = new Event { type = type, rawType = type, mousePosition = new Vector2(x, y) };
    }

    public void Dispose()
    {
        Send(EventType.MouseUp, 0, 0);
        Drag.Overlay();
        GUI.enabled = true;
        GUIUtility.hotControl = 0;
        GUIUtility.clipOffset = default;
    }

    [Fact]
    public void RecruitReleaseIsReservedBeforeRosterButtonsAndDeliveredOnce()
    {
        Send(EventType.MouseDown, 1100, 200);
        Drag.Source(Source, new Recruit("recruit"), _ => { });
        GUIUtility.hotControl = 17; // the source's ordinary click button claimed the press
        Send(EventType.MouseDrag, 1600, 200);
        Drag.Begin();
        Assert.True(Drag.Active); // promotion must not depend on drawing the source again
        Assert.Equal(0, GUIUtility.hotControl);
        Assert.Equal(EventType.Used, Event.current.type);
        Send(EventType.MouseUp, 1600, 200);
        Drag.Begin();
        Assert.Equal(EventType.Used, Event.current.type); // a roster button cannot turn it into a hero click
        Assert.True(Drag.Drop<Recruit>(Roster, out var recruit));
        Assert.Equal("recruit", recruit.HeroId);
        // The real recruitment/save path, not just a payload assertion.
        var estate = new Estate { Recruits = { new HeroRecord { Id = recruit.HeroId, ClassId = "vestal", Name = "Vestal" } } };
        var hamlet = new Hamlet(estate, null, Buildings.Load(Dd1Install.Find()), null);
        Assert.True(hamlet.Recruit(recruit.HeroId));
        Assert.Empty(estate.Recruits);
        Assert.Single(estate.Roster);
        Assert.True(Drag.JustDropped);
        Assert.False(Drag.Drop<Recruit>(Roster, out _));
        Drag.Overlay();
        Assert.False(Drag.Active);
    }

    [Fact]
    public void OrdinaryClickAndSmallMovementRemainAvailableToTheSourceButton()
    {
        Send(EventType.MouseDown, 1100, 200);
        Drag.Source(Source, new Recruit("recruit"), _ => { });
        Send(EventType.MouseDrag, 1104, 202);
        Drag.Begin();
        Assert.False(Drag.Active);
        Assert.Equal(EventType.MouseDrag, Event.current.type);
        Send(EventType.MouseUp, 1104, 202);
        Drag.Begin();
        Assert.Equal(EventType.MouseUp, Event.current.type);
        Assert.False(Drag.Drop<Recruit>(Roster, out _));
        Drag.Overlay();
    }

    [Fact]
    public void DroppingOutsideTargetsClearsTheDragForTheNextRecruit()
    {
        Send(EventType.MouseDown, 1100, 200);
        Drag.Source(Source, new Recruit("first"), _ => { });
        Send(EventType.MouseDrag, 600, 600);
        Drag.Begin();
        Send(EventType.MouseUp, 600, 600);
        Drag.Begin();
        Assert.False(Drag.Drop<Recruit>(Roster, out _));
        Drag.Overlay();
        Assert.False(Drag.Active);
        Send(EventType.MouseDown, 1100, 200);
        Drag.Source(Source, new Recruit("second"), _ => { });
        Send(EventType.MouseDrag, 1600, 200);
        Drag.Begin();
        Send(EventType.MouseUp, 1600, 200);
        Drag.Begin();
        Assert.True(Drag.Drop<Recruit>(Roster, out var second));
        Assert.Equal("second", second.HeroId);
    }

    [Fact]
    public void DisabledUnderlyingPageCannotStartARecruitDrag()
    {
        GUI.enabled = false;
        Send(EventType.MouseDown, 1100, 200);
        Drag.Source(Source, new Recruit("under_modal"), _ => { });
        GUI.enabled = true;
        Send(EventType.MouseDrag, 1600, 200);
        Drag.Begin();
        Assert.False(Drag.Active);
        Send(EventType.MouseUp, 1600, 200);
        Drag.Begin();
        Assert.False(Drag.Drop<Recruit>(Roster, out _));
    }

    [Fact]
    public void SourceInsideAScrollGroupUsesScreenCoordinatesForTheThreshold()
    {
        GUIUtility.clipOffset = new Vector2(900, 170);
        Send(EventType.MouseDown, 50, 20);
        Drag.Source(new Rect(0, 0, 100, 100), new Recruit("scrolled"), _ => { });
        GUIUtility.clipOffset = default; // Begin runs at the root, outside that scroll group
        Send(EventType.MouseDrag, 954, 192);
        Drag.Begin();
        Assert.False(Drag.Active); // actual movement was only (4,2), not (904,172)
        Send(EventType.MouseDrag, 970, 190);
        Drag.Begin();
        Assert.True(Drag.Active);
    }

    [Fact]
    public void RecruitReleasedOverRosterWithoutIntermediateDragEventMustStillHire()
    {
        Send(EventType.MouseDown, 1100, 200);
        Drag.Source(Source, new Recruit("fast_recruit"), _ => { });
        GUIUtility.hotControl = 17;
        // Some input/frame sequences deliver the distant release without a separate MouseDrag pass.
        Send(EventType.MouseUp, 1600, 200);
        Drag.Begin();
        Assert.Equal(EventType.Used, Event.current.type);
        Assert.True(Drag.Drop<Recruit>(Roster, out var carried));
        var estate = new Estate { Recruits = { new HeroRecord { Id = carried.HeroId, ClassId = "vestal" } } };
        var hamlet = new Hamlet(estate, null, Buildings.Load(Dd1Install.Find()), null);
        Assert.True(hamlet.Recruit(carried.HeroId));
        Assert.Empty(estate.Recruits); Assert.Single(estate.Roster);
        Assert.False(Drag.Drop<Recruit>(Roster, out _));
        Assert.Equal(0, GUIUtility.hotControl);
        Drag.Overlay(); Assert.False(Drag.Active);
    }

    [Fact]
    public void FastReleaseOutsideRosterCancelsAndTheNextRecruitCanBeHired()
    {
        Send(EventType.MouseDown, 1100, 200);
        Drag.Source(Source, new Recruit("cancelled"), _ => { });
        GUIUtility.hotControl = 17;
        Send(EventType.MouseUp, 600, 600);
        Drag.Begin();
        Assert.True(Drag.Active);
        Assert.False(Drag.Drop<Recruit>(Roster, out _));
        Drag.Overlay();
        Assert.False(Drag.Active);
        Assert.Equal(0, GUIUtility.hotControl);
        Send(EventType.MouseDown, 1100, 200);
        Drag.Source(Source, new Recruit("next"), _ => { });
        Send(EventType.MouseUp, 1600, 200);
        Drag.Begin();
        Assert.True(Drag.Drop<Recruit>(Roster, out var next));
        Assert.Equal("next", next.HeroId);
    }

    [Theory]
    [InlineData(0, 1104, 202)] // below threshold: ordinary details click
    [InlineData(1, 1600, 200)] // another button must not promote a left press
    public void ReleaseWithoutDragDoesNotPromoteClicksOrAnotherButton(int button, float x, float y)
    {
        Send(EventType.MouseDown, 1100, 200);
        Drag.Source(Source, new Recruit("click"), _ => { });
        Send(EventType.MouseUp, x, y);
        Event.current.button = button;
        Drag.Begin();
        Assert.False(Drag.Active);
        Assert.Equal(EventType.MouseUp, Event.current.type);
        Assert.False(Drag.Drop<Recruit>(Roster, out _));
        Drag.Overlay();
    }

    [Fact]
    public void EscapeCancelsACarriedRecruitWithoutLeavingMouseCapture()
    {
        Send(EventType.MouseDown, 1100, 200);
        Drag.Source(Source, new Recruit("recruit"), _ => { });
        Send(EventType.MouseDrag, 1600, 200);
        Drag.Begin();
        GUIUtility.hotControl = 99;
        Send(EventType.KeyDown, 1600, 200);
        Event.current.keyCode = KeyCode.Escape;
        Drag.Begin();
        Assert.False(Drag.Active);
        Assert.Equal(0, GUIUtility.hotControl);
        Assert.Equal(EventType.Used, Event.current.type);
    }
}
