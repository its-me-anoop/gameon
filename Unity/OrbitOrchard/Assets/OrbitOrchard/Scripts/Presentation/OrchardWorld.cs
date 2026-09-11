using System;
using System.Collections.Generic;
using OrbitOrchard.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitOrchard.Presentation
{
    /// <summary>Owns the Unity scene and applies pure simulation snapshots.</summary>
    public sealed class OrchardWorld : MonoBehaviour
    {
        private const int WorldLayer = 8;
        private const int MaximumTextureDimension = 1536;
        private static readonly Vector3 CameraPosition = new Vector3(0, 10, 6);
        private readonly Plane orbitPlane = new Plane(Vector3.up, Vector3.zero);
        private OrchardArt art;
        private OrchardBodyPool bodies;
        private OrchardGarden garden;
        private Transform catcher;
        private Transform catchArc;
        private readonly List<Transform> basketSeeds = new List<Transform>(5);
        private OrchardGame previousGame;
        private int previousCarried;
        private int previousHearts = 3;
        private double feedbackAt = double.NegativeInfinity;
        private bool feedbackWasHit;
        private bool initialized;
        private bool preview;
        private string theme;
        private float previousAspect;

        public Camera SceneCamera { get; private set; }
        public RenderTexture SceneTexture { get; private set; }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            art = new OrchardArt(WorldLayer);
            var sculptures = new GameObject("Sculptures").transform;
            sculptures.SetParent(transform, false);
            var cameraObject = new GameObject("Orchard camera");
            cameraObject.transform.SetParent(transform, false);
            SceneCamera = cameraObject.AddComponent<Camera>();
            SceneCamera.transform.position = CameraPosition;
            SceneCamera.transform.LookAt(Vector3.zero, Vector3.up);
            SceneCamera.clearFlags = CameraClearFlags.SolidColor;
            SceneCamera.cullingMask = 1 << WorldLayer;
            SceneCamera.nearClipPlane = 0.05f;
            SceneCamera.farClipPlane = 60;
            SceneCamera.allowHDR = false;
            SceneCamera.allowMSAA = true;
            SceneCamera.rect = new Rect(0, 0, 1, 1);

            ConfigureLighting();
            art.Model("Island", sculptures);
            art.Ring("Catch orbit", sculptures, 1.6f, 0.026f, 0, Mathf.PI * 2, "Track");
            catchArc = art.Ring("Catch window", sculptures, 1.6f, 0.066f,
                -(float)OrchardGame.CatchHalfAngle, (float)OrchardGame.CatchHalfAngle, "Petal").transform;
            catchArc.localPosition = new Vector3(0, -0.035f, 0);
            catcher = art.Model("Gardener", sculptures).transform;
            for (int index = 0; index < 5; index++)
            {
                var seed = art.Shape("Basket seed", catcher, art.Sphere, "Gold",
                    new Vector3((index % 3 - 1) * 0.10f, 0.38f + index / 3 * 0.065f, 0.02f),
                    new Vector3(0.075f, 0.08f, 0.075f));
                seed.SetActive(false);
                basketSeeds.Add(seed.transform);
            }
            bodies = new OrchardBodyPool(art, sculptures);
            garden = new OrchardGarden(art, sculptures);
            ApplyTheme("orchard");
            SetRenderSize(512, 512);
        }

        /// <summary>Resizes only when board pixels change; UI owns clipping and placement.</summary>
        public RenderTexture SetRenderSize(int width, int height)
        {
            Initialize();
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            float scale = Mathf.Min(1, MaximumTextureDimension / (float)Mathf.Max(width, height));
            width = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            if (SceneTexture != null && SceneTexture.width == width && SceneTexture.height == height) return SceneTexture;

            ReleaseTexture();
            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 16)
            {
                msaaSamples = 2,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
            };
            descriptor.msaaSamples = Mathf.Max(1, SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));
            SceneTexture = new RenderTexture(descriptor) { name = "Orbit Orchard board", filterMode = FilterMode.Bilinear };
            SceneTexture.Create();
            SceneCamera.targetTexture = SceneTexture;
            UpdateCameraFraming();
            return SceneTexture;
        }

        public void Render(OrchardGame game, int bloomCount, string themeID, bool reducedMotion, bool preview = false)
        {
            if (game == null) return;
            Initialize();
            ApplyTheme(themeID);
            float actualAspect = Mathf.Max(1, SceneCamera.pixelWidth) / (float)Mathf.Max(1, SceneCamera.pixelHeight);
            if (this.preview != preview || Mathf.Abs(previousAspect - actualAspect) > 0.001f)
            {
                this.preview = preview;
                UpdateCameraFraming();
            }

            bool newGame = !ReferenceEquals(previousGame, game);
            if (newGame)
            {
                feedbackAt = double.NegativeInfinity;
                previousCarried = game.Carried;
                previousHearts = game.Hearts;
                bodies.Clear();
            }
            if (game.Carried > previousCarried || game.Hearts < previousHearts)
            {
                feedbackAt = game.Elapsed;
                feedbackWasHit = game.Hearts < previousHearts;
            }
            previousCarried = game.Carried;
            previousHearts = game.Hearts;

            float angle = (float)game.CatcherAngle;
            catcher.localPosition = new Vector3(Mathf.Cos(angle) * 1.6f, 0.025f, Mathf.Sin(angle) * 1.6f);
            float pulse = reducedMotion ? 0 : (float)Math.Exp(-12 * Math.Max(0, game.Elapsed - feedbackAt));
            catcher.localScale = Vector3.one * (1 + pulse * (feedbackWasHit ? -0.10f : 0.10f));
            catchArc.localRotation = Quaternion.AngleAxis(-angle * Mathf.Rad2Deg, Vector3.up);
            for (int index = 0; index < basketSeeds.Count; index++)
                basketSeeds[index].gameObject.SetActive(index < game.Carried);

            bodies.Render(game.Bodies, preview);
            garden.Render(bloomCount, game.Elapsed, reducedMotion || preview, newGame);
            previousGame = game;
        }

        /// <summary>Bottom-left normalized image coordinates, independent of panel placement.</summary>
        public bool ViewportPointToAngle(Vector2 point, out double angle)
        {
            angle = 0;
            if (!initialized || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return false;
            Ray ray = SceneCamera.ViewportPointToRay(point);
            if (!orbitPlane.Raycast(ray, out float distance)) return false;
            Vector3 hit = ray.GetPoint(distance);
            if (hit.x * hit.x + hit.z * hit.z < 0.08f) return false;
            angle = Math.Atan2(hit.z, hit.x);
            if (angle < 0) angle += Math.PI * 2;
            return true;
        }

        private void ApplyTheme(string themeID)
        {
            themeID = string.IsNullOrEmpty(themeID) ? "orchard" : themeID;
            if (theme == themeID) return;
            theme = themeID;
            art.SetTheme(themeID);
            SceneCamera.backgroundColor = OrchardColors.Get("Paper", themeID);
        }

        private void UpdateCameraFraming()
        {
            float width = Mathf.Max(1, SceneCamera.pixelWidth);
            float height = Mathf.Max(1, SceneCamera.pixelHeight);
            previousAspect = width / height;
            SceneCamera.aspect = previousAspect;
            float radius = preview ? 2.15f : 3.12f;
            float tangent = 0.01f;
            for (int index = 0; index < 96; index++)
            {
                float angle = index / 96f * Mathf.PI * 2;
                Vector3 relative = SceneCamera.transform.InverseTransformPoint(
                    new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
                tangent = Mathf.Max(tangent, Mathf.Abs(relative.x) / relative.z / previousAspect / 0.88f);
                tangent = Mathf.Max(tangent, Mathf.Abs(relative.y) / relative.z / 0.88f);
            }
            SceneCamera.fieldOfView = Mathf.Atan(tangent) * 2 * Mathf.Rad2Deg;
        }

        private void ConfigureLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.84f, 0.81f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.60f, 0.56f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.27f, 0.20f);
            RenderSettings.ambientIntensity = 1;
            AddLight("Softbox", new Vector3(-3, 7, 5), 1.1f, true);
            AddLight("Soft fill", new Vector3(4, 2, -3), 0.5f, false);
        }

        private void AddLight(string name, Vector3 position, float intensity, bool shadows)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.position = position;
            lightObject.transform.LookAt(Vector3.zero);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = new Color(1, 0.96f, 0.87f);
            light.cullingMask = 1 << WorldLayer;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.45f;
            light.shadowBias = 0.03f;
            light.shadowNormalBias = 0.2f;
        }

        private void ReleaseTexture()
        {
            if (SceneTexture == null) return;
            if (SceneCamera != null) SceneCamera.targetTexture = null;
            SceneTexture.Release();
            if (Application.isPlaying) Destroy(SceneTexture);
            else DestroyImmediate(SceneTexture);
            SceneTexture = null;
        }

        private void OnDestroy()
        {
            ReleaseTexture();
            art?.Dispose();
        }
    }

    /// <summary>Pool membership changes only when bodies enter or leave a run.</summary>
    internal sealed class OrchardBodyPool
    {
        private sealed class Item { public GameObject Object; public SeedKind Kind; }
        private readonly OrchardArt art;
        private readonly Transform parent;
        private readonly Dictionary<int, Item> active = new Dictionary<int, Item>();
        private readonly Dictionary<SeedKind, Stack<Item>> pools = new Dictionary<SeedKind, Stack<Item>>();
        private readonly HashSet<int> live = new HashSet<int>();
        private readonly List<int> retired = new List<int>();

        public OrchardBodyPool(OrchardArt art, Transform parent)
        {
            this.art = art;
            this.parent = parent;
            foreach (SeedKind kind in Enum.GetValues(typeof(SeedKind)))
            {
                pools[kind] = new Stack<Item>();
                for (int index = 0; index < 4; index++) pools[kind].Push(Create(kind));
            }
        }

        public void Render(IReadOnlyList<SeedBody> incoming, bool hidden)
        {
            live.Clear();
            for (int index = 0; index < incoming.Count; index++) live.Add(incoming[index].Id);
            retired.Clear();
            foreach (var entry in active)
                if (!live.Contains(entry.Key)) retired.Add(entry.Key);
            for (int index = 0; index < retired.Count; index++) Release(retired[index]);

            for (int index = 0; index < incoming.Count; index++)
            {
                SeedBody body = incoming[index];
                if (!active.TryGetValue(body.Id, out Item item))
                {
                    item = pools[body.Kind].Count > 0 ? pools[body.Kind].Pop() : Create(body.Kind);
                    active.Add(body.Id, item);
                }
                item.Object.SetActive(!hidden);
                float radius = 1.6f + ((float)body.Radius - 1.6f) * 0.5f;
                float angle = (float)body.Angle;
                item.Object.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0.13f, Mathf.Sin(angle) * radius);
                item.Object.transform.localRotation = Quaternion.AngleAxis(-angle * Mathf.Rad2Deg, Vector3.up);
            }
        }

        public void Clear()
        {
            retired.Clear();
            foreach (int id in active.Keys) retired.Add(id);
            for (int index = 0; index < retired.Count; index++) Release(retired[index]);
        }

        private Item Create(SeedKind kind)
        {
            var item = new Item { Kind = kind, Object = art.Model(kind.ToString(), parent) };
            item.Object.SetActive(false);
            return item;
        }

        private void Release(int id)
        {
            Item item = active[id];
            item.Object.SetActive(false);
            pools[item.Kind].Push(item);
            active.Remove(id);
        }
    }

    /// <summary>Permanent flower instances reuse a small set of instanced meshes.</summary>
    internal sealed class OrchardGarden
    {
        private readonly OrchardArt art;
        private readonly Transform root;
        private readonly List<Transform> flowers = new List<Transform>(36);
        private readonly List<double> plantedAt = new List<double>(36);
        private bool hasSnapshot;
        private double previousElapsed;
        private int previousCount;

        public OrchardGarden(OrchardArt art, Transform parent)
        {
            this.art = art;
            root = new GameObject("Blooms").transform;
            root.SetParent(parent, false);
        }

        public void Render(int bloomCount, double elapsed, bool reducedMotion, bool newGame)
        {
            int count = Mathf.Clamp(bloomCount, 0, 36);
            bool snapshot = !hasSnapshot || newGame || elapsed < previousElapsed;
            if (snapshot)
                for (int index = 0; index < plantedAt.Count; index++) plantedAt[index] = double.NegativeInfinity;
            while (flowers.Count < count)
            {
                int index = flowers.Count;
                flowers.Add(art.Flower(index, root));
                plantedAt.Add(snapshot ? double.NegativeInfinity : elapsed);
            }
            for (int index = 0; index < flowers.Count; index++)
            {
                flowers[index].gameObject.SetActive(index < count);
                if (index < count && reducedMotion) plantedAt[index] = double.NegativeInfinity;
                else if (index < count && index >= previousCount && !snapshot) plantedAt[index] = elapsed;
                double growth = reducedMotion ? 1 : 1 - Math.Exp(-8 * Math.Max(0, elapsed - plantedAt[index]));
                flowers[index].localScale = Vector3.one * (float)Math.Max(0.001, growth);
            }
            hasSnapshot = true;
            previousElapsed = elapsed;
            previousCount = count;
        }
    }

    /// <summary>Loads Blender models and owns shared materials and generated meshes.</summary>
    internal sealed class OrchardArt : IDisposable
    {
        private const string ResourceRoot = "OrbitOrchard/Models/";
        private readonly int layer;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly Mesh cylinder;
        private readonly Mesh petals;
        private string theme = "orchard";
        public Mesh Sphere { get; }

        public OrchardArt(int layer)
        {
            this.layer = layer;
            Sphere = PrimitiveMesh(PrimitiveType.Sphere);
            cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            petals = BuildPetals();
        }

        public void SetTheme(string id)
        {
            theme = id;
            foreach (var entry in materials) entry.Value.color = OrchardColors.Get(entry.Key, theme);
        }

        public GameObject Model(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            if (!prefabs.TryGetValue(name, out GameObject prefab))
            {
                prefab = Resources.Load<GameObject>(ResourceRoot + name);
                prefabs[name] = prefab;
            }
            if (prefab != null)
            {
                GameObject imported = UnityEngine.Object.Instantiate(prefab, root.transform, false);
                imported.name = "Blender sculpture";
                foreach (var renderer in imported.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material[] slots = renderer.sharedMaterials;
                    for (int index = 0; index < slots.Length; index++)
                    {
                        string role = slots[index] != null ? slots[index].name.Replace(" (Instance)", "") : "Clay";
                        slots[index] = Material(role);
                    }
                    renderer.sharedMaterials = slots;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
            }
            else
            {
                Debug.LogWarning("Orchard model missing; using temporary geometry: " + ResourceRoot + name);
                Fallback(name, root.transform);
            }
            SetLayer(root.transform);
            return root;
        }

        public GameObject Shape(string name, Transform parent, Mesh mesh, string role, Vector3 position, Vector3 scale)
        {
            var shape = new GameObject(name);
            shape.layer = layer;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = shape.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(role);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return shape;
        }

        public GameObject Ring(string name, Transform parent, float radius, float thickness, float start, float end, string role)
        {
            int steps = Mathf.Max(12, Mathf.CeilToInt((end - start) * 24));
            const int sides = 8;
            var vertices = new Vector3[(steps + 1) * sides];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[steps * sides * 6];
            for (int step = 0; step <= steps; step++)
            {
                float angle = Mathf.Lerp(start, end, step / (float)steps);
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                for (int side = 0; side < sides; side++)
                {
                    float cross = side / (float)sides * Mathf.PI * 2;
                    Vector3 normal = radial * Mathf.Cos(cross) + Vector3.up * Mathf.Sin(cross);
                    int index = step * sides + side;
                    vertices[index] = radial * radius + normal * thickness;
                    normals[index] = normal;
                }
            }
            int cursor = 0;
            for (int step = 0; step < steps; step++)
                for (int side = 0; side < sides; side++)
                {
                    int a = step * sides + side;
                    int b = step * sides + (side + 1) % sides;
                    int c = b + sides;
                    int d = a + sides;
                    triangles[cursor++] = a; triangles[cursor++] = b; triangles[cursor++] = c;
                    triangles[cursor++] = a; triangles[cursor++] = c; triangles[cursor++] = d;
                }
            var mesh = new Mesh { name = name, vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            ownedMeshes.Add(mesh);
            return Shape(name, parent, mesh, role, Vector3.zero, Vector3.one);
        }

        public Transform Flower(int index, Transform parent)
        {
            var flower = new GameObject("Bloom " + (index + 1)).transform;
            flower.SetParent(parent, false);
            float angle = index * 2.39996f;
            float radius = 0.24f + index % 4 * 0.13f;
            flower.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0.53f, Mathf.Sin(angle) * radius);
            float height = 0.20f + index % 3 * 0.06f;
            Shape("Stem", flower, cylinder, "Leaf", new Vector3(0, height / 2, 0), new Vector3(0.025f, height / 2, 0.025f));
            var head = Shape("Five ceramic petals", flower, petals, index % 3 == 0 ? "Gold" : "Petal",
                new Vector3(0, height, 0), Vector3.one);
            head.transform.localRotation = Quaternion.Euler(24, 0, 0);
            Shape("Flower center", head.transform, Sphere, "Cream", new Vector3(0, 0.021f, 0), new Vector3(0.09f, 0.068f, 0.09f));
            return flower;
        }

        private Mesh BuildPetals()
        {
            var pieces = new CombineInstance[5];
            for (int index = 0; index < 5; index++)
            {
                float angle = index / 5f * Mathf.PI * 2;
                pieces[index] = new CombineInstance
                {
                    mesh = Sphere,
                    transform = Matrix4x4.TRS(new Vector3(Mathf.Cos(angle) * 0.065f, 0, Mathf.Sin(angle) * 0.065f),
                        Quaternion.AngleAxis(-angle * Mathf.Rad2Deg, Vector3.up), new Vector3(0.17f, 0.052f, 0.086f))
                };
            }
            var result = new Mesh { name = "Shared five-petal bloom" };
            result.CombineMeshes(pieces);
            ownedMeshes.Add(result);
            return result;
        }

        private Material Material(string role)
        {
            if (materials.TryGetValue(role, out Material material)) return material;
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { name = "Orchard " + role, enableInstancing = true };
            material.color = OrchardColors.Get(role, theme);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.24f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
            materials.Add(role, material);
            return material;
        }

        private void Fallback(string name, Transform parent)
        {
            if (name == "Island")
            {
                Shape("Clay planet", parent, Sphere, "Clay", new Vector3(0, -0.24f, 0), new Vector3(1.92f, 1.54f, 1.92f));
                Shape("Cream crown", parent, Sphere, "Cream", new Vector3(0, 0.19f, 0), new Vector3(1.82f, 0.70f, 1.82f));
                Shape("Soil", parent, Sphere, "Soil", new Vector3(0, 0.44f, 0), new Vector3(1.60f, 0.34f, 1.60f));
            }
            else if (name == "Gardener")
            {
                Shape("Pot", parent, cylinder, "Petal", new Vector3(0, 0.21f, 0), new Vector3(0.42f, 0.18f, 0.42f));
                Shape("Rim", parent, cylinder, "Cream", new Vector3(0, 0.40f, 0), new Vector3(0.46f, 0.035f, 0.46f));
                Shape("Leaf", parent, Sphere, "Leaf", new Vector3(0.07f, 0.53f, 0), new Vector3(0.31f, 0.09f, 0.13f));
            }
            else
                Shape(name, parent, Sphere, name == "Stone" ? "Ink" : name == "GoldenSeed" ? "Gold" : "LeafLight",
                    Vector3.zero, Vector3.one * (name == "Stone" ? 0.42f : 0.32f));
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);
            return mesh;
        }

        private void SetLayer(Transform current)
        {
            current.gameObject.layer = layer;
            foreach (Transform child in current) SetLayer(child);
        }

        public void Dispose()
        {
            foreach (var material in materials.Values) Destroy(material);
            foreach (var mesh in ownedMeshes) Destroy(mesh);
        }

        private static void Destroy(UnityEngine.Object obj)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    /// <summary>The same tinted OKLCH paper and clay used by the application UI.</summary>
    internal static class OrchardColors
    {
        public static Color Get(string role, string theme)
        {
            switch (role)
            {
                case "Paper":
                    if (theme == "dusk") return Oklch(0.22, 0.03, 330);
                    if (theme == "porcelain") return Oklch(0.96, 0.015, 155);
                    if (theme == "cherry") return Oklch(0.95, 0.025, 15);
                    return Oklch(0.96, 0.025, 90);
                case "Cream": return Oklch(0.95, 0.04, 88);
                case "Clay": return Oklch(0.66, 0.13, 43);
                case "ClayLight": return Oklch(0.76, 0.10, 50);
                case "Soil": return Oklch(0.66, 0.065, 93);
                case "Bark": return Oklch(0.40, 0.065, 50);
                case "Leaf": return Oklch(0.49, 0.12, 142);
                case "LeafLight": return Oklch(0.73, 0.14, 132);
                case "Petal":
                    if (theme == "dusk") return Oklch(0.78, 0.12, 58);
                    if (theme == "porcelain") return Oklch(0.65, 0.095, 170);
                    if (theme == "cherry") return Oklch(0.55, 0.18, 12);
                    return Oklch(0.68, 0.17, 29);
                case "Gold": return Oklch(0.84, 0.15, 87);
                case "Ink": return Oklch(0.29, 0.035, 47);
                case "Track": return theme == "dusk" ? Oklch(0.65, 0.04, 65) : Oklch(0.78, 0.06, 78);
                case "StoneMark": return Oklch(0.73, 0.03, 65);
                default: return Oklch(0.66, 0.13, 43);
            }
        }

        private static Color Oklch(double light, double chroma, double hue)
        {
            double a = chroma * Math.Cos(hue * Math.PI / 180);
            double b = chroma * Math.Sin(hue * Math.PI / 180);
            double l = Math.Pow(light + 0.3963377774 * a + 0.2158037573 * b, 3);
            double m = Math.Pow(light - 0.1055613458 * a - 0.0638541728 * b, 3);
            double s = Math.Pow(light - 0.0894841775 * a - 1.2914855480 * b, 3);
            return new Color(Gamma(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s),
                Gamma(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s),
                Gamma(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s));
        }

        private static float Gamma(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            return (float)(value <= 0.0031308 ? value * 12.92 : 1.055 * Math.Pow(value, 1 / 2.4) - 0.055);
        }
    }
}
