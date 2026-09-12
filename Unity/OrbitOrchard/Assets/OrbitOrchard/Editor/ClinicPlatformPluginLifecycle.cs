#if UNITY_EDITOR
using IdleClinic.Services;
using UnityEditor;

namespace OrbitOrchard.Editor
{
    [InitializeOnLoad]
    internal static class ClinicPlatformPluginLifecycle
    {
        static ClinicPlatformPluginLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClinicPlatformInput.Shutdown;
            EditorApplication.quitting += ClinicPlatformInput.Shutdown;
        }
    }
}
#endif
