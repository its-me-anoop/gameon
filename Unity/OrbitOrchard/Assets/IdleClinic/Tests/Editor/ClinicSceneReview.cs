using System;
using System.IO;
using IdleClinic.Core;
using IdleClinic.Presentation;
using UnityEngine;

namespace IdleClinic.Tests
{
    /// <summary>Editor-only rendered scene inspection. Scripted fixtures are not native gameplay evidence.</summary>
    public static class ClinicSceneReview
    {
        public static void Capture()
        {
            string output=Environment.GetEnvironmentVariable("CLINIC_REVIEW_OUTPUT");
            if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Set a new CLINIC_REVIEW_OUTPUT directory.");
            if(Directory.Exists(output))throw new InvalidOperationException("Review output already exists.");
            Directory.CreateDirectory(output);
            var host=new GameObject("Scripted clinic scene inspection");
            try
            {
                var world=host.AddComponent<ClinicWorld>();world.Initialize();
                var state=ClinicSimulation.CreateNew().State;state.Patients.Clear();
                state.Room(ClinicRoom.Waiting).Built=true;state.Room(ClinicRoom.Waiting).FacilitiesLevel=6;
                state.Room(ClinicRoom.FirstAid).StationCount=2;state.ReceptionDesks.Add(new ReceptionDeskState{Id=1});
                state.Amenity(ClinicAmenity.Toilet).Level=3;state.Amenity(ClinicAmenity.Vending).Level=3;
                for(int i=0;i<3;i++)state.Room((ClinicRoom)i).Tier=3;
                foreach(int level in new[]{0,1,3})
                {
                    state.Amenity(ClinicAmenity.Parking).Level=level;state.Patients.Clear();
                    for(int i=0;i<level*2;i++)state.Patients.Add(new ClinicPatientState{Id=100+i,AppearanceId=i,ParkingBayId=i,Phase=ClinicPatientPhase.Seated,
                        FromAnchor="waiting.seat."+i,ToAnchor="waiting.seat."+i});
                    world.Render(state,.1f,true);
                    Save(world,new Vector3(-10.4f,.14f,-.15f),8.2f,750,1334,Path.Combine(output,"parking-level-"+level+".png"));
                }
                Save(world,new Vector3(0,.45f,.45f),7.8f,1200,1000,Path.Combine(output,"joined-clinic.png"));
                File.WriteAllText(Path.Combine(output,"SCOPE.txt"),"Scripted Unity Editor scene renders. Includes staged parking levels and joined walls. Not simulator input, a saved player session, or a performance measurement.\n");
            }
            finally{UnityEngine.Object.DestroyImmediate(host);}
        }
        private static void Save(ClinicWorld world,Vector3 focus,float size,int width,int height,string path)
        {
            world.SetRenderSize(width,height);var camera=world.SceneCamera;camera.orthographicSize=size;
            camera.transform.position=focus-camera.transform.forward*22;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=world.Texture;
            var pixels=new Texture2D(world.Texture.width,world.Texture.height,TextureFormat.RGB24,false);
            try{pixels.ReadPixels(new Rect(0,0,pixels.width,pixels.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);}
        }
    }
}
