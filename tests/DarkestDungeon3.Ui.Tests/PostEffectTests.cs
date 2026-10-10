using DarkestDungeon3.Dd2;
using Xunit;
namespace DarkestDungeon3.Ui.Tests;

public class PostEffectTests
{
    private sealed class FloatParameter { public float value { get; set; } public bool overrideState = true; }
    private sealed class ColourAdjustment
    {
        public bool active = true;
        public FloatParameter postExposure = new() { value = 2.25f };
        public FloatParameter contrast = new() { value = 30f };
        public FloatParameter saturation = new() { value = -15f };
    }
    private sealed class Bloom { public bool active = true; }
    [Fact]
    public void NeutralColoursPreserveLiveExposureAndRestoreTheirOriginalValues()
    {
        var colour = new ColourAdjustment();
        var guard = new PostEffectGuard();
        guard.NeutralParameter(colour, "contrast", 0f);
        guard.NeutralParameter(colour, "saturation", 0f);
        guard.Apply();
        Assert.True(colour.active);
        Assert.Equal(2.25f, colour.postExposure.value);
        Assert.Equal(0f, colour.contrast.value);
        Assert.True(colour.contrast.overrideState);
        colour.postExposure.value = 3f;
        colour.contrast.value = 40f;
        guard.NeutralParameter(colour, "contrast", 0f);
        guard.Apply();
        Assert.Equal(3f, colour.postExposure.value);
        Assert.Equal(0f, colour.contrast.value);
        guard.Restore();
        Assert.Equal(30f, colour.contrast.value);
        Assert.Equal(-15f, colour.saturation.value);
        Assert.Equal(3f, colour.postExposure.value);
        Assert.Equal(0, guard.Count);
    }
    [Fact]
    public void SharedAndPerCameraBloomStayOffEvenIfNativeSkillsToggleThem()
    {
        var shared = new Bloom();
        var cameraCopy = new Bloom { active = false };
        var guard = new PostEffectGuard();
        guard.Disable(shared);
        guard.Disable(cameraCopy);
        guard.Apply();
        Assert.False(shared.active);
        shared.active = cameraCopy.active = true;
        guard.Disable(shared);
        guard.Disable(cameraCopy);
        guard.Apply();
        Assert.False(shared.active);
        Assert.False(cameraCopy.active);
        guard.Restore();
        Assert.True(shared.active);
        Assert.False(cameraCopy.active);
    }
    [Fact]
    public void AbsentParametersAreIgnoredAcrossUrpVersions()
    {
        var guard = new PostEffectGuard();
        guard.NeutralParameter(new Bloom(), "contrast", 0f);
        guard.Disable(new object());
        guard.Apply();
        guard.Restore();
        Assert.Equal(0, guard.Count);
    }
}
