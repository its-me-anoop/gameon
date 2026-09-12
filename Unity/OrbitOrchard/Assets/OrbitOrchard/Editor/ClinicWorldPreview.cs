using System;
using System.IO;
using System.Reflection;
using IdleClinic.Core;
using IdleClinic.Presentation;
using UnityEditor;
using UnityEngine;

namespace OrbitOrchard.Editor
{
    /// <summary>Bounded editor-only geometry captures; this fixture is never compiled into a player.</summary>
    public static class ClinicWorldPreview
    {
        public static void Capture()
        {
            string output=Environment.GetEnvironmentVariable("CLINIC_QA_OUTPUT");if(string.IsNullOrWhiteSpace(output))throw new InvalidOperationException("CLINIC_QA_OUTPUT is required.");Directory.CreateDirectory(output);
            var root=new GameObject("Doctors clinic preview fixture");var world=root.AddComponent<ClinicWorld>();
            try
            {
                world.Initialize(ClinicLocation.DoctorsClinic);world.SetRenderSize(900,1600);
                var initial=ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic,743).State;world.Render(initial,.1f);CaptureFrame(world,output,"doctors-opening-home");
                var max=ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic,743).State;
                foreach(var room in max.Rooms){room.Tier=6;room.EquipmentLevel=room.FacilitiesLevel=room.DecorationLevel=12;room.StationCount=room.Kind==ClinicRoom.Waiting?0:room.Kind==ClinicRoom.Pharmacy?2:4;}
                foreach(var a in max.Amenities){a.Level=6;a.Till=a.Kind==ClinicAmenity.Vending?150:0;}
                max.ReceptionDesks.Clear();max.TreatmentStations.Clear();max.ConsultationStations.Clear();max.PharmacyStations.Clear();max.Staff.Clear();max.Patients.Clear();
                for(int role=0;role<4;role++)for(int i=0;i<(role==3?2:4);i++)
                {
                    if(role==0)max.ReceptionDesks.Add(new ReceptionDeskState{Id=i,EquipmentLevel=12,Till=360+i*80});
                    else ClinicRules.Stations(max,(ClinicStaffRole)role).Add(new TreatmentStationState{Id=i,EquipmentLevel=12});
                    string anchor=ClinicRules.StationStaffAnchor((ClinicStaffRole)role,i);max.Staff.Add(new ClinicStaffState{Id=ClinicRules.StaffId((ClinicStaffRole)role,i),Role=(ClinicStaffRole)role,StationId=i,TrainingLevel=12,FromAnchor=anchor,ToAnchor=anchor,PatientId=100+role*10+i});
                    string patient=ClinicRules.StationPatientAnchor((ClinicStaffRole)role,i);max.Patients.Add(new ClinicPatientState{Id=100+role*10+i,AppearanceId=(role*4+i)%12,Phase=role==0?ClinicPatientPhase.CheckingIn:role==1?ClinicPatientPhase.Treating:role==2?ClinicPatientPhase.Consulting:ClinicPatientPhase.Dispensing,FromAnchor=patient,ToAnchor=patient,PhaseStartedTick=0,PhaseEndsTick=600});
                }
                for(int i=0;i<18;i++)max.Patients.Add(new ClinicPatientState{Id=300+i,AppearanceId=i%12,Phase=ClinicPatientPhase.Seated,FromAnchor="waiting.seat."+i,ToAnchor="waiting.seat."+i,SeatId=i,ParkingBayId=i<12?i:-1});
                max.TaxiRides.Add(new ClinicTaxiState{Id=90,PatientId=45,DockId=0,Phase=ClinicTaxiPhase.Boarding,PhaseStartedTick=0,PhaseEndsTick=60});
                max.TaxiRides.Add(new ClinicTaxiState{Id=92,PatientId=46,DockId=1,Phase=ClinicTaxiPhase.Approaching,PhaseStartedTick=0,PhaseEndsTick=160});
                max.Construction.Add(new ClinicConstructionState{Id=1,Room=ClinicRoom.Consultation,StartedTick=0,EndsTick=600,TargetTier=6});max.Tick=40;world.Render(max,.1f);world.Home(true);CaptureFrame(world,output,"doctors-complete-home");
                View(world,new Vector3(-5.3f,0,-6.0f),6.8f);CaptureFrame(world,output,"doctors-reception");
                View(world,new Vector3(-5.2f,0,1.4f),6.8f);CaptureFrame(world,output,"doctors-nursing");
                View(world,new Vector3(-1f,0,6.4f),12.5f);CaptureFrame(world,output,"doctors-consultations");
                View(world,new Vector3(6.9f,0,-3.0f),7.1f);CaptureFrame(world,output,"doctors-waiting-pharmacy-toilets");
                View(world,new Vector3(-16.8f,0,-.8f),10.2f);CaptureFrame(world,output,"doctors-parking");
                View(world,new Vector3(19.8f,0,-9.1f),5.5f);CaptureFrame(world,output,"doctors-taxi");
                world.SetRenderSize(1600,1100);View(world,new Vector3(-4,0,-1.8f),17f);CaptureFrame(world,output,"doctors-neighbourhood-overview");
                Debug.Log("Doctors preview captures written to "+output);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        private static void View(ClinicWorld world,Vector3 center,float size)
        {
            var t=typeof(ClinicWorld);t.GetField("center",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(world,center);t.GetField("size",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(world,size);t.GetMethod("ApplyCamera",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(world,null);
        }
        private static void CaptureFrame(ClinicWorld world,string output,string name)
        {
            world.SceneCamera.Render();var previous=RenderTexture.active;RenderTexture.active=world.Texture;var image=new Texture2D(world.Texture.width,world.Texture.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,world.Texture.width,world.Texture.height),0,0);image.Apply();RenderTexture.active=previous;File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
