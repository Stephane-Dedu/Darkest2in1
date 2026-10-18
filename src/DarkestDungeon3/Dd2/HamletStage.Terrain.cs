using System;
using System.Linq;
using DarkestDungeon3.Core.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DarkestDungeon3.Dd2;

internal sealed partial class HamletStage
{
    private Material _groundPaving, _groundRock, _groundStone, _groundValley;
    private Texture _groundGrass;
    private int _groundLayer;

    private bool BuildGround()
    {
        AdvanceGroundPaint();
        if (_groundPaintTask == null || !PaintReady || !UploadGroundPaint()) return false;
        var paving=_groundPaving; var rock=_groundRock; var stone=_groundStone; var valley=_groundValley;
        int layer=_groundLayer;
        SetPaintTint(paving,new Color(.72f,.75f,.80f,1));
        SetPaintTint(stone,new Color(.82f,.84f,.87f,1));
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
            float tint=(.72f+i*.14f)*.68f;
            SetPaintTint(materials[i+1],new Color(tint,tint,tint,1)); _details.Add(materials[i+1]);
        }
        _groundMesh.subMeshCount=4; _groundMesh.SetTriangles(data.Ground,0);
        for (int i=0;i<3;i++) _groundMesh.SetTriangles(rockBands[i],i+1);
        Surface(_groundMesh,_groundMaterial,layer).sharedMaterials=materials;
        var outcrops=HamletSetDressing.BuildRocks();
        var outcropMesh=MeshData("Hamlet broken cliff ledges",outcrops.Positions,outcrops.Uv);
        outcropMesh.subMeshCount=3; _details.Add(outcropMesh);
        var outcropBands=new[] { new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>() };
        for (int i=0;i<outcrops.Indices.Length;i+=3)
        {
            float shade=outcrops.Shade[outcrops.Indices[i]];
            outcropBands[shade<.83f?0:shade<.90f?1:2].AddRange(outcrops.Indices.Skip(i).Take(3));
        }
        for (int i=0;i<3;i++) outcropMesh.SetTriangles(outcropBands[i],i);
        Surface(outcropMesh,materials[2],layer).sharedMaterials=materials.Skip(1).ToArray();
        var surroundings=MeshData(valley.name,data.Surroundings.Positions,data.Surroundings.Uv);
        surroundings.triangles=data.Surroundings.Indices; _details.Add(surroundings); Surface(surroundings,valley,layer);
        foreach (var part in new[] { (data.Paving,paving),(data.Masonry,stone),(data.Stairs,stone) })
        {
            var mesh=MeshData(part.Item2.name,part.Item1.Positions,part.Item1.Uv);
            mesh.triangles=part.Item1.Indices; _details.Add(mesh); Surface(mesh,part.Item2,layer);
        }
        if (_grassTuftAsset.Status==AsyncOperationStatus.Succeeded && _grassTuftAsset.Result!=null)
        {
            var tufts=TownMaterial(_grassTuftAsset.Result);
            var paint=HamletPalette.GrassTuft;
            tufts.SetColor(Tint,new Color(paint.Tint.X,paint.Tint.Y,paint.Tint.Z,1));
            if (tufts.HasProperty("_Cull")) tufts.SetFloat("_Cull",0);
            tufts.DisableKeyword("_WIND");
            foreach (string property in new[] { "_WindFlutter","_WindSwing" })
                if (tufts.HasProperty(property)) tufts.SetFloat(property,0);
            var vegetation=HamletVegetation.Build();
            var mesh=MeshData("Hamlet native grass tufts",vegetation.Positions,vegetation.Uv);
            mesh.triangles=vegetation.Indices; _details.Add(mesh); Surface(mesh,tufts,layer);
            Plugin.Log.LogInfo($"[hamlet 3d] {HamletVegetation.Clumps.Count} native grass clumps in one owned mesh; 4096px source atlas");
        }
        foreach (var renderer in _root.GetComponentsInChildren<MeshRenderer>())
            if (renderer.name.Contains("road_valley_straight_single")) renderer.enabled=false;
        AddDepthMist(layer);
        return true;
    }

    private void PrepareGround()
    {
        var source=_root.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.sharedMaterials.Any(m=>m!=null&&m.name.Contains("valley_road_main")));
        if (source==null) throw new InvalidOperationException("Native road surface is unavailable.");
        var native=source.sharedMaterials.First(m=>m!=null&&m.name.Contains("valley_road_main"));
        var texture=_terrainAsset.Status==AsyncOperationStatus.Succeeded?BaseTexture(_terrainAsset.Result):BaseTexture(native);
        var surfaceShader=_surfaceShaderAsset.Status==AsyncOperationStatus.Succeeded?_surfaceShaderAsset.Result:null;
        if (surfaceShader==null) throw new InvalidOperationException("Town terrain shader is unavailable. Retry to reload scenery.");
        Material PaintSurface(string name) => new(surfaceShader) { name=name };
        _groundMaterial=PaintSurface("Hamlet earth");
        var paving=PaintSurface("Hamlet worn stone paths"); _details.Add(paving);
        var rock=PaintSurface("Hamlet exposed cliff rock"); _details.Add(rock);
        var stone=PaintSurface("Hamlet terrace masonry"); _details.Add(stone);
        foreach (var material in new[] { _groundMaterial,paving,rock,stone })
            if (texture!=null) SetPaintTexture(material,texture);
        bool soilLoaded=false,rockLoaded=false;
        if (_earthAsset.Status==AsyncOperationStatus.Succeeded && BaseTexture(_earthAsset.Result) is { } earthTexture)
        { SetPaintTexture(_groundMaterial,earthTexture); soilLoaded=true; }
        if (_rockAsset.Status==AsyncOperationStatus.Succeeded && BaseTexture(_rockAsset.Result) is { } rockTexture)
        { SetPaintTexture(rock,rockTexture); rockLoaded=true; }
        if (_wallAsset.Status==AsyncOperationStatus.Succeeded && BaseTexture(_wallAsset.Result) is { } wallTexture)
            SetPaintTexture(stone,wallTexture);
        Grade(_groundMaterial,HamletPalette.Soil,soilLoaded); Grade(rock,HamletPalette.Rock,rockLoaded);
        Grade(paving,HamletPalette.Paving); Grade(stone,HamletPalette.Masonry);
        Texture grass=null;
        if (_grassAsset.Status==AsyncOperationStatus.Succeeded && BaseTexture(_grassAsset.Result) is { } grassTexture)
            grass=PaintCopy(grassTexture,HamletPalette.Grass,false,512);
        if (Environment.GetCommandLineArgs().Contains("-dd3-hamlet3d-preview"))
            Plugin.Log.LogInfo($"[hamlet 3d paint] {_groundMaterial.shader.name}; color space {QualitySettings.activeColorSpace}; grass {_grassAsset.Status}/{_grassAsset.Result?.name}/{(_grassAsset.Result!=null?BaseTexture(_grassAsset.Result)?.name:null)}; rock {_rockAsset.Status}; earth {_earthAsset.Status}");
        var valley=new Material(_groundMaterial) { name="Hamlet surrounding valley" }; _details.Add(valley);
        SetPaintTint(valley,Color.white);
        SetPaintTint(paving,Color.white);
        _groundPaving=paving; _groundRock=rock; _groundStone=stone; _groundValley=valley;
        _groundLayer=source.gameObject.layer;
        _groundGrass=grass;
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
