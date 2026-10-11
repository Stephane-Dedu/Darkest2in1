using System;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    // Crops of DD1's upright idle attachment, expressed as fractions of its original image.
    // These are architectural details on fixed world surfaces, never a camera-facing town billboard.
    private void BuildDd1Details()
    {
        AddDetail("blacksmith", new Rect(.065f, .43f, .17f, .13f), -.28f, .55f, .24f); // Anvil sign.
        AddDetail("blacksmith", new Rect(.49f, .65f, .14f, .15f), .12f, .19f, .25f); // Forge opening.
        AddDetail("guild", new Rect(.18f, .415f, .12f, .25f), -.22f, .46f, .16f);
        AddDetail("guild", new Rect(.47f, .415f, .12f, .25f), .22f, .46f, .16f);
        AddDetail("abbey", new Rect(.23f, .38f, .085f, .08f), 0, .58f, .13f); // Rose window.
        AddDetail("abbey", new Rect(.45f, .54f, .085f, .105f), -.25f, .29f, .11f);
        AddDetail("abbey", new Rect(.45f, .54f, .085f, .105f), .25f, .29f, .11f);
    }

    private void AddDetail(string id, Rect crop, float x, float y, float width)
    {
        var building = _buildings.FirstOrDefault(b => b.Id == id);
        var session = Session.Current;
        if (building == null || session == null) return;
        string folder = session.Dd1.PathOf("fx", "town_" + id + "_level03");
        var files = Dd1Install.SpineIn(folder);
        if (files == null) return;
        var atlas = SpineAtlas.Parse(File.ReadAllText(files.Value.atlas));
        if (!atlas.Regions.TryGetValue("idle", out var region)) return;
        var texture = Art.Png(Path.Combine(folder, region.Page.File));
        if (texture == null) return;
        var shader = Shader.Find("Unlit/Transparent Cutout") ?? Shader.Find("Sprites/Default");
        if (shader == null) return;
        var material = new Material(shader) { name = "DD1 " + id + " facade detail", mainTexture = texture };
        if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(.84f, .81f, .77f, 1));
        if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", .2f);
        float w = building.Bounds.size.x * width;
        float h = w * (crop.height * region.OrigHeight) / (crop.width * region.OrigWidth);
        var centre = new Vector3(building.Bounds.center.x + x * building.Bounds.size.x,
            building.Bounds.min.y + y * building.Bounds.size.y, building.Bounds.min.z - .08f);
        var mesh = new Mesh
        {
            name = "DD1 architectural detail",
            vertices = new[] { new Vector3(-w / 2, -h / 2), new Vector3(-w / 2, h / 2), new Vector3(w / 2, h / 2), new Vector3(w / 2, -h / 2) },
            triangles = new[] { 0, 1, 2, 2, 3, 0 },
            uv = new[] { Uv(crop.xMin, crop.yMax), Uv(crop.xMin, crop.yMin), Uv(crop.xMax, crop.yMin), Uv(crop.xMax, crop.yMax) },
        };
        mesh.RecalculateBounds(); mesh.RecalculateNormals();
        var obj = new GameObject("DD1 " + id + " facade detail"); obj.layer = building.Renderers[0].gameObject.layer;
        obj.transform.SetParent(_root, false); obj.transform.position = centre;
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        building.Renderers.Add(renderer); _baseTints[renderer] = material.color;
        _details.Add(mesh); _details.Add(material);
        Plugin.Log.LogInfo($"[hamlet 3d] DD1 {id} facade detail mapped from installed atlas ({shader.name})");

        Vector2 Uv(float u, float v)
        {
            float px = u * region.OrigWidth - region.OffsetX;
            float py = v * region.OrigHeight - (region.OrigHeight - region.Height - region.OffsetY);
            return region.Rotate
                ? new Vector2((region.X + py) / texture.width, 1 - (region.Y + region.Width - px) / texture.height)
                : new Vector2((region.X + px) / texture.width, 1 - (region.Y + py) / texture.height);
        }
    }
}
