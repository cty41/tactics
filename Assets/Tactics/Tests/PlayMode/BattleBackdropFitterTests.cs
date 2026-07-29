using System.Collections;
using NUnit.Framework;
using Tactics.Common.Battle;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public sealed class BattleBackdropFitterTests
    {
        private GameObject _cameraObject;
        private GameObject _backdropObject;

        [TearDown]
        public void TearDown()
        {
            if (_backdropObject != null)
                Object.DestroyImmediate(_backdropObject);

            if (_cameraObject != null)
                Object.DestroyImmediate(_cameraObject);
        }

        [UnityTest]
        public IEnumerator Backdrop_CoversSupportedAspectRatiosAndRefreshesWhenCameraChanges()
        {
            _cameraObject = new GameObject("BattleBackdropTestCamera");
            _cameraObject.tag = "MainCamera";
            Camera camera = _cameraObject.AddComponent<Camera>();
            camera.orthographic = true;

            _backdropObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _backdropObject.name = "BattleBackdropUnderTest";
            _backdropObject.AddComponent<BattleBackdropFitter>();

            float[] aspectRatios = { 16f / 9f, 16f / 10f, 21f / 9f };
            float[] orthographicSizes = { 5f, 6f, 7f };

            for (int index = 0; index < aspectRatios.Length; index++)
            {
                camera.aspect = aspectRatios[index];
                camera.orthographicSize = orthographicSizes[index];
                yield return null;

                float expectedHeight = camera.orthographicSize * 2f * 1.02f;
                float expectedWidth = expectedHeight * camera.aspect;
                Assert.That(_backdropObject.transform.localScale.x, Is.EqualTo(expectedWidth).Within(0.001f));
                Assert.That(_backdropObject.transform.localScale.y, Is.EqualTo(expectedHeight).Within(0.001f));
            }
        }
    }
}
