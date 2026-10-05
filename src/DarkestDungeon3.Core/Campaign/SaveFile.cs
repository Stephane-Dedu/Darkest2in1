using System;
using System.IO;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>One save slot: the estate plus the expedition in progress, if any.</summary>
public sealed class SaveFile
{
    public Estate Estate = new();
    public ExpeditionState Expedition;

    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    public string ToJson() => JsonConvert.SerializeObject(this, Settings);

    public static SaveFile FromJson(string json) => JsonConvert.DeserializeObject<SaveFile>(json, Settings);

    /// <summary>Write via a temp file and swap, so a crash mid-write never leaves a broken save.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, ToJson());
            if (File.Exists(path)) File.Replace(tmp, path, path + ".bak", ignoreMetadataErrors: true);
            else File.Move(tmp, path);
        }
        finally
        {
            // Never delete the destination to recover from a failed replace.
            try { if (File.Exists(tmp)) File.Delete(tmp); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static SaveFile Load(string path) => File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;
}
