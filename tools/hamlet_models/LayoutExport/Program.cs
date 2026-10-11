using System.Text.Json;
using DarkestDungeon3.Core.Presentation;

if (args.Length!=1) throw new ArgumentException("Pass one private output JSON path.");
string path=Path.GetFullPath(args[0]);
string repo=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../.."));
if (path.StartsWith(repo+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Preview geometry belongs outside Git.");
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
var terrain=HamletTerrain.Build();
File.WriteAllText(path,JsonSerializer.Serialize(new
{
    Notice="Private offline composition geometry; native lighting and UI acceptance still need DD2.",
    HamletLayout.Sites,HamletLayout.Routes,HamletLayout.CameraTarget,HamletLayout.CameraOffset,HamletLayout.FieldOfView,
    Terrain=terrain,
    Palette=new { HamletPalette.Soil,HamletPalette.Rock,HamletPalette.Rubble,HamletPalette.Paving,HamletPalette.Masonry,HamletPalette.Mountain },
    Scenery=new[]
    {
        Scenery("valley_tile_01",-28,-1.8f,51,16),Scenery("valley_tile_02",36,-1.8f,54,18),
        Scenery("valley_tile_01",-58,-1.8f,30,18,30),Scenery("valley_tile_02",49,-1.8f,29,18,-20),
        Scenery("camp_bush_01",-50,0,-3,6,15),Scenery("camp_bush_01",44,0,-7,6,-25),
        Scenery("camp_crate",-50,0,23,2),Scenery("camp_barrel",-51,0,21,1.5f),Scenery("camp_firepit",-51,0,17,2.5f),
        Scenery("common_prop_lamppost",-6,0,4,1.5f),Scenery("common_prop_lamppost",22,0,10,1.5f),
        Scenery("mainmenu_mountain_mdl",0,-36,96,174,heightScale:.85f,depthScale:.34f),Scenery("camp_sky",0,-95,152,460),
        Scenery("barricade_military_decor_rocks_01",-44,-.25f,5,7,25),
        Scenery("barricade_military_decor_rocks_02",-25,-.35f,16,4,-20),
        Scenery("barricade_military_decor_rocks_03",34,-.3f,12,6,15),
        Scenery("barricade_military_decor_rocks_02",40,-.25f,25,8,-40)
    }
},new JsonSerializerOptions { IncludeFields=true }));
Console.WriteLine($"{path}: {terrain.Positions.Length/3:N0} terrain vertices, {terrain.Rock.Length/3:N0} rock faces");

static object Scenery(string id,float x,float y,float z,float width,float yaw=0,float heightScale=1,float depthScale=1)=>new
{ Id=id,X=x,Y=y+HamletLayout.Height(x,z),Z=z,Width=width,Yaw=yaw,HeightScale=heightScale,DepthScale=depthScale };
