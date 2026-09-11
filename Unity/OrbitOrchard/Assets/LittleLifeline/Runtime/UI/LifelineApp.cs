using System;
using System.Collections.Generic;
using System.Linq;
using LittleLifeline.Core;
using LittleLifeline.Presentation;
using LittleLifeline.Services;
using OrbitOrchard.App;
using OrbitOrchard.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace LittleLifeline.App
{
    /// <summary>Phone interface and lifecycle for the hospital simulation.</summary>
    public sealed partial class LifelineApp : MonoBehaviour
    {
        private enum Page { Hospital, Route, Crew, Depot, Guide, Weekly, Wardrobe }
        private Page page;
        private LifelineSimulation campaign, weekly;
        private LifelineProfile profile;
        private LifelineProfileStore saves;
        private LifelineWorld world;
        private AppleServices apple;
        private VisualElement root, screen, pageBody;
        private Image board;
        private ScrollView activeScroll;
        private Page lastBuiltPage;
        private Label statusLabel;
        private Camera backdrop;
        private Font displayFont, bodyFont, boldFont;
        private AudioSource audioSource;
        private AudioClip careSound, buildSound, tapSound;
        private readonly List<Action> readouts = new List<Action>();
        private bool intro, inactive, weeklyPaused = true, weeklyRecorded;
        private int selectedSlot = -1, selectedCarriageId = -1, roomToArrange = -1, moveTargetSlot = -1, roomToRefit = -1;
        private string shownConfiguration;
        private double uiClock, saveClock;
        private string notice = "", previewLivery = "base";
        private double noticeUntil;
        private LifelineOfflineReport returnReport;
        private static readonly string[] LiveryIds = { "base", "sunrise", "coastal", "heritage" };
        private static readonly string[] LiveryNames = { "Willow green", "Sunrise express", "Coastal linen", "Heritage railway" };

        private SimulationState State => (page == Page.Weekly || page == Page.Crew && crewForWeekly) && weekly != null ? weekly.State : campaign.State;
        private LifelineSimulation Active => (page == Page.Weekly || page == Page.Crew && crewForWeekly) && weekly != null ? weekly : campaign;
        private bool ReducedMotion => profile.preferences.reducedMotion || apple.IsReduceMotionEnabled;
        private string ActiveLivery => profile.preferences.livery == "base" || apple.IsPassOwned ? profile.preferences.livery : "base";

        private void Awake()
        {
            OrchardInput.Initialize(gameObject);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            saves = new LifelineProfileStore(Application.persistentDataPath);
            profile = saves.LoadCampaign(DateTimeOffset.UtcNow);
            campaign = new LifelineSimulation(profile.state);
            returnReport = saves.LastOfflineReport;
            intro = campaign.State.Tick == 0 && campaign.State.TotalCompleted == 0;
            apple = AppleServices.Instance;
            if (apple == null) apple = new GameObject("AppleServices").AddComponent<AppleServices>();
            apple.UseLifelineLeaderboards();
            apple.StateChanged += AppleChanged;
            world = new GameObject("Little Lifeline World").AddComponent<LifelineWorld>();
            world.Initialize();
            backdrop = new GameObject("Lifeline Paper").AddComponent<Camera>();
            backdrop.depth = -100; backdrop.cullingMask = 0;
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = LifelinePalette.Paper;
            gameObject.AddComponent<AudioListener>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.spatialBlend = 0;
            careSound = Resources.Load<AudioClip>("LifelineAudio/care");
            buildSound = Resources.Load<AudioClip>("LifelineAudio/build");
            tapSound = Resources.Load<AudioClip>("LifelineAudio/tap");
        }

        private void Start()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null) doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("LifelinePanel");
            root = doc.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("LifelineStyle"));
            root.AddToClassList("lifeline-root");
            bodyFont = Resources.Load<Font>("Fonts/LifelineBody");
            boldFont = Resources.Load<Font>("Fonts/LifelineBodyBold");
            displayFont = Resources.Load<Font>("Fonts/LifelineDisplay");
            root.style.unityFontDefinition = FontDefinition.FromFont(bodyFont);
            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());
            BuildScreen();
        }

        private void Update()
        {
            if (root == null || inactive) return;
            var delta = (double)Time.unscaledDeltaTime;
            var before = campaign.State.TotalCompleted;
            if (!intro && !saves.HasPendingOfflineProgress)
            {
                var report = campaign.Advance(delta);
                if (report.ProjectsCompleted > 0)
                {
                    SuccessFeedback();
                    Notify("A town project is complete. See what changed on your route.");
                }
            }
            if (page == Page.Weekly && weekly != null && !weeklyPaused && !weekly.State.IsFinished)
            {
                weekly.Advance(delta);
                if (weekly.State.IsFinished) CompleteWeekly();
            }
            if (campaign.State.TotalCompleted > before && page == Page.Hospital)
            {
                Sound(careSound, .18f);
                if (before == 0 && selectedSlot < 0) BuildScreen();
            }
            uiClock += delta; saveClock += delta;
            if (uiClock >= .3)
            {
                uiClock = 0;
                if ((page == Page.Hospital || page == Page.Weekly || page == Page.Crew) && shownConfiguration != ConfigurationKey())
                {
                    var selected = State.Carriages.FirstOrDefault(c => c.Id == selectedCarriageId);
                    if (selected != null) { selectedSlot = selected.Slot; world.Focus(selectedSlot, ReducedMotion); }
                    BuildScreen();
                }
                foreach (var update in readouts.ToArray()) update();
                UpdateStatus();
            }
            if (saveClock >= 5) { saveClock = 0; if (saves.HasPendingOfflineProgress) ResumeHospital(); else SaveNow(); }
            UpdateWorld();
        }

        private void OnApplicationPause(bool paused)
        {
            if (campaign == null) return;
            var wasInactive = inactive;
            inactive = paused;
            if (paused) { weeklyPaused = true; if (!wasInactive) SaveNow(); }
            else if (wasInactive) ResumeHospital();
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && weekly != null) weeklyPaused = true;
            if (focused && apple != null) apple.RetryScores();
        }
        private void OnApplicationQuit() { if (campaign != null && !inactive) SaveNow(); }
        private void OnDestroy()
        {
            if (apple != null) apple.StateChanged -= AppleChanged;
            if (world != null) Destroy(world.gameObject);
            if (backdrop != null) Destroy(backdrop.gameObject);
        }
        private void ResumeHospital()
        {
            returnReport = saves.ApplyOffline(DateTimeOffset.UtcNow);
            profile = saves.Profile;
            campaign = new LifelineSimulation(profile.state);
            if (root != null) BuildScreen();
        }
        private void SaveNow()
        {
            if (saves.HasPendingOfflineProgress) return;
            profile.state = campaign.State;
            saves.Save(profile, DateTimeOffset.UtcNow);
        }
        private void AppleChanged()
        {
            if (root == null) return;
            if (page == Page.Wardrobe || page == Page.Weekly) BuildScreen();
        }
        private void UpdateWorld()
        {
            if (board == null || board.panel == null) { world.gameObject.SetActive(false); return; }
            var bounds = board.worldBound;
            if (bounds.width < 5 || bounds.height < 5 || root.resolvedStyle.width < 1 || root.resolvedStyle.height < 1) return;
            world.gameObject.SetActive(true);
            board.image = world.SetRenderSize(
                Mathf.CeilToInt(bounds.width / root.resolvedStyle.width * Screen.width),
                Mathf.CeilToInt(bounds.height / root.resolvedStyle.height * Screen.height));
            world.SetLivery(page == Page.Wardrobe ? previewLivery : ActiveLivery);
            world.Render(State, ReducedMotion);
            PositionRoomMarkers();
        }
        private void ApplySafeArea()
        {
            if (root == null || Screen.width < 1 || Screen.height < 1) return;
            var safe = Screen.safeArea;
            var width = root.resolvedStyle.width; var height = root.resolvedStyle.height;
            if (float.IsNaN(width) || float.IsNaN(height)) return;
            root.EnableInClassList("compact", height < 850);
            root.style.paddingTop = (Screen.height - safe.yMax) / Screen.height * height;
            root.style.paddingBottom = safe.y / Screen.height * height;
            root.style.paddingLeft = safe.x / Screen.width * width;
            root.style.paddingRight = (Screen.width - safe.xMax) / Screen.width * width;
        }
        private void BuildScreen()
        {
            if (root == null) return;
            var offset = page == lastBuiltPage && activeScroll != null ? activeScroll.scrollOffset : Vector2.zero;
            activeScroll = null; lastBuiltPage = page;
            root.Clear(); readouts.Clear(); roomMarkers.Clear(); board = null;
            screen = Box(root, "lifeline-screen");
            if (page == Page.Guide) Header();
            pageBody = Box(screen, "grow immersive-stage");
            if (page == Page.Guide)
            {
                BuildGuide();
            }
            else
            {
                AddWorld(page == Page.Hospital || page == Page.Weekly);
                board.AddToClassList("immersive-world");
                switch (page)
                {
                    case Page.Hospital: BuildHospitalHUD(); break;
                    case Page.Route: BuildRouteHUD(); break;
                    case Page.Crew: BuildCrewHUD(); break;
                    case Page.Depot: BuildDepotHUD(); break;
                    case Page.Weekly: BuildWeeklyHUD(); break;
                    case Page.Wardrobe: BuildWardrobeHUD(); break;
                }
                BuildGameHeader();
            }
            statusLabel = Text(screen, "", "game-toast");
            if (page == Page.Weekly) statusLabel.style.top = 118;
            UpdateStatus();
            BuildGameNavigation();
            ApplySafeArea();
            shownConfiguration = ConfigurationKey();
            if (activeScroll != null && offset.sqrMagnitude > 0)
            {
                var restoreScroll = activeScroll;
                root.schedule.Execute(() => { if (activeScroll == restoreScroll) restoreScroll.scrollOffset = offset; }).ExecuteLater(1);
            }
            foreach (var update in readouts.ToArray()) update();
        }
        private string ConfigurationKey() => string.Join(";", State.Carriages.Select(c => c.Id + ":" + c.Slot + ":" + c.Kind + ":" + c.Level));
        private void Header()
        {
            var header = Row(screen, "header");
            var title = Box(header, "column grow");
            Text(title, "Little Lifeline", "brand", true);
            LiveText(title, () => page == Page.Weekly ? "THE WEEKLY CALL" : TownName(campaign.State.Town), "destination");
            var funds = Box(header, "column");
            LiveText(funds, () => State.Coins.ToString("N0"), "resource");
            Text(funds, page == Page.Weekly ? "shift budget" : "build funds", "resource-caption");
            Button(header, "···", () => Open(Page.Depot), "round").tooltip = "Open the depot and settings";
        }
        private void Nav(string title, Page target, VisualElement parent)
        {
            var b = Button(parent, title, () => { if (target == Page.Crew) OpenCrew(false); else Open(target); });
            b.EnableInClassList("active", page == target);
        }
        private void Open(Page target)
        {
            if (page == Page.Weekly && target != page) weeklyPaused = true;
            page = target; roomToArrange = -1; roomToRefit = -1;
            var selected = State.Carriages.FirstOrDefault(c => c.Id == selectedCarriageId);
            if (selected != null) selectedSlot = selected.Slot;
            world.Focus(target == Page.Hospital || target == Page.Weekly ? selectedSlot : -1, ReducedMotion);
            if (target == Page.Wardrobe) { previewLivery = ActiveLivery; if (apple.Products.Count == 0 && !apple.IsStoreLoading) apple.LoadProducts(); }
            BuildScreen();
        }
        private void Notify(string message)
        {
            notice = message; noticeUntil = Time.realtimeSinceStartupAsDouble + 7; UpdateStatus();
        }
        private void UpdateStatus()
        {
            if (statusLabel == null) return;
            var message = !string.IsNullOrEmpty(saves.Error) ? saves.Error :
                Time.realtimeSinceStartupAsDouble < noticeUntil ? notice : "";
            statusLabel.text = message;
            statusLabel.style.visibility = string.IsNullOrEmpty(message) ? Visibility.Hidden : Visibility.Visible;
        }
        private void Sound(AudioClip clip, float volume = .3f)
        {
            if (profile.preferences.sound && clip != null) audioSource.PlayOneShot(clip, volume);
        }
        private void SuccessFeedback()
        {
            Sound(buildSound);
            if (profile.preferences.haptics) apple.PlayHaptic(1);
        }
        private VisualElement Box(VisualElement parent, string classes = "")
        {
            var element = new VisualElement();
            foreach (var name in classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) element.AddToClassList(name);
            parent.Add(element); return element;
        }
        private VisualElement Row(VisualElement parent, string classes = "") => Box(parent, "row " + classes);
        private Label Text(VisualElement parent, string value, string classes = "", bool display = false)
        {
            var label = new Label(value);
            foreach (var name in classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) label.AddToClassList(name);
            if (display && displayFont != null) label.style.unityFontDefinition = FontDefinition.FromFont(displayFont);
            parent.Add(label); return label;
        }
        private Label LiveText(VisualElement parent, Func<string> value, string classes = "", bool display = false)
        {
            var label = Text(parent, value(), classes, display);
            readouts.Add(() => { var next = value(); if (label.text != next) label.text = next; });
            return label;
        }
        private Button Button(VisualElement parent, string title, Action action, string classes = "")
        {
            var b = new Button(action) { text = title, name = title };
            foreach (var name in classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) b.AddToClassList(name);
            parent.Add(b); return b;
        }
        private VisualElement PageContent(string title, string description)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            activeScroll = scroll;
            scroll.AddToClassList("page-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            pageBody.Add(scroll);
            var content = Box(scroll, "page-content");
            Text(content, title, "page-title", true);
            if (!string.IsNullOrEmpty(description)) Text(content, description, "paragraph");
            return content;
        }
        private void AddWorld(bool selectable = true)
        {
            board = new Image { scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Position };
            board.AddToClassList("world"); board.EnableInClassList("focused", selectedSlot >= 0);
            pageBody.Add(board);
            if (!selectable) return;
            var sceneImage = board;
            sceneImage.RegisterCallback<PointerDownEvent>(e =>
            {
                if (intro || roomToArrange >= 0) return;
                var p = sceneImage.WorldToLocal(e.position);
                var slot = world.PickCarriage(new Vector2(p.x / sceneImage.resolvedStyle.width, 1 - p.y / sceneImage.resolvedStyle.height));
                if (slot >= 0) SelectCarriage(slot);
            });
        }
        private void SelectCarriage(int slot)
        {
            selectedSlot = slot; roomToArrange = -1; roomToRefit = -1;
            selectedCarriageId = State.Carriages.FirstOrDefault(c => c.Slot == slot)?.Id ?? -1;
            moveTargetSlot = -1;
            world.Focus(slot, ReducedMotion); BuildScreen();
        }
    }
}
