using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Ronriku.Composition;
using Ronriku.Domain.Puzzles;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Ronriku.Tests
{
    /// <summary>
    /// Renders the UI into offscreen textures at representative portrait aspect ratios and
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
        public IEnumerator CaptureHomeAndSpatial_AtRepresentativeAspectRatios()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/evidence"));
            Directory.CreateDirectory(folder);

            foreach (var profile in Profiles)
            {
                SceneManager.LoadScene("Bootstrap");
                yield return null;

                var app = Object.FindAnyObjectByType<RonrikuBootstrap>();
                var document = app.GetComponent<UIDocument>();
                var target = new RenderTexture(profile.width, profile.height, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                document.panelSettings = Object.Instantiate(document.panelSettings);
                document.panelSettings.targetTexture = target;
                yield return Settle();

                var root = document.rootVisualElement;
                Save(target, folder, profile.name, "1-home");

                Click(root.Q<Button>("begin-button"));
                yield return Settle();
                Save(target, folder, profile.name, "2-spatial-start");

                var puzzle = (SpatialPuzzleData)typeof(RonrikuBootstrap)
                    .GetField("_puzzle", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(app);
                IReadOnlyList<SpatialMove> solution = SpatialPuzzleSolver.Solve(puzzle);
                for (int i = 0; i < solution.Count; i++)
                {
                    Click(root.Q<Button>(ButtonName(solution[i])));
                    yield return new WaitForSecondsRealtime(0.3f);
                    if (i == 0 && solution.Count > 1) Save(target, folder, profile.name, "3-spatial-move");
                }
                yield return Settle();
                Save(target, folder, profile.name, "4-spatial-solved");

                document.panelSettings.targetTexture = null;
                target.Release();
                Object.Destroy(target);
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
            Object.Destroy(texture);
        }

        private static string ButtonName(SpatialMove move) => move switch
        {
            SpatialMove.TurnLeft => "turn-left-button",
            SpatialMove.TurnRight => "turn-right-button",
            SpatialMove.TipBack => "tip-back-button",
            _ => "tip-forward-button"
        };

        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null);
            using var submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }
    }
}
