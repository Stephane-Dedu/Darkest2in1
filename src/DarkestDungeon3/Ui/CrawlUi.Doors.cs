using System.Collections.Generic;
using System.IO;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal sealed partial class CrawlUi
{
    private static readonly Dictionary<string,Texture2D> DoorImages=new();
    private static Texture2D DoorImage(string zone)
    {
        string file=Art.ZoneArtOf(zone)?.Door;
        if (file==null) return null;
        if (DoorImages.TryGetValue(file,out var cached)) return cached;
        Texture2D texture=null;
        if (File.Exists(file))
        {
            texture=new Texture2D(2,2,TextureFormat.RGBA32,false) {name="DD3 corridor passage",hideFlags=HideFlags.HideAndDontSave};
            if (texture.LoadImage(File.ReadAllBytes(file)))
            {
                var pixels=texture.GetPixels32();
                for(int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++)
                {
                    int i=y*texture.width+x;
                    pixels[i].a=(byte)(pixels[i].a*CorridorDoorLayout.Alpha(x/(float)(texture.width-1),y/(float)(texture.height-1)));
                }
                texture.SetPixels32(pixels);texture.Apply(false,true);
            }
            else { Object.Destroy(texture);texture=null; }
        }
        DoorImages[file]=texture;return texture;
    }

    private static void DrawDoors(Crawl crawl,ExpeditionState exp,string zone,float slide,bool native,bool input)
    {
        var c=crawl.CurrentCorridor;
        bool reverse=exp.HeadingRoomId!=c.RoomB;
        var plan=RegionalScenery.For(exp.Quest.Dungeon);
        var choice=plan==null ? null : RegionSceneryArt.CorridorChoice(plan,exp.Seed,c.Id);
        var plate=RegionSceneryArt.RoomAlpha(choice?.FileName)>=.999f ? RegionSceneryArt.RoomTextureFor(choice?.FileName) : null;
        foreach(bool ahead in new[] {false,true})
        {
            int room=ahead ? exp.HeadingRoomId : c.Other(exp.HeadingRoomId);
            float x=CorridorDoorLayout.X(plate?.width ?? 0,plate?.height ?? 0,c.Tiles.Count,exp.TileIndex,reverse,ahead,slide);
            if(x < -240 || x > 2160) continue;
            bool enabled=input && crawl.CanEnterDoor(room) && !D.DoorTransitioning && !UiRoot.ModalOpen;
            var hit=new Rect(x-132,210,264,470);
            bool hover=enabled && hit.Contains(Event.current.mousePosition);
            var old=GUI.color;
            if(native && DoorImage(zone) is { } door)
            {
                if(hover) GUI.color=new Color(old.r*1.2f,old.g*1.12f,old.b*.96f,old.a);
                GUI.DrawTexture(new Rect(x-230,Feet-460*680/720f,460,460),door);
                GUI.color=old;
            }
            if (input && crawl.CanEnterDoor(room))
            {
                Gui.Fill(new Rect(x-108,210,216,34),new Color(0,0,0,old.a*.78f));
                Gui.Text(new Rect(x-130,210,260,34),ahead ? "Enter room" : "Return to room",22,
                    hover ? Gui.Gold : Gui.Dd1Name,TextAnchor.MiddleCenter,heading:true);
            }
            if (input && !D.DoorTransitioning && hit.Contains(Event.current.mousePosition))
                Gui.Tip(enabled ? "Click the passage to enter this room." : "Approach this passage to enter the room.");
            if(enabled && Gui.Hotspot(hit) && D.EnterDoor(room)) Dd1Audio.Play("/general/map/room_transition");
        }
    }
}
