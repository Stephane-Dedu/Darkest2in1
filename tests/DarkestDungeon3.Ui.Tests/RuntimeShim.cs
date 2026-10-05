namespace DarkestDungeon3;
internal static class Plugin
{
    public static readonly TestLog Log = new();
    public static TestConfig<string> NativeRoomSceneryPath = new();
    public sealed class TestConfig<T> { public T Value; }
    public sealed class TestLog
    {
        public void LogInfo(object message) { }
        public void LogWarning(object message) { }
    }
}
