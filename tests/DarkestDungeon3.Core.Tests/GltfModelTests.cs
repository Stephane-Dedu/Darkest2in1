using System;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Presentation;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

/// <summary>The owner's private Dark Souls III export (Soul of Cinder); without it the model cases have nothing to read.</summary>
public class GltfModelTests
{
    private const string Export = @"C:\Users\Piral\ds3-extract\soul_of_cinder\soul_of_cinder.gltf";
    private static readonly GltfModel Cinder = File.Exists(Export) ? GltfModel.Load(Export) : null;

    [Fact]
    public void ReadsTheWholeSkinnedModel()
    {
        if (Cinder == null) return;
        Assert.Equal(15, Cinder.Primitives.Count);
        Assert.Equal(54468, Cinder.TriangleCount);             // the export's README: 54,468 triangles at LOD0
        Assert.Equal(123, Cinder.Joints.Length);
        Assert.Equal(123, Cinder.InverseBind.Length);
        Assert.Contains(Cinder.Nodes, n => n.Name == "R_Hand");
        foreach (var p in Cinder.Primitives)
        {
            Assert.Equal(p.VertexCount * 4, p.Joints.Length);
            Assert.All(p.Joints, j => Assert.InRange(j, 0, Cinder.Joints.Length - 1));
            Assert.All(p.Triangles, t => Assert.InRange(t, 0, p.VertexCount - 1));
            for (int v = 0; v < p.VertexCount; v++)
                Assert.InRange(p.Weights.Skip(v * 4).Take(4).Sum(), 0.99f, 1.01f);
        }
    }

    [Fact]
    public void StandsAboutThreeMetresTallWithItsTextures()
    {
        if (Cinder == null) return;
        var (height, _) = Cinder.Extent(p => !p.Mesh.Contains("#0") || p.Mesh.Contains("#01#"));
        Assert.InRange(height, 2.8f, 3.4f);                      // the README: about 3.1 m
        Assert.All(Cinder.Materials, m => Assert.True(m.BaseColor == null || File.Exists(m.BaseColor), m.Name));
        Assert.Contains(Cinder.Materials, m => m.Mask);          // DS3's cut-out cloth
    }

    private static GltfModel.Primitive Quad() => new()
    {
        Mesh = "quad",
        // Two triangles: the left half of UV space (u < 0.5) and the right half.
        Positions = new float[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 2, 0, 0, 2, 1, 0 },
        Normals = new float[] { 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1 },
        Uv = new float[] { 0, 0, 0.4f, 0, 0.4f, 1, 0, 1, 0.6f, 0, 1, 1 },
        Joints = new int[24], Weights = Enumerable.Repeat(0.25f, 24).ToArray(),
        Triangles = new[] { 0, 1, 2, 4, 5, 1 },
    };

    [Fact]
    public void CutOutTrianglesAreDropped()
    {
        // Transparent on the left half of the texture, opaque on the right.
        var cut = Quad().CutOut((u, v) => u < 0.5f ? 0f : 1f, 0.5f);
        Assert.Equal(new[] { 4, 5, 1 }, cut.Triangles);
        Assert.Equal(Quad().VertexCount, cut.VertexCount);
        // A triangle crossing into the opaque part (its edge middle or corner reaches it) is kept.
        var keep = Quad().CutOut((u, v) => u > 0.35f ? 1f : 0f, 0.5f);
        Assert.Equal(6, keep.Triangles.Length);
    }

    [Fact]
    public void BackFacesAreTheTrianglesTurnedAround()
    {
        var both = Quad().WithBackFaces();
        Assert.Equal(12, both.VertexCount);
        Assert.Equal(new[] { 0, 1, 2, 4, 5, 1, 6, 8, 7, 10, 7, 11 }, both.Triangles);
        Assert.Equal(-1f, both.Normals[6 * 3 + 2]);       // the copy faces the other way
        Assert.Equal(both.Uv.Length / 2, both.VertexCount);
        Assert.Equal(both.Joints.Length / 4, both.VertexCount);
    }

    [Fact]
    public void MirroringKeepsAMatrixInTheSameSpace()
    {
        // A translation of 2 along X becomes -2; a rotation about Y flips its sign, about X keeps it.
        float[] translate = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 2, 3, 4, 1 };
        var mirrored = GltfModel.Mirror(translate);
        Assert.Equal(new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, -2, 3, 4, 1 }, mirrored);
        float c = (float)Math.Cos(0.5), s = (float)Math.Sin(0.5);
        float[] aboutY = { c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1 };
        Assert.Equal(new float[] { c, 0, s, 0, 0, 1, 0, 0, -s, 0, c, 0, 0, 0, 0, 1 }, GltfModel.Mirror(aboutY));
        Assert.Equal(translate, GltfModel.Mirror(GltfModel.Mirror(translate)));
    }
}
