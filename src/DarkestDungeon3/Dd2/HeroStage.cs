using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Utils;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD2's real, animated hero models for the DD1 corridor: the party is spawned far away from everything and filmed
/// by its own camera into a render texture, which the crawl screen draws where DD1 stands its heroes. Same actor
/// creation call DD2's results screen uses (ActorCreateGameObjectBhv). The heroes stay on DD2's "Characters" layer:
/// DD2's deferred pass only lights renderers on the layers of its DeferredRenderFeature mask (a private layer came
/// out black). If they still render black, the stage notices and the crawl falls back to DD2's flat hero art.
/// </summary>
[DefaultExecutionOrder(10000)]
internal sealed class HeroStage : MonoBehaviour
{
    public static HeroStage Instance { get; private set; }

    private static int _layer = -1;
    private static int Layer => _layer >= 0 ? _layer : _layer = LayerMask.NameToLayer("Characters") is var l && l >= 0 ? l : 31;

    /// <summary>The heroes are lit and shown (the search for a camera setup that lights them succeeded).</summary>
    public static bool Lit => Instance != null && Instance._checked && !RendersBlack && Instance._search == null;

    // The camera setups to try: each URP renderer DD2 has, with DD2's character layer and its UI-character layer.
    private List<(int Renderer, int Layer, bool Post)> _search;
    private readonly List<(int Renderer, int Layer, bool Post, float Brightness)> _results = new();
    private Component _urpData;
    private System.Reflection.MethodInfo _setRenderer;
    private int _mainRenderer = -1;

    /// <summary>The layers DD2's deferred lighting draws and lights (its DeferredRenderFeature mask).</summary>
    private static int DeferredMask()
    {
        int mask = 1 << Layer;
        try
        {
            var type = typeof(ActorBhv).Assembly.GetType("Assets.Code.Rendering.RendererFeatures.DeferredRenderFeature");
            var field = type?.GetField("layerMask");
            if (type != null && field != null)
                foreach (var f in Resources.FindObjectsOfTypeAll(type))
                    mask |= ((LayerMask)field.GetValue(f)).value;
        }
        catch (System.Exception e) { Plugin.Log.LogWarning("[stage] deferred mask: " + e.Message); }
        return mask;
    }

    /// <summary>The models came out black (checked once they loaded): the crawl uses DD2's flat art instead.</summary>
    public static bool RendersBlack { get; private set; }
    private float _loadedAt = -1f;
    private bool _checked;
    private const float PixelsPerUnit = 100f;          // 1920x720 virtual pixels = 19.2 x 7.2 world units
    private static readonly Vector3 Origin = new(20000f, 20000f, 0f);

    private Camera _camera;
    private RenderTexture _texture;
    private Transform _root;
    private readonly List<(uint guid, Transform slot, ActorBhv actor)> _heroes = new();
    private string _partyKey;

    public static float HeroScale = 1f;
    /// <summary>Signed corridor speed: 1 forward, -0.5 backing up, 0 stopped.</summary>
    public static float WalkSpeed;
    private readonly Dictionary<ActorBhv, CorridorHeroMotion> _motion = new();
    private readonly HashSet<ActorBhv> _motionUnavailable = new();
    private float _nextMotionBind;
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
        Shader.SetGlobalColor(Ambient, StageAmbient * _exposure);
        Shader.SetGlobalColor(AmbientNoShadow, StageAmbient * _exposure);
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
        int deferredMask = DeferredMask();
        _camera.cullingMask = deferredMask;   // only the heroes are anywhere near this far-away camera
        Plugin.Log.LogInfo($"[stage] hero layer {Layer} ({LayerMask.LayerToName(Layer)}), deferred mask 0x{deferredMask:X}");
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
            _urpData = data;
            _setRenderer = urpData.GetMethod("SetRenderer");
            if (mainData != null && indexField != null)
            {
                int index = (int)indexField.GetValue(mainData);
                _mainRenderer = index;
                _setRenderer?.Invoke(data, new object[] { index });
                Plugin.Log.LogInfo($"[stage] using renderer {index} (from {main.name})");
            }
        }

        _light = new GameObject("DD3HeroLight").AddComponent<Light>();
        _light.transform.SetParent(_root, false);
        _light.transform.rotation = Quaternion.Euler(20f, -10f, 0f);
        _light.type = LightType.Directional;
        _light.color = new Color(1f, 0.93f, 0.85f);
        _light.cullingMask = deferredMask;   // DD2 may apply a light only if its mask equals the deferred mask
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
                // In fights DD2 spawns actors with no starting state and shows them later (Show(): the art's default
                // animator state, the combat stance facing the enemy); see ShowCombatPose. "idle_neutral" is the
                // road/inn pose, turned away from the camera; used if asked, or if the combat pose drew nothing.
                string pose = Plugin.HeroModelPose.Value == "neutral" || _combatPoseFailed ? "idle_neutral" : null;
                actor = creator.CreateActorGameObject(guids[i], slot, Layer, pose, loadSubclasses: false);
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
            keyLight.cullingMask = _light != null ? _light.cullingMask : DeferredMask();
            _keyLights.Add(keyLight);
        }
        Plugin.Log.LogInfo($"[stage] spawning {_heroes.Count} hero models");
        _loadedAt = -1f;
        _checked = false;
        _tries = 0;
        RendersBlack = false;   // a new party gets a fresh try (the exposure found so far is kept)

    }

    public void Clear()
    {
        foreach (var motion in _motion.Values) motion.Restore();
        _motion.Clear();
        _motionUnavailable.Clear();
        WalkSpeed = 0;
        _shown.Clear();
        foreach (var (_, slot, _) in _heroes)
            if (slot != null) Destroy(slot.gameObject);
        _heroes.Clear();
        _keyLights.Clear();
        _partyKey = null;
    }

    public void SetVisible(bool visible)
    {
        if (_camera != null && _camera.enabled != visible) _camera.enabled = visible;
    }

    private float _nextLayerFix;

    private RenderTexture _bright;
    private Material _brighten;

    /// <summary>The models' picture brightened (DD2's per-arena lighting doesn't reach this far-away stage, so they
    /// come out dark; in fights, lit by the arena, they look right). Null if it can't be made.</summary>
    public Texture Brightened => _bright != null && _bright.IsCreated() ? _bright : null;

    private void Brighten()
    {
        if (_texture == null || _camera == null || !_camera.enabled) return;
        if (_brighten == null)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            if (shader == null) return;
            _brighten = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        if (_bright == null) { _bright = new RenderTexture(_texture.width, _texture.height, 0, RenderTextureFormat.ARGB32) { name = "DD3HeroStageBright" }; _bright.Create(); }
        float k = Plugin.HeroModelBrightness.Value;
        _brighten.color = new Color(k, k, k, 1f);
        var prev = RenderTexture.active;
        RenderTexture.active = _bright;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = prev;
        Graphics.Blit(_texture, _bright, _brighten);
    }

    // The combat pose drew nothing once: use the road pose from then on.
    private static bool _combatPoseFailed;
    private readonly HashSet<ActorBhv> _shown = new();

    /// <summary>Spawned with no starting state (the combat pose), a model stays hidden until shown, as DD2 does when
    /// a fight starts: show each hero once it has loaded.</summary>
    private void ShowCombatPose()
    {
        foreach (var (_, _, actor) in _heroes)
        {
            if (actor == null || !actor.IsInitialized || actor.IsLoading || _shown.Contains(actor)) continue;
            try { actor.Show(); }
            catch (System.Exception e) { Plugin.Log.LogWarning("[stage] show: " + e.Message); }
            _shown.Add(actor);
        }
    }

    private void Update()
    {
        foreach (var motion in _motion.Values) motion.Restore();
    }

    private void LateUpdate()
    {
        ShowCombatPose();
        Brighten();
        if (_light != null) _light.intensity = LightIntensity * _exposure;
        foreach (var k in _keyLights) if (k != null) k.intensity = LightIntensity * 2f * _exposure;
        CheckNotBlack();
        bool bindMotion = Time.unscaledTime >= _nextMotionBind;
        for (int i = 0; i < _heroes.Count; i++)
        {
            var slot = _heroes[i].slot;
            if (slot == null) continue;
            var p = slot.localPosition;
            slot.localPosition = new Vector3(p.x, (720f - FeetY) / PixelsPerUnit, p.z);
            var actor = _heroes[i].actor;
            if (actor == null || actor.IsLoading || !_shown.Contains(actor)) continue;
            if (!_motion.TryGetValue(actor, out var motion) && !_motionUnavailable.Contains(actor)
                && _checked && !RendersBlack && bindMotion)
            {
                try
                {
                    motion = CorridorHeroMotion.TryCreate(actor, slot, i);
                    if (motion != null) _motion.Add(actor, motion);
                }
                catch (System.Exception e)
                {
                    _motionUnavailable.Add(actor);
                    Plugin.Log.LogWarning($"[stage-walk] {actor.GetActorGuid()}: {e.Message}; retaining native pose");
                }
            }
            motion?.Apply(WalkSpeed, Time.unscaledDeltaTime, _camera != null && _camera.enabled);
        }
        if (bindMotion) _nextMotionBind = Time.unscaledTime + 0.5f;
        // Actor parts load asynchronously and may arrive on other layers: keep everything on ours.
        if (_root == null || Time.unscaledTime < _nextLayerFix) return;
        _nextLayerFix = Time.unscaledTime + 0.5f;
        foreach (var (_, slot, _) in _heroes)
            if (slot != null)
                foreach (var t in slot.GetComponentsInChildren<Transform>(includeInactive: true))
                    if (t.gameObject.layer != Layer) t.gameObject.layer = Layer;
    }

    private (int Renderer, int Layer, bool Post)? _best;

    /// <summary>DD1 heroes stand about 400 px tall in the 720 px scene: scale each model's body to that.</summary>
    private void FitToDd1()
    {
        foreach (var (_, slot, actor) in _heroes)
        {
            if (slot == null || actor == null) continue;
            var body = actor.GetComponentsInChildren<Renderer>().Where(r => r is SkinnedMeshRenderer)
                            .OrderByDescending(r => r.bounds.size.x * r.bounds.size.y).FirstOrDefault();
            if (body == null) continue;
            float unscaled = body.bounds.size.y / Mathf.Max(0.01f, slot.lossyScale.y);
            float fit = Mathf.Clamp(4.0f / Mathf.Max(0.1f, unscaled), 0.5f, 6f);
            slot.localScale = Vector3.one * HeroScale * fit;
            Plugin.Log.LogInfo($"[stage] hero {actor.GetActorGuid()}: body {unscaled:0.00} units -> scale {fit:0.00}");
        }
    }

    private static int RendererCount()
    {
        try
        {
            var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var field = asset?.GetType().GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(asset) is System.Array list ? list.Length : 0;
        }
        catch (System.Exception) { return 0; }
    }

    private void StartSearch()
    {
        int count = RendererCount();
        var renderers = new List<int>();
        if (_mainRenderer >= 0) renderers.Add(_mainRenderer);
        for (int i = 0; i < count; i++) if (!renderers.Contains(i)) renderers.Add(i);
        if (renderers.Count == 0) renderers.Add(-1);
        var layers = new List<int> { LayerMask.NameToLayer("Characters"), LayerMask.NameToLayer("ForUI") }.Where(l => l >= 0).Distinct().ToList();
        if (layers.Count == 0) layers.Add(Layer);
        // DD2 may rely on its post-processing (tonemapping, exposure) to bring characters up: try with and without.
        _search = renderers.SelectMany(r => layers.SelectMany(l => new[] { (r, l, false), (r, l, true) })).ToList();
        _results.Clear();
        Plugin.Log.LogInfo($"[stage] looking for a camera setup that lights the heroes: {renderers.Count} renderers x {layers.Count} layers");
        Apply(_search[0]);
    }

    /// <summary>Film the heroes with this URP renderer, on this layer (camera and lights follow).</summary>
    private void Apply((int Renderer, int Layer, bool Post) setup)
    {
        if (setup.Renderer >= 0 && _urpData != null) _setRenderer?.Invoke(_urpData, new object[] { setup.Renderer });
        _urpData?.GetType().GetProperty("renderPostProcessing")?.SetValue(_urpData, setup.Post);
        _layer = setup.Layer;
        int mask = DeferredMask() | (1 << setup.Layer);
        if (_camera != null) _camera.cullingMask = mask;
        if (_light != null) _light.cullingMask = mask;
        foreach (var k in _keyLights) if (k != null) k.cullingMask = mask;
        _nextLayerFix = 0f;   // re-layer the heroes now
    }

    private readonly List<Light> _keyLights = new();
    private float _exposure = 1f;
    private int _tries;
    private float _nextCheck;

    /// <summary>
    /// Once the models have loaded, read the picture back now and then and raise the stage's lights and ambient until
    /// the heroes look lit (DD2's own lighting is set up per arena, which this far-away stage doesn't have). Only if
    /// even that stays near black does the crawl fall back to DD2's flat hero art.
    /// </summary>
    private void CheckNotBlack()
    {
        if (_checked || _texture == null || _camera == null || !_camera.enabled) return;
        if (_heroes.Count == 0 || _heroes.Any(h => h.actor == null || h.actor.IsLoading)) { _loadedAt = -1f; return; }
        if (_loadedAt < 0) { _loadedAt = Time.unscaledTime; _nextCheck = _loadedAt + 1f; return; }
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.6f;
        var prev = RenderTexture.active;
        RenderTexture.active = _texture;
        var tex = new Texture2D(_texture.width, _texture.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, _texture.width, _texture.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        var px = tex.GetPixels32();
        Destroy(tex);
        long sum = 0;
        int count = 0, visible = 0;
        for (int i = 0; i < px.Length; i += 7)
        {
            if (px[i].a > 20) visible++;
            if (px[i].a > 200) { sum += px[i].r + px[i].g + px[i].b; count++; }
        }
        float brightness = count == 0 ? 0f : sum / (count * 3f * 255f);
        // DD2's heroes are inked: fully opaque pixels are mostly outlines and shadows, so "dark" says nothing.
        // Show the models as soon as they draw at all; fall back only if nothing drew.
        _checked = true;
        _search = null;
        RendersBlack = visible < 200;
        if (RendersBlack && !_combatPoseFailed && Plugin.HeroModelPose.Value != "neutral")
        {
            // The combat pose drew nothing: spawn the party again in the road pose before giving up on the models.
            _combatPoseFailed = true;
            Plugin.Log.LogInfo($"[stage] hero models: {visible} visible samples in the combat pose -> trying the road pose");
            _partyKey = null;
            _checked = false;
            RendersBlack = false;
            return;
        }
        if (!RendersBlack) FitToDd1();
        Plugin.Log.LogInfo($"[stage] hero models: {visible} visible samples (opaque brightness {brightness:0.000}) -> {(RendersBlack ? "nothing drawn, using DD2's flat art" : "shown")}");
        return;
        if (_search != null)
        {
            var setup = _search[_results.Count];
            // Post-processing may make the whole picture opaque: count only setups that keep the background clear.
            bool clear = count < px.Length / 7 * 0.6f;
            _results.Add((setup.Renderer, setup.Layer, setup.Post, count >= 200 && clear ? brightness : 0f));
            Plugin.Log.LogInfo($"[stage] renderer {setup.Renderer}, layer {LayerMask.LayerToName(setup.Layer)}, post {setup.Post}: brightness {brightness:0.000} ({count} samples{(clear ? "" : ", opaque background")})");
            if (_results.Count < _search.Count) { Apply(_search[_results.Count]); return; }
            var best = _results.OrderByDescending(r => r.Brightness).First();
            _search = null;
            _best = (best.Renderer, best.Layer, best.Post);
            Apply(_best.Value);
            Plugin.Log.LogInfo($"[stage] best: renderer {best.Renderer}, layer {LayerMask.LayerToName(best.Layer)}, post {best.Post} ({best.Brightness:0.000})");
            _nextCheck = Time.unscaledTime + 0.6f;
            return;
        }
        _tries++;
        if (count >= 200 && brightness >= 0.15f)
        {
            _checked = true;
            Plugin.Log.LogInfo($"[stage] hero models lit: brightness {brightness:0.000} at exposure {_exposure:0.0}");
            return;
        }
        if (_tries < 8 && count >= 200 && _exposure < 40f)
        {
            _exposure = Mathf.Min(40f, _exposure * Mathf.Clamp(0.25f / Mathf.Max(brightness, 0.01f), 1.5f, 4f));
            Plugin.Log.LogInfo($"[stage] hero models too dark ({brightness:0.000}), exposure -> {_exposure:0.0}");
            return;
        }
        _checked = true;
        RendersBlack = count < 200 || brightness < 0.05f;
        Plugin.Log.LogInfo($"[stage] hero models: {count} opaque samples, brightness {brightness:0.000} at exposure {_exposure:0.0} -> {(RendersBlack ? "too dark, using DD2's flat art" : "shown")}");
    }
}
