using System;
using Ronriku.Presentation.Accessibility;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    /// <summary>Pixel UI building blocks. All sizes are dp.</summary>
    public static class UiFactory
    {
        public static Label Label(string text, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontDefinition = style == FontStyle.Bold ? RonrikuTheme.Display : RonrikuTheme.Body;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginLeft = label.style.marginRight = 0;
            label.style.paddingLeft = label.style.paddingRight = 0;
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        /// <summary>Heading in the display pixel font.</summary>
        public static Label Heading(string text, int size, Color color) => Label(text, size, color, FontStyle.Bold);

        /// <summary>Wrapping body text.</summary>
        public static Label Paragraph(string text, int size, Color color)
        {
            var label = Label(text, size, color);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        /// <summary>Legacy entry point: primary → glowing gradient button, otherwise a flat pixel button.</summary>
        public static Button Button(string text, Action clicked, bool primary = false) =>
            primary ? GlowButton(text, clicked, RonrikuTheme.Lab) : FlatButton(text, clicked);

        public static Button FlatButton(string text, Action clicked)
        {
            var button = BaseButton(text, clicked);
            button.style.backgroundColor = RonrikuTheme.Surface2;
            SetBorder(button, 2, RonrikuTheme.Line);
            button.style.color = RonrikuTheme.Text;
            button.style.fontSize = 12;
            button.style.height = 44;
            return button;
        }

        /// <summary>Gradient-filled button with a dithered glow halo, tinted by <paramref name="palette"/>.</summary>
        public static Button GlowButton(string text, Action clicked, Palette palette)
        {
            var button = BaseButton(text, clicked);
            button.style.height = 52;
            button.style.fontSize = 16;
            button.style.color = RonrikuTheme.Background;
            button.style.unityFontDefinition = RonrikuTheme.DisplayBold;
            button.style.backgroundImage = new StyleBackground(PixelTextures.DiagonalGradient(palette.Accent, palette.Accent2));
            button.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            SetBorder(button, 2, Color.Lerp(palette.Accent, Color.white, 0.35f));
            AttachGlow(button, palette.Accent);
            return button;
        }

        /// <summary>
        /// Adds a dithered halo behind <paramref name="target"/>. Children would draw over the target's own
        /// text, so the halo is an absolutely positioned sibling inserted just before it and kept in sync.
        /// </summary>
        public static void AttachGlow(VisualElement target, Color color, float spreadX = 0.1f, float spreadY = 0.5f)
        {
            var glow = new VisualElement { name = "glow", pickingMode = PickingMode.Ignore };
            glow.style.position = Position.Absolute;
            glow.style.backgroundImage = new StyleBackground(
                PixelTextures.RadialGlow(RonrikuTheme.WithAlpha(color, 0.38f), 48, 6));
            glow.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));

            void Sync()
            {
                Rect r = target.layout;
                if (float.IsNaN(r.width) || r.width <= 0) return;
                float ex = r.width * spreadX, ey = r.height * spreadY;
                glow.style.left = r.x - ex;
                glow.style.top = r.y - ey;
                glow.style.width = r.width + ex * 2;
                glow.style.height = r.height + ey * 2;
                glow.style.display = target.resolvedStyle.display;
                glow.style.visibility = target.resolvedStyle.visibility;
                glow.style.opacity = target.enabledInHierarchy ? 1f : 0.25f;
            }

            target.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (target.parent == null) return;
                if (glow.parent != target.parent) target.parent.Insert(target.parent.IndexOf(target), glow);
                target.schedule.Execute(Sync);
            });
            target.RegisterCallback<DetachFromPanelEvent>(_ => glow.RemoveFromHierarchy());
            target.RegisterCallback<GeometryChangedEvent>(_ => Sync());
            target.schedule.Execute(Sync).Every(250);
        }

        public static VisualElement Panel(Color? border = null)
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = RonrikuTheme.WithAlpha(RonrikuTheme.Surface, 0.92f);
            SetBorder(panel, 2, border ?? RonrikuTheme.Line);
            panel.style.paddingLeft = panel.style.paddingRight = 12;
            panel.style.paddingTop = panel.style.paddingBottom = 12;
            return panel;
        }

        public static VisualElement Row(float gap = 8)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.userData = gap;
            return row;
        }

        public static VisualElement Spacer(float size = 0)
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            if (size > 0) spacer.style.height = spacer.style.width = size;
            else spacer.style.flexGrow = 1;
            return spacer;
        }

        public static void SetBorder(VisualElement element, float width, Color color)
        {
            element.style.borderLeftWidth = element.style.borderRightWidth = width;
            element.style.borderTopWidth = element.style.borderBottomWidth = width;
            element.style.borderLeftColor = element.style.borderRightColor = color;
            element.style.borderTopColor = element.style.borderBottomColor = color;
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = 0;
            element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = 0;
        }

        private static Button BaseButton(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.minWidth = RonrikuTheme.TouchTarget;
            button.style.marginLeft = button.style.marginRight = 0;
            button.style.marginTop = button.style.marginBottom = 0;
            button.style.paddingLeft = button.style.paddingRight = 12;
            button.style.unityFontDefinition = RonrikuTheme.Display;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.focusable = true;
            Pressable(button);
            return button;
        }

        /// <summary>Press feedback: shift down 2 dp and dim; haptic tick through the global service.</summary>
        public static void Pressable(VisualElement element)
        {
            element.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (!element.enabledInHierarchy) return;
                element.style.translate = new Translate(0, 2);
                element.style.opacity = 0.85f;
                Feedback.Tap();
            }, TrickleDown.TrickleDown);
            void Release()
            {
                element.style.translate = new Translate(0, 0);
                element.style.opacity = StyleKeyword.Null;
            }
            element.RegisterCallback<PointerUpEvent>(_ => Release());
            element.RegisterCallback<PointerLeaveEvent>(_ => Release());
        }
    }
}
