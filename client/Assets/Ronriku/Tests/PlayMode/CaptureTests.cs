using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Ronriku.Composition;
using Ronriku.Presentation.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Ronriku.Tests
{
    /// <summary>
    /// Renders the Daily flow into offscreen textures at representative portrait aspect ratios and
    /// writes PNG evidence to docs/evidence. Run with: scripts/unity-test.ps1 -Platform PlayMode -Category Capture
    /// </summary>
    [Category("Capture")]
    public sealed class CaptureTests
    {
        private static readonly (int width, int height, string name)[] Profiles =
        {
            (1080, 1920, "narrow-16x9"),
            (1080, 2400, "seeker-20x9"),
            (1080, 2640, "tall-22x9")
        };

        [UnityTest]
        public IEnumerator CaptureDailyFlow_AtRepresentativeAspectRatios()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/evidence"));
            Directory.CreateDirectory(folder);

            foreach (var profile in Profiles)
            {
                string profileDir = Path.Combine(Path.GetTempPath(), "ronriku-capture-" + Guid.NewGuid().ToString("N"));
                RuntimeConfig.ProfileDirectory = profileDir;
                RuntimeConfig.OnlineEnabled = false;
                RuntimeConfig.UtcNowOverride = new DateTime(2026, 9, 23, 16, 41, 7, DateTimeKind.Utc);
                try
                {
                    SceneManager.LoadScene("Bootstrap");
                    yield return null;

                    var app = UnityEngine.Object.FindAnyObjectByType<RonrikuBootstrap>();
                    var document = app.GetComponent<UIDocument>();
                    var target = new RenderTexture(profile.width, profile.height, 24, RenderTextureFormat.ARGB32,
                        RenderTextureReadWrite.sRGB);
                    document.panelSettings = UnityEngine.Object.Instantiate(document.panelSettings);
                    document.panelSettings.targetTexture = target;
                    yield return Settle();
                    var root = document.rootVisualElement;
                    yield return new WaitForSecondsRealtime(1.4f);
                    yield return Settle();
                    Save(target, folder, profile.name, "0-map");
                    foreach (var tab in new[] { AppTab.Shop, AppTab.Bosses, AppTab.Me })
                    {
                        DailyRouteTests.ShowTab(app, tab);
                        yield return Settle();
                        Save(target, folder, profile.name, "0-" + tab.ToString().ToLowerInvariant());
                    }
                    DailyRouteTests.ShowTab(app, AppTab.Daily);
                    yield return Settle();
                    Save(target, folder, profile.name, "1-home");

                    DailyRouteTests.Click(root.Q<Button>("begin-button"));
                    yield return Settle();
                    Save(target, folder, profile.name, "2-trial1-start");

                    for (int trial = 0; trial < 3; trial++)
                    {
                        yield return DailyRouteTests.SolveCurrent(app, root);
                        yield return Settle();
                        Save(target, folder, profile.name, $"3-trial{trial + 1}-solved");
                        DailyRouteTests.Click(root.Q<Button>("continue-button"));
                        yield return Settle();
                        if (trial < 2) Save(target, folder, profile.name, $"2-trial{trial + 2}-start");
                    }

                    yield return new WaitForSecondsRealtime(0.8f);
                    yield return Settle();
                    Save(target, folder, profile.name, "4-results");

                    DailyRouteTests.Click(root.Q<Button>("home-button"));
                    yield return Settle();
                    Save(target, folder, profile.name, "5-home-after");

                    document.panelSettings.targetTexture = null;
                    target.Release();
                    UnityEngine.Object.Destroy(target);
                }
                finally
                {
                    RuntimeConfig.Reset();
                    if (Directory.Exists(profileDir)) Directory.Delete(profileDir, true);
                }
            }
        }

        private static IEnumerator Settle()
        {
            // WaitForEndOfFrame never resumes in batchmode; plain frames let the panel repaint.
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static void Save(RenderTexture source, string folder, string profile, string step)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(folder, $"{profile}-{step}.png"), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
        }
    }
}
