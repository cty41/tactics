using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tactics.Units;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

namespace Tactics.Tests.Editor
{
    public sealed class HomeSceneCompositionEditorTests
    {
        private const string ScenePath = "Assets/Tactics/Scenes/Home.unity";

        private static readonly string[] ExpectedRootNames =
        {
            "AudioListener",
            "Bootstrap",
            "EventSystem",
            "Main Camera"
        };

        private static readonly IReadOnlyDictionary<string, string[]> AllowedComponentTypeNamesByRoot =
            new Dictionary<string, string[]>
            {
                ["Bootstrap"] = new[]
                {
                    "UnityEngine.Transform",
                    "Tactics.SceneController"
                },
                ["EventSystem"] = new[]
                {
                    "UnityEngine.Transform",
                    "UnityEngine.EventSystems.EventSystem",
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule"
                },
                ["Main Camera"] = new[]
                {
                    "UnityEngine.Transform",
                    "UnityEngine.Camera",
                    "UnityEngine.EventSystems.Physics2DRaycaster",
                    "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData"
                },
                ["AudioListener"] = new[]
                {
                    "UnityEngine.Transform",
                    "UnityEngine.AudioListener"
                }
            };

        private UnityEngine.SceneManagement.Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
                EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void Home_ContainsOnlyUiAndInitializationInfrastructure()
        {
            GameObject[] roots = _scene.GetRootGameObjects();
            string[] rootNames = roots.Select(root => root.name).ToArray();
            var violations = new List<string>();

            if (!rootNames.OrderBy(name => name).SequenceEqual(ExpectedRootNames.OrderBy(name => name)))
                violations.Add($"Expected roots [{string.Join(", ", ExpectedRootNames)}], but found [{string.Join(", ", rootNames)}].");

            AddForbiddenComponentViolations<Tilemap>(roots, "Tilemap", violations);
            AddForbiddenComponentViolations<TilemapRenderer>(roots, "TilemapRenderer", violations);
            AddForbiddenComponentViolations<TilemapUnit>(roots, "TilemapUnit", violations);
            AddForbiddenComponentViolations<LandUnitMovementRules>(roots, "LandUnitMovementRules", violations);

            foreach (KeyValuePair<string, string[]> expectation in AllowedComponentTypeNamesByRoot)
            {
                GameObject[] matchingRoots = roots.Where(root => root.name == expectation.Key).ToArray();
                if (matchingRoots.Length == 0)
                {
                    violations.Add($"Could not find root '{expectation.Key}' in {ScenePath}.");
                    continue;
                }

                if (matchingRoots.Length > 1)
                {
                    violations.Add($"Expected exactly one root '{expectation.Key}' in {ScenePath}, but found {matchingRoots.Length}.");
                    continue;
                }

                ValidateRoot(matchingRoots[0], expectation.Value, violations);
            }

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        private static void AddForbiddenComponentViolations<T>(
            IEnumerable<GameObject> roots,
            string componentName,
            ICollection<string> violations)
            where T : Component
        {
            T[] components = roots
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
            if (components.Length != 0)
                violations.Add($"Expected no {componentName} components, but found [{string.Join(", ", components.Select(component => component.name))}].");
        }

        private static void ValidateRoot(
            GameObject root,
            IEnumerable<string> allowedComponentTypeNames,
            ICollection<string> violations)
        {
            if (root.transform.childCount != 0)
                violations.Add($"Root '{root.name}' must have no children, but found {root.transform.childCount}.");

            Component[] components = root.GetComponents<Component>();
            int missingScriptCount = components.Count(component => component == null);
            if (missingScriptCount != 0)
                violations.Add($"Root '{root.name}' contains {missingScriptCount} Missing Script component(s).");

            string[] actualTypeNames = components
                .Where(component => component != null)
                .Select(component => component.GetType().FullName)
                .OrderBy(typeName => typeName)
                .ToArray();
            string[] expectedTypeNames = allowedComponentTypeNames
                .OrderBy(typeName => typeName)
                .ToArray();
            if (!actualTypeNames.SequenceEqual(expectedTypeNames))
            {
                violations.Add(
                    $"Root '{root.name}' expected components [{string.Join(", ", expectedTypeNames)}], " +
                    $"but found [{string.Join(", ", actualTypeNames)}].");
            }
        }
    }
}
