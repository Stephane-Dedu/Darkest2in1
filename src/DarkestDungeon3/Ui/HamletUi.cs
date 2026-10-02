using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>The Hamlet: roster, buildings, upgrades, and the way to the quest board.</summary>
internal sealed class HamletUi
{
    private enum Panel { Town, StageCoach, Abbey, Tavern, Sanitarium, Wagon, Graveyard, Upgrades, Hero }

    private Panel _panel = Panel.Town;
    private string _heroId;
    private string _upgradeBuilding = Buildings.StageCoach;
    private Vector2 _rosterScroll, _panelScroll, _logScroll;

    public bool WantsEmbark;

    private static Session S => Session.Current;
    private static Estate E => S.Save.Estate;

    public void Draw()
    {
        var hamlet = S.Hamlet;
        Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), Color.black);
        Gui.Image(new Rect(0, 0, Gui.W, Gui.H), Art.TownBackdrop);

        // Top bar: estate and resources.
        Gui.Panel(new Rect(0, 0, Gui.W, 64));
        Gui.Title(new Rect(20, 10, 600, 50), $"{E.Name} — Week {E.Week + 1}");
        Gui.Label(new Rect(640, 18, 1260, 40),
            $"{Gui.Colour("Gold", Gui.Gold)} {E.Get(Currency.Gold)}   Busts {E.Get(Currency.Bust)}   Portraits {E.Get(Currency.Portrait)}   " +
            $"Deeds {E.Get(Currency.Deed)}   Crests {E.Get(Currency.Crest)}   Trinkets {E.Trinkets.Count}   Quests done {E.QuestsCompleted - 1}");

        DrawRoster(hamlet);
        DrawBuildingBar(hamlet);

        var area = new Rect(470, 80, 1000, 840);
        switch (_panel)
        {
            case Panel.Town: DrawTownLog(area); break;
            case Panel.StageCoach: DrawStageCoach(area, hamlet); break;
            case Panel.Abbey: DrawActivities(area, hamlet, Buildings.Abbey); break;
            case Panel.Tavern: DrawActivities(area, hamlet, Buildings.Tavern); break;
            case Panel.Sanitarium: DrawSanitarium(area, hamlet); break;
            case Panel.Wagon: DrawWagon(area, hamlet); break;
            case Panel.Graveyard: DrawGraveyard(area); break;
            case Panel.Upgrades: DrawUpgrades(area, hamlet); break;
            case Panel.Hero: DrawHero(area); break;
        }

        if (Gui.Button(new Rect(1500, 960, 400, 90), Gui.Colour("<b>Embark</b>", Gui.Gold) + "\nChoose a quest and a party"))
            WantsEmbark = true;
        if (Gui.Button(new Rect(20, 1000, 300, 60), "Leave the Hamlet"))
            Driver.Instance.LeaveHamlet();
    }

    private void DrawRoster(Hamlet hamlet)
    {
        var r = new Rect(10, 80, 450, 900);
        Gui.Panel(r);
        Gui.Label(new Rect(20, 85, 430, 30), $"<b>Roster</b> {E.Roster.Count}/{S.Buildings.RosterSize(E)}");
        _rosterScroll = GUI.BeginScrollView(new Rect(15, 120, 440, 850), _rosterScroll, new Rect(0, 0, 410, E.Roster.Count * 74));
        for (int i = 0; i < E.Roster.Count; i++)
        {
            var h = E.Roster[i];
            string status = h.MissingWeeks > 0 ? Gui.Colour("missing", Gui.Blood)
                : h.Activity != null ? Gui.Colour(h.Activity, Gui.Dim) : "";
            if (Gui.Button(new Rect(0, i * 74, 410, 70),
                    $"<b>{h.Name}</b>  {Pretty(h.ClassId)}  {Gui.Colour("Lv " + h.ResolveLevel, Gui.Gold)}\nStress {h.Stress}/10  {status}"))
            {
                _heroId = h.Id;
                _panel = Panel.Hero;
            }
        }
        GUI.EndScrollView();
    }

    private void DrawBuildingBar(Hamlet hamlet)
    {
        var buttons = new (Panel p, string building, string label)[]
        {
            (Panel.Town, null, "Town"), (Panel.StageCoach, Buildings.StageCoach, "Stagecoach"), (Panel.Abbey, Buildings.Abbey, "Abbey"),
            (Panel.Tavern, Buildings.Tavern, "Tavern"), (Panel.Sanitarium, Buildings.Sanitarium, "Sanitarium"),
            (Panel.Wagon, Buildings.NomadWagon, "Nomad Wagon"), (Panel.Graveyard, Buildings.Graveyard, "Graveyard"), (Panel.Upgrades, null, "Upgrades"),
        };
        for (int i = 0; i < buttons.Length; i++)
        {
            var (p, building, label) = buttons[i];
            bool open = building == null || S.Buildings.IsOpen(building, E);
            var rect = new Rect(1490, 80 + i * 105, 420, 98);
            Gui.Panel(rect);
            if (building != null) Gui.Image(new Rect(rect.x + 4, rect.y + 4, 90, 90), Art.BuildingIcon(building), ScaleMode.ScaleToFit);
            if (Gui.Button(new Rect(rect.x + 100, rect.y + 14, 310, 70), open ? label : label + "\n" + Gui.Colour("(not yet open)", Gui.Dim), open))
                _panel = p;
        }
    }

    private void DrawTownLog(Rect area)
    {
        Gui.Panel(area);
        Gui.Title(new Rect(area.x + 20, area.y + 10, 900, 50), "The Hamlet");
        var lines = E.TownLog.Concat(Driver.Instance.HomecomingLog).ToList();
        _logScroll = GUI.BeginScrollView(new Rect(area.x + 20, area.y + 70, area.width - 40, area.height - 90), _logScroll, new Rect(0, 0, area.width - 70, lines.Count * 34 + 40));
        if (lines.Count == 0) Gui.Label(new Rect(0, 0, area.width - 70, 30), "All is quiet. Choose a quest and embark when ready.");
        for (int i = 0; i < lines.Count; i++) Gui.Label(new Rect(0, i * 34, area.width - 70, 32), lines[i]);
        GUI.EndScrollView();
    }

    private void DrawStageCoach(Rect area, Hamlet hamlet)
    {
        Gui.Panel(area);
        Gui.Title(new Rect(area.x + 20, area.y + 10, 900, 50), "The Stagecoach");
        Gui.Label(new Rect(area.x + 20, area.y + 60, 900, 30),
            $"New recruits arrive each week. Roster: {E.Roster.Count}/{S.Buildings.RosterSize(E)}");
        for (int i = 0; i < E.Recruits.Count; i++)
        {
            var h = E.Recruits[i];
            var r = new Rect(area.x + 20, area.y + 110 + i * 110, area.width - 40, 100);
            Gui.Panel(r);
            Gui.Label(new Rect(r.x + 10, r.y + 8, 640, 90),
                $"<b>{h.Name}</b> the {Pretty(h.ClassId)}  {Gui.Colour("Lv " + h.ResolveLevel, Gui.Gold)}\n" +
                $"Quirks: {string.Join(", ", h.Quirks.Select(Pretty))}\nCamp: {string.Join(", ", h.CampingSkills.Select(Pretty))}");
            if (Gui.Button(new Rect(r.xMax - 210, r.y + 20, 190, 60), "Recruit", hamlet.CanRecruit))
                hamlet.Recruit(h.Id);
        }
    }

    private string _assignHero;

    private void DrawActivities(Rect area, Hamlet hamlet, string building)
    {
        Gui.Panel(area);
        Gui.Title(new Rect(area.x + 20, area.y + 10, 900, 50), Pretty(building));
        var idle = E.Roster.Where(h => h.IsAvailable).ToList();
        Gui.Small(new Rect(area.x + 20, area.y + 60, 960, 60), "Pick a hero, then an activity. Heroes stay for the week and leave less stressed (and sometimes worse for it).");
        for (int i = 0; i < idle.Count; i++)
            if (Gui.Button(new Rect(area.x + 20 + (i % 5) * 192, area.y + 110 + (i / 5) * 56, 186, 50),
                    (_assignHero == idle[i].Id ? "▶ " : "") + idle[i].Name + $" ({idle[i].Stress})"))
                _assignHero = idle[i].Id;

        int y = (int)area.y + 240;
        foreach (var a in S.Buildings.Activities.Where(a => a.Building == building))
        {
            var cost = a.Cost(E);
            var (lo, hi) = a.StressHeal(E);
            var r = new Rect(area.x + 20, y, area.width - 40, 120);
            Gui.Panel(r);
            var inside = E.Roster.Where(h => h.Activity == a.Key).Select(h => h.Name);
            Gui.Label(new Rect(r.x + 10, r.y + 8, 650, 110),
                $"<b>{Pretty(a.Id)}</b>  — {cost?.Amount} {cost?.Type}, slots {hamlet.UsedSlots(a.Key)}/{a.Slots(E)}, relieves {lo}-{hi} (DD1) ≈ {lo / 10f:0.#} stress\n" +
                $"Inside: {string.Join(", ", inside)}");
            var hero = E.Hero(_assignHero);
            string why = hero == null ? "Pick a hero" : hamlet.WhyCantDo(hero, a);
            if (Gui.Button(new Rect(r.xMax - 260, r.y + 30, 240, 60), why ?? $"Send {hero.Name}", why == null))
            {
                hamlet.StartActivity(hero.Id, a.Key);
                _assignHero = null;
                S.Persist();
            }
            y += 130;
        }
    }

    private void DrawSanitarium(Rect area, Hamlet hamlet)
    {
        Gui.Panel(area);
        Gui.Title(new Rect(area.x + 20, area.y + 10, 900, 50), "The Sanitarium");
        Gui.Small(new Rect(area.x + 20, area.y + 60, 960, 40), "Treat a negative quirk or disease, or lock in a positive quirk. Treatment takes the week.");
        _panelScroll = GUI.BeginScrollView(new Rect(area.x + 20, area.y + 100, area.width - 40, area.height - 120), _panelScroll,
            new Rect(0, 0, area.width - 70, E.Roster.Sum(h => 50 + h.Quirks.Count * 46)));
        float y = 0;
        foreach (var h in E.Roster)
        {
            Gui.Label(new Rect(0, y, 800, 40), $"<b>{h.Name}</b> {(h.IsAvailable ? "" : Gui.Colour("(busy)", Gui.Dim))}");
            y += 44;
            foreach (var q in h.Quirks)
            {
                var cost = hamlet.TreatmentCost(h, q);
                bool locked = h.LockedQuirks.Contains(q);
                Gui.Small(new Rect(30, y, 560, 40), $"{Pretty(q)} {(S.Catalog.IsPositive(q) ? Gui.Colour("+", Gui.Gold) : Gui.Colour("−", Gui.Blood))}{(locked ? " (locked)" : "")}");
                if (h.IsAvailable && cost != null && Gui.Button(new Rect(600, y, 300, 40), $"{(S.Catalog.IsPositive(q) ? "Lock" : "Treat")} ({cost.Amount} {cost.Type})",
                        !(locked && S.Catalog.IsPositive(q))))
                {
                    hamlet.StartTreatment(h.Id, q);
                    S.Persist();
                }
                y += 46;
            }
            y += 6;
        }
        GUI.EndScrollView();
    }

    private void DrawWagon(Rect area, Hamlet hamlet)
    {
        Gui.Panel(area);
        Gui.Title(new Rect(area.x + 20, area.y + 10, 900, 50), "The Nomad Wagon");
        for (int i = 0; i < E.WagonStock.Count; i++)
        {
            string t = E.WagonStock[i];
            int price = hamlet.WagonPrice(t);
            Gui.Label(new Rect(area.x + 30, area.y + 80 + i * 60, 600, 50), Pretty(t));
            if (Gui.Button(new Rect(area.x + 650, area.y + 76 + i * 60, 300, 52), $"Buy ({price} gold)", E.Get(Currency.Gold) >= price))
            {
                hamlet.BuyTrinket(t);
                S.Persist();
                break;
            }
        }
        Gui.Label(new Rect(area.x + 30, area.y + area.height - 120, 940, 100),
            $"Your trinkets: {string.Join(", ", E.Trinkets.Select(Pretty))}\nEquip them from a hero's page (click a hero in the roster).");
    }

    private void DrawGraveyard(Rect area)
    {
        Gui.Panel(area);
        Gui.Title(new Rect(area.x + 20, area.y + 10, 900, 50), "The Graveyard");
        for (int i = 0; i < E.Graveyard.Count; i++)
        {
            var h = E.Graveyard[i];
            Gui.Label(new Rect(area.x + 30, area.y + 80 + i * 40, 940, 38),
                $"<b>{h.Name}</b> the {Pretty(h.ClassId)}, resolve {h.ResolveLevel} — {h.CauseOfDeath} (week {h.WeekDied + 1})");
        }
        if (E.Graveyard.Count == 0) Gui.Label(new Rect(area.x + 30, area.y + 80, 940, 40), "No one rests here. Yet.");
    }

    private void DrawUpgrades(Rect area, Hamlet hamlet)
    {
        Gui.Panel(area);
        Gui.Title(new Rect(area.x + 20, area.y + 10, 900, 50), "Upgrades");
        string[] buildings = { Buildings.StageCoach, Buildings.Abbey, Buildings.Tavern, Buildings.Sanitarium, Buildings.NomadWagon, Buildings.Guild, Buildings.Blacksmith, Buildings.Survivalist };
        for (int i = 0; i < buildings.Length; i++)
            if (Gui.Button(new Rect(area.x + 20 + i * 120, area.y + 66, 116, 44), Pretty(buildings[i]).Split(' ')[0]))
                _upgradeBuilding = buildings[i];

        var trees = S.Buildings.Trees.Trees.Keys.Where(k => k.StartsWith(_upgradeBuilding + ".")).ToList();
        for (int i = 0; i < trees.Count; i++)
        {
            var next = S.Buildings.Trees.Next(E, trees[i]);
            var r = new Rect(area.x + 20, area.y + 130 + i * 90, area.width - 40, 84);
            Gui.Panel(r);
            int owned = S.Buildings.Trees.Trees[trees[i]].Count(l => E.Upgrades.Contains(l.Key));
            Gui.Label(new Rect(r.x + 10, r.y + 8, 620, 70),
                $"<b>{Pretty(trees[i].Substring(trees[i].IndexOf('.') + 1))}</b>  {owned}/{S.Buildings.Trees.Trees[trees[i]].Count}\n" +
                (next == null ? "Fully upgraded." : "Next: " + string.Join(", ", next.Cost.Select(c => $"{c.Amount} {c.Type}"))));
            if (next != null && Gui.Button(new Rect(r.xMax - 220, r.y + 16, 200, 52), "Buy", S.Buildings.Trees.CanBuy(E, next)))
            {
                hamlet.BuyUpgrade(next.TreeId, next.Code);
                S.Persist();
            }
        }
    }

    private void DrawHero(Rect area)
    {
        var h = E.Hero(_heroId);
        Gui.Panel(area);
        if (h == null) { _panel = Panel.Town; return; }
        Gui.Title(new Rect(area.x + 20, area.y + 10, 900, 50), $"{h.Name} the {Pretty(h.ClassId)}");
        Gui.Label(new Rect(area.x + 20, area.y + 70, 960, 300),
            $"Resolve level {h.ResolveLevel} ({h.ResolveXp} xp)   Stress {h.Stress}/10\n" +
            $"Quirks: {string.Join(", ", h.Quirks.Select(Pretty))}\n" +
            $"Camp skills: {string.Join(", ", h.CampingSkills.Select(Pretty))}\n" +
            $"Trinkets: {string.Join(", ", h.Trinkets.Select(Pretty))}\n" +
            $"{(h.Activity != null ? "This week: " + h.Activity : "")}");

        // Trinkets: two slots, from the estate's stash.
        int y = (int)area.y + 380;
        foreach (var t in h.Trinkets.ToList())
            if (Gui.Button(new Rect(area.x + 20, y += 54, 460, 50), "Unequip " + Pretty(t)))
            {
                h.Trinkets.Remove(t);
                E.Trinkets.Add(t);
                S.Persist();
            }
        if (h.Trinkets.Count < 2)
        {
            int x = 0;
            foreach (var t in E.Trinkets.Distinct().Take(8).ToList())
                if (Gui.Button(new Rect(area.x + 500 + (x % 2) * 240, area.y + 434 + (x++ / 2) * 54, 234, 50), "Equip " + Pretty(t)))
                {
                    E.Trinkets.Remove(t);
                    h.Trinkets.Add(t);
                    S.Persist();
                }
        }
        if (Gui.Button(new Rect(area.xMax - 260, area.yMax - 70, 240, 54), Gui.Colour("Dismiss hero", Gui.Blood)))
        {
            S.Hamlet.Dismiss(h.Id);
            S.Persist();
            _panel = Panel.Town;
        }
    }

    public static string Pretty(string id) =>
        string.IsNullOrEmpty(id) ? "" : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' ').Replace('.', ' '));
}
