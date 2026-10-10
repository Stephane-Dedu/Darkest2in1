using DarkestDungeon3.Core.Dd2Data;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: TrinketAudit <StreamingAssets directory> [item IDs ...]");
    return 1;
}
var descriptions = TrinketDescriptions.Load(args[0]);
if (args.Length > 1)
{
    foreach (string id in args.Skip(1))
    {
        string text = descriptions.Effects(id, out bool complete);
        Console.WriteLine($"{id} ({(complete ? "complete" : "incomplete")}):\n{text ?? "[blank]"}");
    }
}
else
{
    var tables = Dd2Tables.Load(Path.Combine(args[0], "Excel"));
    int full = 0, partial = 0, blank = 0;
    foreach (string id in tables.Trinkets.Keys)
    {
        string text = descriptions.Effects(id, out bool complete);
        if (string.IsNullOrWhiteSpace(text)) blank++;
        else if (complete) full++;
        else partial++;
    }
    Console.WriteLine($"{tables.Trinkets.Count} trinkets: {full} complete, {partial} partial, {blank} blank.");
}
return 0;
