using DarkestDungeon3.Runtime;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public sealed class SpriteArtTests
{
    private static Sprite Tight(float width = 2, float height = 4) => new()
    {
        texture = new Texture2D(1000, 500, TextureFormat.RGBA32, false),
        packed = true,
        packingMode = SpritePackingMode.Tight,
        vertices = new[] { new Vector2(0, 0), new Vector2(width, 0), new Vector2(0, height) },
        uv = new[] { new Vector2(.7f, .5f), new Vector2(.7f, .2f), new Vector2(.3f, .5f) },
        triangles = new ushort[] { 0, 1, 2 },
    };

    [Fact]
    public void TightAtlasRetainsItsTriangleAndRotatedUvsWithoutReadingTextureRect()
    {
        Event.current = new Event { type = EventType.Repaint };
        var sprite = Tight();
        int before = Graphics.Draws;
        SpriteArt.Draw(new Rect(10, 20, 100, 100), sprite);
        Assert.Equal(0, sprite.RectReads);
        Assert.Equal(before + 1, Graphics.Draws);
        Assert.Equal(new[] { 0, 1, 2 }, Graphics.LastMesh.triangles);
        Assert.Equal(sprite.uv, Graphics.LastMesh.uv);
        Assert.Equal(200, Graphics.LastMesh.vertices[1].x);
        Assert.Equal(400, Graphics.LastMesh.vertices[2].y);
        Assert.Equal(35, GUI.LastTextureDraw.Rect.x);
        Assert.Equal(20, GUI.LastTextureDraw.Rect.y);
        Assert.Equal(50, GUI.LastTextureDraw.Rect.width);
        var image = GUI.LastTextureDraw.Texture;
        SpriteArt.Draw(new Rect(0, 0, 100, 100), sprite, flipX: true);
        Assert.Same(image, GUI.LastTextureDraw.Texture);
        Assert.Equal(before + 1, Graphics.Draws);
        Assert.Equal(1, GUI.LastTextureDraw.Uv.x);
        Assert.Equal(-1, GUI.LastTextureDraw.Uv.width);
    }

    [Fact]
    public void RectangularSpritesKeepTheDirectAtlasPathAndBottomAlignment()
    {
        Event.current = new Event { type = EventType.Repaint };
        var sprite = new Sprite
        {
            texture = new Texture2D(1000, 500, TextureFormat.RGBA32, false),
            packed = true, packingMode = SpritePackingMode.Rectangle,
            Region = new Rect(20, 30, 100, 50),
        };
        int before = Graphics.Draws;
        SpriteArt.Draw(new Rect(0, 0, 100, 100), sprite, flipX: true);
        Assert.Equal(before, Graphics.Draws);
        Assert.Same(sprite.texture, GUI.LastTextureDraw.Texture);
        Assert.Equal(50, GUI.LastTextureDraw.Rect.y);
        Assert.Equal(.12f, GUI.LastTextureDraw.Uv.x, 5);
        Assert.Equal(-.1f, GUI.LastTextureDraw.Uv.width, 5);
    }

    [Fact]
    public void GpuFailureRestoresStateReleasesItsTextureAndRetriesAfterFiveSeconds()
    {
        Event.current = new Event { type = EventType.Repaint };
        var sprite = Tight();
        var previous = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture.active = previous;
        bool srgb = GL.sRGBWrite;
        int depth = GL.MatrixDepth, before = Graphics.Draws;
        Time.unscaledTime += 10;
        Graphics.FailDraw = true;
        try
        {
            SpriteArt.Draw(new Rect(0, 0, 100, 100), sprite);
            Assert.Same(previous, RenderTexture.active);
            Assert.Equal(srgb, GL.sRGBWrite);
            Assert.Equal(depth, GL.MatrixDepth);
            Assert.False(RenderTexture.Created.Last().IsCreated());
            SpriteArt.Draw(new Rect(0, 0, 100, 100), sprite);
            Assert.Equal(before + 1, Graphics.Draws);
            Time.unscaledTime += 6;
            Graphics.FailDraw = false;
            SpriteArt.Draw(new Rect(0, 0, 100, 100), sprite);
            Assert.Equal(before + 2, Graphics.Draws);
        }
        finally { Graphics.FailDraw = false; RenderTexture.active = null; }
    }

    [Fact]
    public void PackedCopiesHaveBoundedResolutionAndEvictOldImagesAtThePixelBudget()
    {
        Event.current = new Event { type = EventType.Repaint };
        var images = new List<RenderTexture>();
        for (int i = 0; i < 12; i++)
        {
            SpriteArt.Draw(new Rect(0, 0, 100, 100), Tight(20, 20));
            images.Add(Assert.IsType<RenderTexture>(GUI.LastTextureDraw.Texture));
        }
        Assert.All(images, image => Assert.True(image.width <= 1024 && image.height <= 1024));
        Assert.False(images[0].IsCreated());
        Assert.True(images.Last().IsCreated());
        Assert.True(images.Where(image => image.IsCreated()).Sum(image => image.width * image.height) <= 8 * 1024 * 1024);
    }

    [Fact]
    public void InputEventsDoNotInspectOrBakeSprites()
    {
        Event.current = new Event { type = EventType.MouseDown };
        var sprite = Tight();
        int before = Graphics.Draws;
        SpriteArt.Draw(new Rect(0, 0, 100, 100), sprite);
        Assert.Equal(before, Graphics.Draws);
        Assert.Equal(0, sprite.RectReads);
    }
}
