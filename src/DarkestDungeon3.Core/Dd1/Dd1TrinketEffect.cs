using System;
using System.Globalization;

namespace DarkestDungeon3.Core.Dd1;

/// <summary>Explicit DD1-to-DD2 trinket semantics. Conditional rules are retained, never flattened.</summary>
public sealed class Dd1TrinketEffect
{
    public string Stats, Condition, Text;
    public float Stress;

    public static Dd1TrinketEffect Adapt(Dd1Buff b)
    {
        if (b == null || b.Sub == "damage_high") return null; // paired DD1 damage endpoints describe one modifier
        var result = new Dd1TrinketEffect();
        string number(float n) => n.ToString("R", CultureInfo.InvariantCulture);
        string pct(float n) => (n >= 0 ? "+" : "") + (n * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";
        void stat(string key, float amount, string label, bool multiply = false)
        {
            result.Stats = "key_map," + key + ",\n" + (multiply ? "multiply_stats," : "add_stats,") + number(amount) + ",\n";
            result.Text = pct(amount) + " " + label;
        }
        switch (b.Stat, b.Sub)
        {
            case ("combat_stat_multiply", "damage_low"): stat("health_damage_dealt_percent", b.Amount, "DMG"); break;
            case ("combat_stat_multiply", "max_hp"): stat("health_max", b.Amount, "Max HP", true); break;
            case ("combat_stat_add", "speed_rating"):
                result.Stats = "key_map,speed,\nadd_stats," + number(b.Amount) + ",\n";
                result.Text = (b.Amount >= 0 ? "+" : "") + number(b.Amount) + " SPD"; break;
            case ("combat_stat_add", "crit_chance"): stat("crit_chance", b.Amount, "CRIT"); break;
            case ("combat_stat_add", "attack_rating"): stat("crit_chance", b.Amount / 3f, "CRIT (DD1 ACC adaptation)"); break;
            case ("combat_stat_add", "defense_rating"): stat("health_damage_received_percent", -b.Amount, "Damage Taken (DD1 DODGE adaptation)"); break;
            case ("combat_stat_add", "protection_rating"): stat("health_damage_received_percent", -b.Amount, "Damage Taken"); break;
            case ("hp_heal_percent", _): stat("health_heal_dealt_percent", b.Amount, "Healing Given"); break;
            case ("hp_heal_received_percent", _): stat("health_heal_received_percent", b.Amount, "Healing Received"); break;
            case ("resistance", "trap"): result.Text = pct(b.Amount) + " Trap Disarm"; break;
            case ("resistance", var sub):
                sub = sub == "poison" ? "blight" : sub == "death_blow" ? "death" : sub;
                result.Stats = "sub_stat,resistance," + sub + "," + number(b.Amount) + ",\n";
                result.Text = pct(b.Amount) + " " + sub.ToUpperInvariant() + " RES"; break;
            case ("poison_chance", _):
            case ("bleed_chance", _):
            case ("stun_chance", _):
            case ("debuff_chance", _):
            case ("move_chance", _):
                string resist = b.Stat.Replace("_chance", "").Replace("poison", "blight");
                result.Stats = "sub_stat,resistance_ignore," + resist + "," + number(b.Amount) + ",\n";
                result.Text = pct(b.Amount) + " " + resist.ToUpperInvariant() + " Piercing"; break;
            case ("stress_dmg_received_percent", ""):
                result.Stress = b.Amount; result.Text = pct(b.Amount) + " Stress Received"; break;
            case ("resolve_check_percent", _):
                result.Stats = "sub_stat,overstress_chance_modifier,resolute," + number(b.Amount) + ",\n";
                result.Text = pct(b.Amount) + " Resolute Chance (DD1 Virtue adaptation)"; break;
            case ("resolve_xp_bonus_percent", _): result.Text = pct(b.Amount) + " Resolve XP"; break;
            case ("party_surprise_chance", _): result.Text = pct(b.Amount) + " Party Surprise Chance"; break;
            case ("monsters_surprise_chance", _): result.Text = pct(b.Amount) + " Enemy Surprise Chance"; break;
            case ("scouting_chance", _): result.Text = pct(b.Amount) + " Scouting Chance"; break;
            default: result.Text = "DD1 effect awaiting adaptation: " + b.Stat + (b.Sub == "" ? "" : "/" + b.Sub); return result;
        }
        string condition(string type, string actor, string value, float n, string comparison) =>
            "m_ConditionType," + type + ",\nm_ConditionActorType," + actor + ",\nm_ConditionString," + value
            + ",\nm_ConditionNumber," + number(n) + ",\nm_ConditionNumberType," + comparison + ",\n";
        switch (b.Rule)
        {
            case "always": break;
            case "meleeonly":
            case "rangedonly":
                string tag = b.Rule == "meleeonly" ? "melee" : "ranged";
                result.Condition = condition("skill_tag", "NONE", tag, 1, "BOOL"); result.Text += " (" + tag + " skills)"; break;
            case "in_rank": result.Condition = condition("rank", "PERFORMER", "", b.RuleNumber + 1, "EQUAL"); result.Text += " (Rank " + (b.RuleNumber + 1) + ")"; break;
            case "at_deaths_door": result.Condition = condition("health_percent", "PERFORMER", "", 0, "LESS_THAN_OR_EQUAL"); result.Text += " (Death's Door)"; break;
            case "monsterType" when b.RuleString == "eldritch":
                result.Condition = condition("tag", "TARGET", "cultist", 1, "GREATER_THAN_OR_EQUAL"); result.Text += " (vs Cultists; DD1 Eldritch adaptation)"; break;
            default:
                result.Stats = null; result.Stress = 0; result.Text += " (inactive: DD1 rule " + b.Rule + ")"; break;
        }
        return result;
    }
}
