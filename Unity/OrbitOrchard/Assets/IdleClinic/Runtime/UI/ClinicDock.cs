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
            dock.RemoveFromClassList("upgrade-dock");
            if(!settingsOpen && !selectedRoom.HasValue){dock.style.display=DisplayStyle.None;ApplySafeArea();return;}
            dock.style.display=DisplayStyle.Flex;
            var heading=Box(dock,"dock-heading");
            var title=Text(heading,settingsOpen?"Make yourself at home":RoomName(selectedRoom.Value),"dock-title");
            if(displayFont!=null)title.style.unityFontDefinition=FontDefinition.FromFont(displayFont);
            IconButton(heading,ClinicGlyph.Close,"Close controls",CloseContext,"round-control close-control");
            if(settingsOpen){BuildSettings();ApplySafeArea();return;}
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
            Text(dock,"Room "+room.Tier+"  ·  Upgrade limit "+ClinicRules.TrackCap(room.Tier),"room-detail");
            var upgrades=Box(dock,"upgrades");
            BuildUpgrade(upgrades,room,UpgradeTrack.Equipment,ClinicGlyph.Equipment);
            BuildUpgrade(upgrades,room,UpgradeTrack.Facilities,ClinicGlyph.Facility);
            BuildUpgrade(upgrades,room,UpgradeTrack.Decoration,ClinicGlyph.Plant);
            var actions=Box(dock,"action-row");
            var construction=State.Construction.FirstOrDefault(c=>c.Room==room.Kind);
            if(construction!=null)BuildConstructionReadout(construction,actions);
            else if(room.Tier<ClinicRules.MaximumRoomTier)
                Purchase(actions,ClinicGlyph.Upgrade,"Expand room",ClinicRules.RenovationCost(room),()=>true,
                    ()=>simulation.Renovate(room.Kind),"Room "+(room.Tier+1)+" · "+TimeLabel(ClinicRules.RenovationSeconds(room.Tier)),"minor-action");
            else Text(actions,"Room complete","max-room");
            if(room.Kind==ClinicRoom.Reception && State.ReceptionDesks.Count<2)
                Purchase(actions,ClinicGlyph.Reception,"Hire receptionist",300,()=>true,()=>simulation.HireReceptionist(),"+1 desk","minor-action");
            if(room.Kind==ClinicRoom.FirstAid)
            {
                var nurses=State.Staff.Count(s=>s.Role==ClinicStaffRole.Nurse);
                if(room.StationCount<2)
                {
                    if(room.Tier>=2)Purchase(actions,ClinicGlyph.Bed,"Add treatment station",180,()=>true,()=>simulation.AddTreatmentStation(),"+1 station","minor-action");
                }
                else if(nurses<2)
                    Purchase(actions,ClinicGlyph.Nurse,"Hire second nurse",ClinicRules.HireNurseCost(nurses),()=>true,()=>simulation.HireNurse(),"+1 nurse","minor-action");
            }
            ApplySafeArea();
        }

        private void BuildUpgrade(VisualElement parent,ClinicRoomState room,UpgradeTrack track,ClinicGlyph glyph)
        {
            var level=room.Level(track);var capped=level>=ClinicRules.TrackCap(room.Tier);
            var label=track==UpgradeTrack.Equipment?"Equipment":track==UpgradeTrack.Facilities?"Facilities":"Decor";
            var button=new Button(()=>
            {
                if(capped){FocusRenovation(room);return;}
                Run(()=>simulation.Upgrade(room.Kind,track));
            }){name="upgrade-"+room.Kind.ToString().ToLowerInvariant()+"-"+track.ToString().ToLowerInvariant()};
            button.AddToClassList("upgrade-button");
            var top=Box(button,"upgrade-top");top.Add(new ClinicIcon(glyph,24));Text(top,level.ToString(),"level",true);
            Text(button,capped?label:NextBenefit(room,track),"upgrade-label");
            Text(button,capped?(room.Tier<3?"Room "+(room.Tier+1):"Max"):Money(ClinicRules.UpgradeCost(room,track)),"upgrade-price",true);
            button.tooltip=label+" level "+level+". "+(capped?(room.Tier<3?"Requires room "+(room.Tier+1):"Fully improved"):
                Money(ClinicRules.UpgradeCost(room,track))+" coins. "+UpgradeBenefit(room.Kind,track));
            parent.Add(button);
            readouts.Add(()=>button.SetEnabled(capped||State.Wallet>=ClinicRules.UpgradeCost(room,track)));
            RegisterAccessibleButton(button,button.tooltip,()=>
            {
                if(capped)FocusRenovation(room);
                else Run(()=>simulation.Upgrade(room.Kind,track));
            });
        }

        private void FocusRenovation(ClinicRoomState room)
        {
            dock.Q<Button>("expand-room")?.Focus();
            Notify(room.Tier<3?"Room "+(room.Tier+1)+" unlocks more improvements":"Fully improved");
        }

        private string NextBenefit(ClinicRoomState room,UpgradeTrack track)
        {
            if(track==UpgradeTrack.Decoration)return "+5% fee";
            if(track==UpgradeTrack.Facilities)return room.Kind==ClinicRoom.Waiting?"+2 seats":room.Kind==ClinicRoom.Reception?"+1 place":"+15% fee";
            var basis=room.Kind==ClinicRoom.Reception?14:room.Kind==ClinicRoom.FirstAid?18:2;
            var next=basis/(1+.15*room.Level(track));
            return next.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"s";
        }

        private static string UpgradeBenefit(ClinicRoom room,UpgradeTrack track)
        {
            if(track==UpgradeTrack.Decoration)return "More welcoming furnishings increase the visit fee.";
            if(track==UpgradeTrack.Equipment)return room==ClinicRoom.FirstAid?"Faster first aid":room==ClinicRoom.Reception?"Faster check-in":"Faster calls from the waiting room";
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

        private void ToggleSettings(){settingsOpen=!settingsOpen;selectedRoom=null;dockKey="";UpdateReadouts();}
        private void BuildSettings()
        {
            var row=Box(dock,"settings-row");
            Preference(row,ClinicGlyph.Sound,"Sound",()=>profile.preferences.sound,v=>profile.preferences.sound=v);
            Preference(row,ClinicGlyph.Haptic,"Haptics",()=>profile.preferences.haptics,v=>profile.preferences.haptics=v);
            Preference(row,ClinicGlyph.Motion,"Less motion",()=>profile.preferences.reducedMotion,v=>profile.preferences.reducedMotion=v);
            var help=Box(dock,"help-row");
            Text(help,"Drag to explore · Pinch to zoom\nTap a room to improve it. Tap cash to collect.","help-text");
            if(apple!=null)
            {
                var restore=IconButton(dock,ClinicGlyph.Restore,"Restore existing purchases",RequestRestore,"restore-button");
                Text(restore,"Restore purchases","restore-label");
                readouts.Add(()=>restore.SetEnabled(!restoreRequested&&!apple.IsRestoring));
            }
        }
        private void Preference(VisualElement row,ClinicGlyph glyph,string label,Func<bool> get,Action<bool> set)
        {
            var button=IconButton(row,glyph,label,()=>{set(!get());SaveNow();dockKey="";UpdateReadouts();},"preference");
            Text(button,label,"preference-label");Text(button,get()?"On":"Off","preference-value",true);
            button.EnableInClassList("preference-on",get());
        }
    }
}
