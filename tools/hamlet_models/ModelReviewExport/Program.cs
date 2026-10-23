using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DarkestDungeon3.Core.Presentation;
using Newtonsoft.Json.Linq;

string repo=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../.."));
string Private(string path)
{
    path=Path.GetFullPath(path);
    if (path.Equals(repo,StringComparison.OrdinalIgnoreCase) || path.StartsWith(repo+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("Game-derived review data belongs outside Git.");
    return path;
}
string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
var options=new JsonSerializerOptions { IncludeFields=true };
if (args.Length==2 && args[0]=="--grade")
{
    string root=Private(args[1]);var doc=JObject.Parse(File.ReadAllText(Path.Combine(root,"review.json")));
    var sizes=JObject.Parse(File.ReadAllText(Path.Combine(root,"image-sizes.json")));
    var sourceSizes=JObject.Parse(File.ReadAllText(Path.Combine(root,"source-sizes.json")));
    foreach (var entry in ((JObject)doc["Images"]!).Properties())
    {
        if(entry.Name.Length!=64 || entry.Name.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))throw new FormatException("Invalid private image key");
        var p=entry.Value["Paint"]!;var tint=p["Tint"]!;
        var paint=new HamletPalette.Paint((float)tint["X"]!,(float)tint["Y"]!,(float)tint["Z"]!,
            (float)p["Brightness"]!,(float)p["Saturation"]!);
        var bytes=File.ReadAllBytes(Path.Combine(root,entry.Name+"-input.rgba"));
        var extent=HamletPalette.PaintExtent((int)sourceSizes[entry.Name]![0]!, (int)sourceSizes[entry.Name]![1]!,2048,paint,false);
        if((int)sizes[entry.Name]![0]!=extent.Width || (int)sizes[entry.Name]![1]!=extent.Height)throw new FormatException("Native paint extent mismatch");
        if (bytes.Length!=(int)sizes[entry.Name]![0]!*(int)sizes[entry.Name]![1]!*4)throw new FormatException("Incomplete RGBA input");
        HamletPalette.GradeRgba(bytes,paint);
        File.WriteAllBytes(Path.Combine(root,entry.Name+"-graded.rgba"),bytes);
    }
    Console.WriteLine("Graded native model paint through Core HamletPalette.GradeRgba");return;
}
if (args.Length!=2)throw new ArgumentException("Pass private model-package and new output directory, or --grade <review directory>.");
string folder=Private(args[0]), output=Private(args[1]);
if (Directory.Exists(output))throw new ArgumentException("Preserve earlier review output; choose a new directory.");
var manifest=JObject.Parse(File.ReadAllText(Path.Combine(folder,"manifest.json")));
var sources=((JObject)manifest["files"]!).Properties().ToDictionary(p=>p.Name,p=>(string)p.Value!);
foreach (var entry in sources)
    if (Path.GetFileName(entry.Key)!=entry.Key || Hash(Path.Combine(folder,entry.Key))!=entry.Value)throw new FormatException("Changed private asset "+entry.Key);
var images=new Dictionary<string,object>();var models=new Dictionary<string,object>();
foreach (var site in HamletLayout.Sites)
{
    string path=Path.Combine(folder,site.Id+".gltf");var gltf=JObject.Parse(File.ReadAllText(path));var model=GltfModel.Load(path);
    var world=new Dictionary<int,Matrix4x4>();
    Matrix4x4 World(int i)
    {
        if(i<0)return Matrix4x4.Identity;if(world.TryGetValue(i,out var m))return m;
        var n=model.Nodes[i];var s=n.Scale;var r=n.Rotation;var t=n.Translation;
        return world[i]=Matrix4x4.CreateScale(s[0],s[1],s[2])*Matrix4x4.CreateFromQuaternion(new(r[0],r[1],r[2],r[3]))
            *Matrix4x4.CreateTranslation(t[0],t[1],t[2])*World(n.Parent);
    }
    Vector3 At(float[] values,int i)=>new(values[i*3],values[i*3+1],values[i*3+2]);
    var local=model.Primitives.Select(p=>p.Positions.Chunk(3).Select(v=>Vector3.Transform(new(v[0],v[1],v[2]),World(p.Node))).ToArray()).ToArray();
    var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);
    foreach(var v in local.SelectMany(v=>v)){lo=Vector3.Min(lo,v);hi=Vector3.Max(hi,v);}
    var shift=new Vector3((lo.X+hi.X)/2,lo.Y,(lo.Z+hi.Z)/2);float scale=site.Width/Math.Max(hi.X-lo.X,hi.Z-lo.Z);
    var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,(180+site.Yaw)*MathF.PI/180);
    var materials=new List<object>();
    foreach(var info in model.Materials)
    {
        var b=info.BaseColorFactor;var e=info.EmissiveFactor;
        bool painted=b[0]+b[1]+b[2]<.001f && info.Emissive!=null;
        string texture=painted?info.Emissive:info.BaseColor??info.Emissive;string image=null;
        var paint=HamletPalette.BuildingSurface(info.Name);
        if(texture!=null)
        {
            string signature=Hash(texture)+JsonSerializer.Serialize(paint,options);
            image=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))).ToLowerInvariant();
            images.TryAdd(image,new { Source=texture,SourceHash=Hash(texture),Paint=paint });
        }
        var color=HamletPalette.BuildingTint(new(b[0],b[1],b[2]),new(e[0],e[1],e[2]),painted);
        bool light=!b.Take(3).All(v=>v<.001f) && e.Max()>b.Take(3).Max()*1.1f;
        var tints=Enumerable.Range(0,3).Select(i=>
        {
            var facet=HamletSetDressing.FacetTint(i==0?1:0,i==2?1:0,0);
            return new[] { color.X*(light?1:facet.X),color.Y*(light?1:facet.Y),color.Z*(light?1:facet.Z),b[3] };
        }).ToArray();
        materials.Add(new {info.Name,Image=image,Tints=tints,info.Mask,info.Cutoff,info.DoubleSided,info.Blend,Light=light});
    }
    var primitives=new List<object>();
    for(int pi=0;pi<model.Primitives.Count;pi++)
    {
        var p=model.Primitives[pi];var matrix=World(p.Node);Matrix4x4.Invert(matrix,out var inverse);var normalMatrix=Matrix4x4.Transpose(inverse);
        var positions=local[pi].Select(v=>Vector3.Transform((v-shift)*scale,rotation)+new Vector3(site.X,site.Y,site.Z)).ToArray();
        var normals=Enumerable.Range(0,p.VertexCount).Select(i=>Vector3.Normalize(Vector3.TransformNormal(p.Normals==null?Vector3.UnitY:At(p.Normals,i),normalMatrix))).ToArray();
        var indices=(int[])p.Triangles.Clone();var bands=new int[indices.Length/3];
        var info=model.Materials[p.Material];bool light=!info.BaseColorFactor.Take(3).All(v=>v<.001f) && info.EmissiveFactor.Max()>info.BaseColorFactor.Take(3).Max()*1.1f;
        for(int i=0;i<indices.Length;i+=3)
        {
            if(matrix.GetDeterminant()<0)(indices[i+1],indices[i+2])=(indices[i+2],indices[i+1]);
            var n=Vector3.Transform(Vector3.Normalize(normals[indices[i]]+normals[indices[i+1]]+normals[indices[i+2]]),rotation);
            bands[i/3]=HamletPalette.BuildingLightBand(Vector3.Dot(n,new(-.35f,.75f,-.55f)),p.ContactShade?[i/3]??1,light);
        }
        var worldNormals=normals.Select(n=>Vector3.Transform(n,rotation));
        primitives.Add(new {Name=model.Nodes[p.Node].Name,Positions=positions.SelectMany(v=>new[]{v.X,v.Y,v.Z}).ToArray(),
            Normals=worldNormals.SelectMany(v=>new[]{v.X,v.Y,v.Z}).ToArray(),
            Uv=p.Uv??new float[p.VertexCount*2],Indices=indices,Bands=bands,p.Material});
    }
    models.Add(site.Id,new { Materials=materials,Primitives=primitives,SourceGlbHash=(string)gltf["asset"]?["extras"]?["source_glb_sha256"],model.TriangleCount,
        LocalBounds=new[]{new[]{lo.X,lo.Y,lo.Z},new[]{hi.X,hi.Y,hi.Z}},Scale=scale });
}
Directory.CreateDirectory(output);
var core=Directory.EnumerateFiles(Path.Combine(repo,"src/DarkestDungeon3.Core/Presentation"),"Hamlet*.cs").OrderBy(p=>p)
    .ToDictionary(p=>Path.GetRelativePath(repo,p).Replace('\\','/'),p=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(p).Replace("\r\n","\n")))).ToLowerInvariant());
string readerHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(repo,"src/DarkestDungeon3.Core/Presentation/GltfModel.cs")).Replace("\r\n","\n")))).ToLowerInvariant();
File.WriteAllText(Path.Combine(output,"review.json"),JsonSerializer.Serialize(new {Version=1,Notice="Core palette/tints/geometry/contact; native shader appearance still needs comparison.",CoreSources=core,ReaderHash=readerHash,SourceFiles=sources,Images=images,Models=models},options));
Console.WriteLine($"Exported {models.Count} service models and {images.Count} native grade inputs to {output}");
