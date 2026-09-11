using System;
using System.Collections.Generic;
using System.Reflection;
using LittleLifeline.App;
using LittleLifeline.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LittleLifeline.Tests
{
    public sealed class LifelineManagementHUDTests
    {
        private GameObject host;
        private LifelineApp app;
        private VisualElement body;
        private LifelineSimulation campaign;

        [SetUp]
        public void SetUp()
        {
            // Keep lifecycle inactive: these tests build UI without opening a save,
            // initializing Apple services, or advancing the live hospital.
            host = new GameObject("Management HUD test");
            host.SetActive(false);
            app = host.AddComponent<LifelineApp>();
            body = new VisualElement();
            campaign = LifelineSimulation.CreateCampaign();
            Set("campaign", campaign);
            Set("pageBody", body);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void CrewPlanShowsAllFourSlotsAndOnlyExistingRoomsCanBeAssigned()
        {
            Invoke("BuildCrewHUD");
            Assert.That(body.Q<Button>("crew-person-0").ClassListContains("selected"), Is.True);
            for (var slot = 0; slot < LifelineRules.CarriageSlots; slot++)
            {
                var room = body.Q<Button>("crew-carriage-" + slot);
                Assert.That(room, Is.Not.Null);
                Assert.That(room.enabledSelf, Is.EqualTo(slot == 0));
            }
            Assert.That(body.Q<Button>("crew-carriage-0").ClassListContains("selected"), Is.True);
            Assert.That(body.Q<Button>("crew-rest").enabledSelf, Is.True);
            var controlsWidth = 0f;
            foreach (var element in body.Q<Button>("crew-rest").parent.Children())
                if (element is Button button)
                    controlsWidth += button.style.minWidth.value.value + button.style.marginLeft.value.value + button.style.marginRight.value.value;
            Assert.That(controlsWidth, Is.LessThanOrEqualTo(272f), "Six 44px controls must fit a 320px screen after dock margins and padding.");
        }

        [Test]
        public void UnassignedCrewCannotSelectAHelperBeforeAMainRoom()
        {
            Set("crewHUDPersonId", 1);
            Set("crewHUDHelping", true);
            Invoke("BuildCrewHUD");
            Assert.That(body.Q<Button>("crew-assignment-mode").enabledSelf, Is.False);
            Assert.That(body.Q<Button>("crew-rest").ClassListContains("selected"), Is.True);
            Assert.That(campaign.State.Crew[1].PrimaryRoomId, Is.EqualTo(-1));
        }

        [Test]
        public void WeeklyCrewUsesItsOwnTrainingBudgetAndNeverOffersRecruitment()
        {
            var weekly = LifelineSimulation.CreateWeekly(new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero));
            weekly.State.Coins = 0;
            campaign.State.Coins = 10000;
            Set("weekly", weekly);
            Set("crewForWeekly", true);
            var panelType = typeof(LifelineApp).GetNestedType("CrewHUDPanel", BindingFlags.NonPublic);
            Set("crewHUDPanel", Enum.Parse(panelType, "Training"));
            Invoke("BuildCrewHUD");
            RefreshReadouts();
            Assert.That(body.Q<Button>("crew-train").enabledSelf, Is.False);
            Assert.That(body.Q<Button>("crew-panel-Hiring"), Is.Null);
            Assert.That(body.Q<Button>("crew-back-weekly"), Is.Not.Null);
            Assert.That(campaign.State.Coins, Is.EqualTo(10000));
            Assert.That(weekly.State.Coins, Is.Zero);
        }

        [Test]
        public void InspectingALockedTownKeepsTheHospitalAndProjectsAtTheCurrentStop()
        {
            Set("routeHUDTown", TownId.Copperhill);
            Invoke("BuildRouteHUD");
            RefreshReadouts();
            Assert.That(body.Q<Button>("route-town-Copperhill").ClassListContains("selected"), Is.True);
            Assert.That(body.Q<Button>("route-travel").enabledSelf, Is.False);
            Assert.That(body.Q<Button>("route-project-0").enabledSelf, Is.False);
            Assert.That(body.Q<Button>("route-project-1").enabledSelf, Is.False);
            Assert.That(campaign.State.Town, Is.EqualTo(TownId.Willowbank));
            Assert.That(campaign.State.Tick, Is.Zero);
        }

        [Test]
        public void CompactButtonsKeepCaptionsReadableEvenWhenAThemeRequestsSmallerType()
        {
            var button = new Button();
            var caption = new Label("Mara");
            caption.style.fontSize = 12;
            button.Add(caption);
            typeof(LifelineApp).GetMethod("CompactManagementButton", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { button, true });
            Assert.That(caption.style.fontSize.value.value, Is.GreaterThanOrEqualTo(14f));
            Assert.That(button.style.minHeight.value.value, Is.GreaterThanOrEqualTo(44f));
            Assert.That(button.style.minWidth.value.value, Is.GreaterThanOrEqualTo(44f));
        }

        private void RefreshReadouts()
        {
            var field = typeof(LifelineApp).GetField("readouts", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var update in (List<Action>)field.GetValue(app)) update();
        }
        private void Set(string name, object value)
            => typeof(LifelineApp).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(app, value);
        private void Invoke(string name)
            => typeof(LifelineApp).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, null);
    }
}
