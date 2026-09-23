using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    internal static class UiFactory
    {
        public static Label Label(string text, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = style;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        public static Button Button(string text, Action clicked, bool primary = false)
        {
            var button = new Button(clicked) { text = text };
            button.style.height = 64;
            button.style.minWidth = 64;
            button.style.borderTopLeftRadius = 0;
            button.style.borderTopRightRadius = 0;
            button.style.borderBottomLeftRadius = 0;
            button.style.borderBottomRightRadius = 0;
            button.style.borderLeftWidth = button.style.borderRightWidth = 0;
            button.style.borderTopWidth = button.style.borderBottomWidth = 0;
            button.style.backgroundColor = primary ? RonrikuTheme.Teal : RonrikuTheme.NearBlack;
            button.style.color = primary ? RonrikuTheme.Black : RonrikuTheme.OffWhite;
            button.style.fontSize = primary ? 22 : 15;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.focusable = true;
            return button;
        }
    }
}

