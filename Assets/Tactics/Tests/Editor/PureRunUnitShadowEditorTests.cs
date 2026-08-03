using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using Tactics.Units;
using UnityEditor;
using UnityEngine;

namespace Tactics.Tests.Editor
{
    public sealed class PureRunUnitShadowEditorTests
    {
        private const string ApprovedSourcePath =
            "Tools/artworks/pure_run/shadows/approved/pure_run_unit_shadow_1x1_v01.png";
        private const string RuntimeShadowPath =
            "Assets/Tactics/Arts/PureRun/Textures/pure_run_unit_shadow_1x1_v01.png";
        private const string RuntimeShadowMaterialPath =
            "Assets/Tactics/Arts/PureRun/Materials/PureRunUnitShadow.mat";
        private const string PureRunUnitRoot = "Assets/Tactics/Arts/PureRun/Prefabs/Units";
        private const string FighterPrefabPath = "Assets/Tactics/Arts/Prefabs/Units/Fighter.prefab";
        private const string ExpectedSha256 =
            "c232948d6631bb8f88d1c7476f33fde923b5dd9fb7ac9d6793cb84fdd58e9e83";
        private const int MinimumPureRunUnitCount = 12;
        private const float ExpectedGroundShadowFootOffset = -0.03f;

        [Test]
        public void RuntimeShadow_MatchesApprovedSourceAndTextureContract()
        {
            Assert.That(File.Exists(ApprovedSourcePath), Is.True, ApprovedSourcePath);
            Assert.That(File.Exists(RuntimeShadowPath), Is.True, RuntimeShadowPath);

            byte[] approvedBytes = File.ReadAllBytes(ApprovedSourcePath);
            byte[] runtimeBytes = File.ReadAllBytes(RuntimeShadowPath);
            CollectionAssert.AreEqual(approvedBytes, runtimeBytes, RuntimeShadowPath);
            Assert.That(ComputeSha256(runtimeBytes), Is.EqualTo(ExpectedSha256));

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(RuntimeShadowPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RuntimeShadowPath);
            var importer = AssetImporter.GetAtPath(RuntimeShadowPath) as TextureImporter;
            Assert.That(texture, Is.Not.Null, RuntimeShadowPath);
            Assert.That(sprite, Is.Not.Null, RuntimeShadowPath);
            Assert.That(importer, Is.Not.Null, RuntimeShadowPath);

            Assert.That(texture.width, Is.EqualTo(64));
            Assert.That(texture.height, Is.EqualTo(32));
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(64f));
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.isReadable, Is.False);
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(64f, 32f)));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(32f, 16f)));
            Assert.That(sprite.bounds.size.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(sprite.bounds.size.y, Is.EqualTo(0.5f).Within(0.0001f));

            var importerSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(importerSettings);
            Assert.That(importerSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(importerSettings.spriteGenerateFallbackPhysicsShape, Is.False);

            AssertPngPixelContract(approvedBytes);
        }

        [Test]
        public void FighterPrefab_UsesGroundShadowContract()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FighterPrefabPath);
            Assert.That(prefab, Is.Not.Null, FighterPrefabPath);

            AssertShadowContract(prefab, FighterPrefabPath, false);
        }

        [Test]
        public void TilemapUnitVisualLayout_KeepsShadowAnchoredToTileLandingPoint()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FighterPrefabPath);
            Assert.That(prefab, Is.Not.Null, FighterPrefabPath);

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var unit = instance.GetComponent<TilemapUnit>();
                Transform sprite = instance.transform.Find("Sprite");
                Transform shadow = instance.transform.Find("Shadow");
                MethodInfo applyVisualYOffset = typeof(TilemapUnit).GetMethod(
                    "ApplyVisualYOffset",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(unit, Is.Not.Null);
                Assert.That(sprite, Is.Not.Null);
                Assert.That(shadow, Is.Not.Null);
                Assert.That(applyVisualYOffset, Is.Not.Null);

                sprite.localPosition = Vector3.zero;
                shadow.localPosition = new Vector3(0f, ExpectedGroundShadowFootOffset, 0f);
                applyVisualYOffset.Invoke(unit, null);

                Assert.That(sprite.localPosition.y, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(shadow.localPosition,
                    Is.EqualTo(new Vector3(0f, ExpectedGroundShadowFootOffset, 0f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PureRunUnitPrefabs_UseMovementSpecificShadowContract()
        {
            IReadOnlyList<(string Path, GameObject Prefab)> units = AssetDatabase
                .FindAssets("t:Prefab", new[] { PureRunUnitRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => (Path: path, Prefab: AssetDatabase.LoadAssetAtPath<GameObject>(path)))
                .Where(item => item.Prefab != null && item.Prefab.GetComponent<TilemapUnit>() != null)
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .ToList();

            Assert.That(units.Count, Is.GreaterThanOrEqualTo(MinimumPureRunUnitCount),
                "Every Pure Run unit prefab under the directory must enter the shadow contract check.");

            foreach ((string path, GameObject prefab) in units)
            {
                bool hasLandRules = prefab.GetComponent<LandUnitMovementRules>() != null;
                bool hasAirRules = prefab.GetComponent<AirUnitMovementRules>() != null;
                Assert.That(hasLandRules ^ hasAirRules, Is.True,
                    $"{path} must use exactly one supported movement rule type.");

                AssertShadowContract(prefab, path, hasAirRules);
            }
        }

        private static void AssertPngPixelContract(byte[] pngBytes)
        {
            Assert.That(pngBytes, Has.Length.GreaterThan(25));
            Assert.That(pngBytes[24], Is.EqualTo(8), "PNG must use 8-bit channels.");
            Assert.That(pngBytes[25], Is.EqualTo(6), "PNG IHDR color type must be RGBA.");

            var probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(probe.LoadImage(pngBytes, false), Is.True);
                Assert.That(probe.width, Is.EqualTo(64));
                Assert.That(probe.height, Is.EqualTo(32));

                Color32[] pixels = probe.GetPixels32();
                int[] cornerIndices = { 0, probe.width - 1, (probe.height - 1) * probe.width, pixels.Length - 1 };
                Assert.That(cornerIndices.Select(index => pixels[index].a), Is.All.Zero,
                    "All four corners must remain fully transparent.");

                int minX = probe.width;
                int maxX = -1;
                int minY = probe.height;
                int maxY = -1;
                int maximumAlpha = 0;
                double alphaSum = 0d;
                double weightedX = 0d;
                double weightedY = 0d;

                for (int y = 0; y < probe.height; y++)
                {
                    for (int x = 0; x < probe.width; x++)
                    {
                        Color32 pixel = pixels[(y * probe.width) + x];
                        if (pixel.a == 0)
                            continue;

                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                        maximumAlpha = Math.Max(maximumAlpha, pixel.a);
                        alphaSum += pixel.a;
                        weightedX += x * pixel.a;
                        weightedY += y * pixel.a;

                        bool greenKeyResidue = pixel.g > pixel.r + 16 && pixel.g > pixel.b + 16;
                        Assert.That(greenKeyResidue, Is.False,
                            $"Green-key residue at ({x}, {y}): ({pixel.r}, {pixel.g}, {pixel.b}, {pixel.a}).");
                    }
                }

                Assert.That(maxX - minX + 1, Is.EqualTo(38));
                Assert.That(maxY - minY + 1, Is.EqualTo(12));
                Assert.That(maximumAlpha, Is.EqualTo(115));
                Assert.That(weightedX / alphaSum, Is.EqualTo(31.5d).Within(1d));
                Assert.That(weightedY / alphaSum, Is.EqualTo(15.5d).Within(1d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        private static void AssertShadowContract(GameObject prefab, string context, bool isAirUnit)
        {
            Sprite expectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RuntimeShadowPath);
            Material expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RuntimeShadowMaterialPath);
            Assert.That(expectedSprite, Is.Not.Null, RuntimeShadowPath);
            Assert.That(expectedMaterial, Is.Not.Null, RuntimeShadowMaterialPath);

            SpriteRenderer[] shadows = prefab
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.name == "Shadow")
                .ToArray();
            Assert.That(shadows, Has.Length.EqualTo(1), context);

            SpriteRenderer shadow = shadows[0];
            var serializedUnit = new SerializedObject(prefab.GetComponent<TilemapUnit>());
            SerializedProperty shadowFootOffset = serializedUnit.FindProperty("_shadowFootOffset");
            Assert.That(shadowFootOffset, Is.Not.Null, context);

            Vector3 shadowRootPosition = prefab.transform.InverseTransformPoint(shadow.transform.position);
            float expectedScale = isAirUnit ? 0.75f : 1f;
            float expectedAlpha = isAirUnit ? 0.60f : 1f;
            Assert.That(shadow.sprite, Is.SameAs(expectedSprite), context);
            Assert.That(shadow.sharedMaterial, Is.SameAs(expectedMaterial), context);
            Assert.That(shadow.gameObject.activeSelf, Is.True, context);
            Assert.That(shadow.enabled, Is.True, context);
            Assert.That(shadow.forceRenderingOff, Is.False, context);
            Assert.That(shadowFootOffset.floatValue,
                Is.EqualTo(ExpectedGroundShadowFootOffset).Within(0.001f), context);
            Assert.That(shadowRootPosition.x, Is.Zero.Within(0.001f), context);
            Assert.That(shadowRootPosition.y,
                Is.EqualTo(shadowFootOffset.floatValue).Within(0.001f), context);
            Assert.That(shadowRootPosition.z, Is.Zero.Within(0.001f), context);
            Assert.That(shadow.transform.localScale.x, Is.EqualTo(expectedScale).Within(0.001f), context);
            Assert.That(shadow.transform.localScale.y, Is.EqualTo(expectedScale).Within(0.001f), context);
            Assert.That(shadow.transform.localScale.z, Is.EqualTo(expectedScale).Within(0.001f), context);
            Assert.That(shadow.color.r, Is.EqualTo(1f).Within(0.001f), context);
            Assert.That(shadow.color.g, Is.EqualTo(1f).Within(0.001f), context);
            Assert.That(shadow.color.b, Is.EqualTo(1f).Within(0.001f), context);
            Assert.That(shadow.color.a, Is.EqualTo(expectedAlpha).Within(0.001f), context);
            Assert.That(shadow.sortingOrder, Is.EqualTo(3), context);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using var sha256 = SHA256.Create();
            return string.Concat(sha256.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }
    }
}
