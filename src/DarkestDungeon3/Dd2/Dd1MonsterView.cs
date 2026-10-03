using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Code.Combat;
using Assets.Code.Combat.Events;
using Assets.Code.Events;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>
/// DD1's own monsters in DD2's fights. DD2 still runs the battle with the look-alike enemies from data/monsters.json;
/// each one's 3D model is hidden and DD1's Spine animation of the monster it stands in for is drawn in its place,
/// posed every frame (Core's SpineSkeleton.Pose) and drawn with GL on top of the scene: the idle loop, DD1's held
/// attack pose when it acts, the defend pose when it is hit, and its death.
/// </summary>
internal static class Dd1MonsterView
{
    private const float AttackSeconds = 1.6f, DefendSeconds = 1.1f;

    /// <summary>One DD1 animation file (monsters/&lt;family&gt;/anim/&lt;family&gt;.sprite.&lt;anim&gt;.skel) with its pages.</summary>
    private sealed class Rig
    {
        public SpineSkeleton Skeleton;
        public SpineAtlas Atlas;
        public readonly Dictionary<SpineAtlas.Page, Texture2D> Pages = new();
        public float Height;   // of the first pose, in skeleton units
    }

    private sealed class Monster
    {
        public string Dd1;          // e.g. skeleton_arbalist_B
        public string Family;       // skeleton_arbalist
        public char Tier;
        public string Dd2Class;
        public uint Guid;
        public CombatActorBhv Actor;
        public Renderer[] Renderers;
        public bool Hidden;
        public string Anim = "combat";
        public string Clip;         // the animation inside the file (combat, attack_x, defend, death)
        public float Since;
        public bool Loop = true, Dead, Gone;
        public readonly Dictionary<string, string> AttackFor = new();   // DD2 skill -> DD1 attack file
        public List<string> Attacks;
        public float NextRendererScan;
        public readonly HashSet<string> Logged = new();
    }

    private static readonly Dictionary<string, Rig> Rigs = new();
    private static readonly List<Monster> Line = new();
    private static bool _bound, _listening;
    private static float _boundTimeout;
    private static Material _material;
    private static bool _failed;

    /// <summary>The fight about to start: DD1 monsters (front first) and the DD2 enemies standing in for them.</summary>
    public static void Prepare(IReadOnlyList<string> dd1Monsters, IReadOnlyList<string> dd2Enemies)
    {
        Clear();
        if (!Plugin.Dd1MonsterArt.Value || dd1Monsters == null || dd2Enemies == null) return;
        for (int i = 0; i < dd1Monsters.Count && i < dd2Enemies.Count; i++)
        {
            var (family, tier) = Dd1Bestiary.Split(dd1Monsters[i]);
            if (Load(family, "combat") == null) continue;
            Line.Add(new Monster { Dd1 = dd1Monsters[i], Family = family, Tier = tier, Dd2Class = dd2Enemies[i] });
        }
        if (Line.Count == 0) return;
        _boundTimeout = Time.unscaledTime + 8f;
        EventManager.AddListener<EventCombatSkillPresentation>(OnSkill);
        EventManager.AddListener<EventCombatPresentationSkillTarget>(OnTarget);
        EventManager.AddListener<EventCombatActorDeath>(OnDeath);
        _listening = true;
        Plugin.Log.LogInfo($"[dd1art] {Line.Count} DD1 monsters to draw: {string.Join(", ", Line.Select(m => m.Dd1 + " as " + m.Dd2Class))}");
    }

    /// <summary>The fight is over: give DD2 its models back.</summary>
    public static void Clear()
    {
        foreach (var m in Line) Show(m);
        Line.Clear();
        _bound = false;
        if (!_listening) return;
        EventManager.RemoveListener<EventCombatSkillPresentation>(OnSkill);
        EventManager.RemoveListener<EventCombatPresentationSkillTarget>(OnTarget);
        EventManager.RemoveListener<EventCombatActorDeath>(OnDeath);
        _listening = false;
    }

    // ---- DD2's fight → DD1 poses ----

    private static Monster ByGuid(uint guid) => Line.FirstOrDefault(m => m.Actor != null && m.Guid == guid);

    private static void Play(Monster m, string file, bool loop)
    {
        var rig = Load(m.Family, file);
        if (rig == null) return;
        m.Anim = file;
        m.Clip = file == "dead" || (file == "defend" && rig.Skeleton.Animation("defend") == null) ? "death" : file;
        if (rig.Skeleton.Animation(m.Clip) == null) m.Clip = rig.Skeleton.Animations.FirstOrDefault()?.Name;
        m.Loop = loop;
        m.Since = Time.unscaledTime;
    }

    private static void OnSkill(EventCombatSkillPresentation e)
    {
        try
        {
            var data = e.m_SkillPresentationData;
            var m = data == null ? null : ByGuid(data.PerformerGuid);
            if (m == null || m.Dead) return;
            m.Attacks ??= AttackFiles(m.Family);
            if (m.Attacks.Count == 0) return;
            string skill = data.SkillId ?? "";
            if (!m.AttackFor.TryGetValue(skill, out var attack))
                m.AttackFor[skill] = attack = m.Attacks[m.AttackFor.Count % m.Attacks.Count];
            Play(m, attack, loop: false);
        }
        catch (Exception ex) { Plugin.Log.LogWarning("[dd1art] skill: " + ex.Message); }
    }

    private static void OnTarget(EventCombatPresentationSkillTarget e)
    {
        try
        {
            var m = e.m_Actor == null ? null : ByGuid(e.m_Actor.GetActorGuid());
            if (m == null || m.Dead || e.m_PerformerGuid == m.Guid) return;
            Play(m, "defend", loop: false);
        }
        catch (Exception ex) { Plugin.Log.LogWarning("[dd1art] target: " + ex.Message); }
    }

    private static void OnDeath(EventCombatActorDeath e)
    {
        try
        {
            var m = e.m_combatActor == null ? null : ByGuid(e.m_combatActor.GetActorGuid());
            if (m == null) return;
            m.Dead = true;
            Play(m, Load(m.Family, "dead") != null ? "dead" : "defend", loop: false);
        }
        catch (Exception ex) { Plugin.Log.LogWarning("[dd1art] death: " + ex.Message); }
    }

    private static List<string> AttackFiles(string family)
    {
        string dir = Session.Current?.Dd1.PathOf("monsters", family, "anim");
        if (dir == null || !Directory.Exists(dir)) return new List<string>();
        return Directory.GetFiles(dir, family + ".sprite.attack_*.skel")
            .Select(f => Path.GetFileName(f).Substring(family.Length + ".sprite.".Length).Replace(".skel", ""))
            .OrderBy(a => a).ToList();
    }

    // ---- finding DD2's stand-ins ----

    private static void Bind()
    {
        var party = new HashSet<uint>(Runtime.Driver.Instance?.Party?.Guids ?? Enumerable.Empty<uint>());
        var actors = UnityEngine.Object.FindObjectsOfType<CombatActorBhv>()
            .Where(a => a != null && a.ActorInstance != null && !party.Contains(a.GetActorGuid()))
            .OrderBy(a => a.ActorInstance.TeamPosition).ToList();
        var used = new HashSet<CombatActorBhv>();
        int found = 0;
        foreach (var m in Line)
        {
            if (m.Actor != null) { found++; continue; }
            var actor = actors.FirstOrDefault(a => !used.Contains(a) && a.ActorInstance.ActorDataClass?.Id == m.Dd2Class && Line.All(o => o.Actor != a));
            if (actor == null) continue;
            used.Add(actor);
            m.Actor = actor;
            m.Guid = actor.GetActorGuid();
            m.Renderers = ModelRenderers(actor);
            found++;
        }
        if (found == Line.Count || Time.unscaledTime > _boundTimeout)
        {
            _bound = true;
            Plugin.Log.LogInfo($"[dd1art] bound {found}/{Line.Count} DD2 enemies");
        }
    }

    /// <summary>The stand-in's model: its mesh renderers (its parts load a little after the actor appears).</summary>
    private static Renderer[] ModelRenderers(CombatActorBhv actor) =>
        actor.GetComponentsInChildren<Renderer>(true).Where(r => r is SkinnedMeshRenderer || r is MeshRenderer).ToArray();

    private static void Note(Monster m, string reason)
    {
        if (m.Logged.Add(reason)) Plugin.Log.LogInfo($"[dd1art] {m.Dd1}: {reason}");
    }

    /// <summary>The camera filming the fight: the main one, else the deepest enabled camera that sees characters.</summary>
    private static Camera FightCamera()
    {
        var main = Camera.main;
        if (main != null && main.enabled) return main;
        int characters = LayerMask.NameToLayer("Characters");
        return Camera.allCameras.Where(c => c.enabled && c.targetTexture == null && (characters < 0 || (c.cullingMask & (1 << characters)) != 0))
                     .OrderByDescending(c => c.depth).FirstOrDefault();
    }

    private static void Hide(Monster m)
    {
        if (m.Hidden || m.Renderers == null) return;
        foreach (var r in m.Renderers) if (r != null) r.forceRenderingOff = true;
        m.Hidden = true;
    }

    private static void Show(Monster m)
    {
        if (!m.Hidden || m.Renderers == null) return;
        foreach (var r in m.Renderers) if (r != null) r.forceRenderingOff = false;
        m.Hidden = false;
    }

    // ---- drawing (from UiRoot.OnGUI while fighting) ----

    public static void Draw()
    {
        if (Line.Count == 0 || _failed || Event.current.type != EventType.Repaint) return;
        try
        {
            if (!_bound) Bind();
            var cam = FightCamera();
            if (cam == null) { if (Line.Count > 0) Note(Line[0], "no camera to place it with"); return; }
            float torch = Mathf.Clamp01(Dd2Api.Torch / 100f);
            float light = Mathf.Lerp(0.6f, 1f, torch);
            foreach (var m in Line.OrderBy(x => x.Actor != null ? -x.Actor.ActorInstance.TeamPosition : 0))
            {
                if (m.Actor == null || m.Gone) continue;
                if (m.Actor.Equals(null)) { m.Gone = true; continue; }
                DrawOne(m, cam, light);
            }
        }
        catch (Exception e)
        {
            _failed = true;
            foreach (var m in Line) Show(m);
            Plugin.Log.LogError("[dd1art] drawing failed, DD2 models restored: " + e);
        }
    }

    private static void DrawOne(Monster m, Camera cam, float light)
    {
        // Where DD2 draws the stand-in: the bottom and top of its model, on screen.
        var bounds = new Bounds();
        bool any = false;
        foreach (var r in m.Renderers ?? new Renderer[0])
        {
            if (r == null || !r.gameObject.activeInHierarchy) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        if (!any)
        {
            // Its model parts may still be loading: look again now and then.
            if (Time.unscaledTime >= m.NextRendererScan)
            {
                m.NextRendererScan = Time.unscaledTime + 0.5f;
                m.Renderers = ModelRenderers(m.Actor);
                m.Hidden = false;
            }
            Note(m, $"no model renderers yet ({m.Renderers?.Length ?? 0} found)");
            return;
        }
        var feet = cam.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        var head = cam.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
        if (feet.z <= 0) { Note(m, "behind the camera " + cam.name); return; }

        // Back to the idle loop when a held pose is done; the dead stay down until DD2 removes them.
        float t = Time.unscaledTime - m.Since;
        if (!m.Loop && !m.Dead && t > (m.Anim == "defend" ? DefendSeconds : AttackSeconds)) { Play(m, "combat", loop: true); t = 0; }
        var rig = Load(m.Family, m.Anim) ?? Load(m.Family, "combat");
        if (rig == null) { Note(m, "no DD1 animation files"); return; }
        if (m.Clip == null) m.Clip = rig.Skeleton.Animation("combat")?.Name ?? rig.Skeleton.Animations.FirstOrDefault()?.Name;
        string skin = rig.Skeleton.Skins.Keys.FirstOrDefault(k => string.Equals(k, m.Tier.ToString(), StringComparison.OrdinalIgnoreCase));
        var pieces = rig.Skeleton.Pose(rig.Atlas, m.Clip, t, m.Loop, skin: skin);
        if (pieces.Count == 0) { Note(m, $"empty pose ({m.Anim}/{m.Clip})"); return; }

        var idle = Load(m.Family, "combat") ?? rig;
        float scale = Mathf.Abs(head.y - feet.y) / Mathf.Max(1f, idle.Height) * Plugin.Dd1MonsterScale.Value;
        if (!DrawPieces(rig, pieces, feet.x, feet.y, scale, flipX: true, light)) { Note(m, "no atlas page textures"); return; }
        Note(m, $"drawn over {m.Dd2Class} with {cam.name} at ({feet.x:0},{feet.y:0}), {Mathf.Abs(head.y - feet.y):0} px tall");
        Hide(m);   // only once DD1's art is really on screen
    }

    private static bool DrawPieces(Rig rig, List<SpineSkeleton.Piece> pieces, float x0, float y0, float scale, bool flipX, float light)
    {
        var mat = Material();
        if (mat == null) return false;
        GL.PushMatrix();
        GL.LoadPixelMatrix();
        Texture2D current = null;
        bool open = false;
        foreach (var p in pieces)
        {
            if (!rig.Pages.TryGetValue(p.Page, out var tex) || tex == null) continue;
            if (tex != current)
            {
                if (open) GL.End();
                mat.mainTexture = tex;
                mat.SetPass(0);
                GL.Begin(GL.TRIANGLES);
                open = true;
                current = tex;
            }
            uint c = p.Color;
            float a = ((c & 0xff) / 255f) * (p.Additive ? 0.6f : 1f);
            var color = new Color(((c >> 24) & 0xff) / 255f * light, ((c >> 16) & 0xff) / 255f * light, ((c >> 8) & 0xff) / 255f * light, a);
            for (int i = 0; i < p.Triangles.Length; i++)
            {
                int v = p.Triangles[i];
                GL.Color(color);
                GL.TexCoord2(p.PagePixels[v * 2] / tex.width, 1f - p.PagePixels[v * 2 + 1] / tex.height);
                float x = p.Positions[v * 2] * scale;
                GL.Vertex3(x0 + (flipX ? -x : x), y0 + p.Positions[v * 2 + 1] * scale, 0);
            }
        }
        if (open) GL.End();
        GL.PopMatrix();
        return current != null;
    }

    private static Material Material()
    {
        if (_material != null) return _material;
        var shader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        if (shader == null) { Plugin.Log.LogError("[dd1art] no shader to draw with"); _failed = true; return null; }
        _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        Plugin.Log.LogInfo("[dd1art] drawing with " + shader.name);
        return _material;
    }

    // ---- DD1 files ----

    private static Rig Load(string family, string anim)
    {
        string key = family + "/" + anim;
        if (Rigs.TryGetValue(key, out var rig)) return rig;
        Rigs[key] = null;
        try
        {
            string dir = Session.Current?.Dd1.PathOf("monsters", family, "anim");
            if (dir == null) return null;
            string skel = Path.Combine(dir, $"{family}.sprite.{anim}.skel"), atlas = Path.Combine(dir, $"{family}.sprite.{anim}.atlas");
            if (!File.Exists(skel) || !File.Exists(atlas)) return null;
            rig = new Rig { Skeleton = SpineSkeleton.Load(skel), Atlas = SpineAtlas.Parse(File.ReadAllText(atlas)) };
            foreach (var page in rig.Atlas.Pages)
            {
                string png = Path.Combine(dir, page.File);
                if (!File.Exists(png)) continue;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                if (tex.LoadImage(File.ReadAllBytes(png), markNonReadable: true)) rig.Pages[page] = tex;
            }
            var b = SpineSkeleton.Bounds(rig.Skeleton.Pose(rig.Atlas, null, 0f));
            rig.Height = b.maxY - b.minY;
            Rigs[key] = rig;
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[dd1art] {key}: {e.Message}"); }
        return rig;
    }
}
