using System;
using System.Collections.Generic;
using System.Linq;
using LittleLifeline.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LittleLifeline.App
{
    public sealed partial class LifelineApp
    {
        private readonly Dictionary<int, VisualElement> roomMarkers = new Dictionary<int, VisualElement>();

        private VisualElement Dock(VisualElement parent, int bottom = 76)
        {
            var dock = Box(parent, "game-dock");
            dock.style.bottom = bottom;
            return dock;
        }
        private Button IconAction(VisualElement parent, GameGlyph glyph, string hint, Action clicked, string caption = null, string css = null)
        {
            var button = new Button(clicked) { name = hint, tooltip = hint };
            button.AddToClassList("icon-control");
            if (!string.IsNullOrEmpty(css)) foreach (var name in css.Split(' ')) if (name.Length > 0) button.AddToClassList(name);
            var light = css != null && css.Contains("primary");
            button.Add(new LifelineIcon(glyph, light ? new Color(.97f,.95f,.89f) : (Color?)null, 25));
            if (!string.IsNullOrEmpty(caption)) Text(button, caption, "icon-caption").pickingMode = PickingMode.Ignore;
            parent.Add(button); return button;
        }
        private VisualElement IconStat(VisualElement parent, GameGlyph glyph, Func<string> value)
        {
            var stat = Row(parent, "icon-stat");
            stat.Add(new LifelineIcon(glyph, null, 18));
            LiveText(stat, value, "stat-number");
            return stat;
        }
        private static GameGlyph RoomGlyph(RoomKind kind) => kind == RoomKind.Diagnostics ? GameGlyph.Diagnostics : kind == RoomKind.Recovery ? GameGlyph.Recovery : GameGlyph.Clinic;

        private void BuildGameHeader()
        {
            var weeklyContext = page == Page.Weekly || page == Page.Crew && crewForWeekly;
            var header = Row(pageBody, "game-header");
            var destination = Box(header, "destination-chip");
            Text(destination, weeklyContext ? "Weekly Call" : TownName(campaign.State.Town), "place-name", true);
            var gap = Box(header, "grow");
            var funds = IconStat(header, GameGlyph.Coin, () => State.Coins.ToString("N0")); funds.AddToClassList("funds-chip");
            if (!weeklyContext)
            {
                var reputation = IconStat(header, GameGlyph.Star, () => campaign.State.Reputation.ToString()); reputation.AddToClassList("funds-chip");
            }
            IconAction(header, GameGlyph.Depot, "Depot and settings", () => Open(Page.Depot), null, "hud-round");
        }
        private void BuildGameNavigation()
        {
            var nav = Row(screen, "game-nav");
            var hospital = IconAction(nav, GameGlyph.Train, "Hospital", () => Open(Page.Hospital), null, "nav-icon");
            var route = IconAction(nav, GameGlyph.Map, "Route and town projects", () => Open(Page.Route), null, "nav-icon");
            var crew = IconAction(nav, GameGlyph.Crew, "Crew", () => OpenCrew(false), null, "nav-icon");
            var weeklyButton = IconAction(nav, GameGlyph.Trophy, "The Weekly Call", StartWeekly, null, "nav-icon");
            hospital.EnableInClassList("selected", page == Page.Hospital);
            route.EnableInClassList("selected", page == Page.Route);
            crew.EnableInClassList("selected", page == Page.Crew);
            weeklyButton.EnableInClassList("selected", page == Page.Weekly);
        }
        private void BuildRoomMarkers()
        {
            if (intro && page == Page.Hospital) return;
            for (var index = 0; index < LifelineRules.CarriageSlots; index++)
            {
                var slot = index;
                var room = State.Carriages.FirstOrDefault(c => c.Slot == slot);
                var marker = IconAction(pageBody, room == null ? GameGlyph.Plus : RoomGlyph(room.Kind),
                    room == null ? "Build carriage " + (slot + 1) : RoomName(room.Kind) + ", carriage " + (slot + 1), () => SelectCarriage(slot), null, "room-marker");
                if (room != null)
                {
                    var roomId = room.Id;
                    var count = LiveText(marker, () => Active.QueueFor(room.Kind).ToString(), "queue-count");
                    count.pickingMode = PickingMode.Ignore;
                    readouts.Add(() =>
                    {
                        count.style.display = Active.QueueFor(room.Kind) > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                        marker.EnableInClassList("needs-crew", !State.Crew.Any(c => c.PrimaryRoomId == roomId || c.SecondaryRoomId == roomId));
                    });
                }
                roomMarkers[slot] = marker;
            }
        }
        private void PositionRoomMarkers()
        {
            if (board == null || world == null || world.SceneCamera == null) return;
            var width = board.resolvedStyle.width; var height = board.resolvedStyle.height;
            if (width < 1 || height < 1) return;
            foreach (var marker in roomMarkers)
            {
                var p = world.SceneCamera.WorldToViewportPoint(world.CarriageCenter(marker.Key) + Vector3.up * 1.35f);
                var visible = selectedSlot < 0 && p.z > 0 && p.x > .04f && p.x < .96f && p.y > .11f && p.y < .90f;
                marker.Value.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
                marker.Value.style.left = p.x * width - 22;
                marker.Value.style.top = (1 - p.y) * height - 22;
            }
        }

        private void BuildHospitalHUD()
        {
            BuildRoomMarkers();
            if (intro)
            {
                var introDock = Dock(pageBody);
                var introRow = Row(introDock, "hud-spaced");
                Text(introRow, "Little Lifeline", "dock-title grow", true);
                IconAction(introRow, GameGlyph.Play, "Open your clinic", () =>
                { intro = false; campaign.Advance(.1); SaveNow(); BuildScreen(); Notify("Tap a carriage to look inside."); }, "Open clinic", "primary wide-icon");
                return;
            }
            if (returnReport != null && returnReport.applied && returnReport.elapsedSeconds >= 60)
            {
                var welcome = Dock(pageBody); var row = Row(welcome, "hud-spaced");
                var summary = Box(row, "grow");
                Text(summary, "While you were away", "dock-title", true);
                var stats = Row(summary);
                IconStat(stats, GameGlyph.Person, () => returnReport == null ? "" : "+" + returnReport.completed);
                IconStat(stats, GameGlyph.Coin, () => returnReport == null ? "" : "+" + returnReport.coinsEarned.ToString("N0"));
                IconAction(row, GameGlyph.Check, "Continue with your saved progress", () => { returnReport = null; BuildScreen(); }, null, "primary hud-round");
                return;
            }
            if (selectedSlot >= 0) { BuildCarriageHUD(); return; }
            var dock = Dock(pageBody); dock.AddToClassList("mission-dock");
            if (campaign.State.Carriages.Count < 2)
            {
                var row = Row(dock, "hud-spaced");
                Text(row, campaign.State.TotalCompleted == 0 ? "Your first resident is on the way" : "Give the train room to grow", "hud-hint grow");
                IconAction(row, campaign.State.TotalCompleted == 0 ? GameGlyph.Clinic : GameGlyph.Plus, "Choose a carriage", () => SelectCarriage(campaign.State.TotalCompleted == 0 ? 0 : 1), null, "primary hud-round");
            }
            else
            {
                var project = TownProgressFor(campaign.State.Town);
                var row = Row(dock, "hud-spaced");
                IconAction(row, GameGlyph.Flag, LifelineRules.ProjectName(project.Town, project.ProjectIndex), () => Open(Page.Route), null, "hud-round");
                var progress = Box(row, "grow");
                LiveText(progress, () => project.ProjectProgress + " / " + LifelineRules.ProjectGoal(project.Town, project.ProjectIndex), "stat-number");
                var track = Box(progress, "progress-track"); var fill = Box(track, "progress-fill");
                readouts.Add(() => fill.style.width = Length.Percent(Math.Min(100, 100f * project.ProjectProgress / LifelineRules.ProjectGoal(project.Town, project.ProjectIndex))));
                var queue = IconAction(row, GameGlyph.Clinic, "Look at the busiest queue", () =>
                {
                    var bottleneck = campaign.Bottleneck;
                    if (!bottleneck.HasValue) return;
                    var target = campaign.State.Carriages.FirstOrDefault(c => c.Kind == bottleneck.Value);
                    SelectCarriage(target?.Slot ?? FirstEmptySlot());
                }, "0", "hud-round");
                readouts.Add(() =>
                {
                    var bottleneck = campaign.Bottleneck;
                    queue.style.display = bottleneck.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
                    if (!bottleneck.HasValue) return;
                    queue.Q<LifelineIcon>().Glyph = RoomGlyph(bottleneck.Value);
                    queue.Q<Label>().text = campaign.QueueFor(bottleneck.Value).ToString();
                    queue.tooltip = RoomName(bottleneck.Value) + " · busiest queue";
                });
            }
        }
        private int FirstEmptySlot()
        {
            for (var slot = 0; slot < LifelineRules.CarriageSlots; slot++) if (!State.Carriages.Any(c => c.Slot == slot)) return slot;
            return 0;
        }

        private void BuildCarriageHUD()
        {
            var dock = Dock(pageBody);
            var room = State.Carriages.FirstOrDefault(c => c.Slot == selectedSlot);
            var heading = Row(dock, "hud-spaced");
            heading.Add(new LifelineIcon(room == null ? GameGlyph.Plus : RoomGlyph(room.Kind)));
            Text(heading, room == null ? "New carriage" : RoomName(room.Kind), "dock-title grow", true);
            if (room != null)
            {
                IconStat(heading, GameGlyph.Crew, () => State.Crew.Count(c => c.PrimaryRoomId == room.Id || c.SecondaryRoomId == room.Id).ToString());
                IconStat(heading, GameGlyph.Clock, () => Active.QueueFor(room.Kind).ToString());
            }
            IconAction(heading, GameGlyph.Close, "Whole train", () => SelectCarriage(-1), null, "hud-close");
            if (room == null || roomToRefit == room.Id)
            {
                var choices = Row(dock, "hud-actions");
                foreach (RoomKind kind in Enum.GetValues(typeof(RoomKind)))
                {
                    var choice = kind;
                    if (room != null && room.Kind == kind) continue;
                    var cost = room == null ? LifelineRules.BuildCost(kind) : LifelineRules.RefitCost(kind);
                    var action = IconAction(choices, RoomGlyph(kind), (room == null ? "Build " : "Refit as ") + RoomName(kind) + " for " + cost + " funds", () =>
                    {
                        roomToRefit = -1;
                        Command(() => room == null ? Active.Build(choice, selectedSlot) : Active.Refit(room.Id, choice));
                    }, cost.ToString(), "choice-icon");
                    readouts.Add(() => action.SetEnabled(!State.IsFinished && State.Coins >= cost &&
                        (room == null || room.Kind != RoomKind.Consultation || State.Carriages.Count(c => c.Kind == RoomKind.Consultation) > 1)));
                }
                if (room != null && room.Kind == RoomKind.Consultation && State.Carriages.Count(c => c.Kind == RoomKind.Consultation) == 1)
                    Text(dock, "Keep one clinic to welcome residents.", "hud-hint");
                return;
            }
            if (roomToArrange == room.Id)
            {
                var positions = Row(dock, "hud-actions");
                for (var i = 0; i < LifelineRules.CarriageSlots; i++)
                {
                    var target = i;
                    var button = IconAction(positions, GameGlyph.Train, "Preview place " + (target + 1), () => { moveTargetSlot = target; BuildScreen(); }, (i + 1).ToString(), moveTargetSlot == i ? "primary choice-icon" : "choice-icon");
                    button.SetEnabled(i != room.Slot && !State.IsFinished);
                }
                if (moveTargetSlot >= 0)
                {
                    var preview = Row(dock, "hud-spaced");
                    Text(preview, TransferSummary(room.Id, moveTargetSlot).Replace("shortest clinic transfer: ", ""), "hud-hint grow");
                    IconAction(preview, GameGlyph.Check, "Move carriage for free", () =>
                    { var target = moveTargetSlot; roomToArrange = -1; moveTargetSlot = -1; Command(() => Active.Reorder(room.Id, target)); }, "Move", "primary hud-round");
                }
                return;
            }
            if (State.HasPendingRefit && State.PendingRefitRoomId == room.Id || State.PendingMoveRoomId >= 0)
            {
                var pending = Row(dock); pending.Add(new LifelineIcon(GameGlyph.Clock));
                Text(pending, "Finishing the current visit…", "hud-hint"); return;
            }
            var needsCrew = !State.Crew.Any(c => c.PrimaryRoomId == room.Id || c.SecondaryRoomId == room.Id);
            var actions = Row(dock, "hud-actions");
            var upgrade = IconAction(actions, room.Level >= LifelineRules.MaxRoomLevel ? GameGlyph.Check : GameGlyph.Upgrade,
                "Improve " + RoomName(room.Kind), () => Command(() => Active.Upgrade(room.Id)),
                room.Level >= LifelineRules.MaxRoomLevel ? "Max" : LifelineRules.UpgradeCost(room).ToString(), needsCrew ? "choice-icon" : "primary choice-icon");
            readouts.Add(() => upgrade.SetEnabled(!State.IsFinished && room.Level < LifelineRules.MaxRoomLevel && State.Coins >= LifelineRules.UpgradeCost(room)));
            IconAction(actions, GameGlyph.Crew, "Assign crew", () => OpenCrew(page == Page.Weekly), needsCrew ? "Assign" : null, needsCrew ? "primary choice-icon" : "choice-icon");
            IconAction(actions, GameGlyph.Arrange, "Arrange carriages", () => { roomToArrange = room.Id; moveTargetSlot = -1; BuildScreen(); }, null, "choice-icon").SetEnabled(!State.IsFinished);
            IconAction(actions, GameGlyph.Refit, "Change carriage purpose", () => { roomToRefit = room.Id; BuildScreen(); }, null, "choice-icon").SetEnabled(!State.IsFinished);
        }

        private void BuildWeeklyHUD()
        {
            if (weekly == null) { StartWeekly(); return; }
            var shift = Row(pageBody, "shift-hud");
            IconStat(shift, GameGlyph.Clock, () => FormatClock(weekly.TimeRemaining));
            IconStat(shift, GameGlyph.Person, () => weekly.State.TotalCompleted.ToString());
            IconAction(shift, weeklyPaused ? GameGlyph.Play : GameGlyph.Pause, weeklyPaused ? "Start or resume shift" : "Pause to plan", () =>
            { weeklyPaused = !weeklyPaused; BuildScreen(); }, null, "hud-round").SetEnabled(!weekly.State.IsFinished);
            BuildRoomMarkers();
            if (weekly.State.IsFinished)
            {
                var dock = Dock(pageBody); var heading = Row(dock, "hud-spaced");
                Text(heading, "Shift complete", "dock-title grow", true);
                IconStat(heading, GameGlyph.Person, () => weekly.State.TotalCompleted.ToString());
                var actions = Row(dock, "hud-actions");
                IconAction(actions, GameGlyph.Play, "Try a different plan", () => { weekly = null; StartWeekly(); }, "Replay", "primary choice-icon");
                IconAction(actions, GameGlyph.Trophy, "View weekly rankings", ShowWeeklyRanks, "Rankings", "choice-icon");
                IconAction(actions, GameGlyph.Crew, "Review your crew", () => OpenCrew(true), "Crew", "choice-icon");
                return;
            }
            if (selectedSlot >= 0) { BuildCarriageHUD(); return; }
            var plan = Dock(pageBody); var row = Row(plan, "hud-spaced");
            if (weeklyPaused)
                Text(row, DateTimeOffset.UtcNow < new DateTimeOffset(2026,9,14,0,0,0,TimeSpan.Zero) ? "Practice · rankings from 14 Sep" : "Plan your four-minute shift", "hud-hint grow");
            else Text(row, "", "grow");
            IconAction(row, GameGlyph.Crew, "Plan the shift crew", () => OpenCrew(true), null, "hud-round");
            IconAction(row, GameGlyph.Trophy, "Weekly rankings", ShowWeeklyRanks, null, "hud-round");
        }
    }
}
