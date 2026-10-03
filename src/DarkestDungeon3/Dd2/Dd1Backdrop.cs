using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Combat;
using Assets.Code.Utils;
using DarkestDungeon3.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD1 fights where the party stands: the arena DD2 loaded for the fight has its scenery hidden (its lights stay, so
/// the actors are lit as usual) and a screen-filling quad behind the actors shows the DD1 room or hallway square the
/// fight started in, its floor at the heroes' feet. The quad hangs from the camera that drew the scenery.
/// </summary>
internal static class Dd1Backdrop
{
    public static bool Ready { get; private set; }
    private static bool _failed;
    private static float _startedAt;
    private static readonly List<Renderer> Hidden = new();
    private static GameObject _quad;
    private static RenderTexture _texture;
    private static Material _material;

    /// <summary>A fight is about to start here: forget the last one.</summary>
    public static void Reset()
    {
        End();
        Ready = false;
        _failed = !Plugin.Dd1BackdropOn.Value;
        _startedAt = -1f;
    }

    /// <summary>Called every frame of a fight until it is set up (DD2's arena and actors load over a few frames).</summary>
    public static void Update()
    {
        if (Ready || _failed) return;
        if (_startedAt < 0) _startedAt = Time.unscaledTime;
        try
        {
            var arena = SingletonMonoBehaviour<ArenaBhv>.Instance;
            if (arena == null) { GiveUpAfter(3f, "no arena"); return; }
            var heroes = Driver.Instance?.Party?.Guids?.ToHashSet() ?? new HashSet<uint>();
            var actors = UnityEngine.Object.FindObjectsOfType<CombatActorBhv>().Where(a => a != null && a.ActorInstance != null).ToList();
            var heroRenderers = actors.Where(a => heroes.Contains(a.GetActorGuid()))
                                      .SelectMany(a => a.GetComponentsInChildren<Renderer>()).Where(r => r is SkinnedMeshRenderer or MeshRenderer).ToList();
            if (heroRenderers.Count == 0) { GiveUpAfter(3f, "no hero models yet"); return; }

            // The arena's scenery: every renderer in its scene that isn't part of an actor or an effect.
            var scenery = arena.gameObject.scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Renderer>(true))
                .Where(r => r is MeshRenderer or SkinnedMeshRenderer or SpriteRenderer && r.GetComponentInParent<ActorBhv>() == null)
                .ToList();
            if (scenery.Count == 0) { GiveUpAfter(3f, "no scenery found"); return; }
            int layer = scenery.GroupBy(r => r.gameObject.layer).OrderByDescending(g => g.Count()).First().Key;
            var cam = Camera.allCameras.Where(c => c.enabled && c.targetTexture == null && (c.cullingMask & (1 << layer)) != 0)
                                       .OrderBy(c => c.depth).FirstOrDefault() ?? Camera.main;
            if (cam == null) { GiveUpAfter(3f, "no camera"); return; }

            // The heroes' feet on screen (DD1's floor goes there), and how far behind the actors to hang the quad.
            float feetY = heroRenderers.Average(r => cam.WorldToScreenPoint(new Vector3(r.bounds.center.x, r.bounds.min.y, r.bounds.center.z)).y);
            float far = actors.SelectMany(a => a.GetComponentsInChildren<Renderer>()).Where(r => r is SkinnedMeshRenderer or MeshRenderer)
                              .Select(r => Vector3.Dot(r.bounds.center - cam.transform.position, cam.transform.forward)).DefaultIfEmpty(10f).Max();
            float distance = Mathf.Min(far + 6f, cam.farClipPlane * 0.9f);

            var material = Material();
            if (material == null) { _failed = true; return; }
            _texture = Compose(1080f - feetY * 1080f / Screen.height);
            if (_texture == null) { _failed = true; return; }
            material.mainTexture = _texture;

            _quad = new GameObject("DD3Backdrop");
            _quad.layer = layer;
            _quad.transform.SetParent(cam.transform, false);
            _quad.transform.localPosition = new Vector3(0, 0, distance);
            _quad.transform.localRotation = Quaternion.identity;
            float h = cam.orthographic ? cam.orthographicSize * 2f : 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            _quad.transform.localScale = new Vector3(h * cam.aspect, h, 1f);
            _quad.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            var mr = _quad.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            foreach (var r in scenery)
                if (r != null && r.enabled && !r.forceRenderingOff) { r.forceRenderingOff = true; Hidden.Add(r); }
            Ready = true;
            Plugin.Log.LogInfo($"[backdrop] DD1 scene behind the fight: {Hidden.Count} arena renderers hidden, camera {cam.name}, layer {layer}, distance {distance:0.0}, feet at {feetY:0}px");
        }
        catch (Exception e)
        {
            _failed = true;
            End();
            Plugin.Log.LogError("[backdrop] failed, DD2's arena stays: " + e);
        }
    }

    private static void GiveUpAfter(float seconds, string why)
    {
        if (Time.unscaledTime - _startedAt < seconds) return;
        _failed = true;
        Plugin.Log.LogInfo("[backdrop] not set up (" + why + "): DD2's arena stays");
    }

    /// <summary>Waiting on the backdrop: hold the DD1 scene a little longer (until it's in place or given up).</summary>
    public static bool Pending => !Ready && !_failed;

    /// <summary>The fight is over: give DD2 its arena back.</summary>
    public static void End()
    {
        foreach (var r in Hidden) if (r != null) r.forceRenderingOff = false;
        Hidden.Clear();
        if (_quad != null) UnityEngine.Object.Destroy(_quad);
        _quad = null;
        if (_texture != null) { _texture.Release(); UnityEngine.Object.Destroy(_texture); }
        _texture = null;
        Ready = false;
    }

    /// <summary>The DD1 scene where the party stands, on a 1920x1080 canvas, its floor (y 680 of the 720 strip) at
    /// <paramref name="feetY"/> pixels from the top; black around it.</summary>
    private static RenderTexture Compose(float feetY)
    {
        var d = Driver.Instance;
        var exp = d?.Expedition;
        var crawl = d?.Crawl;
        if (exp == null || crawl == null) return null;
        string zone = Core.Dungeon.ZoneBase.Of(exp.Quest.Dungeon);
        var rt = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32) { name = "DD3Backdrop" };
        rt.Create();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.black);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, 1920, 1080, 0);
        float top = feetY - 680f;
        void Draw(Texture2D tex, float x, float y, float w, float h, bool mirror = false)
        {
            if (tex == null) return;
            Graphics.DrawTexture(new Rect(x, y, w, h), tex, mirror ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1), 0, 0, 0, 0);
        }
        if (exp.InRoom)
        {
            var wall = exp.RoomId == exp.Map.EntranceRoomId ? Art.EntranceWall(zone) : Art.RoomWall(zone, exp.RoomId);
            Draw(wall, 0, top, 1920, 720);
        }
        else
        {
            var c = crawl.CurrentCorridor;
            int n = c.Tiles.Count;
            bool towardB = exp.HeadingRoomId == c.RoomB;
            int here = towardB ? exp.TileIndex : n - 1 - exp.TileIndex;
            float slide = -d.WalkProgress * 720f;
            foreach (var layer in new[] { Art.CorridorBackground(zone), Art.CorridorMid(zone) })
                for (float x = -720; x < 1920; x += 720) Draw(layer, x, top, 720, 720);
            var fgTop = Art.ForegroundTop(zone);
            var fgBottom = Art.ForegroundBottom(zone);
            for (int k = -2; k <= 2; k++)
            {
                int h = here + k;
                float x = 600 + k * 720 + slide;
                if (h >= 0 && h < n) Draw(Art.CorridorWall(zone, c.Id * 3 + (towardB ? h : n - 1 - h)), x, top, 720, 720);
                else if (h == -1 || h == n) Draw(Art.CorridorDoor(zone), x, top, 720, 720, mirror: h == -1);
                else if (h == -2 || h == n + 1) Draw(Art.EndHall(zone), x, top, 720, 720, mirror: h == -2);
                else continue;
                if (fgTop != null) Draw(fgTop, x, top, 720, fgTop.height);
                if (fgBottom != null) Draw(fgBottom, x, top + 720 - fgBottom.height, 720, fgBottom.height);
            }
        }
        GL.PopMatrix();
        RenderTexture.active = prev;
        return rt;
    }

    private static Mesh _mesh;

    private static Mesh QuadMesh()
    {
        if (_mesh != null) return _mesh;
        _mesh = new Mesh { name = "DD3BackdropQuad" };
        _mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0) };
        _mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        _mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 };
        _mesh.RecalculateBounds();
        return _mesh;
    }

    /// <summary>An unlit textured material DD2's pipeline draws (in its transparent pass, depth-tested so actors stay in front).</summary>
    private static Material Material()
    {
        if (_material != null) return _material;
        foreach (var name in new[] { "Sprites/Default", "Universal Render Pipeline/Unlit", "UI/Default", "Unlit/Texture" })
        {
            var shader = Shader.Find(name);
            if (shader == null) continue;
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (_material.HasProperty("unity_GUIZTestMode")) _material.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);
            if (_material.HasProperty("_BaseMap")) _material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            Plugin.Log.LogInfo("[backdrop] drawing with " + name);
            return _material;
        }
        Plugin.Log.LogWarning("[backdrop] no unlit shader found");
        return null;
    }
}
