using System;
using System.IO;
using System.Diagnostics;
using IdleClinic.App;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace OrbitOrchard.Editor
{
    public static class OrchardBuild
    {
        private const string ScenePath = "Assets/IdleClinic/Scenes/Clinic.unity";
        [MenuItem("Idle Clinic/Prepare project")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/IdleClinic/Scenes");
            Directory.CreateDirectory("Assets/IdleClinic/Resources");
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/IdleClinic/Resources/ClinicPanel.asset");
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(430, 932);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = .5f;
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/OrbitOrchard/UI/OrchardTheme.tss");
                AssetDatabase.CreateAsset(panel, "Assets/IdleClinic/Resources/ClinicPanel.asset");
            }
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var app = new GameObject("Little Lifeline");
                var doc = app.AddComponent<UIDocument>(); doc.panelSettings = panel;
                app.AddComponent<ClinicApp>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            PlayerSettings.companyName = "Flutterly";
            PlayerSettings.productName = "Little Lifeline";
            PlayerSettings.bundleVersion = "3.2";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.flutterly.gravitile");
            PlayerSettings.iOS.buildNumber = "16";
            PlayerSettings.iOS.targetOSVersionString = "18.0";
            PlayerSettings.iOS.appleDeveloperTeamID = "K6623R3GP5";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SplashScreen.show = false;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowDistance = 24;
            IncludeRuntimeShaders();
            const string iconPath = "Assets/IdleClinic/Art/AppIcon.png";
            var iconImporter = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (iconImporter != null && (iconImporter.textureCompression != TextureImporterCompression.Uncompressed || iconImporter.mipmapEnabled))
            {
                iconImporter.textureCompression = TextureImporterCompression.Uncompressed;
                iconImporter.mipmapEnabled = false;
                iconImporter.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("Idle Clinic project prepared.");
        }
        [MenuItem("Idle Clinic/Build iOS")]
        public static void BuildIOS()
        {
            Prepare();
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            var output = Environment.GetEnvironmentVariable("ORCHARD_IOS_EXPORT");
            if (string.IsNullOrEmpty(output)) output = "Builds/iOS";
            WriteExportProvenance(output, false, "device", false);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.iOS, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Idle Clinic iOS export failed: " + report.summary.result);
            WriteExportProvenance(output, false, "device", true);
            Debug.Log("Idle Clinic iOS export succeeded: " + output);
        }
        [MenuItem("Idle Clinic/Build iOS Development diagnostics")]
        public static void BuildIOSDevelopment()
        {
            Prepare();
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            var output = Environment.GetEnvironmentVariable("ORCHARD_IOS_EXPORT");
            if (string.IsNullOrEmpty(output)) output = "Builds/iOSClinicDevelopment";
            WriteExportProvenance(output, true, "device", false);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.iOS, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Idle Clinic development export failed: " + report.summary.result);
            WriteExportProvenance(output, true, "device", true);
            Debug.Log("Idle Clinic development export succeeded (not for TestFlight): " + output);
        }
        [MenuItem("Idle Clinic/Build iOS Simulator")]
        public static void BuildIOSSimulator()
        {
            Prepare();
            var previousArchitecture = PlayerSettings.iOS.simulatorSdkArchitecture;
            try
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
                var output = Environment.GetEnvironmentVariable("ORCHARD_IOS_EXPORT");
                if (string.IsNullOrEmpty(output)) output = "Builds/iOSSimulator";
                WriteExportProvenance(output, true, "simulator", false);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = output,
                    target = BuildTarget.iOS, options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Idle Clinic iOS simulator export failed: " + report.summary.result);
                WriteExportProvenance(output, true, "simulator", true);
                Debug.Log("Idle Clinic ARM64 iOS simulator export succeeded: " + output);
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = previousArchitecture;
                AssetDatabase.SaveAssets();
            }
        }
        [Serializable] private sealed class ExportProvenance
        {
            public string product, scenePath, sourceCommit, unityVersion, iosSdk;
            public bool sourceDirty, buildSucceeded, developmentBuild;
        }
        private static void WriteExportProvenance(string output, bool development, string sdk, bool succeeded)
        {
            // Invalidate any previous success before reusing an export directory.
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "orbit-orchard-unity-build.json"), JsonUtility.ToJson(new ExportProvenance
            {
                product = "idle-clinic", scenePath = ScenePath,
                sourceCommit = Git("rev-parse HEAD"),
                sourceDirty = !string.IsNullOrEmpty(Git("status --porcelain --untracked-files=all -- .")),
                unityVersion = Application.unityVersion, iosSdk = sdk,
                buildSucceeded = succeeded, developmentBuild = development
            }, true));
        }
        private static string Git(string arguments)
        {
            using (var process = Process.Start(new ProcessStartInfo("/usr/bin/git", arguments)
            { WorkingDirectory = Directory.GetCurrentDirectory(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false }))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd(); process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException("Cannot establish export provenance: " + error);
                return output.Trim();
            }
        }
        private static void IncludeRuntimeShaders()
        {
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var included = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "Standard", "Unlit/Color" })
            {
                var shader = Shader.Find(name);
                if (shader == null) throw new BuildFailedException("Missing runtime shader: " + name);
                var exists = false;
                for (var i = 0; i < included.arraySize; i++) if (included.GetArrayElementAtIndex(i).objectReferenceValue == shader) exists = true;
                if (!exists) { var index = included.arraySize; included.InsertArrayElementAtIndex(index); included.GetArrayElementAtIndex(index).objectReferenceValue = shader; }
            }
            // Materials are instantiated by the world at runtime, so scene scanning
            // cannot discover their GPU instancing variants.
            var instancing = graphics.FindProperty("m_InstancingStripping");
            if (instancing != null) instancing.intValue = 2; // Unity's InstancingStrippingMode.KeepAll.
            graphics.ApplyModifiedPropertiesWithoutUndo();
        }
        [MenuItem("Idle Clinic/Open game scene")]
        public static void OpenGame() { Prepare(); EditorSceneManager.OpenScene(ScenePath); }
    }
}
