namespace DarkestDungeon3.Core.Presentation;

/// <summary>Native architectural scenery behind the ten services. It never participates in world picking.</summary>
public static class HamletVista
{
    public sealed class Piece
    {
        public readonly string Asset, Path;
        public readonly float X,Y,Z,Width,Yaw,HeightScale,DepthScale;
        public readonly bool Distant;
        public Piece(string asset,string path,float x,float y,float z,float width,float yaw=0,
            float heightScale=1,float depthScale=1,bool distant=false)
        { Asset=asset;Path=path;X=x;Y=y;Z=z;Width=width;Yaw=yaw;HeightScale=heightScale;DepthScale=depthScale;Distant=distant; }
    }
    private const string City="Assets/Art/Models/Environments/City/Models/DecorTiles/";
    private const string Valley="Assets/Art/Models/Environments/Valley/Models/Buildings/";
    public static readonly Piece[] Pieces =
    {
        // A small estate silhouette on the rear ridge, clear of the Abbey's roof and the HUD.
        new("city_bigwall_main",City+"city_bigwall_main.fbx",52,7,154,18,depthScale:.55f,distant:true),
        new("city_tower_01",City+"city_tower_01.fbx",43.5f,18,156,5.5f,heightScale:1.1f,distant:true),
        new("city_tower_03",City+"city_tower_03.fbx",61,15.8f,156,5.5f,heightScale:1.1f,distant:true),
        new("city_tower_02",City+"city_tower_02.fbx",52,21,157,4.4f,heightScale:1.7f,distant:true),
        new("valley_cottage_01",Valley+"valley_cottage_01.fbx",-31,HamletLayout.Height(-31,30),30,8,12),
        new("valley_cottage_02",Valley+"valley_cottage_02.fbx",36,HamletLayout.Height(36,36),36,8,-8),
        new("valley_cottage_01",Valley+"valley_cottage_01.fbx",-24,HamletLayout.Height(-24,50),50,9,15),
    };
    public const float SkyDepth=230,SkyWidth=600,SkyBottom=-120,SkyTop=220;
}
