using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    private void BuildGround()
    {
        var source = _root.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.sharedMaterials.Any(m => m != null && m.name.Contains("valley_road_main")));
        if (source == null) throw new InvalidOperationException("Native road surface is unavailable.");
        var native = source.sharedMaterials.First(m => m != null && m.name.Contains("valley_road_main"));
        var texture = _terrainAsset.Status == AsyncOperationStatus.Succeeded ? BaseTexture(_terrainAsset.Result) : BaseTexture(native);
        _groundMaterial = new Material(native) { name = "Hamlet dark earth and shale" };
        if (texture != null)
        {
            _groundTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32)
            { wrapMode = TextureWrapMode.Repeat, name = "Hamlet repeatable native foundation" };
            _groundTexture.Create(); Graphics.Blit(texture, _groundTexture);
            _groundMaterial.SetTexture("_BaseMap_texture", _groundTexture);
        }
        _groundMaterial.SetColor(Tint, new Color(.52f, .50f, .43f));
        var paving = new Material(_groundMaterial) { name = "Hamlet worn stone paths" };
        paving.SetColor(Tint, new Color(.85f, .80f, .68f)); _details.Add(paving);
        var stone = new Material(paving) { name = "Hamlet terrace retaining stone" };
        stone.SetColor(Tint, new Color(.58f, .59f, .58f)); _details.Add(stone);
        int layer = source.gameObject.layer;
        const int side = 121;
        var vertices = new Vector3[side * side]; var uv = new Vector2[vertices.Length]; var triangles = new int[(side - 1) * (side - 1) * 6];
        for (int z = 0; z < side; z++) for (int x = 0; x < side; x++)
        {
            int i = z * side + x; float px = -150 + x * 300f / (side - 1), pz = -120 + z * 300f / (side - 1);
            vertices[i] = new Vector3(px, HamletLayout.Height(px, pz) - .14f, pz);
            uv[i] = new Vector2(px / 7, pz / 7);
            if (x == side - 1 || z == side - 1) continue;
            int t = (z * (side - 1) + x) * 6;
            triangles[t] = i; triangles[t + 1] = i + side; triangles[t + 2] = i + 1;
            triangles[t + 3] = i + 1; triangles[t + 4] = i + side; triangles[t + 5] = i + side + 1;
        }
        _groundMesh = new Mesh { name = "Terraced Hamlet hillside", vertices = vertices, uv = uv, triangles = triangles };
        Surface("Hillside", _groundMesh, _groundMaterial, layer);
        var road = new List<Vector2>();
        for (int z = -42; z <= 82; z += 2) road.Add(new Vector2(HamletLayout.RoadX(z), z));
        PathSurface("Winding uphill road", road, 6, paving, layer);
        foreach (var site in HamletLayout.Sites)
        {
            float z = site.Z - site.Width * .46f;
            var branch = new List<Vector2>();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                branch.Add(new Vector2(Mathf.Lerp(HamletLayout.RoadX(z - 2), site.X, t), Mathf.Lerp(z - 2, z, t)));
            }
            PathSurface(site.Id + " approach", branch, site.Id == "statue" ? 3 : 4, paving, layer);
            if (site.Y < 4) continue;
            // Low masonry along the downhill lip makes each level legible from the overview camera.
            // Open the middle for its approach instead of fencing off the service entrance.
            float front = site.Z - site.Width * .57f, edge = site.Width * .58f;
            for (float x = -edge; x < edge; x += 2.2f)
            {
                if (Mathf.Abs(x) < 2.5f) continue;
                StoneBlock(site.Id + " terrace", new Vector3(site.X + x, site.Y - .45f, front), new Vector3(2.1f, 1.25f, .8f), stone, layer);
            }
            for (int step = 0; step < 4; step++)
            {
                float pz = front - 2 + step * .55f;
                float y = HamletLayout.Height(site.X, pz);
                StoneBlock(site.Id + " approach step", new Vector3(site.X, y - .1f, pz), new Vector3(3.5f, .3f, .65f), paving, layer);
            }
        }
        // Keep the loaded native road material, but replace the flat tile geometry with the hill and ribbons.
        foreach (var renderer in _root.GetComponentsInChildren<MeshRenderer>())
            if (renderer.name.Contains("road_valley_straight_single")) renderer.enabled = false;
    }

    private void Surface(string name, Mesh mesh, Material material, int layer)
    {
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        // DD2 environment variants multiply paint by vertex colour; procedural surfaces must supply white.
        mesh.colors = Enumerable.Repeat(Color.white, mesh.vertexCount).ToArray();
        var obj = new GameObject(name) { layer = layer }; obj.transform.SetParent(_root, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }

    private void PathSurface(string name, List<Vector2> points, float width, Material material, int layer)
    {
        var vertices = new Vector3[points.Count * 2]; var uv = new Vector2[vertices.Length]; var triangles = new int[(points.Count - 1) * 6];
        float length = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var direction = (points[Math.Min(i + 1, points.Count - 1)] - points[Math.Max(0, i - 1)]).normalized;
            var normal = new Vector2(direction.y, -direction.x);
            if (i > 0) length += Vector2.Distance(points[i], points[i - 1]);
            for (int edge = 0; edge < 2; edge++)
            {
                var p = points[i] + normal * (width * (edge == 0 ? -.5f : .5f));
                vertices[i * 2 + edge] = new Vector3(p.x, HamletLayout.Height(p.x, p.y) + .04f, p.y);
                uv[i * 2 + edge] = new Vector2(edge * width / 5, length / 5);
            }
            if (i == points.Count - 1) continue;
            int t = i * 6, v = i * 2;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles }; _details.Add(mesh);
        Surface(name, mesh, material, layer);
    }

    private void StoneBlock(string name, Vector3 position, Vector3 scale, Material material, int layer)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name; obj.layer = layer; obj.transform.SetParent(_root, false);
        obj.transform.localPosition = position; obj.transform.localScale = scale;
        Destroy(obj.GetComponent("BoxCollider"));
        var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }
}
