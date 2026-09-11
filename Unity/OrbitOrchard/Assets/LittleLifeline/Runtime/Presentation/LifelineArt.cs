using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleLifeline.Presentation
{
    internal sealed class LifelineArt : IDisposable
    {
        internal const int Layer = 9;
        private readonly Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly Mesh cube;
        private readonly Mesh sphere;
        private readonly Mesh cylinder;
        private string livery = "base";

        internal LifelineArt()
        {
            cube = PrimitiveMesh(PrimitiveType.Cube);
            sphere = PrimitiveMesh(PrimitiveType.Sphere);
            cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
        }

        internal GameObject Model(string name, Transform parent, Vector3 position, float scale = 1)
        {
            var wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = position;
            wrapper.transform.localScale = Vector3.one * scale;
            if (!templates.TryGetValue(name, out GameObject template))
            {
                template = Resources.Load<GameObject>("Models/" + name);
                templates[name] = template;
            }
            if (template == null)
            {
                Debug.LogError("Little Lifeline model is missing: " + name);
                Box("Missing model", wrapper.transform, Vector3.zero, new Vector3(.5f,.5f,.5f), "Berry");
                return wrapper;
            }
            var imported = UnityEngine.Object.Instantiate(template, wrapper.transform, false);
            foreach (var renderer in imported.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = Material(slots[i] == null ? "Oat" : slots[i].name.Replace(" (Instance)", "").Split('.')[0]);
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            SetLayer(wrapper.transform);
            return wrapper;
        }

        internal GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, string role) =>
            Shape(name, parent, position, scale, cube, role);

        internal GameObject Orb(string name, Transform parent, Vector3 position, Vector3 scale, string role) =>
            Shape(name, parent, position, scale, sphere, role);

        internal GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 size, string role) =>
            Shape(name,parent,position,new Vector3(size.x,size.y*.5f,size.z),cylinder,role);

        private GameObject Shape(string name, Transform parent, Vector3 position, Vector3 scale, Mesh mesh, string role)
        {
            var result = new GameObject(name) { layer = Layer };
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = scale;
            result.AddComponent<MeshFilter>().sharedMesh = mesh;
            result.AddComponent<MeshRenderer>().sharedMaterial = Material(role);
            return result;
        }

        internal Material Material(string role)
        {
            if (materials.TryGetValue(role, out Material material)) return material;
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = "Lifeline " + role, enableInstancing = true };
            material.color = LifelinePalette.Color(role, livery);
            material.SetFloat("_Glossiness", role == "Brass" ? .38f : .20f);
            material.SetFloat("_Metallic", role == "Brass" ? .35f : .02f);
            materials.Add(role, material);
            return material;
        }

        internal void SetLivery(string id)
        {
            if (livery == id) return;
            livery = id;
            foreach (var item in materials) item.Value.color = LifelinePalette.Color(item.Key, livery);
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var temporary = GameObject.CreatePrimitive(type);
            var mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            temporary.SetActive(false);
            Destroy(temporary);
            return mesh;
        }

        private static void SetLayer(Transform parent)
        {
            parent.gameObject.layer = Layer;
            foreach (Transform child in parent) SetLayer(child);
        }

        internal static void Destroy(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        public void Dispose()
        {
            foreach (var material in materials.Values) Destroy(material);
        }
    }

    internal static class LifelinePalette
    {
        internal static Color Color(string role, string livery)
        {
            if (role == "Seaweed" || role == "Enamel")
            {
                double light = role == "Seaweed" ? .41 : .56;
                if (livery == "sunrise") return Oklch(light + .14, .10, 48);
                if (livery == "coastal") return Oklch(light + .03, .055, 225);
                if (livery == "heritage") return Oklch(light - .06, .068, 154);
                return Oklch(light, .045, 155);
            }
            if (role == "Blue")
            {
                if (livery == "sunrise") return Oklch(.72, .105, 47);
                if (livery == "coastal") return Oklch(.79, .035, 190);
                if (livery == "heritage") return Oklch(.69, .11, 78);
                return Oklch(.64, .055, 220);
            }
            switch (role)
            {
                case "Paper": return Oklch(.95,.025,87);
                case "Oat": return Oklch(.86,.024,85);
                case "Linen": return Oklch(.94,.026,90);
                case "Brass": return Oklch(.70,.095,78);
                case "Clay": return Oklch(.68,.065,48);
                case "Wood": return Oklch(.44,.055,56);
                case "Iron": return Oklch(.29,.012,155);
                case "Glass": return Oklch(.75,.040,177);
                case "Leaf": return Oklch(.54,.075,131);
                case "LeafLight": return Oklch(.68,.090,113);
                case "Flower": return Oklch(.80,.12,84);
                case "Skin": return Oklch(.60,.085,49);
                case "SkinLight": return Oklch(.77,.073,58);
                case "Hair": return Oklch(.28,.025,45);
                case "Berry": return Oklch(.53,.080,29);
                case "Ground": return Oklch(.90,.014,92);
                case "Water": return Oklch(.77,.060,190);
                case "River": return Oklch(.76,.038,183);
                case "Rock": return Oklch(.64,.025,62);
                case "Copper": return Oklch(.58,.073,47);
                case "Sand": return Oklch(.89,.025,83);
                case "VillageRoof": return Oklch(.43,.039,157);
                case "CoastalRoof": return Oklch(.57,.050,216);
                case "ClosedWindow": return Oklch(.35,.013,191);
                case "OpenWindow": return Oklch(.80,.055,85);
                default: return Oklch(.84,.032,83);
            }
        }

        private static Color Oklch(double light, double chroma, double hue)
        {
            double angle = hue * Math.PI / 180, a = chroma * Math.Cos(angle), b = chroma * Math.Sin(angle);
            double l = light + .3963377774 * a + .2158037573 * b;
            double m = light - .1055613458 * a - .0638541728 * b;
            double s = light - .0894841775 * a - 1.2914855480 * b;
            l *= l*l; m *= m*m; s *= s*s;
            return new Color(Channel(4.0767416621*l-3.3077115913*m+.2309699292*s),
                Channel(-1.2684380046*l+2.6097574011*m-.3413193965*s),
                Channel(-.0041960863*l-.7034186147*m+1.7076147010*s));
        }
        private static float Channel(double value) => Mathf.Clamp01((float)(value <= .0031308 ? 12.92*value : 1.055*Math.Pow(value,1/2.4)-.055));
    }
}
