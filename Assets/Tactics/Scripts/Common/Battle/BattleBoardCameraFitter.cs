using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// Fits an orthographic camera to the complete world-space render bounds of a battle board.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleBoardCameraFitter : MonoBehaviour
    {
        [Header("Board Camera")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private Tilemap _boardTilemap;

        [Header("World-Space Safety Padding")]
        [SerializeField, Min(0f)] private float _horizontalPadding;
        [SerializeField, Min(0f)] private float _verticalPadding;

        private Camera _lastCamera;
        private Tilemap _lastTilemap;
        private Bounds _lastBoardBounds;
        private float _lastAspect = float.NaN;
        private float _lastHorizontalPadding = float.NaN;
        private float _lastVerticalPadding = float.NaN;
        private bool _hasWarnedMissingCamera;
        private bool _hasWarnedMissingTilemap;

        private void OnEnable()
        {
            InvalidateLayout();
            RefreshLayout();
        }

        private void LateUpdate()
        {
            RefreshLayout();
        }

        private void OnValidate()
        {
            _horizontalPadding = Mathf.Max(0f, _horizontalPadding);
            _verticalPadding = Mathf.Max(0f, _verticalPadding);
            InvalidateLayout();
        }

        private void RefreshLayout()
        {
            if (_targetCamera == null)
            {
                if (!_hasWarnedMissingCamera)
                {
                    TLog.Warning("BattleBoardCameraFitter requires a target Camera.", this);
                    _hasWarnedMissingCamera = true;
                }

                return;
            }

            _hasWarnedMissingCamera = false;
            if (_boardTilemap == null)
            {
                if (!_hasWarnedMissingTilemap)
                {
                    TLog.Warning("BattleBoardCameraFitter requires a board Tilemap.", this);
                    _hasWarnedMissingTilemap = true;
                }

                return;
            }

            _hasWarnedMissingTilemap = false;
            Renderer boardRenderer = _boardTilemap.GetComponent<Renderer>();
            if (boardRenderer == null || _targetCamera.aspect <= 0f)
                return;

            Bounds boardBounds = boardRenderer.bounds;
            float paddedWidth = boardBounds.size.x + 2f * _horizontalPadding;
            float paddedHeight = boardBounds.size.y + 2f * _verticalPadding;
            float requiredHalfHeight = paddedHeight * 0.5f;
            float requiredHalfWidthAsHeight = paddedWidth / (2f * _targetCamera.aspect);
            float expectedOrthographicSize = Mathf.Max(requiredHalfHeight, requiredHalfWidthAsHeight);
            Vector3 cameraPosition = _targetCamera.transform.position;
            bool managedOutputsMatch = _targetCamera.orthographic
                && Mathf.Approximately(cameraPosition.x, boardBounds.center.x)
                && Mathf.Approximately(cameraPosition.y, boardBounds.center.y)
                && Mathf.Approximately(_targetCamera.orthographicSize, expectedOrthographicSize);
            if (_targetCamera == _lastCamera
                && _boardTilemap == _lastTilemap
                && boardBounds == _lastBoardBounds
                && Mathf.Approximately(_targetCamera.aspect, _lastAspect)
                && Mathf.Approximately(_horizontalPadding, _lastHorizontalPadding)
                && Mathf.Approximately(_verticalPadding, _lastVerticalPadding)
                && managedOutputsMatch)
            {
                return;
            }

            _targetCamera.orthographic = true;
            _targetCamera.transform.position = new Vector3(boardBounds.center.x, boardBounds.center.y, cameraPosition.z);
            _targetCamera.orthographicSize = expectedOrthographicSize;
            _lastCamera = _targetCamera;
            _lastTilemap = _boardTilemap;
            _lastBoardBounds = boardBounds;
            _lastAspect = _targetCamera.aspect;
            _lastHorizontalPadding = _horizontalPadding;
            _lastVerticalPadding = _verticalPadding;
        }

        private void InvalidateLayout()
        {
            _lastCamera = null;
            _lastTilemap = null;
            _lastAspect = float.NaN;
            _lastHorizontalPadding = float.NaN;
            _lastVerticalPadding = float.NaN;
        }
    }
}
