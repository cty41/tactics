using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tactics.Common.Units;
using UnityEditor;
using UnityEngine;

namespace Tactics.Tests.Editor
{
    public sealed class FourDirectionSpriteVisualEditorTests
    {
        private const string HunterPrefabPath = "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunHunter.prefab";
        private const string NecromancerPrefabPath = "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunNecromancer.prefab";
        private const string MagePrefabPath = "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunMage.prefab";
        private const string SkeletonWarriorPrefabPath = "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunSkeletonWarrior.prefab";
        private const string SkeletonMagePrefabPath = "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunSkeletonMage.prefab";
        private const string FireDemonPrefabPath = "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunFireDemon.prefab";

        private static readonly string[] PureRunPrefabPaths =
        {
            HunterPrefabPath,
            NecromancerPrefabPath,
            MagePrefabPath,
            SkeletonWarriorPrefabPath,
            SkeletonMagePrefabPath,
            FireDemonPrefabPath,
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatCharger.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatRanged.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatAoe.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatSupport.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatEliteCharger.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatElitePoisonCaster.prefab"
        };

        [TestCaseSource(nameof(PureRunPrefabPaths))]
        public void PureRunPrefab_UsesConfiguredFourDirectionVisual(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            var visual = prefab.GetComponent<FourDirectionSpriteVisual>();
            Assert.That(visual, Is.Not.Null, prefabPath);
            Assert.That(visual.TargetRenderer, Is.Not.Null, prefabPath);
            Assert.That(visual.TargetRenderer.name, Is.EqualTo("Sprite"), prefabPath);
            Assert.That(visual.DownRightSprite, Is.Not.Null, prefabPath);
            Assert.That(visual.UpLeftSprite, Is.Not.Null, prefabPath);
            AssertRuntimeSpriteContract(visual.DownRightSprite, prefabPath);
            AssertRuntimeSpriteContract(visual.UpLeftSprite, prefabPath);
        }

        [Test]
        public void GoatPrefabs_ShareTheSameDirectionalSpritePair()
        {
            var goatVisuals = PureRunPrefabPaths
                .Where(path => path.Contains("PureRunGoat", System.StringComparison.Ordinal))
                .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<FourDirectionSpriteVisual>())
                .ToList();

            Assert.That(goatVisuals, Has.Count.EqualTo(6));
            Assert.That(goatVisuals.All(visual => visual.DownRightSprite == goatVisuals[0].DownRightSprite), Is.True);
            Assert.That(goatVisuals.All(visual => visual.UpLeftSprite == goatVisuals[0].UpLeftSprite), Is.True);
            Assert.That(goatVisuals.All(visual => visual.DeathSprite == goatVisuals[0].DeathSprite), Is.True);
            Assert.That(goatVisuals[0].DeathSprite, Is.Not.Null);
            AssertDeathSpriteContract(goatVisuals[0].DeathSprite, "Goat prefabs");
            Assert.That(goatVisuals.Select(visual => visual.TargetRenderer.sharedMaterial).Distinct().Count(),
                Is.EqualTo(6), "Goat role prefabs should keep distinct body-tint materials.");
        }

        [TestCase(HunterPrefabPath, "Assets/Tactics/Arts/PureRun/Textures/doge_hunter_death.png")]
        [TestCase(NecromancerPrefabPath, "Assets/Tactics/Arts/PureRun/Textures/doge_necromancer_death.png")]
        [TestCase(MagePrefabPath, "Assets/Tactics/Arts/PureRun/Textures/doge_mage_death.png")]
        public void CorpseProducingPlayerPrefab_UsesExpectedDeathSprite(string prefabPath, string expectedSpritePath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var visual = prefab.GetComponent<FourDirectionSpriteVisual>();

            Assert.That(visual.DeathSprite, Is.Not.Null, prefabPath);
            Assert.That(AssetDatabase.GetAssetPath(visual.DeathSprite), Is.EqualTo(expectedSpritePath), prefabPath);
            AssertDeathSpriteContract(visual.DeathSprite, prefabPath);
        }

        [TestCase(SkeletonWarriorPrefabPath)]
        [TestCase(SkeletonMagePrefabPath)]
        [TestCase(FireDemonPrefabPath)]
        public void SummonedPrefab_DoesNotConfigureCorpseSprite(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var visual = prefab.GetComponent<FourDirectionSpriteVisual>();

            Assert.That(visual.DeathSprite, Is.Null, prefabPath);
        }

        private static void AssertRuntimeSpriteContract(Sprite sprite, string prefabPath)
        {
            Assert.That(sprite.texture.width, Is.EqualTo(256), prefabPath);
            Assert.That(sprite.texture.height, Is.EqualTo(256), prefabPath);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(128f), prefabPath);
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(128f, 20f)), prefabPath);
        }

        private static void AssertDeathSpriteContract(Sprite sprite, string context)
        {
            Assert.That(sprite.texture.width, Is.EqualTo(256), context);
            Assert.That(sprite.texture.height, Is.EqualTo(256), context);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(128f), context);
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(128f, 128f)), context);

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter;
            Assert.That(importer, Is.Not.Null, context);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.Tight), context);
        }
    }
}
