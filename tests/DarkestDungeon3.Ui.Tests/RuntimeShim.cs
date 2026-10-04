namespace DarkestDungeon3;
internal static class Plugin
{
    public static readonly TestLog Log = new();
    public sealed class TestLog
    {
        public void LogInfo(object message) { }
        public void LogWarning(object message) { }
    }
}
