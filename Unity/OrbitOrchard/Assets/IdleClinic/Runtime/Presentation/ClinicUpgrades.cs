using IdleClinic.Core;
using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Small, cumulative fittings make each purchased track level visible without moving work sockets.</summary>
    internal sealed class ClinicUpgrades
    {
        private readonly GameObject[,,] details=new GameObject[3,3,5];
        internal ClinicUpgrades(ClinicArt art,Transform parent)
        {
            for(int room=0;room<3;room++)for(int track=0;track<3;track++)for(int level=2;level<=6;level++)
            {
                var root=art.Group(((ClinicRoom)room)+" "+((UpgradeTrack)track)+" level "+level,parent);
                details[room,track,level-2]=root.gameObject;int n=level-2;
                if(room==0)Reception(art,root,track,n);
                else if(room==1)Treatment(art,root,track,n);
                else Waiting(art,root,track,n);
                root.gameObject.SetActive(false);
            }
        }
        internal void Render(ClinicState state)
        {
            for(int room=0;room<3;room++)
            {
                ClinicRoomState data=null;for(int i=0;i<state.Rooms.Count;i++)if((int)state.Rooms[i].Kind==room){data=state.Rooms[i];break;}
                for(int track=0;track<3;track++)for(int level=2;level<=6;level++)
                {
                    bool active=data!=null&&data.Built&&data.Level((UpgradeTrack)track)>=level;
                    var detail=details[room,track,level-2];if(detail.activeSelf!=active)detail.SetActive(active);
                }
            }
        }
        private static void Reception(ClinicArt art,Transform root,int track,int n)
        {
            if(track==0)
            {
                // A larger register suite is added behind the active counter, clear of both actor sockets.
                float x=-4.45f+n*.27f;
                art.Box("Register accessory base",root,new Vector3(x,1.23f,-2.27f),new Vector3(.22f,.15f,.20f),n%2==0?"Ivory":"SageDark");
                art.Box("Receipt and appointment slips",root,new Vector3(x,1.32f,-2.26f),new Vector3(.13f,.015f,.16f),"Linen");
            }
            else if(track==1)
            {
                float x=-4.70f+n*.82f;
                art.Cylinder("Queue guide post",root,new Vector3(x,.47f,-6.60f),new Vector3(.055f,.65f,.055f),"Gold");
                art.Cylinder("Queue guide foot",root,new Vector3(x,.16f,-6.60f),new Vector3(.19f,.045f,.19f),"SageDark");
                art.Box("Queue floor arrow",root,new Vector3(x,.146f,-5.18f),new Vector3(.28f,.008f,.06f),"Gold");
            }
            else WallPicture(art,root,new Vector3(-5.53f,1.17f,-3.60f+n*.72f),true,n);
        }
        private static void Treatment(ClinicArt art,Transform root,int track,int n)
        {
            if(track==0)
            {
                float x=-4.55f+n*.39f;
                art.Box("Treatment instrument shelf",root,new Vector3(x,1.04f,4.50f),new Vector3(.34f,.075f,.34f),"Gold");
                art.Cylinder("Treatment supply bottle",root,new Vector3(x,1.23f,4.50f),new Vector3(.13f,.32f,.13f),n%2==0?"Blue":"Linen");
                art.Box("Supply label",root,new Vector3(x,1.23f,4.428f),new Vector3(.08f,.09f,.018f),"Apricot");
            }
            else
            {
                if(track==1)
                {
                    float x=-4.56f+n*.57f;
                    art.Box("Clean linen cupboard",root,new Vector3(x,.46f,4.47f),new Vector3(.49f,.60f,.42f),"Sage");
                    art.Box("Fresh linen stack",root,new Vector3(x,.80f,4.47f),new Vector3(.35f,.07f,.28f),"Linen");
                    art.Box("Cupboard pull",root,new Vector3(x,.53f,4.24f),new Vector3(.14f,.03f,.025f),"Gold");
                }
                else WallPicture(art,root,new Vector3(-5.53f,1.20f,.71f+n*.75f),true,n);
            }
        }
        private static void Waiting(ClinicArt art,Transform root,int track,int n)
        {
            if(track==0)
            {
                float z=1.2f+n*.57f;
                art.Box("Reading shelf",root,new Vector3(1.43f,.43f,z),new Vector3(.32f,.08f,.43f),"Wood");
                art.Cylinder("Reading shelf foot",root,new Vector3(1.43f,.28f,z),new Vector3(.075f,.28f,.075f),"Gold");
                art.Box("Lounge reading book",root,new Vector3(1.43f,.49f,z),new Vector3(.27f,.045f,.25f),n%2==0?"Apricot":"Blue");
            }
            else if(track==1)
            {
                // Seat pairs themselves expand in ClinicWorld; these matching wall sconces identify each addition.
                float z=.30f+n*.80f;
                art.Box("Lounge comfort sconce",root,new Vector3(5.54f,.89f,z),new Vector3(.09f,.20f,.19f),"Gold");
                art.Orb("Sconce shade",root,new Vector3(5.49f,1.02f,z),new Vector3(.19f,.24f,.19f),"Linen");
            }
            else WallPicture(art,root,new Vector3(1.73f+n*.78f,1.54f,4.84f),false,n);
        }
        private static void WallPicture(ClinicArt art,Transform parent,Vector3 position,bool west,int index)
        {
            var root=art.Group("Botanical framed print",parent,position);
            if(west)root.localRotation=Quaternion.Euler(0,90,0);
            art.Box("Oak frame",root,Vector3.zero,new Vector3(.48f,.54f,.055f),"Wood");
            art.Box("Print paper",root,new Vector3(0,0,-.037f),new Vector3(.41f,.46f,.015f),"Linen");
            var leaf=art.Orb("Embossed botanical leaf",root,new Vector3(0,.03f,-.050f),new Vector3(.19f,.31f,.018f),index%2==0?"Sage":"Apricot");
            leaf.transform.localRotation=Quaternion.Euler(0,0,index%2==0?-23:23);
            art.Box("Botanical stem",root,new Vector3(0,-.10f,-.060f),new Vector3(.015f,.21f,.015f),"Gold");
        }
    }
}
