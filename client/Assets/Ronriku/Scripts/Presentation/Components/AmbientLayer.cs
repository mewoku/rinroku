using Ronriku.Presentation.Accessibility;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    /// <summary>
    /// Full-screen background: dark vertical gradient plus three large dithered glows that drift slowly
    /// and shift with device tilt. Tinted by the active <see cref="Palette"/>.
    /// </summary>
    public sealed class AmbientLayer : VisualElement
    {
        private readonly VisualElement[] _blobs = new VisualElement[3];
        private readonly Vector2[] _anchors = { new Vector2(0.15f, 0.12f), new Vector2(0.9f, 0.45f), new Vector2(0.3f, 0.88f) };
        private readonly float[] _sizes = { 360f, 300f, 420f };
        private readonly float _start;

        public AmbientLayer(Palette palette)
        {
            name = "ambient";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            style.overflow = Overflow.Hidden;
            style.backgroundImage = new StyleBackground(
                PixelTextures.VerticalGradient(RonrikuTheme.Background2, RonrikuTheme.Background, 4, 96));
            style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));

            for (int i = 0; i < _blobs.Length; i++)
            {
                var blob = new VisualElement { pickingMode = PickingMode.Ignore };
                blob.style.position = Position.Absolute;
                blob.style.width = blob.style.height = _sizes[i];
                blob.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                _blobs[i] = blob;
                Add(blob);
            }
            SetPalette(palette);
            _start = Time.realtimeSinceStartup;
            schedule.Execute(Animate).Every(33);
            RegisterCallback<GeometryChangedEvent>(_ => Animate());
        }

        public void SetPalette(Palette palette)
        {
            Color[] tints =
            {
                RonrikuTheme.WithAlpha(palette.Ambient, 0.95f),
                RonrikuTheme.WithAlpha(palette.Accent2, 0.35f),
                RonrikuTheme.WithAlpha(palette.Accent, 0.22f)
            };
            for (int i = 0; i < _blobs.Length; i++)
                _blobs[i].style.backgroundImage = new StyleBackground(PixelTextures.RadialGlow(tints[i], 40, 5));
        }

        private void Animate()
        {
            float w = resolvedStyle.width, h = resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0) return;
            float t = MotionSettings.ReducedMotion ? 0f : Time.realtimeSinceStartup - _start;
            Vector2 tilt = Tilt.Current;
            for (int i = 0; i < _blobs.Length; i++)
            {
                float phase = i * 2.1f;
                float dx = Mathf.Sin(t * 0.11f + phase) * 28f + tilt.x * (12f + 10f * i);
                float dy = Mathf.Cos(t * 0.09f + phase) * 22f - tilt.y * (10f + 8f * i);
                float size = _sizes[i];
                // translate avoids re-running layout every frame.
                _blobs[i].style.translate = new Translate(
                    _anchors[i].x * w - size * 0.5f + dx, _anchors[i].y * h - size * 0.5f + dy);
            }
        }
    }
}
