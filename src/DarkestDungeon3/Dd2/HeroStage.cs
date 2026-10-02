using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Utils;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD2's real, animated hero models for the DD1 corridor: the party is spawned far away from everything on a
/// private layer and filmed by its own camera into a render texture, which the crawl screen draws where DD1
/// stands its heroes. Same actor creation call DD2's results screen uses (ActorCreateGameObjectBhv).
/// </summary>
internal sealed class HeroStage : MonoBehaviour
{
    public static HeroStage Instance { get; private set; }

    private const int Layer = 31;
    private const float PixelsPerUnit = 100f;          // 1920x720 virtual pixels = 19.2 x 7.2 world units
    private static readonly Vector3 Origin = new(20000f, 20000f, 0f);

    private Camera _camera;
    private RenderTexture _texture;
    private Transform _root;
    private readonly List<(uint guid, Transform slot, ActorBhv actor)> _heroes = new();
    private string _partyKey;

    public static float HeroScale = 1f;
    public static float FeetY = 680f;

    /// <summary>The rendered party, or null while nothing has loaded.</summary>
    public Texture Texture => _heroes.Any(h => h.actor != null && !h.actor.IsLoading) ? _texture : null;

    private static readonly int FogEnabled = Shader.PropertyToID("_global_fog_enabled");
    // DD2 lights characters with its own tiled lights (computed for the main camera only) plus a global ambient.
    // Our camera sees none of those lights, so it gets its own ambient: DD1's warm torchlight.
    private static readonly int Ambient = Shader.PropertyToID("_GlobalAmbientColor");
    private static readonly int AmbientNoShadow = Shader.PropertyToID("_GlobalAmbientColorWithoutShadowColor");
    private float _savedFog;
    private Color _savedAmbient, _savedAmbientNoShadow;
    private bool _savedUnityFog;
    public static Color StageAmbient = new(1.15f, 1.02f, 0.88f, 1f);

    private void Awake()
    {
        Instance = this;
        // DD2 fogs everything it renders (_global_fog_*); far from any arena the heroes fade to black. Turn the fog
        // off only while our camera renders, and put it back right after.
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    private void OnDestroy()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnEndCamera;
    }

    private void OnBeginCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != _camera) return;
        _savedFog = Shader.GetGlobalFloat(FogEnabled);
        _savedAmbient = Shader.GetGlobalColor(Ambient);
        _savedAmbientNoShadow = Shader.GetGlobalColor(AmbientNoShadow);
        _savedUnityFog = RenderSettings.fog;
        Shader.SetGlobalFloat(FogEnabled, 0f);
        Shader.SetGlobalColor(Ambient, StageAmbient);
        Shader.SetGlobalColor(AmbientNoShadow, StageAmbient);
        RenderSettings.fog = false;
    }

    private void OnEndCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != _camera) return;
        Shader.SetGlobalFloat(FogEnabled, _savedFog);
        Shader.SetGlobalColor(Ambient, _savedAmbient);
        Shader.SetGlobalColor(AmbientNoShadow, _savedAmbientNoShadow);
        RenderSettings.fog = _savedUnityFog;
    }

    private void Ensure()
    {
        if (_root != null) return;
        _root = new GameObject("DD3HeroStage").transform;
        DontDestroyOnLoad(_root.gameObject);
        _root.position = Origin;

        _texture = new RenderTexture(1920, 720, 24, RenderTextureFormat.ARGB32) { name = "DD3HeroStage" };
        var camGo = new GameObject("DD3HeroCamera");
        camGo.transform.SetParent(_root, false);
        camGo.transform.localPosition = new Vector3(19.2f / 2f, 7.2f / 2f, -20f);
        _camera = camGo.AddComponent<Camera>();
        _camera.orthographic = true;
        _camera.orthographicSize = 7.2f / 2f;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0, 0, 0, 0);
        _camera.cullingMask = 1 << Layer;
        _camera.nearClipPlane = 0.1f;
        _camera.farClipPlane = 100f;
        _camera.targetTexture = _texture;

        // URP: no post-processing or anti-aliasing on this camera (they'd touch the alpha we composite with).
        var urpData = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpData != null)
        {
            var data = camGo.GetComponent(urpData) ?? camGo.AddComponent(urpData);
            urpData.GetProperty("renderPostProcessing")?.SetValue(data, false);
            urpData.GetProperty("renderShadows")?.SetValue(data, false);
            // DD2 draws characters with its own deferred renderer; a camera on URP's default renderer gets black
            // heroes. Use whatever renderer DD2's main camera uses.
            var main = Camera.main;
            var mainData = main != null ? main.GetComponent(urpData) : null;
            var indexField = urpData.GetField("m_RendererIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mainData != null && indexField != null)
            {
                int index = (int)indexField.GetValue(mainData);
                urpData.GetMethod("SetRenderer")?.Invoke(data, new object[] { index });
                Plugin.Log.LogInfo($"[stage] using renderer {index} (from {main.name})");
            }
        }

        _light = new GameObject("DD3HeroLight").AddComponent<Light>();
        _light.transform.SetParent(_root, false);
        _light.transform.rotation = Quaternion.Euler(20f, -10f, 0f);
        _light.type = LightType.Directional;
        _light.color = new Color(1f, 0.93f, 0.85f);
        _light.cullingMask = 1 << Layer;
    }

    private Light _light;
    public static float LightIntensity = 2.2f;

    /// <summary>Testing (F7): write the raw render texture to a PNG to inspect colour and alpha.</summary>
    public string Dump(string path)
    {
        if (_texture == null) return "no texture";
        var prev = RenderTexture.active;
        RenderTexture.active = _texture;
        var tex = new Texture2D(_texture.width, _texture.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, _texture.width, _texture.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        // Sample the alpha histogram of non-empty pixels.
        var px = tex.GetPixels32();
        int opaque = px.Count(p => p.a > 240), partial = px.Count(p => p.a > 10 && p.a <= 240);
        Destroy(tex);
        return $"{path}: opaque {opaque}, partial {partial}";
    }

    /// <summary>Show these actors, rank 1 first, at DD1's rank positions. Re-spawns only if the party changed.</summary>
    public void SetParty(IReadOnlyList<uint> guids, IReadOnlyList<float> rankX)
    {
        string key = string.Join(",", guids);
        if (key == _partyKey) return;
        Clear();
        Ensure();
        _partyKey = key;
        var creator = SingletonMonoBehaviour<ActorCreateGameObjectBhv>.Instance;
        if (creator == null) { Plugin.Log.LogWarning("[stage] no ActorCreateGameObjectBhv"); return; }

        for (int i = 0; i < guids.Count && i < rankX.Count; i++)
        {
            var slot = new GameObject("DD3HeroSlot" + i).transform;
            slot.SetParent(_root, false);
            slot.localPosition = new Vector3(rankX[i] / PixelsPerUnit, (720f - FeetY) / PixelsPerUnit, 0f);
            slot.localScale = Vector3.one * HeroScale;
            ActorBhv actor = null;
            try
            {
                actor = creator.CreateActorGameObject(guids[i], slot, Layer, "idle_neutral", loadSubclasses: false);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[stage] actor {guids[i]}: {e.Message}"); }
            _heroes.Add((guids[i], slot, actor));

            // DD2's deferred pass lights characters with point/spot lights: give each hero a warm key light.
            var keyLight = new GameObject("DD3HeroKeyLight").AddComponent<Light>();
            keyLight.transform.SetParent(slot, false);
            keyLight.transform.localPosition = new Vector3(0.6f, 2.2f, -2.5f);
            keyLight.type = LightType.Point;
            keyLight.range = 8f;
            keyLight.intensity = LightIntensity * 2f;
            keyLight.color = new Color(1f, 0.86f, 0.66f);
        }
        Plugin.Log.LogInfo($"[stage] spawning {_heroes.Count} hero models");
    }

    public void Clear()
    {
        foreach (var (_, slot, _) in _heroes)
            if (slot != null) Destroy(slot.gameObject);
        _heroes.Clear();
        _partyKey = null;
    }

    public void SetVisible(bool visible)
    {
        if (_camera != null && _camera.enabled != visible) _camera.enabled = visible;
    }

    private float _nextLayerFix;

    private void LateUpdate()
    {
        if (_light != null) _light.intensity = LightIntensity;
        // Actor parts load asynchronously and may arrive on other layers: keep everything on ours.
        if (_root == null || Time.unscaledTime < _nextLayerFix) return;
        _nextLayerFix = Time.unscaledTime + 0.5f;
        foreach (var (_, slot, _) in _heroes)
            if (slot != null)
                foreach (var t in slot.GetComponentsInChildren<Transform>(includeInactive: true))
                    if (t.gameObject.layer != Layer) t.gameObject.layer = Layer;
    }
}
