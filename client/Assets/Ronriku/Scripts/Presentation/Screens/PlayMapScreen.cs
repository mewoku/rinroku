using System;
using System.Collections.Generic;
using Ronriku.Domain.Adventure;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Figures;
using Ronriku.Domain.Player;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using Ronriku.Presentation.Voxels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    /// <summary>
    /// Adventure map: world tabs, a winding path of 12 levels (boss on top), the player's avatar walking
    /// between nodes, guardian monsters, and floating shards to tap. Tapping a node walks there and opens
    /// the encounter card; FIGHT starts the level.
    /// </summary>
    public sealed class PlayMapScreen : VisualElement
    {
        private const float NodeSize = 52f;
        private const float BossSize = 68f;
        private const float RowHeight = 96f;
        private static readonly float[] Wiggle = { 0f, -0.28f, -0.1f, 0.22f, 0.3f, 0.05f, -0.25f, -0.3f, 0f, 0.26f, 0.2f, 0f };

        private readonly PlayerProfile _profile;
        private readonly Figure _avatarFigure;
        private readonly Action<int, int> _play;
        private readonly Func<bool> _collectShard;
        private readonly VisualElement _mapHost;
        private readonly VisualElement _encounter;
        private readonly Label _worldTitle;
        private readonly Label _worldStars;
        private readonly List<Button> _worldChips = new List<Button>();
        private ScrollView _scroll;
        private VisualElement _canvas;
        private VoxelView _walker;
        private Vector2[] _nodes;
        private int _world;
        private int _walkerIndex;
        private bool _walking;

        /// <summary>World shown last this session; -1 opens the player's current world.</summary>
        public static int LastWorld = -1;

        public PlayMapScreen(PlayerProfile profile, Figure avatar, Action<int, int> play, Func<bool> collectShard)
        {
            _profile = profile;
            _avatarFigure = avatar;
            _play = play;
            _collectShard = collectShard;
            name = "play-map";
            style.flexGrow = 1;

            var worlds = new ScrollView(ScrollViewMode.Horizontal) { name = "world-chips" };
            worlds.style.flexShrink = 0;
            worlds.style.height = 44;
            worlds.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            worlds.contentContainer.style.flexDirection = FlexDirection.Row;
            worlds.contentContainer.style.paddingLeft = RonrikuTheme.Gutter;
            for (int w = 0; w < LevelDef.WorldCount; w++)
            {
                int world = w;
                var chip = UiFactory.FlatButton(LevelDef.WorldNames[w], () => SelectWorld(world));
                chip.name = $"world-{w}";
                chip.style.height = 32;
                chip.style.fontSize = 10;
                chip.style.marginRight = 8;
                _worldChips.Add(chip);
                worlds.Add(chip);
            }
            Add(worlds);

            var header = UiFactory.Row();
            header.style.paddingLeft = header.style.paddingRight = RonrikuTheme.Gutter;
            header.style.height = 40;
            header.style.flexShrink = 0;
            _worldTitle = UiFactory.Heading(string.Empty, 20, RonrikuTheme.Text);
            _worldTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            _worldTitle.style.flexGrow = 1;
            header.Add(_worldTitle);
            header.Add(new PixelIcon("star", RonrikuTheme.Yellow, 14));
            _worldStars = UiFactory.Heading(string.Empty, 12, RonrikuTheme.Text);
            _worldStars.style.marginLeft = 6;
            header.Add(_worldStars);
            Add(header);

            _mapHost = new VisualElement { name = "map-host" };
            _mapHost.style.flexGrow = 1;
            Add(_mapHost);

            _encounter = new VisualElement { name = "encounter" };
            _encounter.style.position = Position.Absolute;
            _encounter.style.left = _encounter.style.right = 0;
            _encounter.style.bottom = 0;
            _encounter.style.display = DisplayStyle.None;
            Add(_encounter);

            (int nextWorld, _) = AdventureProgress.Next(profile);
            SelectWorld(Mathf.Clamp(LastWorld >= 0 && WorldUnlocked(LastWorld) ? LastWorld : nextWorld, 0, LevelDef.WorldCount - 1));
        }

        private Palette WorldPalette(int world) => RonrikuTheme.Worlds[world % RonrikuTheme.Worlds.Length];

        private bool WorldUnlocked(int world) => AdventureProgress.IsUnlocked(_profile, world, 0);

        private void SelectWorld(int world)
        {
            if (!WorldUnlocked(world))
            {
                Feedback.Error();
                return;
            }
            _world = world;
            LastWorld = world;
            Palette palette = WorldPalette(world);
            for (int w = 0; w < _worldChips.Count; w++)
            {
                bool unlocked = WorldUnlocked(w);
                Button chip = _worldChips[w];
                chip.text = LevelDef.WorldNames[w];
                chip.style.opacity = unlocked ? 1f : 0.55f;
                chip.style.color = w == world ? WorldPalette(w).Accent : unlocked ? RonrikuTheme.Text : RonrikuTheme.Muted;
                UiFactory.SetBorder(chip, 2, w == world ? WorldPalette(w).Accent : RonrikuTheme.Line);
            }
            _worldTitle.text = $"W{world + 1}  {LevelDef.WorldNames[world]}";
            _worldTitle.style.color = palette.Accent;
            _worldStars.text = $"{AdventureProgress.StarsIn(_profile, world)} / {LevelDef.LevelsPerWorld * 3}";
            BuildMap(palette);
            HideEncounter();
        }

        private void BuildMap(Palette palette)
        {
            _mapHost.Clear();
            _scroll = new ScrollView(ScrollViewMode.Vertical) { name = "map-scroll" };
            _scroll.style.flexGrow = 1;
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.mode = ScrollViewMode.Vertical;
            _mapHost.Add(_scroll);

            float height = RowHeight * LevelDef.LevelsPerWorld + 80f;
            _canvas = new VisualElement { name = "map-canvas" };
            _canvas.style.height = height;
            _scroll.Add(_canvas);

            var path = new MapPathElement(palette);
            path.style.position = Position.Absolute;
            path.style.left = path.style.right = path.style.top = path.style.bottom = 0;
            _canvas.Add(path);

            _nodes = new Vector2[LevelDef.LevelsPerWorld];
            int current = -1;
            for (int i = 0; i < LevelDef.LevelsPerWorld; i++)
            {
                float y = height - 60f - i * RowHeight;
                _nodes[i] = new Vector2(Wiggle[i], y);
                if (current < 0 && AdventureProgress.IsUnlocked(_profile, _world, i) && !AdventureProgress.IsCleared(_profile, _world, i))
                    current = i;
            }
            if (current < 0) current = LevelDef.BossIndex;
            _walkerIndex = current;

            _canvas.RegisterCallback<GeometryChangedEvent>(_ => Layout(path, palette));
        }

        private bool _laidOut;

        private void Layout(MapPathElement path, Palette palette)
        {
            float width = _canvas.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0 || _laidOut && _canvas.childCount > 1) return;
            _laidOut = true;
            var points = new Vector2[_nodes.Length];
            var cleared = new bool[_nodes.Length];
            for (int i = 0; i < _nodes.Length; i++)
            {
                points[i] = new Vector2(width * 0.5f + _nodes[i].x * width, _nodes[i].y);
                cleared[i] = AdventureProgress.IsCleared(_profile, _world, i);
                _canvas.Add(Node(i, points[i], palette));
            }
            path.SetPoints(points, cleared);

            for (int s = 0; s < 3; s++) _canvas.Add(FloatingShard(new Vector2(width * (0.2f + 0.3f * s), points[Mathf.Min(_walkerIndex + 1 + s, points.Length - 1)].y - 40f), s));

            _walker = new VoxelView(_avatarFigure, 64, 0f) { name = "walker" };
            _walker.style.position = Position.Absolute;
            _walker.style.width = _walker.style.height = 64;
            _canvas.Add(_walker);
            PlaceWalker(points[_walkerIndex]);
            _canvas.schedule.Execute(() => _scroll.scrollOffset = new Vector2(0, Mathf.Max(0, points[_walkerIndex].y - _scroll.resolvedStyle.height * 0.6f))).StartingIn(30);
            _screenPoints = points;
        }

        private Vector2[] _screenPoints;

        private VisualElement Node(int index, Vector2 centre, Palette palette)
        {
            bool boss = index == LevelDef.BossIndex;
            float size = boss ? BossSize : NodeSize;
            bool unlocked = AdventureProgress.IsUnlocked(_profile, _world, index);
            LevelRecord record = _profile.LevelRecordFor(_world, index);
            LevelDef def = LevelDef.For(_world, index);

            var node = new VisualElement { name = $"node-{index}" };
            node.style.position = Position.Absolute;
            node.style.left = centre.x - size * 0.5f;
            node.style.top = centre.y - size * 0.5f;
            node.style.width = node.style.height = size;
            node.style.alignItems = Align.Center;
            node.style.justifyContent = Justify.Center;
            Color accent = boss ? RonrikuTheme.Red : palette.Accent;
            node.style.backgroundColor = unlocked ? RonrikuTheme.WithAlpha(RonrikuTheme.Surface2, 0.95f) : RonrikuTheme.WithAlpha(RonrikuTheme.Surface, 0.7f);
            UiFactory.SetBorder(node, boss ? 3 : 2, record != null ? accent : unlocked ? Color.Lerp(accent, Color.white, 0.2f) : RonrikuTheme.Line);
            string icon = !unlocked ? "lock" : boss ? "boss" : def.Kind == TrialKind.Pattern ? "pattern" : def.Kind == TrialKind.Spatial ? "cube" : "link";
            node.Add(new PixelIcon(icon, unlocked ? accent : RonrikuTheme.Muted, boss ? 30 : 22));
            if (unlocked && record == null) UiFactory.AttachGlow(node, accent, 0.5f, 0.5f);

            var stars = UiFactory.Row();
            stars.style.position = Position.Absolute;
            stars.style.bottom = -14;
            for (int s = 0; s < 3; s++)
                stars.Add(new PixelIcon("star", record != null && record.stars > s ? RonrikuTheme.Yellow : RonrikuTheme.Line, 10));
            if (record != null) node.Add(stars);

            var number = UiFactory.Heading(boss ? "BOSS" : (index + 1).ToString(), 9, RonrikuTheme.Muted);
            number.style.position = Position.Absolute;
            number.style.top = -14;
            node.Add(number);

            node.RegisterCallback<ClickEvent>(_ => OnNode(index, unlocked));
            UiFactory.Pressable(node);
            return node;
        }

        private void OnNode(int index, bool unlocked)
        {
            if (_walking) return;
            if (!unlocked)
            {
                Feedback.Error();
                return;
            }
            WalkTo(index, () => ShowEncounter(index));
        }

        private void PlaceWalker(Vector2 point)
        {
            _walker.style.left = point.x - 32f;
            _walker.style.top = point.y - 32f - 44f;
        }

        /// <summary>Hops node by node, 170 ms per hop.</summary>
        private void WalkTo(int target, Action arrived)
        {
            if (_screenPoints == null || target == _walkerIndex)
            {
                arrived();
                return;
            }
            _walking = true;
            int step = target > _walkerIndex ? 1 : -1;
            void Hop()
            {
                _walkerIndex += step;
                Vector2 from = _screenPoints[_walkerIndex - step], to = _screenPoints[_walkerIndex];
                float start = Time.realtimeSinceStartup;
                Feedback.Move();
                _walker.Hop();
                IVisualElementScheduledItem anim = null;
                anim = _walker.schedule.Execute(() =>
                {
                    float t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / 0.17f);
                    Vector2 p = Vector2.Lerp(from, to, t) + Vector2.down * Mathf.Sin(t * Mathf.PI) * 18f;
                    PlaceWalker(p);
                    if (t < 1f) return;
                    anim.Pause();
                    if (_walkerIndex != target) Hop();
                    else
                    {
                        _walking = false;
                        arrived();
                    }
                }).Every(16);
            }
            Hop();
        }

        private void ShowEncounter(int index)
        {
            LevelDef def = LevelDef.For(_world, index);
            Palette palette = def.IsBoss ? RonrikuTheme.Boss : WorldPalette(_world);
            LevelRecord record = _profile.LevelRecordFor(_world, index);
            Figure monster = def.Monster();

            _encounter.Clear();
            var panel = UiFactory.Panel(palette.Accent);
            panel.style.marginLeft = panel.style.marginRight = RonrikuTheme.Gutter;
            panel.style.marginBottom = RonrikuTheme.Gutter;
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.backgroundColor = RonrikuTheme.WithAlpha(RonrikuTheme.Background2, 0.97f);

            var monsterView = new VoxelView(monster, 72, 40f) { name = "monster" };
            monsterView.style.width = monsterView.style.height = 104;
            monsterView.style.flexShrink = 0;
            panel.Add(monsterView);

            var info = new VisualElement();
            info.style.flexGrow = 1;
            info.style.marginLeft = 12;
            info.style.justifyContent = Justify.Center;
            var tag = UiFactory.Heading(def.IsBoss ? "WORLD BOSS" : $"LEVEL {index + 1}  ·  {KindName(def.Kind)}", 10, palette.Accent);
            tag.style.unityTextAlign = TextAnchor.MiddleLeft;
            info.Add(tag);
            var title = UiFactory.Heading(monster.Name, 20, RonrikuTheme.Text);
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            title.style.marginTop = 4;
            info.Add(title);
            var sub = UiFactory.Label(record != null ? $"BEST {record.stars}/3 STARS  ·  REPLAY" :
                def.IsBoss ? "3 STAGES  ·  +300 SHARDS" : $"+{Economy.LevelBase}–{Economy.LevelBase + 2 * Economy.StarBonus} SHARDS", 11, RonrikuTheme.Muted);
            sub.style.unityTextAlign = TextAnchor.MiddleLeft;
            sub.style.marginTop = 2;
            info.Add(sub);

            var buttons = UiFactory.Row();
            buttons.style.marginTop = 10;
            var fight = UiFactory.GlowButton(def.IsBoss ? "FIGHT BOSS" : "FIGHT", () => _play(_world, index), palette);
            fight.name = "fight-button";
            fight.style.flexGrow = 1;
            fight.style.height = 44;
            buttons.Add(fight);
            var close = UiFactory.FlatButton("X", HideEncounter);
            close.style.width = 44;
            close.style.marginLeft = 8;
            buttons.Add(close);
            info.Add(buttons);
            panel.Add(info);

            _encounter.Add(panel);
            _encounter.style.display = DisplayStyle.Flex;
            _encounter.style.translate = new Translate(0, 40);
            _encounter.schedule.Execute(() => _encounter.style.translate = new Translate(0, 0)).StartingIn(16);
            _encounter.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("translate") };
            _encounter.style.transitionDuration = new List<TimeValue> { new TimeValue(160, TimeUnit.Millisecond) };
        }

        private void HideEncounter()
        {
            _encounter.Clear();
            _encounter.style.display = DisplayStyle.None;
        }

        private VisualElement FloatingShard(Vector2 position, int index)
        {
            var shard = new VisualElement { name = $"float-shard-{index}" };
            shard.style.position = Position.Absolute;
            shard.style.left = position.x;
            shard.style.top = position.y;
            shard.style.width = shard.style.height = 36;
            shard.style.alignItems = Align.Center;
            shard.style.justifyContent = Justify.Center;
            shard.Add(new PixelIcon("shard", RonrikuTheme.Teal, 18));
            UiFactory.AttachGlow(shard, RonrikuTheme.Teal, 0.2f, 0.2f);
            float phase = index * 1.7f;
            shard.schedule.Execute(() =>
            {
                if (MotionSettings.ReducedMotion) return;
                shard.style.translate = new Translate(0, Mathf.Sin(Time.realtimeSinceStartup * 2.2f + phase) * 5f);
            }).Every(33);
            shard.RegisterCallback<ClickEvent>(_ =>
            {
                if (!_collectShard()) return;
                Feedback.Coin();
                shard.style.scale = new Scale(new Vector3(1.6f, 1.6f, 1));
                shard.style.opacity = 0;
                shard.schedule.Execute(() => shard.RemoveFromHierarchy()).StartingIn(120);
            });
            return shard;
        }

        private static string KindName(TrialKind kind) => kind == TrialKind.Logic ? "LINK" : kind.ToString().ToUpperInvariant();
    }

    /// <summary>Chunky dotted pixel path between map nodes; cleared segments glow in the world accent.</summary>
    public sealed class MapPathElement : VisualElement
    {
        private readonly Palette _palette;
        private Vector2[] _points = Array.Empty<Vector2>();
        private bool[] _cleared = Array.Empty<bool>();

        public MapPathElement(Palette palette)
        {
            _palette = palette;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetPoints(Vector2[] points, bool[] cleared)
        {
            _points = points;
            _cleared = cleared;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            for (int i = 0; i + 1 < _points.Length; i++)
            {
                Vector2 a = _points[i], b = _points[i + 1];
                float length = Vector2.Distance(a, b);
                int dots = Mathf.Max(2, Mathf.FloorToInt(length / 12f));
                p.fillColor = _cleared[i] ? _palette.Accent : RonrikuTheme.Line;
                for (int d = 1; d < dots; d++)
                {
                    Vector2 c = Vector2.Lerp(a, b, d / (float)dots);
                    float s = _cleared[i] ? 4f : 3f;
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-s, -s));
                    p.LineTo(c + new Vector2(s, -s));
                    p.LineTo(c + new Vector2(s, s));
                    p.LineTo(c + new Vector2(-s, s));
                    p.ClosePath();
                    p.Fill();
                }
            }
        }
    }
}
