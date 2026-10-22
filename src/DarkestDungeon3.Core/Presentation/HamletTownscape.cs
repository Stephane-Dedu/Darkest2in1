using System.Collections.Generic;

namespace DarkestDungeon3.Core.Presentation;

/// <summary>Residential streets surround the service buildings. Lots have real grounded pads, never service IDs.</summary>
public static class HamletTownscape
{
    // The private painting is a seamless square tile, not the native rock atlas.
    public static readonly HamletPalette.Paint CliffPaint=new(.95f,.99f,1.05f,.001f,.65f);
    public sealed class Lot
    {
        public readonly string Asset, Path;
        public readonly HamletLayout.Site Pad;
        public Lot(string id,string asset,string path,float x,float y,float z,float width,float yaw,float depth,float across=1)
        { Asset=asset;Path=path;Pad=new(id,x,y,z,width,yaw,across,depth,groundBlend:4.5f); }
    }
    private const string Valley="Assets/Art/Models/Environments/Valley/Models/Buildings/";
    private const string Farm="Assets/Art/Models/Environments/Farm/Models/Buildings/";
    private const string City="Assets/Art/Models/Environments/City/Models/DecorTiles/";
    public static readonly IReadOnlyList<Lot> Lots = new[]
    {
        new Lot("coach_yard", "farm_cottage",Farm+"farm_cottage.fbx",-47,-1,-18,10,20,.745f),
        new Lot("lower_lane", "valley_cottage_02",Valley+"valley_cottage_02.fbx",-14,0,-26,9,-10,.69f),
        new Lot("forge_store", "farm_cottage",Farm+"farm_cottage.fbx",37,0,-26,12,-15,.745f),
        new Lot("tavern_lane", "valley_cottage_01",Valley+"valley_cottage_01.fbx",-29,5,23,9,12,.68f),
        new Lot("west_street", "valley_cottage_02",Valley+"valley_cottage_02.fbx",-26,10,39,10,-15,.69f),
        new Lot("upper_square", "valley_academic_house",Valley+"valley_academic_house.fbx",-12,13,48,11,-8,.572f),
        new Lot("west_roofs", "valley_cottage_02",Valley+"valley_cottage_02.fbx",-31,15,57,10,14,.69f),
        new Lot("mill_house", "valley_cottage_01",Valley+"valley_cottage_01.fbx",-40,13,47,9,-20,.68f),
        new Lot("ridge_house", "valley_cottage_01",Valley+"valley_cottage_01.fbx",-19,17,64,12,8,.68f),
        new Lot("abbey_close", "farm_cottage",Farm+"farm_cottage.fbx",-4,18,65,9,-8,.745f),
        new Lot("east_roofs", "valley_cottage_01",Valley+"valley_cottage_01.fbx",28,13,57,11,18,.68f),
        new Lot("guild_yard", "farm_cottage",Farm+"farm_cottage.fbx",43,4,22,9,-15,.745f),
        new Lot("east_street", "valley_academic_house",Valley+"valley_academic_house.fbx",40,8,40,11,14,.572f),
        new Lot("east_gate", "city_building_destroyed_01",City+"city_building_destroyed_01.fbx",50,7,55,9,-12,1,.814f),
        new Lot("windmill", "farm_windmill_01",Farm+"farm_windmill_01.fbx",-46,20,62,9,15,.789f),
    };
    public static readonly IReadOnlyList<HamletLayout.Route> Streets = new[]
    {
        new HamletLayout.Route("residential_west",2.5f,new(-20,1,-3),new(-23,4,16),new(-26,9,32),
            new(-32,13,44),new(-27,15,54),new(-15,17,60),new(0,17,61),new(19,14,57),new(39,8,46),new(46,7,52)),
        new HamletLayout.Route("residential_east",2.5f,new(16,6,14),new(38,4,15),new(42,7,31),new(39,8,46)),
        new HamletLayout.Route("mill_lane",2.2f,new(-32,13,44),new(-43,17,39),new(-52,17,38),new(-54,19,52),new(-46,20,57)),
    };
}
