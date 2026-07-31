using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Testing.Gameplay;
using Tactics.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// Isolated Home-scene smoke coverage for production PlayerInput UI interaction.
    /// </summary>
    public sealed class HomeSceneInputSmokeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = false;
            PlayerInputGameplayStepAdapter.RemoveResidualVirtualTestDevices();

            var initializeTask = TestGameAssetHelper.EnsureInitialized();
            yield return WaitForTask(initializeTask);
            Assume.That(initializeTask.Result, Is.Not.Null, "GameAssetManager should be initialized.");

            foreach (var eventSystem in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (eventSystem != null)
                    Object.Destroy(eventSystem.gameObject);
            }
            yield return null;

            var loadHomeTask = initializeTask.Result.LoadSceneAsync(
                SceneProjectPathHelper.ToProjectPath("Home"),
                LoadSceneMode.Single);
            yield return WaitForTask(loadHomeTask);

            DestroyCachedUiInstances();
            yield return null;

            var showHomeTask = UIManager.Instance.ShowAsync(UIManager.UIId.Home);
            yield return WaitForTask(showHomeTask);

            HomeUIController homeController = null;
            for (int frame = 0; frame < 120; frame++)
            {
                homeController = Object.FindFirstObjectByType<HomeUIController>();
                if (homeController?.IsReadyForInput == true)
                    break;
                yield return null;
            }

            Assert.That(homeController?.IsReadyForInput, Is.True,
                "Home UI should be wired to the current UIDocument tree.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            DestroyCachedUiInstances();
            yield return null;
            PlayerInputGameplayStepAdapter.RemoveResidualVirtualTestDevices();
            TestGameAssetHelper.Cleanup();
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_OpensOptionsThroughPlayerInput()
        {
            var task = ExecutePlan(GetPlanPath("home-options-player-input-smoke.plan.json"));
            yield return WaitForTask(task);

            AssertPlanPassed(task.Result);
            Assert.That(PlayerInputGameplayStepAdapter.HasVirtualTestDevices, Is.False,
                "The runtime runner should release all test-owned virtual input devices.");
        }

        [UnityTest]
        public IEnumerator RuntimeDefaultFont_AddsChineseGlyphsAndIsInherited()
        {
            var root = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
            Assert.That(root, Is.Not.Null);

            var probe = new Label("中文测试 ABC");
            root.Add(probe);
            yield return null;

            FontAsset rootFontAsset = root.style.unityFontDefinition.value.fontAsset;
            FontAsset probeFontAsset = probe.resolvedStyle.unityFontDefinition.fontAsset;

            Assert.That(rootFontAsset, Is.Not.Null);
            Assert.That(probeFontAsset, Is.SameAs(rootFontAsset),
                "Child text should inherit the shared runtime FontAsset from the UIDocument root.");
            Assert.That(rootFontAsset.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(rootFontAsset.isMultiAtlasTexturesEnabled, Is.True);
            Assert.That(rootFontAsset.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
            Assert.That(rootFontAsset.sourceFontFile, Is.Not.Null,
                "The runtime FontAsset must retain its source TTF reference.");
            Assert.That(rootFontAsset.sourceFontFile.HasCharacter('中'), Is.True,
                "The source TTF must contain Chinese glyphs.");
            Assert.That(FontEngine.InitializeFontEngine(), Is.EqualTo(FontEngineError.Success));
            Assert.That(
                FontEngine.LoadFontFace(
                    rootFontAsset.sourceFontFile,
                    (int)rootFontAsset.faceInfo.pointSize),
                Is.EqualTo(FontEngineError.Success));
            Assert.That(FontEngine.TryGetGlyphIndex('中', out uint chineseGlyphIndex), Is.True);
            Assert.That(chineseGlyphIndex, Is.GreaterThan(0u));
            Assert.That(FontEngine.TryGetGlyphIndex('A', out uint latinGlyphIndex), Is.True);
            Assert.That(latinGlyphIndex, Is.GreaterThan(0u));
            Assert.That(rootFontAsset.TryAddCharacters("中文测试 ABC"), Is.True,
                "Runtime font should add or already contain all requested glyphs.");
            Assert.That(rootFontAsset.HasCharacter('中'), Is.True);
            Assert.That(rootFontAsset.HasCharacter('A'), Is.True);

            root.Remove(probe);
        }

        [UnityTest]
        public IEnumerator RuntimeDefaultFont_DynamicAtlasesStayDontSaveAcrossHideAndReopen()
        {
            var root = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
            Assert.That(root, Is.Not.Null);

            FontAsset rootFontAsset = root.style.unityFontDefinition.value.fontAsset;
            Assert.That(rootFontAsset, Is.Not.Null);
            int initialAtlasCount = rootFontAsset.atlasTextureCount;
            string generatedChineseText = AddChineseGlyphsUntilAtlasGrows(rootFontAsset, initialAtlasCount);
            Assert.That(rootFontAsset.atlasTextureCount, Is.GreaterThan(initialAtlasCount),
                "The test must create at least one dynamic atlas before checking lifetime propagation.");

            var renderProbe = new Label(generatedChineseText);
            root.Add(renderProbe);
            yield return null;
            yield return null;

            Assert.That(renderProbe.panel, Is.Not.Null,
                "The Chinese label must be attached to a live panel so UI Toolkit reaches its text rendering path.");
            Assert.That(renderProbe.resolvedStyle.unityFontDefinition.fontAsset, Is.SameAs(rootFontAsset));

            UIManager.Instance.Hide(UIManager.UIId.Home);
            yield return null;
            var showHomeTask = UIManager.Instance.ShowAsync(UIManager.UIId.Home);
            yield return WaitForTask(showHomeTask);
            yield return null;

            var reopenedRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
            Assert.That(reopenedRoot, Is.Not.Null);
            var reopenedProbe = new Label(generatedChineseText.Substring(0, 32));
            reopenedRoot.Add(reopenedProbe);
            yield return null;
            yield return null;

            FontAsset reopenedFontAsset = reopenedRoot.style.unityFontDefinition.value.fontAsset;
            Assert.That(reopenedFontAsset, Is.SameAs(rootFontAsset));
            Assert.That(reopenedProbe.panel, Is.Not.Null);
            Assert.That(reopenedProbe.resolvedStyle.unityFontDefinition.fontAsset, Is.SameAs(rootFontAsset),
                "A label created after UIDocument reactivation must inherit the same runtime font.");
            AssertRuntimeFontGraphIsDontSave(rootFontAsset);
        }

        [UnityTest]
        public IEnumerator RuntimeDefaultFont_RepairsOwnedAtlasFlagsBeforeShowingAnotherUi()
        {
            var homeRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
            Assert.That(homeRoot, Is.Not.Null);

            FontAsset originalFontAsset = homeRoot.style.unityFontDefinition.value.fontAsset;
            Assert.That(originalFontAsset, Is.Not.Null);
            Material originalMaterial = originalFontAsset.material;
            int initialAtlasCount = originalFontAsset.atlasTextureCount;
            AddChineseGlyphsUntilAtlasGrows(originalFontAsset, initialAtlasCount);
            Assert.That(originalFontAsset.atlasTextureCount, Is.GreaterThan(initialAtlasCount));
            Texture2D[] originalAtlases = originalFontAsset.atlasTextures
                .Take(originalFontAsset.atlasTextureCount)
                .ToArray();
            int originalCandidateCount = CountEquivalentRuntimeFontCandidates(originalFontAsset.sourceFontFile);

            for (int index = initialAtlasCount; index < originalFontAsset.atlasTextureCount; index++)
                originalFontAsset.atlasTextures[index].hideFlags = HideFlags.None;

            var showOptionsTask = UIManager.Instance.ShowAsync(UIManager.UIId.Options);
            yield return WaitForTask(showOptionsTask);
            yield return null;

            var optionsRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Options);
            Assert.That(optionsRoot, Is.Not.Null);
            FontAsset optionsFontAsset = optionsRoot.style.unityFontDefinition.value.fontAsset;
            Assert.That(optionsFontAsset, Is.SameAs(originalFontAsset),
                "A repairable owned atlas flag must not replace the shared runtime FontAsset.");
            Assert.That(optionsFontAsset.material, Is.SameAs(originalMaterial));
            Assert.That(optionsFontAsset.atlasTextureCount, Is.EqualTo(originalAtlases.Length));
            for (int index = 0; index < originalAtlases.Length; index++)
                Assert.That(optionsFontAsset.atlasTextures[index], Is.SameAs(originalAtlases[index]));
            Assert.That(
                CountEquivalentRuntimeFontCandidates(originalFontAsset.sourceFontFile),
                Is.EqualTo(originalCandidateCount),
                "Repairing owned atlas flags must not create a replacement FontAsset.");
            AssertRuntimeFontGraphIsDontSave(optionsFontAsset);
        }

        [UnityTest]
        public IEnumerator RuntimeDefaultFont_RecoversSurvivingAssetAfterStaticReferenceLoss()
        {
            var root = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
            Assert.That(root, Is.Not.Null);
            FontAsset originalFontAsset = root.style.unityFontDefinition.value.fontAsset;
            Assert.That(originalFontAsset, Is.Not.Null);
            Material originalMaterial = originalFontAsset.material;
            Texture2D[] originalAtlases = originalFontAsset.atlasTextures
                .Take(originalFontAsset.atlasTextureCount)
                .ToArray();
            int originalCandidateCount = CountEquivalentRuntimeFontCandidates(originalFontAsset.sourceFontFile);

            FontAsset sameNameImpostor = FontAsset.CreateFontAsset(
                originalFontAsset.sourceFontFile,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            Assert.That(sameNameImpostor, Is.Not.Null);
            string originalName = originalFontAsset.name;
            sameNameImpostor.name = originalName;

            try
            {
                const System.Reflection.BindingFlags FieldFlags =
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var sourceField = typeof(UIManager).GetField("_runtimeDefaultFontSource", FieldFlags);
                var assetField = typeof(UIManager).GetField("_runtimeDefaultFontAsset", FieldFlags);
                var ownerField = typeof(UIManager).GetField("_runtimeDefaultFontOwner", FieldFlags);
                Assert.That(sourceField, Is.Not.Null);
                Assert.That(assetField, Is.Not.Null);
                Assert.That(ownerField, Is.Not.Null);
                sourceField.SetValue(UIManager.Instance, null);
                assetField.SetValue(UIManager.Instance, null);
                ownerField.SetValue(UIManager.Instance, null);

                DestroyCachedUiInstances();
                yield return null;

                var showHomeTask = UIManager.Instance.ShowAsync(UIManager.UIId.Home);
                yield return WaitForTask(showHomeTask);
                yield return null;

                var recoveredRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
                Assert.That(recoveredRoot, Is.Not.Null);
                FontAsset recoveredFontAsset = recoveredRoot.style.unityFontDefinition.value.fontAsset;
                Assert.That(recoveredFontAsset, Is.SameAs(originalFontAsset),
                    "Recovery must use the uniquely owned FontAsset, not an otherwise valid same-name candidate.");
                Assert.That(recoveredFontAsset.sourceFontFile, Is.SameAs(originalFontAsset.sourceFontFile));
                Assert.That(recoveredFontAsset.material, Is.SameAs(originalMaterial),
                    "Recovery must preserve the existing runtime material instance.");
                Assert.That(recoveredFontAsset.atlasTextureCount, Is.EqualTo(originalAtlases.Length));
                for (int index = 0; index < originalAtlases.Length; index++)
                {
                    Assert.That(recoveredFontAsset.atlasTextures[index], Is.SameAs(originalAtlases[index]),
                        $"Recovery must preserve atlas instance {index}.");
                }
                AssertRuntimeFontGraphIsDontSave(recoveredFontAsset);
            }
            finally
            {
                DestroyRuntimeFontAsset(sameNameImpostor);
            }

            yield return null;
            Assert.That(
                CountEquivalentRuntimeFontCandidates(originalFontAsset.sourceFontFile),
                Is.EqualTo(originalCandidateCount),
                "Recovery must not grow the runtime FontAsset population.");
        }

        [UnityTest]
        public IEnumerator RuntimeDefaultFont_DuplicateOwnerSharingGraphPreservesResources()
        {
            var homeRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
            Assert.That(homeRoot, Is.Not.Null);
            FontAsset originalFontAsset = homeRoot.style.unityFontDefinition.value.fontAsset;
            Assert.That(originalFontAsset, Is.Not.Null);
            Material originalMaterial = originalFontAsset.material;
            Texture2D[] originalAtlases = originalFontAsset.atlasTextures
                .Take(originalFontAsset.atlasTextureCount)
                .ToArray();

            const System.Reflection.BindingFlags FieldFlags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var ownerField = typeof(UIManager).GetField("_runtimeDefaultFontOwner", FieldFlags);
            Assert.That(ownerField, Is.Not.Null);
            var originalOwner = ownerField.GetValue(UIManager.Instance) as ScriptableObject;
            Assert.That(originalOwner, Is.Not.Null);
            var ownerFontAssetField = originalOwner.GetType().GetField("fontAsset", FieldFlags);
            Assert.That(ownerFontAssetField, Is.Not.Null);
            var duplicateOwner = CreateRuntimeFontOwnerForTest(
                originalOwner,
                originalFontAsset,
                originalOwner.hideFlags);

            try
            {
                Assert.That(duplicateOwner, Is.Not.Null);
                Assert.That(ownerFontAssetField.GetValue(duplicateOwner), Is.SameAs(originalFontAsset));
                ClearRuntimeDefaultFontStaticFields();

                var showOptionsTask = UIManager.Instance.ShowAsync(UIManager.UIId.Options);
                while (!showOptionsTask.IsCompleted)
                    yield return null;
                Assert.That(showOptionsTask.IsFaulted, Is.False,
                    $"Duplicate-owner recovery must not destroy the retained graph: {showOptionsTask.Exception}");
                yield return null;

                var optionsRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Options);
                Assert.That(optionsRoot, Is.Not.Null);
                FontAsset optionsFontAsset = optionsRoot.style.unityFontDefinition.value.fontAsset;
                Assert.That(optionsFontAsset, Is.SameAs(originalFontAsset));
                Assert.That(optionsFontAsset.material, Is.SameAs(originalMaterial));
                Assert.That(optionsFontAsset.atlasTextureCount, Is.EqualTo(originalAtlases.Length));
                for (int index = 0; index < originalAtlases.Length; index++)
                    Assert.That(optionsFontAsset.atlasTextures[index], Is.SameAs(originalAtlases[index]));

                int ownersSharingGraph = Resources.FindObjectsOfTypeAll(originalOwner.GetType())
                    .Count(owner => owner != null &&
                                    ownerFontAssetField.GetValue(owner) as FontAsset == originalFontAsset);
                Assert.That(ownersSharingGraph, Is.EqualTo(1),
                    "Duplicate ownership must be removed without destroying the retained resource graph.");
            }
            finally
            {
                if (duplicateOwner != null)
                    Object.Destroy(duplicateOwner);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeDefaultFont_InvalidOwnerSharingGraphPreservesRecoveryResources()
        {
            var homeRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
            Assert.That(homeRoot, Is.Not.Null);
            FontAsset originalFontAsset = homeRoot.style.unityFontDefinition.value.fontAsset;
            Assert.That(originalFontAsset, Is.Not.Null);
            Material originalMaterial = originalFontAsset.material;
            Texture2D[] originalAtlases = originalFontAsset.atlasTextures
                .Take(originalFontAsset.atlasTextureCount)
                .ToArray();

            const System.Reflection.BindingFlags FieldFlags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var ownerField = typeof(UIManager).GetField("_runtimeDefaultFontOwner", FieldFlags);
            Assert.That(ownerField, Is.Not.Null);
            var originalOwner = ownerField.GetValue(UIManager.Instance) as ScriptableObject;
            Assert.That(originalOwner, Is.Not.Null);
            var ownerSourceField = originalOwner.GetType().GetField("source", FieldFlags);
            var ownerFontAssetField = originalOwner.GetType().GetField("fontAsset", FieldFlags);
            Assert.That(ownerSourceField, Is.Not.Null);
            Assert.That(ownerFontAssetField, Is.Not.Null);
            var invalidOwner = CreateRuntimeFontOwnerForTest(
                originalOwner,
                originalFontAsset,
                originalOwner.hideFlags);
            ownerSourceField.SetValue(invalidOwner, null);

            try
            {
                ClearRuntimeDefaultFontStaticFields();
                var showOptionsTask = UIManager.Instance.ShowAsync(UIManager.UIId.Options);
                while (!showOptionsTask.IsCompleted)
                    yield return null;
                Assert.That(showOptionsTask.IsFaulted, Is.False,
                    $"Invalid shared ownership must not destroy the recoverable graph: {showOptionsTask.Exception}");
                yield return null;

                var optionsRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Options);
                Assert.That(optionsRoot, Is.Not.Null);
                FontAsset optionsFontAsset = optionsRoot.style.unityFontDefinition.value.fontAsset;
                Assert.That(optionsFontAsset, Is.SameAs(originalFontAsset));
                Assert.That(optionsFontAsset.material, Is.SameAs(originalMaterial));
                Assert.That(optionsFontAsset.atlasTextureCount, Is.EqualTo(originalAtlases.Length));
                for (int index = 0; index < originalAtlases.Length; index++)
                    Assert.That(optionsFontAsset.atlasTextures[index], Is.SameAs(originalAtlases[index]));

                int ownersSharingGraph = Resources.FindObjectsOfTypeAll(originalOwner.GetType())
                    .Count(owner => owner != null &&
                                    ownerFontAssetField.GetValue(owner) as FontAsset == originalFontAsset);
                Assert.That(ownersSharingGraph, Is.EqualTo(1));
            }
            finally
            {
                if (invalidOwner != null)
                    Object.Destroy(invalidOwner);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeDefaultFont_InvalidOwnerSharingGraphPreservesSynchronizedResources()
        {
            var homeRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
            Assert.That(homeRoot, Is.Not.Null);
            FontAsset originalFontAsset = homeRoot.style.unityFontDefinition.value.fontAsset;
            Assert.That(originalFontAsset, Is.Not.Null);
            Material originalMaterial = originalFontAsset.material;
            Texture2D[] originalAtlases = originalFontAsset.atlasTextures
                .Take(originalFontAsset.atlasTextureCount)
                .ToArray();

            const System.Reflection.BindingFlags FieldFlags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var ownerField = typeof(UIManager).GetField("_runtimeDefaultFontOwner", FieldFlags);
            Assert.That(ownerField, Is.Not.Null);
            var originalOwner = ownerField.GetValue(UIManager.Instance) as ScriptableObject;
            Assert.That(originalOwner, Is.Not.Null);
            var ownerSourceField = originalOwner.GetType().GetField("source", FieldFlags);
            var ownerFontAssetField = originalOwner.GetType().GetField("fontAsset", FieldFlags);
            Assert.That(ownerSourceField, Is.Not.Null);
            Assert.That(ownerFontAssetField, Is.Not.Null);
            var invalidOwner = CreateRuntimeFontOwnerForTest(
                originalOwner,
                originalFontAsset,
                originalOwner.hideFlags);
            ownerSourceField.SetValue(invalidOwner, null);

            try
            {
                UIManager.Instance.Hide(UIManager.UIId.Home);
                yield return null;

                Assert.That(originalFontAsset, Is.Not.Null);
                Assert.That(originalFontAsset.material, Is.SameAs(originalMaterial));
                Assert.That(originalFontAsset.atlasTextureCount, Is.EqualTo(originalAtlases.Length));
                for (int index = 0; index < originalAtlases.Length; index++)
                    Assert.That(originalFontAsset.atlasTextures[index], Is.SameAs(originalAtlases[index]));

                yield return WaitForTask(UIManager.Instance.ShowAsync(UIManager.UIId.Home));
                var reopenedHomeRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
                Assert.That(reopenedHomeRoot, Is.Not.Null);
                Assert.That(reopenedHomeRoot.style.unityFontDefinition.value.fontAsset,
                    Is.SameAs(originalFontAsset));

                int ownersSharingGraph = Resources.FindObjectsOfTypeAll(originalOwner.GetType())
                    .Count(owner => owner != null &&
                                    ownerFontAssetField.GetValue(owner) as FontAsset == originalFontAsset);
                Assert.That(ownersSharingGraph, Is.EqualTo(1));
            }
            finally
            {
                if (invalidOwner != null)
                    Object.Destroy(invalidOwner);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeDefaultFont_CleanupPreservesSharedResourcesAndUnusedAtlasCapacity()
        {
            var homeRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
            Assert.That(homeRoot, Is.Not.Null);
            FontAsset originalFontAsset = homeRoot.style.unityFontDefinition.value.fontAsset;
            Assert.That(originalFontAsset, Is.Not.Null);
            Material originalMaterial = originalFontAsset.material;
            Texture2D originalAtlas = originalFontAsset.atlasTextures[0];
            int candidateCountBefore = CountEquivalentRuntimeFontCandidates(originalFontAsset.sourceFontFile);

            FontAsset partialFontAsset = FontAsset.CreateFontAsset(
                originalFontAsset.sourceFontFile,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            Assert.That(partialFontAsset, Is.Not.Null);
            Assert.That(partialFontAsset.atlasTextureCount, Is.EqualTo(1));
            Material replacedMaterial = partialFontAsset.material;
            Texture2D unusedTailAtlas = partialFontAsset.atlasTextures[0];
            var materialProperty = typeof(FontAsset).GetProperty("material");
            var atlasTexturesProperty = typeof(FontAsset).GetProperty("atlasTextures");
            Assert.That(materialProperty, Is.Not.Null);
            Assert.That(materialProperty.CanWrite, Is.True);
            Assert.That(atlasTexturesProperty, Is.Not.Null);
            Assert.That(atlasTexturesProperty.CanWrite, Is.True);
            materialProperty.SetValue(partialFontAsset, originalMaterial);
            atlasTexturesProperty.SetValue(partialFontAsset, new[] { originalAtlas, unusedTailAtlas });
            originalMaterial.mainTexture = originalAtlas;
            Assert.That(originalFontAsset.material, Is.SameAs(originalMaterial));
            Assert.That(originalFontAsset.atlasTextures[0], Is.SameAs(originalAtlas));
            Assert.That(originalMaterial.mainTexture, Is.SameAs(originalAtlas));
            Assert.That(partialFontAsset.material, Is.SameAs(originalMaterial));
            Assert.That(partialFontAsset.atlasTextures[0], Is.SameAs(originalAtlas));
            Assert.That(partialFontAsset.atlasTextures[1], Is.SameAs(unusedTailAtlas));

            const System.Reflection.BindingFlags FieldFlags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var ownerField = typeof(UIManager).GetField("_runtimeDefaultFontOwner", FieldFlags);
            Assert.That(ownerField, Is.Not.Null);
            var originalOwner = ownerField.GetValue(UIManager.Instance) as ScriptableObject;
            Assert.That(originalOwner, Is.Not.Null);
            var ownerSourceField = originalOwner.GetType().GetField("source", FieldFlags);
            Assert.That(ownerSourceField, Is.Not.Null);
            var partialOwner = CreateRuntimeFontOwnerForTest(
                originalOwner,
                partialFontAsset,
                originalOwner.hideFlags);
            ownerSourceField.SetValue(partialOwner, null);

            try
            {
                ClearRuntimeDefaultFontStaticFields();
                var showOptionsTask = UIManager.Instance.ShowAsync(UIManager.UIId.Options);
                while (!showOptionsTask.IsCompleted)
                    yield return null;
                Assert.That(showOptionsTask.IsFaulted, Is.False,
                    $"Partial sharing must not destroy retained resources: {showOptionsTask.Exception}");
                yield return null;

                var optionsRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Options);
                Assert.That(optionsRoot, Is.Not.Null);
                FontAsset optionsFontAsset = optionsRoot.style.unityFontDefinition.value.fontAsset;
                Assert.That(optionsFontAsset, Is.SameAs(originalFontAsset));
                Assert.That(optionsFontAsset.material, Is.SameAs(originalMaterial));
                Assert.That(optionsFontAsset.atlasTextures[0], Is.SameAs(originalAtlas));
                Assert.That(partialOwner == null, Is.True);
                Assert.That(partialFontAsset == null, Is.True,
                    "The unretained FontAsset itself must still be destroyed.");
                Assert.That(unusedTailAtlas != null, Is.True,
                    "Unused atlas capacity must not be treated as an owned atlas resource.");
                Assert.That(CountEquivalentRuntimeFontCandidates(originalFontAsset.sourceFontFile),
                    Is.EqualTo(candidateCountBefore));
            }
            finally
            {
                if (partialOwner != null)
                    Object.Destroy(partialOwner);
                if (partialFontAsset != null)
                    Object.Destroy(partialFontAsset);
                if (unusedTailAtlas != null)
                    Object.Destroy(unusedTailAtlas);
                if (replacedMaterial != null)
                    Object.Destroy(replacedMaterial);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeDefaultFont_RecoveryIgnoresOwnerWithoutRuntimeProvenance()
        {
            var homeRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Home);
            Assert.That(homeRoot, Is.Not.Null);
            FontAsset originalFontAsset = homeRoot.style.unityFontDefinition.value.fontAsset;
            Assert.That(originalFontAsset, Is.Not.Null);
            int candidateCountBefore = CountEquivalentRuntimeFontCandidates(originalFontAsset.sourceFontFile);

            const System.Reflection.BindingFlags FieldFlags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var ownerField = typeof(UIManager).GetField("_runtimeDefaultFontOwner", FieldFlags);
            Assert.That(ownerField, Is.Not.Null);
            var originalOwner = ownerField.GetValue(UIManager.Instance) as ScriptableObject;
            Assert.That(originalOwner, Is.Not.Null);
            var ownerFontAssetField = originalOwner.GetType().GetField("fontAsset", FieldFlags);
            Assert.That(ownerFontAssetField, Is.Not.Null);

            FontAsset foreignFontAsset = FontAsset.CreateFontAsset(
                originalFontAsset.sourceFontFile,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            Assert.That(foreignFontAsset, Is.Not.Null);
            Material foreignMaterial = foreignFontAsset.material;
            Texture2D[] foreignAtlases = foreignFontAsset.atlasTextures
                .Take(foreignFontAsset.atlasTextureCount)
                .ToArray();
            var foreignOwner = CreateRuntimeFontOwnerForTest(
                originalOwner,
                foreignFontAsset,
                HideFlags.None);
            foreignFontAsset.hideFlags = HideFlags.None;
            foreignFontAsset.material.hideFlags = HideFlags.None;
            for (int index = 0; index < foreignFontAsset.atlasTextureCount; index++)
                foreignFontAsset.atlasTextures[index].hideFlags = HideFlags.None;
            int candidateCountWithForeign = CountEquivalentRuntimeFontCandidates(
                originalFontAsset.sourceFontFile);
            Assert.That(candidateCountWithForeign, Is.EqualTo(candidateCountBefore + 1));

            try
            {
                ClearRuntimeDefaultFontStaticFields();
                var showOptionsTask = UIManager.Instance.ShowAsync(UIManager.UIId.Options);
                while (!showOptionsTask.IsCompleted)
                    yield return null;
                Assert.That(showOptionsTask.IsFaulted, Is.False,
                    $"An untrusted owner must not disrupt valid runtime recovery: {showOptionsTask.Exception}");
                yield return null;

                var optionsRoot = UIManager.Instance.GetRootElement(UIManager.UIId.Options);
                Assert.That(optionsRoot, Is.Not.Null);
                Assert.That(optionsRoot.style.unityFontDefinition.value.fontAsset, Is.SameAs(originalFontAsset),
                    "Recovery must ignore an owner that lacks runtime-only provenance.");
                Assert.That(foreignOwner != null, Is.True,
                    "An untrusted owner must not be destroyed by the runtime-font cleanup path.");
                Assert.That(foreignOwner.hideFlags, Is.EqualTo(HideFlags.None));
                Assert.That(foreignFontAsset != null, Is.True);
                Assert.That(foreignFontAsset.hideFlags, Is.EqualTo(HideFlags.None));
                Assert.That(foreignFontAsset.material, Is.SameAs(foreignMaterial));
                Assert.That(foreignMaterial != null, Is.True);
                Assert.That(foreignMaterial.hideFlags, Is.EqualTo(HideFlags.None));
                Assert.That(foreignFontAsset.atlasTextureCount, Is.EqualTo(foreignAtlases.Length));
                for (int index = 0; index < foreignAtlases.Length; index++)
                {
                    Assert.That(foreignAtlases[index] != null, Is.True);
                    Assert.That(foreignFontAsset.atlasTextures[index], Is.SameAs(foreignAtlases[index]));
                    Assert.That(foreignAtlases[index].hideFlags, Is.EqualTo(HideFlags.None));
                }
                Assert.That(CountEquivalentRuntimeFontCandidates(originalFontAsset.sourceFontFile),
                    Is.EqualTo(candidateCountWithForeign),
                    "Recovery must not create an additional equivalent candidate while a valid owner survives.");
            }
            finally
            {
                if (foreignOwner != null)
                    Object.Destroy(foreignOwner);
                DestroyRuntimeFontAsset(foreignFontAsset);
            }
        }

        private static string AddChineseGlyphsUntilAtlasGrows(FontAsset fontAsset, int initialAtlasCount)
        {
            var renderedText = new StringBuilder();
            const int chunkSize = 256;
            for (int start = 0x4E00; start <= 0x9FFF && fontAsset.atlasTextureCount <= initialAtlasCount; start += chunkSize)
            {
                var chunk = new StringBuilder(chunkSize);
                int end = System.Math.Min(start + chunkSize, 0xA000);
                for (int codePoint = start; codePoint < end; codePoint++)
                    chunk.Append((char)codePoint);

                string chunkText = chunk.ToString();
                fontAsset.TryAddCharacters(chunkText);
                renderedText.Append(chunkText);
            }

            return renderedText.ToString();
        }

        private static void AssertRuntimeFontGraphIsDontSave(FontAsset fontAsset)
        {
            Assert.That(fontAsset.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
            Assert.That(fontAsset.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(fontAsset.isMultiAtlasTexturesEnabled, Is.True);
            Assert.That(fontAsset.material, Is.Not.Null);
            Assert.That(fontAsset.atlasTextureCount, Is.GreaterThan(0));
            Assert.That(fontAsset.atlasTextures.Length, Is.GreaterThanOrEqualTo(fontAsset.atlasTextureCount));
            Assert.That(fontAsset.material.mainTexture, Is.SameAs(fontAsset.atlasTextures[0]));
            Assert.That(fontAsset.material.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
            for (int index = 0; index < fontAsset.atlasTextureCount; index++)
            {
                var atlasTexture = fontAsset.atlasTextures[index];
                Assert.That(atlasTexture, Is.Not.Null);
                Assert.That(atlasTexture.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
            }
        }

        private static int CountEquivalentRuntimeFontCandidates(Font source)
        {
            return Resources.FindObjectsOfTypeAll<FontAsset>().Count(candidate =>
                candidate != null &&
                candidate.sourceFontFile == source &&
                candidate.atlasPopulationMode == AtlasPopulationMode.Dynamic &&
                candidate.isMultiAtlasTexturesEnabled);
        }

        private static void ClearRuntimeDefaultFontStaticFields()
        {
            const System.Reflection.BindingFlags FieldFlags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var sourceField = typeof(UIManager).GetField("_runtimeDefaultFontSource", FieldFlags);
            var assetField = typeof(UIManager).GetField("_runtimeDefaultFontAsset", FieldFlags);
            var ownerField = typeof(UIManager).GetField("_runtimeDefaultFontOwner", FieldFlags);
            Assert.That(sourceField, Is.Not.Null);
            Assert.That(assetField, Is.Not.Null);
            Assert.That(ownerField, Is.Not.Null);
            sourceField.SetValue(UIManager.Instance, null);
            assetField.SetValue(UIManager.Instance, null);
            ownerField.SetValue(UIManager.Instance, null);
        }

        private static ScriptableObject CreateRuntimeFontOwnerForTest(
            ScriptableObject template,
            FontAsset fontAsset,
            HideFlags hideFlags)
        {
            const System.Reflection.BindingFlags FieldFlags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var owner = ScriptableObject.CreateInstance(template.GetType());
            var markerField = template.GetType().GetField("marker", FieldFlags);
            var sourceField = template.GetType().GetField("source", FieldFlags);
            var fontAssetField = template.GetType().GetField("fontAsset", FieldFlags);
            Assert.That(markerField, Is.Not.Null);
            Assert.That(sourceField, Is.Not.Null);
            Assert.That(fontAssetField, Is.Not.Null);
            markerField.SetValue(owner, markerField.GetValue(template));
            sourceField.SetValue(owner, sourceField.GetValue(template));
            fontAssetField.SetValue(owner, fontAsset);
            owner.hideFlags = hideFlags;
            return owner;
        }

        private static void DestroyRuntimeFontAsset(FontAsset fontAsset)
        {
            if (fontAsset == null)
                return;

            if (fontAsset.material != null)
                Object.Destroy(fontAsset.material);
            foreach (var atlasTexture in fontAsset.atlasTextures ?? System.Array.Empty<Texture2D>())
            {
                if (atlasTexture != null)
                    Object.Destroy(atlasTexture);
            }
            Object.Destroy(fontAsset);
        }

        private static async Task<GameplayTestResult> ExecutePlan(string planPath)
        {
            Assert.IsTrue(File.Exists(planPath), $"Plan file not found: {planPath}");
            var plan = ExecutableScenarioPlanLoader.FromFile(planPath);
            var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[]
            {
                new PlayerInputGameplayStepAdapter(),
                new UiGameplayStepAdapter()
            });
            return await runner.ExecuteAsync(plan);
        }

        private static void AssertPlanPassed(GameplayTestResult result)
        {
            var stepTrace = string.Join("; ", result.ExecutedSteps.Select(step => $"{step.Kind}: {step.Message}"));
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, " +
                          $"Diagnostics=[{string.Join("; ", result.Diagnostics)}], StepTrace=[{stepTrace}]";
            Assert.IsTrue(result.Passed, details);
        }

        private static string GetPlanPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Tests",
                "gameplay-specs",
                "compiled",
                fileName));
        }

        private static void DestroyCachedUiInstances()
        {
            if (UIManager.Instance == null)
                return;

            foreach (UIManager.UIId uiId in System.Enum.GetValues(typeof(UIManager.UIId)))
                UIManager.Instance.Destroy(uiId);
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                Assert.Fail($"Task failed: {task.Exception}");
        }
    }
}
