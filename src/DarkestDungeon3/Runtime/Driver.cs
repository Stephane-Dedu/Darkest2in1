using System.Collections.Generic;
using System.Linq;
using Assets.Code.Game;
using DarkestDungeon3.Core;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Dd2;
using UnityEngine;

namespace DarkestDungeon3.Runtime;

internal enum Phase
{
    Off,         // plain DD2
    Hamlet,      // our town screens
    Embarking,   // waiting for the DD2 host run to reach the road
    Recovery,    // a saved expedition needs a retry; preserve it instead of abandoning it
    Crawling,    // our dungeon screens
    Fighting,    // DD2 combat (our UI hidden)
    Homecoming,  // results screen before the Hamlet
}

/// <summary>
/// The DD1 game loop on top of DD2: Hamlet → embark (host a DD2 run) → crawl, handing fights to DD2 → homecoming.
/// Screens call into this; it calls into Core for rules and into the Dd2 bridge for anything in the game.
/// </summary>
internal sealed class Driver : MonoBehaviour
{
    public static Driver Instance { get; private set; }

    public Phase Phase { get; private set; } = Phase.Off;
    public Crawl Crawl { get; private set; }
    public Dd2Party Party { get; private set; }
    public List<string> Log { get; } = new();
    public List<string> HomecomingLog { get; private set; } = new();
    public CurioReport LastCurio => Crawl?.LastCurio;
    /// <summary>How the last expedition ended (the results screen).</summary>
    public HomecomingReport LastReport { get; private set; }
    public string RecoveryMessage { get; private set; }
    public bool CanResumeSaved => ExpeditionRecovery.Inspect(S?.Save) == ExpeditionLoadRoute.Resume;

    /// <summary>The hero shown in the bottom-left banner (DD1: click a hero to select).</summary>
    public string SelectedHeroId;
    /// <summary>For the corridor slide animation: when and which way the party last moved (+1 forward, -1 back).</summary>
    public float LastStepTime { get; private set; } = -10f;
    public int LastStepDir { get; private set; }

    private Session S => Session.Current;
    public ExpeditionState Expedition => S?.Save?.Expedition;

    // Walking to a clicked map destination, one square at a time.
    private readonly Queue<int> _route = new();
    private int _routeTargetRoom = -1;
    private (int corridor, int tile) _routeTargetTile = (-1, -1);

    private void Awake()
    {
        Instance = this;
        Dd2Combat.Finished += OnFightFinished;
    }

    private void OnDestroy()
    {
        Dd2Combat.Finished -= OnFightFinished;
        RegionSceneryArt.Clear();
    }

    public void Say(string line)
    {
        Log.Add(line);
        if (Log.Count > 60) Log.RemoveAt(0);
        Plugin.Log.LogInfo("[dd3] " + line);
    }

    private static readonly float[] RankX = { 788, 620, 452, 284 };

    /// <summary>After DD2 has moved its actors and camera: pose DD1's monsters for this frame.</summary>
    private void LateUpdate()
    {
        if (Phase == Phase.Fighting) { Dd1Backdrop.Tick(); Dd1MonsterView.Render(); }
    }

    private bool _townArtPrimed;

    private void Update()
    {
        Dd2Run.Update();
        if (!_townArtPrimed && S?.Dd1 != null)
        {
            _townArtPrimed = true;
            Art.PrepareTown(S.Dd1);
            // Resolve Unity's cache path on this thread; conversion/extraction only receives plain paths.
            try { Ui.CinematicCache.Prepare(S.Dd1, Core.Dd1.Dd1Cinematic.Opening); }
            catch (System.Exception e) { Plugin.Log.LogWarning("[cinematic] " + e.Message); }
            // Common town art can bake while the player is still at the menu/estate picker.
            foreach (var id in new[] { "ground", "stage_coach", "graveyard", "statue" })
                SpineArt.Get(Core.Campaign.Town.TownLayout.ArtFolder(S.Dd1, id, true, 0), "idle",
                    slot => Core.Campaign.Town.TownLayout.IdleSlot(slot.Name));
        }
        SpineArt.Update();
        Art.Update();
        Ui.CinematicCache.Update();
        ItemText.Prime();
        RegionSceneryArt.Prepare(Phase is Phase.Embarking or Phase.Crawling or Phase.Fighting ? Expedition?.Quest?.Dungeon : null);
        Dd1Audio.Update(Phase, Core.Dungeon.ZoneBase.Of(Expedition?.Quest?.Dungeon), Expedition?.Camp != null,
                        Expedition?.Light ?? 100f, Expedition != null && !Expedition.InRoom);
        if (Phase == Phase.Fighting && Dd2Combat.InFight) Dd1Backdrop.Update();
        var stage = HeroStage.Instance;
        stage?.SetVisible(Phase == Phase.Crawling && Plugin.HeroModels.Value && !HeroStage.RendersBlack);
        if (Phase != Phase.Crawling || Crawl == null) return;
        LockRoadInput();
        if (stage != null && Plugin.HeroModels.Value)
        {
            HeroStage.HeroScale = Plugin.HeroModelScale.Value;
            // Living heroes keep their DD1 rank slots; the dead leave a gap.
            var guids = Expedition.Party.Select(Party.Guid).ToList();
            var alive = guids.Where(g => g != 0 && !Dd2Api.IsDead(g)).ToList();
            var xs = guids.Select((g, i) => (g, x: RankX[System.Math.Min(i, 3)])).Where(t => alive.Contains(t.g)).Select(t => t.x).ToList();
            stage.SetParty(alive, xs);
        }

        var kb = UnityEngine.InputSystem.Keyboard.current;
        Walk(kb);
        HeroStage.WalkSpeed = Expedition.Camp == null && !Expedition.InRoom ? _walkVelocity * SecondsPerSquare : 0f;
        if (kb != null && kb.tKey.wasPressedThisFrame) UseTorch();
    }

    /// <summary>
    /// DD2 steers its stagecoach with the "Driving" input map, still live behind our crawl screen: switch it off
    /// so walking keys never drive the hidden coach. DD2 turns it back on whenever it changes mode.
    /// </summary>
    private void LockRoadInput()
    {
        if (GameModeMgr.CurrentMode != GameModeType.DRIVING) return;
        var input = Assets.Code.Utils.SingletonMonoBehaviour<Assets.Code.Inputs.InputSystemBhv>.Instance;
        input?.SetInputActionMapEnabled(GameModeType.DRIVING.m_inputMapName, false);
    }

    // ---------------- Hamlet ----------------

    public void EnterHamlet(int slot)
    {
        StopWalking();
        Crawl = null; Party = null; SelectedHeroId = null; LastReport = null; RecoveryMessage = null;
        HeroStage.Instance?.Clear(); Log.Clear(); HomecomingLog.Clear();
        Phase = Phase.Off;
        LogDd2Libraries();
        try { S.LoadOrCreate(slot); }
        catch (System.Exception e)
        {
            Plugin.Log.LogError("[session] estate could not load: " + e);
            RecoveryFailed("The saved estate could not be read. Its files have been kept. Return to the main menu and try again.");
            return;
        }
        var route = ExpeditionRecovery.Inspect(S.Save);
        if (route == ExpeditionLoadRoute.Refund)
        {
            var stillborn = Expedition;
            // The party never reached the dungeon (the game closed or failed during embark): call it off.
            S.Save.Estate.Add(Currency.Gold, stillborn.ProvisionCost);
            S.Save.Expedition = null;
            Say($"The expedition never left. {stillborn.ProvisionCost} gold of provisions refunded.");
            S.Persist();
        }
        else if (route == ExpeditionLoadRoute.Resume)
        {
            ResumeExpedition();
            return;
        }
        else if (route == ExpeditionLoadRoute.Results)
        {
            Expedition.Ended = true;
            CompleteHomecoming(ExpeditionParty.Outcomes(Expedition, S.Save.Estate));
            return;
        }
        else if (route == ExpeditionLoadRoute.Invalid)
        {
            RecoveryFailed("The saved dungeon could not be restored. Your expedition has been kept.");
            return;
        }
        Phase = Phase.Hamlet;
    }

    public void ResumeExpedition()
    {
        if (!CanResumeSaved || Phase is Phase.Embarking or Phase.Fighting or Phase.Crawling) return;
        ExpeditionFight.RestoreParty(Expedition);
        Phase = Phase.Embarking;
        RegionSceneryArt.Prepare(Expedition.Quest.Dungeon);
        Say("Returning to the saved expedition.");
        if (!Dd2Run.Start(OnRoadReady, () => RecoveryFailed("The dungeon could not open. Retry when the game is ready.")))
            RecoveryFailed("The game is still changing scenes. Your expedition is saved; try again in a moment.");
    }

    private void RecoveryFailed(string message)
    {
        RecoveryMessage = message;
        Phase = Phase.Recovery;
        Say(message);
    }

    public void LeaveHamlet()
    {
        S?.Persist();
        Phase = Phase.Off;
    }

    private static void LogDd2Libraries()
    {
        var quirks = Assets.Code.Utils.SingletonMonoBehaviour<Assets.Code.Library.Library<string, Assets.Code.Quirk.QuirkDefinition>>.Instance;
        var items = Assets.Code.Utils.SingletonMonoBehaviour<Assets.Code.Library.Library<string, Assets.Code.Item.ItemDefinition>>.Instance;
        Plugin.Log.LogInfo($"[dd2] at the Hamlet: quirk library {(quirks == null ? "missing" : quirks.GetNumberOfLibraryElements().ToString())}, " +
                           $"item library {(items == null ? "missing" : items.GetNumberOfLibraryElements().ToString())}");
    }

    // ---------------- Embark ----------------

    public string Embark(QuestOffer quest, List<HeroRecord> party, Inventory bought, bool opening = false)
    {
        if (Expedition != null) return "The saved expedition must be resumed before setting out again.";
        // This week's town event sets prices and who will go; it changes when the week ends below.
        var hamlet = S.Hamlet;
        var why = Core.Campaign.Embark.WhyCantEmbark(S.Save.Estate, quest, party, hamlet.AnyResolveCanEmbark);
        if (why != null) return why;
        int cost = opening ? 0 : bought.Items.Sum(kv => hamlet.ProvisionPrice(S.Provisioner, S.Content.Items, kv.Key) * kv.Value);   // the opening's pack is DD1's, free
        if (S.Save.Estate.Get(Currency.Gold) < cost) return "Not enough gold for these provisions.";

        S.Save.Estate.Add(Currency.Gold, -cost);
        // DD1's opening raid comes before the first week: no free supplies, no town event buffs, no week passing.
        var exp = Core.Campaign.Embark.Create(S.Campaign, quest, party, bought, opening ? null : S.Provisioner);
        exp.ProvisionCost = cost;
        if (!opening)
            foreach (var buff in hamlet.EmbarkPartyBuffs(quest))
                foreach (var hero in exp.Party)
                {
                    if (!exp.PendingBuffs.TryGetValue(hero, out var list)) exp.PendingBuffs[hero] = list = new List<string>();
                    if (!list.Contains(buff)) list.Add(buff);
                }
        S.Save.Expedition = exp;
        if (opening) S.Save.Estate.OpeningRaidPending = false;
        // DD1 resolves town activities while the party is away.
        else S.Hamlet.EndWeek();
        CampaignJournal.Embark(S.Save.Estate, quest, party);
        S.Persist();

        Phase = Phase.Embarking;
        RegionSceneryArt.Prepare(quest.Dungeon);
        Say($"The party sets out: {quest}.");
        if (!Dd2Run.Start(OnRoadReady, () => RecoveryFailed("The dungeon could not open. Re-open the estate to cancel this departure.")))
            RecoveryFailed("The dungeon could not open. Re-open the estate to cancel this departure.");
        return null;
    }

    /// <summary>A new estate still has DD1's opening raid to play (the road and its bandits).</summary>
    public bool OpeningDue => Phase == Phase.Hamlet && S?.Save?.Estate is { OpeningRaidPending: true } && S.Save.Expedition == null;

    /// <summary>
    /// A new estate's opening, called by the UI once the cinematics are over and the Old Road's loading screen has been
    /// up a moment: the party sets out exactly like any embark (it froze when started from Update right after the
    /// cinematics), before the first week begins.
    /// </summary>
    public void EmbarkOpening()
    {
        if (!OpeningDue) return;
        var opening = S.Hamlet.OpeningRaid();
        if (opening is not { } o) { S.Save.Estate.OpeningRaidPending = false; S.Persist(); return; }
        Plugin.Log.LogInfo($"[opening] {string.Join(" and ", o.Party.Select(h => h.Name))} set out on DD1's opening raid ({o.Quest.Dungeon}, {o.Quest.GoalId})");
        string why = Embark(o.Quest, o.Party, o.Pack, opening: true);
        if (why != null) { Plugin.Log.LogWarning("[opening] " + why); S.Save.Estate.OpeningRaidPending = false; S.Persist(); }
    }

    private void OnRoadReady()
    {
        bool resuming = Expedition.Started;
        var heroes = Expedition.Party.Select(id => S.Save.Estate.Hero(id)).Where(h => h != null).ToList();
        var conditions = resuming ? Expedition.PartyStates : null;
        var expected = heroes.Count(h => ExpeditionParty.HeroForRestore(h,
            conditions != null && conditions.TryGetValue(h.Id, out var saved) ? saved : null) != null);
        var guids = Dd2Heroes.BuildParty(heroes, conditions);
        if (guids.Count != expected || expected == 0)
        {
            RecoveryFailed("The party could not be restored. Your expedition is saved; try again.");
            return;
        }
        Party = new Dd2Party(guids, S.Catalog);
        Crawl = new Crawl(Expedition, S.Rules, Party, S.Content);
        Crawl.HeroDd1Class = id => S.Campaign.HeroUpgrades.Dd1Class(S.Save.Estate.Hero(id)?.ClassId);
        Crawl.EquipmentBuffs = id => S.Save.Estate.Hero(id)?.WornTrinkets.Select(Dd1TrinketData.Get).Where(t => t != null)
            .SelectMany(t => t.BuffIds).Select(S.Content.Buffs.Get).Where(b => b != null);
        Crawl.TrinketOfRarity = (rarity, rng) => S.Catalog.RandomTrinket(rarity, rng);
        CapturePartyState();
        Dd2Api.Torch = Expedition.Light;
        Phase = Phase.Crawling;
        Say($"{(resuming ? "Returned to" : "Entered")} {S.Zones.ZoneName(Expedition.Quest.Dungeon)}.");
        FadedMemoryController.Place(Expedition);
        if (FadedMemory.Active(Expedition)) { StartFight(FightKind.Room, false); return; }
        Handle(resuming ? Crawl.Resume() : Crawl.Begin());
    }

    public void CapturePartyState()
    {
        if (Party == null || Expedition == null || Crawl?.State != Expedition) return;
        var snapshots = new List<ExpeditionHeroState>();
        foreach (var id in Expedition.Party)
            if (S.Save.Estate.Hero(id) is { } hero
                && Dd2Heroes.TryCapture(hero, Party.Guid(id), Expedition.PartyStates.TryGetValue(id, out var previous) ? previous : null, out var snapshot))
                snapshots.Add(snapshot);
        ExpeditionParty.Capture(Expedition, snapshots);
    }

    // ---------------- Crawl ----------------

    public void Travel(int roomId)
    {
        if (!Crawl.CanNavigate) return;
        MarkStep(+1);
        Handle(Crawl.Travel(roomId));
    }

    public void Step(bool forward)
    {
        if (!Crawl.CanNavigate) return;
        if (Crawl.IsBlocked && !forward && Crawl.CurrentTile?.Content == Core.Dungeon.HallContent.Battle) return;
        MarkStep(forward ? +1 : -1);
        Handle(Crawl.Step(forward));
    }

    public bool EnterSecretRoom() => ChangeSecretRoom(enter: true);
    public bool ExitSecretRoom() => ChangeSecretRoom(enter: false);

    private bool ChangeSecretRoom(bool enter)
    {
        if (Phase != Phase.Crawling || Crawl == null || !Crawl.CanNavigate || Ui.UiRoot.ModalOpen || _travelTo >= 0
            || (enter ? !Crawl.CanEnterSecretRoom : Crawl.CurrentRoom?.IsSecret != true)) return false;
        StopWalking(); _mouseWalk = 0; _walkVelocity = 0; WalkProgress = 0;
        int before = Expedition.RoomId;
        Handle(enter ? Crawl.EnterSecretRoom() : Crawl.ExitSecretRoom());
        if (Expedition.RoomId == before) return false;
        _interruptsThisStep = false; // this explicit transition already stopped the route
        _wasInRoom = Expedition.InRoom;
        _fadeInFrom = Time.unscaledTime;
        Dd1Audio.Play("/general/map/room_transition");
        return true;
    }

    private void MarkStep(int dir)
    {
        LastStepDir = dir;
        // Held-key walking scrolls continuously (WalkProgress); only discrete steps get the slide animation.
        LastStepTime = _continuousStep ? -10f : Time.unscaledTime;
    }

    // ---- DD1 walking: hold D/→ to walk the hallway (A/← backs up at half speed); in a room the arrows pick the exit ----

    private const float SecondsPerSquare = 1.2f;
    private bool _continuousStep;

    /// <summary>Where the party stands in its square, from -0.5 (just in from behind) to +0.5 (about to leave
    /// ahead), + toward the heading: the hallway scrolls by this much. It stays put when the party stops, as in DD1;
    /// the next square is entered at the halfway mark.</summary>
    public float WalkProgress { get; private set; }

    /// <summary>The party is walking right now (held key or auto-walk): heroes bob.</summary>
    public bool IsMovingNow => Mathf.Abs(_walkVelocity) > 0.01f || IsWalking || Time.unscaledTime - LastStepTime < 0.3f;
    private float _walkVelocity;

    /// <summary>
    /// DD1 walking. In a hallway the party walks continuously: hold D/→ (A/← backs up at half speed), or follow a
    /// route picked on the map. Squares are entered as the party walks into them. Rooms are left through a fade, by
    /// the arrows (the exit that way on the map) or by the route.
    /// </summary>
    private void Walk(UnityEngine.InputSystem.Keyboard kb)
    {
        _walkVelocity = 0;
        if (!Crawl.CanNavigate || Ui.UiRoot.ModalOpen)
        {
            _mouseWalk = 0;
            _travelTo = -1;
            StopWalking();
            return;
        }
        if (Expedition.InRoom != _wasInRoom) { _wasInRoom = Expedition.InRoom; _fadeInFrom = Time.unscaledTime; }
        if (_travelTo >= 0)
        {
            WalkProgress = 0;
            if (Time.unscaledTime - _travelFrom < FadeSeconds) return;   // fading out of the room
            int to = _travelTo;
            _travelTo = -1;
            Travel(to);
            WalkProgress = Expedition.InRoom ? 0 : -0.45f;   // just in through the door
            _wasInRoom = Expedition.InRoom;
            _fadeInFrom = Time.unscaledTime;
            if (_interruptsThisStep) { StopWalking(); _interruptsThisStep = false; }
            return;
        }
        int mouse = MouseWalk();
        bool right = mouse > 0 || (kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed));
        bool left = mouse < 0 || (kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed));

        if (Expedition.InRoom)
        {
            WalkProgress = 0;
            if (Crawl.IsBlocked) { StopWalking(); return; }
            bool rightNow = kb != null && (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame);
            bool leftNow = kb != null && (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame);
            bool up = kb != null && (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame);
            bool down = kb != null && (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame);
            Vector2 dir = rightNow ? Vector2.right : leftNow ? Vector2.left : up ? Vector2.down : down ? Vector2.up : Vector2.zero;
            if (Crawl.CurrentRoom.IsSecret && dir != Vector2.zero) { ExitSecretRoom(); return; }
            if (dir != Vector2.zero && Crawl.CurioHere == null)
            {
                int exit = ExitToward(dir);
                if (exit >= 0) { StopWalking(); BeginTravel(exit); }
                return;
            }
            if (!IsWalking) return;
            if (_routeTargetTile.corridor >= 0) { BeginTravel(Expedition.Map.Corridor(_routeTargetTile.corridor).Other(Expedition.RoomId)); return; }
            while (_route.Count > 0 && _route.Peek() == Expedition.RoomId) _route.Dequeue();
            if (_route.Count == 0) { StopWalking(); return; }
            BeginTravel(_route.Peek());
            return;
        }

        int intent = right ^ left ? (right ? 1 : -1) : 0;
        if (intent != 0) StopWalking();
        else if (IsWalking) intent = RouteIntent();
        if (intent == 0) return;   // the party stays where it stopped
        bool forward = intent > 0;
        if (forward && Crawl.IsBlocked)   // an obstacle or a fight in this square: no further than its middle
        {
            WalkProgress = Mathf.Min(WalkProgress, 0f);
            StopWalking();
            return;
        }
        float speed = (forward ? 1f : -0.5f) / SecondsPerSquare;
        _walkVelocity = speed;
        if (Time.unscaledTime >= _nextFootstep) { Dd1Audio.Play("/general/party/hero_step"); _nextFootstep = Time.unscaledTime + (forward ? 0.55f : 0.8f); }
        WalkProgress += speed * Time.unscaledDeltaTime;
        if (Mathf.Abs(WalkProgress) < 0.5f) return;
        float carry = WalkProgress - Mathf.Sign(WalkProgress);   // -0.5 + overshoot in the next square
        _continuousStep = true;
        Step(forward);
        _continuousStep = false;
        WalkProgress = Expedition.InRoom ? 0 : carry;
        if (Expedition.InRoom) Dd1Audio.Play("/general/map/room_transition");
        // Something happened (a fight, a curio, a trap, a room): stop and let the player look.
        if (_interruptsThisStep || (IsWalking && Crawl.CurioHere != null)) { _interruptsThisStep = false; StopWalking(); }
        if (_routeTargetTile.corridor >= 0 && !Expedition.InRoom && Expedition.TileIndex == _routeTargetTile.tile) StopWalking();
    }

    private int _mouseWalk;

    /// <summary>DD1: hold the mouse button at the right of the hallway to walk on, at the left to back up.</summary>
    private int MouseWalk()
    {
        var m = UnityEngine.InputSystem.Mouse.current;
        if (m == null || !m.leftButton.isPressed || Ui.Drag.Active) return _mouseWalk = 0;
        if (m.leftButton.wasPressedThisFrame)
        {
            var p = m.position.ReadValue();
            float x = p.x * 1920f / Screen.width, y = (Screen.height - p.y) * 1080f / Screen.height;
            _mouseWalk = y > 160 && y < 700 ? (x > 1210 ? 1 : x < 200 ? -1 : 0) : 0;
        }
        return _mouseWalk;
    }

    /// <summary>Which way the map route goes from here in the hallway: +1 ahead, -1 back, 0 arrived.</summary>
    private int RouteIntent()
    {
        var c = Crawl.CurrentCorridor;
        if (_routeTargetTile.corridor >= 0)
        {
            int idx = _routeTargetTile.tile;
            if (_routeTargetTile.corridor != c.Id || Expedition.TileIndex == idx) { StopWalking(); return 0; }
            return (idx > Expedition.TileIndex) == (Expedition.HeadingRoomId == c.RoomB) ? 1 : -1;
        }
        int next = _route.Count > 0 ? _route.Peek() : _routeTargetRoom;
        if (next < 0) { StopWalking(); return 0; }
        return Expedition.HeadingRoomId == next ? 1 : -1;
    }

    // ---- DD1's fades: out of a room into a hallway, and into a room at the end of one ----

    private const float FadeSeconds = 0.3f;
    private int _travelTo = -1;
    private float _travelFrom, _fadeInFrom = -10f;
    private bool _wasInRoom;

    private float _nextFootstep;

    private void BeginTravel(int roomId)
    {
        Dd1Audio.Play("/general/map/room_transition");
        _travelTo = roomId;
        _travelFrom = Time.unscaledTime;
    }

    /// <summary>How dark the scene is from a transition (0 clear .. 1 black).</summary>
    public float FadeAlpha
    {
        get
        {
            float now = Time.unscaledTime;
            if (_travelTo >= 0) return Mathf.Clamp01((now - _travelFrom) / FadeSeconds);
            float fadeIn = 1f - Mathf.Clamp01((now - _fadeInFrom) / (FadeSeconds * 1.4f));
            // Walking into a door at either end of the hallway: the room fades in.
            float door = 0f;
            if (Crawl != null && !Expedition.InRoom && _walkVelocity != 0)
            {
                var c = Crawl.CurrentCorridor;
                bool towardB = Expedition.HeadingRoomId == c.RoomB;
                int last = towardB ? c.Tiles.Count - 1 : 0, first = towardB ? 0 : c.Tiles.Count - 1;
                if (_walkVelocity > 0 && Expedition.TileIndex == last && !Crawl.IsBlocked) door = (WalkProgress - 0.1f) / 0.4f;
                if (_walkVelocity < 0 && Expedition.TileIndex == first) door = (-WalkProgress - 0.1f) / 0.4f;
            }
            return Mathf.Clamp01(Mathf.Max(fadeIn, door));
        }
    }

    private bool _interruptsThisStep;

    /// <summary>The room exit closest to a direction on the map (DD1 rooms are left by their doors to the map's neighbours).</summary>
    private int ExitToward(Vector2 dir)
    {
        var map = Expedition.Map;
        var here = map.Room(Expedition.RoomId);
        int best = -1;
        float bestScore = 0.3f;
        foreach (int n in map.Neighbours(here.Id))
        {
            var r = map.Room(n);
            var delta = new Vector2(r.X - here.X, r.Y - here.Y);
            if (delta == Vector2.zero) continue;
            float score = Vector2.Dot(delta.normalized, dir);
            if (score > bestScore) { bestScore = score; best = n; }
        }
        return best;
    }

    public void UseTorch()
    {
        float before = Expedition.Light;
        Handle(Crawl.UseTorch());
        if (Expedition.Light > before) Ui.Gui.Announce("The torch flares.");
    }

    // ---- walking to a map destination (DD1: click a room or hall square on the map) ----

    public bool IsWalking => _route.Count > 0 || _routeTargetRoom >= 0 || _routeTargetTile.corridor >= 0;

    public void StopWalking()
    {
        _route.Clear();
        _routeTargetRoom = -1;
        _routeTargetTile = (-1, -1);
    }

    /// <summary>Walk to a room: through the rooms on the shortest path, square by square.</summary>
    public void WalkToRoom(int target)
    {
        StopWalking();
        if (!Crawl.CanNavigate) return;
        var map = Expedition.Map;
        if (target < 0 || target >= map.Rooms.Count || target == Expedition.RoomId) return;
        if (Crawl.CurrentRoom?.IsSecret == true && !ExitSecretRoom()) return;
        var destination = map.Room(target);
        if (destination.IsSecret)
        {
            if (!destination.Scouted && !destination.Visited) return;
            if (Crawl.CanEnterSecretRoom && Crawl.CurrentTile.SecretRoomId == target) { EnterSecretRoom(); return; }
            var entrance = map.SecretEntrance(target);
            if (entrance.Corridor == null) return;
            WalkToTile(entrance.Corridor.Id, entrance.Tile.Index);
            if (!IsWalking) Ui.Gui.Announce("Approach the marked corridor square to enter the secret room.");
            return;
        }
        int from = Expedition.InRoom ? Expedition.RoomId : NearestEnd(target);
        foreach (int r in RoomPath(map, from, target)) _route.Enqueue(r);
        _routeTargetRoom = target;
    }

    /// <summary>Walk to a square of the corridor the party is in, or of a corridor next to its room.</summary>
    public void WalkToTile(int corridorId, int tileIndex)
    {
        StopWalking();
        if (!Crawl.CanNavigate) return;
        if (Crawl.CurrentRoom?.IsSecret == true && !ExitSecretRoom()) return;
        var c = Expedition.Map.Corridor(corridorId);
        if (Expedition.InRoom && c.RoomA != Expedition.RoomId && c.RoomB != Expedition.RoomId) return;
        if (!Expedition.InRoom && Expedition.CorridorId != corridorId) return;
        _routeTargetTile = (corridorId, tileIndex);
    }

    private int NearestEnd(int target)
    {
        var c = Crawl.CurrentCorridor;
        var map = Expedition.Map;
        return map.Distances(c.RoomA)[target] <= map.Distances(c.RoomB)[target] ? c.RoomA : c.RoomB;
    }

    private static List<int> RoomPath(Core.Dungeon.DungeonMap map, int from, int to)
    {
        var prev = new Dictionary<int, int> { [from] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int r = queue.Dequeue();
            if (r == to) break;
            foreach (int n in map.Neighbours(r))
                if (!prev.ContainsKey(n)) { prev[n] = r; queue.Enqueue(n); }
        }
        var path = new List<int>();
        if (!prev.ContainsKey(to)) return path;
        for (int at = to; at != -1; at = prev[at]) path.Add(at);
        path.Reverse();
        return path;   // starts with `from`
    }

    public void Fight()
    {
        if (Crawl == null || !Crawl.IsBlocked) return;
        bool inRoom = Expedition.InRoom;
        var room = inRoom ? Crawl.CurrentRoom : null;
        var kind = room is { Content: Core.Dungeon.RoomContent.Boss } ? FightKind.Boss : inRoom ? FightKind.Room : FightKind.Hall;
        StartFight(kind, heroesSurprised: false);
    }

    private int _debugFights;

    /// <summary>Testing (F11): start a hall fight right here; alternately the heroes and the monsters are surprised.</summary>
    public void DebugFight()
    {
        if (Phase != Phase.Crawling) return;
        bool heroes = _debugFights++ % 2 == 0;
        StartFight(FightKind.Hall, heroesSurprised: heroes, monstersSurprised: !heroes);
    }

    private void StartFight(FightKind kind, bool heroesSurprised, bool monstersSurprised = false)
    {
        var quest = Expedition.Quest;
        var rng = new Rng(Expedition.Seed * 7 + Expedition.BattlesWon * 131 + Expedition.StepsTaken);
        var plan = S.Zones.Plan(quest.Dungeon, quest.Difficulty, kind, rng, quest.BossId);
        var checkpoint = ExpeditionFight.Current(Expedition);
        bool memory = FadedMemory.Active(Expedition);
        if (memory && !FadedMemoryController.TryPlan(Expedition, Party, out plan, out _))
        {
            RecoveryFailed("The memory is waiting for its encounter resources. Your return is saved.");
            return;
        }
        // DD1's encounter tables pick the monsters; DD2 look-alikes fight in their place (bosses keep DD2's battles,
        // and DD2's regions keep their own natives).
        if (!memory && checkpoint == null && kind != FightKind.Boss && S.Bestiary != null && !Core.Dungeon.ZoneBase.IsExtra(quest.Dungeon))
        {
            var monsters = Crawl.FightMonsters(kind == FightKind.Room ? "room" : "hall");
            plan.Enemies = S.Bestiary.Translate(monsters, rng, Dd2Combat.EnemySize);
            Plugin.Log.LogInfo($"[combat] DD1 encounter [{string.Join(", ", monsters)}] -> {(plan.Enemies != null ? string.Join(", ", plan.Enemies) : "zone table")}");
        }
        if (checkpoint != null)
        {
            plan = checkpoint.Plan();
            heroesSurprised = checkpoint.HeroesSurprised;
            monstersSurprised = checkpoint.MonstersSurprised;
        }
        CapturePartyState();
        var guids = Expedition.Party.Select(Party.Guid).Where(g => g != 0 && !Dd2Api.IsDead(g)).ToList();
        var buffs = Crawl.FightBuffs().Select(b => (Party.Guid(b.Hero), b.Buff)).Where(b => b.Item1 != 0).ToList();
        if (Dd2Combat.Start(plan, guids, Expedition.Light, heroesSurprised, buffs, monstersSurprised,
                (battle, arena) =>
                {
                    if (checkpoint == null && !ExpeditionFight.Record(Expedition, plan, battle, arena, heroesSurprised, monstersSurprised))
                    {
                        Say("The fight is waiting for a complete party checkpoint.");
                        return false;
                    }
                    return S.Persist();
                }))
        {
            // DD1's own monster art over the DD2 stand-ins (only for a translated DD1 encounter).
            Dd1Audio.Play(heroesSurprised ? "/general/combat/ambush" : "/general/combat/start");
            Dd1MonsterView.Prepare(memory ? new[] { Expedition.FadedMemory.BossId } : plan.Enemies != null && Dd2Combat.LastBattleId == "dd3_dd1_encounter" ? Expedition.FightMonsters : null, plan.Enemies);
            if (memory) Dd1MonsterView.PrepareHeroes(FadedMemoryController.HeroArt(Expedition, Party));
            Phase = Phase.Fighting;
            S.Persist();
        }
        else if (memory) RecoveryFailed("The memory could not open. Retry the saved expedition when the game is ready.");
        else Say("The fight could not start (see the log).");
    }

    private void OnFightFinished(bool wiped)
    {
        if (Phase != Phase.Fighting) return;
        Expedition.Light = Dd2Api.Torch;
        if (wiped || Party.Alive.Count == 0)
        {
            Say("The party has fallen.");
            Expedition.Ended = true;
            FinishExpedition();
            return;
        }
        if (Dd2Combat.Retreated)
        {
            if (FadedMemory.Active(Expedition)) { ReturnFromMemory(false); return; }
            // DD1: the fight stays where it was; the party falls back the way it came.
            Handle(Crawl.FleeBattle());
            Dd1Audio.Play("/general/combat/retreat");
            Announce("The party retreats!");
            Phase = Phase.Crawling;
            S.Persist();
            return;
        }
        if (FadedMemory.Active(Expedition)) { ReturnFromMemory(true); return; }
        Handle(Crawl.ResolveBattle());
        Dd1Audio.Play("/general/combat/victory");
        if (Crawl.LastSpoils is { } spoils)
            Plugin.Log.LogInfo($"[loot] {spoils.Kind} fight ({string.Join(" ", spoils.Dd1Monsters)}): " +
                               $"took {string.Join(", ", spoils.Taken)}; left {string.Join(", ", spoils.LeftBehind)}");
        Phase = Phase.Crawling;
        S.Persist();
    }

    public void ClearObstacle() => Handle(Crawl.ClearObstacle());
    /// <summary>DD1: the selected hero tries to disarm the spotted trap.</summary>
    public void DisarmTrap() => Handle(Crawl.DisarmTrap(SelectedHeroId));

    public void Investigate(string heroId, string itemId)
    {
        if (Crawl?.CurioHere == FadedMemory.CurioId)
        {
            if (Phase != Phase.Crawling || !Crawl.CanNavigate || Party?.Alive.Contains(heroId) != true) return;
            if (itemId != null) { Say("No offering is required. Confront the past by hand."); return; }
            if (!FadedMemoryController.Ready(Expedition, Party)) { Say("This memory cannot yet take form."); return; }
            if (!FadedMemory.Enter(Expedition, heroId, null)) return;
            if (!S.Persist()) { Expedition.FadedMemory.Stage = "ready"; return; }
            StopWalking();
            Say("What was buried has endured.");
            StartFight(FightKind.Room, false);
            return;
        }
        var report = Crawl.InteractCurio(heroId, itemId, out var overflow);
        if (report != null)
        {
            Say($"{S.Save.Estate.Hero(heroId)?.Name}: {report.Text ?? report.OutcomeType}" +
                (report.Loot.Count > 0 ? " Found " + string.Join(", ", report.Loot) + "." : ""));
            foreach (var drop in overflow) Say($"No room for {drop}: make room in the pack to take it.");
            if (Expedition.QuestComplete) Say("The quest is complete! You may return to the Hamlet.");
        }
        S.Persist();
    }

    private void ReturnFromMemory(bool victory)
    {
        if (!FadedMemory.Finish(Expedition, S.Content.Items, victory))
        {
            RecoveryFailed("The memory's return checkpoint needs repair. The expedition remains saved.");
            return;
        }
        Phase = Phase.Crawling;
        Dd1Audio.Play(victory ? "/general/combat/victory" : "/general/combat/retreat");
        Say(Expedition.PendingCurio.Text);
        S.Persist();
        Plugin.Log.LogInfo($"[memory] returned: {Expedition.FadedMemory.Stage}; rewards {string.Join(",", Expedition.PendingCurio.Loot)}; quest complete {Expedition.QuestComplete}");
    }

    public void DebugMemory()
    {
        if (System.IO.Path.GetFileName(S?.SavePath) != "estate_2.json" || Phase != Phase.Crawling
            || Crawl?.CanNavigate != true || !Expedition.InRoom || Crawl.IsBlocked || FadedMemory.Active(Expedition)) return;
        Expedition.FadedMemory = null;
        FadedMemory.Place(Expedition, "crypts", "necromancer_A", 1, test: true);
        S.Persist();
        Say("Faded Memory placed here. Click the mirror, then Confront the past.");
    }

    public void DismissCurio(CurioReport report)
    {
        if (Crawl?.DismissCurio(report) == true) S.Persist();
    }

    public void SkipCurio() => Crawl.SkipCurio();

    public void MakeCamp()
    {
        if (!Crawl.CanCamp) return;
        Dd1Audio.Play("/general/map/camp_start");
        Handle(Crawl.MakeCamp());
    }
    public bool EatMeal(Meal meal)
    {
        if (!Crawl.EatMeal(meal)) return false;
        S.Persist();
        return true;
    }
    public bool UseCampSkill(string hero, string skill, string target)
    {
        if (!Crawl.UseCampSkill(hero, skill, target)) return false;
        S.Persist();
        return true;
    }
    public void DismissSpoils(BattleSpoils report)
    {
        if (Crawl?.DismissSpoils(report) == true) S.Persist();
    }
    public void BreakCamp()
    {
        if (!Crawl.CanContinueCamp) return;
        Dd1Audio.Play("/general/map/camp_end");
        Handle(Crawl.BreakCamp());
    }

    /// <summary>Leave the dungeon: after the quest is done, or as a retreat before it is.</summary>
    public void Leave()
    {
        if (Crawl?.TryLeave() != true) { Say("The expedition cannot be left yet."); return; }
        if (Expedition.Retreated) Say("The party retreats.");
        FinishExpedition();
    }

    /// <summary>Events that should stop an auto-walk (DD1 stops for anything you need to look at).</summary>
    private int _interrupts;

    private void Handle(List<CrawlEvent> events)
    {
        Dd2Api.Torch = Expedition.Light;
        FightKind? fight = null;
        bool surprised = false, monstersSurprised = false;
        foreach (var e in events)
        {
            switch (e.Type)
            {
                case CrawlEventType.Battle:
                    // DD1 starts the fight the moment the party walks into it.
                    var room = e.RoomId >= 0 ? Expedition.Map.Room(e.RoomId) : null;
                    fight = room?.Content == Core.Dungeon.RoomContent.Boss ? FightKind.Boss : e.RoomId >= 0 ? FightKind.Room : FightKind.Hall;
                    surprised = e.HeroesSurprised;
                    monstersSurprised = e.MonstersSurprised;
                    Announce(e.HeroesSurprised ? "Ambush! The heroes are surprised!" : e.MonstersSurprised ? "The enemy is caught unawares!" : "Enemies ahead!");
                    break;
                case CrawlEventType.Ambush:
                    fight = e.ContentId == "camp" ? FightKind.CampAmbush : FightKind.Hall;
                    surprised = e.HeroesSurprised;
                    monstersSurprised = e.MonstersSurprised;
                    Announce(e.ContentId == "camp" ? "The camp is ambushed in the night!" : "Something stirs in the dark...");
                    break;
                case CrawlEventType.TrapSprung:
                    Dd1Audio.Play("prop_trap_" + (e.ContentId ?? "spikes").Replace("_", ""));
                    Announce($"{S.Save.Estate.Hero(e.HeroId)?.Name ?? "The party"} springs the trap!");
                    break;
                case CrawlEventType.TrapDisarmed:
                    string zone = Core.Dungeon.ZoneBase.Of(Expedition.Quest?.Dungeon);
                    Dd1Audio.Play(Dd1Audio.Has("prop_trap_disarm_" + zone) ? "prop_trap_disarm_" + zone : "prop_trap_disarm");
                    Announce($"{S.Save.Estate.Hero(e.HeroId)?.Name ?? "The party"} disarms the trap.");
                    break;
                case CrawlEventType.Trap: Announce("A trap lies ahead."); break;
                case CrawlEventType.Obstacle: Announce("The way is blocked."); break;
                case CrawlEventType.ObstacleCleared: Say("The way is clear."); break;
                case CrawlEventType.Curio: _interrupts++; Say($"There is something here: {e.ContentId}."); break;
                case CrawlEventType.Ate: Announce("The party eats."); break;
                case CrawlEventType.Starving: Announce("There is no food. The party starves!"); break;
                case CrawlEventType.Scouted: Announce("Scouting reveals the way ahead."); break;
                case CrawlEventType.QuestComplete: Announce("Quest complete! You may return to the Hamlet."); break;
                case CrawlEventType.Stress: break;
            }
        }
        S.Persist();
        if (fight != null) StartFight(fight.Value, surprised, monstersSurprised);
    }

    private void Announce(string text)
    {
        _interrupts++;
        _interruptsThisStep = true;
        Say(text);
        Ui.Gui.Announce(text);
    }

    // ---------------- Homecoming ----------------

    private void FinishExpedition()
    {
        CapturePartyState();
        CompleteHomecoming(ExpeditionParty.Outcomes(Expedition, S.Save.Estate));
    }

    private void CompleteHomecoming(List<HeroOutcome> outcomes)
    {
        var exp = Expedition;
        var rng = new Rng(exp.Seed * 31 + exp.StepsTaken);
        LastReport = Homecoming.Report(S.Save.Estate, S.Campaign, exp, outcomes, S.Content.Items, rarity => S.Catalog.RandomTrinket(rarity, rng), Crawl?.EquipmentBuffs);
        HomecomingLog = LastReport.Log;
        S.Save.Expedition = null;
        S.Persist();
        Crawl = null;
        Party = null;
        HeroStage.Instance?.Clear();
        Phase = Phase.Homecoming;
        if (Dd2Run.Hosting) Dd2Run.End();
    }

    public void BackToHamlet() => Phase = Phase.Hamlet;
}
