namespace DarkestDungeon3;
internal static class Plugin
{
    public static readonly TestLog Log = new();
    public static TestConfig<string> NativeRoomSceneryPath = new();
    public sealed class TestConfig<T> { public T Value; }
    public sealed class TestLog
    {
        public readonly List<string> Infos = new(), Warnings = new();
        public readonly List<int> Threads = new();
        public void LogInfo(object message) { Infos.Add(message.ToString()); Threads.Add(Environment.CurrentManagedThreadId); }
        public void LogWarning(object message) { Warnings.Add(message.ToString()); Threads.Add(Environment.CurrentManagedThreadId); }
        public void LogError(object message) { Warnings.Add(message.ToString()); Threads.Add(Environment.CurrentManagedThreadId); }
    }
}
