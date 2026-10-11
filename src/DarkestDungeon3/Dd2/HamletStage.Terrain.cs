using System;
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
        var source=_root.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.sharedMaterials.Any(m=>m!=null&&m.name.Contains("valley_road_main")));
        if (source==null) throw new InvalidOperationException("Native road surface is unavailable.");
        var native=source.sharedMaterials.First(m=>m!=null&&m.name.Contains("valley_road_main"));
        var texture=_terrainAsset.Status==AsyncOperationStatus.Succeeded?BaseTexture(_terrainAsset.Result):BaseTexture(native);
        _groundMaterial=new Material(native) { name="Hamlet earth" };
        if (texture!=null)
        {
            _groundTexture=new RenderTexture(512,512,0,RenderTextureFormat.ARGB32)
            { wrapMode=TextureWrapMode.Repeat,name="Hamlet repeatable native foundation" };
            _groundTexture.Create(); Graphics.Blit(texture,_groundTexture);
            _groundMaterial.SetTexture("_BaseMap_texture",_groundTexture);
        }
        var paving=new Material(_groundMaterial) { name="Hamlet worn stone paths" };
        Grade(paving,HamletPalette.Paving); _details.Add(paving);
        var rock=new Material(_groundMaterial) { name="Hamlet exposed cliff rock" };
        Grade(rock,HamletPalette.Rock); _details.Add(rock);
        var stone=new Material(paving) { name="Hamlet terrace masonry" };
        Grade(stone,HamletPalette.Masonry); _details.Add(stone);
        Grade(_groundMaterial,HamletPalette.Soil);
        if (_earthAsset.Status==AsyncOperationStatus.Succeeded && BaseTexture(_earthAsset.Result) is { } earthTexture)
            _groundMaterial.SetTexture("_BaseMap_texture",TileTexture(earthTexture,"Hamlet native soil",HamletPalette.Soil));
        if (_rockAsset.Status==AsyncOperationStatus.Succeeded && BaseTexture(_rockAsset.Result) is { } rockTexture)
            rock.SetTexture("_BaseMap_texture",TileTexture(rockTexture,"Hamlet native cliff paint",HamletPalette.Rock));
        int layer=source.gameObject.layer;
        var data=_modelPack.Terrain;
        _groundMesh=MeshData("Compact DD1 cliff terrain",data.Positions,data.Uv);
        _groundMesh.colors=data.Shade.Select(s=>new Color(s,s,s,1)).ToArray();
        _groundMesh.subMeshCount=2; _groundMesh.SetTriangles(data.Ground,0); _groundMesh.SetTriangles(data.Rock,1);
        Surface(_groundMesh,_groundMaterial,layer).sharedMaterials=new[] { _groundMaterial,rock };
        foreach (var part in new[] { (data.Paving,paving),(data.Masonry,stone) })
        {
            var mesh=MeshData(part.Item2.name,part.Item1.Positions,part.Item1.Uv);
            mesh.triangles=part.Item1.Indices; _details.Add(mesh); Surface(mesh,part.Item2,layer);
        }
        foreach (var renderer in _root.GetComponentsInChildren<MeshRenderer>())
            if (renderer.name.Contains("road_valley_straight_single")) renderer.enabled=false;
    }

    // Only owned material copies reach this method. Red Hook/Lit/Environment uses _COLOR_CONTROLS.
    private static void Grade(Material material,HamletPalette.Paint paint)
    {
        if (material==null) return;
        if (material.HasProperty(Tint)) material.SetColor(Tint,new Color(paint.Tint.X,paint.Tint.Y,paint.Tint.Z));
        if (material.HasProperty("_UseColorControl"))
        {
            material.SetFloat("_UseColorControl",1); material.EnableKeyword("_COLOR_CONTROLS");
            material.SetFloat("_Brightness",paint.Brightness); material.SetFloat("_Saturation",paint.Saturation);
        }
        if (material.HasProperty("_HasVertexPainting")) material.SetFloat("_HasVertexPainting",0);
        material.DisableKeyword("_HASVERTEXPAINTING_ON");
    }

    private RenderTexture TileTexture(Texture source,string name,HamletPalette.Paint paint)
    {
        var texture=new RenderTexture(512,512,0,RenderTextureFormat.ARGB32) { wrapMode=TextureWrapMode.Mirror,name=name };
        _details.Add(texture); texture.Create();
        Graphics.Blit(source,texture,new Vector2(paint.UvScale.X,paint.UvScale.Y),new Vector2(paint.UvOffset.X,paint.UvOffset.Y));
        return texture;
    }

    private static Mesh MeshData(string name,float[] positions,float[] coordinates)
    {
        var vertices=new Vector3[positions.Length/3]; var uv=new Vector2[vertices.Length];
        for (int i=0;i<vertices.Length;i++)
        { vertices[i]=new(positions[i*3],positions[i*3+1],positions[i*3+2]); uv[i]=new(coordinates[i*2],coordinates[i*2+1]); }
        return new Mesh { name=name,indexFormat=IndexFormat.UInt32,vertices=vertices,uv=uv };
    }

    private MeshRenderer Surface(Mesh mesh,Material material,int layer)
    {
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (mesh.colors.Length!=mesh.vertexCount) mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();
        var obj=new GameObject(mesh.name) { layer=layer }; obj.transform.SetParent(_root,false);
        obj.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        return renderer;
    }
}
