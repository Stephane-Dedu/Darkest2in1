using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Assets.Code.Actor
{
    public enum ActorStatusType { DEATHS_DOOR }
    public sealed class ActorInstance
    {
        private float m_Hp = 30, m_Stress = 0, m_WoundPercent = 0;
        public float HpRaw => m_Hp;
        public float Stress => m_Stress;
        public float WoundPercent => m_WoundPercent;
        public float CurrentHpMax => 30 - MathF.Round(30 * m_WoundPercent);
        public readonly uint ActorGuid = 123;
        public readonly object Inventory = new(), Quirks = new(), Buffs = new();
        public readonly List<string> Calls = new();
        public bool DeathsDoor, LoadedStatus;
        public float PreviousHpMax;
        public int ConditionEvents;
        public void ClampHealthToMax() { Calls.Add("clamp"); m_Hp = Math.Min(m_Hp, CurrentHpMax); }
        public void UpdatePreviousHpMax() { Calls.Add("previous"); PreviousHpMax = CurrentHpMax; }
        private void UpdateStatus(ActorStatusType status, Assets.Code.Source.SourceType source, bool isLoad)
        {
            Calls.Add("status"); LoadedStatus = isLoad; DeathsDoor = m_Hp <= 0;
            if (!isLoad) ConditionEvents++;
        }
    }
}
namespace Assets.Code.Source { public enum SourceType { STATUS } }
namespace Assets.Code.Utils.Serialization
{
    public static class JsonSerializationUtils
    {
        public static void PerFieldApplyTo<T>(T target, JToken data)
        {
            var actor = (Assets.Code.Actor.ActorInstance)(object)target;
            actor.Calls.Add("serialize");
            // Native PerFieldApplyTo leaves unspecified fields alone.
            foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (data[field.Name] != null) field.SetValue(target, data[field.Name].ToObject(field.FieldType));
        }
    }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static MethodInfo Method(Type type, string name, Type[] parameters) =>
            type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, parameters, null);
    }
}
