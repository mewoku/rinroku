using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Ronriku.Composition;
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
            SceneManager.LoadScene("Bootstrap");
            yield return null;
            yield return null;

            var app = Object.FindFirstObjectByType<RonrikuBootstrap>();
            Assert.That(app, Is.Not.Null);
            var document = app.GetComponent<UIDocument>();
            Assert.That(document.rootVisualElement.Q<Button>("begin-button"), Is.Not.Null);

            MethodInfo route = typeof(RonrikuBootstrap).GetMethod("ShowPuzzle",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(route, Is.Not.Null);
            route.Invoke(app, null);
            yield return null;

            Assert.That(document.rootVisualElement.Q<Button>("check-button"), Is.Not.Null);
        }
    }
}

