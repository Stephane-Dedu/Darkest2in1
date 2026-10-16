using System;
using System.Threading;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Composes the owned town paint from readback bytes. No Unity or GPU work runs here.</summary>
public static class HamletPaintBaker
{
    public const int TileSize = 512;
    public const int PaintSize = 2048;

    public sealed class Result
    {
        public byte[] Ground, Lanes, Valley;
        public readonly int Size;
        internal Result(int size) { Size = size; }
    }

    private readonly struct Rgb
    {
        public readonly float R, G, B, A;
        public Rgb(float r, float g, float b, float a = 1) { R = r; G = g; B = b; A = a; }
    }

    public static Result Bake(byte[] soil, byte[] stone, byte[] grass, byte[] rock,
        CancellationToken cancellation = default, int size = PaintSize, int fieldSize = 256)
    {
        ValidateTile(soil, nameof(soil)); ValidateTile(stone, nameof(stone)); ValidateTile(rock, nameof(rock));
        if (grass != null) ValidateTile(grass, nameof(grass));
        if (size < 1 || size > PaintSize) throw new ArgumentOutOfRangeException(nameof(size));
        if (fieldSize < 2 || fieldSize > 256) throw new ArgumentOutOfRangeException(nameof(fieldSize));
        cancellation.ThrowIfCancellationRequested();
        var result = new Result(size);
        Ground(result, soil, stone, grass, fieldSize, cancellation);
        Valley(result, soil, stone, grass ?? soil, rock, fieldSize, cancellation);
        return result;
    }

    private static void ValidateTile(byte[] pixels, string name)
    {
        if (pixels == null || pixels.Length != TileSize * TileSize * 4)
            throw new ArgumentException("Paint readbacks must contain a 512 by 512 RGBA tile.", name);
    }

    private static void Ground(Result output, byte[] earth, byte[] gravel, byte[] turf, int fieldSize, CancellationToken cancellation)
    {
        int size = output.Size;
        var pixels = new byte[size * size * 4]; var lanePixels = new byte[pixels.Length];
        var cover = new float[fieldSize * fieldSize]; var shade = new float[cover.Length];
        var wear = new float[cover.Length]; var laneField = new float[cover.Length];
        for (int y = 0; y < fieldSize; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < fieldSize; x++)
            {
                float px = Lerp(HamletLayout.MinX, HamletLayout.MaxX, (float)x / (fieldSize - 1));
                float pz = Lerp(HamletLayout.MinZ, HamletLayout.MaxZ, (float)y / (fieldSize - 1));
                int i = y * fieldSize + x;
                cover[i] = turf != null ? HamletVegetation.Cover(px, pz) : 0;
                shade[i] = HamletGroundPaint.ContactShade(px, pz);
                wear[i] = HamletGroundPaint.EdgeWear(px, pz);
                laneField[i] = HamletGroundPaint.LaneCover(px, pz);
            }
        }
        for (int y = 0; y < size; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < size; x++)
            {
                float px = Lerp(HamletLayout.MinX, HamletLayout.MaxX, (x + .5f) / size);
                float pz = Lerp(HamletLayout.MinZ, HamletLayout.MaxZ, (y + .5f) / size);
                var sample = HamletGroundPaint.At(px, pz);
                var a = Tile(earth, px / 10, pz / 3); var b = Tile(gravel, px / 5.5f + .37f, pz / 5.5f + .19f);
                float fx = (x + .5f) / size * (fieldSize - 1), fy = (y + .5f) / size * (fieldSize - 1);
                var color = Mix(a, b, Math.Max(sample.Gravel, Field(wear, fieldSize, fx, fy)));
                if (turf != null) color = Mix(color, Tile(turf, px / 7.5f, pz / 7.5f), Field(cover, fieldSize, fx, fy));
                float light = Field(shade, fieldSize, fx, fy), glow = HamletSetDressing.EntranceLight(px, pz);
                int i = (y * size + x) * 4;
                Write(pixels, i, new Rgb(color.R * sample.Tint.X * light + glow * .40f,
                    color.G * sample.Tint.Y * light + glow * .24f, color.B * sample.Tint.Z * light + glow * .065f));
                // The paving blends the final quantized ground colour, as in the original renderer.
                var ground = new Rgb(pixels[i] / 255f, pixels[i + 1] / 255f, pixels[i + 2] / 255f);
                Write(lanePixels, i, Mix(ground, Tile(gravel, px / 5, pz / 5), Field(laneField, fieldSize, fx, fy)));
            }
        }
        output.Ground = pixels; output.Lanes = lanePixels;
    }

    private static void Valley(Result output, byte[] earth, byte[] gravel, byte[] turf, byte[] bedrock, int fieldSize, CancellationToken cancellation)
    {
        int size = output.Size;
        var cover = new float[fieldSize * fieldSize]; var pixels = new byte[size * size * 4];
        for (int y = 0; y < fieldSize; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < fieldSize; x++)
                cover[y * fieldSize + x] = HamletLandscape.GrassCover(
                    Lerp(HamletLandscape.MinX, HamletLandscape.MaxX, (float)x / (fieldSize - 1)),
                    Lerp(HamletLandscape.MinZ, HamletLandscape.MaxZ, (float)y / (fieldSize - 1)));
        }
        for (int y = 0; y < size; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < size; x++)
            {
                float px = Lerp(HamletLandscape.MinX, HamletLandscape.MaxX, (x + .5f) / size);
                float pz = Lerp(HamletLandscape.MinZ, HamletLandscape.MaxZ, (y + .5f) / size);
                var sample = HamletGroundPaint.At(px, pz);
                float fx = (x + .5f) / size * (fieldSize - 1), fy = (y + .5f) / size * (fieldSize - 1);
                var color = Mix(Tile(earth, px / 10, pz / 3), Tile(gravel, px / 5.5f + .37f, pz / 5.5f + .19f), sample.Gravel);
                color = Mix(color, Tile(turf, px / 7.5f, pz / 7.5f), Field(cover, fieldSize, fx, fy));
                color = new Rgb(color.R * sample.Tint.X, color.G * sample.Tint.Y, color.B * sample.Tint.Z);
                color = Mix(color, Tile(gravel, px / 5, pz / 5), HamletLandscape.RoadCover(px, pz) * .88f);
                var cliff = Tile(bedrock, px / 28, pz / 18);
                color = Mix(color, new Rgb(cliff.R * .56f, cliff.G * .64f, cliff.B * .76f), HamletLandscape.FoothillRock(pz));
                float dim = 1 - .35f * HamletLandscape.Wilderness(px, pz);
                color = new Rgb(color.R * dim, color.G * dim, color.B * dim, color.A * dim);
                Write(pixels, (y * size + x) * 4, Mix(color, new Rgb(.13f, .17f, .23f), HamletLandscape.Haze(pz)));
            }
        }
        output.Valley = pixels;
    }

    private static Rgb Tile(byte[] tile, float u, float v)
    {
        int x = (int)(PingPong(u) * (TileSize - 1)), y = (int)(PingPong(v) * (TileSize - 1));
        int i = (y * TileSize + x) * 4;
        return new Rgb(tile[i] / 255f, tile[i + 1] / 255f, tile[i + 2] / 255f, tile[i + 3] / 255f);
    }
    private static float PingPong(float value)
    {
        float t = Math.Max(0, Math.Min(2, value - (float)Math.Floor(value / 2) * 2));
        return 1 - Math.Abs(t - 1);
    }
    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0, Math.Min(1, t));
    private static Rgb Mix(Rgb a, Rgb b, float t) => new(Lerp(a.R, b.R, t), Lerp(a.G, b.G, t), Lerp(a.B, b.B, t), Lerp(a.A, b.A, t));
    private static float Field(float[] field, int size, float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        return Lerp(Lerp(field[iy * size + ix], field[iy * size + ix + 1], x - ix),
            Lerp(field[(iy + 1) * size + ix], field[(iy + 1) * size + ix + 1], x - ix), y - iy);
    }
    private static void Write(byte[] pixels, int i, Rgb color)
    {
        pixels[i] = Byte(color.R); pixels[i + 1] = Byte(color.G); pixels[i + 2] = Byte(color.B); pixels[i + 3] = Byte(color.A);
    }
    private static byte Byte(float value) => (byte)Math.Round(Math.Max(0, Math.Min(1, value)) * 255f);
}
