using System.Reflection;
using Assets.Code.Actor;
using Assets.Code.Source;
using Assets.Code.Utils.Serialization;
using DarkestDungeon3.Core.Expedition;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DarkestDungeon3.Dd2;

/// <summary>Use DD2's load semantics, so restoring condition does not deal damage or reroll an overstress event.</summary>
internal static class ExpeditionActorRestore
{
    private static readonly MethodInfo UpdateStatus = AccessTools.Method(typeof(ActorInstance), "UpdateStatus",
        new[] { typeof(ActorStatusType), typeof(SourceType), typeof(bool) });

    public static bool Apply(ActorInstance actor, ExpeditionHeroState saved)
    {
        if (actor == null || !ExpeditionParty.IsValid(saved) || saved.Outcome.Died) return false;
        JsonSerializationUtils.PerFieldApplyTo(actor, new JObject
        {
            ["m_Hp"] = saved.Hp, ["m_Stress"] = saved.Stress, ["m_WoundPercent"] = saved.WoundPercent
        });
        actor.ClampHealthToMax();
        UpdateStatus.Invoke(actor, new object[] { ActorStatusType.DEATHS_DOOR, SourceType.STATUS, true });
        actor.UpdatePreviousHpMax();
        return true;
    }
}
