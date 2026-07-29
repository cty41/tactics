using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// Keeps a battle backdrop quad aligned with and large enough for an orthographic camera.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BattleBackdropFitter : MonoBehaviour
    {
        private const float MinimumOverscan = 1f;
        private const float DepthPadding = 0.01f;

        [Header("Camera")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField, Min(MinimumOverscan)] private float _overscan = 1.02f;
        [SerializeField, Min(0f)] private float _distanceFromCamera = 1f;

        [Header("Rendering")]
        [SerializeField] private MeshRenderer _meshRenderer;

        private Camera _lastCamera;
        private float _lastAspect = float.NaN;
        private float _lastOrthographicSize = float.NaN;
        private Vector3 _lastCameraPosition;
        private Quaternion _lastCameraRotation;
        private bool _missingCameraWarningShown;
        private bool _perspectiveCameraWarningShown;

        private void Awake()
        {
            ResolveRenderer();
        }

        private void OnEnable()
        {
            ResolveRenderer();
            InvalidateLayout();
            RefreshLayout();
        }

        private void LateUpdate()
        {
            RefreshLayout();
        }

        private void OnValidate()
        {
            _overscan = Mathf.Max(MinimumOverscan, _overscan);
            _distanceFromCamera = Mathf.Max(0f, _distanceFromCamera);
            InvalidateLayout();
        }

        private void ResolveRenderer()
        {
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void RefreshLayout()
        {
            Camera camera = ResolveCamera();
            if (camera == null)
            {
                SetRendererEnabled(false);
                WarnMissingCameraOnce();
                return;
            }

            if (!camera.orthographic)
            {
                SetRendererEnabled(false);
                WarnPerspectiveCameraOnce();
                return;
            }

            SetRendererEnabled(true);
            if (!NeedsLayoutRefresh(camera))
                return;

            float distance = Mathf.Clamp(
                _distanceFromCamera,
                camera.nearClipPlane + DepthPadding,
                camera.farClipPlane - DepthPadding);
            float height = camera.orthographicSize * 2f * _overscan;
            float width = height * camera.aspect;

            transform.SetPositionAndRotation(
                camera.transform.position + camera.transform.forward * distance,
                camera.transform.rotation);
            transform.localScale = new Vector3(width, height, 1f);

            CacheLayout(camera);
        }

        private Camera ResolveCamera()
        {
            if (_targetCamera == null)
                _targetCamera = Camera.main;

            return _targetCamera;
        }

        private bool NeedsLayoutRefresh(Camera camera)
        {
            return camera != _lastCamera
                || !Mathf.Approximately(camera.aspect, _lastAspect)
                || !Mathf.Approximately(camera.orthographicSize, _lastOrthographicSize)
                || camera.transform.position != _lastCameraPosition
                || camera.transform.rotation != _lastCameraRotation;
        }

        private void CacheLayout(Camera camera)
        {
            _lastCamera = camera;
            _lastAspect = camera.aspect;
            _lastOrthographicSize = camera.orthographicSize;
            _lastCameraPosition = camera.transform.position;
            _lastCameraRotation = camera.transform.rotation;
        }

        private void InvalidateLayout()
        {
            _lastCamera = null;
            _lastAspect = float.NaN;
            _lastOrthographicSize = float.NaN;
        }

        private void SetRendererEnabled(bool isEnabled)
        {
            ResolveRenderer();
            if (_meshRenderer != null)
                _meshRenderer.enabled = isEnabled;
        }

        private void WarnMissingCameraOnce()
        {
            if (_missingCameraWarningShown)
                return;

            _missingCameraWarningShown = true;
            TLog.Warning("[BattleBackdropFitter] No main camera is available. The battle backdrop is hidden.", this);
        }

        private void WarnPerspectiveCameraOnce()
        {
            if (_perspectiveCameraWarningShown)
                return;

            _perspectiveCameraWarningShown = true;
            TLog.Warning("[BattleBackdropFitter] The selected camera is not orthographic. The battle backdrop is hidden.", this);
        }
    }
}
