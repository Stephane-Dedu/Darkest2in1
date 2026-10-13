namespace DarkestDungeon3.Core.Presentation;

/// <summary>Separates drawing the town from retaining it during quest selection and provisioning.</summary>
public sealed class HamletSceneLease
{
    private int _lastRequest = -100;
    private bool _visible;

    public void Request(int frame, bool visible)
    {
        _lastRequest = frame;
        _visible = visible;
    }

    public HamletSceneActivity Activity(int frame) =>
        frame - _lastRequest > 3 ? HamletSceneActivity.Release :
            _visible ? HamletSceneActivity.Render : HamletSceneActivity.Park;

    public void Reset() { _lastRequest = -100; _visible = false; }
}

public enum HamletSceneActivity { Release, Park, Render }
