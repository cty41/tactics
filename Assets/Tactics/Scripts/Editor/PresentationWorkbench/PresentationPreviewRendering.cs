#if UNITY_EDITOR
using System;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Tactics.EditorTools
{
    internal enum PresentationPreviewRenderState
    {
        Idle,
        Dirty,
        Playing,
        ResizeSuspended,
        Disposed
    }

    internal sealed class PresentationPreviewFrame
    {
        internal PresentationPreviewFrame(RenderTexture texture, long sequence, double sampledAt)
        {
            Texture = texture;
            Sequence = sequence;
            SampledAt = sampledAt;
        }

        internal RenderTexture Texture { get; }
        internal long Sequence { get; }
        internal double SampledAt { get; }
    }

    internal sealed class PresentationPreviewSurface : VisualElement
    {
        private readonly Image _image;
        private readonly Label _resizeOverlay;
        private readonly Label _playbackOverlay;
        private bool _isPlaying;

        internal PresentationPreviewSurface()
        {
            name = "presentation-preview-surface";
            style.flexGrow = 1f;
            style.minHeight = 300f;
            style.backgroundColor = new Color(0.08f, 0.075f, 0.08f, 1f);
            style.position = Position.Relative;

            _image = new Image
            {
                name = "presentation-preview-image",
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            _image.style.position = Position.Absolute;
            _image.style.left = 0f;
            _image.style.right = 0f;
            _image.style.top = 0f;
            _image.style.bottom = 0f;
            Add(_image);

            _resizeOverlay = new Label("Resizing preview...")
            {
                name = "presentation-preview-resize-overlay",
                pickingMode = PickingMode.Ignore
            };
            _resizeOverlay.style.position = Position.Absolute;
            _resizeOverlay.style.left = 0f;
            _resizeOverlay.style.right = 0f;
            _resizeOverlay.style.top = 0f;
            _resizeOverlay.style.bottom = 0f;
            _resizeOverlay.style.unityTextAlign = TextAnchor.MiddleCenter;
            _resizeOverlay.style.backgroundColor = new Color(0.02f, 0.02f, 0.025f, 0.58f);
            _resizeOverlay.style.color = new Color(0.78f, 0.78f, 0.78f, 1f);
            _resizeOverlay.style.display = DisplayStyle.None;
            Add(_resizeOverlay);

            _playbackOverlay = new Label("Paused")
            {
                name = "presentation-preview-playback-overlay",
                pickingMode = PickingMode.Ignore
            };
            _playbackOverlay.style.position = Position.Absolute;
            _playbackOverlay.style.right = 8f;
            _playbackOverlay.style.top = 6f;
            _playbackOverlay.style.paddingLeft = 6f;
            _playbackOverlay.style.paddingRight = 6f;
            _playbackOverlay.style.paddingTop = 2f;
            _playbackOverlay.style.paddingBottom = 2f;
            _playbackOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            _playbackOverlay.style.color = Color.white;
            Add(_playbackOverlay);
        }

        internal Texture ImageTexture => _image.image;
        internal bool IsResizeOverlayVisible => _resizeOverlay.style.display == DisplayStyle.Flex;

        internal void SetFrame(PresentationPreviewFrame frame)
        {
            if (frame?.Texture != null && _image.image != frame.Texture)
                _image.image = frame.Texture;
            _image.MarkDirtyRepaint();
        }

        internal void SetResizeSuspended(bool suspended)
        {
            _resizeOverlay.style.display = suspended ? DisplayStyle.Flex : DisplayStyle.None;
        }

        internal void SetPlaying(bool playing)
        {
            if (_isPlaying == playing)
                return;
            _isPlaying = playing;
            _playbackOverlay.text = playing ? "Playing" : "Paused";
        }

        internal void ClearFrame()
        {
            _image.image = null;
        }
    }

    /// <summary>
    /// Decouples fixed-size preview rendering from EditorWindow repaint and resize events.
    /// </summary>
    internal sealed class PresentationPreviewRenderController : IDisposable
    {
        internal const int TextureWidth = 1280;
        internal const int TextureHeight = 720;
        internal const double PlayingFrameIntervalSeconds = 1d / 30d;
        internal const double ResizeStableSeconds = 0.5d;
        internal const int ResizeStableUpdateCount = 3;

        private readonly Func<double> _timeProvider;
        private readonly Func<Vector2> _windowSizeProvider;
        private readonly Func<bool> _isPlayingProvider;
        private readonly Action<RenderTexture> _renderFrame;
        private readonly Func<RenderTexture> _textureFactory;
        private readonly Action<RenderTexture> _textureDisposer;
        private readonly PresentationPreviewSurface _surface;
        private readonly bool _subscribedToEditorUpdate;
        private RenderTexture _displayTexture;
        private Vector2 _lastWindowSize;
        private double _resizeStableSince;
        private double _lastRenderAt = double.NegativeInfinity;
        private int _stableUpdateCount;
        private int _explicitResizeDepth;
        private bool _waitingForStableSize;
        private bool _dirty = true;
        private bool _disposed;
        private long _frameSequence;

        internal PresentationPreviewRenderController(
            Func<double> timeProvider,
            Func<Vector2> windowSizeProvider,
            Func<bool> isPlayingProvider,
            Action<RenderTexture> renderFrame,
            PresentationPreviewSurface surface = null,
            Func<RenderTexture> textureFactory = null,
            Action<RenderTexture> textureDisposer = null,
            bool subscribeToEditorUpdate = true)
        {
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _windowSizeProvider = windowSizeProvider ?? throw new ArgumentNullException(nameof(windowSizeProvider));
            _isPlayingProvider = isPlayingProvider ?? throw new ArgumentNullException(nameof(isPlayingProvider));
            _renderFrame = renderFrame ?? throw new ArgumentNullException(nameof(renderFrame));
            _surface = surface;
            _textureFactory = textureFactory ?? CreateDisplayTexture;
            _textureDisposer = textureDisposer ?? DisposeDisplayTexture;
            _lastWindowSize = _windowSizeProvider();
            State = PresentationPreviewRenderState.Dirty;
            _subscribedToEditorUpdate = subscribeToEditorUpdate;
            if (_subscribedToEditorUpdate)
                EditorApplication.update += Tick;
        }

        internal PresentationPreviewRenderState State { get; private set; }
        internal PresentationPreviewFrame CurrentFrame { get; private set; }
        internal long RenderCount => _frameSequence;
        internal bool IsResizeSuspended => !_disposed &&
            (_explicitResizeDepth > 0 || _waitingForStableSize);

        internal void RequestRender()
        {
            if (_disposed)
                return;
            _dirty = true;
            if (!IsResizeSuspended)
                State = PresentationPreviewRenderState.Dirty;
        }

        internal void BeginResize()
        {
            if (_disposed)
                return;
            _explicitResizeDepth++;
            EnterResizeSuspension(_timeProvider());
        }

        internal void EndResize()
        {
            if (_disposed || _explicitResizeDepth == 0)
                return;
            _explicitResizeDepth--;
            if (_explicitResizeDepth == 0)
                EnterResizeSuspension(_timeProvider());
        }

        internal void Tick()
        {
            if (_disposed)
                return;

            double now = _timeProvider();
            Vector2 windowSize = _windowSizeProvider();
            if (!IsFinite(windowSize) || !Approximately(windowSize, _lastWindowSize))
            {
                _lastWindowSize = windowSize;
                EnterResizeSuspension(now);
                return;
            }

            if (_explicitResizeDepth > 0)
            {
                SetResizeState(true);
                return;
            }

            if (_waitingForStableSize)
            {
                _stableUpdateCount++;
                if (now - _resizeStableSince < ResizeStableSeconds ||
                    _stableUpdateCount < ResizeStableUpdateCount)
                {
                    SetResizeState(true);
                    return;
                }

                _waitingForStableSize = false;
                _dirty = true;
                SetResizeState(false);
            }

            bool playing = _isPlayingProvider();
            _surface?.SetPlaying(playing);
            bool renderPlayingFrame = playing &&
                now - _lastRenderAt >= PlayingFrameIntervalSeconds;
            if (!_dirty && !renderPlayingFrame)
            {
                State = playing
                    ? PresentationPreviewRenderState.Playing
                    : PresentationPreviewRenderState.Idle;
                return;
            }

            Render(now, playing);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_subscribedToEditorUpdate)
                EditorApplication.update -= Tick;
            _surface?.ClearFrame();
            if (_displayTexture != null)
            {
                _textureDisposer(_displayTexture);
                _displayTexture = null;
            }
            CurrentFrame = null;
            State = PresentationPreviewRenderState.Disposed;
        }

        private void Render(double now, bool playing)
        {
            PresentationPreviewRenderState attemptedState = State;
            try
            {
                _displayTexture ??= _textureFactory();
                _renderFrame(_displayTexture);
                _lastRenderAt = now;
                _dirty = false;
                CurrentFrame = new PresentationPreviewFrame(
                    _displayTexture,
                    ++_frameSequence,
                    now);
                _surface?.SetFrame(CurrentFrame);
                State = playing
                    ? PresentationPreviewRenderState.Playing
                    : PresentationPreviewRenderState.Idle;
            }
            catch (Exception exception)
            {
                _dirty = false;
                State = PresentationPreviewRenderState.Idle;
                TLog.Error(
                    $"[PresentationWorkbench] Interactive preview render failed. " +
                    $"State={attemptedState}, Window={_lastWindowSize.x:0.#}x{_lastWindowSize.y:0.#}, " +
                    $"Texture={_displayTexture?.GetInstanceID().ToString() ?? "none"}, " +
                    $"Playing={playing}. {exception.Message}");
            }
        }

        private void EnterResizeSuspension(double now)
        {
            _waitingForStableSize = true;
            _resizeStableSince = now;
            _stableUpdateCount = 0;
            _dirty = true;
            SetResizeState(true);
        }

        private void SetResizeState(bool suspended)
        {
            State = suspended
                ? PresentationPreviewRenderState.ResizeSuspended
                : PresentationPreviewRenderState.Dirty;
            _surface?.SetResizeSuspended(suspended);
        }

        private static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) &&
                   value.x > 0f && value.y > 0f;
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Approximately(left.x, right.x) &&
                   Mathf.Approximately(left.y, right.y);
        }

        private static RenderTexture CreateDisplayTexture()
        {
            var texture = new RenderTexture(
                TextureWidth,
                TextureHeight,
                0,
                RenderTextureFormat.ARGB32)
            {
                name = "Presentation Workbench Display",
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            texture.Create();
            return texture;
        }

        private static void DisposeDisplayTexture(RenderTexture texture)
        {
            if (texture == null)
                return;
            if (texture.IsCreated())
                texture.Release();
            Object.DestroyImmediate(texture);
        }
    }
}
#endif
