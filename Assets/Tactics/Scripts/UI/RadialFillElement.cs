using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    /// <summary>
    /// A VisualElement that draws a radial fill (pie slice) using Painter2D.
    /// fillAmount ranges from 0 (none) to 1 (full circle).
    /// </summary>
    public sealed class RadialFillElement : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<RadialFillElement> { }

        private float _fillAmount;
        private Color _fillColor = Color.white;
        private int _segments = 64;

        public float FillAmount
        {
            get => _fillAmount;
            set
            {
                if (!Mathf.Approximately(_fillAmount, value))
                {
                    _fillAmount = Mathf.Clamp01(value);
                    MarkDirtyRepaint();
                }
            }
        }

        public Color FillColor
        {
            get => _fillColor;
            set
            {
                if (_fillColor != value)
                {
                    _fillColor = value;
                    MarkDirtyRepaint();
                }
            }
        }

        public RadialFillElement()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (_fillAmount <= 0f) return;

            var painter = mgc.painter2D;
            Rect r = contentRect;
            Vector2 center = new Vector2(r.width * 0.5f, r.height * 0.5f);
            float radius = Mathf.Min(r.width, r.height) * 0.5f;
            float angle = _fillAmount * 360f;

            painter.fillColor = _fillColor;
            painter.BeginPath();
            painter.MoveTo(center);

            // Draw arc from -90 degrees (top) clockwise
            float startAngle = -90f;
            float endAngle = startAngle + angle;

            int segs = Mathf.Max(3, Mathf.RoundToInt(_segments * _fillAmount));
            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                float a = Mathf.Deg2Rad * Mathf.Lerp(startAngle, endAngle, t);
                Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                painter.LineTo(p);
            }

            painter.LineTo(center);
            painter.ClosePath();
            painter.Fill();
        }
    }
}
