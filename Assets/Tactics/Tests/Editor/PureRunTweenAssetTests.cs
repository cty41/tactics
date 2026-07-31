using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Tween;
using UnityEditor;
using UnityEngine;

namespace Tactics.Tests.Editor
{
    public class PureRunTweenAssetTests
    {
        private const string RuntimeProjectileRoot =
            "Assets/Tactics/Arts/PureRun/Textures/Projectiles";
        private const string ProjectileProfileRoot =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles";
        private const string GlowOverlayShaderName = "Tactics/PureRun/GlowOverlay";
        private const string GlowOverlayMaterialPath =
            "Assets/Tactics/Arts/PureRun/Tween/PureRunGlowOverlay.mat";

        private static readonly string[] PrefabPaths =
        {
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunHunter.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunNecromancer.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunMage.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunSkeletonWarrior.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunSkeletonMage.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunFireDemon.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatCharger.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatEliteCharger.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatRanged.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatAoe.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatElitePoisonCaster.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatSupport.prefab"
        };

        [Test]
        public void StandardGroundPrefabs_ShareConfiguredTweenProfile()
        {
            var expectedProfile = AssetDatabase.LoadAssetAtPath<StandardUnitTweenProfile>(
                "Assets/Tactics/Arts/PureRun/Tween/StandardUnitTweenProfile.asset");
            var expectedGlowMaterial = AssetDatabase.LoadAssetAtPath<Material>(GlowOverlayMaterialPath);
            Assert.That(expectedProfile, Is.Not.Null);
            Assert.That(expectedGlowMaterial, Is.Not.Null);
            Assert.That(expectedGlowMaterial.shader, Is.Not.Null);
            Assert.That(expectedGlowMaterial.shader.name, Is.EqualTo(GlowOverlayShaderName));
            Assert.That(expectedGlowMaterial.shader.isSupported, Is.True);

            foreach (string path in PrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var visual = prefab.GetComponent<UnitTweenVisual>();
                Assert.That(visual, Is.Not.Null, path);
                Assert.That(visual.Profile, Is.SameAs(expectedProfile), path);
                Assert.That(visual.VisualRoot, Is.Not.Null, path);
                Assert.That(visual.VisualRoot.name, Is.EqualTo("Sprite"), path);
                Assert.That(visual.PrimaryRenderer, Is.Not.Null, path);
                Assert.That(visual.PrimaryRenderer.gameObject.name, Is.EqualTo("Sprite"), path);
                Assert.That(visual.GlowOverlayMaterial, Is.SameAs(expectedGlowMaterial), path);
                Assert.That(visual.PrimaryRenderer.sharedMaterial, Is.Not.SameAs(expectedGlowMaterial), path);
                Assert.That(visual.VisualRoot.Find("GlowOverlay"), Is.Null, path);
            }
        }

        [Test]
        public void ProjectileProfiles_HaveRuntimeSprites()
        {
            string[] profileNames =
            {
                "PhysicalBasic", "MagicBasic", "Fire", "Ice",
                "BoneSpear", "AmazonSpear", "AmazonPoisonSpear"
            };

            foreach (string name in profileNames)
            {
                var profile = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                    $"Assets/Tactics/Arts/PureRun/Tween/Projectiles/{name}.asset");
                Assert.That(profile, Is.Not.Null, name);
                Assert.That(profile.Sprite, Is.Not.Null, name);
                Assert.That(profile.Scale, Is.GreaterThan(0f), name);
            }
        }

        [TestCase(
            "Tools/artworks/doge/concepts/doge_capsule_hunter_spear_projectile_color_v01.png",
            RuntimeProjectileRoot + "/pure_run_spear_projectile.png")]
        [TestCase(
            "Tools/artworks/doge/concepts/doge_capsule_mage_arcane_bolt_projectile_color_v02.png",
            RuntimeProjectileRoot + "/pure_run_arcane_bolt_projectile.png")]
        [TestCase(
            "Tools/artworks/doge/concepts/doge_capsule_necromancer_pale_orb_projectile_color_v03.png",
            RuntimeProjectileRoot + "/pure_run_necromancer_orb_projectile.png")]
        public void RuntimeProjectileTextures_MatchApprovedSourcesAndImportContract(
            string approvedSourcePath,
            string runtimePath)
        {
            Assert.That(File.Exists(approvedSourcePath), Is.True, approvedSourcePath);
            Assert.That(File.Exists(runtimePath), Is.True, runtimePath);
            CollectionAssert.AreEqual(
                File.ReadAllBytes(approvedSourcePath),
                File.ReadAllBytes(runtimePath),
                runtimePath);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(runtimePath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(runtimePath);
            var importer = AssetImporter.GetAtPath(runtimePath) as TextureImporter;
            Assert.That(texture, Is.Not.Null, runtimePath);
            Assert.That(texture.width, Is.EqualTo(256), runtimePath);
            Assert.That(texture.height, Is.EqualTo(256), runtimePath);
            Assert.That(sprite, Is.Not.Null, runtimePath);
            Assert.That(importer, Is.Not.Null, runtimePath);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), runtimePath);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), runtimePath);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(128f), runtimePath);
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.5f, 0.5f)), runtimePath);
            Assert.That(importer.mipmapEnabled, Is.False, runtimePath);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), runtimePath);
        }

        [Test]
        public void ProjectileProfiles_UseApprovedSpriteMappingAndTints()
        {
            AssertProfile("PhysicalBasic", "pure_run_spear_projectile.png");
            AssertProfile("AmazonSpear", "pure_run_spear_projectile.png");
            AssertProfile("AmazonPoisonSpear", "pure_run_spear_projectile.png");
            AssertProfile("MagicBasic", "pure_run_arcane_bolt_projectile.png");
            AssertProfile("Fire", "pure_run_arcane_bolt_projectile.png");
            AssertProfile("Ice", "pure_run_arcane_bolt_projectile.png");
            AssertProfile("BoneSpear", "pure_run_necromancer_orb_projectile.png");

            var normalSpear = LoadProfile("AmazonSpear");
            var poisonSpear = LoadProfile("AmazonPoisonSpear");
            var boneSpear = LoadProfile("BoneSpear");
            Assert.That(normalSpear.Tint, Is.EqualTo(Color.white));
            Assert.That(poisonSpear.Tint.g, Is.GreaterThan(poisonSpear.Tint.r));
            Assert.That(poisonSpear.Tint.g, Is.GreaterThan(poisonSpear.Tint.b));
            Assert.That(boneSpear.Tint, Is.EqualTo(Color.white));
            Assert.That(boneSpear.RotateAlongTangent, Is.True);
        }

        [TestCase("RangedAttack_Graph")]
        [TestCase("MagicAttack_Graph")]
        [TestCase("Fireball_Graph")]
        [TestCase("IceBolt_Graph")]
        [TestCase("BoneSpear_Graph")]
        [TestCase("FireDemonAttack_Graph")]
        public void PublishedRangedGraphs_HaveVisibleProjectileProfile(string graphName)
        {
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphs/{graphName}.asset");
            Assert.That(graph, Is.Not.Null, graphName);
            var projectile = graph.Nodes.OfType<ProjectileLaunchNodeRecord>().Single();
            Assert.That(projectile.VisualProfile, Is.Not.Null, graphName);
            Assert.That(projectile.VisualProfile.Sprite, Is.Not.Null, graphName);
        }

        [TestCase("RangedAttack_Graph")]
        [TestCase("HeavyShot_Graph")]
        [TestCase("MagicAttack_Graph")]
        [TestCase("Fireball_Graph")]
        [TestCase("Fireball_Lv1_Graph")]
        [TestCase("Fireball_Lv2_Graph")]
        [TestCase("Fireball_Lv3_Graph")]
        [TestCase("IceBolt_Graph")]
        [TestCase("IceBolt_Lv2_Graph")]
        [TestCase("IceBolt_Lv3_Graph")]
        [TestCase("BoneSpear_Graph")]
        [TestCase("BoneSpear_Lv2_Graph")]
        [TestCase("BoneSpear_Lv3_Graph")]
        [TestCase("PoisonSpear_Graph")]
        [TestCase("PoisonSpear_Lv2_Graph")]
        [TestCase("PoisonSpear_Lv3_Graph")]
        [TestCase("FireDemonAttack_Graph")]
        public void PublishedProjectileGraphs_HaveSingleLaunchThenOnHit(string graphName)
        {
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphs/{graphName}.asset");
            Assert.That(graph, Is.Not.Null, graphName);
            var projectile = graph.Nodes.OfType<ProjectileLaunchNodeRecord>().Single();
            var onHit = graph.Nodes.OfType<OnHitNodeRecord>().Single();
            Assert.That(projectile.VisualProfile, Is.Not.Null, graphName);
            Assert.That(
                graph.GetEdgesFrom(projectile.NodeId).Select(edge => edge.TargetNodeId),
                Does.Contain(onHit.NodeId),
                graphName);
            Assert.That(graph.GetEdgesFrom(onHit.NodeId), Is.Not.Empty, graphName);
        }

        [TestCase("PoisonSpear_Graph")]
        [TestCase("PoisonSpear_Lv2_Graph")]
        [TestCase("PoisonSpear_Lv3_Graph")]
        public void PoisonSpearGraph_UsesProjectileThenOnHit(string graphName)
        {
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphs/{graphName}.asset");
            Assert.That(graph, Is.Not.Null);
            var projectile = graph.Nodes.OfType<ProjectileLaunchNodeRecord>().Single();
            var onHit = graph.Nodes.OfType<OnHitNodeRecord>().Single();
            var effect = graph.Nodes.OfType<AmazonSkillNodeRecord>().Single();
            Assert.That(projectile.VisualProfile, Is.Not.Null);
            Assert.That(projectile.DropOnHit, Is.False);
            Assert.That(graph.GetEdgesFrom(projectile.NodeId).Select(edge => edge.TargetNodeId),
                Does.Contain(onHit.NodeId));
            Assert.That(graph.GetEdgesFrom(onHit.NodeId).Select(edge => edge.TargetNodeId),
                Does.Contain(effect.NodeId));
        }

        [Test]
        public void AbilityAssets_StoreExplicitVisualActions()
        {
            AssertAction("MeleeAttack_Graph_Ability", UnitVisualAction.Melee);
            AssertAction("RangedAttack_Graph_Ability", UnitVisualAction.Ranged);
            AssertAction("MagicAttack_Graph_Ability", UnitVisualAction.Cast);
            AssertAction("Move_Graph_Ability", UnitVisualAction.None);
        }

        [Test]
        public void PublishedAbilityAssets_DoNotSilentlyFallBackToNone()
        {
            var intentionalNone = new HashSet<string>
            {
                "Move_Graph_Ability",
                "PickupSpear_Graph_Ability"
            };
            string[] guids = AssetDatabase.FindAssets(
                "t:SkillGraphAbilityConfig",
                new[] { "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(path);
                Assert.That(config, Is.Not.Null, path);
                if (intentionalNone.Contains(config.name))
                    Assert.That(config.VisualAction, Is.EqualTo(UnitVisualAction.None), config.name);
                else
                    Assert.That(config.VisualAction, Is.Not.EqualTo(UnitVisualAction.None), config.name);
            }
        }

        private static void AssertAction(string assetName, UnitVisualAction expected)
        {
            var config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/{assetName}.asset");
            Assert.That(config, Is.Not.Null, assetName);
            Assert.That(config.VisualAction, Is.EqualTo(expected), assetName);
        }

        private static ProjectileVisualProfile LoadProfile(string profileName)
        {
            var profile = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                $"{ProjectileProfileRoot}/{profileName}.asset");
            Assert.That(profile, Is.Not.Null, profileName);
            return profile;
        }

        private static void AssertProfile(string profileName, string expectedTextureName)
        {
            var profile = LoadProfile(profileName);
            string spritePath = AssetDatabase.GetAssetPath(profile.Sprite);
            Assert.That(Path.GetFileName(spritePath), Is.EqualTo(expectedTextureName), profileName);
        }
    }
}
