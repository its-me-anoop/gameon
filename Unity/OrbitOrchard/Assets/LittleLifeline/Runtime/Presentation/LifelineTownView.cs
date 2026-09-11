using LittleLifeline.Core;
using UnityEngine;

namespace LittleLifeline.Presentation
{
    /// <summary>Cached destination scenery and the two independently persistent town projects.</summary>
    internal sealed class LifelineTownView
    {
        private readonly LifelineArt art;
        private readonly Transform root;
        private readonly Transform[] towns = new Transform[3];
        private readonly ProjectVisual[,] projects = new ProjectVisual[3,2];
        private TownId shownTown = (TownId)(-1);
        private int shownMask = -1;

        internal LifelineTownView(LifelineArt art, Transform parent)
        {
            this.art = art;
            root = Group("Town scenery",parent,Vector3.zero);
            for (int i=0;i<towns.Length;i++) towns[i]=Group(((TownId)i).ToString(),root,Vector3.zero);
            BuildWillowbank(towns[0]);
            BuildCopperhill(towns[1]);
            BuildSeabrook(towns[2]);
            Apply(TownId.Willowbank,0);
        }

        internal void Render(SimulationState state)
        {
            int mask=0;
            for (int i=0;i<state.Towns.Count;i++)
                if (state.Towns[i].Town==state.Town) { mask=state.Towns[i].ProjectCompletionMask; break; }
            if (shownTown!=state.Town || shownMask!=mask) Apply(state.Town,mask);
        }

        internal void SetFocused(bool focused) => root.gameObject.SetActive(!focused);

        private void Apply(TownId town,int mask)
        {
            shownTown=town;shownMask=mask;
            for (int i=0;i<towns.Length;i++) towns[i].gameObject.SetActive(i==(int)town);
            for (int index=0;index<2;index++) projects[(int)town,index].SetRestored((mask&(1<<index))!=0);
        }

        private void BuildWillowbank(Transform town)
        {
            var river=Group("River",town,new Vector3(5.15f,-.005f,.7f));
            art.Box("Gentle riverside water",river,Vector3.zero,new Vector3(3.1f,.07f,23),"River");
            for (int i=0;i<9;i++)
            {
                art.Orb("Riverbank stone",river,new Vector3(-1.4f,.02f,-10+i*2.5f),new Vector3(.40f,.20f,.53f),"Rock");
                if (i%2==0) art.Model("Tree",town,new Vector3(6.3f,0,-8+i*2.1f),.62f);
            }
            var bridge=Group("Village footbridge",town,new Vector3(4.8f,.12f,8.8f));
            for (int i=0;i<8;i++) art.Box("Bridge plank",bridge,new Vector3(-1.25f+i*.36f,.10f,0),new Vector3(.33f,.12f,1.05f),"Wood");
            foreach(float z in new[]{-.48f,.48f}) art.Box("Bridge handrail",bridge,new Vector3(0,.74f,z),new Vector3(2.9f,.07f,.07f),"Oat");
            var garden=Project("Station garden",town,new Vector3(2.9f,0,-3.8f),0,0);
            art.Box("Waiting garden beds",garden.Unrestored,new Vector3(0,.13f,0),new Vector3(1.9f,.26f,2.5f),"Wood");
            Boards(garden.Unrestored,2.15f,0);
            art.Model("Garden",garden.Restored,Vector3.zero);
            var bench=art.Model("Bench",garden.Restored,new Vector3(0,0,-1.7f),.73f);
            bench.transform.localRotation=Quaternion.Euler(0,180,0);
            art.Model("Resident",garden.Restored,new Vector3(-.8f,0,-1.45f),.63f);
            var school=Project("Village school",town,new Vector3(3.0f,0,3.1f),0,1);
            Building(school.Root,new Vector3(2.35f,1.65f,1.85f),"Oat","VillageRoof");
            Windows(school.Unrestored,false,2.35f);
            Windows(school.Restored,true,2.35f);
            Boards(school.Unrestored,1.1f,-1.02f);
            // The bell turret and playground make the school distinct from the station.
            art.Box("School bell turret",school.Root,new Vector3(0,2.17f,0),new Vector3(.54f,.69f,.53f),"Oat");
            Roof(school.Root,new Vector3(0,2.56f,0),.82f,.73f,"VillageRoof");
            art.Cylinder("School bell",school.Restored,new Vector3(0,2.30f,-.29f),new Vector3(.24f,.22f,.24f),"Brass");
            art.Box("Open school door",school.Restored,new Vector3(0,.59f,-.94f),new Vector3(.52f,1.17f,.08f),"OpenWindow");
            art.Box("School noticeboard",school.Restored,new Vector3(.88f,1.17f,-.97f),new Vector3(.36f,.42f,.06f),"Paper");
            for(int i=0;i<3;i++) art.Model("Resident",school.Restored,new Vector3(-.65f+i*.58f,.02f,-1.35f),.47f);
            art.Box("Playground hopscotch",school.Restored,new Vector3(.3f,.02f,-2.10f),new Vector3(1.1f,.035f,.9f),"Linen");
            for(int i=0;i<4;i++) art.Box("Hopscotch square",school.Restored,new Vector3(-.05f+i*.23f,.044f,-2.10f),new Vector3(.15f,.018f,.60f),"Clay");
        }

        private void BuildCopperhill(Transform town)
        {
            var rocks=Group("Rock terraces",town,new Vector3(4.4f,0,0));
            for(int i=0;i<10;i++)
            {
                var rock=art.Box("Weathered copperhill ledge",rocks,new Vector3(i%2*.9f,.15f+(i%3)*.15f,-9+i*2.0f),new Vector3(1.6f,.50f+(i%3)*.28f,1.55f),i%2==0?"Rock":"Copper");
                rock.transform.localRotation=Quaternion.Euler(0,i*17,0);
            }
            var workshop=Project("Community workshop",town,new Vector3(2.95f,0,-3.8f),1,0);
            Building(workshop.Root,new Vector3(2.45f,1.45f,1.95f),"Clay","Copper");
            art.Box("Workshop bay shadow",workshop.Root,new Vector3(0,.66f,-.992f),new Vector3(1.62f,1.26f,.06f),"ClosedWindow");
            Boards(workshop.Unrestored,1.80f,-1.08f);
            art.Box("Timber workbench",workshop.Restored,new Vector3(0,.72f,-1.2f),new Vector3(1.55f,.12f,.58f),"Wood");
            foreach(float x in new[]{-.56f,.56f}) art.Box("Workbench legs",workshop.Restored,new Vector3(x,.35f,-1.2f),new Vector3(.10f,.68f,.38f),"Iron");
            art.Box("Repaired workshop shutter",workshop.Restored,new Vector3(0,1.34f,-1.02f),new Vector3(1.72f,.25f,.11f),"Copper");
            art.Box("Tool handle",workshop.Restored,new Vector3(.31f,.82f,-1.18f),new Vector3(.44f,.06f,.08f),"Wood");
            art.Box("Hammer head",workshop.Restored,new Vector3(.51f,.84f,-1.18f),new Vector3(.15f,.09f,.25f),"Iron");
            art.Model("Crew",workshop.Restored,new Vector3(-.70f,0,-1.75f),.66f);
            art.Box("Workshop chimney",workshop.Root,new Vector3(.68f,2.1f,.38f),new Vector3(.37f,.80f,.40f),"Copper");
            var tower=Project("Clock tower",town,new Vector3(3.05f,0,3.1f),1,1);
            art.Box("Clock tower footing",tower.Root,new Vector3(0,.16f,0),new Vector3(1.85f,.32f,1.78f),"Rock");
            art.Box("Clock tower masonry",tower.Root,new Vector3(0,1.63f,0),new Vector3(1.20f,2.94f,1.12f),"Oat");
            for(int i=0;i<4;i++) art.Box("Stone cornice",tower.Root,new Vector3(0,.58f+i*.65f,0),new Vector3(1.31f,.09f,1.22f),"Rock");
            Roof(tower.Root,new Vector3(0,3.26f,0),1.67f,1.48f,"Copper");
            Disc("Silent clock face",tower.Unrestored,new Vector3(0,2.43f,-.60f),.73f,"ClosedWindow");
            Disc("Restored brass clock rim",tower.Restored,new Vector3(0,2.43f,-.61f),.86f,"Brass");
            Disc("Clock face",tower.Restored,new Vector3(0,2.43f,-.66f),.73f,"Paper");
            var minute=art.Box("Clock minute hand",tower.Restored,new Vector3(.07f,2.54f,-.704f),new Vector3(.045f,.33f,.025f),"Iron");
            minute.transform.localRotation=Quaternion.Euler(0,0,-35);
            var hour=art.Box("Clock hour hand",tower.Restored,new Vector3(-.075f,2.46f,-.709f),new Vector3(.24f,.053f,.024f),"Iron");
            hour.transform.localRotation=Quaternion.Euler(0,0,12);
            Boards(tower.Unrestored,.74f,-.65f);
            art.Box("Open tower door",tower.Restored,new Vector3(0,.62f,-.58f),new Vector3(.52f,1.07f,.07f),"VillageRoof");
            art.Model("Garden",tower.Restored,new Vector3(1.07f,0,.10f),.40f);
        }

        private void BuildSeabrook(Transform town)
        {
            var sea=Group("Coastal water",town,new Vector3(5.0f,-.01f,.8f));
            art.Box("Seabrook bay",sea,Vector3.zero,new Vector3(4.1f,.06f,24),"Water");
            for(int i=0;i<7;i++)
            {
                art.Orb("Coastal breakwater",sea,new Vector3(-1.6f,.06f,-10+i*3.2f),new Vector3(.90f,.40f,1.12f),"Rock");
                art.Box("Foam line",sea,new Vector3(.7f,.036f,-9.7f+i*3.2f),new Vector3(1.50f,.018f,.06f),"Linen");
            }
            var clinic=Project("Seaside clinic",town,new Vector3(2.95f,0,-3.8f),2,0);
            art.Box("Clinic sandbank",clinic.Root,new Vector3(0,.035f,0),new Vector3(3.05f,.13f,3.85f),"Sand");
            Building(clinic.Root,new Vector3(2.30f,1.47f,1.88f),"Linen","CoastalRoof");
            Windows(clinic.Unrestored,false,2.30f);
            Windows(clinic.Restored,true,2.30f);
            Boards(clinic.Unrestored,1.12f,-1.06f);
            art.Box("Clinic open door",clinic.Restored,new Vector3(0,.60f,-.98f),new Vector3(.57f,1.17f,.08f),"OpenWindow");
            art.Box("Clinic veranda",clinic.Restored,new Vector3(0,1.35f,-1.38f),new Vector3(2.50f,.095f,1.05f),"CoastalRoof");
            foreach(float x in new[]{-1.03f,1.03f}) art.Box("Veranda post",clinic.Restored,new Vector3(x,.68f,-1.80f),new Vector3(.065f,1.34f,.065f),"Linen");
            var bench=art.Model("Bench",clinic.Restored,new Vector3(.22f,.10f,-1.46f),.55f);
            bench.transform.localRotation=Quaternion.Euler(0,180,0);
            art.Model("Crew",clinic.Restored,new Vector3(-.8f,.10f,-1.55f),.66f);
            // The lighthouse exists first; project two repairs the route and relights its lantern.
            var path=Project("Lighthouse path",town,new Vector3(3.0f,0,3.1f),2,1);
            art.Orb("Lighthouse rocky island",path.Root,new Vector3(.42f,.05f,.55f),new Vector3(2.2f,.42f,2.25f),"Rock");
            art.Cylinder("Lighthouse base",path.Root,new Vector3(.43f,.70f,.55f),new Vector3(.98f,1.24f,.98f),"Linen");
            art.Cylinder("Lighthouse stripe",path.Root,new Vector3(.43f,1.35f,.55f),new Vector3(.91f,.33f,.91f),"Clay");
            art.Cylinder("Lighthouse upper tower",path.Root,new Vector3(.43f,1.85f,.55f),new Vector3(.85f,.73f,.85f),"Linen");
            art.Cylinder("Lighthouse balcony",path.Root,new Vector3(.43f,2.25f,.55f),new Vector3(1.20f,.13f,1.20f),"CoastalRoof");
            art.Cylinder("Dark lighthouse lantern",path.Unrestored,new Vector3(.43f,2.54f,.55f),new Vector3(.73f,.51f,.73f),"ClosedWindow");
            art.Cylinder("Relit lighthouse lantern",path.Restored,new Vector3(.43f,2.54f,.55f),new Vector3(.73f,.51f,.73f),"OpenWindow");
            art.Cylinder("Lighthouse cap",path.Root,new Vector3(.43f,2.85f,.55f),new Vector3(.91f,.14f,.91f),"CoastalRoof");
            for(int i=0;i<8;i++)
            {
                var point=new Vector3(-.22f,.20f,-2.1f+i*.34f);
                art.Box("Repaired boardwalk",path.Restored,point,new Vector3(.85f,.09f,.31f),"Wood");
                if(i<2 || i>5)
                {
                    var broken=art.Box("Broken boardwalk",path.Unrestored,point,new Vector3(.65f,.065f,.25f),"Rock");
                    broken.transform.localRotation=Quaternion.Euler(0,i%2==0 ? 18 : -24,0);
                }
            }
            foreach(float x in new[]{-.70f,.25f})
            {
                art.Box("Safe boardwalk railing",path.Restored,new Vector3(x,.66f,-.99f),new Vector3(.055f,.07f,2.68f),"Linen");
                for(int i=0;i<4;i++) art.Box("Railing post",path.Restored,new Vector3(x,.45f,-2.13f+i*.81f),new Vector3(.055f,.64f,.055f),"Linen");
            }
            Boards(path.Unrestored,.94f,-2.36f);
            art.Model("Resident",path.Restored,new Vector3(-.19f,.26f,-1.20f),.62f);
        }

        private ProjectVisual Project(string name,Transform parent,Vector3 position,int town,int index)
        {
            var value=new ProjectVisual(Group(name,parent,position));
            value.Root.localRotation=Quaternion.Euler(0,28,0);
            projects[town,index]=value;
            return value;
        }

        private void Building(Transform parent,Vector3 size,string wall,string roof)
        {
            art.Box("Building footing",parent,new Vector3(0,.12f,0),new Vector3(size.x+.18f,.24f,size.z+.18f),"Rock");
            art.Box("Town building",parent,new Vector3(0,size.y*.5f+.12f,0),size,wall);
            Roof(parent,new Vector3(0,size.y+.32f,0),size.x+.32f,size.z+.32f,roof);
        }

        private void Roof(Transform parent,Vector3 point,float width,float depth,string role)
        {
            for(int side=-1;side<=1;side+=2)
            {
                var roof=art.Box("Pitched roof",parent,point+new Vector3(side*width*.25f,0,0),new Vector3(width*.56f,.11f,depth),role);
                roof.transform.localRotation=Quaternion.Euler(0,0,side*-22);
            }
        }

        private void Windows(Transform parent,bool open,float width)
        {
            foreach(float side in new[]{-1f,1f})
            {
                art.Box("Window frame",parent,new Vector3(side*width*.32f,1.05f,-.962f),new Vector3(.47f,.60f,.06f),"Wood");
                art.Box("Window glass",parent,new Vector3(side*width*.32f,1.05f,-1.003f),new Vector3(.36f,.47f,.025f),open ? "OpenWindow" : "ClosedWindow");
                if(open) art.Box("Window flower box",parent,new Vector3(side*width*.32f,.78f,-1.07f),new Vector3(.48f,.10f,.16f),"Leaf");
            }
        }

        private void Boards(Transform parent,float width,float z)
        {
            foreach(float y in new[]{.58f,.89f})
            {
                var board=art.Box("Restoration barrier",parent,new Vector3(0,y,z),new Vector3(width,.16f,.075f),"Wood");
                board.transform.localRotation=Quaternion.Euler(0,0,y<.7f ? 12 : -12);
            }
        }

        private void Disc(string name,Transform parent,Vector3 point,float diameter,string role)
        {
            var disc=art.Cylinder(name,parent,point,new Vector3(diameter,.04f,diameter),role);
            disc.transform.localRotation=Quaternion.Euler(90,0,0);
        }

        private static Transform Group(string name,Transform parent,Vector3 position)
        {
            var result=new GameObject(name).transform;
            result.SetParent(parent,false);result.localPosition=position;
            return result;
        }

        private sealed class ProjectVisual
        {
            internal readonly Transform Root,Unrestored,Restored;
            internal ProjectVisual(Transform root)
            { Root=root;Unrestored=Group("Unrestored",root,Vector3.zero);Restored=Group("Restored",root,Vector3.zero); }
            internal void SetRestored(bool value)
            { Restored.gameObject.SetActive(value);Unrestored.gameObject.SetActive(!value); }
        }
    }
}
