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
        private const string GlowOverlayMaterialPath =
            "Assets/Tactics/Arts/PureRun/Tween/PureRunGlowOverlay.mat";
        private const string GlowOverlayShaderPath =
            "Assets/Tactics/Arts/PureRun/Shaders/PureRunGlowOverlay.shader";
        private const string AmazonActionRoot =
            "Assets/Tactics/Arts/PureRun/Textures/Actions/Amazon";
        private const string AmazonPoseRoot =
            "Assets/Tactics/Arts/PureRun/Tween/ActionPoses";

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
            Assert.That(expectedProfile, Is.Not.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(GlowOverlayMaterialPath), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(GlowOverlayShaderPath), Is.Null);

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
                Assert.That(visual.VisualRoot.Find("GlowOverlay"), Is.Null, path);
                Assert.That(new SerializedObject(visual).FindProperty("_glowOverlayMaterial"), Is.Null, path);
            }
        }

        [TestCase("idle_unarmed_dr_v02", "idle_unarmed_dr")]
        [TestCase("idle_unarmed_ul_v01", "idle_unarmed_ul")]
        [TestCase("melee_attack_held_dr_v03", "melee_attack_dr")]
        [TestCase("melee_attack_held_ul_v08", "melee_attack_ul")]
        [TestCase("cast_spear_hidden_dr_v02", "cast_dr")]
        [TestCase("cast_spear_hidden_ul_v04", "cast_ul")]
        [TestCase("hit_spear_hidden_dr_v02", "hit_dr")]
        [TestCase("hit_spear_hidden_ul_v02", "hit_ul")]
        public void AmazonActionSprites_MatchApprovedSourceAndImportContract(
            string approvedName,
            string runtimeName)
        {
            string sourcePath = $"Tools/artworks/doge/candidates/doge_hunter_{approvedName}.png";
            string runtimePath = $"{AmazonActionRoot}/doge_hunter_{runtimeName}.png";
            CollectionAssert.AreEqual(File.ReadAllBytes(sourcePath), File.ReadAllBytes(runtimePath), runtimePath);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(runtimePath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(runtimePath);
            var importer = AssetImporter.GetAtPath(runtimePath) as TextureImporter;
            Assert.That(texture, Is.Not.Null, runtimePath);
            Assert.That(texture.width, Is.EqualTo(256), runtimePath);
            Assert.That(texture.height, Is.EqualTo(256), runtimePath);
            Assert.That(sprite, Is.Not.Null, runtimePath);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(128f), runtimePath);
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(128f, 20f)), runtimePath);
            Assert.That(importer, Is.Not.Null, runtimePath);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), runtimePath);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), runtimePath);
            Assert.That(importer.mipmapEnabled, Is.False, runtimePath);
            Assert.That(importer.alphaIsTransparency, Is.True, runtimePath);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), runtimePath);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed), runtimePath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.Tight), runtimePath);
        }

        [Test]
        public void AmazonActionPoseAssets_UseApprovedMinimalMapping()
        {
            UnitPoseFamily melee = LoadPoseFamily("MeleeAttack");
            UnitPoseFamily thrown = LoadPoseFamily("ThrownAttack");
            UnitPoseFamily cast = LoadPoseFamily("Cast");
            UnitPoseFamily hit = LoadPoseFamily("Hit");
            var profile = AssetDatabase.LoadAssetAtPath<UnitActionPoseProfile>(
                $"{AmazonPoseRoot}/AmazonActionPoseProfile.asset");

            Assert.That(melee.ExitPolicy, Is.EqualTo(UnitPoseExitPolicy.RecoveryStart));
            Assert.That(thrown.ExitPolicy, Is.EqualTo(UnitPoseExitPolicy.Release));
            Assert.That(cast.ExitPolicy, Is.EqualTo(UnitPoseExitPolicy.RecoveryStart));
            Assert.That(hit.ExitPolicy, Is.EqualTo(UnitPoseExitPolicy.RecoveryStart));
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.ResolveFamily(UnitVisualAction.Melee), Is.SameAs(melee));
            Assert.That(profile.ResolveFamily(UnitVisualAction.Ranged), Is.SameAs(thrown));
            Assert.That(profile.ResolveFamily(UnitVisualAction.Cast), Is.SameAs(cast));
            Assert.That(profile.HitFamily, Is.SameAs(hit));

            AssertIdle(profile, UnitVisualState.Default, "doge_hunter.png", "doge_hunter_ul.png");
            AssertIdle(profile, UnitVisualState.Unarmed,
                "doge_hunter_idle_unarmed_dr.png", "doge_hunter_idle_unarmed_ul.png");
            AssertPose(profile, melee, UnitVisualState.Default,
                "doge_hunter_melee_attack_dr.png", "doge_hunter_melee_attack_ul.png");
            AssertPose(profile, thrown, UnitVisualState.Default,
                "doge_hunter_melee_attack_dr.png", "doge_hunter_melee_attack_ul.png");
            AssertPose(profile, cast, UnitVisualState.Default,
                "doge_hunter_cast_dr.png", "doge_hunter_cast_ul.png");
            AssertPose(profile, cast, UnitVisualState.Unarmed,
                "doge_hunter_cast_dr.png", "doge_hunter_cast_ul.png");
            AssertPose(profile, hit, UnitVisualState.Default,
                "doge_hunter_hit_dr.png", "doge_hunter_hit_ul.png");
            AssertPose(profile, hit, UnitVisualState.Unarmed,
                "doge_hunter_hit_dr.png", "doge_hunter_hit_ul.png");

            GameObject hunter = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[0]);
            Assert.That(hunter.GetComponent<Tactics.Common.Units.FourDirectionSpriteVisual>().ActionPoseProfile,
                Is.SameAs(profile));
        }

        [TestCase("PoisonSpear_Graph_Ability")]
        [TestCase("PoisonSpear_Lv2_Graph_Ability")]
        [TestCase("PoisonSpear_Lv3_Graph_Ability")]
        public void PoisonSpearAbilities_ExplicitlyUseThrownPose(string abilityName)
        {
            SkillGraphAbilityConfig config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/{abilityName}.asset");
            Assert.That(config.PoseFamily, Is.SameAs(LoadPoseFamily("ThrownAttack")), abilityName);
        }

        [Test]
        public void ProjectileProfiles_HaveRuntimeSprites()
        {
            string[] profileNames =
            {
                "PhysicalBasic", "MagicBasic", "Ice",
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
        [TestCase(
            "Tools/artworks/doge/concepts/doge_capsule_necromancer_bone_spear_projectile_color_v01.png",
            RuntimeProjectileRoot + "/pure_run_bone_spear_projectile.png")]
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
            AssertProfile("Ice", "pure_run_arcane_bolt_projectile.png");
            AssertProfile("BoneSpear", "pure_run_bone_spear_projectile.png");

            var fire = LoadProfile("Fire");
            Assert.That(fire.VisualKind, Is.EqualTo(ProjectileVisualKind.SoftDisc));
            Assert.That(fire.Sprite, Is.Null);
            Assert.That(fire.Material, Is.Not.Null);
            Assert.That(fire.Material.shader.name, Is.EqualTo("Tactics/PureRun/SkillVfxPrimitive"));
            Assert.That(fire.Scale, Is.EqualTo(0.17f).Within(0.001f));
            Assert.That(fire.PulseAmount, Is.EqualTo(0.06f).Within(0.001f));
            Assert.That(fire.ParticleTrail.Enabled, Is.True);
            Assert.That(fire.ParticleTrail.MaximumParticles, Is.EqualTo(3));

            var normalSpear = LoadProfile("AmazonSpear");
            var poisonSpear = LoadProfile("AmazonPoisonSpear");
            var boneSpear = LoadProfile("BoneSpear");
            Assert.That(normalSpear.Tint, Is.EqualTo(Color.white));
            Assert.That(poisonSpear.Tint.g, Is.GreaterThan(poisonSpear.Tint.r));
            Assert.That(poisonSpear.Tint.g, Is.GreaterThan(poisonSpear.Tint.b));
            Assert.That(boneSpear.VisualKind, Is.EqualTo(ProjectileVisualKind.Sprite));
            Assert.That(boneSpear.Material, Is.Null,
                "Sprite projectiles must retain SpriteRenderer's compatible default material.");
            Assert.That(boneSpear.Tint, Is.EqualTo(Color.white));
            Assert.That(boneSpear.RotateAlongTangent, Is.True);
            Assert.That(boneSpear.Scale, Is.EqualTo(1f).Within(0.001f));
            Assert.That(boneSpear.PulseAmount, Is.Zero.Within(0.001f));
            Assert.That(boneSpear.ParticleTrail.Enabled, Is.False);
            Assert.That(boneSpear.GhostTrail.Enabled, Is.True);
            Assert.That(boneSpear.GhostTrail.SampleInterval, Is.EqualTo(0.055f).Within(0.001f));
            Assert.That(boneSpear.GhostTrail.Lifetime, Is.EqualTo(0.12f).Within(0.001f));
            Assert.That(boneSpear.GhostTrail.Alpha, Is.EqualTo(0.28f).Within(0.001f));
            Assert.That(boneSpear.GhostTrail.Scale, Is.EqualTo(0.92f).Within(0.001f));
            Assert.That(boneSpear.GhostTrail.MaximumAlive, Is.EqualTo(2));
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
            if (projectile.VisualProfile.VisualKind == ProjectileVisualKind.SoftDisc)
                Assert.That(projectile.VisualProfile.Material, Is.Not.Null, graphName);
            else
                Assert.That(projectile.VisualProfile.Sprite, Is.Not.Null, graphName);
        }

        [Test]
        public void SkillVfxRecipes_UseFinitePrimitivesAndAuthoredBlockingMarkers()
        {
            SkillVfxRecipe defaultCast = LoadRecipe("DefaultCastSkillVfxRecipe");
            SkillVfxRecipe fireball = LoadRecipe("FireballSkillVfxRecipe");
            SkillVfxRecipe boneSpear = LoadRecipe("BoneSpearSkillVfxRecipe");
            SkillVfxRecipe thrust = LoadRecipe("ThrustSkillVfxRecipe");

            foreach (SkillVfxRecipe recipe in new[] { defaultCast, fireball, boneSpear })
            {
                SkillVfxPrimitiveLayer castCharge = recipe.GetLayers(SkillVfxCueKind.CastCharge).Single();
                Assert.That(castCharge.PrimitiveKind, Is.EqualTo(SkillVfxPrimitiveKind.RadialRing));
                Assert.That(castCharge.BlendMode, Is.EqualTo(SkillVfxBlendMode.Additive));
                Assert.That(castCharge.StartSize, Is.EqualTo(0.22f).Within(0.001f));
                Assert.That(castCharge.PeakSize, Is.EqualTo(0.42f).Within(0.001f));
                Assert.That(castCharge.EndSize, Is.EqualTo(0.48f).Within(0.001f));
                Assert.That(castCharge.PeakTime, Is.EqualTo(0.28f).Within(0.001f));
                Assert.That(castCharge.Duration, Is.EqualTo(0.54f).Within(0.001f));
                Assert.That(castCharge.PeakAlpha, Is.EqualTo(0.36f).Within(0.001f));
                Assert.That(castCharge.BlockingMarker, Is.Zero);
                Assert.That(castCharge.RadialInner, Is.EqualTo(0.72f).Within(0.001f));
                Assert.That(castCharge.Softness, Is.EqualTo(0.12f).Within(0.001f));
                Assert.That(castCharge.Emission, Is.EqualTo(0.7f).Within(0.001f));
                Assert.That(castCharge.MaximumInstances, Is.EqualTo(1));
                Assert.That(castCharge.SortingOrderOffset, Is.EqualTo(-2));
            }

            AssertColor(defaultCast, new Color(0.32f, 0.62f, 0.85f, 1f));
            AssertColor(fireball, new Color(1.00f, 0.36f, 0.08f, 1f));
            AssertColor(boneSpear, new Color(0.68f, 0.90f, 0.88f, 1f));

            Assert.That(
                fireball.GetLayers(SkillVfxCueKind.ProjectileImpact).Max(layer => layer.BlockingMarker),
                Is.EqualTo(0.10f).Within(0.001f));
            Assert.That(
                fireball.GetLayers(SkillVfxCueKind.ConditionalDetonation).Max(layer => layer.BlockingMarker),
                Is.EqualTo(0.06f).Within(0.001f));
            Assert.That(
                boneSpear.GetLayers(SkillVfxCueKind.PrimaryTargetHit).Max(layer => layer.BlockingMarker),
                Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(
                thrust.GetLayers(SkillVfxCueKind.DirectionalStrike).Max(layer => layer.BlockingMarker),
                Is.EqualTo(0.065f).Within(0.001f));

            foreach (SkillVfxRecipe recipe in new[] { defaultCast, fireball, boneSpear, thrust })
            {
                Assert.That(recipe.TransparentMaterial, Is.Not.Null, recipe.name);
                Assert.That(recipe.AdditiveMaterial, Is.Not.Null, recipe.name);
                foreach (SkillVfxCueKind cue in System.Enum.GetValues(typeof(SkillVfxCueKind)))
                {
                    foreach (SkillVfxPrimitiveLayer layer in recipe.GetLayers(cue))
                    {
                        if (layer.PrimitiveKind is SkillVfxPrimitiveKind.ParticleBurst or
                            SkillVfxPrimitiveKind.ProjectileGhostTrail)
                        {
                            Assert.That(layer.BlockingMarker, Is.Zero, $"{recipe.name}/{cue}");
                        }
                    }
                }
            }

            SkillVfxPrimitiveLayer boneParticles = boneSpear
                .GetLayers(SkillVfxCueKind.PrimaryTargetHit)
                .Single(layer => layer.PrimitiveKind == SkillVfxPrimitiveKind.ParticleBurst);
            Assert.That(boneParticles.ParticleCount, Is.EqualTo(2));
            Assert.That(boneParticles.MaximumInstances, Is.EqualTo(4));
        }

        private static void AssertColor(SkillVfxRecipe recipe, Color expected)
        {
            Color actual = recipe.GetLayers(SkillVfxCueKind.CastCharge).Single().Color;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f), recipe.name);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f), recipe.name);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f), recipe.name);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f), recipe.name);
        }

        [Test]
        public void SkillVfxPreviewSampling_UsesAuthoredRuntimeTimelineKeys()
        {
            SkillVfxPrimitiveLayer fireCore = LoadRecipe("FireballSkillVfxRecipe")
                .GetLayers(SkillVfxCueKind.ProjectileImpact)
                .Single(layer => layer.PrimitiveKind == SkillVfxPrimitiveKind.RadialCore);
            Assert.That(SkillVfxPrimitiveBuilder.EvaluatePreviewState(fireCore, 0f).Size,
                Is.EqualTo(0.16f).Within(0.001f));
            Assert.That(SkillVfxPrimitiveBuilder.EvaluatePreviewState(fireCore, 0.04f).Size,
                Is.EqualTo(0.12f).Within(0.001f));
            Assert.That(SkillVfxPrimitiveBuilder.EvaluatePreviewState(fireCore, 0.10f).Size,
                Is.EqualTo(0.22f).Within(0.001f));

            SkillVfxPrimitiveLayer thrustLine = LoadRecipe("ThrustSkillVfxRecipe")
                .GetLayers(SkillVfxCueKind.DirectionalStrike)
                .Where(layer => layer.PrimitiveKind == SkillVfxPrimitiveKind.TaperedLine)
                .OrderByDescending(layer => layer.PeakAlpha)
                .First();
            SkillVfxPrimitivePreviewState contact =
                SkillVfxPrimitiveBuilder.EvaluatePreviewState(thrustLine, 0.065f);
            Assert.That(contact.Size, Is.EqualTo(1f).Within(0.001f));
            Assert.That(contact.Alpha, Is.GreaterThan(0.8f));
            Assert.That(SkillVfxPrimitiveBuilder.EvaluatePreviewState(thrustLine, thrustLine.Duration + 0.01f)
                .IsVisible, Is.False);
        }

        [TestCase("Fireball_Lv1_Ability", "FireballSkillVfxRecipe")]
        [TestCase("Fireball_Lv2_Ability", "FireballSkillVfxRecipe")]
        [TestCase("Fireball_Lv3_Ability", "FireballSkillVfxRecipe")]
        [TestCase("BoneSpear_Graph_Ability", "BoneSpearSkillVfxRecipe")]
        [TestCase("BoneSpear_Lv2_Graph_Ability", "BoneSpearSkillVfxRecipe")]
        [TestCase("BoneSpear_Lv3_Graph_Ability", "BoneSpearSkillVfxRecipe")]
        [TestCase("Thrust_Graph_Ability", "ThrustSkillVfxRecipe")]
        [TestCase("Thrust_Lv2_Graph_Ability", "ThrustSkillVfxRecipe")]
        [TestCase("Thrust_Lv3_Graph_Ability", "ThrustSkillVfxRecipe")]
        [TestCase("MagicAttack_Graph_Ability", "DefaultCastSkillVfxRecipe")]
        public void AbilityAssets_ReferenceSharedFamilyRecipe(string abilityName, string recipeName)
        {
            var config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/{abilityName}.asset");
            Assert.That(config, Is.Not.Null, abilityName);
            Assert.That(config.SkillVfxRecipe, Is.SameAs(LoadRecipe(recipeName)), abilityName);
        }

        [Test]
        public void EveryCastAbility_ResolvesCastChargeRecipe()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:SkillGraphAbilityConfig",
                new[] { "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(path);
                if (config == null || config.VisualAction != UnitVisualAction.Cast)
                    continue;

                Assert.That(config.SkillVfxRecipe, Is.Not.Null, config.name);
                Assert.That(
                    config.SkillVfxRecipe.GetLayers(SkillVfxCueKind.CastCharge),
                    Has.Count.EqualTo(1),
                    config.name);
            }
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

        private static SkillVfxRecipe LoadRecipe(string recipeName)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<SkillVfxRecipe>(
                $"Assets/Tactics/Arts/PureRun/Tween/SkillVfx/Recipes/{recipeName}.asset");
            Assert.That(recipe, Is.Not.Null, recipeName);
            return recipe;
        }

        private static void AssertProfile(string profileName, string expectedTextureName)
        {
            var profile = LoadProfile(profileName);
            string spritePath = AssetDatabase.GetAssetPath(profile.Sprite);
            Assert.That(Path.GetFileName(spritePath), Is.EqualTo(expectedTextureName), profileName);
        }

        private static UnitPoseFamily LoadPoseFamily(string familyName)
        {
            var family = AssetDatabase.LoadAssetAtPath<UnitPoseFamily>(
                $"{AmazonPoseRoot}/{familyName}.asset");
            Assert.That(family, Is.Not.Null, familyName);
            return family;
        }

        private static void AssertIdle(
            UnitActionPoseProfile profile,
            UnitVisualState state,
            string expectedDownRight,
            string expectedUpLeft)
        {
            Assert.That(
                profile.TryResolveIdle(state, out Sprite downRight, out Sprite upLeft),
                Is.True,
                state.ToString());
            Assert.That(Path.GetFileName(AssetDatabase.GetAssetPath(downRight)), Is.EqualTo(expectedDownRight));
            Assert.That(Path.GetFileName(AssetDatabase.GetAssetPath(upLeft)), Is.EqualTo(expectedUpLeft));
        }

        private static void AssertPose(
            UnitActionPoseProfile profile,
            UnitPoseFamily family,
            UnitVisualState state,
            string expectedDownRight,
            string expectedUpLeft)
        {
            Assert.That(profile.TryResolvePose(
                family,
                state,
                out Sprite downRight,
                out Sprite upLeft,
                out UnitPoseResolution resolution), Is.True, $"{family.name}/{state}");
            Assert.That(resolution, Is.EqualTo(UnitPoseResolution.ExactPoseState));
            Assert.That(Path.GetFileName(AssetDatabase.GetAssetPath(downRight)), Is.EqualTo(expectedDownRight));
            Assert.That(Path.GetFileName(AssetDatabase.GetAssetPath(upLeft)), Is.EqualTo(expectedUpLeft));
        }
    }
}
