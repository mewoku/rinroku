using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ronriku.Infrastructure.Online;
using Ronriku.Presentation.Accessibility;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Components
{
    /// <summary>Online social block: handle, leaderboards (global / daily / friends) and friend requests.</summary>
    public sealed class SocialPanel : VisualElement
    {
        private readonly OnlineService _online;
        private readonly VisualElement _list;
        private readonly List<Button> _tabs = new List<Button>();
        private string _scope = "global";

        public SocialPanel(OnlineService online)
        {
            _online = online;
            name = "social";

            var handleRow = UiFactory.Row();
            handleRow.style.marginBottom = 10;
            var handle = Field(string.IsNullOrEmpty(online.Handle) ? "" : online.Handle, "YOUR @HANDLE");
            handle.name = "handle-field";
            handle.style.flexGrow = 1;
            handleRow.Add(handle);
            var save = UiFactory.FlatButton("SET", () => Run(async () =>
            {
                await _online.SetHandle(handle.value.Trim());
                Toast.Show(this, $"HANDLE SET: @{handle.value.Trim().ToUpperInvariant()}", RonrikuTheme.Forest.Accent);
            }));
            save.style.marginLeft = 8;
            handleRow.Add(save);
            Add(handleRow);

            var tabs = UiFactory.Row();
            foreach (var (scope, label) in new[] { ("global", "GLOBAL"), ("daily", "TODAY"), ("friends", "FRIENDS") })
            {
                var tab = UiFactory.FlatButton(label, () => Load(scope));
                tab.name = "rank-" + scope;
                tab.style.flexGrow = 1;
                tab.style.flexBasis = 0;
                tab.style.height = 36;
                tab.style.fontSize = 10;
                tab.userData = scope;
                if (_tabs.Count > 0) tab.style.marginLeft = 6;
                _tabs.Add(tab);
                tabs.Add(tab);
            }
            Add(tabs);

            _list = new VisualElement { name = "rank-list" };
            _list.style.marginTop = 8;
            _list.style.minHeight = 80;
            Add(_list);

            var friendRow = UiFactory.Row();
            friendRow.style.marginTop = 12;
            var friend = Field("", "ADD FRIEND BY @HANDLE");
            friend.name = "friend-field";
            friend.style.flexGrow = 1;
            friendRow.Add(friend);
            var add = UiFactory.FlatButton("ADD", () => Run(async () =>
            {
                string h = friend.value.Trim().TrimStart('@');
                if (h.Length == 0) return;
                await _online.AddFriend(h);
                friend.value = string.Empty;
                Toast.Show(this, $"REQUEST SENT TO @{h.ToUpperInvariant()}", RonrikuTheme.Frost.Accent);
                Load("friends");
            }));
            add.style.marginLeft = 8;
            friendRow.Add(add);
            Add(friendRow);

            Load("global");
        }

        private void Load(string scope)
        {
            _scope = scope;
            foreach (Button tab in _tabs)
                UiFactory.SetBorder(tab, 2, (string)tab.userData == scope ? RonrikuTheme.Frost.Accent : RonrikuTheme.Line);
            _list.Clear();
            _list.Add(UiFactory.Label("LOADING…", 11, RonrikuTheme.Muted));
            Run(async () =>
            {
                if (scope == "friends")
                {
                    List<FriendRow> friends = await _online.Friends();
                    List<LeaderboardRow> ranks = await _online.Leaderboard("friends", 25);
                    if (_scope != scope) return;
                    _list.Clear();
                    foreach (FriendRow f in friends)
                        if (f.Status == "pending" && f.Direction == "incoming") _list.Add(RequestRow(f));
                    if (ranks.Count == 0 && friends.Count == 0) _list.Add(UiFactory.Label("NO FRIENDS YET  ·  ADD ONE BELOW", 11, RonrikuTheme.Muted));
                    foreach (LeaderboardRow r in ranks) _list.Add(RankRow(r));
                    return;
                }
                List<LeaderboardRow> rows = await _online.Leaderboard(scope, 25);
                if (_scope != scope) return;
                _list.Clear();
                if (rows.Count == 0) _list.Add(UiFactory.Label("NO SCORES YET  ·  BE FIRST", 11, RonrikuTheme.Muted));
                foreach (LeaderboardRow r in rows) _list.Add(RankRow(r));
            });
        }

        private static VisualElement RankRow(LeaderboardRow r)
        {
            var row = UiFactory.Row();
            row.style.height = 30;
            row.style.paddingLeft = row.style.paddingRight = 6;
            if (r.IsMe) row.style.backgroundColor = RonrikuTheme.WithAlpha(RonrikuTheme.Frost.Accent, 0.15f);
            Color rankColour = r.Rank == 1 ? RonrikuTheme.Gold : r.Rank <= 3 ? RonrikuTheme.Text : RonrikuTheme.Muted;
            var rank = UiFactory.Heading($"#{r.Rank}", 11, rankColour);
            rank.style.width = 44;
            rank.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(rank);
            var name = UiFactory.Heading(r.Name + (r.IsMe ? "  (YOU)" : ""), 11, r.IsMe ? RonrikuTheme.Frost.Accent : RonrikuTheme.Text);
            name.style.flexGrow = 1;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(name);
            row.Add(UiFactory.Heading(r.Score.ToString(), 11, RonrikuTheme.Yellow));
            return row;
        }

        private VisualElement RequestRow(FriendRow f)
        {
            var row = UiFactory.Row();
            row.style.height = 36;
            var name = UiFactory.Heading($"@{f.Name} WANTS TO BE FRIENDS", 10, RonrikuTheme.Text);
            name.style.flexGrow = 1;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(name);
            var accept = UiFactory.FlatButton("OK", () => Run(async () =>
            {
                await _online.RespondFriend(f.UserId, "accept");
                Load("friends");
            }));
            accept.style.height = 30;
            row.Add(accept);
            return row;
        }

        private static TextField Field(string value, string placeholder)
        {
            var field = new TextField { value = value, maxLength = 20 };
            field.textEdition.placeholder = placeholder;
            field.style.height = 40;
            field.style.marginLeft = field.style.marginRight = 0;
            field.style.unityFontDefinition = RonrikuTheme.Display;
            field.style.fontSize = 12;
            var input = field.Q(TextField.textInputUssName);
            if (input != null)
            {
                input.style.backgroundColor = RonrikuTheme.Surface2;
                input.style.color = RonrikuTheme.Text;
                UiFactory.SetBorder(input, 2, RonrikuTheme.Line);
            }
            return field;
        }

        private async void Run(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (OnlineException e)
            {
                Feedback.Error();
                Toast.Show(this, e.Code.Replace('_', ' ').ToUpperInvariant(), RonrikuTheme.Red);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RONRIKU social: {e.Message}");
            }
        }
    }
}
