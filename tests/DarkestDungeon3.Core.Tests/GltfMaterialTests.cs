using System;
using System.IO;
using DarkestDungeon3.Core.Presentation;
using Xunit;

namespace DarkestDungeon3.Core.Tests;

public class GltfMaterialTests
{
    [Fact]
    public void PreservesPaintedEmissionAndAlphaInsteadOfDefaultingToWhite()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dd3-material-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            string file = Path.Combine(folder, "model.gltf");
            File.WriteAllText(file, """
                {"asset":{"version":"2.0"},"images":[{"uri":"paint.png"}],"textures":[{"source":0}],
                 "materials":[{},
                  {"name":"paint","pbrMetallicRoughness":{"baseColorFactor":[0,0,0,0.8]},
                   "emissiveTexture":{"index":0},"emissiveFactor":[0.5,0.25,0.1],
                   "extensions":{"KHR_materials_emissive_strength":{"emissiveStrength":2}},
                   "alphaMode":"MASK","alphaCutoff":0.22,"doubleSided":true},
                  {"alphaMode":"BLEND","pbrMetallicRoughness":{"baseColorFactor":[0.4,0.3,0.2,1]}}]}
                """);
            var model = GltfModel.Load(file);
            Assert.Equal(new float[] { 1, 1, 1, 1 }, model.Materials[0].BaseColorFactor);
            Assert.Equal(new float[] { 0, 0, 0 }, model.Materials[0].EmissiveFactor);
            var paint = model.Materials[1];
            Assert.Equal(new float[] { 0, 0, 0, .8f }, paint.BaseColorFactor);
            Assert.Equal(new float[] { 1, .5f, .2f }, paint.EmissiveFactor);
            Assert.Equal(Path.Combine(folder, "paint.png"), paint.Emissive);
            Assert.True(paint.Mask && paint.DoubleSided);
            Assert.Equal(.22f, paint.Cutoff);
            Assert.True(model.Materials[2].Blend);
            Assert.False(model.Materials[2].Mask);
        }
        finally { Directory.Delete(folder, true); }
    }
}
