using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleClinic.Presentation
{
    internal sealed class ClinicArt : IDisposable
    {
        internal const int Layer=10;
        private readonly Dictionary<string,GameObject> models=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        private readonly Mesh cube,sphere,cylinder;
        internal ClinicArt() { cube=ClinicPrimitives.Cube();sphere=ClinicPrimitives.Sphere();cylinder=ClinicPrimitives.Cylinder(); }
        internal GameObject Model(string name,Transform parent,Vector3 position,Quaternion rotation=default)
        {
            var wrapper=new GameObject(name);wrapper.transform.SetParent(parent,false);wrapper.transform.localPosition=position;
            wrapper.transform.localRotation=rotation==default ? Quaternion.identity : rotation;
            if(!models.TryGetValue(name,out var model)) { model=Resources.Load<GameObject>("Clinic/Models/"+name);models[name]=model; }
            if(model==null) { Debug.LogError("Missing authored clinic model "+name);return wrapper; }
            var instance=UnityEngine.Object.Instantiate(model,wrapper.transform,false);
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++) slots[i]=Material(slots[i]==null ? "Ivory" : slots[i].name.Replace(" (Instance)","").Split('.')[0]);
                renderer.sharedMaterials=slots;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                if(renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen=false;
            }
            SetLayer(wrapper.transform);return wrapper;
        }
        internal Transform Group(string name,Transform parent,Vector3 position=default)
        { var item=new GameObject(name);item.transform.SetParent(parent,false);item.transform.localPosition=position;return item.transform; }
        internal GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,string role)=>Shape(name,parent,position,size,cube,role);
        internal GameObject Orb(string name,Transform parent,Vector3 position,Vector3 size,string role)=>Shape(name,parent,position,size,sphere,role);
        internal GameObject Cylinder(string name,Transform parent,Vector3 position,Vector3 size,string role)=>Shape(name,parent,position,new Vector3(size.x,size.y*.5f,size.z),cylinder,role);
        private GameObject Shape(string name,Transform parent,Vector3 position,Vector3 size,Mesh mesh,string role)
        {
            var result=new GameObject(name){layer=Layer};result.transform.SetParent(parent,false);
            result.transform.localPosition=position;result.transform.localScale=size;
            result.AddComponent<MeshFilter>().sharedMesh=mesh;result.AddComponent<MeshRenderer>().sharedMaterial=Material(role);return result;
        }
        private Material Material(string role)
        {
            if(materials.TryGetValue(role,out var material)) return material;
            material=new Material(Shader.Find(role=="Paper" ? "Unlit/Color" : "Standard")){name="Clinic "+role,enableInstancing=true};
            material.color=Color(role);
            if(role!="Paper") { material.SetFloat("_Glossiness",role=="Gold"?.40f:.19f);material.SetFloat("_Metallic",role=="Gold"?.45f:0); }
            materials.Add(role,material);return material;
        }
        internal static Color Color(string role)
        {
            switch(role)
            {
                case "Paper":return new Color(.95f,.935f,.875f);
                case "Ivory":return new Color(.88f,.875f,.81f);
                case "Linen":return new Color(.94f,.93f,.86f);
                case "Sage":return new Color(.40f,.57f,.45f);
                case "SageDark":return new Color(.22f,.36f,.28f);
                case "Apricot":return new Color(.84f,.49f,.31f);
                case "Gold":return new Color(.76f,.56f,.23f);
                case "Ink":return new Color(.20f,.27f,.26f);
                case "Blue":return new Color(.41f,.62f,.64f);
                case "Skin":return new Color(.78f,.56f,.39f);
                case "Wood":return new Color(.59f,.40f,.27f);
                case "Clay":return new Color(.74f,.55f,.41f);
                case "Leaf":return new Color(.31f,.49f,.30f);
                case "Asphalt":return new Color(.53f,.57f,.52f);
                default:return new Color(.86f,.84f,.77f);
            }
        }
        private static void SetLayer(Transform root) { root.gameObject.layer=Layer;foreach(Transform child in root)SetLayer(child); }
        internal static void Destroy(UnityEngine.Object value) { if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value); }
        public void Dispose() { foreach(var material in materials.Values)Destroy(material);Destroy(cube);Destroy(sphere);Destroy(cylinder); }
    }
}
