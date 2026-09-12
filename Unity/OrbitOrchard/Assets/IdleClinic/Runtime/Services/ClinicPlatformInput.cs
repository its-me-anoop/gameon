using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace IdleClinic.Services
{
    /// <summary>Device-reported scroll precision, never inferred from a gesture's changing delta.</summary>
    public static class ClinicPlatformInput
    {
        private static bool initialized;
        private static bool nativeAvailable;

        public static bool HasNativeScrollSource => nativeAvailable;
        public static bool IsPreciseScroll
        {
            get
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                Initialize();
                return nativeAvailable && Clinic_IsPreciseScroll() != 0;
#else
                return false;
#endif
            }
        }

        /// <summary>Call while creating the clinic UI, before its first wheel event.</summary>
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            try { nativeAvailable = Clinic_InitializeScrollMonitor() != 0; }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException || error is BadImageFormatException)
            {
                Debug.LogWarning("Clinic native scroll input is unavailable. Use Shift + wheel to pan. " + error.GetType().Name);
            }
#endif
            Application.quitting -= Shutdown;
            Application.quitting += Shutdown;
        }

        public static void Shutdown()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (nativeAvailable) Clinic_ShutdownScrollMonitor();
#endif
            initialized = false;
            nativeAvailable = false;
            Application.quitting -= Shutdown;
        }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        [DllImport("ClinicPlatformInput")] private static extern int Clinic_InitializeScrollMonitor();
        [DllImport("ClinicPlatformInput")] private static extern int Clinic_IsPreciseScroll();
        [DllImport("ClinicPlatformInput")] private static extern void Clinic_ShutdownScrollMonitor();
#endif
    }
}
