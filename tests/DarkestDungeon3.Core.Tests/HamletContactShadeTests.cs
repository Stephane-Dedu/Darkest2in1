using DarkestDungeon3.Core.Presentation;

namespace DarkestDungeon3.Core.Tests;

public class HamletContactShadeTests
{
    [Fact]
    public void ContactShadeFollowsCutoutAndBackFaceTriangles()
    {
        var p = new GltfModel.Primitive
        {
            Positions = new float[18], Normals = new float[18],
            Uv = new float[] {0,0,.2f,0,.2f,1,.8f,0,1,0,1,1},
            Triangles = new[] {0,1,2,3,4,5}, ContactShade = new[] {.25f,.875f}
        };
        Assert.Equal(new[] {.25f,.875f,.25f,.875f},p.WithBackFaces().ContactShade);
        var cut = p.CutOut((u,v) => u > .5f ? 1 : 0,.5f);
        Assert.Equal(new[] {3,4,5},cut.Triangles);
        Assert.Equal(new[] {.875f},cut.ContactShade);
    }

    [Theory]
    [InlineData(-.8f)]
    [InlineData(0f)]
    [InlineData(.8f)]
    public void ContactNeverBrightensSurfacesOrDimsAnAuthoredLight(float key)
    {
        int open = HamletPalette.BuildingLightBand(key,1,false);
        Assert.Equal(key < -.15f ? 0 : key > .35f ? 2 : 1,open);
        foreach (float visibility in new[] {0f,.25f,.5f,.875f,1f})
        {
            Assert.InRange(HamletPalette.BuildingLightBand(key,visibility,false),0,open);
            Assert.Equal(1,HamletPalette.BuildingLightBand(key,visibility,true));
        }
        Assert.Equal(0,HamletPalette.BuildingLightBand(key,.25f,false));
    }

    [Theory]
    [InlineData(false,2,.5f)]
    [InlineData(true,2,.5f)]
    [InlineData(true,1,.5f)]
    [InlineData(true,2,-.1f)]
    public void LoaderValidatesOptionalContactData(bool include,int count,float first)
    {
        string folder=Path.Combine(Path.GetTempPath(),"dd3-contact-"+Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            using (var writer=new BinaryWriter(File.Create(Path.Combine(folder,"model.bin"))))
            {
                for (int i=0;i<18;i++) writer.Write((float)i/10);
                for (int i=0;i<6;i++) writer.Write(i);
                writer.Write(first); writer.Write(.875f);
            }
            string extra=include ? ",\"extras\":{\"hamlet_contact_shade\":2}" : "";
            string json="""
                {"asset":{"version":"2.0"},"buffers":[{"uri":"model.bin","byteLength":104}],
                 "bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":72},
                  {"buffer":0,"byteOffset":72,"byteLength":24},{"buffer":0,"byteOffset":96,"byteLength":8}],
                 "accessors":[{"bufferView":0,"componentType":5126,"count":6,"type":"VEC3"},
                  {"bufferView":1,"componentType":5125,"count":6,"type":"SCALAR"},
                  {"bufferView":2,"componentType":5126,"count":COUNT,"type":"SCALAR"}],
                 "nodes":[{"mesh":0}],"meshes":[{"primitives":[{"attributes":{"POSITION":0},"indices":1EXTRA}]}]}
                """.Replace("COUNT",count.ToString()).Replace("EXTRA",extra);
            string path=Path.Combine(folder,"model.gltf"); File.WriteAllText(path,json);
            if (include && (count != 2 || first < 0)) Assert.Throws<FormatException>(() => GltfModel.Load(path));
            else
            {
                var primitive=Assert.Single(GltfModel.Load(path).Primitives);
                if (include) Assert.Equal(new[] {first,.875f},primitive.ContactShade);
                else Assert.Null(primitive.ContactShade);
            }
        }
        finally { Directory.Delete(folder,true); }
    }
}
