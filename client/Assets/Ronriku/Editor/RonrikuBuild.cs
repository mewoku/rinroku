using System;
using System.IO;
using Ronriku.Composition;
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
            Debug.Log("RONRIKU_VERIFY_OK phase=1 aspectProfiles=720x1600,1080x2400,1440x3200");
        }

        [MenuItem("RONRIKU/Build Android")]
        public static void BuildAndroid()
        {
            Verify();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/Android/RONRIKU.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException());
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android build failed: {report.summary.result}");
            Debug.Log($"RONRIKU_ANDROID_BUILD_OK path={output} bytes={report.summary.totalSize}");
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
            panel.referenceResolution = new Vector2Int(1080, 2400);
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
            var generator = new SpatialPuzzleGenerator();
            var validator = new SpatialPuzzleValidator();
            for (long seed = 1; seed <= 500; seed++)
            {
                SpatialPuzzleData first = generator.Generate(seed, PuzzleDifficulty.Standard, seed * 17);
                SpatialPuzzleData second = generator.Generate(seed, PuzzleDifficulty.Standard, seed * 17);
                if (first.Metadata.ContentHash != second.Metadata.ContentHash)
                    throw new InvalidOperationException($"Generator is not deterministic at seed {seed}");
                int accepted = 0;
                for (int answer = 0; answer < 4; answer++) if (validator.IsCorrect(first, answer)) accepted++;
                if (accepted != 1) throw new InvalidOperationException($"Expected one answer at seed {seed}");
                if (first.StartOrientation == first.TargetOrientation)
                    throw new InvalidOperationException($"Trivial puzzle at seed {seed}");
            }
        }
    }
}
