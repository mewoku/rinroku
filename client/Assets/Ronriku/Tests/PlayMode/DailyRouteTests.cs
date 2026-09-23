using System.Collections;
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
    public sealed class DailyRouteTests
    {
        [UnityTest]
        public IEnumerator HomeRoute_ContainsBegin_AndOpensPlayableSpatialHost()
        {
            var (app, root) = default((RonrikuBootstrap, VisualElement));
            yield return OpenSpatial(result => (app, root) = result);

            Assert.That(root.Q<Button>("turn-left-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("tip-forward-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("continue-button").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator SolverPath_PlayedThroughButtons_RevealsContinue()
        {
            var (app, root) = default((RonrikuBootstrap, VisualElement));
            yield return OpenSpatial(result => (app, root) = result);

            var puzzle = (SpatialPuzzleData)typeof(RonrikuBootstrap)
                .GetField("_puzzle", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(app);
            foreach (SpatialMove move in SpatialPuzzleSolver.Solve(puzzle))
            {
                Click(root.Q<Button>(ButtonName(move)));
                yield return new WaitForSecondsRealtime(0.3f);
            }

            Assert.That(root.Q<Button>("continue-button").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Click(root.Q<Button>("continue-button"));
            yield return null;
            Assert.That(root.Q<Button>("begin-button"), Is.Not.Null, "continue returns to Home");
        }

        private static IEnumerator OpenSpatial(System.Action<(RonrikuBootstrap, VisualElement)> ready)
        {
            SceneManager.LoadScene("Bootstrap");
            yield return null;
            yield return null;

            var app = Object.FindAnyObjectByType<RonrikuBootstrap>();
            Assert.That(app, Is.Not.Null);
            var root = app.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Button>("begin-button"), Is.Not.Null);

            Click(root.Q<Button>("begin-button"));
            yield return null;
            yield return null;
            ready((app, root));
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
