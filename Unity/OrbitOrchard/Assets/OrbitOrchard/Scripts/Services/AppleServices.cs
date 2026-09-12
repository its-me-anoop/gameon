using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace OrbitOrchard.Services
{
    [Serializable, Preserve]
    public sealed class AppleProduct
    {
        public string id;
        public string title;
        public string description;
        public string price;
        public bool isConsumable;
    }

    /// <summary>Native Apple APIs behind a Unity-owned game and interface.</summary>
    [Preserve]
    public sealed class AppleServices : MonoBehaviour
    {
        public const string PassProductId = "com.flutterly.gravitile.plus";
        public const string SmallTipProductId = "com.flutterly.gravitile.tip.small";
        public const string MediumTipProductId = "com.flutterly.gravitile.tip.medium";
        public const string LargeTipProductId = "com.flutterly.gravitile.tip.large";
        public const string ClassicLeaderboardId = "grv3.orchard.classic";
        public const string WeeklyLeaderboardId = "grv3.lifeline.weekly.v1";
        public const string DailyLeaderboardId = "grv3.orchard.daily";

        public static AppleServices Instance { get; private set; }
        public event Action StateChanged;

        public bool IsPassOwned { get; private set; }
        public bool IsReduceMotionEnabled { get; private set; }
        public bool IsStoreLoading { get; private set; }
        public bool IsPurchasing { get; private set; }
        public bool IsRestoring { get; private set; }
        public bool IsGameCenterAuthenticated { get; private set; }
        public string PlayerDisplayName { get; private set; } = "";
        public int PendingScoreCount { get; private set; }
        public string Status { get; private set; } = "Apple services are starting.";
        public string StoreStatus { get; private set; } = "";
        public string GameCenterStatus { get; private set; } = "";
        public string ProductState { get; private set; } = "idle";
        public string PurchaseState { get; private set; } = "idle";
        public string GameCenterState { get; private set; } = "signedOut";
        public string DailyAvailability { get; private set; } = "";
        public IReadOnlyList<AppleProduct> Products => products;
        public bool IsAuthenticating => GameCenterState == "authenticating";
        /// <summary>-1 unsupported; iOS 0 nominal, 1 fair, 2 serious, 3 critical.</summary>
        public int ThermalState
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return OO_ThermalState();
#else
                return -1;
#endif
            }
        }
        /// <summary>UIKit window width in points. Refresh when the viewport changes; no DPI guess.</summary>
        public float ScreenWidthPoints
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return OO_ScreenWidthPoints();
#else
                return Mathf.Max(1, Screen.width);
#endif
            }
        }

        private AppleProduct[] products = Array.Empty<AppleProduct>();
        private bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateServices()
        {
            if (Instance == null) new GameObject("AppleServices").AddComponent<AppleServices>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            gameObject.name = "AppleServices";
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
#if UNITY_IOS && !UNITY_EDITOR
            OO_Initialize();
#else
            StoreStatus = "Purchases are available in the iPhone and iPad app.";
            GameCenterStatus = "Game Center is available in the iPhone and iPad app.";
            ProductState = "unavailable";
            SetStatus("Apple services require an iOS build. Local play is available.");
#endif
        }

        public void LoadProducts()
        {
#if UNITY_IOS && !UNITY_EDITOR
            OO_LoadProducts();
#else
            SetStatus(StoreStatus);
#endif
        }

        public void Purchase(string productId)
        {
            if (IsPurchasing || IsRestoring || string.IsNullOrEmpty(productId)) return;
#if UNITY_IOS && !UNITY_EDITOR
            OO_Purchase(productId);
#else
            SetStatus(StoreStatus);
#endif
        }

        public void RestorePurchases()
        {
            if (IsPurchasing || IsRestoring) return;
#if UNITY_IOS && !UNITY_EDITOR
            OO_RestorePurchases();
#else
            SetStatus(StoreStatus);
#endif
        }

        public void AuthenticateGameCenter()
        {
            if (IsAuthenticating) return;
#if UNITY_IOS && !UNITY_EDITOR
            OO_AuthenticateGameCenter();
#else
            SetStatus(GameCenterStatus);
#endif
        }

        public void ShowLeaderboard(bool daily = false)
        {
#if UNITY_IOS && !UNITY_EDITOR
            OO_ShowLeaderboard(daily ? 1 : 0);
#else
            SetStatus(GameCenterStatus);
#endif
        }

        /// <summary>Explicit opt-in to the previous weekly feature. Clinic startup leaves its saved queues untouched.</summary>
        public void UseLifelineLeaderboards()
        {
#if UNITY_IOS && !UNITY_EDITOR
            OO_UseLifelineLeaderboards();
#endif
        }

        public void ShowWeeklyLeaderboard()
        {
#if UNITY_IOS && !UNITY_EDITOR
            OO_ShowWeeklyLeaderboard();
#else
            SetStatus(GameCenterStatus);
#endif
        }

        /// <param name="weekKey">UTC Monday yyyy-MM-dd captured when the challenge starts.</param>
        public void SubmitWeeklyScore(long score, string weekKey)
        {
            if (score <= 0) return;
            if (score > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(score));
            if (string.IsNullOrEmpty(weekKey)) throw new ArgumentException("A challenge week is required.", nameof(weekKey));
#if UNITY_IOS && !UNITY_EDITOR
            OO_SubmitScore((int)score, "lifelineWeekly", weekKey);
#endif
        }

        /// <summary>Legacy entry point never submits scores from the previous game.</summary>
        public void SubmitScore(int score, string mode, string dayKey = null)
        {
            if (mode == "lifelineWeekly") SubmitWeeklyScore(score, dayKey);
        }

        public void RetryScores()
        {
#if UNITY_IOS && !UNITY_EDITOR
            OO_RetryScores();
#endif
        }

        /// <param name="kind">0 for catch, 1 for bank, 2 for miss. Respect the player's haptics setting.</param>
        public void PlayHaptic(int kind)
        {
            if (kind < 0 || kind > 2) return;
#if UNITY_IOS && !UNITY_EDITOR
            OO_Haptic(kind);
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && initialized) RetryScores();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // UnitySendMessage requires exactly a public method with one string parameter.
        [Preserve]
        public void OnNativeMessage(string json)
        {
            NativeState state;
            try { state = JsonUtility.FromJson<NativeState>(json); }
            catch (ArgumentException)
            {
                Debug.LogError("Apple services returned an invalid state message.");
                return;
            }
            if (state == null || state.type != "state") return;
            IsPassOwned = state.isPassOwned;
            IsReduceMotionEnabled = state.isReduceMotionEnabled;
            IsStoreLoading = state.isStoreLoading;
            IsPurchasing = state.isPurchasing;
            IsRestoring = state.isRestoring;
            IsGameCenterAuthenticated = state.isGameCenterAuthenticated;
            PlayerDisplayName = state.playerDisplayName ?? "";
            PendingScoreCount = state.pendingScoreCount;
            ProductState = state.productState ?? "idle";
            PurchaseState = state.purchaseState ?? "idle";
            GameCenterState = state.gameCenterState ?? "signedOut";
            StoreStatus = state.storeStatus ?? "";
            GameCenterStatus = state.gameCenterStatus ?? "";
            DailyAvailability = state.dailyAvailability ?? "";
            products = state.products ?? Array.Empty<AppleProduct>();
            SetStatus(state.status ?? "");
        }

        private void SetStatus(string message)
        {
            Status = message;
            StateChanged?.Invoke();
        }

        [Serializable, Preserve]
        private sealed class NativeState
        {
            public string type;
            public bool isPassOwned;
            public bool isReduceMotionEnabled;
            public bool isStoreLoading;
            public bool isPurchasing;
            public bool isRestoring;
            public string productState;
            public string purchaseState;
            public string storeStatus;
            public bool isGameCenterAuthenticated;
            public string gameCenterState;
            public string playerDisplayName;
            public string gameCenterStatus;
            public int pendingScoreCount;
            public string dailyAvailability;
            public string status;
            public AppleProduct[] products;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern float OO_ScreenWidthPoints();
        [DllImport("__Internal")] private static extern int OO_ThermalState();
        [DllImport("__Internal")] private static extern void OO_Initialize();
        [DllImport("__Internal")] private static extern void OO_LoadProducts();
        [DllImport("__Internal")] private static extern void OO_Purchase(string productID);
        [DllImport("__Internal")] private static extern void OO_RestorePurchases();
        [DllImport("__Internal")] private static extern void OO_AuthenticateGameCenter();
        [DllImport("__Internal")] private static extern void OO_ShowLeaderboard(int daily);
        [DllImport("__Internal")] private static extern void OO_UseLifelineLeaderboards();
        [DllImport("__Internal")] private static extern void OO_ShowWeeklyLeaderboard();
        [DllImport("__Internal")] private static extern void OO_SubmitScore(int score, string mode, string dayKey);
        [DllImport("__Internal")] private static extern void OO_RetryScores();
        [DllImport("__Internal")] private static extern void OO_Haptic(int kind);
#endif
    }
}
