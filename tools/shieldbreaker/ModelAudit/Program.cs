using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Dd2;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

if (args.Length != 2) throw new ArgumentException("ModelAudit <private model folder> <private donor.json>");
var model = ShieldbreakerModelData.Read(Path.Combine(args[0], "shieldbreaker.json"));
var donor = JObject.Parse(File.ReadAllText(args[1]));
if (!model.Bones.SequenceEqual(donor["bones"]!.ToObject<string[]>())) throw new Exception("Bone order changed");
var binds = donor["bindposes"]!.ToObject<float[][]>()!;
if (!model.Bindposes.SelectMany(r => r).SequenceEqual(binds.SelectMany(r => r))) throw new Exception("Bind matrices changed");
foreach (string texture in new[] { "shieldbreaker_base.png", "shieldbreaker_ink.png" })
    if (!ShieldbreakerModelData.ValidTexturePng(File.ReadAllBytes(Path.Combine(args[0], texture)))) throw new Exception("Invalid texture: " + texture);
foreach (var part in model.Parts)
{
    string target = part.Name.StartsWith("spear_") ? "r_Arm_WristSHJnt" : part.Name.StartsWith("shield_") ? "l_Arm_ElbowSHJnt" : null;
    if (target == null) continue;
    int bone = Array.IndexOf(model.Bones, target);
    for (int i = part.Start; i < part.Start + part.Count; i++)
        if (model.Indices[i][0] != bone || model.Weights[i][0] != 1) throw new Exception(part.Name + " has a sliding weapon binding");
}
var idle = model.Clips["idle"].Sample(0);
foreach (string clip in new[] { "attack", "defend" }) {
    var end = model.Clips[clip].Sample(model.Clips[clip].Duration);
    if (!end.Right.Equals(idle.Right) || !end.Left.Equals(idle.Left) || !end.Spear.Equals(idle.Spear)) throw new Exception(clip + " does not recover to idle");
}
var frames = Enumerable.Range(0, 24).Select(i => {
    var l = CorridorWalkCycle.Sample(i / 24.0); var r = CorridorWalkCycle.Sample(i / 24.0 + .5);
    return new { phase = i / 24.0, left = new { l.Forward, l.Lift, l.Pitch }, right = new { r.Forward, r.Lift, r.Pitch },
        support = CorridorWalkCycle.SupportShift(i / 24.0) };
});
File.WriteAllText(Path.Combine(args[0], "walk_samples.json"), JsonConvert.SerializeObject(frames));
Console.WriteLine(JsonConvert.SerializeObject(new { Verified = true, Vertices = model.Vertices.Length, Triangles = model.Triangles.Length / 3,
    Bones = model.Bones.Length, BodyHeight = model.BodyHeight, Parts = model.Parts.Length, Clips = model.Clips.Keys,
    TextureBounds = "4096x2048", NativeBindMatrices = "exact", Weapons = "right wrist / left forearm, weight 1", SavesAccessed = false }));
