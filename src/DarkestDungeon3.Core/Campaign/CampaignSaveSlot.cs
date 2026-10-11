namespace DarkestDungeon3.Core.Campaign;

/// <summary>A selected campaign and its destination belong together; failed loads must retain neither.</summary>
public sealed class CampaignSaveSlot
{
    public SaveFile Save { get; set; }
    public string Path { get; private set; }

    public void Clear() { Save = null; Path = null; }

    public void Select(string path)
    {
        Clear();
        var loaded = SaveFile.Load(path);
        Save = loaded; Path = path;
    }
}
