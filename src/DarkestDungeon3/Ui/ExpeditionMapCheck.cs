using System;
using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dungeon;
using DarkestDungeon3.Core.Presentation;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Opt-in native input checks inside the unsaved expedition fixture. Never sends desktop input.</summary>
internal static class ExpeditionMapCheck
{
    internal static readonly bool Enabled = Environment.GetCommandLineArgs().Contains("-dd3-expedition-check");
    private static readonly Dictionary<QuestOffer, Rect> Quests = new();
    private static readonly Dictionary<string, Rect> Switches = new();
    private static Dictionary<QuestOffer, Rect> _anchors;
    private static ExpeditionMap3D.MapRect _before;
    private static QuestOffer _clicked, _selected;
    private static string _switchedFrom;
    private static int _step;
    private static float _next, _deadline;
    private static bool _done;

    internal static void RecordQuest(QuestOffer quest, Rect rect) { if (Enabled) Quests[quest] = rect; }
    internal static void RecordSwitch(string zone, Rect rect) { if (Enabled) Switches[zone] = rect; }
    internal static void BeginDraw() { if (Enabled) { Quests.Clear(); Switches.Clear(); } }
    internal static void Failed() => _done = true;

    internal static Event Input(EmbarkUi ui)
    {
        if (!Enabled || _done || !ExpeditionPreview.OwnsSession || Event.current.type != EventType.Repaint) return null;
        if (_deadline == 0) { _deadline = Time.unscaledTime + 90; ui.CheckEnableMap(); }
        Require(Time.unscaledTime < _deadline, "Timed out waiting for the private map/effects or a control.");
        if (Time.unscaledTime < _next) return null;
        if (_step is 0 or 22 or 30 && (!ui.CheckMapReady || ExpeditionMapEffects.ReadyCount != 6)) return null;
        _before = ui.CheckCamera;
        _next = Time.unscaledTime + 0.06f;
        Event Mouse(Rect rect, bool up) => new() { type = up ? EventType.MouseUp : EventType.MouseDown,
            button = 0, mousePosition = rect.center };
        Event Quest(string location, bool up, bool apprentice = false)
        {
            if (!up)
            {
                _clicked = Quests.Keys.FirstOrDefault(q => ZoneBase.Of(q.Dungeon) == location && (!apprentice || q.Difficulty == 1));
                Require(_clicked != null, "No visible quest in " + location);
            }
            Require(Quests.TryGetValue(_clicked, out var rect), "Quest disappeared during camera motion.");
            return Mouse(rect, up);
        }
        Event Switch(bool up)
        {
            string zone = ui.CheckShown("weald");
            Require(Switches.TryGetValue(zone, out var rect), "Missing paired-area arrow.");
            if (!up) _switchedFrom = zone;
            return Mouse(rect, up);
        }
        return _step switch
        {
            1 or 2 => Quest("crypts", _step == 2),
            3 or 4 => Quest("cove", _step == 4),
            6 or 7 => Quest("warrens", _step == 7),
            9 or 10 => Quest("weald", _step == 10),
            11 => new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape },
            13 or 14 or 15 or 16 => Switch(_step % 2 == 0),
            17 or 18 or 20 or 21 => Mouse(EmbarkUi.MapModeButton, _step is 18 or 21),
            23 or 24 => Quest("crypts", _step == 24, apprentice: true),
            25 or 26 => Mouse(new Rect(1190, 905, 330, 90), _step == 26),
            28 or 29 => Mouse(new Rect(30, 1000, 300, 60), _step == 29),
            _ => new Event { type = EventType.Repaint, mousePosition = new Vector2(150, 950) },
        };
    }

    internal static void Verify(EmbarkUi ui)
    {
        void StableAnchors()
        {
            Require(_anchors.Count == Quests.Count && _anchors.All(a => Quests.TryGetValue(a.Key, out var r) && r == a.Value),
                "Visible quest controls changed position or disappeared during camera motion.");
        }
        void Selected(string location)
        {
            Require(ReferenceEquals(ui.CheckQuest, _clicked) && ui.CheckLocation == location, "Quest click did not focus " + location);
            Require(Same(_before, ui.CheckCamera), "Retargeting changed the current camera frame immediately.");
            StableAnchors();
        }
        if (_step == 0)
        {
            Require(Quests.Count >= 4 && ExpeditionMapAtmosphere.LastDrawCount == 52, "Missing native effects or regional quest rows.");
            _anchors = new Dictionary<QuestOffer, Rect>(Quests);
            Checkpoint("overview: all six native effect IDs ready; 52 quads under fixed quest controls");
            _next = Time.unscaledTime + 2;
        }
        else if (_step == 2) { Selected("crypts"); _next = Time.unscaledTime + 0.18f; }
        else if (_step == 4)
        {
            Selected("cove"); Require(_before.Width > 1920 && _before.Width < 1920 * 1.999f, "Retarget did not occur mid-flight.");
            Checkpoint("mid-flight city to Shroud retarget preserved the camera frame and every quest control");
            _next = Time.unscaledTime + 2;
        }
        else if (_step == 5) { StableAnchors(); Checkpoint("Shroud close view"); }
        else if (_step == 7) { Selected("warrens"); _next = Time.unscaledTime + 2; }
        else if (_step == 8) { StableAnchors(); Checkpoint("Foetor close view"); }
        else if (_step == 10) { Selected("weald"); _selected = ui.CheckQuest; _next = Time.unscaledTime + 0.18f; }
        else if (_step == 11)
        {
            Require(ui.CheckLocation == null && Same(_before, ui.CheckCamera), "Escape jumped instead of reversing the camera.");
            Require(ReferenceEquals(_selected, ui.CheckQuest), "Returning to the map reset the selected quest.");
            StableAnchors(); _next = Time.unscaledTime + 2;
        }
        else if (_step == 12)
        {
            Require(Math.Abs(ui.CheckCamera.Width - 1920) < 0.1f, "Return did not settle at the overview.");
            StableAnchors(); Checkpoint("overview reversal preserved the selected quest");
        }
        else if (_step is 14 or 16)
        {
            Require(ui.CheckShown("weald") != _switchedFrom && ui.CheckQuest?.Dungeon == ui.CheckShown("weald"), "Paired-area arrow did not select its own quests.");
            Require(Same(_before, ui.CheckCamera), "Paired-area switch moved the camera.");
        }
        else if (_step == 18) _next = Time.unscaledTime + 1;
        else if (_step == 19)
        {
            Require(!ui.CheckMapWanted && ExpeditionMapEffects.TextureCount == 0, "Painted fallback retained native effects.");
            Checkpoint("paired areas and painted rollback passed; five owned textures released");
        }
        else if (_step == 22) Checkpoint("native effect pack reloaded after painted rollback");
        else if (_step == 24) { Selected("crypts"); ui.CheckRestoreParty(); }
        else if (_step == 26)
        {
            Require(ui.CheckProvisioning, "Provision button did not open the ordinary provisioner.");
            _next = Time.unscaledTime + 1;
        }
        else if (_step == 27) Require(ExpeditionMapEffects.TextureCount == 0, "Provisioner retained map effect textures.");
        else if (_step == 29) Require(!ui.CheckProvisioning && ReferenceEquals(ui.CheckQuest, _clicked), "Back to quests lost selection.");
        else if (_step == 30)
        {
            _done = true;
            Checkpoint("PASS: fixed quest controls, mid-flight retarget/reverse, paired regions, painted fallback/reload, provisioning cleanup/return; no estate selected or saved");
        }
        _step++;
    }

    private static bool Same(ExpeditionMap3D.MapRect a, ExpeditionMap3D.MapRect b) =>
        a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException("[expedition map check] " + message); }
    private static void Checkpoint(string message) => Plugin.Log.LogInfo("[expedition map check] " + message);
}

internal sealed partial class EmbarkUi
{
    internal void CheckEnableMap() { if (ExpeditionMapCheck.Enabled && ExpeditionPreview.OwnsSession) _map3dPreviewMode = true; }
    internal ExpeditionMap3D.MapRect CheckCamera => _mapCamera.Frame;
    internal string CheckLocation => _mapCamera.Location;
    internal QuestOffer CheckQuest => _quest;
    internal string CheckShown(string location) => _mapAreas.TryGetValue(location, out var area) ? area : null;
    internal bool CheckMapReady => Map3DAvailable;
    internal bool CheckMapWanted => Map3DWanted;
    internal bool CheckProvisioning => _provisioning;
    internal void CheckRestoreParty()
    {
        if (!ExpeditionMapCheck.Enabled || !ExpeditionPreview.OwnsSession) return;
        _party.Clear(); _party.AddRange(E.Roster.Take(4).Select(h => h.Id));
    }
}
