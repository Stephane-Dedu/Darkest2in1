using DarkestDungeon3.Dd2;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class FightBackdropLayoutTests
{
    [Theory]
    [InlineData(1920, 1080, 0f, 1f / 3f, 1f, 2f / 3f)]       // the virtual canvas itself
    [InlineData(1600, 900, 0f, 1f / 3f, 1f, 2f / 3f)]        // the test window: same proportions
    [InlineData(2560, 1080, 0.125f, 1f / 3f, 0.75f, 2f / 3f)] // ultrawide: bars left and right
    [InlineData(1280, 1024, 0f, 0.3828125f, 1f, 0.46875f)]   // 5:4: bars above and below
    public void TheSceneStripFollowsTheLetterboxedCanvas(int width, int height, float x, float y, float w, float h)
    {
        var strip = FightBackdropLayout.SceneStrip(width, height);
        Assert.Equal(x, strip.x, 4);
        Assert.Equal(y, strip.y, 4);
        Assert.Equal(w, strip.width, 4);
        Assert.Equal(h, strip.height, 4);
    }

    [Fact]
    public void TheStripIsTheTopOfTheCanvasInTextureCoordinates()
    {
        // Texture coordinates start at the bottom: the scene (top two thirds of the canvas) ends at v = 1.
        var strip = FightBackdropLayout.SceneStrip(1920, 1080);
        Assert.Equal(1f, strip.yMax, 4);
    }
}
