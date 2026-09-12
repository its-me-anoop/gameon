using System;
using System.Linq;
using IdleClinic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleClinic.App
{
    public sealed partial class ClinicApp
    {
        private void RebuildDock()
        {
            dock.Clear();readouts.Clear();
            dock.RemoveFromClassList("upgrade-dock");dock.RemoveFromClassList("management-dock");dock.RemoveFromClassList("locations-dock");
            if(!locationsOpen&&!settingsOpen && !selectedRoom.HasValue&&!selectedObject.HasValue){dock.style.display=DisplayStyle.None;ApplySafeArea();return;}
            dock.style.display=DisplayStyle.Flex;
            var heading=Box(dock,"dock-heading");
            var title=Text(heading,locationsOpen?"Your clinics":settingsOpen?"Make yourself at home":selectedObject.HasValue?ObjectName(selectedObject.Value):RoomName(selectedRoom.Value),"dock-title");
            if(displayFont!=null)title.style.unityFontDefinition=FontDefinition.FromFont(displayFont);
            if(!locationsOpen&&!settingsOpen&&!selectedObject.HasValue&&State.Tutorial==ClinicTutorialStep.Complete)BuildRoomShortcuts(heading,selectedRoom.Value);
            IconButton(heading,ClinicGlyph.Close,"Close controls",CloseContext,"round-control close-control");
            if(locationsOpen){BuildLocationsDock();ApplySafeArea();return;}
            if(settingsOpen){BuildSettings();ApplySafeArea();return;}
            if(selectedObject.HasValue){BuildManagementDock(selectedObject.Value);ApplySafeArea();return;}
            var room=State.Room(selectedRoom.Value);
            if(!room.Built)
            {
                var job=State.Construction.FirstOrDefault(c=>c.Room==ClinicRoom.Waiting);
                if(job!=null){BuildConstructionReadout(job);ApplySafeArea();return;}
                Text(dock,State.WaitingRoomUnlocked?"Four seats. A softer wait.":"Opens when two paid patients are waiting","room-detail");
                var line=Box(dock,"action-row");
                Purchase(line,ClinicGlyph.Chair,"Build waiting room",ClinicRules.WaitingRoomCost,
                    ()=>State.WaitingRoomUnlocked && State.Tutorial==ClinicTutorialStep.Complete,
                    ()=>simulation.BuildWaitingRoom(),"4 seats · 20s","primary-action");
                ApplySafeArea();return;
            }
            if(State.Tutorial!=ClinicTutorialStep.Complete)
            {
                if(State.Tutorial==ClinicTutorialStep.HireFirstNurse && room.Kind==ClinicRoom.FirstAid)
                {
                    Text(dock,"Your first patient is ready for care","room-detail");
                    var row=Box(dock,"action-row");
                    Purchase(row,ClinicGlyph.Nurse,"Hire first nurse",50,()=>true,()=>simulation.HireNurse(),"First aid","primary-action");
                }
                else Text(dock,State.Tutorial==ClinicTutorialStep.CollectFirstPayment?"Collect the coins on the counter":
                    State.Tutorial==ClinicTutorialStep.FirstTreatment?"Watch your nurse help the first patient":"Your first patient is on the way","room-detail");
                ApplySafeArea();return;
            }
            dock.AddToClassList("upgrade-dock");
            Text(dock,"Room "+room.Tier+"  ·  Upgrade limit "+ClinicRules.ComponentCap(State,room.Kind),"room-detail");
            var upgrades=Box(dock,"upgrades");
            BuildUpgrade(upgrades,room,UpgradeTrack.Equipment,ClinicGlyph.Equipment);
            BuildUpgrade(upgrades,room,UpgradeTrack.Facilities,ClinicGlyph.Facility);
            BuildUpgrade(upgrades,room,UpgradeTrack.Decoration,ClinicGlyph.Plant);
            var actions=Box(dock,"action-row");
            var construction=State.Construction.FirstOrDefault(c=>c.Room==room.Kind);
            if(construction!=null)BuildConstructionReadout(construction,actions);
            else if(room.Tier<ClinicRules.MaximumTier(State))
                Purchase(actions,ClinicGlyph.Upgrade,"Expand room",ClinicRules.RenovationCost(State,room.Kind),()=>true,
                    ()=>simulation.Renovate(room.Kind),"Room "+(room.Tier+1)+" · "+TimeLabel(ClinicRules.RenovationSeconds(State,room.Kind)),"minor-action");
            else Text(actions,"Room complete","max-room");
            BuildStaffAction(actions,room);
            ApplySafeArea();
        }

        private void BuildStaffAction(VisualElement actions,ClinicRoomState room)
        {
            var role=RoomRole(room.Kind);if(!role.HasValue)return;
            var staff=State.Staff.Count(s=>s.Role==role.Value);
            var stations=ClinicRules.StationCount(State,role.Value);
            if(staff>=ClinicRules.MaximumStaff(State,role.Value))return;
            if(role.Value==ClinicStaffRole.Receptionist||staff<stations)
            {
                Purchase(actions,RoleGlyph(role.Value),"Hire "+RoleName(role.Value),ClinicRules.HireCost(State,role.Value),()=>true,
                    ()=>simulation.HireStaff(role.Value),"+1 "+RoleName(role.Value),"minor-action");
                return;
            }
            if(stations<ClinicRules.StationCap(State,role.Value))
                Purchase(actions,RoleGlyph(role.Value),"Add "+(role.Value==ClinicStaffRole.Doctor?"consultation room":role.Value==ClinicStaffRole.Pharmacist?"pharmacy counter":"treatment station"),
                    ClinicRules.AddStationCost(State,role.Value),()=>true,()=>simulation.AddStation(role.Value),"+1 station","minor-action");
            else if(room.Tier<ClinicRules.MaximumTier(State))
            {
                var next=IconButton(actions,RoleGlyph(role.Value),"Renovate to add another "+RoleName(role.Value),()=>FocusRenovation(room),"room-shortcut");
                Text(next,"Room "+(room.Tier+1)+" · +1 station","purchase-detail");
            }
        }

        private void BuildUpgrade(VisualElement parent,ClinicRoomState room,UpgradeTrack track,ClinicGlyph glyph)
        {
            var level=room.Level(track);var capped=level>=ClinicRules.ComponentCap(State,room.Kind);
            var label=track==UpgradeTrack.Equipment?"Equipment":track==UpgradeTrack.Facilities?"Facilities":"Decor";
            var button=new Button(()=>
            {
                if(capped){FocusRenovation(room);return;}
                Run(()=>simulation.Upgrade(room.Kind,track));
            }){name="upgrade-"+room.Kind.ToString().ToLowerInvariant()+"-"+track.ToString().ToLowerInvariant()};
            button.AddToClassList("upgrade-button");
            var top=Box(button,"upgrade-top");top.Add(new ClinicIcon(glyph,24));Text(top,level.ToString(),"level",true);
            Text(button,capped?label:NextBenefit(room,track),"upgrade-label");
            Text(button,capped?(room.Tier<ClinicRules.MaximumTier(State)?"Room "+(room.Tier+1):"Max"):Money(ClinicRules.UpgradeCost(State,room.Kind,track)),"upgrade-price",true);
            button.tooltip=label+" level "+level+". "+(capped?(room.Tier<ClinicRules.MaximumTier(State)?"Requires room "+(room.Tier+1):"Fully improved"):
                Money(ClinicRules.UpgradeCost(State,room.Kind,track))+" coins. "+UpgradeBenefit(room.Kind,track));
            parent.Add(button);
            readouts.Add(()=>button.SetEnabled(capped||State.Wallet>=ClinicRules.UpgradeCost(State,room.Kind,track)));
            RegisterAccessibleButton(button,button.tooltip,()=>
            {
                if(capped)FocusRenovation(room);
                else Run(()=>simulation.Upgrade(room.Kind,track));
            });
        }

        private void FocusRenovation(ClinicRoomState room)
        {
            dock.Q<Button>("expand-room")?.Focus();
            Notify(room.Tier<ClinicRules.MaximumTier(State)?"Room "+(room.Tier+1)+" unlocks more improvements":"Fully improved");
        }

        private string NextBenefit(ClinicRoomState room,UpgradeTrack track)
        {
            if(track==UpgradeTrack.Decoration)return "+5% fee";
            if(track==UpgradeTrack.Facilities)return room.Kind==ClinicRoom.Waiting?"+2 seats":room.Kind==ClinicRoom.Reception?"+1 place":room.Kind==ClinicRoom.Consultation||room.Kind==ClinicRoom.Pharmacy?"+10% fee":"+15% fee";
            if(room.Kind==ClinicRoom.Waiting)return ServiceTime(ClinicRules.WaitingCallTicks(State,equipmentLevelsAdded:1));
            var role=RoomRole(room.Kind)??ClinicStaffRole.Nurse;
            var ids=StationIds(State,role);
            var times=ids.Select(id=>ClinicRules.StationServiceTicks(State,role,id,roomEquipmentLevelsAdded:1)).OrderBy(t=>t).ToArray();
            if(times.Length==0)return "Faster service";
            return times.First()==times.Last()?ServiceTime(times.First()):ServiceTime(times.First()).TrimEnd('s')+"–"+ServiceTime(times.Last());
        }

        private static string UpgradeBenefit(ClinicRoom room,UpgradeTrack track)
        {
            if(track==UpgradeTrack.Decoration)return "More welcoming furnishings increase the visit fee.";
            if(track==UpgradeTrack.Equipment)return room==ClinicRoom.FirstAid?"Faster first aid":room==ClinicRoom.Reception?"Faster check-in":room==ClinicRoom.Consultation?"Faster consultations":room==ClinicRoom.Pharmacy?"Faster dispensing":"Faster calls from the waiting room";
            return room==ClinicRoom.Waiting?"Two more seats":room==ClinicRoom.Reception?"One more queue place":"Improved care increases the visit fee";
        }

        private Button Purchase(VisualElement parent,ClinicGlyph glyph,string label,long price,Func<bool> unlocked,
            Func<ClinicCommandResult> action,string detail,string classes)
        {
            var button=new Button(()=>Run(action)){tooltip=label+" for "+Money(price)+" coins",name=label.ToLowerInvariant().Replace(' ','-')};
            button.AddToClassList("purchase-button");button.AddToClassList(classes);
            button.Add(new ClinicIcon(glyph,26));
            var words=Box(button,"purchase-words");
            Text(words,detail,"purchase-detail");
            var cost=Box(words,"cost-row");cost.Add(new ClinicIcon(ClinicGlyph.Coin,15));Text(cost,Money(price),"purchase-price",true);
            parent.Add(button);
            readouts.Add(()=>button.SetEnabled(unlocked()&&State.Wallet>=price));
            RegisterAccessibleButton(button,label+", "+Money(price)+" coins. "+detail,()=>Run(action));
            return button;
        }

        private void BuildConstructionReadout(ClinicConstructionState job,VisualElement parent=null)
        {
            var row=Box(parent??dock,"construction-readout");
            var ring=new ClinicProgress(30);row.Add(ring);var label=Text(row,"","construction-time",true);
            readouts.Add(()=>
            {
                ring.Progress=(State.Tick-job.StartedTick)/(float)Math.Max(1,job.EndsTick-job.StartedTick);
                label.text=TimeLabel((job.EndsTick-State.Tick)/(double)ClinicRules.TicksPerSecond)+"  ·  Improving";
            });
        }

        private void ToggleSettings(){locationsOpen=false;settingsOpen=!settingsOpen;selectedRoom=null;selectedObject=null;dockKey="";UpdateReadouts();}
        private void BuildSettings()
        {
            var content=new ScrollView(ScrollViewMode.Vertical){name="clinic-settings-content",horizontalScrollerVisibility=ScrollerVisibility.Hidden};
            content.AddToClassList("bounded-dock-content");dock.Add(content);
            BindTouchCaptureLifecycle(content.contentContainer);BindTouchCaptureLifecycle(content.contentViewport);
            var row=Box(content,"settings-row");
            Preference(row,ClinicGlyph.Music,"Music",()=>profile.preferences.music,v=>{profile.preferences.music=v;RefreshAudioPreferences();});
            Preference(row,ClinicGlyph.Sound,"Effects",()=>profile.preferences.sound,v=>{profile.preferences.sound=v;RefreshAudioPreferences();});
            Preference(row,ClinicGlyph.Haptic,"Haptics",()=>profile.preferences.haptics,v=>profile.preferences.haptics=v);
            Preference(row,ClinicGlyph.Motion,"Less motion",()=>profile.preferences.reducedMotion,v=>profile.preferences.reducedMotion=v);
            var help=Box(content,"help-row");
            Text(help,"Drag to explore · Pinch to zoom\nTap a room to improve it. Tap cash to collect.","help-text");
            var links=Box(content,"settings-links");
            SettingsLink(links,ClinicGlyph.Help,"Privacy policy","https://github.com/its-me-anoop/gravitile-support/blob/main/privacy.md");
            SettingsLink(links,ClinicGlyph.Help,"Contact support","https://github.com/its-me-anoop/gravitile-support");
            if(apple!=null)
            {
                var restore=IconButton(content,ClinicGlyph.Restore,"Restore existing purchases",RequestRestore,"restore-button");
                Text(restore,"Restore purchases","restore-label");
                readouts.Add(()=>restore.SetEnabled(!restoreRequested&&!apple.IsRestoring));
            }
        }
        private void SettingsLink(VisualElement parent,ClinicGlyph glyph,string label,string destination)
        {
            var button=IconButton(parent,glyph,label+", opens in browser",()=>{SaveNow();Application.OpenURL(destination);},"settings-link");
            button.name=label.ToLowerInvariant().Replace(' ','-');
            Text(button,label,"settings-link-label");
        }
        private void Preference(VisualElement row,ClinicGlyph glyph,string label,Func<bool> get,Action<bool> set)
        {
            var button=IconButton(row,glyph,label,()=>{set(!get());SaveNow();dockKey="";UpdateReadouts();},"preference");
            Text(button,label,"preference-label");Text(button,get()?"On":"Off","preference-value",true);
            button.EnableInClassList("preference-on",get());
        }
    }
}
