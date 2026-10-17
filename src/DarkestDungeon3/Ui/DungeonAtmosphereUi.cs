using System.Collections.Generic;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Native atlases shared with the quest map; scene-clipped, behind props and actors.</summary>
internal static class DungeonAtmosphereUi
{
    private static readonly List<DungeonAtmosphere.Particle> Particles = new(56);
    internal static int LastDrawCount { get; private set; }
    private struct View { public string Region, File; public int Seed; public float Light, Alpha; public PanoramaCrop Crop; }
    private static View? _frozen;
    public static void Freeze() { _frozen = Current(out var view) ? view : null; }
    public static void Unfreeze() { _frozen = null; }

    public static void Draw(string region, int seed, float seconds, float light, PanoramaCrop crop, float alpha = 1, bool graphics = false, string file = null)
    {
        if (!graphics && Event.current.type != EventType.Repaint) return;
        LastDrawCount = 0;
        ExpeditionMapEffects.Prepare();
        DungeonAtmosphere.Fill(Particles, region, seed, seconds, light, RegionSceneryArt.FireBounds(file));
        var previous = GUI.color;
        try
        {
            foreach (var particle in Particles)
            {
                if (!ExpeditionMapEffects.TryGet(particle.Effect, out var clip, out var texture)
                    || !DungeonAtmosphere.Project(particle, crop, clip.Uv(seconds, particle.Phase), out var r, out var uv)) continue;
                var colour = new Color(particle.R,particle.G,particle.B,particle.Alpha*alpha*previous.a);
                var rect = new Rect(r.X,r.Y,r.Width,r.Height);
                var coords = new Rect(uv.X,uv.Y,uv.Width,uv.Height);
                if (graphics) Graphics.DrawTexture(rect,texture,coords,0,0,0,0,colour);
                else { GUI.color = colour; GUI.DrawTextureWithTexCoords(rect,texture,coords,true); }
                LastDrawCount++;
            }
        }
        finally { GUI.color = previous; }
    }

    public static void DrawCurrent(bool graphics = false, float alpha = 1)
    {
        View view;
        if (Driver.Instance?.Phase == Phase.Fighting && _frozen.HasValue) view = _frozen.Value;
        else if (!Current(out view)) return;
        Draw(view.Region,view.Seed,Time.unscaledTime,view.Light,view.Crop,alpha*view.Alpha,graphics,view.File);
    }

    private static bool Current(out View view)
    {
        view = default;
        var d = Driver.Instance;
        var exp = d?.Expedition;
        if (exp == null || d.Crawl == null || RegionalScenery.For(exp.Quest.Dungeon) is not { } plan) return false;
        var choice = exp.InRoom ? RegionSceneryArt.RoomChoice(plan, exp.Seed, d.Crawl.CurrentRoom)
            : RegionSceneryArt.CorridorChoice(plan, exp.Seed, d.Crawl.CurrentCorridor.Id);
        var texture = RegionSceneryArt.RoomTextureFor(choice?.FileName);
        if (texture == null) return false;
        float slide = d.SceneSlide;
        var corridor = d.Crawl.CurrentCorridor;
        var crop = exp.InRoom ? new PanoramaCrop(0,0,1,1) : CorridorPanoramaLayout.Crop(texture.width,texture.height,
            CorridorPanoramaLayout.ViewProgress(exp.TileIndex,corridor.Tiles.Count,exp.HeadingRoomId != corridor.RoomB,slide),corridor.Tiles.Count);
        view = new View { Region = plan.Region, File = choice.FileName, Seed = exp.Seed ^ (exp.InRoom ? exp.RoomId : corridor.Id),
            Light = exp.Light, Crop = crop, Alpha = RegionSceneryArt.RoomAlpha(choice.FileName) };
        return true;
    }
}
