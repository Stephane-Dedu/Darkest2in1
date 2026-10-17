using System.Linq;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Campaign.Town;
using DarkestDungeon3.Dd2;
using DarkestDungeon3.Runtime;
using UnityEngine;

namespace DarkestDungeon3.Ui;

internal sealed partial class HamletUi
{
    private bool _guildPaths;
    private string _pathHero, _previewPath, _previewSkill;
    private Vector2 _pathSkillScroll, _pathCurrentScroll, _pathPreviewScroll, _pathEffectsScroll;

    private void DrawGuildPathSummary(HeroRecord hero)
    {
        var paths = HeroPathBridge.ForClass(hero.ClassId);
        Gui.Text(W(694, 136, 205, 30), Pretty(hero.ClassId), 23, Gui.Dd1Name, TextAnchor.MiddleLeft, heading: true);
        ExpeditionSkin.Text(W(694, 174, 205, 38), HeroPathBridge.Name(hero.ClassId, hero.PathId), 22, ExpeditionSkin.Brass, heading: true);
        string flavour = HeroPathBridge.Flavour(hero.ClassId, hero.PathId);
        ExpeditionSkin.Text(W(694, 222, 205, 120), flavour, 17, ExpeditionSkin.Muted);
        if (paths.Count > 1)
        {
            if (ExpeditionSkin.Button(W(683, 354, 215, 46), "Choose Path", size: 23))
            {
                _guildPaths = true;
                _previewPath = HeroPathBridge.Catalog.ResolveId(hero.ClassId, hero.PathId);
                _previewSkill = null;
            }
            ExpeditionSkin.Text(W(694, 414, 200, 110), $"Specialise at resolve {HeroPaths.RequiredResolve}.\nRetrain here for {Gui.Num(S.Hamlet.PathTrainingCost, "#,0")} gold.", 17, ExpeditionSkin.Muted);
        }
        else ExpeditionSkin.Text(W(694, 354, 200, 100), hero.ClassId == RecruitClasses.BountyHunter
            ? "The Bounty Hunter follows the Professional Path."
            : "This class has no DD2 specialisations.", 17, ExpeditionSkin.Muted);
        if (hero.PathId != null && !HeroPathBridge.Catalog.IsValid(hero.ClassId, hero.PathId))
            ExpeditionSkin.Text(W(694, 544, 200, 90), "Saved Path unavailable. Using the class default.", 16, ExpeditionSkin.Red);
    }

    private void DrawGuildPaths(HeroRecord hero, Hamlet hamlet)
    {
        var catalog = HeroPathBridge.Catalog;
        var paths = HeroPathBridge.ForClass(hero.ClassId);
        var skills = HeroSkills.ForClass(hero.ClassId);
        if (catalog == null || paths.Count == 0 || skills == null) { _guildPaths = false; return; }
        if (_pathHero != hero.Id)
        {
            _pathHero = hero.Id;
            _previewPath = catalog.ResolveId(hero.ClassId, hero.PathId);
            _previewSkill = null;
        }
        if (!catalog.IsValid(hero.ClassId, _previewPath)) _previewPath = catalog.DefaultId(hero.ClassId);
        var area = W(48, 124, 1300, 624);
        Gui.Fill(area, new Color(.025f, .03f, .035f, .98f));
        ExpeditionSkin.Rule(new Rect(area.x, area.y, area.width, 10), ExpeditionSkin.Brass);
        ExpeditionSkin.Text(W(72, 145, 215, 44), "Hero Paths", 34, ExpeditionSkin.Ivory, heading: true);
        for (int i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            var r = W(70, 207 + i * 68, 226, 56);
            bool selected = path.Id == _previewPath;
            ExpeditionSkin.Banner(r, selected ? ExpeditionSkin.Red : ExpeditionSkin.Brass);
            ExpeditionSkin.Text(new Rect(r.x + 16, r.y + 4, r.width - 32, r.height - 8), HeroPathBridge.Name(hero.ClassId, path.Id), 23,
                selected ? ExpeditionSkin.Ivory : ExpeditionSkin.Muted, TextAnchor.MiddleCenter, heading: true);
            if (Gui.Hotspot(r))
            {
                _previewPath = path.Id;
                _previewSkill = null;
                _pathEffectsScroll = _pathCurrentScroll = _pathPreviewScroll = Vector2.zero;
            }
        }
        ExpeditionSkin.Text(W(72, 510, 220, 100), "Paths change a hero’s role and skills. Learned skills and mastery carry over.", 18, ExpeditionSkin.Muted);
        if (ExpeditionSkin.Button(W(70, 660, 226, 52), "Back to skills", size: 24)) _guildPaths = false;

        string previewName = HeroPathBridge.Name(hero.ClassId, _previewPath);
        ExpeditionSkin.Text(W(324, 144, 980, 42), previewName, 34, ExpeditionSkin.Ivory, heading: true);
        ExpeditionSkin.Text(W(324, 188, 980, 42), HeroPathBridge.Flavour(hero.ClassId, _previewPath), 18, ExpeditionSkin.Muted);
        ScrollText(W(324, 232, 980, 92), HeroPathBridge.Effects(hero.ClassId, _previewPath), ref _pathEffectsScroll, 18);
        ExpeditionSkin.Rule(W(320, 336, 990, 8));

        // Include changes made by both Paths, so switching back to Wanderer shows what will revert.
        string current = catalog.ResolveId(hero.ClassId, hero.PathId);
        var comparisons = catalog.Paths[current].ComparisonSkills.Union(catalog.Paths[_previewPath].ComparisonSkills).ToList();
        var affected = skills.Where(s => comparisons.Contains(s.Id) && (catalog.SkillId(hero.ClassId, current, s.Id, false) !=
            catalog.SkillId(hero.ClassId, _previewPath, s.Id, false) || catalog.SkillId(hero.ClassId, current, s.Id, true) !=
            catalog.SkillId(hero.ClassId, _previewPath, s.Id, true))).ToList();
        if (affected.Count == 0) affected = skills;
        if (!affected.Any(s => s.Id == _previewSkill)) _previewSkill = affected.FirstOrDefault()?.Id;
        ExpeditionSkin.Text(W(324, 350, 210, 28), "Skills to inspect", 20, ExpeditionSkin.Brass, heading: true);
        var list = W(320, 391, 235, 235);
        _pathSkillScroll = GUI.BeginScrollView(list, _pathSkillScroll, new Rect(0, 0, 211, Mathf.Max(list.height, affected.Count * 46)));
        for (int i = 0; i < affected.Count; i++)
        {
            var skill = affected[i];
            var r = new Rect(3, i * 46, 204, 42);
            if (_previewSkill == skill.Id) Gui.Fill(r, new Color(.22f, .12f, .085f, .8f));
            if (skill.Icon != null) Art.DrawSprite(new Rect(r.x + 2, r.y + 3, 36, 36), skill.Icon);
            ExpeditionSkin.Text(new Rect(r.x + 44, r.y, 156, 42), HeroSkills.Name(skill.Id) + (hero.MasteredSkills.Contains(skill.Id) ? " +" : ""), 17,
                _previewSkill == skill.Id ? ExpeditionSkin.Ivory : ExpeditionSkin.Muted, TextAnchor.MiddleLeft);
            if (Gui.Hotspot(r)) { _previewSkill = skill.Id; _pathCurrentScroll = _pathPreviewScroll = Vector2.zero; }
        }
        GUI.EndScrollView();
        ExpeditionSkin.Text(W(574, 350, 350, 36), "Current · " + HeroPathBridge.Name(hero.ClassId, current), 21, ExpeditionSkin.Muted, heading: true);
        ExpeditionSkin.Text(W(956, 350, 350, 36), "Preview · " + previewName, 21, ExpeditionSkin.Brass, heading: true);
        if (_previewSkill != null)
        {
            ScrollText(W(574, 395, 350, 231), HeroPathBridge.SkillDescription(HeroPathBridge.SkillId(hero, _previewSkill)), ref _pathCurrentScroll, 18);
            ScrollText(W(956, 395, 350, 231), HeroPathBridge.SkillDescription(HeroPathBridge.SkillId(hero, _previewSkill, _previewPath, preview: true)), ref _pathPreviewScroll, 18);
        }
        string why = hamlet.WhyCantTrainPath(hero, catalog, _previewPath, S.Save.Expedition != null);
        ExpeditionSkin.Text(W(574, 634, 735, 26), why ?? "Resolve, equipment, learned skills and mastery are retained.", 17,
            why == null ? ExpeditionSkin.Muted : ExpeditionSkin.Brass);
        if (ExpeditionSkin.Button(W(574, 670, 735, 48), $"Follow {previewName} · {Gui.Num(hamlet.PathTrainingCost, "#,0")} gold", why == null, 25)
            && hamlet.TrainPath(hero.Id, catalog, _previewPath, S.Save.Expedition != null))
        {
            Runtime.Dd1Audio.Play("/town/guild_purchase_skill");
            S.Persist();
            _guildPaths = false;
        }
    }

    private static void ScrollText(Rect area, string text, ref Vector2 scroll, int size)
    {
        var style = ExpeditionSkin.PanelTextStyle(size, TextAnchor.UpperLeft, false);
        float height = Mathf.Max(area.height, style.CalcHeight(new GUIContent(text ?? ""), area.width - 24) + 12);
        scroll = GUI.BeginScrollView(area, scroll, new Rect(0, 0, area.width - 20, height));
        ExpeditionSkin.Text(new Rect(0, 0, area.width - 24, height), text, size, ExpeditionSkin.Ivory);
        GUI.EndScrollView();
    }
}
