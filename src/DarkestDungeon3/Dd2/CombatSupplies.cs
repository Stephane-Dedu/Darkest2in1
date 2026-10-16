using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Code.Actor;
using Assets.Code.Actor.ActorController;
using Assets.Code.Actor.Events;
using Assets.Code.Combat;
using Assets.Code.Game;
using Assets.Code.Item;
using Assets.Code.Library;
using Assets.Code.Skill;
using Assets.Code.Utils;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using DarkestDungeon3.Ui;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkestDungeon3.Dd2;

/// <summary>DD1's shared medical supplies use DD2's actual item skills and targeting. A one-charge equipment
/// proxy never owns goods: the expedition pack is charged only after native skill finalization consumes it.</summary>
internal static class CombatSupplies
{
    private static readonly FieldInfo InventoryField = AccessTools.Field(typeof(ActorInstance), "m_CombatInventory");
    private static readonly MethodInfo RefreshMaps = AccessTools.Method(typeof(ActorControllerBase), "RefreshSkillMaps");
    private sealed class Equipment
    {
        public ActorInstance Actor;
        public ItemInventory Original, Proxy;
    }

    private static readonly Dictionary<uint, Equipment> EquipmentByActor = new();
    private static CombatSupplyLedger _ledger;
    private static Core.Expedition.Inventory _pack;
    private static string _message;
    private static float _messageUntil;
    private static (ActorInstance Actor, string NativeId, int Before, int After)? _pendingUse;

    public static void Begin()
    {
        End();
        _pack = Driver.Instance?.Expedition?.Pack;
        if (_pack != null && InventoryField != null) _ledger = new CombatSupplyLedger(_pack);
    }

    public static void End()
    {
        foreach (var equipment in EquipmentByActor.Values)
        {
            if (equipment.Actor == null || !ReferenceEquals(equipment.Actor.GetCombatSkillInventory(), equipment.Proxy)) continue;
            try { equipment.Proxy.Clear(); }
            catch (Exception e) { Plugin.Log.LogWarning("[combat supplies] clearing temporary equipment: " + e.Message); }
            finally { InventoryField.SetValue(equipment.Actor, equipment.Original); }
            try
            {
                if (ReferenceEquals(Dd2Api.Actor(equipment.Actor.ActorGuid), equipment.Actor)) equipment.Actor.RefreshStats();
            }
            catch (Exception e) { Plugin.Log.LogWarning("[combat supplies] equipment restored after actor teardown: " + e.Message); }
        }
        EquipmentByActor.Clear();
        _ledger = null;
        _pack = null;
        _message = null;
        _pendingUse = null;
    }

    private static ActorInstance CurrentActor(out CombatBhv combat)
    {
        combat = SingletonMonoBehaviour<CombatBhv>.Instance;
        if (_ledger == null || !Dd2Combat.InFight || GameModeMgr.CurrentMode != GameModeType.COMBAT
            || combat == null || !combat.IsBattleRunning || combat.CurrentBattleState != BattleState.IN_TURN_SELECT) return null;
        var actor = Dd2Api.Actor(combat.CurrentActorGuid);
        return actor?.Controller?.GetIsInputValid() == true && Dd2Api.Party.Contains(actor.ActorGuid) ? actor : null;
    }

    private static ItemDefinition Definition(string supply)
    {
        string id = CombatSupplyLedger.NativeId(supply);
        var library = SingletonMonoBehaviour<Library<string, ItemDefinition>>.Instance;
        return id != null && library?.GetHasLibraryKey(id) == true ? library.GetLibraryElement(id) : null;
    }

    /// <summary>Called by the supply tray; native input remains responsible for choosing and confirming a legal target.</summary>
    public static bool Select(string supply)
    {
        var actor = CurrentActor(out var combat);
        var definition = Definition(supply);
        if (actor == null || definition == null || !definition.m_isConsumable
            || definition.GetActorDataSkill()?.Id != definition.m_id || !actor.GetIsItemConditionsMet(definition)) return false;
        var selection = _ledger.Select(actor.ActorGuid, supply, combat.CurrentRound, combat.CurrentTurn);
        if (selection == null) return false;
        if (!EquipmentByActor.TryGetValue(actor.ActorGuid, out var equipment))
        {
            equipment = new Equipment { Actor = actor, Original = actor.GetCombatSkillInventory(), Proxy = new CombatItemInventory(actor) };
            EquipmentByActor[actor.ActorGuid] = equipment;
            InventoryField.SetValue(actor, equipment.Proxy);
        }
        actor.DeselectSkill();
        equipment.Proxy.Clear();
        actor.AddCombatSkillInventoryItem(definition, 1);
        RefreshMaps.Invoke(actor.Controller, null);
        if (!actor.Controller.GetIsValidSkill(definition.m_id))
        {
            _ledger.Cancel(actor.ActorGuid);
            equipment.Proxy.Clear();
            InventoryField.SetValue(actor, equipment.Original);
            EquipmentByActor.Remove(actor.ActorGuid);
            actor.RefreshStats();
            RefreshMaps.Invoke(actor.Controller, null);
            Tell("No legal target for " + ItemDescription.GetTitle(definition) + ".");
            return false;
        }
        EventSkillSelectionChanged.Trigger(true, actor.ActorGuid, definition.m_id, isUserInput: true, autohighlightTarget: true);
        Tell("Choose a highlighted ally. Right-click to cancel.");
        return true;
    }

    private static void Tell(string text) { _message = text; _messageUntil = Time.unscaledTime + 4f; }

    internal static int BeforeUse(ActorInstance actor, string nativeId)
    {
        var selected = _ledger?.Selected(actor.ActorGuid);
        if (selected?.NativeId != nativeId
            || !EquipmentByActor.TryGetValue(actor.ActorGuid, out var equipment)
            || !ReferenceEquals(actor.GetCombatSkillInventory(), equipment.Proxy)) return -1;
        // A cancelled item can remain equipped until a later turn and be selected through DD2's own slot.
        // Commit against the actual confirmation turn rather than the earlier tray-click turn.
        var combat = SingletonMonoBehaviour<CombatBhv>.Instance;
        if (combat == null || _ledger.Select(actor.ActorGuid, selected.SupplyId, combat.CurrentRound, combat.CurrentTurn) == null) return -1;
        return actor.GetItemCountFromSkillId(nativeId);
    }

    internal static bool BlocksNativeSelection(ActorInstance actor, string nativeId)
    {
        if (_ledger == null || actor == null || !EquipmentByActor.TryGetValue(actor.ActorGuid, out var equipment)
            || !ReferenceEquals(actor.GetCombatSkillInventory(), equipment.Proxy)
            || !equipment.Proxy.GetValidItems().Any(item => item.GetItemDefinition().m_id == nativeId)) return false;
        var combat = SingletonMonoBehaviour<CombatBhv>.Instance;
        return combat == null || !_ledger.CanUseSelection(actor.ActorGuid, nativeId, combat.CurrentRound, combat.CurrentTurn);
    }

    internal static void AfterUse(ActorInstance actor, string nativeId, int before)
    {
        if (before < 0 || _ledger == null) return;
        int after = actor.GetItemCountFromSkillId(nativeId);
        if (before == 1 && after == 0) _pendingUse = (actor, nativeId, before, after);
    }

    internal static void AfterNativeApply(SkillCalculation calculation)
    {
        if (_pendingUse is not { } receipt || _ledger == null) return;
        if (calculation.FinalResult?.m_SkillId != receipt.NativeId
            || calculation.FinalResult.m_PerformerActorGuid != receipt.Actor.ActorGuid) return;
        _pendingUse = null;
        var actor = receipt.Actor;
        string nativeId = receipt.NativeId;
        string supply = _ledger.Selected(actor.ActorGuid)?.SupplyId;
        if (!_ledger.ConfirmUse(actor.ActorGuid, nativeId, receipt.Before, receipt.After)) return;
        Plugin.Log.LogInfo($"[combat supplies] {actor.ActorGuid} used {supply}; {_pack.Count(supply)} remain in the expedition pack");
        Session.Current?.Persist();
    }

    /// <summary>Never serialize temporary equipment as additional owned supplies, including the native battle autosave.</summary>
    internal static void StripProxyFromSave(ActorInstance actor, JToken data)
    {
        if (data is JObject json && EquipmentByActor.TryGetValue(actor.ActorGuid, out var equipment)
            && ReferenceEquals(actor.GetCombatSkillInventory(), equipment.Proxy))
            json["combat_inventory"] = equipment.Original.SaveToJson();
    }

    internal static void Draw()
    {
        if (Driver.Instance?.Phase != Phase.Fighting || Dd2Combat.CoverAlpha > .05f || _ledger == null) return;
        var actor = CurrentActor(out var combat);
        if (actor == null) return;
        if (EquipmentByActor.TryGetValue(actor.ActorGuid, out var equipment)
            && equipment.Proxy.GetItemIds().Any(id => BlocksNativeSelection(actor, id)))
        {
            actor.DeselectSkill();
            equipment.Proxy.Clear();
            _ledger.Cancel(actor.ActorGuid);
            RefreshMaps.Invoke(actor.Controller, null);
        }
        Gui.Begin();
        var panel = new Rect(1390, 94, 490, 156);
        Gui.Fill(panel, ExpeditionSkin.Ink);
        ExpeditionSkin.Rule(new Rect(panel.x, panel.y, panel.width, 3), ExpeditionSkin.Brass);
        bool used = _ledger.UsedThisTurn(actor.ActorGuid, combat.CurrentRound, combat.CurrentTurn);
        ExpeditionSkin.Text(new Rect(panel.x + 14, panel.y + 8, panel.width - 28, 30),
            used ? "Supplies · used this turn" : "Supplies · one per turn", 24, ExpeditionSkin.Ivory, heading: true);
        for (int i = 0; i < CombatSupplyLedger.Supplies.Length; i++)
        {
            string supply = CombatSupplyLedger.Supplies[i];
            int count = _pack.Count(supply);
            var r = new Rect(panel.x + 16 + i * 92, panel.y + 42, 78, 78);
            bool selected = _ledger.Selected(actor.ActorGuid)?.SupplyId == supply && actor.GetSelectedSkillId() == CombatSupplyLedger.NativeId(supply);
            ExpeditionSkin.Slot(r, selected);
            var tint = GUI.color;
            if (used || count == 0) GUI.color = new Color(.42f, .42f, .42f, 1);
            var icon = Art.InventoryIcon(supply, count, Session.Current.Content.Items.StackLimit(supply));
            if (icon != null) GUI.DrawTexture(new Rect(r.x + 8, r.y + 5, 62, 62), icon, ScaleMode.ScaleToFit);
            GUI.color = tint;
            ExpeditionSkin.Text(new Rect(r.x + 44, r.y + 50, 30, 26), count.ToString(), 24, ExpeditionSkin.Ivory, TextAnchor.MiddleRight);
            if (r.Contains(Event.current.mousePosition))
            {
                var definition = Definition(supply);
                string title = definition != null ? ItemDescription.GetTitle(definition) : supply;
                ExpeditionSkin.Tip(r, title + "\n" + Hint(supply) + "\nUses one from the shared expedition pack after confirmation.");
            }
            if (!used && count > 0 && Gui.Hotspot(r)) Select(supply);
        }
        if (_messageUntil > Time.unscaledTime)
            ExpeditionSkin.Text(new Rect(panel.x + 14, panel.y + 120, panel.width - 28, 30), _message, 21, ExpeditionSkin.Ivory);
        ExpeditionSkin.DrawTip();
    }

    private static string Hint(string supply) => supply switch
    {
        Supply.Bandage => "Bleeding ally: remove bleed and heal 5 HP.",
        Supply.Antivenom => "Blighted ally: remove blight and heal 5 HP.",
        Supply.Herbs => "Remove bleed, blight and burn from an afflicted ally.",
        Supply.HolyWater => "Remove negative tokens and combo from an ally.",
        Supply.Laudanum => "Relieve stress and remove horror from an ally.",
        _ => "Select, then choose a legal native target.",
    };
}

[HarmonyPatch(typeof(ActorInstance), nameof(ActorInstance.RemoveOneUseOfCombatSkillItemIfConsumable))]
internal static class PayForConfirmedCombatSupply
{
    private static void Prefix(ActorInstance __instance, string skillId, out int __state) => __state = CombatSupplies.BeforeUse(__instance, skillId);
    private static void Postfix(ActorInstance __instance, string skillId, int __state) => CombatSupplies.AfterUse(__instance, skillId, __state);
}

// DD2 consumes its item at FinalizeResults, one state before ApplyResults actually cures/heals the target.
// Persist the pack charge and resulting hero condition together after that latter native operation.
[HarmonyPatch(typeof(SkillCalculation), nameof(SkillCalculation.ApplyResults))]
internal static class SaveCombatSupplyAfterNativeEffects
{
    private static void Postfix(SkillCalculation __instance) => CombatSupplies.AfterNativeApply(__instance);
}

[HarmonyPatch(typeof(ActorInstance), nameof(ActorInstance.SaveToJson))]
internal static class TemporaryCombatSuppliesAreNotOwnedItems
{
    private static void Postfix(ActorInstance __instance, JToken __result) => CombatSupplies.StripProxyFromSave(__instance, __result);
}

[HarmonyPatch(typeof(UiRoot), "OnGUI")]
internal static class DrawCombatSupplyTray
{
    private static void Postfix() => CombatSupplies.Draw();
}

/// <summary>A proxy retained on another hero cannot outlive the last shared item. Guard the native target
/// confirmation seam too, so a stale slot/hotkey cannot apply a free cure before the tray next repaints.</summary>
[HarmonyPatch]
internal static class RejectUnavailableSharedCombatSupplies
{
    private static readonly FieldInfo Performer = AccessTools.Field(typeof(ActorControllerBase), "m_PerformerActor");
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ActorControllerBase), nameof(ActorControllerBase.GetIsValidSkill));
        yield return AccessTools.Method(typeof(ActorControllerBase), nameof(ActorControllerBase.GetIsValidSkillTarget));
    }
    private static void Postfix(ActorControllerBase __instance, string skillId, ref bool __result)
    {
        if (__result && CombatSupplies.BlocksNativeSelection(Performer.GetValue(__instance) as ActorInstance, skillId)) __result = false;
    }
}
