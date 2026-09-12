#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace OrbitOrchard.Editor
{
    public static class AppleBuildPostprocessor
    {
        [PostProcessBuild(100)]
        public static void ConfigureAppleServices(BuildTarget target, string exportPath)
        {
            if (target != BuildTarget.iOS) return;
            var projectPath = PBXProject.GetPBXProjectPath(exportPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var appTarget = project.GetUnityMainTargetGuid();
            var frameworkTarget = project.GetUnityFrameworkTargetGuid();
            var source = Path.Combine(Application.dataPath, "OrbitOrchard/Plugins/iOS/Native~");
            var relativeDirectory = "Libraries/OrbitOrchardApple";
            Directory.CreateDirectory(Path.Combine(exportPath, relativeDirectory));

            // Native~ is ignored by Unity's asset importer. Copy explicitly so Swift is
            // compiled exactly once in UnityFramework, irrespective of importer version.
            foreach (var name in new[] { "OrchardStoreService.swift", "OrchardGameCenterService.swift", "OrchardAppleBridge.swift", "OrchardApplePlugin.mm", "OrchardAppController.mm" })
            {
                var relative = relativeDirectory + "/" + name;
                File.Copy(Path.Combine(source, name), Path.Combine(exportPath, relative), true);
                var fileGuid = project.FindFileGuidByProjectPath(relative);
                if (string.IsNullOrEmpty(fileGuid))
                    fileGuid = project.AddFile(relative, relative, PBXSourceTree.Source);
                project.RemoveFileFromBuild(frameworkTarget, fileGuid);
                project.AddFileToBuild(frameworkTarget, fileGuid);
            }

            project.SetBuildProperty(frameworkTarget, "SWIFT_VERSION", "6.0");
            project.SetBuildProperty(frameworkTarget, "CLANG_ENABLE_MODULES", "YES");
            project.SetBuildProperty(frameworkTarget, "DEFINES_MODULE", "YES");
            project.SetBuildProperty(frameworkTarget, "SWIFT_OBJC_INTERFACE_HEADER_NAME", "UnityFramework-Swift.h");
            project.SetBuildProperty(frameworkTarget, "SWIFT_INSTALL_OBJC_HEADER", "YES");
            project.SetBuildProperty(appTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            project.SetBuildProperty(frameworkTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
            foreach (var framework in new[] { "StoreKit.framework", "GameKit.framework", "UIKit.framework", "Foundation.framework" })
                project.AddFrameworkToProject(frameworkTarget, framework, false);
            AddAppPrivacyManifest(project, appTarget, exportPath);
            project.WriteToFile(projectPath);

            var entitlements = project.GetBuildPropertyForAnyConfig(appTarget, "CODE_SIGN_ENTITLEMENTS");
            if (string.IsNullOrEmpty(entitlements)) entitlements = "OrbitOrchard.entitlements";
            var capabilities = new ProjectCapabilityManager(projectPath, entitlements, targetGuid: appTarget);
            capabilities.AddGameCenter();
            capabilities.AddInAppPurchase();
            capabilities.WriteToFile();

            var infoPath = Path.Combine(exportPath, "Info.plist");
            var info = new PlistDocument();
            info.ReadFromFile(infoPath);
            // Seal the app identity explicitly for transport validation before Xcode expands settings.
            info.root.SetString("CFBundleIdentifier", PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS));
            info.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            info.WriteToFile(infoPath);
        }

        private static void AddAppPrivacyManifest(PBXProject project, string appTarget, string exportPath)
        {
            const string relativePath = "PrivacyInfo.xcprivacy";
            var path = Path.Combine(exportPath, relativePath);
            var privacy = new PlistDocument();
            if (File.Exists(path)) privacy.ReadFromFile(path);
            // Merge the application's reason; never replace Unity's bundled declarations.
            if (!privacy.root.values.ContainsKey("NSPrivacyTracking")) privacy.root.SetBoolean("NSPrivacyTracking", false);
            if (!privacy.root.values.ContainsKey("NSPrivacyTrackingDomains")) privacy.root.CreateArray("NSPrivacyTrackingDomains");
            if (!privacy.root.values.ContainsKey("NSPrivacyCollectedDataTypes")) privacy.root.CreateArray("NSPrivacyCollectedDataTypes");
            var accessed = privacy.root.values.TryGetValue("NSPrivacyAccessedAPITypes", out var accessedValue)
                ? accessedValue.AsArray() : privacy.root.CreateArray("NSPrivacyAccessedAPITypes");
            EnsureReason(accessed, "NSPrivacyAccessedAPICategoryUserDefaults", "CA92.1");
            // The clinic bounds reads using file size and persists snapshots in its app container.
            EnsureReason(accessed, "NSPrivacyAccessedAPICategoryFileTimestamp", "C617.1");
            privacy.WriteToFile(path);
            var fileGuid = project.FindFileGuidByProjectPath(relativePath);
            if (string.IsNullOrEmpty(fileGuid)) fileGuid = project.AddFile(relativePath, relativePath, PBXSourceTree.Source);
            project.RemoveFileFromBuild(appTarget, fileGuid);
            project.AddFileToBuild(appTarget, fileGuid);
        }

        private static void EnsureReason(PlistElementArray accessed, string category, string reason)
        {
            PlistElementDict matching = null;
            foreach (var entry in accessed.values)
            {
                var dictionary = entry.AsDict();
                if (dictionary.values.TryGetValue("NSPrivacyAccessedAPIType", out var type) && type.AsString() == category)
                { matching = dictionary; break; }
            }
            if (matching == null)
            {
                matching = accessed.AddDict();
                matching.SetString("NSPrivacyAccessedAPIType", category);
            }
            var reasons = matching.values.TryGetValue("NSPrivacyAccessedAPITypeReasons", out var value)
                ? value.AsArray() : matching.CreateArray("NSPrivacyAccessedAPITypeReasons");
            foreach (var existing in reasons.values) if (existing.AsString() == reason) return;
            reasons.AddString(reason);
        }
    }
}
#endif
