using DarkestDungeon3.Core.Dd1;

namespace DarkestDungeon3.Runtime
{
    internal sealed class Session
    {
        public static Session Current;
        private static string _saveDir;
        public static readonly List<int> SaveDirReads = new();
        public static string SaveDir
        {
            get { SaveDirReads.Add(Environment.CurrentManagedThreadId); return _saveDir; }
            set => _saveDir = value;
        }
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
