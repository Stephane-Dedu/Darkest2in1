using System.Text.Json;
using DarkestDungeon3.Core.Presentation;

if (args.Length<1 || args.Length>2 || (args.Length==2 && args[1]!="--full-cliff-tile"))
    throw new ArgumentException("Pass a private output JSON path and optional --full-cliff-tile.");
bool fullCliffTile=args.Length==2;
string path=Path.GetFullPath(args[0]);
string repo=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../.."));
if (path.StartsWith(repo+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Preview geometry belongs outside Git.");
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
var terrain=HamletTerrain.Build(fullCliffTile:fullCliffTile);
File.WriteAllText(path,JsonSerializer.Serialize(new
{
    Notice="Private offline composition geometry; native lighting and UI acceptance still need DD2.",
    HamletLayout.Sites,HamletLayout.Routes,HamletLayout.CameraTarget,HamletLayout.CameraOffset,HamletLayout.FieldOfView,
    EstateSky=new { HamletVista.SkyDepth,HamletVista.SkyWidth,HamletVista.SkyBottom,HamletVista.SkyTop },
    CliffPaint=HamletTownscape.CliffPaint,
    Terrain=terrain,Vegetation=HamletVegetation.Build(),Clumps=HamletVegetation.Clumps,
    Outcrops=HamletSetDressing.BuildRocks(fullCliffTile),Mists=HamletSetDressing.Mists,
    EntranceLight=Enumerable.Range(0,256*256).Select(i=>HamletSetDressing.EntranceLight(
        HamletLayout.MinX+(HamletLayout.MaxX-HamletLayout.MinX)*(i%256)/255,
        HamletLayout.MinZ+(HamletLayout.MaxZ-HamletLayout.MinZ)*(i/256)/255)),
    Valley=new
    {
        HamletLandscape.MinX,HamletLandscape.MaxX,HamletLandscape.MinZ,HamletLandscape.MaxZ,
        Samples=Enumerable.Range(0,256*256).Select(i=>
        {
            float x=HamletLandscape.MinX+(HamletLandscape.MaxX-HamletLandscape.MinX)*(i%256+.5f)/256;
            float z=HamletLandscape.MinZ+(HamletLandscape.MaxZ-HamletLandscape.MinZ)*(i/256+.5f)/256;
            return new { Stain=HamletGroundPaint.At(x,z),Road=HamletLandscape.RoadCover(x,z),Haze=HamletLandscape.Haze(z),Grass=HamletLandscape.GrassCover(x,z),Wilderness=HamletLandscape.Wilderness(x,z),Rock=HamletLandscape.FoothillRock(z) };
        })
    },
    Palette=new { HamletPalette.Soil,HamletPalette.Rock,HamletPalette.Rubble,HamletPalette.Paving,HamletPalette.Masonry,HamletPalette.Mountain,HamletPalette.Woodland,HamletPalette.DistantWoodland,HamletPalette.Wildwood,HamletPalette.Building,HamletPalette.Grass,HamletPalette.GrassTuft },
    LaneCover=Enumerable.Range(0,256*256).Select(i=>HamletGroundPaint.LaneCover(
        HamletLayout.MinX+(HamletLayout.MaxX-HamletLayout.MinX)*(i%256)/255,
        HamletLayout.MinZ+(HamletLayout.MaxZ-HamletLayout.MinZ)*(i/256)/255)),
    GroundStains=Enumerable.Range(0,128*128).Select(i=>HamletGroundPaint.At(
        HamletLayout.MinX+(HamletLayout.MaxX-HamletLayout.MinX)*(i%128+.5f)/128,
        HamletLayout.MinZ+(HamletLayout.MaxZ-HamletLayout.MinZ)*(i/128+.5f)/128)),
    GrassCover=Enumerable.Range(0,256*256).Select(i=>HamletVegetation.Cover(
        HamletLayout.MinX+(HamletLayout.MaxX-HamletLayout.MinX)*(i%256)/255,
        HamletLayout.MinZ+(HamletLayout.MaxZ-HamletLayout.MinZ)*(i/256)/255)),
    ContactShade=Enumerable.Range(0,256*256).Select(i=>HamletGroundPaint.ContactShade(
        HamletLayout.MinX+(HamletLayout.MaxX-HamletLayout.MinX)*(i%256)/255,
        HamletLayout.MinZ+(HamletLayout.MaxZ-HamletLayout.MinZ)*(i/256)/255)),
    EdgeWear=Enumerable.Range(0,256*256).Select(i=>HamletGroundPaint.EdgeWear(
        HamletLayout.MinX+(HamletLayout.MaxX-HamletLayout.MinX)*(i%256)/255,
        HamletLayout.MinZ+(HamletLayout.MaxZ-HamletLayout.MinZ)*(i/256)/255)),
    Scenery=new[]
    {
        Scenery("valley_tile_01",-28,-1.8f,51,16),Scenery("valley_tile_02",36,-1.8f,54,18),
        Scenery("valley_tile_01",-58,-1.8f,30,18,30),Scenery("valley_tile_02",49,-1.8f,29,18,-20),
        Scenery("camp_bush_01",-50,0,-3,6,15),Scenery("camp_bush_01",44,0,-7,6,-25),
        Scenery("camp_crate",-50,0,23,2),Scenery("camp_barrel",-51,0,21,1.5f),Scenery("camp_firepit",-51,0,17,2.5f),
        Scenery("common_prop_lamppost",-6,0,4,1.5f),Scenery("common_prop_lamppost",22,0,10,1.5f),
        Scenery("barricade_military_decor_rocks_01",-44,-.25f,5,7,25),
        Scenery("barricade_military_decor_rocks_02",-25,-.35f,16,4,-20),
        Scenery("barricade_military_decor_rocks_03",34,-.3f,12,6,15),
        Scenery("barricade_military_decor_rocks_02",40,-.25f,25,8,-40),
        Scenery("valley_tile_09",-46,-.25f,-10,9,20),Scenery("valley_tile_09",-16,-.2f,-14,7,-10),
        Scenery("valley_tile_09",43,-.25f,10,7,65),Scenery("valley_tile_09",-1,-.2f,41,6,15),
        Scenery("coast_skirt_cliffs_mdl_01",-59,-2,-7,15,15),Scenery("coast_skirt_cliffs_mdl_01",49,-1,25,18,-25)
    }.Concat(HamletVista.Pieces.Select(p=>(object)new
    { Id=p.Asset,p.X,p.Y,p.Z,p.Width,p.Yaw,p.HeightScale,p.DepthScale,Background=p.Distant }))
    .Concat(HamletTownscape.Lots.Select(l=>(object)new
    { Id=l.Asset,l.Pad.X,l.Pad.Y,l.Pad.Z,l.Pad.Width,l.Pad.Yaw,HeightScale=1,DepthScale=1,Architecture=true }))
    .Concat(HamletLandscape.Props.Select(p=>(object)new
    { p.Id,p.X,p.Y,p.Z,p.Width,p.Yaw,p.HeightScale,DepthScale=1,Background=true }))
    .Concat(HamletSetDressing.Props.Select(p=>(object)new { p.Id,p.X,p.Y,p.Z,p.Width,p.Yaw,p.HeightScale,p.DepthScale }))
},new JsonSerializerOptions { IncludeFields=true }));
Console.WriteLine($"{path}: {terrain.Positions.Length/3:N0} terrain vertices, {terrain.Rock.Length/3:N0} rock faces");

static object Scenery(string id,float x,float y,float z,float width,float yaw=0,float heightScale=1,float depthScale=1,bool absolute=false)=>new
{ Id=id,X=x,Y=y+(absolute?0:HamletLayout.Height(x,z)),Z=z,Width=width,Yaw=yaw,HeightScale=heightScale,DepthScale=depthScale };
