using System;
using System.Linq;
using OrbitOrchard.Core;
using OrbitOrchard.Presentation;
using OrbitOrchard.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace OrbitOrchard.App
{
    /// <summary>Owns the run lifecycle. Rendering and Apple transactions remain separate services.</summary>
    public sealed class OrchardApp : MonoBehaviour
    {
        private enum Page { Home, Play, Result, Guide, Worlds, Garden, Rankings }
        private OrchardGame game;
        private OrchardProfile profile;
        private ProfileStore saves;
        private OrchardWorld world;
        private AppleServices apple;
        private Camera paperCamera;
        private VisualElement root, body;
        private Image board;
        private ScrollView scroll;
        private Label scoreLabel, basketLabel, multiplierLabel, timeLabel, heartsLabel, feedbackLabel;
        private Label saveStatus;
        private int shownScore = -1, shownCarried = -1, shownMultiplier = -1, shownSeconds = -1, shownHearts = -1;
        private string shownFeedback;
        private bool shownPaused;
        private Button bankButton;
        private Page page = Page.Home;
        private Page returnPage = Page.Home;
        private bool paused, newBest, pointerHeld;
        private string runDay, feedback = "Catch a little. Grow a lot.", previewTheme, renderedTheme;
        private double feedbackUntil;
        private AudioSource audioSource;
        private AudioClip catchSound, bankSound, missSound;
        private int pointerId;
        private static readonly string[] Themes = { "orchard", "dusk", "porcelain", "cherry" };
        private static readonly string[] ThemeNames = { "Terracotta", "Afterglow", "Porcelain", "Cherry milk" };
        private static readonly string[] ThemeDetails = { "Sun-warmed clay & wild leaves", "Apricot blooms after sundown", "Soft jade & ceramic flowers", "Berry clay & strawberry skies" };

        private void Awake()
        {
            OrchardInput.Initialize(gameObject);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            saves = new ProfileStore(Application.persistentDataPath);
            profile = saves.Load();
            game = new OrchardGame(42);
            apple = AppleServices.Instance;
            if (apple == null) apple = new GameObject("AppleServices").AddComponent<AppleServices>();
            apple.StateChanged += OnServiceChanged;
            apple.Initialize();
            world = new GameObject("Orchard World").AddComponent<OrchardWorld>();
            world.Initialize();
            paperCamera = new GameObject("Paper Background").AddComponent<Camera>();
            paperCamera.depth = -100;
            paperCamera.cullingMask = 0;
            paperCamera.clearFlags = CameraClearFlags.SolidColor;
            audioSource = gameObject.AddComponent<AudioSource>();
            gameObject.AddComponent<AudioListener>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            catchSound = Resources.Load<AudioClip>("Audio/catch");
            bankSound = Resources.Load<AudioClip>("Audio/upgrade");
            missSound = Resources.Load<AudioClip>("Audio/denied");
        }

        private void Start()
        {
            var document = GetComponent<UIDocument>();
            if (document == null) document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("OrchardPanel");
            root = document.rootVisualElement;
            var style = Resources.Load<StyleSheet>("OrchardStyle");
            if (style != null) root.styleSheets.Add(style);
            root.AddToClassList("root");
            var bodyFont = Resources.Load<Font>("Fonts/OrchardBody");
            if (bodyFont != null) root.style.unityFontDefinition = FontDefinition.FromFont(bodyFont);
            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());
            BuildScreen();
        }
        private void OnDestroy()
        {
            if (apple != null) apple.StateChanged -= OnServiceChanged;
            if (world != null) Destroy(world.gameObject);
            if (paperCamera != null) Destroy(paperCamera.gameObject);
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }
        private void OnApplicationPause(bool inactive) { if (inactive && page == Page.Play) SetPaused(true); }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && page == Page.Play) SetPaused(true);
            if (focused && apple != null) apple.RetryScores();
        }
        private void Update()
        {
            if (root == null) return;
            if (page == Page.Play && !paused)
            {
                if (Input.GetKey(KeyCode.LeftArrow)) game.Steer(game.CatcherAngle - Time.unscaledDeltaTime * 2.8);
                if (Input.GetKey(KeyCode.RightArrow)) game.Steer(game.CatcherAngle + Time.unscaledDeltaTime * 2.8);
                if (Input.GetKeyDown(KeyCode.Space)) Bank();
                game.Update(Math.Min(.1, Time.unscaledDeltaTime));
                ConsumeEvents();
                if (game.Elapsed > feedbackUntil) feedback = game.Carried > 0 ? "Bank your seeds — or risk them for a bigger harvest" : "Catch leaves. Dodge stones.";
                UpdateReadouts();
                if (game.IsFinished) FinishRun();
            }
            UpdateViewport();
            if (board != null)
            {
                var seeds = page == Page.Home ? Math.Max(12, Math.Min(36, profile.totalSeeds)) : page == Page.Garden ? profile.totalSeeds : page == Page.Worlds ? 16 : game.BankedSeeds;
                world.Render(game, seeds, page == Page.Worlds ? previewTheme : ActiveTheme, profile.reducedMotion || apple.IsReduceMotionEnabled, page != Page.Play);
            }
        }
        private string ActiveTheme => profile.theme == "orchard" || apple.IsPassOwned ? profile.theme : "orchard";
        private bool AppleFlowIsOpening => apple.IsAuthenticating || apple.IsPurchasing || apple.IsRestoring;
        private Color Paper => ColorFor(ActiveTheme, "paper");
        private void ApplySafeArea()
        {
            if (root == null || Screen.width == 0 || Screen.height == 0) return;
            var safe = Screen.safeArea;
            var w = root.resolvedStyle.width;
            var h = root.resolvedStyle.height;
            root.EnableInClassList("compact", h < 850);
            root.style.paddingTop = (Screen.height - safe.yMax) / Screen.height * h;
            root.style.paddingBottom = safe.y / Screen.height * h;
            root.style.paddingLeft = safe.x / Screen.width * w;
            root.style.paddingRight = (Screen.width - safe.xMax) / Screen.width * w;
        }
        private void UpdateViewport()
        {
            if (board == null || board.panel == null) { world.gameObject.SetActive(false); return; }
            var bound = board.worldBound;
            var rw = root.resolvedStyle.width;
            var rh = root.resolvedStyle.height;
            if (rw <= 0 || rh <= 0 || bound.height < 5) return;
            var visible = bound.yMax > 0 && bound.yMin < rh && bound.width > 0;
            world.gameObject.SetActive(visible);
            // A texture stays inside UI Toolkit's clipping hierarchy, including scrolling
            // and the pause overlay. A screen camera rect would paint over that hierarchy.
            if (visible) board.image = world.SetRenderSize(Mathf.CeilToInt(bound.width / rw * Screen.width), Mathf.CeilToInt(bound.height / rh * Screen.height));
        }
        private void BuildScreen()
        {
            if (root == null) return;
            root.Clear(); board = null; scroll = null;
            scoreLabel = null;
            shownScore = shownCarried = shownMultiplier = shownSeconds = shownHearts = -1;
            shownFeedback = null;
            root.EnableInClassList("night", ActiveTheme == "dusk");
            renderedTheme = ActiveTheme;
            root.style.color = ColorFor(ActiveTheme, "ink");
            paperCamera.backgroundColor = Paper;
            body = new VisualElement { name = "screen" };
            body.AddToClassList("screen"); root.Add(body);
            switch (page)
            {
                case Page.Home: Home(); break;
                case Page.Play: Play(); break;
                case Page.Result: Results(); break;
                case Page.Guide: Guide(); break;
                case Page.Worlds: Worlds(); break;
                case Page.Garden: Garden(); break;
                case Page.Rankings: Rankings(); break;
            }
            if (paused && page == Page.Play) PauseOverlay();
            saveStatus = Label(saves.Error, "note", body);
            saveStatus.style.display = string.IsNullOrEmpty(saves.Error) ? DisplayStyle.None : DisplayStyle.Flex;
            ApplySafeArea();
        }
        private void Home()
        {
            var header = Row(body); Label("GRAVITILE", "eyebrow", header); Spacer(header); Button("?", () => Open(Page.Guide), header, "round");
            Label("Orbit\nOrchard.", "title", body);
            Label("A tiny world. A wild little harvest.", "subtitle", body);
            AddBoard(body, "hero");
            var caption = Row(body); Label("CATCH. RISK. BLOOM.", "eyebrow", caption); Spacer(caption);
            if (profile.best > 0) Label("BEST " + profile.best, "eyebrow", caption);
            Button("Let’s grow  ↗", () => StartRun(GameMode.Classic), body, "primary");
            Button("The daily harvest  →\n" + (profile.DailyBest(DailySeed.Identifier(DateTimeOffset.UtcNow)) > 0 ? "Your best today: " + profile.DailyBest(DailySeed.Identifier(DateTimeOffset.UtcNow)) : "Same seeds. One shared challenge."), () => StartRun(GameMode.Daily), body, "daily");
            var nav = Row(body); nav.AddToClassList("nav");
            Button("Rankings", () => Open(Page.Rankings), nav, "nav-button");
            Button("Worlds", () => Open(Page.Worlds), nav, "nav-button");
            Button("Your garden", () => Open(Page.Garden), nav, "nav-button");
        }
        private void Play()
        {
            var hud = Row(body); var left = Column(hud);
            Label(game.Mode == GameMode.Daily ? "DAILY HARVEST" : game.Mode == GameMode.Practice ? "FREE GROW" : "THE HARVEST", "eyebrow", left);
            scoreLabel = Label("0", "score", left); Spacer(hud); var right = Column(hud);
            var clockRow = Row(right); timeLabel = Label("1:30", "clock", clockRow); Button("Ⅱ", () => SetPaused(true), clockRow, "round");
            heartsLabel = Label("♥ ♥ ♥", "hearts", right);
            AddBoard(body, "playfield");
            var playBoard = board;
            playBoard.RegisterCallback<PointerDownEvent>(e => { if (paused || pointerHeld) return; pointerHeld = true; pointerId = e.pointerId; playBoard.CapturePointer(e.pointerId); SteerAt(e.position); e.StopPropagation(); });
            playBoard.RegisterCallback<PointerMoveEvent>(e => { if (pointerHeld && e.pointerId == pointerId) SteerAt(e.position); });
            playBoard.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId != pointerId) return; pointerHeld = false; playBoard.ReleasePointer(e.pointerId); });
            playBoard.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId != pointerId) return; pointerHeld = false; playBoard.ReleasePointer(e.pointerId); });
            playBoard.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == pointerId) pointerHeld = false; });
            feedbackLabel = Label(feedback, "feedback", body);
            var basket = Row(body); var basketText = Column(basket); Label("IN YOUR BASKET", "eyebrow", basketText);
            basketLabel = Label("0 seeds", "basket", basketText); Spacer(basket); multiplierLabel = Label("×1", "multiplier", basket);
            var controls = Row(body); controls.AddToClassList("controls");
            Button("↶", () => Move(-1), controls, "steer"); bankButton = Button("Plant & bank\nCatch a seed first", Bank, controls, "bank primary"); Button("↷", () => Move(1), controls, "steer");
            Label("Slide around the world · Bank before you spill", "footnote", body);
            UpdateReadouts();
        }
        private void StartRun(GameMode mode)
        {
            if (AppleFlowIsOpening)
            {
                if (saveStatus != null)
                {
                    saveStatus.text = "Finish the Apple account window, then start your harvest.";
                    saveStatus.style.display = DisplayStyle.Flex;
                }
                return;
            }
            var now = DateTimeOffset.UtcNow; runDay = DailySeed.Identifier(now);
            var seed = mode == GameMode.Daily ? DailySeed.ForDate(now) : BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
            game = new OrchardGame(seed, mode); page = Page.Play; paused = false; newBest = false; pointerHeld = false;
            feedback = "Slide around the orbit to catch green seeds"; feedbackUntil = 8;
            Screen.sleepTimeout = SleepTimeout.NeverSleep; BuildScreen();
        }
        private void SteerAt(Vector3 panelPoint)
        {
            if (page != Page.Play || paused || board == null || board.resolvedStyle.width <= 0 || board.resolvedStyle.height <= 0) return;
            var point = board.WorldToLocal(panelPoint);
            var uv = new Vector2(point.x / board.resolvedStyle.width, 1 - point.y / board.resolvedStyle.height);
            if (world.ViewportPointToAngle(uv, out var angle)) game.Steer(angle);
        }
        private void Move(int direction) { if (page == Page.Play && !paused) game.Steer(game.CatcherAngle + direction * Math.PI / 10); }
        private void Bank() { if (page != Page.Play || paused || game.Carried == 0) return; game.Bank(); ConsumeEvents(); UpdateReadouts(); }
        private void ConsumeEvents()
        {
            foreach (var e in game.DrainEvents())
            {
                switch (e.Kind)
                {
                    case GameEventKind.Caught: feedback = e.Body?.Kind == SeedKind.GoldenSeed ? "Golden seed! A little extra sunshine." : "Nice catch · ×" + game.Multiplier + " harvest"; Sound(catchSound); Haptic(0); break;
                    case GameEventKind.Banked: feedback = "+" + e.Amount + " planted. Your orchard is growing."; Sound(bankSound); Haptic(1); break;
                    case GameEventKind.Missed: feedback = "Slipped away. Meet the seed at the ring."; Sound(missSound); Haptic(2); break;
                    case GameEventKind.Hit: feedback = "Ouch. Watch the dark stones."; Sound(missSound); Haptic(2); break;
                }
                feedbackUntil = game.Elapsed + 3;
            }
        }
        private void Sound(AudioClip clip) { if (profile.sound && clip != null) audioSource.PlayOneShot(clip, .45f); }
        private void Haptic(int kind) { if (profile.haptics) apple.PlayHaptic(kind); }
        private void UpdateReadouts()
        {
            if (scoreLabel == null) return;
            var seconds = game.Mode == GameMode.Practice ? 0 : (int)Math.Ceiling(game.TimeRemaining);
            if (shownScore == game.Score && shownCarried == game.Carried && shownMultiplier == game.Multiplier
                && shownSeconds == seconds && shownHearts == game.Hearts && shownFeedback == feedback && shownPaused == paused) return;
            shownScore = game.Score; shownCarried = game.Carried; shownMultiplier = game.Multiplier;
            shownSeconds = seconds; shownHearts = game.Hearts; shownFeedback = feedback; shownPaused = paused;
            scoreLabel.text = game.Score.ToString(); basketLabel.text = game.Carried + " seeds";
            multiplierLabel.text = "×" + game.Multiplier;
            timeLabel.text = game.Mode == GameMode.Practice ? "∞" : (seconds / 60) + ":" + (seconds % 60).ToString("00");
            heartsLabel.text = string.Join(" ", Enumerable.Range(0, 3).Select(i => i < game.Hearts ? "♥" : "♡"));
            bankButton.text = "Plant & bank\n" + (game.Carried > 0 ? "+" + game.BasketValue + " points" : "Catch a seed first");
            bankButton.SetEnabled(game.Carried > 0 && !paused); feedbackLabel.text = feedback;
        }
        private void SetPaused(bool value) { paused = value; pointerHeld = false; if (root != null) BuildScreen(); }
        private void PauseOverlay()
        {
            var overlay = new VisualElement(); overlay.AddToClassList("overlay"); overlay.style.backgroundColor = Paper; root.Add(overlay);
            Label("Take a\nlittle breath.", "title", overlay); Label("Your orbit is paused.", "subtitle", overlay);
            Button("Keep growing  →", () => SetPaused(false), overlay, "primary");
            Button("How to play", () => Open(Page.Guide), overlay, "text-button");
            if (game.Mode == GameMode.Practice) Button("Finish practice", () => { game.Bank(); ConsumeEvents(); FinishRun(); }, overlay, "text-button");
            else { Button("Leave this harvest", HomeAgain, overlay, "text-button"); Label("This run won’t be saved.", "note", overlay); }
        }
        private void FinishRun()
        {
            if (page != Page.Play) return;
            if (game.Mode != GameMode.Practice)
            {
                newBest = game.Score > (game.Mode == GameMode.Daily ? profile.DailyBest(runDay) : profile.best);
                profile.Record(new HarvestRecord { score = game.Score, seeds = game.BankedSeeds, seconds = game.Elapsed, day = runDay }, game.Mode == GameMode.Daily);
                saves.Save(profile); apple.SubmitScore(game.Score, game.Mode == GameMode.Daily ? "daily" : "classic", runDay);
            }
            page = Page.Result; paused = false; Screen.sleepTimeout = SleepTimeout.SystemSetting; BuildScreen();
        }
        private void Results()
        {
            Header(game.Mode == GameMode.Practice ? "A MOMENT IN THE GARDEN" : "HARVEST COMPLETE", HomeAgain);
            Label(game.BankedSeeds > 0 ? "Look what\nyou grew." : "Every garden\nstarts somewhere.", "title smaller", body);
            AddBoard(body, "hero");
            var line = Row(body); Label(game.Score.ToString(), "result-score", line); Spacer(line);
            var detail = Column(line); Label(newBest ? "PERSONAL BEST" : game.BankedSeeds + " SEEDS PLANTED", "eyebrow", detail);
            Label(game.Mode == GameMode.Practice ? "Practice · unranked" : ((int)game.Elapsed) + " seconds in orbit", "note", detail);
            Label(game.Score == 0 ? "Catch a seed, then tap Plant & bank. Even one seed is a start." : "A longer streak grows your multiplier. Banking keeps your points safe.", "paragraph", body);
            Button("One more harvest  ↻", () => StartRun(game.Mode), body, "primary");
            Button("Back to garden", HomeAgain, body, "text-button");
        }
        private void Open(Page destination) { returnPage = page; page = destination; previewTheme = ActiveTheme; BuildScreen(); }
        private void ClosePage() { page = returnPage == Page.Play ? Page.Play : Page.Home; BuildScreen(); }
        private void HomeAgain() { page = Page.Home; paused = false; Screen.sleepTimeout = SleepTimeout.SystemSetting; BuildScreen(); }
        private void BeginScroll(string title)
        {
            Header("ORBIT ORCHARD", ClosePage); Label(title, "title smaller", body);
            scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("page-scroll"); body.Add(scroll);
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            body = scroll.contentContainer; body.AddToClassList("scroll-content");
        }
        private void Guide()
        {
            BeginScroll("A little\nfield guide.");
            Instruction("01", "Meet the seed.", "Slide your finger around the world. Your gardener follows the orbit. Catch green seeds when they reach the ring.");
            Instruction("02", "Let it ride.", "Five catches in a row raise your harvest multiplier. Golden seeds are worth three. Keep a streak, then bank it.");
            Instruction("03", "Plant before you spill.", "Tap Plant & bank to turn seeds into points and flowers. A missed seed or a dark stone costs a heart and spills your basket.");
            Label("Three hearts. Ninety seconds. Banked points are safe. At the bell, everything left in the basket is planted.", "paragraph", body);
            Button("Try Free Grow  ↗", () => StartRun(GameMode.Practice), body, "primary");
            Label("No timer, no lost hearts, no rankings. Just room to learn.", "note", body);
        }
        private void Worlds()
        {
            BeginScroll("A world\nof your own."); Label("Same little game. A different kind of day.", "paragraph", body); AddBoard(body, "shop-world");
            for (int i = 0; i < Themes.Length; i++)
            {
                var id = Themes[i];
                Button(ThemeNames[i] + (id == ActiveTheme ? "  ✓" : id != "orchard" && !apple.IsPassOwned ? "  · Pass" : "") + "\n" + ThemeDetails[i], () => { previewTheme = id; if (id == "orchard" || apple.IsPassOwned) { profile.theme = id; saves.Save(profile); } BuildScreen(); previewTheme = id; }, body, "theme-option");
            }
            Label(apple.IsPassOwned ? "Your Orchard Pass." : "Orchard Pass", "section-title", body);
            Label("Afterglow, Porcelain and Cherry milk. One purchase. No ads, subscriptions or paid score boosts.", "paragraph", body);
            var pass = apple.Products.FirstOrDefault(p => p.id == AppleServices.PassProductId);
            if (!apple.IsPassOwned)
            {
                if (pass != null) Button("Unlock worlds  ·  " + pass.price, () => apple.Purchase(pass.id), body, "primary").SetEnabled(!apple.IsPurchasing);
                else Button(apple.IsStoreLoading ? "Connecting to the shop…" : "Retry the shop", apple.LoadProducts, body, "text-button");
            }
            Button(apple.IsRestoring ? "Restoring…" : "Restore purchases", apple.RestorePurchases, body, "text-button").SetEnabled(!apple.IsRestoring && !apple.IsPurchasing);
            Label(apple.Status, "note", body);
            var tips = apple.Products.Where(p => p.isConsumable).ToArray();
            if (tips.Length > 0)
            {
                Label("A little sunshine", "section-title", body); Label("Optional tips support the garden. They don’t unlock content.", "paragraph", body);
                foreach (var tip in tips) { var item = tip; Button(item.title + " · " + item.price, () => apple.Purchase(item.id), body, "text-button").SetEnabled(!apple.IsPurchasing); }
            }
        }
        private void Garden()
        {
            BeginScroll("Your patch\nof the universe."); AddBoard(body, "shop-world");
            Label(profile.totalSeeds + " seeds planted", "section-title", body);
            Label(profile.totalSeeds == 0 ? "Your first flower is one harvest away. Catch a seed and bank it to start growing." : "Every seed banked in a classic or daily harvest becomes part of your garden.", "paragraph", body);
            Label("Classic harvests  " + profile.classicRuns + "\nPersonal best  " + profile.best, "paragraph", body);
            Toggle("Sounds", profile.sound, value => profile.sound = value);
            Toggle("Haptics", profile.haptics, value => profile.haptics = value);
            Toggle("Less movement", profile.reducedMotion, value => profile.reducedMotion = value);
            if (apple.IsReduceMotionEnabled) Label("Reduce Motion is also enabled in your device settings.", "note", body);
            Label("Made for little breaks.", "section-title", body);
            Label("Gravitile · Orbit Orchard · 3.0\nNo advertising or tracking SDKs. Progress is saved on this device. Game Center and purchases use your Apple account.", "note", body);
        }
        private void Rankings()
        {
            BeginScroll("Good things\ngrow together.");
            Label("A fair little competition. Everyone gets the same tools; different worlds only change the scenery.", "paragraph", body);
            Label("All-time harvest  " + profile.best, "section-title", body); Label("Your best 90-second run", "note", body);
            if (apple.IsGameCenterAuthenticated) Button("View classic leaderboard  →", () => apple.ShowLeaderboard(false), body, "text-button");
            Label("Today’s harvest  " + profile.DailyBest(DailySeed.Identifier(DateTimeOffset.UtcNow)), "section-title", body);
            Label("A shared seed pattern · resets at 00:00 UTC", "note", body);
            if (apple.IsGameCenterAuthenticated) Button("View daily leaderboard  →", () => apple.ShowLeaderboard(true), body, "text-button");
            else Button("Connect Game Center", apple.AuthenticateGameCenter, body, "primary");
            Label(apple.Status, "paragraph", body);
            Label("Your personal bests stay on this device. Connect Game Center to share your score and see other growers.", "note", body);
            if (apple.PendingScoreCount > 0) Label(apple.PendingScoreCount + " score updates waiting to sync.", "note", body);
        }
        private void OnServiceChanged()
        {
            if (root == null) return;
            // UIKit sheets do not always change Unity's application focus.
            if (page == Page.Play && !paused && AppleFlowIsOpening) { SetPaused(true); return; }
            if (page == Page.Worlds || page == Page.Rankings || page == Page.Garden || renderedTheme != ActiveTheme)
            {
                var offset = scroll?.scrollOffset ?? Vector2.zero; BuildScreen();
                scroll?.schedule.Execute(() => scroll.scrollOffset = offset);
            }
        }
        private void Header(string text, Action close) { var row = Row(body); Label(text, "eyebrow", row); Spacer(row); Button("×", close, row, "round"); }
        private void AddBoard(VisualElement parent, string css) { board = new Image { name = "orbit-board", scaleMode = ScaleMode.StretchToFill }; board.AddToClassList(css); parent.Add(board); }
        private void Instruction(string n, string title, string text) { Label(n, "eyebrow", body); Label(title, "section-title", body); Label(text, "paragraph", body); }
        private void Toggle(string text, bool value, Action<bool> change)
        {
            var toggle = new Toggle(text) { value = value }; toggle.AddToClassList("preference"); body.Add(toggle);
            toggle.RegisterValueChangedCallback(e =>
            {
                change(e.newValue); saves.Save(profile);
                if (saveStatus == null) return;
                saveStatus.text = saves.Error ?? "";
                saveStatus.style.display = string.IsNullOrEmpty(saves.Error) ? DisplayStyle.None : DisplayStyle.Flex;
            });
        }
        private Label Label(string text, string classes, VisualElement parent)
        {
            var label = new Label(text ?? ""); foreach (var c in classes.Split(' ')) label.AddToClassList(c); parent.Add(label);
            if (classes.Contains("title")) { var font = Resources.Load<Font>("Fonts/OrchardDisplay"); if (font != null) label.style.unityFontDefinition = FontDefinition.FromFont(font); }
            return label;
        }
        private Button Button(string text, Action action, VisualElement parent, string classes)
        {
            var button = new Button(action) { text = text }; foreach (var c in classes.Split(' ')) button.AddToClassList(c); parent.Add(button); return button;
        }
        private static VisualElement Row(VisualElement parent) { var e = new VisualElement(); e.AddToClassList("row"); parent.Add(e); return e; }
        private static VisualElement Column(VisualElement parent) { var e = new VisualElement(); e.AddToClassList("column"); parent.Add(e); return e; }
        private static void Spacer(VisualElement parent) { var e = new VisualElement(); e.style.flexGrow = 1; parent.Add(e); }
        public static Color ColorFor(string theme, string role)
        {
            if (role == "ink") return theme == "dusk" ? OrchardColors.FromOKLCH(.95,.024,75) : OrchardColors.FromOKLCH(.27,.035,45);
            if (theme == "dusk") return OrchardColors.FromOKLCH(.22,.03,330);
            if (theme == "porcelain") return OrchardColors.FromOKLCH(.96,.015,155);
            if (theme == "cherry") return OrchardColors.FromOKLCH(.95,.025,15);
            return OrchardColors.FromOKLCH(.96,.025,90);
        }
    }
}
