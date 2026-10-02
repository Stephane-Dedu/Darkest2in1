using System.Collections.Generic;
using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Quest board, party selection and the provisioner, then off to the dungeon.</summary>
internal sealed class EmbarkUi
{
    private QuestOffer _quest;
    private readonly List<string> _party = new();
    private readonly Inventory _cart = new();
    private string _error;
    private bool _confirmLow;

    public bool WantsBack;

    private static Session S => Session.Current;
    private static Estate E => S.Save.Estate;

    public void Draw()
    {
        Gui.Fill(new Rect(0, 0, Gui.W, Gui.H), new Color(0.04f, 0.03f, 0.03f, 1f));
        Gui.Title(new Rect(30, 14, 900, 50), "Embark");
        Gui.Label(new Rect(980, 22, 900, 40), $"Gold {E.Get(Currency.Gold)}   Provisions cost {CartCost()}");

        DrawQuests(new Rect(20, 80, 620, 900));
        DrawParty(new Rect(660, 80, 600, 900));
        DrawProvisions(new Rect(1280, 80, 620, 900));

        if (_error != null) Gui.Label(new Rect(660, 990, 900, 40), Gui.Colour(_error, Gui.Blood));
        if (Gui.Button(new Rect(20, 1000, 260, 60), "Back to the Hamlet")) WantsBack = true;

        var heroes = _party.Select(E.Hero).Where(h => h != null).ToList();
        string why = Embark.WhyCantEmbark(E, _quest, heroes);

        // DD1 asks for confirmation before leaving with too little food or no torches.
        int minFood = S.Provisioner.MinimumFood(_quest?.Length ?? 1);
        bool lowFood = _cart.Count(Supply.Food) < minFood, noTorch = _cart.Count(Supply.Torch) == 0;
        if (why == null && (lowFood || noTorch))
        {
            string warn = lowFood ? $"Less than {minFood} food: the party may starve." : "No torches: the dark will press in.";
            Gui.Label(new Rect(980, 940, 590, 50), Gui.Colour(warn + (_confirmLow ? " Click Embark again to go anyway." : ""), Gui.Blood));
        }
        if (Gui.Button(new Rect(1580, 990, 320, 76), why ?? Gui.Colour("<b>Embark!</b>", Gui.Gold), why == null))
        {
            if ((lowFood || noTorch) && !_confirmLow) { _confirmLow = true; return; }
            _confirmLow = false;
            var bought = new Inventory();
            foreach (var kv in _cart.Items) bought.Add(kv.Key, kv.Value);
            _error = Driver.Instance.Embark(_quest, heroes, bought);
            if (_error == null) { _party.Clear(); _cart.Items.Clear(); _quest = null; }
        }
    }

    private void DrawQuests(Rect area)
    {
        Gui.Panel(area);
        Gui.Label(new Rect(area.x + 14, area.y + 6, 600, 30), "<b>Quests</b>");
        for (int i = 0; i < E.Quests.Count; i++)
        {
            var q = E.Quests[i];
            string name = (q.IsPlot ? Gui.Colour("★ ", Gui.Blood) : "") + $"{q.DifficultyName} {q.Size} {HamletUi.Pretty(q.Type)}";
            string where = S.Zones.ZoneName(q.Dungeon);
            string rewards = string.Join(", ", q.Rewards.Select(r => r.Type == "trinket" ? $"{r.Id} trinket" : $"{r.Amount} {r.Type}"));
            if (Gui.Button(new Rect(area.x + 10, area.y + 44 + i * 92, area.width - 20, 86),
                    $"{(q == _quest ? "▶ " : "")}<b>{name}</b> — {where}\n{Gui.Colour(rewards, Gui.Dim)}"))
            {
                _quest = q;
                _party.RemoveAll(id => !Homecoming.WillEmbark(E.Hero(id), q));
            }
        }
    }

    private void DrawParty(Rect area)
    {
        Gui.Panel(area);
        Gui.Label(new Rect(area.x + 14, area.y + 6, 580, 30), $"<b>Party</b> ({_party.Count}/4) — front rank first");
        for (int i = 0; i < _party.Count; i++)
        {
            var h = E.Hero(_party[i]);
            if (Gui.Button(new Rect(area.x + 10, area.y + 44 + i * 64, area.width - 20, 58), $"{i + 1}. <b>{h.Name}</b> the {HamletUi.Pretty(h.ClassId)} (remove)"))
            { _party.RemoveAt(i); break; }
        }
        Gui.Label(new Rect(area.x + 14, area.y + 320, 580, 30), "<b>Available</b>");
        var available = E.Roster.Where(h => !_party.Contains(h.Id)).ToList();
        for (int i = 0; i < available.Count && i < 9; i++)
        {
            var h = available[i];
            string note = !h.IsAvailable ? " (busy)" : _quest != null && !Homecoming.WillEmbark(h, _quest) ? " (refuses)" : "";
            bool ok = h.IsAvailable && _party.Count < 4 && (_quest == null || Homecoming.WillEmbark(h, _quest));
            if (Gui.Button(new Rect(area.x + 10, area.y + 356 + i * 60, area.width - 20, 54),
                    $"<b>{h.Name}</b> {HamletUi.Pretty(h.ClassId)} Lv{h.ResolveLevel} stress {h.Stress}{note}", ok))
                _party.Add(h.Id);
        }
    }

    private void DrawProvisions(Rect area)
    {
        Gui.Panel(area);
        int length = _quest?.Length ?? 1;
        Gui.Label(new Rect(area.x + 14, area.y + 6, 600, 30), $"<b>Provisions</b> (recommended food: {S.Provisioner.MinimumFood(length)})");
        var stock = S.Provisioner.Stock(length);
        int i = 0;
        foreach (var id in Supply.Provisioner)
        {
            int have = _cart.Count(id), max = stock.TryGetValue(id, out var m) ? m : 0, price = S.Provisioner.Price(id);
            float y = area.y + 50 + i++ * 70;
            Gui.Label(new Rect(area.x + 16, y + 10, 300, 40), $"{HamletUi.Pretty(id)} ({price}g)");
            if (Gui.Button(new Rect(area.x + 330, y, 60, 56), "−", have > 0)) _cart.Add(id, -1);
            Gui.Label(new Rect(area.x + 400, y + 10, 80, 40), $"{have}/{max}");
            if (Gui.Button(new Rect(area.x + 480, y, 60, 56), "+", have < max)) _cart.Add(id, 1);
        }
        Gui.Small(new Rect(area.x + 16, area.yMax - 60, 600, 50), "Heroes also bring their own supplies, and longer quests come with firewood.");
    }

    private int CartCost() => _cart.Items.Sum(kv => S.Provisioner.Price(kv.Key) * kv.Value);
}
