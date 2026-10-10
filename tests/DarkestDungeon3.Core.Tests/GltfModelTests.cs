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
