using System;
using System.Collections.Generic;
using System.Text;
using Ronriku.Domain.Arcade;
using Ronriku.Domain.Figures;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Audio;
using Ronriku.Presentation.Components;
using Ronriku.Presentation.Voxels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Arcade
{
    /// <summary>8×8 pixel sprites for board things.</summary>
    internal static class Sprites
    {
        public static readonly string[] Slime = { "........", "...##...", "..####..", ".######.", ".#.##.#.", "########", "########", ".######." };
        public static readonly string[] Bat = { "........", "#......#", "##.##.##", "########", ".#.##.#.", "..####..", "...##...", "........" };
        public static readonly string[] Skeleton = { "..####..", ".######.", ".#.##.#.", ".######.", "..#..#..", "..####..", ".#.##.#.", ".#....#." };
        public static readonly string[] Gem = { "...##...", "..####..", ".##..##.", "##.##.##", ".######.", "..####..", "...##...", "........" };
        public static readonly string[] Stairs = { "........", "......##", "......##", "....####", "....####", "..######", "..######", "########" };
        public static readonly string[] Door = { "..####..", ".#....#.", "#......#", "#......#", "#....#.#", "#......#", "#......#", "########" };

        public static void Draw(Painter2D p, string[] sprite, Vector2 topLeft, float size, Color color)
        {
            float px = size / 8f;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                if (sprite[y][x] == '#') Glyphs.Rect(p, topLeft + new Vector2(x * px, y * px), px + 0.3f, px + 0.3f, color);
        }
    }

    /// <summary>Shared board chrome: fits a W×H grid in its rect, hosts the hero voxel and handles swipes.</summary>
    public abstract class GridBoard : VisualElement
    {
        protected readonly int W;
        protected readonly int H;
        protected readonly Palette Palette;
        private readonly VisualElement _hero;
        private Vector2 _heroCell;

        public SwipeInput Input { get; }

        protected GridBoard(int width, int height, Palette palette, Figure hero)
        {
            W = width;
            H = height;
            Palette = palette;
            name = "board";
            style.flexGrow = 1;
            generateVisualContent += ctx => DrawBoard(ctx.painter2D);
            _hero = new VisualElement { pickingMode = PickingMode.Ignore, name = "hero" };
            _hero.style.position = Position.Absolute;
            if (hero != null)
            {
                var view = new VoxelView(hero, 64, 0f, false);
                view.style.flexGrow = 1;
                _hero.Add(view);
            }
            Add(_hero);
            Input = new SwipeInput(this);
            RegisterCallback<GeometryChangedEvent>(_ => PlaceHero(_heroCell));
        }

        public float Cell => Mathf.Floor(Mathf.Min(contentRect.width / W, contentRect.height / H));
        public Vector2 Origin => new Vector2((contentRect.width - Cell * W) * 0.5f, (contentRect.height - Cell * H) * 0.5f);
        public Vector2 CellTopLeft(int cell) => Origin + new Vector2(cell % W * Cell, cell / W * Cell);
        public Vector2 CellCenter(int cell) => CellTopLeft(cell) + Vector2.one * Cell * 0.5f;
        public VisualElement HeroElement => _hero;

        public void PlaceHero(Vector2 cell)
        {
            _heroCell = cell;
            float c = Cell;
            if (float.IsNaN(c) || c <= 0) return;
            float size = c * 1.25f;
            _hero.style.width = _hero.style.height = size;
            Vector2 at = Origin + new Vector2(cell.x * c, cell.y * c) - new Vector2((size - c) * 0.5f, size - c);
            _hero.style.left = at.x;
            _hero.style.top = at.y;
        }

        public void PlaceHero(int cell) => PlaceHero(new Vector2(cell % W, cell / W));

        /// <summary>Tweens the hero through cells, calling <paramref name="each"/> as each is entered.</summary>
        public void MoveHero(IReadOnlyList<int> path, float msPerCell, Action<int> each, Action done)
        {
            if (path.Count == 0)
            {
                done?.Invoke();
                return;
            }
            Vector2 from = _heroCell;
            int index = 0;
            float segmentStart = Time.realtimeSinceStartup;
            IVisualElementScheduledItem item = null;
            item = schedule.Execute(() =>
            {
                var to = new Vector2(path[index] % W, path[index] / W);
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - segmentStart) * 1000f / msPerCell);
                PlaceHero(Vector2.Lerp(from, to, t));
                if (t < 1f) return;
                each?.Invoke(path[index]);
                from = to;
                index++;
                segmentStart = Time.realtimeSinceStartup;
                if (index >= path.Count)
                {
                    item.Pause();
                    done?.Invoke();
                }
            }).Every(16);
        }

        protected void DrawFloor(Painter2D p, Func<int, bool> isWall)
        {
            float c = Cell;
            Color floorA = Color.Lerp(RonrikuTheme.Background2, Palette.Ambient, 0.35f);
            Color floorB = Color.Lerp(RonrikuTheme.Background2, Palette.Ambient, 0.55f);
            Color wallTop = Color.Lerp(Palette.Accent2, Color.white, 0.15f);
            Color wallSide = Color.Lerp(Palette.Accent2, Color.black, 0.45f);
            for (int i = 0; i < W * H; i++)
            {
                var tl = CellTopLeft(i);
                if (isWall(i))
                {
                    Glyphs.Rect(p, tl, c, c, wallSide);
                    Glyphs.Rect(p, tl, c, c * 0.72f, wallTop);
                    Glyphs.Rect(p, tl + new Vector2(c * 0.1f, c * 0.12f), c * 0.35f, c * 0.08f, Color.Lerp(wallTop, Color.white, 0.3f));
                }
                else Glyphs.Rect(p, tl, c, c, (i % W + i / W) % 2 == 0 ? floorA : floorB);
            }
        }

        protected abstract void DrawBoard(Painter2D p);
    }

    // ====================================================================== ICE DASH

    public sealed class DashBoard : GridBoard
    {
        private readonly DashState _state;

        public DashBoard(DashState state, Palette palette, Figure hero) : base(DashLevel.Width, DashLevel.Height, palette, hero)
        {
            _state = state;
            PlaceHero(state.Pos);
        }

        protected override void DrawBoard(Painter2D p)
        {
            var level = _state.Level;
            DrawFloor(p, i => level.Tiles[i] == DashTile.Wall);
            float c = Cell;
            for (int i = 0; i < level.Tiles.Length; i++)
            {
                var tl = CellTopLeft(i);
                if (level.Tiles[i] == DashTile.Spike)
                    for (int k = 0; k < 3; k++)
                        Glyphs.Poly(p, RonrikuTheme.Red, tl + new Vector2(c * (0.1f + k * 0.28f), c * 0.85f), tl + new Vector2(c * (0.24f + k * 0.28f), c * 0.25f), tl + new Vector2(c * (0.38f + k * 0.28f), c * 0.85f));
            }
            bool open = _state.ExitOpen;
            Sprites.Draw(p, Sprites.Door, CellTopLeft(level.Exit) + Vector2.one * c * 0.12f, c * 0.76f, open ? RonrikuTheme.Gold : RonrikuTheme.BlueGrey);
            for (int g = 0; g < level.Gems.Length; g++)
            {
                if ((_state.Gems & (1 << g)) != 0) continue;
                float bob = MotionSettings.ReducedMotion ? 0 : Mathf.Sin(Time.realtimeSinceStartup * 4f + g) * c * 0.05f;
                Sprites.Draw(p, Sprites.Gem, CellTopLeft(level.Gems[g]) + new Vector2(c * 0.18f, c * 0.14f + bob), c * 0.64f, Glyphs.TokenColors[(g + 1) % Glyphs.TokenColors.Length]);
            }
        }
    }

    /// <summary>
    /// Ice Dash: swipe and the hero slides until something stops them. Grab every gem, then the door
    /// opens. Spikes send you back. Moves at or under par earn three stars.
    /// </summary>
    public sealed class DashScreen : VisualElement
    {
        private readonly DashState _state;
        private readonly DashBoard _board;
        private readonly Palette _palette;
        private readonly Action<ArcadeResult> _completed;
        private readonly Label _moves;
        private readonly VisualElement _overlay;
        private readonly float _startedAt;
        private bool _moving;
        private bool _finished;

        public DashState State => _state;

        public DashScreen(DashLevel level, Figure hero, Palette palette, string title, Action back, Action<ArcadeResult> completed)
        {
            _state = new DashState(level);
            _palette = palette;
            _completed = completed;
            name = "dash";
            style.flexGrow = 1;
            style.paddingLeft = style.paddingRight = 10;
            style.paddingTop = 6;
            style.paddingBottom = 12;

            Add(ArcadeChrome.TopBar(title, back, out _, extra: _moves = UiFactory.Heading(string.Empty, 12, RonrikuTheme.Text)));
            var hint = UiFactory.Heading("SWIPE: SLIDE TILL YOU HIT A WALL. GRAB ALL GEMS, THEN THE DOOR", 10, palette.Accent);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.flexShrink = 0;
            hint.style.marginBottom = 6;
            Add(hint);

            _board = new DashBoard(_state, palette, hero);
            _board.Input.Swiped += Move;
            Add(_board);
            _board.schedule.Execute(_board.MarkDirtyRepaint).Every(100);

            var controls = UiFactory.Row();
            controls.style.flexShrink = 0;
            controls.style.marginTop = 8;
            controls.style.justifyContent = Justify.Center;
            controls.Add(DPad.Build(Move, palette.Accent));
            var restart = UiFactory.FlatButton("RESET", Restart);
            restart.name = "restart";
            restart.style.width = 84;
            restart.style.height = 56;
            restart.style.marginLeft = 8;
            controls.Add(restart);
            Add(controls);

            _overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = _overlay.style.right = _overlay.style.top = _overlay.style.bottom = 0;
            Add(_overlay);

            _startedAt = Time.realtimeSinceStartup;
            UpdateMoves();
            RegisterCallback<AttachToPanelEvent>(_ => Juice.Banner(_overlay, "DASH!", palette.Accent, 0.7f));
        }

        private void UpdateMoves()
        {
            _moves.text = $"MOVES {_state.Moves}  ·  PAR {_state.Level.Par}";
            _moves.style.color = _state.Moves <= _state.Level.Par ? RonrikuTheme.Text : RonrikuTheme.Hex("FF8A3D");
        }

        public void Move(int dir)
        {
            if (_moving || _finished) return;
            DashSlide slide = _state.Move(dir);
            if (slide.Path.Count == 0)
            {
                Juice.Shake(_board.HeroElement, 5f, 0.18f);
                Feedback.Error();
                return;
            }
            _moving = true;
            Feedback.Whoosh();
            UpdateMoves();
            _board.MoveHero(slide.Path, 55f, cell =>
            {
                if (slide.Collected.Contains(cell))
                {
                    Feedback.Coin();
                    Juice.Popup(_overlay, _overlay.WorldToLocal(_board.LocalToWorld(_board.CellCenter(cell))), "+GEM", RonrikuTheme.Gold, 16, 40f, 0.6f);
                    _board.MarkDirtyRepaint();
                    if (_state.ExitOpen || slide.Gems == _state.Level.AllGems) Juice.Flash(_board, RonrikuTheme.WithAlpha(RonrikuTheme.Gold, 0.2f), 0.3f);
                }
            }, () =>
            {
                _moving = false;
                _board.MarkDirtyRepaint();
                if (slide.Dead)
                {
                    Feedback.Hit();
                    Juice.Flash(_overlay, RonrikuTheme.WithAlpha(RonrikuTheme.Red, 0.35f), 0.3f);
                    Juice.Shake(this, 10f, 0.3f);
                    Juice.Popup(_overlay, _overlay.WorldToLocal(_board.LocalToWorld(_board.CellCenter(slide.End))), "OUCH", RonrikuTheme.Red, 20);
                    _board.PlaceHero(_state.Pos);
                    return;
                }
                Juice.Shake(_board.HeroElement, 3f, 0.12f);
                Feedback.Snap();
                if (_state.Won) Finish();
            });
        }

        private void Restart()
        {
            if (_moving || _finished) return;
            _state.Restart();
            _board.PlaceHero(_state.Pos);
            _board.MarkDirtyRepaint();
            UpdateMoves();
            Feedback.Whoosh();
        }

        private void Finish()
        {
            _finished = true;
            var sb = new StringBuilder("D1:");
            foreach (int d in _state.Log) sb.Append(d < 0 ? 'r' : (char)('0' + d));
            var result = new ArcadeResult
            {
                Won = true,
                Stars = _state.Stars,
                ElapsedMs = Mathf.RoundToInt((Time.realtimeSinceStartup - _startedAt) * 1000f),
                Score = _state.Moves,
                Proof = sb.ToString()
            };
            Feedback.Win();
            Juice.Banner(_overlay, _state.Stars == 3 ? "PERFECT!" : "CLEAR!", RonrikuTheme.Gold, 1.1f, () => _completed(result));
        }
    }

    // ====================================================================== BEAT CRAWL

    public sealed class CrawlBoard : GridBoard
    {
        private readonly CrawlState _state;
        public float Pulse;

        public CrawlBoard(CrawlState state, Palette palette, Figure hero) : base(CrawlLevel.Width, CrawlLevel.Height, palette, hero)
        {
            _state = state;
            PlaceHero(state.Hero);
        }

        protected override void DrawBoard(Painter2D p)
        {
            var level = _state.Level;
            DrawFloor(p, i => level.Walls[i]);
            float c = Cell;
            Sprites.Draw(p, Sprites.Stairs, CellTopLeft(level.Stairs) + Vector2.one * c * 0.12f, c * 0.76f,
                _state.StairsOpen ? RonrikuTheme.Gold : RonrikuTheme.WithAlpha(RonrikuTheme.BlueGrey, 0.5f));
            float squash = 1f + Pulse * 0.12f;
            foreach (Enemy e in _state.Enemies)
            {
                string[] sprite = e.Kind == EnemyKind.Slime ? Sprites.Slime : e.Kind == EnemyKind.Bat ? Sprites.Bat : Sprites.Skeleton;
                Color color = e.Kind == EnemyKind.Slime ? RonrikuTheme.Hex("7BE35A") : e.Kind == EnemyKind.Bat ? RonrikuTheme.Hex("B45CFF") : RonrikuTheme.Text;
                float size = c * 0.7f * squash;
                Sprites.Draw(p, sprite, CellTopLeft(e.Pos) + new Vector2((c - size) * 0.5f, c - size - c * 0.08f), size, color);
                if (e.Hp > 1) Glyphs.Rect(p, CellTopLeft(e.Pos) + new Vector2(c * 0.1f, c * 0.06f), c * 0.8f, c * 0.08f, RonrikuTheme.Red);
                int intent = _state.Intent(e);
                if (intent >= 0)
                {
                    int target = CrawlLevel.Step(e.Pos, intent);
                    if (target >= 0)
                    {
                        Color warn = target == _state.Hero ? RonrikuTheme.Red : RonrikuTheme.WithAlpha(RonrikuTheme.Yellow, 0.75f);
                        Glyphs.Shape(p, Token.Arrow, Vector2.Lerp(CellCenter(e.Pos), CellCenter(target), 0.62f), c * 0.16f, warn, intent);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Beat Crawl: monsters move on the beat of the music; you get one move per beat. Moving on the beat
    /// builds a combo. Bump monsters to hit them, clear the room, take the stairs. Arrows show where each
    /// monster steps next.
    /// </summary>
    public sealed class CrawlScreen : VisualElement
    {
        /// <summary>Game beats are every second music beat so there is time to think.</summary>
        private static int MusicBeatsPerMove => RonrikuTuning.Current.musicBeatsPerMove;
        private static float FallbackSecondsPerMove => RonrikuTuning.Current.fallbackSecondsPerMove;
        private static float Window => RonrikuTuning.Current.beatWindow;

        private readonly CrawlState _state;
        private readonly CrawlBoard _board;
        private readonly Palette _palette;
        private readonly Action<ArcadeResult> _completed;
        private readonly HeartsRow _hearts;
        private readonly Label _comboLabel;
        private readonly VisualElement _beatBar;
        private readonly VisualElement _beatCore;
        private readonly VisualElement _overlay;
        private readonly float _startedAt;
        private readonly StringBuilder _proof = new StringBuilder("R1:");
        private int _lastBeat;
        private int _actedOnBeat = -1;
        private int _combo;
        private int _bestCombo;
        private int _perfects;
        private bool _started;
        private bool _finished;

        public CrawlState State => _state;

        public CrawlScreen(CrawlLevel level, Figure hero, Palette palette, string title, Action back, Action<ArcadeResult> completed)
        {
            _state = new CrawlState(level);
            _palette = palette;
            _completed = completed;
            name = "crawl";
            style.flexGrow = 1;
            style.paddingLeft = style.paddingRight = 10;
            style.paddingTop = 6;
            style.paddingBottom = 12;

            _hearts = new HeartsRow(CrawlState.MaxHearts);
            _hearts.Set(_state.Hearts);
            Add(ArcadeChrome.TopBar(title, back, out _, extra: _hearts));
            var hint = UiFactory.Heading("MOVE ON THE BEAT. BUMP MONSTERS. ARROWS SHOW THEIR NEXT STEP", 10, palette.Accent);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.flexShrink = 0;
            Add(hint);

            var beatRow = UiFactory.Row();
            beatRow.style.height = 34;
            beatRow.style.flexShrink = 0;
            beatRow.style.justifyContent = Justify.SpaceBetween;
            _comboLabel = UiFactory.Heading("COMBO 0", 12, RonrikuTheme.Muted);
            _comboLabel.style.width = 100;
            beatRow.Add(_comboLabel);
            _beatBar = new VisualElement { name = "beat" };
            _beatBar.style.width = 160;
            _beatBar.style.height = 22;
            _beatBar.style.backgroundColor = RonrikuTheme.NearBlack;
            UiFactory.SetBorder(_beatBar, 2, RonrikuTheme.Line);
            _beatBar.style.alignItems = Align.Center;
            _beatBar.style.justifyContent = Justify.Center;
            _beatCore = new VisualElement();
            _beatCore.style.width = 18;
            _beatCore.style.height = 18;
            _beatCore.style.backgroundColor = palette.Accent;
            _beatBar.Add(_beatCore);
            _beatBar.generateVisualContent += DrawBeatTicks;
            beatRow.Add(_beatBar);
            var coins = UiFactory.Heading("", 12, RonrikuTheme.Gold);
            coins.style.width = 100;
            coins.style.unityTextAlign = TextAnchor.MiddleRight;
            coins.schedule.Execute(() => coins.text = $"KILLS {_state.Coins}").Every(200);
            beatRow.Add(coins);
            Add(beatRow);

            _board = new CrawlBoard(_state, palette, hero);
            _board.Input.Swiped += Act;
            Add(_board);

            var pad = DPad.Build(Act, palette.Accent);
            pad.style.marginTop = 8;
            Add(pad);

            _overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = _overlay.style.right = _overlay.style.top = _overlay.style.bottom = 0;
            Add(_overlay);

            _startedAt = Time.realtimeSinceStartup;
            schedule.Execute(Tick).Every(16);
            RegisterCallback<AttachToPanelEvent>(_ => Juice.Banner(_overlay, "READY?", palette.Accent, 1.2f, () =>
            {
                _started = true;
                _lastBeat = Mathf.FloorToInt((float)Beats());
            }));
        }

        /// <summary>Continuous game-beat position: music clock when the hero track plays, otherwise a steady timer.</summary>
        private double Beats()
        {
            if (Music.IsPlaying && Music.Current == MusicTrack.HeroRun) return Music.SongBeats / MusicBeatsPerMove;
            return (Time.realtimeSinceStartup - _startedAt) / FallbackSecondsPerMove;
        }

        private void Tick()
        {
            double beats = Beats();
            float phase = (float)(beats - Math.Floor(beats));
            float pulse = Mathf.Clamp01(1f - phase * 3f);
            _board.Pulse = pulse;
            _beatCore.style.scale = new Scale(Vector3.one * (1f + pulse * 0.6f));
            _beatCore.style.backgroundColor = Color.Lerp(_palette.Accent, Color.white, pulse * 0.6f);
            _beatBar.MarkDirtyRepaint();
            _board.MarkDirtyRepaint();
            if (!_started || _finished) return;
            int beat = Mathf.FloorToInt((float)beats);
            while (_lastBeat < beat)
            {
                _lastBeat++;
                // A beat passed with no move while enemies close in: the combo survives only on moves.
                bool hit = _state.Tick();
                _proof.Append('|');
                if (hit)
                {
                    int lost = _state.Hearts;
                    _hearts.Set(lost);
                    _hearts.Break(lost);
                    Feedback.Hit();
                    Juice.Flash(_overlay, RonrikuTheme.WithAlpha(RonrikuTheme.Red, 0.35f), 0.3f);
                    Juice.Shake(this, 10f, 0.3f);
                    Juice.Shake(_board.HeroElement, 8f, 0.3f);
                    SetCombo(0);
                }
                if (_state.Lost)
                {
                    Finish();
                    return;
                }
            }
        }

        private void DrawBeatTicks(MeshGenerationContext ctx)
        {
            double beats = Beats();
            float phase = (float)(beats - Math.Floor(beats));
            var r = _beatBar.contentRect;
            float half = r.width * 0.5f;
            for (int k = 0; k < 3; k++)
            {
                float d = (k + 1 - phase) / 3f * half;
                Color c = RonrikuTheme.WithAlpha(_palette.Accent, 0.4f + 0.2f * (2 - k));
                Glyphs.Rect(ctx.painter2D, new Vector2(half - d - 2, r.height * 0.2f), 4, r.height * 0.6f, c);
                Glyphs.Rect(ctx.painter2D, new Vector2(half + d - 2, r.height * 0.2f), 4, r.height * 0.6f, c);
            }
        }

        public void Act(int dir)
        {
            if (!_started || _finished) return;
            double beats = Beats();
            int nearest = (int)Math.Round(beats);
            float offset = (float)Math.Abs(beats - nearest);
            if (_actedOnBeat == nearest)
            {
                Juice.Shake(_beatBar, 4f, 0.15f);
                return;
            }
            _actedOnBeat = nearest;
            bool onBeat = offset <= Window;
            _proof.Append(dir).Append(onBeat ? '+' : '-');
            int heroBefore = _state.Hero;
            HeroAction action = _state.Act(dir);
            Vector2 heroAt = _overlay.WorldToLocal(_board.LocalToWorld(_board.CellCenter(_state.Hero)));
            if (onBeat)
            {
                SetCombo(_combo + 1);
                _perfects++;
                if (offset < Window * 0.45f) Juice.Popup(_overlay, heroAt + new Vector2(0, -40), "PERFECT", RonrikuTheme.Teal, 13, 30f, 0.45f);
            }
            else
            {
                SetCombo(0);
                Juice.Popup(_overlay, heroAt + new Vector2(0, -40), "OFF BEAT", RonrikuTheme.Muted, 12, 24f, 0.45f);
            }

            switch (action)
            {
                case HeroAction.Moved:
                case HeroAction.Won:
                    Feedback.Move();
                    _board.MoveHero(new[] { _state.Hero }, 70f, null, () =>
                    {
                        if (_state.Won) Finish();
                    });
                    break;
                case HeroAction.Attacked:
                case HeroAction.Killed:
                    Juice.Lunge(_board.HeroElement, (dir == 1 ? 1 : dir == 3 ? -1 : 0) * 18f, 0.15f);
                    Feedback.Hit();
                    Juice.Shake(_board, 6f, 0.18f);
                    Vector2 at = _overlay.WorldToLocal(_board.LocalToWorld(_board.CellCenter(CrawlLevel.Step(heroBefore, dir))));
                    Juice.Popup(_overlay, at, action == HeroAction.Killed ? "SLAIN" : "HIT", action == HeroAction.Killed ? RonrikuTheme.Gold : RonrikuTheme.Text, 18, 40f, 0.6f);
                    if (action == HeroAction.Killed && _state.StairsOpen)
                    {
                        Juice.Banner(_overlay, "STAIRS OPEN", RonrikuTheme.Gold, 0.8f);
                        Feedback.Success();
                    }
                    break;
                default:
                    Juice.Shake(_board.HeroElement, 4f, 0.12f);
                    Feedback.Tap();
                    break;
            }
        }

        private void SetCombo(int combo)
        {
            _combo = combo;
            _bestCombo = Math.Max(_bestCombo, combo);
            UpdateComboLabel();
            if (combo > 0 && combo % 8 == 0) Music.Stinger(MusicStinger.Combo);
            Music.SetIntensity(combo >= 8 ? 2 : 1);
        }

        private void UpdateComboLabel()
        {
            _comboLabel.text = $"COMBO {_combo}";
            _comboLabel.style.color = _combo >= 8 ? RonrikuTheme.Gold : _combo >= 3 ? _palette.Accent : RonrikuTheme.Muted;
            if (_combo > 0) Juice.Punch(_comboLabel, 0.2f, 0.15f);
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            var result = new ArcadeResult
            {
                Won = _state.Won,
                Stars = _state.Stars,
                ElapsedMs = Mathf.RoundToInt((Time.realtimeSinceStartup - _startedAt) * 1000f),
                Score = _state.Coins * 10 + _perfects,
                BestCombo = _bestCombo,
                Proof = _proof.ToString()
            };
            if (_state.Won)
            {
                Feedback.Win();
                Juice.Banner(_overlay, "CLEAR!", RonrikuTheme.Gold, 1.1f, () => _completed(result));
            }
            else
            {
                Feedback.Lose();
                Juice.Banner(_overlay, "DEFEATED", RonrikuTheme.Red, 1.2f, () => _completed(result));
            }
        }
    }

    /// <summary>Shared top bar for arcade screens.</summary>
    public static class ArcadeChrome
    {
        public static VisualElement TopBar(string title, Action back, out Button backButton, VisualElement extra = null)
        {
            var top = UiFactory.Row();
            top.style.height = 48;
            top.style.flexShrink = 0;
            backButton = UiFactory.FlatButton(string.Empty, back);
            backButton.name = "back-button";
            backButton.style.width = 44;
            backButton.style.height = 40;
            backButton.style.alignItems = Align.Center;
            backButton.style.justifyContent = Justify.Center;
            backButton.Add(new PixelIcon("back", RonrikuTheme.Text, 16));
            top.Add(backButton);
            var titleLabel = UiFactory.Heading(title, 11, RonrikuTheme.Muted);
            titleLabel.style.flexGrow = 1;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            titleLabel.style.marginLeft = 10;
            top.Add(titleLabel);
            if (extra != null) top.Add(extra);
            return top;
        }
    }
}
