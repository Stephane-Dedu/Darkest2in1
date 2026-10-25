using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DarkestDungeon3.Core.Presentation;
using Newtonsoft.Json.Linq;

if (args.Length != 4 && !(args.Length == 5 && args[4] == "--turf"))
    throw new ArgumentException("Pass private input, model package, shadow field and NEW output directories, optionally --turf.");
string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
string Private(string path)
{
    path = Path.GetFullPath(path);
    if (path.Equals(repo, StringComparison.OrdinalIgnoreCase) || path.StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("Game-derived paint belongs outside Git.");
    return path;
}
string input = Private(args[0]), package = Private(args[1]), shadows = Private(args[2]), output = Private(args[3]);
if (Directory.Exists(output)) throw new ArgumentException("Preserve previous paint; choose a new output directory.");
string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
var inputHashes = new Dictionary<string,string>();
byte[] Read(string name, HamletPalette.Paint paint)
{
    var pixels = File.ReadAllBytes(Path.Combine(input, name + ".rgba"));
    if (pixels.Length != HamletPaintBaker.TileSize * HamletPaintBaker.TileSize * 4)
        throw new FormatException("Expected a complete bottom-left 512px RGBA input: " + name);
    inputHashes[name] = Hash(pixels);
    HamletPalette.GradeRgba(pixels, paint);
    return pixels;
}
var manifest = JObject.Parse(File.ReadAllText(Path.Combine(package, "manifest.json")));
var sources = ((JObject)manifest["models"]!).Properties().ToDictionary(p => p.Name, p => (string)p.Value["source_sha256"]!);
var terrain = HamletTerrain.Build(fullCliffTile: true);
var sun = HamletSunShadow.ReadOptional(shadows, terrain, sources, out var sunStatus);
if (sun == null) throw new InvalidDataException("A matching reviewed ground shadow field is required: " + sunStatus);
var timer = Stopwatch.StartNew();
var baked = HamletPaintBaker.Bake(Read("soil", HamletPalette.EarthDetail), Read("stone", HamletPalette.CobbleDetail),
    Read("grass", args.Length == 5 ? HamletPalette.TurfDetail : HamletPalette.Grass), Read("rock", HamletTownscape.CliffPaint), sun: sun);
Directory.CreateDirectory(output);
var files = new Dictionary<string,string>();
foreach (var pair in new[] { ("ground",baked.Ground), ("lanes",baked.Lanes), ("valley",baked.Valley) })
{
    File.WriteAllBytes(Path.Combine(output, pair.Item1 + ".rgba"), pair.Item2);
    files[pair.Item1 + ".rgba"] = Hash(pair.Item2);
}
var core = Directory.EnumerateFiles(Path.Combine(repo,"src/DarkestDungeon3.Core/Presentation"),"Hamlet*.cs").OrderBy(p=>p)
    .ToDictionary(p=>Path.GetRelativePath(repo,p).Replace('\\','/'),p=>Hash(Encoding.UTF8.GetBytes(File.ReadAllText(p).Replace("\r\n","\n"))));
File.WriteAllText(Path.Combine(output,"core-bake.json"),JsonSerializer.Serialize(new
{
    producer="offline-core-bake", version=1, size=baked.Size, encoding="RGBA8_sRGB", origin="bottom-left",
    notice="Core grading/baking is exact for supplied bytes. Input downsampling approximates Unity GPU readback; no native screenshot or shader acceptance.",
    inputHashes, rawFiles=files, paint_sources=core, sunStatus, seconds=timer.Elapsed.TotalSeconds
},new JsonSerializerOptions { WriteIndented=true }));
Console.WriteLine($"Offline Core ground bake complete in {timer.Elapsed.TotalSeconds:F2}s; {sunStatus}");
