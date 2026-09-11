using System;
using System.Linq;
using LittleLifeline.Core;
using UnityEngine.UIElements;

namespace LittleLifeline.App
{
    public sealed partial class LifelineApp
    {
        private enum CrewHUDPanel { Assignments, Training, Hiring, Policy }
        private int crewHUDPersonId;
        private bool crewHUDHelping;
        private CrewHUDPanel crewHUDPanel;

        private void BuildCrewHUD()
        {
            var sim = CrewSimulation;
            var state = sim.State;
            var person = state.Crew.FirstOrDefault(c => c.Id == crewHUDPersonId) ?? state.Crew[0];
            crewHUDPersonId = person.Id;
            if (crewForWeekly && crewHUDPanel == CrewHUDPanel.Hiring) crewHUDPanel = CrewHUDPanel.Assignments;
            var dock = Dock(pageBody);
            dock.name = "crew-hud";
            dock.style.maxHeight = 230;
            var roster = Row(dock);
            foreach (var member in state.Crew)
            {
                var chosen = member;
                var pick = IconAction(roster, CrewRoleGlyph(chosen.Role), "Select " + chosen.Name + ", " + LifelineRules.RoleName(chosen.Role),
                    () => { crewHUDPersonId = chosen.Id; BuildScreen(); }, chosen.Name);
                CompactManagementButton(pick);
                pick.name = "crew-person-" + chosen.Id;
                pick.EnableInClassList("selected", person.Id == chosen.Id);
            }

            var identity = Row(dock);
            identity.style.minHeight = 24;
            var name = Text(identity, person.Name + " · " + LifelineRules.RoleName(person.Role), "grow");
            name.style.fontSize = 14;
            var level = Text(identity, "Lv " + person.Level);
            level.style.fontSize = 14;
            var details = Row(dock);
            details.style.minHeight = 46;
            switch (crewHUDPanel)
            {
                case CrewHUDPanel.Training: BuildCrewTraining(details, sim, person); break;
                case CrewHUDPanel.Hiring: BuildCrewHiring(details, sim); break;
                case CrewHUDPanel.Policy: BuildCrewPolicy(details, sim); break;
                default: BuildCrewAssignments(details, sim, person); break;
            }

            var tabs = Row(dock);
            CrewPanelTab(tabs, CrewHUDPanel.Assignments, GameGlyph.Arrange, "Plan");
            CrewPanelTab(tabs, CrewHUDPanel.Training, GameGlyph.Upgrade, "Train");
            if (!crewForWeekly && state.Crew.Count < LifelineRules.MaxCrew)
                CrewPanelTab(tabs, CrewHUDPanel.Hiring, GameGlyph.Plus, "Recruit");
            CrewPanelTab(tabs, CrewHUDPanel.Policy, GameGlyph.Flag, "Priority");
            if (crewForWeekly)
            {
                var back = IconAction(tabs, GameGlyph.Back, "Return to the weekly shift", () => Open(Page.Weekly));
                CompactManagementButton(back);
                back.name = "crew-back-weekly";
            }
        }

        private void BuildCrewAssignments(VisualElement row, LifelineSimulation sim, CrewState person)
        {
            if (person.PrimaryRoomId < 0) crewHUDHelping = false;
            var mode = IconAction(row, crewHUDHelping ? GameGlyph.Crew : GameGlyph.Person,
                crewHUDHelping ? "Choose a main carriage instead" : "Choose a second carriage to help when free",
                () => { crewHUDHelping = !crewHUDHelping; BuildScreen(); }, crewHUDHelping ? "Help" : "Main");
            CompactManagementButton(mode);
            mode.name = "crew-assignment-mode";
            mode.SetEnabled(person.PrimaryRoomId >= 0);
            for (var slot = 0; slot < LifelineRules.CarriageSlots; slot++)
            {
                var room = sim.State.Carriages.FirstOrDefault(c => c.Slot == slot);
                var target = room;
                var title = "Carriage " + (slot + 1) + (room == null ? ": empty" : ": " + RoomName(room.Kind));
                var assign = IconAction(row, room == null ? GameGlyph.Plus : CrewRoomGlyph(room.Kind), title,
                    () =>
                    {
                        if (target == null) return;
                        Command(() => crewHUDHelping
                            ? sim.Assign(person.Id, person.PrimaryRoomId, person.SecondaryRoomId == target.Id ? -1 : target.Id)
                            : sim.Assign(person.Id, target.Id, person.SecondaryRoomId == target.Id ? -1 : person.SecondaryRoomId));
                    }, (slot + 1).ToString());
                CompactManagementButton(assign);
                assign.name = "crew-carriage-" + slot;
                assign.SetEnabled(!sim.State.IsFinished && room != null && (!crewHUDHelping || room.Id != person.PrimaryRoomId));
                assign.EnableInClassList("selected", room != null && room.Id == (crewHUDHelping ? person.SecondaryRoomId : person.PrimaryRoomId));
                if (room != null) assign.tooltip += " · " + LifelineRules.TreatmentSeconds(room, person).ToString("0.#") + "s care";
            }
            var rest = IconAction(row, GameGlyph.Pause, "Rest at the platform; clear both assignments",
                () => Command(() => sim.Assign(person.Id, -1)), "Rest");
            CompactManagementButton(rest);
            rest.name = "crew-rest";
            rest.SetEnabled(!sim.State.IsFinished);
            rest.EnableInClassList("selected", person.PrimaryRoomId < 0);
        }

        private void BuildCrewTraining(VisualElement row, LifelineSimulation sim, CrewState person)
        {
            var capped = person.Level >= LifelineRules.MaxCrewLevel;
            var cost = LifelineRules.TrainCost(person);
            Text(row, capped ? "Fully trained" : "Lv " + person.Level + " → " + (person.Level + 1), "grow").style.fontSize = 14;
            if (!capped) IconStat(row, GameGlyph.Coin, () => cost.ToString());
            var train = IconAction(row, GameGlyph.Upgrade, "Train " + person.Name,
                () => Command(() => sim.Train(person.Id)), capped ? "Max" : "Train");
            CompactManagementButton(train, false);
            train.name = "crew-train";
            readouts.Add(() => train.SetEnabled(!sim.State.IsFinished && !capped && sim.State.Coins >= cost));
        }

        private void BuildCrewHiring(VisualElement row, LifelineSimulation sim)
        {
            var state = sim.State;
            if (crewForWeekly || state.Crew.Count >= LifelineRules.MaxCrew)
            { Text(row, "Crew complete", "grow").style.fontSize = 14; return; }
            var name = LifelineRules.NextHireName(state.Crew.Count);
            var role = LifelineRules.NextHireRole(state.Crew.Count);
            var cost = LifelineRules.HireCost(state.Crew.Count);
            var reputation = LifelineRules.HireReputation(state.Crew.Count);
            row.Add(new LifelineIcon(CrewRoleGlyph(role), size: 20));
            Text(row, name, "grow").style.fontSize = 14;
            row.tooltip = name + " · " + LifelineRules.RoleName(role);
            IconStat(row, GameGlyph.Coin, () => cost.ToString());
            var unlock = IconStat(row, GameGlyph.Star, () => state.Reputation + "/" + reputation);
            readouts.Add(() => unlock.style.display = state.Reputation < reputation ? DisplayStyle.Flex : DisplayStyle.None);
            var hire = IconAction(row, GameGlyph.Plus, "Recruit " + name + ", " + LifelineRules.RoleName(role),
                () => Command(() => sim.Hire()));
            CompactManagementButton(hire, false);
            hire.name = "crew-hire";
            readouts.Add(() => hire.SetEnabled(!state.IsFinished && state.Reputation >= reputation && state.Coins >= cost));
        }

        private void BuildCrewPolicy(VisualElement row, LifelineSimulation sim)
        {
            foreach (ServicePolicy policy in Enum.GetValues(typeof(ServicePolicy)))
            {
                var choice = policy;
                var glyph = choice == ServicePolicy.OldestFirst ? GameGlyph.Clock : choice == ServicePolicy.ShortVisitsFirst ? GameGlyph.Play : GameGlyph.Flag;
                var caption = choice == ServicePolicy.OldestFirst ? "Waiting" : choice == ServicePolicy.ShortVisitsFirst ? "Quick" : "Project";
                var select = IconAction(row, glyph, LifelineRules.PolicyName(choice), () => Command(() => sim.SetPolicy(choice)), caption);
                CompactManagementButton(select);
                select.name = "crew-policy-" + choice;
                select.EnableInClassList("selected", sim.State.Policy == choice);
                select.SetEnabled(!sim.State.IsFinished);
            }
        }

        private void CrewPanelTab(VisualElement row, CrewHUDPanel panel, GameGlyph glyph, string caption)
        {
            var button = IconAction(row, glyph, caption, () => { crewHUDPanel = panel; BuildScreen(); }, caption);
            CompactManagementButton(button);
            button.name = "crew-panel-" + panel;
            button.EnableInClassList("selected", crewHUDPanel == panel);
        }

        private static void CompactManagementButton(Button button, bool grow = true)
        {
            button.style.minWidth = 44;
            button.style.minHeight = 44;
            button.style.height = 48;
            button.style.flexDirection = FlexDirection.Column;
            button.style.justifyContent = Justify.Center;
            button.style.alignItems = Align.Center;
            button.style.flexGrow = grow ? 1 : 0;
            if (grow) button.style.flexBasis = 0;
            button.style.marginTop = button.style.marginBottom = 1;
            button.style.marginLeft = button.style.marginRight = .5f;
            button.style.paddingTop = button.style.paddingBottom = 2;
            button.style.paddingLeft = button.style.paddingRight = 2;
            button.style.fontSize = 14;
            button.Query<Label>().ForEach(label => label.style.fontSize = 14);
        }

        private static GameGlyph CrewRoomGlyph(RoomKind room)
            => room == RoomKind.Consultation ? GameGlyph.Clinic : room == RoomKind.Diagnostics ? GameGlyph.Diagnostics : GameGlyph.Recovery;
        private static GameGlyph CrewRoleGlyph(CrewRole role)
            => role == CrewRole.Doctor ? GameGlyph.Clinic : role == CrewRole.Technician ? GameGlyph.Diagnostics : GameGlyph.Recovery;
    }
}
