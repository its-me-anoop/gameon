using System;
using LittleLifeline.Core;
using UnityEngine.UIElements;

namespace LittleLifeline.App
{
    public sealed partial class LifelineApp
    {
        private TownId? routeHUDTown;

        private void BuildRouteHUD()
        {
            var state = campaign.State;
            var destination = routeHUDTown ?? state.Town;
            var progress = TownProgressFor(destination);
            var dock = Dock(pageBody);
            dock.name = "route-hud";
            dock.style.maxHeight = 230;
            var stops = Row(dock);
            foreach (TownId town in Enum.GetValues(typeof(TownId)))
            {
                var choice = town;
                var pick = IconAction(stops, RouteTownGlyph(choice), "Inspect " + TownName(choice),
                    () => { routeHUDTown = choice; BuildScreen(); }, TownName(choice));
                CompactManagementButton(pick);
                pick.name = "route-town-" + choice;
                pick.EnableInClassList("selected", destination == choice);
            }

            var demand = Row(dock);
            demand.style.minHeight = 28;
            Text(demand, TownName(destination), "grow").style.fontSize = 14;
            var consultation = destination == TownId.Willowbank ? 60 : 30;
            var diagnostics = destination == TownId.Copperhill ? 50 : destination == TownId.Willowbank ? 25 : 20;
            var recovery = 100 - consultation - diagnostics;
            IconStat(demand, GameGlyph.Clinic, () => consultation + "%").tooltip = "Consultation-only visits";
            IconStat(demand, GameGlyph.Diagnostics, () => diagnostics + "%").tooltip = "Consultation then tests";
            IconStat(demand, GameGlyph.Recovery, () => recovery + "%").tooltip = "Consultation then recovery";

            var projects = Row(dock);
            for (var index = 0; index < 2; index++)
            {
                var choice = index;
                var project = IconAction(projects, RouteProjectGlyph(destination, choice), LifelineRules.ProjectName(destination, choice),
                    () => Command(() => campaign.ChooseProject(choice)), RouteProjectCaption(destination, choice));
                CompactManagementButton(project);
                project.style.height = 64;
                project.name = "route-project-" + choice;
                var amount = Text(project, "");
                amount.style.fontSize = 14;
                amount.pickingMode = PickingMode.Ignore;
                readouts.Add(() =>
                {
                    var value = choice == 0 ? progress.ProjectOneProgress : progress.ProjectTwoProgress;
                    var complete = (progress.ProjectCompletionMask & (1 << choice)) != 0;
                    amount.text = complete ? "✓" : value + "/" + LifelineRules.ProjectGoal(destination, choice);
                    project.EnableInClassList("selected", progress.ProjectIndex == choice);
                    project.SetEnabled(destination == state.Town && !complete);
                });
            }

            var actions = Row(dock);
            var goal = LifelineRules.TownUnlockReputation(destination);
            var current = destination == state.Town;
            var depart = IconAction(actions, current ? GameGlyph.Check : GameGlyph.Train,
                current ? "Your hospital is at " + TownName(destination) : "Travel to " + TownName(destination),
                () => TravelFromRouteHUD(destination), current ? "Here" : "Travel");
            CompactManagementButton(depart);
            depart.name = "route-travel";
            var locked = IconStat(actions, GameGlyph.Star, () => state.Reputation + "/" + goal);
            readouts.Add(() =>
            {
                var unlocked = state.Reputation >= goal;
                depart.SetEnabled(!current && unlocked && !saves.HasPendingOfflineProgress);
                locked.style.display = !current && !unlocked ? DisplayStyle.Flex : DisplayStyle.None;
            });
            var weeklyAction = IconAction(actions, GameGlyph.Trophy, "Plan the Weekly Call", StartWeekly);
            CompactManagementButton(weeklyAction, false);
            weeklyAction.name = "route-weekly";
        }

        private void TravelFromRouteHUD(TownId destination)
        {
            if (saves.HasPendingOfflineProgress)
            { Notify("Offline care is still saving. Departure will be available shortly."); return; }
            var result = campaign.Travel(destination);
            if (!result.Success) { Notify(result.Message); return; }
            selectedSlot = -1;
            selectedCarriageId = -1;
            SaveNow();
            SuccessFeedback();
            Open(Page.Hospital);
            Notify(result.Message);
        }

        private static GameGlyph RouteTownGlyph(TownId town)
            => town == TownId.Copperhill ? GameGlyph.Workshop : town == TownId.Seabrook ? GameGlyph.Lighthouse : GameGlyph.Garden;
        private static GameGlyph RouteProjectGlyph(TownId town, int index)
            => town == TownId.Copperhill ? (index == 0 ? GameGlyph.Workshop : GameGlyph.Clock)
            : town == TownId.Seabrook ? (index == 0 ? GameGlyph.Clinic : GameGlyph.Lighthouse)
            : index == 0 ? GameGlyph.Garden : GameGlyph.School;
        private static string RouteProjectCaption(TownId town, int index)
            => town == TownId.Copperhill ? (index == 0 ? "Workshop" : "Clock tower")
            : town == TownId.Seabrook ? (index == 0 ? "Clinic" : "Lighthouse")
            : index == 0 ? "Garden" : "School";
    }
}
