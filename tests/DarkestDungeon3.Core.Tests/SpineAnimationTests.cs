using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkestDungeon3.Core.Dd1;
using Xunit;
using Xunit.Abstractions;

namespace DarkestDungeon3.Core.Tests;

/// <summary>Spine 2.1 animations from the user's DD1 install (monsters and heroes).</summary>
public class SpineAnimationTests
{
    private static readonly Dd1Install Install = Dd1Install.Find();
    private readonly ITestOutputHelper _out;
    public SpineAnimationTests(ITestOutputHelper output) => _out = output;

    private static (SpineSkeleton, SpineAtlas) Anim(string monster, string anim)
    {
        string dir = Install.PathOf("monsters", monster, "anim");
        var skel = SpineSkeleton.Load(Path.Combine(dir, $"{monster}.sprite.{anim}.skel"));
        var atlas = SpineAtlas.Parse(File.ReadAllText(Path.Combine(dir, $"{monster}.sprite.{anim}.atlas")));
        return (skel, atlas);
    }

    [Fact]
    public void Every_monster_and_hero_animation_reads_to_the_last_byte()
    {
        var failures = new List<string>();
        int files = 0, withIk = 0, withFfd = 0;
        foreach (var root in new[] { "monsters", "heroes" })
            foreach (var file in Directory.GetFiles(Install.PathOf(root), "*.skel", SearchOption.AllDirectories))
            {
                files++;
                try
                {
                    var bytes = File.ReadAllBytes(file);
                    var skel = SpineSkeleton.Read(bytes);
                    if (skel.BytesRead != bytes.Length) failures.Add($"{Path.GetFileName(file)}: stopped at {skel.BytesRead}/{bytes.Length}");
                    else if (skel.Animations.Count == 0) failures.Add($"{Path.GetFileName(file)}: no animation");
                    if (skel.IkConstraints.Count > 0) withIk++;
                    if (skel.Animations.Any(a => a.DeformsMeshes)) withFfd++;
                }
                catch (System.Exception e) { failures.Add($"{Path.GetFileName(file)}: {e.Message}"); }
            }
        _out.WriteLine($"{files} files, {withIk} with IK, {withFfd} with mesh deforms");
        Assert.True(files > 500);
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(15)));
    }

    [Fact]
    public void Arbalist_animations()
    {
        foreach (var name in new[] { "combat", "attack_crossbow", "attack_bayonet", "defend", "dead" })
        {
            var (skel, atlas) = Anim("skeleton_arbalist", name);
            foreach (var anim in skel.Animations)
                _out.WriteLine($"{name}/{anim.Name}: {anim.Duration:0.00}s, {anim.TimelineCount} timelines, events [{string.Join(", ", anim.Events.Select(e => e.Event))}]");
            // Every one of them draws (DD1's attacks are single held poses; the idle loops).
            Assert.NotEmpty(skel.Pose(atlas, skel.Animations[0].Name, 0.1f));
        }
        // A looped animation wraps around to its start; past the end a one-shot holds its last frame.
        var (idle, idleAtlas) = Anim("skeleton_arbalist", "combat");
        var loop = idle.Animations[0];
        Assert.Equal(SpineSkeleton.Bounds(idle.Pose(idleAtlas, loop.Name, 0f)), SpineSkeleton.Bounds(idle.Pose(idleAtlas, loop.Name, loop.Duration * 3f)));
        Assert.Equal(SpineSkeleton.Bounds(idle.Pose(idleAtlas, loop.Name, loop.Duration, loop: false)),
                     SpineSkeleton.Bounds(idle.Pose(idleAtlas, loop.Name, loop.Duration + 5f, loop: false)));
    }

    [Fact]
    public void Idle_loop_breathes()
    {
        var (skel, atlas) = Anim("skeleton_arbalist", "combat");
        var anim = skel.Animations.Single();
        var poses = Enumerable.Range(0, 8).Select(i => SpineSkeleton.Bounds(skel.Pose(atlas, anim.Name, anim.Duration * i / 8f))).Distinct().Count();
        Assert.True(poses > 2, $"only {poses} distinct poses over the idle loop");
        Assert.NotEmpty(skel.Pose(atlas, anim.Name, 0.3f));
    }
}

/// <summary>Writes posed meshes as JSON for an offline look (set DD3_POSE_DUMP to a folder to enable).</summary>
public class SpinePoseDump
{
    private static readonly Dd1Install Install = Dd1Install.Find();

    [Fact]
    public void Dump()
    {
        string dir = System.Environment.GetEnvironmentVariable("DD3_POSE_DUMP");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        var jobs = new[] { ("skeleton_arbalist", "combat"), ("skeleton_arbalist", "attack_crossbow"), ("skeleton_arbalist", "defend"),
                           ("swine_wretch", "combat"), ("fishman_harpoon", "combat"), ("ghoul", "combat") };
        foreach (var (monster, anim) in jobs)
        {
            string folder = Install.PathOf("monsters", monster, "anim");
            string skelPath = Path.Combine(folder, $"{monster}.sprite.{anim}.skel");
            if (!File.Exists(skelPath)) continue;
            var skel = SpineSkeleton.Load(skelPath);
            var atlas = SpineAtlas.Parse(File.ReadAllText(Path.Combine(folder, $"{monster}.sprite.{anim}.atlas")));
            var a = skel.Animations.FirstOrDefault(x => x.Name == anim) ?? skel.Animations[0];
            var sb = new System.Text.StringBuilder("{\"folder\":" + Newtonsoft.Json.JsonConvert.ToString(folder) + ",\"frames\":[");
            for (int i = 0; i < 6; i++)
            {
                var pieces = skel.Pose(atlas, a.Name, a.Duration * i / 6f);
                sb.Append(i > 0 ? "," : "").Append('[');
                for (int p = 0; p < pieces.Count; p++)
                {
                    var pc = pieces[p];
                    sb.Append(p > 0 ? "," : "").Append(Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        page = pc.Page.File, pos = pc.Positions, uv = pc.PagePixels, tri = pc.Triangles, add = pc.Additive, color = pc.Color,
                    }));
                }
                sb.Append(']');
            }
            sb.Append("]}");
            File.WriteAllText(Path.Combine(dir, $"{monster}.{anim}.json"), sb.ToString());
        }
    }
}
