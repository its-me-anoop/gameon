using System;
using System.Linq;
using LittleLifeline.Core;
using OrbitOrchard.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace LittleLifeline.App
{
    public sealed partial class LifelineApp
    {
        private void BuildDepot()
        {
            var content = PageContent("At the depot.", "Make the train feel like yours, or settle in for a quieter journey.");
            Button(content, "Train finishes & crew outfits", () => Open(Page.Wardrobe), "primary");
            Text(content, "Your journey so far", "section-heading", true);
            LiveText(content, () => campaign.State.TotalCompleted.ToString("N0") + " residents cared for\n" + campaign.State.Reputation + " reputation · " + campaign.State.Towns.Sum(t => t.CompletedProjects) + " town projects", "paragraph");
            Text(content, "Make yourself comfortable", "section-heading", true);
            Preference(content, "Sound", profile.preferences.sound, () => profile.preferences.sound = !profile.preferences.sound);
            Preference(content, "Haptics", profile.preferences.haptics, () => profile.preferences.haptics = !profile.preferences.haptics);
            Preference(content, "Reduce motion", profile.preferences.reducedMotion, () => profile.preferences.reducedMotion = !profile.preferences.reducedMotion);
            if (apple.IsReduceMotionEnabled) Text(content, "Your iPhone’s Reduce Motion setting is also respected.", "item-detail");
            Text(content, "A hand with the hospital", "section-heading", true);
            Button(content, "How the train works", () => Open(Page.Guide), "quiet");
            Button(content, "Restore purchases", apple.RestorePurchases, "quiet");
            LiveText(content, () => apple.IsRestoring ? "Restoring your collection…" : apple.StoreStatus, "item-detail");
            Text(content, "Your progress stays on this device. Purchases can be restored with the Apple account that bought them.", "paragraph");
        }
        private void Preference(VisualElement parent, string title, bool on, Action change)
        {
            Button(parent, title + " · " + (on ? "On" : "Off"), () => { change(); SaveNow(); BuildScreen(); }, "preference");
        }

        private void BuildGuide()
        {
            var content = PageContent("A good little system.", "Watch the people. They will show you what the hospital needs.");
            GuideStep(content, "01", "Start with a complete visit", "Residents arrive on the platform, visit consultation, then move to tests or recovery if needed. Your crew handles care automatically.");
            GuideStep(content, "02", "Give every carriage a crew", "Tap a carriage, then Assign crew. A doctor is quickest in consultation, a technician in diagnostics and a nurse in recovery. Everyone can help with other work too.");
            GuideStep(content, "03", "Follow the queue", "An empty room with waiting residents needs a crew member. A busy room may need an improvement or another carriage. Staff can help in a second room when their main job is quiet.");
            GuideStep(content, "04", "Arrange for the next town", "Keep frequently connected rooms close together to shorten walking. Move a carriage for free; your crew will finish current care before it moves.");
            GuideStep(content, "05", "Leave something better", "Town projects track the care you provide. Completing them changes the station and earns a construction grant. Choose another project or visit a new town; your progress stays with you.");
            GuideStep(content, "06", "Come back when you like", "The configured hospital keeps working for up to 24 hours while you are away. There is no missed-day penalty or energy meter.");
            GuideStep(content, "07", "Try the Weekly Call", "Plan a four-minute shift with the same starting resources as everyone else. Pause to think, try again freely, and compare your best result in Game Center. Your purchased finishes and home upgrades never add power.");
            Button(content, "Back to the hospital", () => Open(Page.Hospital), "primary");
        }
        private void GuideStep(VisualElement parent, string number, string title, string description)
        {
            var step = Box(parent, "list-item"); Text(step, number, "eyebrow");
            Text(step, title, "section-heading", true); Text(step, description, "paragraph");
        }

        private void BuildWardrobe()
        {
            AddWorld(false);
            board.style.minHeight = 190; board.style.maxHeight = 270;
            var content = PageContent("Made yours.", "Preview a whole-train collection: paintwork, room fabrics and matching crew accents.");
            for (var i = 0; i < LiveryIds.Length; i++)
            {
                var index = i; var id = LiveryIds[i];
                var item = Box(content, "list-item");
                Text(item, LiveryNames[i], "item-title");
                Text(item, i == 0 ? "Sage carriages and warm oat interiors. Included." : i == 1 ? "Apricot paintwork, brass details and warm fabrics." : i == 2 ? "Mineral blue carriages and fresh linen accents." : "Bottle green paintwork and golden railway details.", "item-detail");
                var row = Row(item, "room-actions");
                Button(row, previewLivery == id ? "Previewing" : "Preview", () => { previewLivery = id; world.Focus(-1, ReducedMotion); BuildScreen(); }, "secondary");
                if (i == 0 || apple.IsPassOwned)
                    Button(row, ActiveLivery == id ? "On your train" : "Use this finish", () =>
                    {
                        profile.preferences.livery = id; previewLivery = id; SaveNow(); SuccessFeedback(); BuildScreen();
                    }, "primary").SetEnabled(ActiveLivery != id);
            }
            Text(content, "Founder’s Carriage Collection", "section-heading", true);
            Text(content, apple.IsPassOwned ? "Your collection is yours to keep. Previous Plus purchases include these finishes." :
                "Sunrise, Coastal and Heritage finishes in one permanent collection. Purely cosmetic; care, income and rankings stay the same.", "paragraph");
            if (!apple.IsPassOwned)
            {
                var product = apple.Products.FirstOrDefault(p => p.id == AppleServices.PassProductId);
                var buy = Button(content, product == null ? apple.IsStoreLoading ? "Finding the collection…" : "Collection unavailable" : "Get the collection · " + product.price,
                    () => apple.Purchase(AppleServices.PassProductId), "primary");
                buy.SetEnabled(product != null && !apple.IsPurchasing && !apple.IsRestoring);
            }
            LiveText(content, () => apple.IsPurchasing ? "Complete your purchase with Apple." : apple.StoreStatus, "item-detail");
            var serviceActions = Row(content, "room-actions");
            Button(serviceActions, "Restore purchases", apple.RestorePurchases, "quiet");
            Button(serviceActions, "Refresh store", apple.LoadProducts, "quiet");
            Text(content, "Leave a little thank-you", "section-heading", true);
            Text(content, "Optional tips support the game. They do not unlock items or improve scores.", "item-detail");
            foreach (var id in new[] { AppleServices.SmallTipProductId, AppleServices.MediumTipProductId, AppleServices.LargeTipProductId })
            {
                var product = apple.Products.FirstOrDefault(p => p.id == id);
                if (product != null) Button(content, "Tip · " + product.price, () => apple.Purchase(product.id), "quiet").SetEnabled(!apple.IsPurchasing && !apple.IsRestoring);
            }
        }

        private void StartWeekly()
        {
            if (apple.IsAuthenticating || apple.IsPurchasing || apple.IsRestoring)
            { Notify("Finish the Apple window, then open your shift."); return; }
            var id = LifelineRules.WeeklyIdentifier(DateTimeOffset.UtcNow);
            if (weekly == null || weekly.State.IsFinished || weekly.State.WeeklyId != id)
            {
                weekly = LifelineSimulation.CreateWeekly(DateTimeOffset.UtcNow);
                weeklyRecorded = false;
            }
            weeklyPaused = true; selectedSlot = -1; selectedCarriageId = -1; Open(Page.Weekly);
        }
        private void BuildWeekly()
        {
            if (weekly == null) { StartWeekly(); return; }
            var status = Box(pageBody, "next-step");
            if (DateTimeOffset.UtcNow < new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero))
                Text(status, "Practice is open. Weekly rankings begin 14 September, 00:00 UTC.", "next-detail");
            var row = Row(status);
            LiveText(row, () => "The Weekly Call · " + FormatClock(weekly.TimeRemaining), "next-title grow");
            var control = Button(row, weeklyPaused ? "Start shift" : "Pause", () =>
            {
                if (weekly.State.IsFinished) return;
                weeklyPaused = !weeklyPaused; BuildScreen();
            }, "secondary small");
            control.SetEnabled(!weekly.State.IsFinished);
            LiveText(status, () => weekly.State.TotalCompleted + " cared for · " + weekly.State.Patients.Count(p => p.Phase == PatientPhase.Waiting) + " waiting" +
                (weeklyPaused && !weekly.State.IsFinished ? " · planning paused" : ""), "next-detail");
            AddWorld();
            if (weekly.State.IsFinished)
            {
                var result = Box(pageBody, "sheet");
                Text(result, "Your shift is complete.", "sheet-title", true);
                Text(result, weekly.State.TotalCompleted + " residents cared for\n" + weekly.LeaderboardScore.ToString("N0") + " care points", "sheet-info");
                Text(result, "Completed visits decide the ranking; shorter waiting breaks ties.", "sheet-info");
                var actions = Row(result, "room-actions");
                Button(actions, "Try another plan", () => { weekly = null; StartWeekly(); }, "primary");
                Button(actions, "Rankings", ShowWeeklyRanks, "secondary");
                LiveText(result, () => apple.GameCenterStatus, "sheet-info");
                return;
            }
            CarriageStrip();
            if (selectedSlot >= 0) CarriageSheet();
            else
            {
                var sheet = Box(pageBody, "sheet");
                Text(sheet, "A fair start. Your own plan.", "sheet-title", true);
                Text(sheet, "Arrange the carriages and crew before starting. Pause whenever you need to think.", "sheet-info");
                var rowActions = Row(sheet, "room-actions");
                Button(rowActions, "Plan the crew", () => OpenCrew(true), "secondary");
                Button(rowActions, "Rankings", ShowWeeklyRanks, "secondary");
                LiveText(sheet, () => apple.GameCenterStatus, "sheet-info");
            }
        }
        private void CompleteWeekly()
        {
            if (weeklyRecorded) return;
            weeklyRecorded = true; weeklyPaused = true;
            var score = weekly.LeaderboardScore;
            profile.AddWeeklyRecord(weekly.State.WeeklyId, score, DateTimeOffset.UtcNow);
            SaveNow(); apple.SubmitWeeklyScore(score, weekly.State.WeeklyId);
            SuccessFeedback(); BuildScreen();
        }
        private void ShowWeeklyRanks()
        {
            weeklyPaused = true;
            if (apple.IsGameCenterAuthenticated) apple.ShowWeeklyLeaderboard();
            else apple.AuthenticateGameCenter();
        }
        private static string FormatClock(double seconds)
        {
            var value = Math.Max(0, (int)Math.Ceiling(seconds));
            return (value / 60) + ":" + (value % 60).ToString("D2");
        }
    }
}
