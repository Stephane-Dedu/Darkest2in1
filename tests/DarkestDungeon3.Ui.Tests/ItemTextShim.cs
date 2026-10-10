namespace Assets.Code.Utils
{
    public static class SingletonMonoBehaviour<T> { public static T Instance; public static bool HasInstance() => Instance != null; }
    public static class Singleton<T> { public static T Instance; }
}
namespace Assets.Code.Library
{
    public class Library<TKey, TValue>
    {
        public readonly Dictionary<TKey, TValue> Elements = new();
        public TValue GetLibraryElement(TKey key) => Elements.TryGetValue(key, out var v) ? v : default;
        public bool TryGetLibraryElement(TKey key, out TValue value) => Elements.TryGetValue(key, out value);
        public bool IsInitializationFinished() => true;
    }
}
namespace Assets.Code.Item
{
    public class ItemDefinition { public string Description; }
    public static class ItemDescription
    {
        public static string GetDescription(ItemDefinition def, int qty, bool run, int duration, bool sell, bool discard,
            bool hideTitle = false, bool showBlockedItems = true, bool showItemTag = true) => def.Description;
    }
}
namespace Assets.Code.Locale
{
    public class Localization
    {
        public object Language = new();
        public object GetLanguage() => Language;
        public string TryGetString(string key) => null;
    }
}
namespace UnityEngine
{
    public static class Application
    {
        public static string streamingAssetsPath = @"C:\Users\Piral\DarkestDungeon3\game\Darkest Dungeon II_Data\StreamingAssets";
    }
}
