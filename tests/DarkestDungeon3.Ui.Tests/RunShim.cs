namespace Assets.Code.Game
{
    public class GameModeType
    {
        public static readonly GameModeType MAIN_MENU = new("menu"), DRIVING = new("driving"), COMBAT = new("combat"), HERO_SELECT = new("heroes");
        private readonly string _name;
        private GameModeType(string name) => _name = name;
        public override string ToString() => _name;
    }
    public enum GameType { EXPEDITION }
    public class GameTypeMgr { public GameType Current; public void SetGameType(GameType value) => Current = value; }
    public class GameModeMgr
    {
        public static GameModeType CurrentMode;
        public bool Changing;
        public int ModeRequests;
        private readonly List<Action<GameModeType>> _callbacks = new();
        public bool IsChangingState() => Changing;
        public void SetMode(GameModeType mode, bool isLoad) { ModeRequests++; }
        public void OnNextGameModeEnterComplete(Action<GameModeType> callback) => _callbacks.Add(callback);
        public void Complete(GameModeType mode)
        {
            CurrentMode = mode;
            var ready = _callbacks.ToArray(); _callbacks.Clear();
            foreach (var callback in ready) callback(mode);
        }
    }
    public static class GameModeNames { public static string GetName(this GameModeType mode) => mode.ToString(); }
}
namespace Assets.Code.Run
{
    public enum RunStartType { MAIN_MENU_NEW_RUN }
    public enum GameOverReason { ABANDON }
    public class RunBhv
    {
        public readonly ScoreManager RunScoreManager = new();
        public int NewRunRequests;
        public void SetNextRunStartType(RunStartType value) => NewRunRequests++;
    }
    public class ScoreManager
    {
        private GameOverReason? _reason;
        public GameOverReason? GetGameOverReason() => _reason;
        public void SetGameOver(GameOverReason value) => _reason = value;
    }
}
namespace Assets.Code.Utils
{
    public class TextBasedEditorPrefsBaseType
    {
        public static readonly TextBasedEditorPrefsBaseType MAP_GENERATION_SKIP_VALLEY = new(), RUN_TEST_SKIP_PROLOGUE = new(),
            DRIVING_DISABLE_COMBATS = new(), DISABLE_INTRO_CINEMATIC = new(), DISABLE_TUTORIALS = new();
        public bool Value;
        public void SetValue(bool value) => Value = value;
    }
}
namespace DarkestDungeon3.Dd2
{
    internal static class Dd2Api
    {
        public static Assets.Code.Game.GameModeMgr Modes;
        public static readonly List<uint> Party = new();
    }
}
