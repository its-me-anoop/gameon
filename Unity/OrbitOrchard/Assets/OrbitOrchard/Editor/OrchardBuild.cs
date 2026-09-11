using System;
using System.IO;
using System.Diagnostics;
using LittleLifeline.App;
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
        private const string ScenePath = "Assets/LittleLifeline/Scenes/Lifeline.unity";
        [MenuItem("Little Lifeline/Prepare project")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/LittleLifeline/Scenes");
            Directory.CreateDirectory("Assets/LittleLifeline/Resources");
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/LittleLifeline/Resources/LifelinePanel.asset");
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(430, 932);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = .5f;
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/OrbitOrchard/UI/OrchardTheme.tss");
                AssetDatabase.CreateAsset(panel, "Assets/LittleLifeline/Resources/LifelinePanel.asset");
            }
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var app = new GameObject("Little Lifeline");
                var doc = app.AddComponent<UIDocument>(); doc.panelSettings = panel;
                app.AddComponent<LifelineApp>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            PlayerSettings.companyName = "Flutterly";
            PlayerSettings.productName = "Little Lifeline";
            PlayerSettings.bundleVersion = "3.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.flutterly.gravitile");
            PlayerSettings.iOS.buildNumber = "14";
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
            const string iconPath = "Assets/LittleLifeline/Art/AppIcon.png";
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
            Debug.Log("Little Lifeline project prepared.");
        }
        [MenuItem("Little Lifeline/Build iOS")]
        public static void BuildIOS()
        {
            Prepare();
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            var output = Environment.GetEnvironmentVariable("ORCHARD_IOS_EXPORT");
            if (string.IsNullOrEmpty(output)) output = "Builds/iOS";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.iOS, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Little Lifeline iOS export failed: " + report.summary.result);
            File.WriteAllText(Path.Combine(output, "orbit-orchard-unity-build.json"), JsonUtility.ToJson(new ExportProvenance
            {
                product = "little-lifeline",
                scenePath = ScenePath,
                sourceCommit = Git("rev-parse HEAD"),
                sourceDirty = !string.IsNullOrEmpty(Git("status --porcelain --untracked-files=all -- .")),
                unityVersion = Application.unityVersion,
                buildSucceeded = true
            }, true));
            Debug.Log("Little Lifeline iOS export succeeded: " + output);
        }
        [MenuItem("Little Lifeline/Build iOS Simulator")]
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
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = output,
                    target = BuildTarget.iOS, options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Little Lifeline iOS simulator export failed: " + report.summary.result);
                Debug.Log("Little Lifeline ARM64 iOS simulator export succeeded: " + output);
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
            public string product, scenePath, sourceCommit, unityVersion;
            public bool sourceDirty, buildSucceeded;
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
        [MenuItem("Little Lifeline/Open game scene")]
        public static void OpenGame() { Prepare(); EditorSceneManager.OpenScene(ScenePath); }
    }
}
