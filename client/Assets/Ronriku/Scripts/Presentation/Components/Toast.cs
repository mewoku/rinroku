using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    /// <summary>Short message that slides in near the top of the given root and fades out.</summary>
    public static class Toast
    {
        public static void Show(VisualElement anywhere, string text, Color accent)
        {
            VisualElement root = anywhere?.panel?.visualTree;
            if (root == null) return;
            var toast = UiFactory.Panel(accent);
            toast.name = "toast";
            toast.pickingMode = PickingMode.Ignore;
            toast.style.position = Position.Absolute;
            toast.style.top = 80;
            toast.style.left = 24;
            toast.style.right = 24;
            toast.style.backgroundColor = RonrikuTheme.WithAlpha(RonrikuTheme.Background2, 0.96f);
            var label = UiFactory.Heading(text, 12, RonrikuTheme.Text);
            label.style.whiteSpace = WhiteSpace.Normal;
            toast.Add(label);
            toast.style.opacity = 0;
            root.Add(toast);
            toast.schedule.Execute(() => toast.style.opacity = 1).StartingIn(16);
            toast.schedule.Execute(() => toast.style.opacity = 0).StartingIn(1700);
            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).StartingIn(2000);
        }
    }
}
