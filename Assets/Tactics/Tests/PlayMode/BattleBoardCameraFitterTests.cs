using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Tactics.Common.Battle;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Tactics.Tests.PlayMode
{
    public sealed class BattleBoardCameraFitterTests
    {
        private GameObject _cameraObject;
        private GameObject _fitterObject;
        private GameObject _gridObject;
        private GameObject _tilemapObject;
        private Texture2D _texture;
        private Sprite _sprite;
        private Tile _tile;

        [TearDown]
        public void TearDown()
        {
            if (_fitterObject != null)
                Object.DestroyImmediate(_fitterObject);
            if (_cameraObject != null)
                Object.DestroyImmediate(_cameraObject);
            if (_gridObject != null)
                Object.DestroyImmediate(_gridObject);
            if (_tilemapObject != null)
                Object.DestroyImmediate(_tilemapObject);
            if (_tile != null)
                Object.DestroyImmediate(_tile);
            if (_sprite != null)
                Object.DestroyImmediate(_sprite);
            if (_texture != null)
                Object.DestroyImmediate(_texture);
        }

        [Test]
        public void Component_CanBeAddedToAGameObject()
        {
            _fitterObject = new GameObject("BattleBoardCameraFitterUnderTest");
            _fitterObject.SetActive(false);

            BattleBoardCameraFitter fitter = _fitterObject.AddComponent<BattleBoardCameraFitter>();

            Assert.That(fitter, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MissingCamera_WarnsOnceAndDoesNotModifyAnUnassignedCamera()
        {
            Camera unassignedCamera = CreateCamera();
            Tilemap tilemap = CreateBoardTilemap();
            Vector3 originalPosition = unassignedCamera.transform.position;
            float originalSize = unassignedCamera.orthographicSize;
            bool originalOrthographic = unassignedCamera.orthographic;

            LogAssert.Expect(
                LogType.Warning,
                new Regex(@".*\[WARNING\] BattleBoardCameraFitter requires a target Camera\..*"));
            CreateConfiguredFitter(null, tilemap, 1f, 1f);
            yield return null;
            yield return null;
            LogAssert.NoUnexpectedReceived();

            Assert.That(unassignedCamera.transform.position, Is.EqualTo(originalPosition));
            Assert.That(unassignedCamera.orthographicSize, Is.EqualTo(originalSize));
            Assert.That(unassignedCamera.orthographic, Is.EqualTo(originalOrthographic));
        }

        [UnityTest]
        public IEnumerator MissingTilemap_WarnsOnceWithoutChangingCameraAndRecoversWhenAssigned()
        {
            Camera camera = CreateCamera();
            Vector3 originalPosition = camera.transform.position;
            float originalSize = camera.orthographicSize;
            bool originalOrthographic = camera.orthographic;

            LogAssert.Expect(
                LogType.Warning,
                new Regex(@".*\[WARNING\] BattleBoardCameraFitter requires a board Tilemap\..*"));
            BattleBoardCameraFitter fitter = CreateConfiguredFitter(camera, null, 1.25f, 0.75f);
            yield return null;
            yield return null;
            LogAssert.NoUnexpectedReceived();

            Assert.That(camera.transform.position, Is.EqualTo(originalPosition));
            Assert.That(camera.orthographicSize, Is.EqualTo(originalSize));
            Assert.That(camera.orthographic, Is.EqualTo(originalOrthographic));

            Tilemap tilemap = CreateBoardTilemap();
            SetField(fitter, "_boardTilemap", tilemap);
            yield return null;

            Bounds boardBounds = tilemap.GetComponent<TilemapRenderer>().bounds;
            float expectedSize = Mathf.Max(
                (boardBounds.size.y + 1.5f) * 0.5f,
                (boardBounds.size.x + 2.5f) / (2f * camera.aspect));
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.transform.position.x, Is.EqualTo(boardBounds.center.x).Within(0.001f));
            Assert.That(camera.transform.position.y, Is.EqualTo(boardBounds.center.y).Within(0.001f));
            Assert.That(camera.transform.position.z, Is.EqualTo(-17f).Within(0.001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(expectedSize).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Camera_FitsPaddedWorldBoundsAtSupportedAspectRatios()
        {
            Camera camera = CreateCamera();
            Tilemap tilemap = CreateBoardTilemap();
            BattleBoardCameraFitter fitter = CreateConfiguredFitter(camera, tilemap, 1.25f, 0.75f);
            float[] aspects = { 16f / 9f, 16f / 10f, 21f / 9f };

            foreach (float aspect in aspects)
            {
                camera.aspect = aspect;
                yield return null;

                Bounds boardBounds = tilemap.GetComponent<TilemapRenderer>().bounds;
                Vector3 expectedCenter = boardBounds.center;
                float paddedWidth = boardBounds.size.x + 2.5f;
                float paddedHeight = boardBounds.size.y + 1.5f;
                float expectedSize = Mathf.Max(paddedHeight * 0.5f, paddedWidth / (2f * aspect));

                Assert.That(camera.orthographic, Is.True);
                Assert.That(camera.transform.position.x, Is.EqualTo(expectedCenter.x).Within(0.001f));
                Assert.That(camera.transform.position.y, Is.EqualTo(expectedCenter.y).Within(0.001f));
                Assert.That(camera.transform.position.z, Is.EqualTo(-17f).Within(0.001f));
                Assert.That(camera.orthographicSize, Is.EqualTo(expectedSize).Within(0.001f));
                AssertPaddedCornersAreVisible(camera, boardBounds, 1.25f, 0.75f);
            }
        }

        [UnityTest]
        public IEnumerator Camera_RefreshesWhenBoardBoundsAndPaddingChange()
        {
            Camera camera = CreateCamera();
            camera.aspect = 16f / 9f;
            Tilemap tilemap = CreateBoardTilemap();
            BattleBoardCameraFitter fitter = CreateConfiguredFitter(camera, tilemap, 0f, 0f);
            yield return null;

            float originalSize = camera.orthographicSize;
            tilemap.SetTile(new Vector3Int(12, 8, 0), _tile);
            tilemap.RefreshAllTiles();
            SetField(fitter, "_horizontalPadding", 2f);
            SetField(fitter, "_verticalPadding", 1f);
            yield return null;

            Bounds boardBounds = tilemap.GetComponent<TilemapRenderer>().bounds;
            float expectedSize = Mathf.Max(
                (boardBounds.size.y + 2f) * 0.5f,
                (boardBounds.size.x + 4f) / (2f * camera.aspect));
            Assert.That(camera.orthographicSize, Is.GreaterThan(originalSize));
            Assert.That(camera.orthographicSize, Is.EqualTo(expectedSize).Within(0.001f));
            Assert.That(camera.transform.position.x, Is.EqualTo(boardBounds.center.x).Within(0.001f));
            Assert.That(camera.transform.position.y, Is.EqualTo(boardBounds.center.y).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Camera_RepairsExternallyMutatedManagedOutputsWithoutInputChange()
        {
            Camera camera = CreateCamera();
            camera.aspect = 16f / 9f;
            Tilemap tilemap = CreateBoardTilemap();
            CreateConfiguredFitter(camera, tilemap, 1.25f, 0.75f);
            yield return null;

            Bounds boardBounds = tilemap.GetComponent<TilemapRenderer>().bounds;
            float expectedSize = Mathf.Max(
                (boardBounds.size.y + 1.5f) * 0.5f,
                (boardBounds.size.x + 2.5f) / (2f * camera.aspect));

            camera.orthographic = false;
            yield return null;
            Assert.That(camera.orthographic, Is.True);

            camera.transform.position = new Vector3(99f, -77f, -17f);
            yield return null;
            Assert.That(camera.transform.position.x, Is.EqualTo(boardBounds.center.x).Within(0.001f));
            Assert.That(camera.transform.position.y, Is.EqualTo(boardBounds.center.y).Within(0.001f));
            Assert.That(camera.transform.position.z, Is.EqualTo(-17f).Within(0.001f));

            camera.orthographicSize = expectedSize + 3f;
            yield return null;
            Assert.That(camera.orthographicSize, Is.EqualTo(expectedSize).Within(0.001f));
        }

        private Camera CreateCamera()
        {
            _cameraObject = new GameObject("BattleBoardCameraFitterTestCamera");
            _cameraObject.transform.position = new Vector3(100f, -100f, -17f);
            Camera camera = _cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = false;
            return camera;
        }

        private Tilemap CreateBoardTilemap()
        {
            _gridObject = new GameObject("BattleBoardCameraFitterTestGrid");
            _gridObject.AddComponent<Grid>();
            _tilemapObject = new GameObject("BattleBoardCameraFitterTestTilemap");
            _tilemapObject.transform.SetParent(_gridObject.transform, false);
            Tilemap tilemap = _tilemapObject.AddComponent<Tilemap>();
            _tilemapObject.AddComponent<TilemapRenderer>();
            _texture = new Texture2D(1, 1);
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _tile = ScriptableObject.CreateInstance<Tile>();
            _tile.sprite = _sprite;
            tilemap.SetTile(new Vector3Int(-2, -1, 0), _tile);
            tilemap.SetTile(new Vector3Int(7, 4, 0), _tile);
            tilemap.RefreshAllTiles();
            return tilemap;
        }

        private BattleBoardCameraFitter CreateConfiguredFitter(
            Camera camera,
            Tilemap tilemap,
            float horizontalPadding,
            float verticalPadding)
        {
            _fitterObject = new GameObject("BattleBoardCameraFitterUnderTest");
            _fitterObject.SetActive(false);
            BattleBoardCameraFitter fitter = _fitterObject.AddComponent<BattleBoardCameraFitter>();
            SetField(fitter, "_targetCamera", camera);
            SetField(fitter, "_boardTilemap", tilemap);
            SetField(fitter, "_horizontalPadding", horizontalPadding);
            SetField(fitter, "_verticalPadding", verticalPadding);
            _fitterObject.SetActive(true);
            return fitter;
        }

        private static void SetField<T>(BattleBoardCameraFitter fitter, string fieldName, T value)
        {
            FieldInfo field = typeof(BattleBoardCameraFitter).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{fieldName}'.");
            field.SetValue(fitter, value);
        }

        private static void AssertPaddedCornersAreVisible(
            Camera camera,
            Bounds boardBounds,
            float horizontalPadding,
            float verticalPadding)
        {
            float minX = boardBounds.min.x - horizontalPadding;
            float maxX = boardBounds.max.x + horizontalPadding;
            float minY = boardBounds.min.y - verticalPadding;
            float maxY = boardBounds.max.y + verticalPadding;
            Vector3[] corners =
            {
                new Vector3(minX, minY, 0f),
                new Vector3(minX, maxY, 0f),
                new Vector3(maxX, minY, 0f),
                new Vector3(maxX, maxY, 0f),
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                Assert.That(viewport.x, Is.InRange(-0.001f, 1.001f));
                Assert.That(viewport.y, Is.InRange(-0.001f, 1.001f));
            }
        }
    }
}
