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
        public List<(string Anim, string Fx, string TargetFx)> Skills;   // from DD1's .art.darkest
        public string DeathFx;
        public float Scale = 1f;
        public Vector3 Feet;   // last screen position of its feet (GL pixels)
    }

    private static readonly Dictionary<string, Rig> Rigs = new();

    /// <summary>A one-shot DD1 effect (a skill's flash, a hit on a hero, a death) drawn where it happens.</summary>
    private sealed class Effect
    {
        public Rig Rig;
        public CombatActorBhv Anchor;   // follows this actor (chest), or stays at Fixed
        public Vector3 Fixed;
        public float Since, Scale;
        public bool Flip;
    }

    private static readonly List<Effect> Effects = new();
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
            var monster = new Monster { Dd1 = dd1Monsters[i], Family = family, Tier = tier, Dd2Class = dd2Enemies[i] };
            ReadArt(monster);
            Line.Add(monster);
        }
        if (Line.Count == 0) return;
        _boundTimeout = Time.unscaledTime + 8f;
        EventManager.AddListener<EventCombatSkillPresentation>(OnSkill);
        EventManager.AddListener<EventCombatPresentationSkillTarget>(OnTarget);
        EventManager.AddListener<EventCombatActorDeath>(OnDeath);
        _listening = true;
        Plugin.Log.LogInfo($"[dd1art] {Line.Count} DD1 monsters to draw: {string.Join(", ", Line.Select(m => m.Dd1 + " as " + m.Dd2Class))}");
    }

    /// <summary>DD1's skill list for the monster: each skill's attack pose, its own effect and the effect on its target.</summary>
    private static void ReadArt(Monster m)
    {
        m.Skills = new List<(string, string, string)>();
        try
        {
            string tierName = $"{m.Family}_{m.Tier}";
            string file = Session.Current?.Dd1.PathOf("monsters", m.Family, tierName, tierName + ".art.darkest");
            if (file == null || !File.Exists(file)) return;
            foreach (var r in Core.Dd1.DarkestFile.Load(file))
            {
                if (r.Type == "skill") m.Skills.Add((r.Str("anim", null), r.Str("fx", null), r.Str("targchestfx", null)));
                else if (r.Type == "commonfx") m.DeathFx = r.Str("deathfx", null);
            }
        }
        catch (Exception e) { Plugin.Log.LogInfo($"[dd1art] {m.Dd1} art: {e.Message}"); }
    }

    /// <summary>The fight is over: give DD2 its models back.</summary>
    public static void Clear()
    {
        Effects.Clear();
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
            var skills = m.Skills is { Count: > 0 } ? m.Skills : m.Attacks.Select(a => (Anim: a, Fx: (string)null, TargetFx: (string)null)).ToList();
            if (skills.Count == 0) return;
            string skill = data.SkillId ?? "";
            // Each DD2 skill the stand-in uses gets one of the DD1 monster's skills, in order.
            if (!m.AttackFor.TryGetValue(skill, out var pick))
                m.AttackFor[skill] = pick = (m.AttackFor.Count % skills.Count).ToString();
            var dd1 = skills[int.Parse(pick) % skills.Count];
            Play(m, dd1.Anim ?? "combat", loop: false);
            if (dd1.Fx != null && Fx(m.Family, dd1.Fx) is { } fx)
                Effects.Add(new Effect { Rig = fx, Anchor = m.Actor, Since = Time.unscaledTime, Scale = m.Scale, Flip = true, Fixed = m.Feet });
            if (dd1.TargetFx != null && Fx(m.Family, dd1.TargetFx) is { } hit)
                foreach (var target in data.TargetActors ?? (IReadOnlyList<CombatActorBhv>)new CombatActorBhv[0])
                    if (target != null) Effects.Add(new Effect { Rig = hit, Anchor = target, Since = Time.unscaledTime + 0.35f, Scale = m.Scale, Flip = true });
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
            if (m.DeathFx != null && Fx(m.Family, m.DeathFx) is { } death)
                Effects.Add(new Effect { Rig = death, Fixed = m.Feet, Since = Time.unscaledTime, Scale = m.Scale, Flip = true });
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
        // Once per kind of note (its first word): positions change every frame.
        if (m.Logged.Add(reason.Split(' ')[0])) Plugin.Log.LogInfo($"[dd1art] {m.Dd1}: {reason}");
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

    // ---- drawing: posed into a screen-sized texture after DD2 has moved things (LateUpdate), shown by OnGUI ----

    private static RenderTexture _screen;
    private static bool _drawn;

    /// <summary>OnGUI: show what <see cref="Render"/> drew this frame.</summary>
    public static void Draw()
    {
        if (!_drawn || _screen == null || Event.current.type != EventType.Repaint) return;
        GUI.DrawTexture(new Rect(0, 0, Ui.Gui.W, Ui.Gui.H), _screen);
    }

    /// <summary>LateUpdate while fighting: pose every DD1 monster and effect into the screen texture.</summary>
    public static void Render()
    {
        _drawn = false;
        if (Line.Count == 0 || _failed) return;
        var prev = RenderTexture.active;
        bool pushed = false;
        try
        {
            if (!_bound) Bind();
            var cam = FightCamera();
            if (cam == null) { Note(Line[0], "no camera to place it with"); return; }
            if (_screen == null || _screen.width != Screen.width || _screen.height != Screen.height)
            {
                if (_screen != null) { _screen.Release(); UnityEngine.Object.Destroy(_screen); }
                _screen = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32) { name = "DD3Monsters" };
                _screen.Create();
            }
            RenderTexture.active = _screen;
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            GL.PushMatrix();
            pushed = true;
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);   // top-left origin, like the backdrop
            float torch = Mathf.Clamp01(Dd2Api.Torch / 100f);
            float light = Mathf.Lerp(0.6f, 1f, torch);
            foreach (var m in Line.OrderBy(x => x.Actor != null ? -x.Actor.ActorInstance.TeamPosition : 0))
            {
                if (m.Actor == null || m.Gone) continue;
                if (m.Actor.Equals(null)) { m.Gone = true; continue; }
                if (DrawOne(m, cam, light)) _drawn = true;
            }
            if (DrawEffects(cam, light)) _drawn = true;
        }
        catch (Exception e)
        {
            _failed = true;
            foreach (var m in Line) Show(m);
            Plugin.Log.LogError("[dd1art] drawing failed, DD2 models restored: " + e);
        }
        finally
        {
            if (pushed) GL.PopMatrix();
            RenderTexture.active = prev;
        }
    }

    /// <summary>The stand-in's body: its biggest skinned mesh (shadows, effects and props left out).</summary>
    private static Renderer Body(Monster m) =>
        (m.Renderers ?? new Renderer[0]).Where(r => r != null && r.gameObject.activeInHierarchy)
            .OrderByDescending(r => r is SkinnedMeshRenderer ? 1 : 0)
            .ThenByDescending(r => r.bounds.size.x * r.bounds.size.y)
            .FirstOrDefault();

    private static bool DrawOne(Monster m, Camera cam, float light)
    {
        // Where DD2 draws the stand-in: the bottom and top of its body, on screen.
        var body = Body(m);
        if (body == null)
        {
            // Its model parts may still be loading: look again now and then.
            if (Time.unscaledTime >= m.NextRendererScan)
            {
                m.NextRendererScan = Time.unscaledTime + 0.5f;
                m.Renderers = ModelRenderers(m.Actor);
                m.Hidden = false;
            }
            Note(m, "no model renderers yet");
            return false;
        }
        var bounds = body.bounds;
        var feet = cam.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        var head = cam.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
        if (feet.z <= 0) { Note(m, "behind the camera " + cam.name); return false; }

        // Back to the idle loop when a held pose is done; the dead stay down until DD2 removes them.
        float t = Time.unscaledTime - m.Since;
        if (!m.Loop && !m.Dead && t > (m.Anim == "defend" ? DefendSeconds : AttackSeconds)) { Play(m, "combat", loop: true); t = 0; }
        var rig = Load(m.Family, m.Anim) ?? Load(m.Family, "combat");
        if (rig == null) { Note(m, "no DD1 animation files"); return false; }
        if (m.Clip == null) m.Clip = rig.Skeleton.Animation("combat")?.Name ?? rig.Skeleton.Animations.FirstOrDefault()?.Name;
        string skin = rig.Skeleton.Skins.Keys.FirstOrDefault(k => string.Equals(k, m.Tier.ToString(), StringComparison.OrdinalIgnoreCase));
        var pieces = rig.Skeleton.Pose(rig.Atlas, m.Clip, t, m.Loop, skin: skin);
        if (pieces.Count == 0) { Note(m, $"empty pose ({m.Anim}/{m.Clip})"); return false; }

        // As tall as the DD2 body it replaces (DD1's height measured without stray far-off parts).
        var idle = Load(m.Family, "combat") ?? rig;
        float bodyPx = Mathf.Abs(head.y - feet.y);
        float scale = bodyPx / Mathf.Max(1f, idle.Height) * Plugin.Dd1MonsterScale.Value;
        m.Scale = scale;
        m.Feet = feet;
        if (!DrawPieces(rig, pieces, feet.x, Screen.height - feet.y, scale, flipX: true, light)) { Note(m, "no atlas page textures"); return false; }
        Note(m, $"drawn, over {m.Dd2Class} with {cam.name} at ({feet.x:0},{feet.y:0}): DD2 body {bodyPx:0} px, DD1 height {idle.Height:0}, scale {scale:0.00}");
        Hide(m);   // only once DD1's art is really on screen
        return true;
    }

    private static bool DrawEffects(Camera cam, float light)
    {
        bool any = false;
        float now = Time.unscaledTime;
        for (int i = Effects.Count - 1; i >= 0; i--)
        {
            var e = Effects[i];
            float t = now - e.Since;
            if (t < 0) continue;
            var anim = e.Rig.Skeleton.Animations.FirstOrDefault();
            float duration = Mathf.Max(0.4f, anim?.Duration ?? 0.6f);
            if (t > duration) { Effects.RemoveAt(i); continue; }
            Vector3 at = e.Fixed;
            if (e.Anchor != null && !e.Anchor.Equals(null))
            {
                var rs = e.Anchor.GetComponentsInChildren<Renderer>().Where(r => r is SkinnedMeshRenderer or MeshRenderer).ToList();
                if (rs.Count > 0)
                {
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    at = cam.WorldToScreenPoint(new Vector3(b.center.x, b.min.y, b.center.z));
                }
            }
            if (at.z <= 0) continue;
            var pieces = e.Rig.Skeleton.Pose(e.Rig.Atlas, anim?.Name, t, loop: false);
            if (DrawPieces(e.Rig, pieces, at.x, Screen.height - at.y, e.Scale, e.Flip, light)) any = true;
        }
        return any;
    }

    /// <summary>A DD1 effect: the monster's own (monsters/&lt;family&gt;/fx) or a shared one (fx/&lt;name&gt;).</summary>
    private static Rig Fx(string family, string name)
    {
        var dd1 = Session.Current?.Dd1;
        if (dd1 == null) return null;
        return LoadFile(dd1.PathOf("monsters", family, "fx"), $"{family}.sprite.{name}")
               ?? LoadFile(dd1.PathOf("fx", name), $"{name}.sprite");
    }

    private static bool DrawPieces(Rig rig, List<SpineSkeleton.Piece> pieces, float x0, float y0, float scale, bool flipX, float light)
    {
        var mat = Material();
        if (mat == null) return false;
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
                GL.Vertex3(x0 + (flipX ? -x : x), y0 - p.Positions[v * 2 + 1] * scale, 0);
            }
        }
        if (open) GL.End();
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
        var dd1 = Session.Current?.Dd1;
        return dd1 == null ? null : LoadFile(dd1.PathOf("monsters", family, "anim"), $"{family}.sprite.{anim}");
    }

    /// <summary>A DD1 Spine file (&lt;dir&gt;/&lt;stem&gt;.skel + .atlas + pages), cached.</summary>
    private static Rig LoadFile(string dir, string stem)
    {
        string key = dir + "/" + stem;
        if (Rigs.TryGetValue(key, out var rig)) return rig;
        Rigs[key] = null;
        try
        {
            if (dir == null) return null;
            string skel = Path.Combine(dir, stem + ".skel"), atlas = Path.Combine(dir, stem + ".atlas");
            if (!File.Exists(skel) || !File.Exists(atlas)) return null;
            rig = new Rig { Skeleton = SpineSkeleton.Load(skel), Atlas = SpineAtlas.Parse(File.ReadAllText(atlas)) };
            foreach (var page in rig.Atlas.Pages)
            {
                string png = Path.Combine(dir, page.File);
                if (!File.Exists(png)) continue;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                if (tex.LoadImage(File.ReadAllBytes(png), markNonReadable: true)) rig.Pages[page] = tex;
            }
            var ys = rig.Skeleton.Pose(rig.Atlas, null, 0f).SelectMany(p => Enumerable.Range(0, p.Positions.Length / 2).Select(k => p.Positions[k * 2 + 1])).OrderBy(y => y).ToList();
            int hi = Math.Min(ys.Count - 1, (int)(ys.Count * 0.98f)), lo = (int)(ys.Count * 0.02f);
            rig.Height = ys.Count == 0 ? 1f : Math.Max(1f, ys[hi] - Math.Min(0f, ys[lo]));
            Rigs[key] = rig;
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[dd1art] {key}: {e.Message}"); }
        return rig;
    }
}
