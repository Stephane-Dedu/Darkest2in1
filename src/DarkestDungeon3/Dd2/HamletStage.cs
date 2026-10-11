using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Runtime;
using DarkestDungeon3.Ui;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DarkestDungeon3.Dd2;

/// <summary>Owned, disposable perspective town. Copies render components only, never native road behaviours.</summary>
internal sealed partial class HamletStage : MonoBehaviour
{
    public static HamletStage Instance { get; private set; }
    private static bool? _previewMode;
    public static bool Enabled => _previewMode ?? Plugin.Hamlet3D.Value;
    public static readonly string[] Services = { "tavern", "stage_coach", "sanitarium", "graveyard", "abbey", "guild", "nomad_wagon", "blacksmith", "camping_trainer", "statue" };
    public static void SetEnabled(bool value)
    {
        if (HamletPreview.OwnsSession) _previewMode = value; else Plugin.Hamlet3D.Value = value;
        if (!value) Instance?.Clear();
        else if (Instance != null) Instance.Failure = null;
    }
    internal static void PreviewMode(bool? value) => _previewMode = value;

    private const string Common = "Assets/Data/Biome/Common/Map/NodeTiles/";
    private const string Models = "Assets/Art/Models/Environments/Common/Models/Nodes/";
    private const string Valley = "Assets/Data/Biome/ValleyCommon/Map/";
    private const string Mountains = "Assets/Art/Models/Environments/Common/Models/MainMenu/";
    private const string PaintTemplate = Models + "inn_lvl_03_prefab.prefab";
    private sealed class Piece
    {
        public readonly string Id, Path, Include;
        public readonly Vector3 Position;
        public readonly float Width, Rotation, HeightScale, DepthScale;
        public Piece(string id, string path, float x, float y, float z, float width, float rotation = 0, string include = null, float heightScale = 1, float depthScale = 1)
        { Id = id; Path = path; Position = new(x, y, z); Width = width; Rotation = rotation; Include = include; HeightScale = heightScale; DepthScale = depthScale; }
    }
    // Surrounding native scenery is separate from the ten private building models.
    private static readonly Piece[] Pieces =
    {
        new(null, Valley + "RoadTiles/road_valley_straight_single.prefab", -16, .04f, 8, 16, 90),
        new(null, Valley + "RoadTiles/road_valley_straight_single.prefab", 0, .04f, 8, 16, 90),
        new(null, Valley + "RoadTiles/road_valley_straight_single.prefab", 16, .04f, 8, 16, 90),
        new(null, Valley + "DecorTiles/valley_tile_01.prefab", -28, -1.8f, 51, 16),
        new(null, Valley + "DecorTiles/valley_tile_02.prefab", 36, -1.8f, 54, 18),
        new(null, Valley + "DecorTiles/valley_tile_01.prefab", -58, -1.8f, 30, 18, 30),
        new(null, Valley + "DecorTiles/valley_tile_02.prefab", 49, -1.8f, 29, 18, -20),
        new(null, "Assets/Art/Models/Environments/Camp/camp_bush_01.fbx", -50, 0, -3, 6, 15),
        new(null, "Assets/Art/Models/Environments/Camp/camp_bush_01.fbx", 44, 0, -7, 6, -25),
        new(null, Mountains + "mainmenu_mountain_mdl.fbx", 0, -36, 96, 174, heightScale: .85f, depthScale: .34f),
        new(null, "Assets/Art/Models/Environments/Camp/camp_sky.fbx", 0, -95, 152, 460),
        new(null, "Assets/Art/Models/Environments/Camp/camp_crate.fbx", -50, 0, 23, 2),
        new(null, "Assets/Art/Models/Environments/Camp/camp_barrel.fbx", -51, 0, 21, 1.5f),
        new(null, "Assets/Art/Models/Environments/Camp/camp_firepit.fbx", -51, 0, 17, 2.5f),
        new(null, "Assets/Art/Models/Environments/Common/Models/Props/common_prop_lamppost.fbx", -6, 0, 4, 1.5f),
        new(null, "Assets/Art/Models/Environments/Common/Models/Props/common_prop_lamppost.fbx", 22, 0, 10, 1.5f),
        new(null, Models + "barricade_military_decor_rocks_01.fbx", -44, -.25f, 5, 7, 25),
        new(null, Models + "barricade_military_decor_rocks_02.fbx", -25, -.35f, 16, 4, -20),
        new(null, Models + "barricade_military_decor_rocks_03.fbx", 34, -.3f, 12, 6, 15),
        new(null, Models + "barricade_military_decor_rocks_02.fbx", 40, -.25f, 25, 8, -40),
        new(null, Valley + "DecorTiles/valley_tile_09.prefab", -46, -.25f, -10, 9, 20),
        new(null, Valley + "DecorTiles/valley_tile_09.prefab", -16, -.2f, -14, 7, -10),
        new(null, Valley + "DecorTiles/valley_tile_09.prefab", 43, -.25f, 10, 7, 65),
        new(null, Valley + "DecorTiles/valley_tile_09.prefab", -1, -.2f, 41, 6, 15),
        new(null, "Assets/Art/Models/Environments/Coast/Models/Skirt/coast_skirt_cliffs_mdl_01.fbx", -59, -2, -7, 15, 15),
        new(null, "Assets/Art/Models/Environments/Coast/Models/Skirt/coast_skirt_cliffs_mdl_01.fbx", 49, -1, 25, 18, -25),
    };

    private sealed class Building
    {
        public string Id;
        public Bounds Bounds;
        public readonly List<Renderer> Renderers = new();
        public readonly List<Bounds> HitBounds = new();
        public bool Available = true;
    }
    private readonly Dictionary<string, AsyncOperationHandle<GameObject>> _assets = new();
    private readonly List<Building> _buildings = new();
    private readonly Dictionary<Renderer, Color> _baseTints = new();
    private readonly List<UnityEngine.Object> _details = new();
    private readonly Dictionary<Material, Material> _materials = new();
    private AsyncOperationHandle<Material> _terrainAsset, _earthAsset, _rockAsset;
    private Transform _root;
    private Camera _camera;
    private Camera _nativeView;
    private RenderPipelineAsset _nativePipeline;
    private GraphicsFormat _nativeFormat;
    private bool _nativeHdr;
    private RenderTexture _texture;
    private Mesh _groundMesh;
    private Material _groundMaterial;
    private int _lastRequest = -100, _piece;
    private float _started;
    private bool _rendered;
    private string _focus, _hover;
    private Vector2 _pan;
    private float _zoom = 1;
    private Vector3 _look;
    private float _savedFog;
    private Color _savedAmbient, _savedAmbientNoShadow;
    private bool _savedUnityFog;
    private static readonly Vector3 Origin = new(30000, 20000, 0);
    private static readonly int Fog = Shader.PropertyToID("_global_fog_enabled"), Ambient = Shader.PropertyToID("_GlobalAmbientColor"),
        AmbientNoShadow = Shader.PropertyToID("_GlobalAmbientColorWithoutShadowColor"), Tint = Shader.PropertyToID("_BaseMap_ColorTint");
    public string Failure { get; private set; }
    public bool Ready => _rendered && _buildings.Count == Services.Length && Failure == null;
    public Texture Texture => _texture;
    internal bool HasResources => _root != null || _assets.Count != 0 || _terrainAsset.IsValid() || _earthAsset.IsValid() || _rockAsset.IsValid() || _details.Count != 0
        || _texture != null || _groundMesh != null || _groundMaterial != null || _modelTask != null;
    internal Vector2 Pan => _pan;
    internal float Zoom => _zoom;

    private void Awake()
    {
        Instance = this;
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
        RenderPipelineManager.endCameraRendering += AfterCamera;
    }
    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        RenderPipelineManager.endCameraRendering -= AfterCamera;
        Clear();
        if (Instance == this) Instance = null;
    }
    public void Request()
    {
        _lastRequest = Time.frameCount;
        if (_root != null || Failure != null) return;
        try { Begin(); }
        catch (Exception e) { Fail(e.Message); }
    }
    private void Begin()
    {
        _started = Time.unscaledTime;
        _root = new GameObject("DD3 Hamlet 2.0 visuals").transform;
        DontDestroyOnLoad(_root.gameObject);
        _root.position = Origin;
        _texture = new RenderTexture(Screen.width, Screen.height, 24)
        { name = "DD3 perspective Hamlet", graphicsFormat = NativeTargetFormat(NativeViewCamera()) };
        _texture.Create();
        var cameraObject = new GameObject("DD3 Hamlet perspective camera");
        cameraObject.transform.SetParent(_root, false);
        _camera = cameraObject.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(.065f, .075f, .105f);
        _camera.fieldOfView = HamletLayout.FieldOfView;
        _camera.aspect = 16f / 9;
        _camera.nearClipPlane = .3f;
        _camera.farClipPlane = 400;
        _camera.targetTexture = _texture;
        var nativeView = NativeViewCamera();
        _camera.allowHDR = nativeView != null && nativeView.allowHDR;
        _camera.allowMSAA = false;
        _camera.depth = -20;
        _camera.enabled = false;
        var urp = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urp != null)
        {
            var data = cameraObject.AddComponent(urp);
            urp.GetProperty("renderPostProcessing")?.SetValue(data, false);
            urp.GetProperty("renderShadows")?.SetValue(data, false);
            var mainData = nativeView != null ? nativeView.GetComponent(urp) : null;
            var field = urp.GetField("m_RendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            if (mainData != null && field != null) urp.GetMethod("SetRenderer")?.Invoke(data, new[] { field.GetValue(mainData) });
        }
        _look = Overview;
        ResetView();
        MoveCamera(1);
        BeginModels();
        foreach (var piece in Pieces)
            if (!_assets.ContainsKey(piece.Path)) _assets[piece.Path] = NativeAssets.Load<GameObject>(piece.Path);
        _assets[PaintTemplate] = NativeAssets.Load<GameObject>(PaintTemplate);
        _terrainAsset = NativeAssets.Load<Material>("Assets/Art/Textures/Environments/Tileable/foundation_ground.mat");
        _earthAsset = NativeAssets.Load<Material>("Assets/Art/Textures/Environments/Tileable/farm_ground_hill_transition.mat");
        _rockAsset = NativeAssets.Load<Material>("Assets/Art/Materials/Environments/Nodes/cultist_barricade_rocks.mat");
        Plugin.Log.LogInfo("[hamlet 3d] loading installed scenery; visual components only; no road scripts");
    }
    private void Update()
    {
        if (_root == null) return;
        if (!Enabled || Time.frameCount - _lastRequest > 3) { Clear(); return; }
        try
        {
            MatchNativeTarget();
            if (_piece < Pieces.Length)
            {
                if (Time.unscaledTime - _started > 45) throw new InvalidOperationException("Town scenery did not finish loading. Painted town remains available.");
                var piece = Pieces[_piece];
                var handle = _assets[piece.Path];
                if (!handle.IsDone) return;
                if (_piece == Pieces.Length - 1 && (!_terrainAsset.IsDone || !_earthAsset.IsDone || !_rockAsset.IsDone)) return;
                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null) Build(piece, handle.Result);
                else if (piece.Id != null) throw new InvalidOperationException("Missing native model for " + piece.Id);
                _piece++;
            }
            else if (!_camera.enabled && AdvanceModels())
            {
                if (_buildings.Count != Services.Length) throw new InvalidOperationException("Some town services have no model.");
                BuildGround();
                _camera.enabled = true;
                Plugin.Log.LogInfo($"[hamlet 3d] built v02: {_buildings.Count} services, {_root.GetComponentsInChildren<MeshRenderer>().Length} renderers; compact DD1 cliff layout, 24.5 metres of service elevation");
                Plugin.Log.LogInfo($"[hamlet 3d] {_root.GetComponentsInChildren<ParticleSystem>().Length} owned chimney plumes; target {_texture.width}x{_texture.height}/{_texture.graphicsFormat}");
            }
            MoveCamera(1 - Mathf.Exp(-Time.unscaledDeltaTime * 5));
        }
        catch (Exception e) { Fail(e.Message); }
    }
    private Camera NativeViewCamera()
    {
        if (_nativeView!=null && _nativeView.isActiveAndEnabled && _nativeView.targetTexture==null) return _nativeView;
        return _nativeView=Camera.main ?? Camera.allCameras.FirstOrDefault(c=>c!=_camera && c.targetTexture==null);
    }

    private GraphicsFormat NativeTargetFormat(Camera native)
    {
        var pipeline=GraphicsSettings.currentRenderPipeline;
        bool cameraHdr=native!=null && native.allowHDR;
        if (_nativeFormat!=GraphicsFormat.None && pipeline==_nativePipeline && cameraHdr==_nativeHdr) return _nativeFormat;
        _nativePipeline=pipeline; _nativeHdr=cameraHdr;
        var type=pipeline?.GetType();
        bool hdr=cameraHdr && (bool?)(type?.GetProperty("supportsHDR")?.GetValue(pipeline))==true;
        var precision=type?.GetProperty("hdrColorBufferPrecision")?.GetValue(pipeline);
        var urp=Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipeline, Unity.RenderPipelines.Universal.Runtime");
        var choose=urp?.GetMethod("MakeRenderTextureGraphicsFormat",BindingFlags.Static|BindingFlags.NonPublic);
        return _nativeFormat=choose!=null && precision!=null
            ? (GraphicsFormat)choose.Invoke(null,new[] { (object)hdr,precision,Graphics.preserveFramebufferAlpha })
            : SystemInfo.GetGraphicsFormat(hdr?DefaultFormat.HDR:DefaultFormat.LDR);
    }

    private void MatchNativeTarget()
    {
        var native=NativeViewCamera();
        if (native!=null) _camera.allowHDR=native.allowHDR;
        var format=NativeTargetFormat(native);
        if (_texture.width==Screen.width && _texture.height==Screen.height && _texture.graphicsFormat==format) return;
        _camera.targetTexture=null; _texture.Release();
        _texture.width=Mathf.Max(1,Screen.width); _texture.height=Mathf.Max(1,Screen.height); _texture.graphicsFormat=format; _texture.Create();
        _camera.targetTexture=_texture;
    }
    private static Mesh SourceMesh(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
    private static bool Visual(Renderer renderer, string include)
    {
        string name = renderer.name.ToLowerInvariant();
        string mesh = SourceMesh(renderer)?.name.ToLowerInvariant();
        if (mesh == null) return false;
        if (new[] { "vfx", "ink_smoke", "fog", "glow", "scorch", "shadow", "_low", "lod1", "lod2" }.Any(s => name.Contains(s) || mesh.Contains(s))) return false;
        // DD2 wagon wheels are deliberately named Mesh and MeshCollider; they are visible geometry.
        if (name.Contains("collider") && name.Replace(" ", "") != "meshandmeshcollider") return false;
        if (renderer.sharedMaterials.All(m => m == null || m.name.IndexOf("vfx", StringComparison.OrdinalIgnoreCase) >= 0)) return false;
        if (include != null && !include.Split('|').Any(name.Contains)) return false;
        return true;
    }
    private void Build(Piece piece, GameObject prefab)
    {
        var group = new GameObject("Hamlet " + (piece.Id ?? "scenery")).transform;
        group.SetParent(_root, false);
        var lowerLods = new HashSet<Renderer>();
        foreach (var lod in prefab.GetComponentsInChildren<LODGroup>(true))
            foreach (var level in lod.GetLODs().Skip(1)) foreach (var renderer in level.renderers) lowerLods.Add(renderer);
        var renderers = new List<Renderer>();
        foreach (var source in prefab.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer or SkinnedMeshRenderer))
        {
            if (lowerLods.Contains(source) || !Visual(source, piece.Include)) continue;
            var matrix = prefab.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
            var copy = new GameObject(source.name);
            copy.layer = source.gameObject.layer;
            copy.transform.SetParent(group, false);
            copy.transform.localPosition = matrix.GetColumn(3);
            copy.transform.localRotation = matrix.rotation;
            copy.transform.localScale = matrix.lossyScale;
            var mesh = SourceMesh(source);
            if (source is SkinnedMeshRenderer skinned)
            {
                mesh = new Mesh { name = "Hamlet static " + mesh.name };
                _details.Add(mesh); skinned.BakeMesh(mesh);
            }
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = copy.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials.Select(TownMaterial).ToArray();
            if (piece.Path.EndsWith("camp_sky.fbx"))
                renderer.sharedMaterials = source.sharedMaterials.Select(BackdropMaterial).ToArray();
            else if (piece.Path.StartsWith(Mountains))
                foreach (var painted in renderer.sharedMaterials) Grade(painted,HamletPalette.Mountain);
            else if (piece.Path.Contains("barricade_military_decor_rocks") || piece.Path.Contains("coast_skirt_cliffs"))
                foreach (var painted in renderer.sharedMaterials) Grade(painted,HamletPalette.Rubble);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            source.GetPropertyBlock(block); renderer.SetPropertyBlock(block);
            var material = renderer.sharedMaterial;
            _baseTints[renderer] = material != null && material.HasProperty(Tint) ? block.HasColor(Tint) ? block.GetColor(Tint) : material.GetColor(Tint)
                : material != null && material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            renderers.Add(renderer);
            if (Environment.GetCommandLineArgs().Contains("-dd3-hamlet3d-preview"))
                Plugin.Log.LogInfo($"[hamlet 3d mesh] {piece.Id ?? "scenery"}/{source.name}: {mesh.name}; {string.Join(",", source.sharedMaterials.Select(m => m == null ? "null" : m.name + ":" + m.shader.name))}; bounds {renderer.bounds.size}");
        }
        if (renderers.Count == 0) { Destroy(group.gameObject); if (piece.Id != null) throw new InvalidOperationException("No usable geometry for " + piece.Id); return; }
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        float scale = piece.Width / Mathf.Max(bounds.size.x, bounds.size.z, .1f);
        // Normalise each native assembly to a ground pivot before placing it in the shared scene.
        var shift = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - Origin;
        foreach (Transform child in group) child.localPosition -= shift;
        group.localScale = new Vector3(scale, scale * piece.HeightScale, scale * piece.DepthScale);
        group.localRotation = Quaternion.Euler(0, piece.Rotation, 0);
        group.localPosition = piece.Position + Vector3.up * HamletLayout.Height(piece.Position.x, piece.Position.z);
        if (piece.Id != null)
        {
            var building = new Building { Id = piece.Id, Bounds = renderers[0].bounds };
            building.Renderers.AddRange(renderers);
            foreach (var renderer in renderers.Skip(1)) building.Bounds.Encapsulate(renderer.bounds);
            building.HitBounds.AddRange(renderers.Select(r => r.bounds));
            _buildings.Add(building);
        }
        Plugin.Log.LogInfo($"[hamlet 3d] {piece.Id ?? "scenery"}: {renderers.Count} visual meshes ({prefab.name})");
    }
    private static readonly Vector3 Overview = new(HamletLayout.CameraTarget.X, HamletLayout.CameraTarget.Y, HamletLayout.CameraTarget.Z);

    private Material BackdropMaterial(Material source)
    {
        if (source == null || !source.HasProperty("_BaseMap_texture")) return source;
        var material = new Material(Shader.Find("Sprites/Default")) { name = "Hamlet " + source.name, mainTexture = source.GetTexture("_BaseMap_texture") };
        _details.Add(material);
        return material;
    }

    private static Texture BaseTexture(Material source)
    {
        if (source == null) return null;
        foreach (string property in new[] { "_BaseMap_texture", "_BaseMap", "_MainTex" })
            if (source.HasProperty(property) && source.GetTexture(property) is { } texture) return texture;
        return null;
    }

    private Material TownMaterial(Material source)
    {
        if (source == null) return null;
        if (_materials.TryGetValue(source, out var existing)) return existing;
        Material material;
        if (source.name == "env_black" || source.name == "farm_ground_dark")
            material = new Material(Shader.Find("Sprites/Default")) { color = Color.clear, name = "Hidden native road backing" };
        else
        {
            material = new Material(source) { name = source.name + " Hamlet" };
            foreach (string property in new[] { "_____Darken_Over_Distance", "_NearDark", "_SampleFog", "_____IsDithering", "_____Nearfade", "_UseDither01", "_HasLighting" })
                if (material.HasProperty(property)) material.SetFloat(property, 0);
            foreach (string keyword in new[] { "_FOG", "_HASLIGHTING_ON", "_NEARDARK_ON", "_____DARKEN_OVER_DISTANCE_ON", "_____ISDITHERING_ON", "_____NEARFADE_ON" })
                material.DisableKeyword(keyword);
        }
        _materials[source] = material; _details.Add(material);
        return material;
    }
    private void MoveCamera(float blend)
    {
        if (_camera == null) return;
        var focused = _buildings.FirstOrDefault(b => b.Id == _focus);
        var target = focused == null ? Overview : Vector3.Lerp(Overview, focused.Bounds.center - Origin, .4f);
        target += new Vector3(_pan.x, 0, _pan.y);
        _look = Vector3.Lerp(_look, target, blend);
        var layoutOffset = HamletLayout.CameraOffset;
        var offset = new Vector3(layoutOffset.X, layoutOffset.Y, layoutOffset.Z) * (_zoom * (focused == null ? 1 : .87f));
        _camera.transform.localPosition = _look + offset;
        _camera.transform.LookAt(Origin + _look);
    }
    public void SetFocus(string id) => _focus = id;
    public void ResetView() { _pan = Vector2.zero; _zoom = 1; }
    public void HandleViewInput(Event e)
    {
        if (e.type == EventType.ScrollWheel) { _zoom = Mathf.Clamp(_zoom + e.delta.y * .035f, .72f, 1.3f); e.Use(); }
        else if (e.type == EventType.MouseDrag && e.button == 2)
        { _pan.x = Mathf.Clamp(_pan.x - e.delta.x * .05f, -12, 12); _pan.y = Mathf.Clamp(_pan.y + e.delta.y * .05f, -8, 8); e.Use(); }
    }
    public string Pick(Vector2 point)
    {
        if (!Ready) return null;
        var ray = _camera.ViewportPointToRay(new Vector3(point.x / 1920, 1 - point.y / 1080, 0));
        string id = null; float nearest = float.MaxValue;
        foreach (var building in _buildings)
            foreach (var bounds in building.HitBounds)
                if (bounds.IntersectRay(ray, out float distance) && distance < nearest) { nearest = distance; id = building.Id; }
        return id;
    }
    public Vector2 Anchor(string id)
    {
        var b = _buildings.FirstOrDefault(x => x.Id == id);
        if (b == null || _camera == null) return Vector2.zero;
        var p = _camera.WorldToViewportPoint(new Vector3(b.Bounds.center.x, b.Bounds.max.y, b.Bounds.center.z));
        return new Vector2(p.x * 1920, (1 - p.y) * 1080);
    }
    internal Vector2? VisiblePoint(string id)
    {
        var b = _buildings.FirstOrDefault(x => x.Id == id);
        if (b == null || !Ready) return null;
        foreach (var bounds in b.HitBounds)
        {
            var p = _camera.WorldToViewportPoint(bounds.center);
            var point = new Vector2(p.x * 1920, (1 - p.y) * 1080);
            if (point.x > 120 && point.x < 1540 && point.y > 180 && point.y < 940 && Pick(point) == id) return point;
        }
        return null;
    }
    public void Hover(string id) { if (_hover == id) return; _hover = id; Feedback(); }
    public void SetAvailable(string id, bool value)
    {
        var b = _buildings.FirstOrDefault(x => x.Id == id);
        if (b == null || b.Available == value) return;
        b.Available = value; Feedback();
    }
    private void Feedback()
    {
        foreach (var b in _buildings) foreach (var renderer in b.Renderers)
        {
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            float factor = !b.Available ? .38f : b.Id == _hover ? 1.3f : 1;
            var material = renderer.sharedMaterial;
            if (material != null && material.HasProperty(Tint)) block.SetColor(Tint, _baseTints[renderer] * factor);
            else if (material != null && material.HasProperty("_Color")) block.SetColor("_Color", _baseTints[renderer] * factor);
            renderer.SetPropertyBlock(block);
        }
    }
    private void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera) return;
        _savedFog = Shader.GetGlobalFloat(Fog); _savedUnityFog = RenderSettings.fog;
        _savedAmbient = Shader.GetGlobalColor(Ambient); _savedAmbientNoShadow = Shader.GetGlobalColor(AmbientNoShadow);
        Shader.SetGlobalFloat(Fog, 0); RenderSettings.fog = false;
        Shader.SetGlobalColor(Ambient, new Color(1.4f, 1.3f, 1.2f));
        Shader.SetGlobalColor(AmbientNoShadow, new Color(1.4f, 1.3f, 1.2f));
    }
    private void AfterCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera) return;
        Shader.SetGlobalFloat(Fog, _savedFog); RenderSettings.fog = _savedUnityFog;
        Shader.SetGlobalColor(Ambient, _savedAmbient); Shader.SetGlobalColor(AmbientNoShadow, _savedAmbientNoShadow);
        _rendered = true;
    }
    private void Fail(string reason)
    {
        Plugin.Log.LogWarning("[hamlet 3d] " + reason + "; retaining painted town");
        Clear(); Failure = reason;
    }
    internal void Clear()
    {
        if (_camera != null) _camera.enabled = false;
        if (_root != null) { _root.gameObject.SetActive(false); Destroy(_root.gameObject); }
        if (_texture != null) { _texture.Release(); Destroy(_texture); }
        if (_groundMesh != null) Destroy(_groundMesh);
        if (_groundMaterial != null) Destroy(_groundMaterial);
        foreach (var detail in _details) if (detail != null) { if (detail is RenderTexture rt) rt.Release(); Destroy(detail); }
        _details.Clear();
        _gradedMaterials.Clear(); _gradedPaint.Clear();
        _smokeMaterial = null;
        foreach (var handle in _assets.Values) if (handle.IsValid()) Addressables.Release(handle);
        if (_terrainAsset.IsValid()) Addressables.Release(_terrainAsset);
        if (_earthAsset.IsValid()) Addressables.Release(_earthAsset);
        if (_rockAsset.IsValid()) Addressables.Release(_rockAsset);
        _terrainAsset = default; _earthAsset = default; _rockAsset = default;
        _assets.Clear(); _buildings.Clear(); _baseTints.Clear(); _materials.Clear();
        ClearModels();
        _root = null; _camera = null; _texture = null; _groundMesh = null; _groundMaterial = null;
        _nativeView = null; _nativePipeline = null; _nativeFormat = GraphicsFormat.None;
        _piece = 0; _rendered = false; _focus = null; _hover = null;
    }
}
