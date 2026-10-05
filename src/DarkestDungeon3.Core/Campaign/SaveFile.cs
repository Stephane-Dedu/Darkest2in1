using System;
using System.IO;
using DarkestDungeon3.Core.Expedition;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>One save slot: the estate plus the expedition in progress, if any.</summary>
public sealed class SaveFile
{
    public Estate Estate = new();
    public ExpeditionState Expedition;
    [JsonIgnore] public bool RecoveredFromBackup;

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

    /// <summary>Recover missing/invalid payloads from their backup without rewriting either file.</summary>
    public static SaveFile Load(string path)
    {
        Exception invalid = null;
        try { return ReadValidated(path); }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (JsonException e) { invalid = e; }
        catch (InvalidDataException e) { invalid = e; }
        try
        {
            var recovered = ReadValidated(path + ".bak");
            recovered.RecoveredFromBackup = true;
            return recovered;
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (JsonException e) { invalid = e; }
        catch (InvalidDataException e) { invalid = e; }
        if (invalid != null) throw new InvalidDataException("The saved estate and its backup have no valid campaign payload.", invalid);
        return null;
    }

    private static SaveFile ReadValidated(string path)
    {
        string json = File.ReadAllText(path);
        var root = JObject.Parse(json);
        if (root["Estate"] is not JObject) throw new InvalidDataException("The saved campaign has no estate record.");
        var save = root.ToObject<SaveFile>(JsonSerializer.Create(Settings));
        if (save?.Estate == null) throw new InvalidDataException("The saved campaign has no estate record.");
        return save;
    }
}
