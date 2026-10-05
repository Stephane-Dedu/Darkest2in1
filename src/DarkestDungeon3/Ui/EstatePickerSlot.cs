using System;
using DarkestDungeon3.Core.Campaign;

namespace DarkestDungeon3.Ui;

/// <summary>Keep unreadable slots distinct from new estates; summaries only read files.</summary>
internal sealed class EstatePickerSlot
{
    public SaveFile Save { get; private set; }
    public Exception Error { get; private set; }
    public bool CanOpen => Error == null;

    public static EstatePickerSlot Read(string path)
    {
        try { return new EstatePickerSlot { Save = SaveFile.Load(path) }; }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("[session] estate summary could not load: " + e.Message);
            return new EstatePickerSlot { Error = e };
        }
    }
}
