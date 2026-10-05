using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Runtime
{
    internal sealed class Session
    {
        public static Session Current;
        public static string SaveDir;
        public Dd1Install Dd1;
    }
}

namespace DarkestDungeon3.Ui
{
    internal static class HamletUi
    {
        public static string Pretty(string id) => id;
    }
}
