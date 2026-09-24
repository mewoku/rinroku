using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Ronriku.Domain.Player;
using UnityEngine;

namespace Ronriku.Infrastructure.Online
{
    public sealed class LeaderboardRow
    {
        public int Rank;
        public string UserId;
        public string Name;
        public long Score;
        public bool IsMe;
    }

    public sealed class LevelSubmitResult
    {
        public int Earned;
        public int Stars;
        public bool FirstClear;
    }

    public sealed class DailySubmitResult
    {
        public bool Counted;
        public int ShardsEarned;
        public int RatingAfter;
    }

    public sealed class FriendRow
    {
        public string UserId;
        public string Name;
        public int Rating;
        public string Status;
        public string Direction;
    }

    /// <summary>
    /// Offline-first bridge to the backend. Local play always works; when connected, the server is
    /// authoritative for shards, rating, streak, figures and level progress, and every result is
    /// submitted as answers for server-side replay (backend/README.md).
    /// </summary>
    public sealed class OnlineService
    {
        private readonly SupabaseClient _client;

        public bool Connected { get; private set; }
        public string Handle { get; private set; }
        public string LastError { get; private set; }
        public event Action StateChanged;

        public OnlineService(SupabaseClient client) => _client = client;

        public static OnlineService CreateFromConfig()
        {
            OnlineConfig config = OnlineConfig.Load();
            return config == null || string.IsNullOrEmpty(config.url) ? null : new OnlineService(new SupabaseClient(config));
        }

        /// <summary>Signs in (anonymously on first run) and pulls the authoritative profile into <paramref name="profile"/>.</summary>
        public async Task<bool> ConnectAsync(PlayerProfile profile)
        {
            try
            {
                await Pull(profile);
                Connected = true;
                LastError = null;
            }
            catch (Exception e)
            {
                Connected = false;
                LastError = e is OnlineException oe ? oe.Code : e.Message;
                Debug.Log($"RONRIKU online: offline ({LastError})");
            }
            StateChanged?.Invoke();
            return Connected;
        }

        /// <summary>Refreshes balances, figures and levels from the server.</summary>
        public async Task Pull(PlayerProfile profile)
        {
            JToken me = await _client.Rpc("ensure_profile");
            if (me is JArray array) me = array.Count > 0 ? array[0] : null;
            if (me == null) throw new OnlineException("no_profile", 0);
            Handle = (string)me["handle"];
            profile.displayName = string.IsNullOrEmpty(Handle) ? (string)me["display_name"] ?? profile.displayName : Handle.ToUpperInvariant();
            profile.shards = me["shards"]?.Value<int>() ?? profile.shards;
            profile.rating = me["rating"]?.Value<int>() ?? profile.rating;
            profile.streak = me["streak"]?.Value<int>() ?? profile.streak;
            profile.bestStreak = me["best_streak"]?.Value<int>() ?? profile.bestStreak;
            int lastDay = me["last_daily_day"]?.Type == JTokenType.Integer ? me["last_daily_day"].Value<int>() : 0;
            if (lastDay > 0)
            {
                profile.lastCompletedDay = Math.Max(profile.lastCompletedDay, lastDay);
                profile.completedDailies = Math.Max(profile.completedDailies, me["completed_dailies"]?.Value<int>() ?? 1);
            }

            string uid = _client.UserId;
            JArray figures = await _client.Select("figures", $"select=id,seed,tier&owner_id=eq.{uid}&order=created_at.asc");
            if (figures.Count > 0)
            {
                profile.figures.Clear();
                foreach (JToken f in figures)
                {
                    // Server stores the generator's ulong seed reinterpreted as signed bigint.
                    ulong seed = unchecked((ulong)f["seed"].Value<long>());
                    profile.figures.Add(new OwnedFigure { id = (string)f["id"], seed = seed.ToString(), size = f["tier"].Value<int>(), acquired = "server" });
                }
                string avatar = (string)me["avatar_figure_id"];
                profile.avatarFigureId = !string.IsNullOrEmpty(avatar) ? avatar : profile.figures[0].id;
            }

            JArray levels = await _client.Select("level_progress", "select=world,level,stars,best_ms");
            foreach (JToken l in levels)
            {
                int world = l["world"].Value<int>(), level = l["level"].Value<int>();
                LevelRecord record = profile.LevelRecordFor(world, level);
                if (record == null) profile.levels.Add(record = new LevelRecord { world = world, level = level });
                record.stars = Math.Max(record.stars, l["stars"].Value<int>());
                int best = l["best_ms"]?.Type == JTokenType.Integer ? l["best_ms"].Value<int>() : 0;
                record.bestMs = record.bestMs <= 0 ? best : best <= 0 ? record.bestMs : Math.Min(record.bestMs, best);
            }
        }

        /// <summary>Level (1 outcome) or world boss (3 stage outcomes). Stars are capped server-side at <paramref name="stars"/>.</summary>
        public async Task<LevelSubmitResult> CompleteLevel(int world, int level, int stars, int elapsedMs, IReadOnlyList<TrialOutcome> outcomes)
        {
            JToken r = await _client.Rpc("complete_level", new JObject
            {
                ["p_world"] = world,
                ["p_level"] = level,
                ["p_stars"] = stars,
                ["p_elapsed_ms"] = elapsedMs,
                ["p_proof"] = Proof(outcomes)
            });
            return new LevelSubmitResult
            {
                Earned = r["earned"]?.Value<int>() ?? 0,
                Stars = r["stars"]?.Value<int>() ?? 0,
                FirstClear = r["first_clear"]?.Value<bool>() ?? false
            };
        }

        public async Task<DailySubmitResult> SubmitDaily(int day, IReadOnlyList<TrialOutcome> outcomes)
        {
            var list = new JArray();
            foreach (TrialOutcome o in outcomes)
                list.Add(new JObject
                {
                    // C# enum names; the server compares case-insensitively.
                    ["kind"] = o.Kind.ToString(),
                    ["elapsed_ms"] = o.ElapsedMilliseconds,
                    ["resets"] = o.Resets,
                    ["moves"] = o.Moves,
                    ["answer"] = ParseAnswer(o.Answer)
                });
            JToken r = await _client.Rpc("submit_daily", new JObject { ["p_day"] = day, ["p_outcomes"] = list });
            return new DailySubmitResult
            {
                Counted = r["counted"]?.Value<bool>() ?? false,
                ShardsEarned = r["shards_earned"]?.Value<int>() ?? 0,
                RatingAfter = r["rating_after"]?.Value<int>() ?? 0
            };
        }

        public Task BuyFigure(string shelfItemId) =>
            _client.Rpc("buy_figure_shards", new JObject { ["p_item"] = shelfItemId });

        /// <summary>Pays the entry and returns the attempt id.</summary>
        public async Task<string> EnterBoss(string bossCode)
        {
            JArray rows = await _client.Select("boss_events", $"select=id&code=eq.{Uri.EscapeDataString(bossCode)}");
            if (rows.Count == 0) throw new OnlineException("unknown_boss", 404);
            JToken attempt = await _client.Rpc("enter_boss", new JObject { ["p_boss_id"] = rows[0]["id"], ["p_pay_with"] = "shards" });
            if (attempt is JArray a) attempt = a[0];
            return (string)attempt["id"];
        }

        public Task FinishBoss(string attemptId, IReadOnlyList<TrialOutcome> outcomes, int elapsedMs)
        {
            JObject result = Proof(outcomes);
            result["elapsed_ms"] = elapsedMs;
            return _client.Rpc("finish_boss", new JObject { ["p_attempt_id"] = attemptId, ["p_result"] = result });
        }

        public async Task<List<LeaderboardRow>> Leaderboard(string scope, int limit = 25)
        {
            JToken rows = await _client.Rpc("leaderboard", new JObject { ["p_scope"] = scope, ["p_limit"] = limit });
            var result = new List<LeaderboardRow>();
            foreach (JToken r in rows)
                result.Add(new LeaderboardRow
                {
                    Rank = r["rank"].Value<int>(),
                    UserId = (string)r["user_id"],
                    Name = ((string)r["handle"] ?? (string)r["display_name"] ?? "PLAYER").ToUpperInvariant(),
                    Score = r["score"]?.Value<long>() ?? 0,
                    IsMe = (string)r["user_id"] == _client.UserId
                });
            return result;
        }

        public async Task<List<FriendRow>> Friends()
        {
            JToken rows = await _client.Rpc("list_friends");
            var result = new List<FriendRow>();
            foreach (JToken r in rows)
                result.Add(new FriendRow
                {
                    UserId = (string)r["user_id"],
                    Name = ((string)r["handle"] ?? (string)r["display_name"] ?? "PLAYER").ToUpperInvariant(),
                    Rating = r["rating"]?.Value<int>() ?? 0,
                    Status = (string)r["status"],
                    Direction = (string)r["direction"]
                });
            return result;
        }

        public Task AddFriend(string handle) => _client.Rpc("send_friend_request", new JObject { ["p_handle"] = handle });

        public Task RespondFriend(string requesterId, string action) =>
            _client.Rpc("respond_friend_request", new JObject { ["p_requester_id"] = requesterId, ["p_action"] = action });

        public async Task SetHandle(string handle)
        {
            await _client.Rpc("update_profile", new JObject { ["p_handle"] = handle });
            Handle = handle;
        }

        public Task SetAvatar(string figureId) =>
            _client.Rpc("update_profile", new JObject { ["p_avatar_figure_id"] = figureId });

        /// <summary>{answers, moves, resets} parallel arrays (backend/README.md).</summary>
        private static JObject Proof(IReadOnlyList<TrialOutcome> outcomes)
        {
            var answers = new JArray();
            var moves = new JArray();
            var resets = new JArray();
            foreach (TrialOutcome o in outcomes)
            {
                answers.Add(ParseAnswer(o.Answer));
                moves.Add(o.Moves);
                resets.Add(o.Resets);
            }
            return new JObject { ["answers"] = answers, ["moves"] = moves, ["resets"] = resets };
        }

        private static JToken ParseAnswer(string answer) =>
            string.IsNullOrEmpty(answer) ? JValue.CreateNull() : JToken.Parse(answer);
    }
}
