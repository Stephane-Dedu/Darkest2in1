using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DarkestDungeon3.Core.Dd1;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class CinematicStreamTests
{
    // Hashes recorded from the deployed pre-streaming extractor, without copying game data into Git.
    [Theory]
    [InlineData("house_of_ruin", 1233823, "FB77CAE6070988C999F9D34FEF2E7E467334C516F30573CE73BB41B42A3ADFF5")]
    [InlineData("old_road", 661094, "290D3E4CDB19634D2C250ABDB7DB745D124F74378EFA4C01CBBAD78201F4B4E8")]
    public void NativeAudioIsByteIdenticalToThePreviousExtractor(string name, int length, string hash)
    {
        using var file = File.OpenRead(Dd1Cinematic.VideoPath(Dd1Install.Find(), name));
        using var input = new ChunkedInput(file, 4096);
        using var output = new MemoryStream();
        Assert.Equal(length, Dd1Cinematic.VorbisAudio(input, output));
        Assert.Equal(length, output.Length);
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(output.ToArray())));
        Assert.InRange(input.MaxRequested, 1, 65025);
        Assert.Equal(65307, input.BufferSize);
        Assert.True(file.CanRead);
        Assert.True(output.CanWrite);
    }

    [Fact]
    public void ShortNonSeekingReadsKeepOnlyTheFirstVorbisSerialAndPreserveWholePages()
    {
        byte[] video = Page(4, 2, new byte[] { 128, 116, 104, 101, 111, 114, 97 });
        byte[] header = Page(unchecked((int)0xf0123456), 2, new byte[] { 1, 118, 111, 114, 98, 105, 115 });
        byte[] anotherAudio = Page(9, 2, new byte[] { 1, 118, 111, 114, 98, 105, 115 });
        byte[] audio = Page(unchecked((int)0xf0123456), 4, new byte[] { 8, 7, 6, 5 });
        byte[] expected = header.Concat(audio).ToArray();
        using var source = new MemoryStream(video.Concat(header).Concat(anotherAudio).Concat(audio).ToArray());
        using var input = new ChunkedInput(source, 3);
        using var output = new MemoryStream();
        Assert.Equal(expected.Length, Dd1Cinematic.VorbisAudio(input, output));
        Assert.Equal(expected, output.ToArray());
        Assert.Equal(expected, Dd1Cinematic.VorbisAudio(source.ToArray()));
        Assert.True(source.CanRead);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(26)]
    [InlineData(27)]
    [InlineData(30)]
    public void TruncatedTailNeverWritesAnIncompletePage(int tailLength)
    {
        byte[] first = Page(1, 2, new byte[] { 1, 118, 111, 114, 98, 105, 115 });
        byte[] tail = Page(1, 4, new byte[] { 1, 2, 3, 4, 5 });
        using var source = new MemoryStream(first.Concat(tail.Take(tailLength)).ToArray());
        using var input = new ChunkedInput(source, 1);
        using var output = new MemoryStream();
        Assert.Equal(first.Length, Dd1Cinematic.VorbisAudio(input, output));
        Assert.Equal(first, output.ToArray());
    }

    [Fact]
    public void MaximumSizePageFitsTheFixedBufferAndUnknownStreamsYieldNoAudio()
    {
        var body = Enumerable.Repeat((byte)33, 65025).ToArray();
        new byte[] { 1, 118, 111, 114, 98, 105, 115 }.CopyTo(body, 0);
        byte[] page = Page(3, 2, body);
        Assert.Equal(65307, page.Length);
        using var source = new MemoryStream(page);
        using var input = new ChunkedInput(source, 23);
        using var output = new MemoryStream();
        Assert.Equal(page.Length, Dd1Cinematic.VorbisAudio(input, output));
        Assert.Equal(page, output.ToArray());
        Assert.Equal(65307, input.BufferSize);
        Assert.InRange(input.MaxRequested, 1, 65025);
        Assert.Empty(Dd1Cinematic.VorbisAudio(Array.Empty<byte>()));
        Assert.Empty(Dd1Cinematic.VorbisAudio(Page(3, 2, new byte[] { 128, 116, 104, 101, 111, 114, 97 })));
    }

    private static byte[] Page(int serial, byte flags, byte[] body)
    {
        int segments = (body.Length + 254) / 255;
        var page = new byte[27 + segments + body.Length];
        new byte[] { 79, 103, 103, 83 }.CopyTo(page, 0);
        page[5] = flags;
        for (int i = 0; i < 4; i++) page[14 + i] = (byte)(serial >> (8 * i));
        page[18] = 17; // sequence/checksum bytes must pass through unchanged
        page[22] = 23;
        page[26] = (byte)segments;
        for (int i = 0; i < segments; i++) page[27 + i] = (byte)Math.Min(255, body.Length - i * 255);
        body.CopyTo(page, 27 + segments);
        return page;
    }

    private sealed class ChunkedInput(Stream input, int limit) : Stream
    {
        public int MaxRequested, BufferSize;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            MaxRequested = Math.Max(MaxRequested, count);
            BufferSize = Math.Max(BufferSize, buffer.Length);
            return input.Read(buffer, offset, Math.Min(limit, count));
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long length) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
