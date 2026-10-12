using System;
using System.Collections.Generic;

namespace DarkestDungeon3.Core.Campaign;

/// <summary>Front-to-back expedition selection, packed into at most four ranks.</summary>
public static class ExpeditionFormation
{
    public static bool Drop(List<string> party, string heroId, int rank)
    {
        if (party == null || party.Count > 4 || string.IsNullOrEmpty(heroId) || rank < 0 || rank >= 4) return false;
        int from = party.IndexOf(heroId);
        if (from >= 0)
        {
            int to = Math.Min(rank, party.Count - 1);
            (party[from], party[to]) = (party[to], party[from]);
        }
        else if (rank < party.Count) party[rank] = heroId;
        else if (party.Count < 4) party.Add(heroId);
        else return false;
        return true;
    }
}
