using System;
using UnityEngine;
using IdleClinic.Core;

namespace IdleClinic.Presentation
{
    /// <summary>Twelve repeatable wardrobe silhouettes applied afresh whenever a patient rig is reused.</summary>
    internal sealed class ClinicAppearance
    {
        private static readonly string[] Clothes={"Apricot","Denim","Sage","Mustard","Rose","Blue","Apricot","Denim","Sage","Rose","Mustard","Blue"};
        private static readonly string[] Skins={"Skin","SkinDeep","SkinLight","SkinBrown","SkinOlive","SkinDeep","SkinLight","SkinBrown","SkinLight","SkinDeep","SkinOlive","SkinBrown"};
        private static readonly float[] Heights={1,1.06f,.94f,1.03f,.97f,1.02f,.84f,1.10f,.92f,.96f,1.01f,.95f};
        private static readonly float[] Widths={1,1.05f,.94f,1.14f,.96f,1.07f,.87f,1.04f,1.05f,.98f,1.12f,.93f};
        private readonly Transform root,head,chest,headwear,bodywear;
        private readonly GameObject[] variants=new GameObject[12];
        private readonly Renderer skin;
        private readonly Material[][] materials=new Material[12][];
        private readonly GameObject[] training=new GameObject[11];
        private readonly bool patient;
        private int current=-1;
        internal ClinicAppearance(ClinicArt art,Transform root,bool patient,ClinicStaffRole role=ClinicStaffRole.Nurse)
        {
            this.root=root;this.patient=patient;skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
            var rig=skin as SkinnedMeshRenderer;if(rig!=null){head=Array.Find(rig.bones,b=>b.name=="head");chest=Array.Find(rig.bones,b=>b.name=="chest");}
            headwear=art.Group("Character head accessories",root);bodywear=art.Group("Character body accessories",root);
            var originals=skin==null?Array.Empty<Material>():skin.sharedMaterials;
            for(int appearance=0;appearance<12;appearance++)
            {
                materials[appearance]=new Material[originals.Length];
                for(int slot=0;slot<originals.Length;slot++)
                {
                    string name=originals[slot]==null?"":originals[slot].name;
                    materials[appearance][slot]=name=="Clinic Skin"?art.Material(Skins[appearance]):patient&&name=="Clinic Apricot"?art.Material(Clothes[appearance]):!patient&&(name=="Clinic Sage"||name=="Clinic Apricot"||name=="Clinic Blue")&&role==ClinicStaffRole.Doctor?art.Material("Linen"):!patient&&(name=="Clinic Sage"||name=="Clinic Apricot"||name=="Clinic Blue")&&role==ClinicStaffRole.Pharmacist?art.Material("SageDark"):originals[slot];
                }
                var variant=art.Group("Appearance silhouette "+appearance,headwear);variants[appearance]=variant.gameObject;
                if(appearance==1||appearance==3||appearance==7)
                {
                    for(int curl=0;curl<4;curl++)art.Orb("Textured hair curl",variant,new Vector3((curl%2-.5f)*.17f,.14f+(curl/2)*.045f,-.04f),new Vector3(.20f,.15f,.19f),appearance==3?"HairChestnut":"Ink");
                }
                if(appearance==2||appearance==4||appearance==10)
                {
                    art.Orb("Swept hair",variant,new Vector3(0,.12f,-.06f),new Vector3(.35f,.18f,.33f),appearance==4?"HairGold":"HairChestnut");
                    art.Orb(appearance==4?"Long ponytail":"Hair bun",variant,new Vector3(.02f,appearance==4?-.06f:.13f,-.20f),new Vector3(.17f,appearance==4?.39f:.21f,.19f),appearance==4?"HairGold":"HairChestnut");
                }
                if(appearance==5||appearance==11)
                {
                    art.Orb("Head scarf",variant,new Vector3(0,.08f,-.045f),new Vector3(.37f,.33f,.33f),appearance==5?"Rose":"Mustard");
                    art.Box("Scarf tail",variant,new Vector3(.10f,-.19f,-.12f),new Vector3(.17f,.32f,.13f),appearance==5?"Rose":"Mustard");
                }
                if(appearance==6)
                {
                    art.Orb("Visitor cap",variant,new Vector3(0,.14f,0),new Vector3(.37f,.17f,.35f),"Blue");
                    art.Box("Visitor cap brim",variant,new Vector3(0,.105f,.19f),new Vector3(.31f,.045f,.19f),"Blue");
                }
                if(appearance==8||appearance==9)
                {
                    art.Orb("Silver hair",variant,new Vector3(0,.135f,-.015f),new Vector3(.34f,.18f,.31f),"HairSilver");
                    if(appearance==9)art.Orb("Silver hair bun",variant,new Vector3(0,.12f,-.19f),new Vector3(.19f,.20f,.18f),"HairSilver");
                    else art.Orb("Silver beard",variant,new Vector3(0,-.11f,.08f),new Vector3(.21f,.17f,.17f),"HairSilver");
                }
                if(appearance==1||appearance==8||appearance==9||appearance==10)
                {
                    for(int side=-1;side<=1;side+=2)
                    {
                        art.Box("Spectacle top",variant,new Vector3(side*.072f,.041f,.167f),new Vector3(.115f,.015f,.019f),"Wood");
                        art.Box("Spectacle bottom",variant,new Vector3(side*.072f,-.019f,.167f),new Vector3(.115f,.012f,.019f),"Wood");
                        for(int edge=-1;edge<=1;edge+=2)art.Box("Spectacle edge",variant,new Vector3(side*.072f+edge*.054f,.009f,.167f),new Vector3(.011f,.072f,.019f),"Wood");
                    }
                    art.Box("Spectacle bridge",variant,new Vector3(0,.018f,.172f),new Vector3(.04f,.014f,.02f),"Gold");
                }
                variant.gameObject.SetActive(false);
            }
            if(patient)
            {
                art.Box("Visitor backpack",bodywear,new Vector3(0,.93f,-.21f),new Vector3(.32f,.40f,.19f),"Wood");
                art.Box("Backpack pocket",bodywear,new Vector3(0,.86f,-.322f),new Vector3(.24f,.15f,.045f),"Gold");
            }
            else
            {
                for(int level=2;level<=12;level++)training[level-2]=art.Box("Staff training pin "+level,bodywear,new Vector3(-.14f+((level-2)%6)*.047f,1.16f-((level-2)/6)*.055f,.15f),new Vector3(.034f,.043f,.023f),"Gold");
                if(role==ClinicStaffRole.Doctor)
                {art.Box("Doctor coat lapel",bodywear,new Vector3(.09f,1.07f,.175f),new Vector3(.13f,.39f,.045f),"Linen");art.Orb("Doctor stethoscope diaphragm",bodywear,new Vector3(.15f,.90f,.19f),new Vector3(.09f,.09f,.035f),"Gold");art.Box("Doctor stethoscope tube",bodywear,new Vector3(.14f,1.10f,.18f),new Vector3(.022f,.33f,.025f),"Ink");}
                if(role==ClinicStaffRole.Pharmacist)
                {art.Box("Pharmacist name badge",bodywear,new Vector3(.13f,1.10f,.18f),new Vector3(.14f,.12f,.023f),"Linen");art.Box("Pharmacist badge cross",bodywear,new Vector3(.13f,1.10f,.195f),new Vector3(.085f,.025f,.012f),"Sage");art.Box("Pharmacist badge cross",bodywear,new Vector3(.13f,1.10f,.20f),new Vector3(.025f,.085f,.012f),"Sage");}
            }
        }
        internal void Apply(int appearance,int trainingLevel=1)
        {
            appearance=Math.Max(0,appearance)%12;
            if(current!=appearance)
            {
                current=appearance;if(skin!=null)skin.sharedMaterials=materials[appearance];
                for(int i=0;i<variants.Length;i++)variants[i].SetActive(i==appearance);
                root.localScale=patient?new Vector3(Widths[appearance],Heights[appearance],Widths[appearance]):Vector3.one;
                if(patient)bodywear.gameObject.SetActive(appearance==6||appearance==7);
            }
            if(!patient)for(int i=0;i<training.Length;i++)training[i].SetActive(i+2<=trainingLevel);
        }
        internal void AfterPose(bool seated)
        {
            // Seated pelvis sits 0.55m above the actor origin. Offset just that scale
            // difference so a shorter or taller visitor still rests on the same chair.
            if(seated&&patient&&current>=0)root.position+=Vector3.up*(.55f*(1-Heights[current]));
            if(head!=null){headwear.position=head.position;headwear.rotation=root.rotation;}
            if(chest!=null)bodywear.localPosition=Vector3.up*(root.InverseTransformPoint(chest.position).y-1.10f);
        }
    }
}
