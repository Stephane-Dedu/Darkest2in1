namespace DarkestDungeon3.Dd2;

/// <summary>Poll a loading dependency at a bounded rate. Its timeout starts only once its owner is ready.</summary>
internal sealed class DeferredPoll
{
    private readonly float _interval, _timeout;
    private float? _started;
    private float _next;

    public DeferredPoll(float interval, float timeout = float.PositiveInfinity)
    { _interval = interval; _timeout = timeout; }

    public bool Due(float now, bool ownerReady)
    {
        if (!ownerReady) return false;
        _started ??= now;
        if (now < _next) return false;
        _next = now + _interval;
        return true;
    }

    public bool Expired(float now) => _started is float began && now - began >= _timeout;
    public void Reset() { _started = null; _next = 0; }
}
