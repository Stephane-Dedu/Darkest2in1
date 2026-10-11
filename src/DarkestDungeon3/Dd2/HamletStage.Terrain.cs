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
        var paving=new Material(native) { name="Hamlet worn stone paths" }; _details.Add(paving);
        var rock=new Material(native) { name="Hamlet exposed cliff rock" }; _details.Add(rock);
        var stone=new Material(native) { name="Hamlet terrace masonry" }; _details.Add(stone);
        foreach (var material in new[] { _groundMaterial,paving,rock,stone })
            if (texture!=null) material.SetTexture("_BaseMap_texture",texture);
        bool soilLoaded=false,rockLoaded=false;
        if (_earthAsset.Status==AsyncOperationStatus.Succeeded && BaseTexture(_earthAsset.Result) is { } earthTexture)
        { _groundMaterial.SetTexture("_BaseMap_texture",earthTexture); soilLoaded=true; }
        if (_rockAsset.Status==AsyncOperationStatus.Succeeded && BaseTexture(_rockAsset.Result) is { } rockTexture)
        { rock.SetTexture("_BaseMap_texture",rockTexture); rockLoaded=true; }
        Grade(_groundMaterial,HamletPalette.Soil,soilLoaded); Grade(rock,HamletPalette.Rock,rockLoaded);
        Grade(paving,HamletPalette.Paving); Grade(stone,HamletPalette.Masonry);
        int layer=source.gameObject.layer;
        var data=_modelPack.Terrain;
        _groundMesh=MeshData("Compact DD1 cliff terrain",data.Positions,data.Uv);
        _groundMesh.colors=data.Shade.Select(s=>new Color(s,s,s,1)).ToArray();
        // Native paint ignores vertex colours. Three shared tint bands preserve the hillside's faceted light.
        var rockBands=new[] { new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>() };
        for (int i=0;i<data.Rock.Length;i+=3)
        {
            float light=data.Shade[data.Rock[i]];
            var band=rockBands[light<.83f?0:light<.90f?1:2];
            band.Add(data.Rock[i]); band.Add(data.Rock[i+1]); band.Add(data.Rock[i+2]);
        }
        var materials=new Material[4]; materials[0]=_groundMaterial;
        for (int i=0;i<3;i++)
        {
            materials[i+1]=new Material(rock) { name="Hamlet cliff facet "+i };
            float tint=.72f+i*.14f;
            materials[i+1].SetColor(Tint,new Color(tint,tint,tint,1)); _details.Add(materials[i+1]);
        }
        _groundMesh.subMeshCount=4; _groundMesh.SetTriangles(data.Ground,0);
        for (int i=0;i<3;i++) _groundMesh.SetTriangles(rockBands[i],i+1);
        Surface(_groundMesh,_groundMaterial,layer).sharedMaterials=materials;
        foreach (var part in new[] { (data.Paving,paving),(data.Masonry,stone) })
        {
            var mesh=MeshData(part.Item2.name,part.Item1.Positions,part.Item1.Uv);
            mesh.triangles=part.Item1.Indices; _details.Add(mesh); Surface(mesh,part.Item2,layer);
        }
        foreach (var renderer in _root.GetComponentsInChildren<MeshRenderer>())
            if (renderer.name.Contains("road_valley_straight_single")) renderer.enabled=false;
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
