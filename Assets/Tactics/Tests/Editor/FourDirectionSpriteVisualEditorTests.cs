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
        private static readonly string[] PureRunPrefabPaths =
        {
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunHunter.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunNecromancer.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunMage.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunSkeletonWarrior.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunSkeletonMage.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunFireDemon.prefab",
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
            var goatSprites = PureRunPrefabPaths
                .Where(path => path.Contains("PureRunGoat", System.StringComparison.Ordinal))
                .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<FourDirectionSpriteVisual>())
                .Select(visual => (visual.DownRightSprite, visual.UpLeftSprite))
                .ToList();

            Assert.That(goatSprites, Has.Count.EqualTo(6));
            Assert.That(goatSprites.All(pair => pair.DownRightSprite == goatSprites[0].DownRightSprite), Is.True);
            Assert.That(goatSprites.All(pair => pair.UpLeftSprite == goatSprites[0].UpLeftSprite), Is.True);
        }

        private static void AssertRuntimeSpriteContract(Sprite sprite, string prefabPath)
        {
            Assert.That(sprite.texture.width, Is.EqualTo(256), prefabPath);
            Assert.That(sprite.texture.height, Is.EqualTo(256), prefabPath);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(128f), prefabPath);
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(128f, 20f)), prefabPath);
        }
    }
}
