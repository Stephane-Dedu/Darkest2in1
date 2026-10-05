using Assets.Code.Game;
using Assets.Code.Run;
using Assets.Code.Utils;
using DarkestDungeon3.Dd2;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class RunRecoveryTests : IDisposable
{
    private readonly GameModeMgr _modes = new();
    private readonly RunBhv _run = new();
    public RunRecoveryTests()
    {
        Dd2Api.Modes = _modes;
        Singleton<GameTypeMgr>.Instance = new(); SingletonMonoBehaviour<RunBhv>.Instance = _run;
        Dd2Run.End(); _modes.ModeRequests = 0;
        GameModeMgr.CurrentMode = GameModeType.MAIN_MENU;
    }
    public void Dispose() { Dd2Run.End(); Dd2Api.Modes = null; }

    [Fact]
    public void BusyOrMissingModeManagerNeverStartsOrReportsReady()
    {
        int ready = 0;
        _modes.Changing = true;
        Assert.False(Dd2Run.Start(() => ready++));
        Dd2Api.Modes = null;
        Assert.False(Dd2Run.Start(() => ready++));
        Assert.False(Dd2Run.Hosting); Assert.Equal(0, ready); Assert.Equal(0, _modes.ModeRequests);
        Assert.False(TextBasedEditorPrefsBaseType.DRIVING_DISABLE_COMBATS.Value);
    }

    [Fact]
    public void NativeCompletedEntryControlsReadyAndDrivingRetryDoesNotStartAnotherRun()
    {
        int ready = 0, failed = 0;
        Assert.True(Dd2Run.Start(() => ready++, () => failed++));
        Assert.True(Dd2Run.Hosting); Assert.Equal(0, ready);
        _modes.Complete(GameModeType.HERO_SELECT); Assert.Equal(0, ready);
        _modes.Complete(GameModeType.DRIVING); Assert.Equal(1, ready);
        Assert.Equal(1, _run.NewRunRequests); Assert.Equal(1, _modes.ModeRequests);
        Assert.True(Dd2Run.Start(() => ready++, () => failed++));
        Assert.Equal(2, ready); Assert.Equal(0, failed);
        Assert.Equal(1, _run.NewRunRequests); Assert.Equal(1, _modes.ModeRequests);
        Assert.True(TextBasedEditorPrefsBaseType.DRIVING_DISABLE_COMBATS.Value);
    }

    [Fact]
    public void SetupExceptionIsReportedAndCanRetryOnTheReadyRoad()
    {
        int failed = 0, ready = 0;
        Assert.True(Dd2Run.Start(() => throw new InvalidOperationException("synthetic setup failure"), () => failed++));
        _modes.Complete(GameModeType.DRIVING);
        Assert.Equal(1, failed);
        Assert.True(Dd2Run.Start(() => ready++, () => failed++));
        Assert.Equal(1, ready); Assert.Equal(1, failed); Assert.Equal(1, _run.NewRunRequests);
    }

    [Fact]
    public void ExhaustedTransitionsReportFailureInsteadOfPretendingTheDungeonOpened()
    {
        int ready = 0, failed = 0;
        Assert.True(Dd2Run.Start(() => ready++, () => failed++));
        for (int i = 0; i < 7; i++) _modes.Complete(GameModeType.HERO_SELECT);
        Assert.Equal(0, ready); Assert.Equal(1, failed);
        Dd2Run.End(); Assert.False(Dd2Run.Hosting);
        Assert.False(TextBasedEditorPrefsBaseType.DRIVING_DISABLE_COMBATS.Value);
    }

    [Fact]
    public void EndingOrReplacingAHostCancelsItsStaleReadyCallbacks()
    {
        int oldReady = 0, newReady = 0;
        Assert.True(Dd2Run.Start(() => oldReady++));
        Dd2Run.End();
        Assert.True(Dd2Run.Start(() => newReady++));
        _modes.Complete(GameModeType.DRIVING);
        Assert.Equal(0, oldReady); Assert.Equal(1, newReady);
        Dd2Run.End();
        _modes.Complete(GameModeType.DRIVING);
        Assert.Equal(0, oldReady); Assert.Equal(1, newReady);
    }
}
