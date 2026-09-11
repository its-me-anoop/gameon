using System;
using System.Linq;
using LittleLifeline.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LittleLifeline.App
{
    public sealed partial class LifelineApp
    {
        private int expandedCrew = -1;
        private bool crewForWeekly;
        private static string TownName(TownId town) => LifelineRules.TownName(town);
        private static string RoomName(RoomKind room) => LifelineRules.RoomName(room);
        private static string ShortRoomName(RoomKind room) => room == RoomKind.Consultation ? "Clinic" : room == RoomKind.Diagnostics ? "Tests" : "Recovery";
        private TownProgress TownProgressFor(TownId town) => campaign.State.Towns.First(t => t.Town == town);

        private void Command(Func<CommandResult> command)
        {
            if (saves.HasPendingOfflineProgress && !(page == Page.Weekly || page == Page.Crew && crewForWeekly))
            { Notify("Your crew’s offline work is waiting to be saved. We’re retrying before changing the train."); return; }
            var result = command();
            var selected = State.Carriages.FirstOrDefault(c => c.Id == selectedCarriageId);
            if (selected == null && selectedSlot >= 0)
                selected = State.Carriages.FirstOrDefault(c => c.Slot == selectedSlot);
            if (selected != null)
            {
                selectedCarriageId = selected.Id; selectedSlot = selected.Slot;
                world.Focus(selectedSlot, ReducedMotion);
            }
            if (result.Success) { SuccessFeedback(); SaveNow(); }
            Notify(result.Message); BuildScreen();
        }

        private void BuildHospital()
        {
            AddWorld();
            if (intro)
            {
                var welcome = Box(pageBody, "welcome");
                Text(welcome, "FIRST STOP · WILLOWBANK", "eyebrow");
                Text(welcome, "A little care goes a long way.", "welcome-title", true);
                Text(welcome, "Your crew is ready. Open the clinic, meet your first residents, and help this station bloom again.", "sheet-info");
                Button(welcome, "Open the doors", () => { intro = false; campaign.Advance(.1); SaveNow(); BuildScreen(); }, "primary");
                return;
            }
            if (returnReport != null && returnReport.applied && returnReport.elapsedSeconds >= 60)
            {
                var welcome = Box(pageBody, "welcome");
                Text(welcome, "YOUR CREW KEPT BUSY", "eyebrow");
                Text(welcome, "Welcome back.", "welcome-title", true);
                Text(welcome, returnReport.completed + " residents cared for · +" + returnReport.coinsEarned.ToString("N0") + " build funds", "sheet-info");
                var elapsed = TimeSpan.FromSeconds(returnReport.elapsedSeconds);
                Text(welcome, "While you were away: " + (elapsed.TotalHours >= 1 ? ((int)elapsed.TotalHours) + "h " + elapsed.Minutes + "m" : elapsed.Minutes + (elapsed.Minutes == 1 ? " minute" : " minutes")) +
                    (returnReport.wasCapped ? " · up to 24 hours of work" : "") + ". Progress is already saved.", "sheet-info");
                Button(welcome, "See the hospital", () => { returnReport = null; BuildScreen(); }, "primary");
                return;
            }
            CarriageStrip();
            if (selectedSlot >= 0) CarriageSheet();
            else HospitalNextStep();
        }

        private void CarriageStrip()
        {
            var strip = Row(pageBody, "strip");
            for (var i = 0; i < LifelineRules.CarriageSlots; i++)
            {
                var slot = i; var room = State.Carriages.FirstOrDefault(c => c.Slot == slot);
                var label = room == null ? "+ Carriage" : ShortRoomName(room.Kind);
                var button = Button(strip, (slot + 1) + " · " + label, () => SelectCarriage(slot), "room-tab");
                button.EnableInClassList("selected", selectedSlot == slot);
            }
        }
        private void HospitalNextStep()
        {
            var next = Box(pageBody, "next-step");
            if (campaign.State.TotalCompleted == 0)
            {
                Text(next, "The first residents are arriving", "next-title");
                LiveText(next, () => campaign.State.TotalCompleted > 0 ? "First care visit complete. Your train is earning build funds." :
                    "Follow them from the platform into the clinic. Your doctor takes care of the consultation.", "next-detail");
                Button(next, "Look inside the clinic", () => SelectCarriage(0), "quiet");
                return;
            }
            if (campaign.State.Carriages.Count < 2)
            {
                Text(next, "Make room for more care", "next-title");
                Text(next, "Add a testing or recovery carriage, then give a crew member the new job.", "next-detail");
                Button(next, "Choose the next carriage", () => SelectCarriage(1), "primary");
                return;
            }
            var project = TownProgressFor(campaign.State.Town);
            Text(next, LifelineRules.ProjectName(project.Town, project.ProjectIndex), "next-title");
            LiveText(next, () => ProjectSummary(project), "next-detail");
            var track = Box(next, "progress-track"); var fill = Box(track, "progress-fill");
            readouts.Add(() => fill.style.width = Length.Percent(Math.Min(100, 100f * project.ProjectProgress / LifelineRules.ProjectGoal(project.Town, project.ProjectIndex))));
            var actions = Row(next, "room-actions");
            Button(actions, "Town projects", () => Open(Page.Route), "quiet");
            Button(actions, "Care plan", () => OpenCrew(false), "quiet");
        }

        private string ProjectSummary(TownProgress project)
        {
            if ((project.ProjectCompletionMask & (1 << project.ProjectIndex)) != 0) return "Complete · you made a lasting difference here.";
            return project.ProjectProgress + " / " + LifelineRules.ProjectGoal(project.Town, project.ProjectIndex) + " care points · " +
                (LifelineRules.ProjectPath(project.Town, project.ProjectIndex) == CarePath.Diagnostics ? "testing visits help most" :
                 LifelineRules.ProjectPath(project.Town, project.ProjectIndex) == CarePath.Recovery ? "recovery visits help most" : "consultations help most");
        }

        private void CarriageSheet()
        {
            var sheet = Box(pageBody, "sheet");
            var room = State.Carriages.FirstOrDefault(c => c.Slot == selectedSlot);
            var heading = Row(sheet);
            Text(heading, room == null ? "Room for something new" : RoomName(room.Kind), "sheet-title grow", true);
            Button(heading, "×", () => SelectCarriage(-1), "round").tooltip = "Show the whole train";
            if (room == null)
            {
                Text(sheet, "Choose what this carriage brings to the town.", "sheet-info");
                foreach (RoomKind kind in Enum.GetValues(typeof(RoomKind)))
                {
                    var choice = kind;
                    var b = Button(sheet, RoomName(choice) + " · " + LifelineRules.BuildCost(choice) + " funds", () => Command(() => Active.Build(choice, selectedSlot)), "secondary small");
                    b.style.marginBottom = 5;
                    readouts.Add(() => b.SetEnabled(State.Coins >= LifelineRules.BuildCost(choice) && !State.IsFinished));
                }
                return;
            }
            LiveText(sheet, () => RoomSummary(room), "sheet-info");
            if (roomToRefit == room.Id)
            {
                Text(sheet, "A new purpose, starting at fit-out 1. Your crew will finish current care before refitting.", "sheet-info");
                foreach (RoomKind kind in Enum.GetValues(typeof(RoomKind)))
                {
                    var nextKind = kind;
                    if (nextKind == room.Kind) continue;
                    var change = Button(sheet, RoomName(nextKind) + " · " + LifelineRules.RefitCost(nextKind) + " funds", () =>
                    { roomToRefit = -1; Command(() => Active.Refit(room.Id, nextKind)); }, "secondary small");
                    change.style.marginBottom = 4;
                    readouts.Add(() => change.SetEnabled(!State.IsFinished && State.Coins >= LifelineRules.RefitCost(nextKind) &&
                        (room.Kind != RoomKind.Consultation || State.Carriages.Count(c => c.Kind == RoomKind.Consultation) > 1)));
                }
                if (room.Kind == RoomKind.Consultation && State.Carriages.Count(c => c.Kind == RoomKind.Consultation) == 1)
                    Text(sheet, "Keep one consultation carriage to welcome residents.", "sheet-info");
                Button(sheet, "Keep its current purpose", () => { roomToRefit = -1; BuildScreen(); }, "quiet");
                return;
            }
            if (roomToArrange == room.Id)
            {
                Text(sheet, "Shorter transfers leave more time for care. Choose a place to compare.", "sheet-info");
                var order = Row(sheet, "room-actions");
                for (var i = 0; i < LifelineRules.CarriageSlots; i++)
                {
                    var target = i;
                    var b = Button(order, "Place " + (i + 1), () => { moveTargetSlot = target; BuildScreen(); }, moveTargetSlot == target ? "primary small" : "secondary small");
                    b.SetEnabled(target != room.Slot && !State.IsFinished);
                }
                Text(sheet, "Now · " + TransferSummary(-1, -1), "sheet-info");
                if (moveTargetSlot >= 0)
                {
                    Text(sheet, "After · " + TransferSummary(room.Id, moveTargetSlot), "sheet-info");
                    Button(sheet, "Move to place " + (moveTargetSlot + 1) + " · free", () =>
                    {
                        var target = moveTargetSlot; roomToArrange = -1; moveTargetSlot = -1;
                        Command(() => Active.Reorder(room.Id, target));
                    }, "primary");
                }
                Button(sheet, "Keep the current order", () => { roomToArrange = -1; BuildScreen(); }, "quiet");
                return;
            }
            var actions = Row(sheet, "room-actions");
            var needsCrew = !State.Crew.Any(c => c.PrimaryRoomId == room.Id || c.SecondaryRoomId == room.Id);
            var improve = Button(actions, room.Level >= LifelineRules.MaxRoomLevel ? "Fully fitted" : "Improve · " + LifelineRules.UpgradeCost(room),
                () => Command(() => Active.Upgrade(room.Id)), needsCrew ? "secondary" : "primary");
            readouts.Add(() => improve.SetEnabled(!State.IsFinished && room.Level < LifelineRules.MaxRoomLevel && State.Coins >= LifelineRules.UpgradeCost(room)));
            Button(actions, "Assign crew", () => OpenCrew(page == Page.Weekly), needsCrew ? "primary" : "secondary");
            var more = Row(sheet, "room-actions");
            Button(more, "Arrange train", () => { roomToArrange = room.Id; moveTargetSlot = -1; BuildScreen(); }, "quiet").SetEnabled(!State.IsFinished);
            Button(more, "Change purpose", () => { roomToRefit = room.Id; BuildScreen(); }, "quiet").SetEnabled(!State.IsFinished);
        }

        private string RoomSummary(CarriageState room)
        {
            var crew = State.Crew.Where(c => c.PrimaryRoomId == room.Id || c.SecondaryRoomId == room.Id).ToList();
            var waiting = Active.QueueFor(room.Kind);
            var treating = State.Patients.Count(p => p.RoomId == room.Id && p.Phase == PatientPhase.Treating);
            if (State.HasPendingRefit && State.PendingRefitRoomId == room.Id) return "Refit booked · finishing the current care visit.";
            if (State.PendingMoveRoomId >= 0) return "The crew is finishing current care before rearranging.";
            return "Carriage " + (room.Slot + 1) + " · fit-out " + room.Level + "\n" +
                (crew.Count == 0 ? "Needs a crew member" : string.Join(" + ", crew.Select(c => c.Name))) +
                " · " + treating + " in care · " + waiting + " waiting";
        }

        private string TransferSummary(int movingRoomId, int targetSlot)
        {
            var moving = State.Carriages.FirstOrDefault(c => c.Id == movingRoomId);
            var swapped = State.Carriages.FirstOrDefault(c => c.Slot == targetSlot);
            Func<CarriageState, int> slot = room => room.Id == movingRoomId ? targetSlot :
                moving != null && swapped != null && room.Id == swapped.Id ? moving.Slot : room.Slot;
            var clinics = State.Carriages.Where(c => c.Kind == RoomKind.Consultation).ToArray();
            var summaries = new System.Collections.Generic.List<string>();
            foreach (var kind in new[] { RoomKind.Diagnostics, RoomKind.Recovery })
            {
                var rooms = State.Carriages.Where(c => c.Kind == kind).ToArray();
                if (clinics.Length == 0 || rooms.Length == 0) continue;
                var shortest = clinics.Min(c => rooms.Min(r => LifelineRules.TransferSeconds(slot(c), slot(r))));
                summaries.Add((kind == RoomKind.Diagnostics ? "tests " : "recovery ") + shortest.ToString("0.#") + "s");
            }
            return summaries.Count == 0 ? "Add a specialist carriage to compare transfers." : "shortest clinic transfer: " + string.Join(" · ", summaries);
        }

        private void BuildRoute()
        {
            var content = PageContent("A world to care for.", "Your train grows with you. Every town keeps the changes you make.");
            foreach (TownId town in Enum.GetValues(typeof(TownId)))
            {
                var destination = town; var progress = TownProgressFor(town);
                var stop = Box(content, "route-stop");
                stop.EnableInClassList("current", town == campaign.State.Town);
                var row = Row(stop); Text(row, "0" + ((int)town + 1), "route-number", true);
                var detail = Box(row, "column grow");
                Text(detail, town == campaign.State.Town ? "YOU ARE HERE" : "NEXT ON THE LINE", "badge");
                Text(detail, TownName(town), "item-title");
                Text(stop, LifelineRules.TownDescription(town), "item-detail");
                Text(stop, LifelineRules.DemandDescription(town), "item-detail");
                if (town != campaign.State.Town)
                {
                    var b = Button(stop, "Depart for " + TownName(town), () =>
                    {
                        if (saves.HasPendingOfflineProgress) { Notify("Your offline progress needs to finish saving before departure."); return; }
                        var result = campaign.Travel(destination);
                        if (!result.Success) { Notify(result.Message); return; }
                        selectedSlot = -1; SaveNow(); SuccessFeedback(); Open(Page.Hospital); Notify(result.Message);
                    }, "primary");
                    readouts.Add(() =>
                    {
                        var unlocked = campaign.State.Reputation >= LifelineRules.TownUnlockReputation(destination);
                        b.SetEnabled(unlocked);
                        b.text = unlocked ? "Depart for " + TownName(destination) : "Earn " + LifelineRules.TownUnlockReputation(destination) + " reputation · " + campaign.State.Reputation + " so far";
                    });
                }
                else
                {
                    Text(stop, "TOWN PROJECTS", "eyebrow");
                    for (var index = 0; index < 2; index++)
                    {
                        var choice = index;
                        var b = Button(stop, "", () => Command(() => campaign.ChooseProject(choice)), "quiet");
                        readouts.Add(() => b.text = ((progress.ProjectCompletionMask & (1 << choice)) != 0 ? "Completed · " : progress.ProjectIndex == choice ? "Working on · " : "Choose · ") + LifelineRules.ProjectName(destination, choice));
                    }
                    LiveText(stop, () => ProjectSummary(progress), "item-detail");
                }
                LiveText(stop, () => progress.Completed + " residents cared for · " + progress.CompletedProjects + " projects restored", "item-detail");
            }
            Text(content, "The Weekly Call", "section-heading", true);
            Text(content, "One shared station shift. The same crew, budget and arrivals for everyone.", "paragraph");
            Button(content, "Plan this week’s shift", StartWeekly, "primary");
        }

        private void OpenCrew(bool forWeekly)
        {
            crewForWeekly = forWeekly; expandedCrew = -1;
            if (forWeekly) weeklyPaused = true;
            Open(Page.Crew);
        }
        private LifelineSimulation CrewSimulation => crewForWeekly && weekly != null ? weekly : campaign;
        private void BuildCrew()
        {
            var sim = CrewSimulation; var state = sim.State;
            var content = PageContent(crewForWeekly ? "Your shift crew." : "Good care takes a team.", "Give each person a main carriage. A second assignment lets them help when their first job is quiet.");
            if (crewForWeekly) Button(content, "← Back to the weekly shift", () => Open(Page.Weekly), "quiet");
            foreach (var crew in state.Crew)
            {
                var person = crew; var item = Box(content, "list-item");
                Text(item, person.Name + " · " + LifelineRules.RoleName(person.Role), "item-title");
                LiveText(item, () => CrewSummary(person, state), "item-detail");
                var action = Row(item, "room-actions");
                Button(action, expandedCrew == person.Id ? "Close plan" : "Change plan", () => { expandedCrew = expandedCrew == person.Id ? -1 : person.Id; BuildScreen(); }, "secondary");
                var train = Button(action, person.Level >= LifelineRules.MaxCrewLevel ? "Fully trained" : "Train · " + LifelineRules.TrainCost(person), () => Command(() => sim.Train(person.Id)), "secondary");
                readouts.Add(() => train.SetEnabled(!state.IsFinished && person.Level < LifelineRules.MaxCrewLevel && state.Coins >= LifelineRules.TrainCost(person)));
                if (expandedCrew != person.Id) continue;
                Text(item, "MAIN CARRIAGE", "eyebrow");
                foreach (var room in state.Carriages.OrderBy(c => c.Slot))
                {
                    var target = room;
                    var b = Button(item, (person.PrimaryRoomId == target.Id ? "Assigned · " : "") + "Carriage " + (target.Slot + 1) + " · " + RoomName(target.Kind) +
                        " · " + LifelineRules.TreatmentSeconds(target, person).ToString("0.#") + "s per visit", () => Command(() => sim.Assign(person.Id, target.Id,
                            person.SecondaryRoomId == target.Id ? -1 : person.SecondaryRoomId)), "quiet");
                    b.SetEnabled(!state.IsFinished);
                }
                if (person.PrimaryRoomId >= 0)
                {
                    Text(item, "HELP WHEN FREE", "eyebrow");
                    foreach (var room in state.Carriages.Where(c => c.Id != person.PrimaryRoomId).OrderBy(c => c.Slot))
                    {
                        var target = room;
                        Button(item, (person.SecondaryRoomId == target.Id ? "Helping · " : "Help · ") + RoomName(target.Kind) + " in carriage " + (target.Slot + 1),
                            () => Command(() => sim.Assign(person.Id, person.PrimaryRoomId, person.SecondaryRoomId == target.Id ? -1 : target.Id)), "quiet").SetEnabled(!state.IsFinished);
                    }
                }
                Button(item, "Rest at the platform", () => Command(() => sim.Assign(person.Id, -1)), "quiet").SetEnabled(!state.IsFinished);
            }
            if (state.Crew.Count < LifelineRules.MaxCrew && !crewForWeekly)
            {
                Text(content, "A new face on the platform", "section-heading", true);
                Text(content, LifelineRules.NextHireName(state.Crew.Count) + " · " + LifelineRules.RoleName(LifelineRules.NextHireRole(state.Crew.Count)), "item-title");
                var hire = Button(content, "", () => Command(() => campaign.Hire()), "primary");
                readouts.Add(() =>
                {
                    var cost = LifelineRules.HireCost(state.Crew.Count); var reputation = LifelineRules.HireReputation(state.Crew.Count);
                    hire.text = state.Reputation < reputation ? "Earn " + reputation + " reputation to recruit · " + state.Reputation + " so far" : "Welcome a crew member · " + cost + " funds";
                    hire.SetEnabled(state.Reputation >= reputation && state.Coins >= cost);
                });
            }
            Text(content, "Who gets seen first?", "section-heading", true);
            Text(content, "Long waits, quick visits or the town project: choose the priority your crew follows.", "item-detail");
            foreach (ServicePolicy policy in Enum.GetValues(typeof(ServicePolicy)))
            {
                var choice = policy;
                Button(content, (state.Policy == choice ? "Selected · " : "") + LifelineRules.PolicyName(choice), () => Command(() => sim.SetPolicy(choice)), "preference").SetEnabled(!state.IsFinished);
            }
        }

        private string CrewSummary(CrewState person, SimulationState state)
        {
            var primary = state.Carriages.FirstOrDefault(c => c.Id == person.PrimaryRoomId);
            var secondary = state.Carriages.FirstOrDefault(c => c.Id == person.SecondaryRoomId);
            return "Training " + person.Level + " · " + (primary == null ? "Resting at the platform" : RoomName(primary.Kind) + " in carriage " + (primary.Slot + 1)) +
                (secondary == null ? "" : "\nHelps " + RoomName(secondary.Kind).ToLowerInvariant() + " when free");
        }
    }
}
