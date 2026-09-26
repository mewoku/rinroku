using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Ronriku.Domain.Puzzles;
using Ronriku.Presentation.Screens;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Tests
{
    /// <summary>Solves an embedded classic trial (BIG CARD) through its real controls.</summary>
    internal static class BigCardSolver
    {
        private static T Data<T>(object screen) where T : class =>
            screen.GetType().GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(screen) as T;

        public static IEnumerator Solve(VisualElement card)
        {
            if (card.Q<PatternPuzzleScreen>() is { } pattern)
            {
                var data = Data<PatternPuzzleData>(pattern);
                DailyRouteTests.Click(pattern.Q<Button>($"option-{data.CorrectOption}"));
            }
            else if (card.Q<LogicPuzzleScreen>() is { } logic)
            {
                var data = Data<LogicPuzzleData>(logic);
                var path = LogicPuzzleSolver.Solve(data);
                for (int i = 1; i < path.Count; i++) Assert.That(logic.TryExtend(path[i]), Is.True, $"link step {i}");
            }
            else if (card.Q<SpatialPuzzleScreen>() is { } spatial)
            {
                var data = Data<SpatialPuzzleData>(spatial);
                foreach (SpatialMove move in SpatialPuzzleSolver.Solve(data))
                {
                    DailyRouteTests.Click(spatial.Q<Button>(DailyRouteTests.ButtonName(move)));
                    yield return new WaitForSecondsRealtime(0.2f);
                }
            }
            else Assert.Fail("BIG CARD without a classic trial");
            yield return null;
        }
    }
}
