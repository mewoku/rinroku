using System;
using Ronriku.Domain.Figures;
using Ronriku.Presentation.Components;
using Ronriku.Presentation.Voxels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Shell
{
    public sealed class TopBarModel
    {
        public string Name;
        public int Level;
        public int Rating;
        public int Shards;
        public Figure Avatar;
        public string AvatarKey;
        public bool Online;
    }

    /// <summary>Avatar (pixel 3D, tap to hop), name + level, shard and rating chips.</summary>
    public sealed class TopBar : VisualElement
    {
        private readonly Func<TopBarModel> _model;
        private readonly Action _openProfile;
        private readonly VisualElement _avatarBox;
        private readonly Label _name;
        private readonly Label _level;
        private readonly Label _shards;
        private readonly Label _rating;
        private VoxelView _avatar;
        private string _avatarKey;

        public TopBar(Func<TopBarModel> model, Action openProfile)
        {
            _model = model;
            _openProfile = openProfile;
            name = "top-bar";
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.height = 60;
            style.flexShrink = 0;
            style.paddingLeft = style.paddingRight = RonrikuTheme.Gutter;

            _avatarBox = new VisualElement { name = "avatar" };
            _avatarBox.style.width = _avatarBox.style.height = 44;
            _avatarBox.style.backgroundColor = RonrikuTheme.WithAlpha(RonrikuTheme.Surface, 0.9f);
            UiFactory.SetBorder(_avatarBox, 2, RonrikuTheme.Teal);
            Add(_avatarBox);

            var identity = new VisualElement();
            identity.style.marginLeft = 10;
            identity.style.flexGrow = 1;
            _name = UiFactory.Heading(string.Empty, 12, RonrikuTheme.Text);
            _name.name = "profile-name";
            _name.style.unityTextAlign = TextAnchor.MiddleLeft;
            _level = UiFactory.Label(string.Empty, 11, RonrikuTheme.Muted);
            _level.style.unityTextAlign = TextAnchor.MiddleLeft;
            identity.Add(_name);
            identity.Add(_level);
            identity.RegisterCallback<ClickEvent>(_ => _openProfile?.Invoke());
            Add(identity);

            _shards = Chip(this, "shard", RonrikuTheme.Teal, "profile-shards");
            _rating = Chip(this, "trophy", RonrikuTheme.Yellow, "profile-rating");
        }

        public void Refresh()
        {
            TopBarModel m = _model();
            _name.text = m.Name;
            _level.text = $"LV {m.Level}{(m.Online ? "" : "  ·  LOCAL")}";
            _shards.text = m.Shards.ToString();
            _rating.text = m.Rating.ToString();
            if (m.AvatarKey == _avatarKey && _avatar != null) return;
            _avatarKey = m.AvatarKey;
            _avatarBox.Clear();
            _avatar = new VoxelView(m.Avatar, 48, 30f);
            _avatar.style.flexGrow = 1;
            _avatarBox.Add(_avatar);
        }

        /// <summary>Makes the shard counter pop, e.g. after a reward.</summary>
        public void Pulse()
        {
            _shards.style.scale = new Scale(new Vector3(1.3f, 1.3f, 1f));
            _shards.schedule.Execute(() => _shards.style.scale = new Scale(Vector3.one)).StartingIn(160);
        }

        private static Label Chip(VisualElement parent, string icon, Color colour, string valueName)
        {
            var chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.height = 28;
            chip.style.paddingLeft = chip.style.paddingRight = 8;
            chip.style.marginLeft = 6;
            chip.style.backgroundColor = RonrikuTheme.WithAlpha(RonrikuTheme.Surface, 0.85f);
            UiFactory.SetBorder(chip, 2, RonrikuTheme.WithAlpha(colour, 0.5f));
            chip.Add(new PixelIcon(icon, colour, 14));
            var value = UiFactory.Heading("0", 12, RonrikuTheme.Text);
            value.name = valueName;
            value.style.marginLeft = 6;
            chip.Add(value);
            parent.Add(chip);
            return value;
        }
    }
}
