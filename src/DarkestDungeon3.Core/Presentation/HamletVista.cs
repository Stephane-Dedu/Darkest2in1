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
    public const string MountainAsset="mainmenu_mountain_mdl";
    public const string MountainPath="Assets/Art/Models/Environments/Common/Models/MainMenu/mainmenu_mountain_mdl.fbx";
    public static readonly Piece[] Pieces =
    {
        // The native Mountain rises behind the whole settlement. Its buried foot
        // lets the existing distant hills occlude the model's broad basal skirt.
        new(MountainAsset,MountainPath,42,-45,190,155,depthScale:.25f,distant:true),
    };
    public const float SkyDepth=230,SkyWidth=600,SkyBottom=-120,SkyTop=220;
}
