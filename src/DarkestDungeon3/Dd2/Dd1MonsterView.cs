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
        public List<(string Id, string Anim, string Fx, string TargetFx)> Skills;   // from DD1's .art.darkest
        public readonly Dictionary<string, int> SkillFor = new();   // stand-in's DD2 skill -> DD1 skill (index in Skills)
        public readonly Dictionary<string, string> SkillNames = new();   // stand-in's DD2 skill -> DD1 skill name
        public HashSet<string> Allowed;   // the DD2 skills it may use: those whose kind its DD1 monster has
        public Vector3 Offset;      // body foot point relative to the actor's root, measured once
        public float WorldRatio;    // DD2 body height (world units) per DD1 unit, measured once
        public string DeathFx;
        public float Scale = 1f, Ratio;
        public Vector3 Feet;   // last screen position of its feet (GL pixels)
    }

    private static readonly Dictionary<string, Rig> Rigs = new();

    /// <summary>What DD2's fight screens call our stand-ins and their skills: the DD1 monster's names
    /// (localization keys: the actor class id, "skill_name_&lt;skill&gt;").</summary>
    private static readonly Dictionary<string, string> Names = new();

    /// <summary>The actor whose skill DD2's banner is naming right now (set around the banner's calls), or 0.</summary>
    internal static uint Performer;

    public static bool TryName(string key, out string name)
    {
        name = null;
        if (key == null || Line.Count == 0) return false;
        // A skill named for a known performer: its own DD1 monster's name for it (two DD1 monsters can share a stand-in).
        if (Performer != 0 && key.StartsWith("skill_name_", StringComparison.Ordinal) && ByGuid(Performer) is { } m
            && m.SkillNames.TryGetValue(key.Substring(11), out name))
            return true;
        return Names.Count > 0 && Names.TryGetValue(key, out name);
    }

    /// <summary>The DD2 skills a DD1 monster's stand-in may use (null: no limit).</summary>
    internal static HashSet<string> AllowedFor(uint guid) => Line.Count == 0 ? null : ByGuid(guid)?.Allowed;

    private static float _worldPerUnit;
    private static GameObject _quad;
    private static Material _quadMaterial;

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
            family = Session.Current?.Bestiary?.ArtFamily(family) ?? family;
            if (Load(family, "combat") == null) continue;
            var monster = new Monster { Dd1 = dd1Monsters[i], Family = family, Tier = tier, Dd2Class = dd2Enemies[i] };
            ReadArt(monster);
            Line.Add(monster);
        }
        if (Line.Count == 0) return;
        _boundTimeout = Time.unscaledTime + 8f;
        Names.Clear();
        var lore = Session.Current?.Lore;
        foreach (var m in Line)
            if (lore != null && lore.MonsterNames.TryGetValue(m.Dd1, out var name) && !Names.ContainsKey(m.Dd2Class)) Names[m.Dd2Class] = name;
        EventManager.AddListener<EventCombatSkillPresentation>(OnSkill);
        EventManager.AddListener<EventCombatPresentationSkillTarget>(OnTarget);
        EventManager.AddListener<EventCombatActorDeath>(OnDeath);
        _listening = true;
        Plugin.Log.LogInfo($"[dd1art] {Line.Count} DD1 monsters to draw: {string.Join(", ", Line.Select(m => m.Dd1 + " as " + m.Dd2Class))}");
    }

    /// <summary>DD1's skill list for the monster: each skill's attack pose, its own effect and the effect on its target.</summary>
    private static void ReadArt(Monster m)
    {
        m.Skills = new List<(string, string, string, string)>();
        try
        {
            // The monster's own tier, else any tier DD1 has (the raider and the hunter only have "_D").
            string tierName = $"{m.Family}_{m.Tier}";
            string file = Session.Current?.Dd1.PathOf("monsters", m.Family, tierName, tierName + ".art.darkest");
            if (file == null || !File.Exists(file))
            {
                string dir = Session.Current?.Dd1.PathOf("monsters", m.Family);
                file = dir != null && Directory.Exists(dir) ? Directory.GetFiles(dir, "*.art.darkest", SearchOption.AllDirectories).OrderBy(f => f).FirstOrDefault() : null;
            }
            if (file == null || !File.Exists(file)) return;
            foreach (var r in Core.Dd1.DarkestFile.Load(file))
            {
                if (r.Type == "skill") m.Skills.Add((r.Str("id", null), r.Str("anim", null), r.Str("fx", null), r.Str("targchestfx", null)));
                else if (r.Type == "commonfx") m.DeathFx = r.Str("deathfx", null);
            }
        }
        catch (Exception e) { Plugin.Log.LogInfo($"[dd1art] {m.Dd1} art: {e.Message}"); }
    }

    /// <summary>The fight is over: give DD2 its models back.</summary>
    public static void Clear()
    {
        Effects.Clear();
        Names.Clear();
        _worldPerUnit = 0f;
        if (_quad != null) UnityEngine.Object.Destroy(_quad);
        _quad = null;
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
            var skills = m.Skills is { Count: > 0 } ? m.Skills : m.Attacks.Select(a => (Id: (string)null, Anim: a, Fx: (string)null, TargetFx: (string)null)).ToList();
            if (skills.Count == 0) return;
            string skill = data.SkillId ?? "";
            // The DD1 skill this DD2 skill stands for (mapped when the fight began; new ones in order).
            if (!m.SkillFor.TryGetValue(skill, out int pick)) m.SkillFor[skill] = pick = m.SkillFor.Count % skills.Count;
            var dd1 = skills[pick % skills.Count];
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
            MapSkills(m);
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

    /// <summary>
    /// Pairs each of the stand-in's DD2 skills with the DD1 skill of the same kind (an attack with an attack, melee or
    /// ranged; a heal or buff with one) launched from the same ranks, so the DD1 pose, effect and name fit what the
    /// skill does. DD2 skills of a kind the DD1 monster lacks are kept from its AI (see Dd1MonsterSkillChoice). The
    /// stand-in also takes the DD1 monster's name, so two DD1 monsters on one DD2 enemy keep their own.
    /// </summary>
    private static void MapSkills(Monster m)
    {
        try
        {
            var lore = Session.Current?.Lore;
            if (lore != null && lore.MonsterNames.TryGetValue(m.Dd1, out var monsterName)) m.Actor.ActorInstance.SetActorName(monsterName);
            if (m.Skills == null || m.Skills.Count == 0) return;
            var ids = m.Actor.ActorInstance.GetEquippedCombatSkillIds();
            var library = Assets.Code.Utils.SingletonMonoBehaviour<Assets.Code.Library.Library<string, Assets.Code.Skill.ActorDataSkill>>.Instance;
            var dd2 = ids.Select(id => Shape(id, library?.GetLibraryElement(id))).ToList();
            var dd1 = Session.Current?.Dd1 != null ? Dd1MonsterSkills.Read(Session.Current.Dd1, m.Family, m.Tier) : new List<SkillShape>();
            if (dd1.Count == 0)
            {
                for (int i = 0; i < ids.Count; i++) m.SkillFor[ids[i]] = i % m.Skills.Count;
                Note(m, "skills: no DD1 info file, paired in order");
                return;
            }
            var match = Dd1MonsterSkills.Match(dd2, dd1);
            m.Allowed = Dd1MonsterSkills.Allowed(dd2, dd1);
            foreach (var s in dd2)
            {
                if (!match.TryGetValue(s.Id, out int k)) continue;
                string dd1Id = dd1[k].Id;
                int art = m.Skills.FindIndex(x => x.Id == dd1Id);   // that skill's entry in the art file
                m.SkillFor[s.Id] = art >= 0 ? art : k % m.Skills.Count;
                if (lore != null && dd1Id != null && lore.MonsterSkillNames.TryGetValue(dd1Id, out var name))
                {
                    m.SkillNames[s.Id] = name;
                    if (!Names.ContainsKey("skill_name_" + s.Id)) Names["skill_name_" + s.Id] = name;
                }
            }
            Note(m, "skills: " + string.Join(", ", dd2.Select(s => $"{s.Id}={(m.Allowed.Contains(s.Id) ? dd1[match[s.Id]].Id : "unused")}")));
        }
        catch (Exception e) { Plugin.Log.LogInfo($"[dd1art] {m.Dd1} skills: {e.Message}"); }
    }

    /// <summary>A DD2 skill's shape from its runtime data.</summary>
    private static SkillShape Shape(string id, Assets.Code.Skill.ActorDataSkill data) => new SkillShape
    {
        Id = id,
        Friendly = data?.m_IsFriendly ?? false,
        Ranged = data?.m_Tags != null && data.m_Tags.Contains("ranged"),
        LaunchRanks = data?.LaunchRanks?.ToList() ?? new List<int>(),
        TargetRanks = data?.TargetRelativeRanks?.ToList() ?? new List<int>(),
    };

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
        if (!_drawn || _screen == null || _quad != null || Event.current.type != EventType.Repaint) return;
        GUI.DrawTexture(new Rect(0, 0, Ui.Gui.W, Ui.Gui.H), _screen);
    }

    /// <summary>
    /// The monsters' picture on a screen-filling quad just behind the enemies, on the backdrop's camera and layer:
    /// DD2's health bars, status icons and the UI draw over it, as they would over its own models.
    /// </summary>
    private static void PlaceInScene(Camera fallback)
    {
        var cam = Dd1Backdrop.SceneCamera;
        if (!Dd1Backdrop.Ready || cam == null)
        {
            if (_quad != null) { UnityEngine.Object.Destroy(_quad); _quad = null; }
            return;
        }
        if (_quadMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            if (shader == null) return;
            _quadMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        _quadMaterial.mainTexture = _screen;
        if (_quad == null || _quad.transform.parent != cam.transform)
        {
            if (_quad != null) UnityEngine.Object.Destroy(_quad);
            _quad = new GameObject("DD3Monsters") { layer = Dd1Backdrop.QuadLayer };
            _quad.transform.SetParent(cam.transform, false);
            _quad.AddComponent<MeshFilter>().sharedMesh = Dd1Backdrop.SharedQuad;
            var mr = _quad.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _quadMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        float near = Line.Where(m => m.Actor != null && !m.Actor.Equals(null))
                         .Select(m => Vector3.Dot(m.Actor.transform.position + m.Offset - cam.transform.position, cam.transform.forward))
                         .Where(d => d > 0).DefaultIfEmpty(10f).Min();
        float d = Mathf.Clamp(near + 0.5f, cam.nearClipPlane + 0.5f, cam.farClipPlane * 0.8f);
        _quad.transform.localPosition = new Vector3(0, 0, d);
        _quad.transform.localRotation = Quaternion.identity;
        float h = cam.orthographic ? cam.orthographicSize * 2f : 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        _quad.transform.localScale = new Vector3(h * cam.aspect, h, 1f);
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
            var cam = Dd1Backdrop.Ready && Dd1Backdrop.SceneCamera != null ? Dd1Backdrop.SceneCamera : FightCamera();
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
            PlaceInScene(cam);
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
        // Measured once: where the body stands relative to the actor's root, and how tall it is in the world. After
        // that the DD1 monster follows the actor (not its animated outline) and keeps a fixed size in the world, so
        // DD2's hit and cast motions don't make it pulse, and camera zooms enlarge it like the others.
        var root = m.Actor.transform.position;
        var idle = Load(m.Family, "combat");
        if (m.WorldRatio <= 0 && idle != null)
        {
            var b = body.bounds;
            m.Offset = new Vector3(b.center.x, b.min.y, b.center.z) - root;
            m.WorldRatio = b.size.y / Mathf.Max(1f, idle.Height);
        }
        var anchor = root + m.Offset;
        var feet = cam.WorldToScreenPoint(anchor);
        if (feet.z <= 0) { Note(m, "behind the camera " + cam.name); return false; }
        float pixelsPerUnit = Mathf.Abs(cam.WorldToScreenPoint(anchor + cam.transform.up).y - feet.y);

        // Back to the idle loop when a held pose is done; the dead stay down until DD2 removes them.
        float t = Time.unscaledTime - m.Since;
        if (!m.Loop && !m.Dead && t > (m.Anim == "defend" ? DefendSeconds : AttackSeconds)) { Play(m, "combat", loop: true); t = 0; }
        var rig = Load(m.Family, m.Anim) ?? Load(m.Family, "combat");
        if (rig == null) { Note(m, "no DD1 animation files"); return false; }
        if (m.Clip == null) m.Clip = rig.Skeleton.Animation("combat")?.Name ?? rig.Skeleton.Animations.FirstOrDefault()?.Name;
        string skin = rig.Skeleton.Skins.Keys.FirstOrDefault(k => string.Equals(k, m.Tier.ToString(), StringComparison.OrdinalIgnoreCase));
        var pieces = rig.Skeleton.Pose(rig.Atlas, m.Clip, t, m.Loop, skin: skin);
        if (pieces.Count == 0) { Note(m, $"empty pose ({m.Anim}/{m.Clip})"); return false; }

        // One world size per DD1 unit for the whole line-up (the median over the stand-ins, a fifth smaller), so
        // DD1's own proportions between its monsters stay.
        if (_worldPerUnit <= 0 || Line.Any(x => x.Actor != null && x.WorldRatio <= 0))
        {
            var ratios = Line.Where(x => x.WorldRatio > 0).Select(x => x.WorldRatio).OrderBy(r => r).ToList();
            _worldPerUnit = ratios.Count > 0 ? ratios[ratios.Count / 2] * 0.8f : m.WorldRatio * 0.8f;
        }
        float scale = _worldPerUnit * pixelsPerUnit * Plugin.Dd1MonsterScale.Value;
        float bodyPx = (idle?.Height ?? 0) * scale;
        m.Scale = scale;
        m.Feet = feet;
        if (!DrawPieces(rig, pieces, feet.x, Screen.height - feet.y, scale, flipX: true, light)) { Note(m, "no atlas page textures"); return false; }
        Note(m, $"drawn, over {m.Dd2Class} with {cam.name} at ({feet.x:0},{feet.y:0}): {bodyPx:0} px tall, scale {scale:0.00}");
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

/// <summary>DD2's fight screens name our stand-ins and their skills as the DD1 monsters they stand in for.</summary>
[HarmonyLib.HarmonyPatch(typeof(Assets.Code.Locale.Localization), nameof(Assets.Code.Locale.Localization.GetString))]
internal static class Dd1NamesInFights
{
    private static bool Prefix(string key, ref string __result)
    {
        if (!Dd1MonsterView.TryName(key, out var name)) return true;
        __result = name;
        return false;
    }
}

[HarmonyLib.HarmonyPatch(typeof(Assets.Code.Locale.Localization), nameof(Assets.Code.Locale.Localization.TryGetString))]
internal static class Dd1NamesInFightsTry
{
    private static bool Prefix(string key, ref string __result)
    {
        if (!Dd1MonsterView.TryName(key, out var name)) return true;
        __result = name;
        return false;
    }
}

/// <summary>DD2's skill banner names the performer's skill: let our names know whose (its own DD1 monster's names).</summary>
[HarmonyLib.HarmonyPatch(typeof(Assets.Code.UI.Combat.SkillBannerBhv), nameof(Assets.Code.UI.Combat.SkillBannerBhv.OnSkillActivated))]
internal static class Dd1SkillBannerPerformer
{
    private static void Prefix(Assets.Code.Combat.Presentation.SkillPresentationData skillPresentationData) =>
        Dd1MonsterView.Performer = skillPresentationData?.PerformerGuid ?? 0u;

    private static Exception Finalizer(Exception __exception)
    {
        Dd1MonsterView.Performer = 0;
        return __exception;
    }
}

[HarmonyLib.HarmonyPatch(typeof(Assets.Code.UI.Combat.SkillBannerBhv), "HandleEventSkillSelectionChanged")]
internal static class Dd1SkillBannerSelection
{
    private static void Prefix(Assets.Code.Actor.Events.EventSkillSelectionChanged evt) => Dd1MonsterView.Performer = evt?.m_ActorGuid ?? 0u;

    private static Exception Finalizer(Exception __exception)
    {
        Dd1MonsterView.Performer = 0;
        return __exception;
    }
}

/// <summary>
/// A DD1 monster's stand-in only picks among the DD2 skills whose kind its DD1 monster has (no buffing its side when
/// the DD1 monster only attacks, and the other way round). DD2's enemies choose from this list (ActorControllerRandom);
/// if nothing would be left, DD2's own list stands.
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(Assets.Code.Actor.ActorController.ActorControllerBase), nameof(Assets.Code.Actor.ActorController.ActorControllerBase.GetValidSkillTargetEntries))]
internal static class Dd1MonsterSkillChoice
{
    private static void Postfix(Assets.Code.Actor.ActorInstance ___m_PerformerActor,
                                ref IReadOnlyList<Assets.Code.Actor.ActorController.SkillTargetEntry> __result)
    {
        try
        {
            if (___m_PerformerActor == null || __result == null || __result.Count < 2) return;
            var allowed = Dd1MonsterView.AllowedFor(___m_PerformerActor.ActorGuid);
            if (allowed == null || allowed.Count == 0) return;
            var kept = __result.Where(e => e != null && allowed.Contains(e.m_SkillId)).ToList();
            if (kept.Count > 0 && kept.Count < __result.Count) __result = kept;
        }
        catch (Exception e) { Plugin.Log.LogWarning("[dd1art] skill choice: " + e.Message); }
    }
}

/// <summary>DD2's enemy inspection lists the inspected actor's skills: name them as that actor's own DD1 monster does.</summary>
[HarmonyLib.HarmonyPatch(typeof(Assets.Code.UI.Canvases.AcademicViewUiBhv), nameof(Assets.Code.UI.Canvases.AcademicViewUiBhv.Populate))]
internal static class Dd1InspectedMonsterSkills
{
    private static void Prefix(uint actorGuid) => Dd1MonsterView.Performer = actorGuid;

    private static Exception Finalizer(Exception __exception)
    {
        Dd1MonsterView.Performer = 0;
        return __exception;
    }
}
