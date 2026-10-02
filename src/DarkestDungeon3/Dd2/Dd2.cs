using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Game;
using Assets.Code.Library;
using Assets.Code.Roster;
using Assets.Code.Run;
using Assets.Code.Source;
using Assets.Code.Utils;

namespace DarkestDungeon3.Dd2;

/// <summary>Thin, null-safe access to the DD2 systems the mod drives. Every call here was read off the decomp.</summary>
internal static class Dd2
{
    public static GameTypeMgr GameType => Singleton<GameTypeMgr>.Instance;
    public static GameModeMgr Modes => Singleton<GameModeMgr>.Instance;
    public static RosterManager Roster => GameType?.RosterManager;

    public static ActorInstance Actor(uint guid) =>
        SingletonMonoBehaviour<Library<uint, ActorInstance>>.HasInstance()
            ? SingletonMonoBehaviour<Library<uint, ActorInstance>>.Instance.GetLibraryElement(guid)
            : null;

    public static IReadOnlyList<uint> Party => Roster?.GetActorGuids(RosterStatusType.PARTY) ?? new List<uint>();

    public static bool IsDead(uint guid) =>
        Roster != null && (Roster.GetActorGuids(RosterStatusType.DEAD).Contains(guid) || Actor(guid) == null);

    /// <summary>DD2's torch (0-100), which drives its own light rules in combat.</summary>
    public static float Torch
    {
        get => GameType?.RunValues?.GetValue(RunValueType.TORCH) ?? 100f;
        set
        {
            var values = GameType?.RunValues;
            if (values == null) return;
            values.ChangeValue(RunValueType.TORCH, value - values.GetValue(RunValueType.TORCH), SourceType.DRIVING);
        }
    }

    public static float HpFraction(ActorInstance a) =>
        a == null || a.CurrentHpMax <= 0 ? 0f : UnityEngine.Mathf.Clamp01(a.HpRaw / a.CurrentHpMax);
}
