using System.Diagnostics;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Core.Dd1;

// Actual DD1 geometry and pixels through the same CPU rasterizer the plugin uses.
// PNG decoding runs before the timed raster stage; Unity uploads are outside this headless harness.
var dd1 = Dd1Install.Find(args.FirstOrDefault());
if (dd1 == null) throw new InvalidOperationException("DD1 install not found");
var decoder = Path.GetFullPath("tools/SpineTiming/decode_page.py");
var total = 0.0;
foreach (var id in new[] { "stage_coach", "blacksmith", "abbey", "tavern", "ground" })
{
    var folder = TownLayout.ArtFolder(dd1, id, true, 0f);
    var files = Dd1Install.SpineIn(folder);
    if (files == null) continue;
    var skeleton = SpineSkeleton.Load(files.Value.skel);
    var atlas = SpineAtlas.Parse(File.ReadAllText(files.Value.atlas));
    var pages = new Dictionary<SpineAtlas.Page, RgbaImage>();
    foreach (var page in atlas.Pages)
    {
        var start = new ProcessStartInfo("python") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(decoder);
        start.ArgumentList.Add(Path.Combine(folder, page.File));
        using var process = Process.Start(start)!;
        using var binary = new BinaryReader(process.StandardOutput.BaseStream);
        int w = binary.ReadInt32(), h = binary.ReadInt32();
        byte[] pixels = binary.ReadBytes(w * h * 4);
        process.WaitForExit();
        if (process.ExitCode != 0 || pixels.Length != w * h * 4) throw new InvalidDataException(page.File);
        pages[page] = new RgbaImage(w, h, pixels);
    }
    foreach (var variant in id == "ground" ? new[] { "idle" } : new[] { "idle", "active" })
    {
        var pieces = skeleton.SetupPose(atlas, s => variant == "idle" ? TownLayout.IdleSlot(s.Name) : TownLayout.HoverSlot(s.Name));
        var watch = Stopwatch.StartNew();
        var result = SpineRaster.Render(pieces, p => pages[p]);
        watch.Stop();
        total += watch.Elapsed.TotalMilliseconds;
        Console.WriteLine($"{id}/{variant}: {watch.Elapsed.TotalMilliseconds:F1} ms, {result?.Image.Width}x{result?.Image.Height}, {pieces.Count} pieces");
    }
}
Console.WriteLine($"Total CPU raster (five town assets, idle plus hover): {total:F1} ms");
