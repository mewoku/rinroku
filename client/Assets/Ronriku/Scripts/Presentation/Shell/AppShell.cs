using System;
using System.Collections.Generic;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Shell
{
    public enum AppTab { Play = 0, Daily = 1, Bosses = 2, Shop = 3, Me = 4 }

    /// <summary>
    /// Root layout: ambient background, top bar, tab content, bottom tab bar, and a full-screen layer for
    /// trials/results that hides the chrome. Screens are rebuilt on every visit so they always show fresh state.
    /// </summary>
    public sealed class AppShell : VisualElement
    {
        private static readonly (AppTab tab, string icon, string label, Palette palette)[] Tabs =
        {
            (AppTab.Play, "map", "PLAY", RonrikuTheme.Lab),
            (AppTab.Daily, "daily", "DAILY", RonrikuTheme.Frost),
            (AppTab.Bosses, "boss", "BOSSES", RonrikuTheme.Boss),
            (AppTab.Shop, "shop", "SHOP", RonrikuTheme.Pattern),
            (AppTab.Me, "me", "ME", RonrikuTheme.Forest)
        };

        private readonly AmbientLayer _ambient;
        private readonly VisualElement _chrome;
        private readonly VisualElement _content;
        private readonly VisualElement _fullscreen;
        private readonly TopBar _topBar;
        private readonly List<(AppTab tab, VisualElement root, PixelIcon icon, Label label, VisualElement dot)> _tabButtons =
            new List<(AppTab, VisualElement, PixelIcon, Label, VisualElement)>();
        private readonly Func<AppTab, VisualElement> _screenFactory;

        public AppTab Current { get; private set; } = AppTab.Play;
        public bool InFullscreen => _fullscreen.style.display == DisplayStyle.Flex;
        public TopBar TopBar => _topBar;

        public AppShell(Func<AppTab, VisualElement> screenFactory, TopBar topBar)
        {
            _screenFactory = screenFactory;
            name = "app-shell";
            style.flexGrow = 1;
            style.backgroundColor = RonrikuTheme.Background;

            _ambient = new AmbientLayer(RonrikuTheme.Lab);
            Add(_ambient);

            _chrome = new VisualElement { name = "chrome" };
            _chrome.style.flexGrow = 1;
            Add(_chrome);

            _topBar = topBar;
            _chrome.Add(_topBar);

            _content = new VisualElement { name = "tab-content" };
            _content.style.flexGrow = 1;
            _content.style.flexShrink = 1;
            _content.style.overflow = Overflow.Hidden;
            _chrome.Add(_content);

            _chrome.Add(BuildTabBar());

            _fullscreen = new VisualElement { name = "fullscreen" };
            _fullscreen.style.position = Position.Absolute;
            _fullscreen.style.left = _fullscreen.style.right = _fullscreen.style.top = _fullscreen.style.bottom = 0;
            _fullscreen.style.display = DisplayStyle.None;
            Add(_fullscreen);
        }

        public void SetSafeArea(float left, float top, float right, float bottom)
        {
            foreach (VisualElement layer in new[] { _chrome, _fullscreen })
            {
                layer.style.paddingLeft = left;
                layer.style.paddingTop = top;
                layer.style.paddingRight = right;
                layer.style.paddingBottom = bottom;
            }
        }

        public void ShowTab(AppTab tab)
        {
            Current = tab;
            CloseFullscreen();
            _content.Clear();
            VisualElement screen = _screenFactory(tab);
            screen.style.flexGrow = 1;
            _content.Add(screen);
            Palette palette = Tabs[(int)tab].palette;
            _ambient.SetPalette(palette);
            foreach (var b in _tabButtons)
            {
                bool active = b.tab == tab;
                Color colour = active ? Tabs[(int)b.tab].palette.Accent : RonrikuTheme.Muted;
                b.icon.SetColor(colour);
                b.label.style.color = colour;
                b.dot.style.visibility = active ? Visibility.Visible : Visibility.Hidden;
            }
            _topBar.Refresh();
        }

        public void ShowFullscreen(VisualElement screen, Palette palette)
        {
            _fullscreen.Clear();
            screen.style.flexGrow = 1;
            _fullscreen.Add(screen);
            _fullscreen.style.display = DisplayStyle.Flex;
            _chrome.style.display = DisplayStyle.None;
            _ambient.SetPalette(palette);
            Feedback.Whoosh();
        }

        public void CloseFullscreen()
        {
            _fullscreen.Clear();
            _fullscreen.style.display = DisplayStyle.None;
            _chrome.style.display = DisplayStyle.Flex;
        }

        private VisualElement BuildTabBar()
        {
            var bar = new VisualElement { name = "tab-bar" };
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.height = 64;
            bar.style.flexShrink = 0;
            bar.style.backgroundColor = RonrikuTheme.WithAlpha(RonrikuTheme.Background, 0.9f);
            bar.style.borderTopWidth = 2;
            bar.style.borderTopColor = RonrikuTheme.Line;
            foreach (var (tab, icon, label, palette) in Tabs)
            {
                var item = new VisualElement { name = $"tab-{label.ToLowerInvariant()}" };
                item.style.flexGrow = 1;
                item.style.flexBasis = 0;
                item.style.alignItems = Align.Center;
                item.style.justifyContent = Justify.Center;
                var dot = new VisualElement();
                dot.style.width = 16;
                dot.style.height = 3;
                dot.style.marginBottom = 5;
                dot.style.backgroundColor = palette.Accent;
                item.Add(dot);
                var pixelIcon = new PixelIcon(icon, RonrikuTheme.Muted, 20);
                item.Add(pixelIcon);
                var text = UiFactory.Heading(label, 9, RonrikuTheme.Muted);
                text.style.marginTop = 5;
                item.Add(text);
                AppTab captured = tab;
                item.RegisterCallback<ClickEvent>(_ => ShowTab(captured));
                UiFactory.Pressable(item);
                _tabButtons.Add((tab, item, pixelIcon, text, dot));
                bar.Add(item);
            }
            return bar;
        }
    }
}
