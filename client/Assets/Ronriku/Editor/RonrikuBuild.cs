using System;
using System.IO;
using Ronriku.Composition;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Puzzles;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ronriku.Editor
{
    public static class RonrikuBuild
    {
        private const string ScenePath = "Assets/Ronriku/Scenes/Bootstrap.unity";
        private const string PanelSettingsPath = "Assets/Ronriku/Settings/RonrikuPanelSettings.asset";

        [MenuItem("RONRIKU/Verify Phase 1")]
        public static void Verify()
        {
            ConfigureProject();
            CreateScene();
            VerifyDomain();
            Debug.Log("RONRIKU_VERIFY_OK");
        }

        [MenuItem("RONRIKU/Build Android (Development)")]
        public static void BuildAndroid() => Build("RONRIKU-dev.apk", BuildOptions.Development);

        [MenuItem("RONRIKU/Build Android (Release)")]
        public static void BuildAndroidRelease() => Build("RONRIKU.apk", BuildOptions.None);

        /// <summary>Applies the project's player settings without building. Safe to run any time.</summary>
        [MenuItem("RONRIKU/Apply Project Settings")]
        public static void ApplyProjectSettings()
        {
            ConfigureProject();
            AssetDatabase.SaveAssets();
            Debug.Log("RONRIKU_SETTINGS_APPLIED");
        }

        private static void Build(string fileName, BuildOptions buildOptions)
        {
            Verify();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/Android", fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException());
            // Always write a fresh file: in-place APK updates leave dead space between zip entries.
            if (File.Exists(output)) File.Delete(output);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                options = buildOptions
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android build failed: {report.summary.result}");
            Debug.Log($"RONRIKU_ANDROID_BUILD_OK path={output} bytes={new FileInfo(output).Length}");
        }

        private static void ConfigureProject()
        {
            PlayerSettings.companyName = "RONRIKU";
            PlayerSettings.productName = "RONRIKU";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ronriku.game");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // No engine splash: the game shows its own pixel boot sequence.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            // Size: strip unused engine and managed code; Ronriku.Runtime is preserved by link.xml.
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.Android.minifyRelease = true;

            // Runtime cost: plain logs carry no stack trace; errors keep script frames for diagnosis.
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
        }

        private static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? throw new InvalidOperationException());
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x28, 0x29, 0x2F, 0xFF);
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";
            var app = new GameObject("RONRIKU");
            var document = app.AddComponent<UIDocument>();
            document.panelSettings = CreatePanelSettings();
            app.AddComponent<RonrikuBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        private static PanelSettings CreatePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PanelSettingsPath) ?? throw new InvalidOperationException());
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.name = "RONRIKU Panel Settings";
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }

            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(432, 960);
            panel.match = 0.5f;
            panel.clearColor = true;
            panel.colorClearValue = new Color32(0x28, 0x29, 0x2F, 0xFF);
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Assets/Ronriku/Settings/RonrikuRuntimeTheme.tss");
            if (panel.themeStyleSheet == null)
                throw new InvalidOperationException("Unity DefaultRuntimeTheme.tss could not be loaded.");
            EditorUtility.SetDirty(panel);
            return panel;
        }

        private static void VerifyDomain()
        {
            // Gate every build on the next 60 Dailies generating valid trials.
            int today = DailyCalendar.DayNumber(DateTime.UtcNow);
            var spatial = new SpatialPuzzleGenerator();
            var pattern = new PatternPuzzleGenerator();
            var logic = new LogicPuzzleGenerator();
            for (int day = today; day < today + 60; day++)
            foreach (TrialSpec spec in DailyPlan.For(day).Trials)
            {
                string violation = spec.Kind switch
                {
                    TrialKind.Pattern => PatternPuzzleInvariants.Check(pattern.Generate(spec.Seed, spec.Difficulty, 0)),
                    TrialKind.Logic => LogicPuzzleInvariants.Check(logic.Generate(spec.Seed, spec.Difficulty, 0)),
                    _ => SpatialPuzzleInvariants.Check(spatial.Generate(spec.Seed, spec.Difficulty, 0))
                };
                if (violation != null)
                    throw new InvalidOperationException($"Invalid {spec.Kind} trial on day {day}: {violation}");
            }
        }
    }
}
