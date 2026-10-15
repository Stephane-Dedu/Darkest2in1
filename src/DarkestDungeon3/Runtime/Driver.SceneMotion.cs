using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Dd2;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

internal sealed partial class Driver
{
    private readonly CorridorSceneMotion _sceneMotion = new();
    public float SceneSlide => CorridorSceneMotion.Slide(Time.unscaledTime - LastStepTime,LastStepDir,WalkProgress);
    public double SceneTravel => _sceneMotion.Travel;

    private void UpdateSceneMotion()
    {
        if (Phase != Phase.Crawling || Expedition == null || Expedition.InRoom || Expedition.Camp != null || Crawl == null)
        { _sceneMotion.Reset(); return; }
        var corridor = Crawl.CurrentCorridor;
        var plan = RegionalScenery.For(Expedition.Quest.Dungeon);
        var choice = plan != null ? RegionSceneryArt.CorridorChoice(plan,Expedition.Seed,corridor.Id) : null;
        // During the complete panorama's initial fade the legacy/native road remains
        // visible. Adopt its new camera as a fresh baseline, never as a giant footstep.
        var texture = RegionSceneryArt.RoomAlpha(choice?.FileName) >= .999f
            ? RegionSceneryArt.RoomTextureFor(choice?.FileName) : null;
        int width = texture != null ? texture.width : 0, height = texture != null ? texture.height : 0;
        float camera = CorridorSceneMotion.Camera(width,height,Expedition.TileIndex,corridor.Tiles.Count,
            Expedition.HeadingRoomId != corridor.RoomB,SceneSlide);
        float distance = _sceneMotion.Sample(Expedition.Seed,corridor.Id,Expedition.HeadingRoomId,
            texture != null ? choice.FileName : null,width,height,camera);
        HeroStage.WalkDistancePixels = distance;
        // Exported native clips also follow distance, rather than a separate held-key clock.
        HeroStage.WalkSpeed = Time.unscaledDeltaTime > 0 ? distance / (100 * Time.unscaledDeltaTime) : 0;
    }
}
