using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Tactics.UI;
using UnityEditor.Compilation;
using UnityEngine;

namespace Tactics.Tests.Editor
{
    public sealed class EditorAssemblyBoundaryTests
    {
        [Test]
        public void PlayerAssemblies_DoNotContainProjectEditorSources()
        {
            var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            var violations = new List<string>();

            if (playerAssemblies == null || playerAssemblies.Length == 0)
            {
                violations.Add("Player assembly enumeration is empty");
            }

            Assembly tacticsAssembly = playerAssemblies?
                .FirstOrDefault(assembly => assembly.name == "com.tactics");
            if (tacticsAssembly == null)
            {
                violations.Add("Required Player assembly 'com.tactics' is missing");
            }
            else if (tacticsAssembly.sourceFiles != null && tacticsAssembly.sourceFiles.Length == 0)
            {
                violations.Add("com.tactics: sourceFiles metadata is empty");
            }

            foreach (Assembly assembly in playerAssemblies ?? Array.Empty<Assembly>())
            {
                if (assembly.sourceFiles == null)
                {
                    violations.Add($"{assembly.name}: sourceFiles metadata is null");
                    continue;
                }

                foreach (string sourceFile in assembly.sourceFiles)
                {
                    if (IsProjectEditorSource(sourceFile))
                        violations.Add($"{assembly.name}: {sourceFile.Replace('\\', '/')}");
                }
            }

            violations.Sort(StringComparer.Ordinal);

            Assert.That(
                violations,
                Is.Empty,
                $"Player assembly boundary violations:{Environment.NewLine}" +
                string.Join(Environment.NewLine, violations));
        }

        [TestCase("Assets/Tactics/Example/Editor/Tool.cs", true)]
        [TestCase("D:/codes/tactics/Assets/Tactics/Example/Editor/Tool.cs", true)]
        [TestCase(@"D:\codes\tactics\Assets\Tactics\Example\Editor\Tool.cs", true)]
        [TestCase("Packages/com.example/Editor/Tool.cs", false)]
        [TestCase("Assets/Tactics/Example/Runtime/Tool.cs", false)]
        public void ProjectEditorSourceClassifier_HandlesRelativeAndAbsolutePaths(
            string sourcePath,
            bool expected)
        {
            Assert.That(IsProjectEditorSource(sourcePath), Is.EqualTo(expected));
        }

        [Test]
        public void PlayModeTestAssembly_RemainsDiscoverableEditorHostedAndTestOnly()
        {
            const string assemblyPath =
                "Assets/Tactics/Tests/PlayMode/Tactics.Tests.PlayMode.asmdef";
            var definition = JsonUtility.FromJson<AssemblyDefinitionData>(
                File.ReadAllText(assemblyPath));

            Assert.That(definition, Is.Not.Null, assemblyPath);
            Assert.That(definition.IncludePlatforms, Is.Empty,
                "Editor-only includePlatforms was proven to produce zero PlayMode discovery.");
            Assert.That(definition.DefineConstraints, Does.Contain("UNITY_INCLUDE_TESTS"),
                "The Editor-hosted assembly must remain excluded from production Player builds.");
            Assert.That(definition.AutoReferenced, Is.False);
            Assert.That(definition.References, Does.Contain("Tactics.Editor"),
                "Editor integration tests call SkillGraphMcpFacade and its asset builders directly.");
        }

        [Test]
        public void RadialFillElement_IsCodeConstructibleWithoutDeprecatedUxmlFactory()
        {
            Assert.That(new RadialFillElement(), Is.Not.Null,
                "The repository constructs RadialFillElement directly from C#.");

            string[] offenders = typeof(RadialFillElement).Assembly.GetTypes()
                .Where(InheritsDeprecatedRadialFillElementUxmlFactory)
                .Select(type => type.FullName)
                .OrderBy(fullName => fullName, StringComparer.Ordinal)
                .ToArray();

            Assert.That(
                offenders,
                Is.Empty,
                $"Types inherit deprecated UxmlFactory<RadialFillElement>:{Environment.NewLine}" +
                string.Join(Environment.NewLine, offenders));
        }

        private static bool InheritsDeprecatedRadialFillElementUxmlFactory(Type type)
        {
            for (Type baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                if (!baseType.IsGenericType)
                {
                    continue;
                }

                Type genericDefinition = baseType.GetGenericTypeDefinition();
                if (genericDefinition.FullName?.StartsWith(
                        "UnityEngine.UIElements.UxmlFactory`",
                        StringComparison.Ordinal) == true &&
                    baseType.GetGenericArguments().Contains(typeof(RadialFillElement)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsProjectEditorSource(string sourcePath)
        {
            string normalizedPath = sourcePath.Replace('\\', '/');
            int projectRootIndex = normalizedPath.IndexOf("Assets/Tactics/", StringComparison.Ordinal);
            bool isRootBoundary = projectRootIndex == 0 ||
                projectRootIndex > 0 && normalizedPath[projectRootIndex - 1] == '/';
            return projectRootIndex >= 0 &&
                isRootBoundary &&
                normalizedPath.IndexOf(
                    "/Editor/",
                    projectRootIndex + "Assets/Tactics".Length,
                    StringComparison.Ordinal) >= 0;
        }

        [Serializable]
        private sealed class AssemblyDefinitionData
        {
            [SerializeField] private string[] references = Array.Empty<string>();
            [SerializeField] private string[] includePlatforms = Array.Empty<string>();
            [SerializeField] private bool autoReferenced = true;
            [SerializeField] private string[] defineConstraints = Array.Empty<string>();

            public string[] References => references;
            public string[] IncludePlatforms => includePlatforms;
            public bool AutoReferenced => autoReferenced;
            public string[] DefineConstraints => defineConstraints;
        }
    }
}
