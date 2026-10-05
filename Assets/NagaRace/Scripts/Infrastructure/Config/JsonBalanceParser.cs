using System;
using System.Collections.Generic;
using NagaRace.Application.Race;
using NagaRace.Domain.Balance;
using Newtonsoft.Json;

namespace NagaRace.Infrastructure.Config
{
    /// <summary>game_balance_defaults.json → <see cref="BalanceConfig"/>。</summary>
    public static class JsonBalanceParser
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static BalanceConfig Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("バランス設定の JSON が空です。");

            var config = JsonConvert.DeserializeObject<BalanceConfig>(json, Settings);
            Validate(config);
            return config;
        }

        /// <summary>レンジ指定の欠落など、実行時に気づきにくい設定ミスを読込時に検出する。</summary>
        private static void Validate(BalanceConfig c)
        {
            var errors = new List<string>();
            if (c.Race.TickRate <= 0) errors.Add("race.tickRate");
            if (c.Race.TimeoutSeconds <= 0) errors.Add("race.timeoutSeconds");
            RequireRange(errors, "race.targetDurationSeconds", c.Race.TargetDurationSeconds);
            RequireRange(errors, "stats.flowRateRange", c.Stats.FlowRateRange);
            RequireRange(errors, "stats.accelRateRange", c.Stats.AccelRateRange);
            RequireRange(errors, "stats.roughnessPenaltyByBuoyancy", c.Stats.RoughnessPenaltyByBuoyancy);
            RequireRange(errors, "stats.contactSlowdownByStability", c.Stats.ContactSlowdownByStability);
            RequireRange(errors, "stats.stunSecondsByStability", c.Stats.StunSecondsByStability);
            RequireRange(errors, "stats.routeNoiseByJudgement", c.Stats.RouteNoiseByJudgement);
            RequireRange(errors, "stats.blockReactionSecondsByJudgement", c.Stats.BlockReactionSecondsByJudgement);
            RequireRange(errors, "affinity.modifierRange", c.Affinity.ModifierRange);
            RequireRange(errors, "lane.lateralSpeedMulByAgility", c.Lane.LateralSpeedMulByAgility);
            RequireRange(errors, "stats.stumbleHazardByStability", c.Stats.StumbleHazardByStability);
            RequireRange(errors, "stats.goodFlowSkewByJudgement", c.Stats.GoodFlowSkewByJudgement);
            if (c.Stats.DecelRate <= 0) errors.Add("stats.decelRate");
            if (c.Lane.RacerWidthMeters <= 0) errors.Add("lane.racerWidthMeters");

            if (errors.Count > 0)
            {
                throw new FormatException("バランス設定に不足・不正な項目があります: " + string.Join(", ", errors));
            }
        }

        private static void RequireRange(List<string> errors, string key, double[] range)
        {
            if (range == null || range.Length != 2) errors.Add(key);
        }
    }

    /// <summary>JSON 文字列から読み込むバランス設定リポジトリ（初回読込をキャッシュ）。</summary>
    public sealed class JsonBalanceRepository : IBalanceRepository
    {
        private readonly string _json;
        private BalanceConfig _cache;

        public JsonBalanceRepository(string json)
        {
            _json = json;
        }

        public BalanceConfig Load()
        {
            return _cache ?? (_cache = JsonBalanceParser.Parse(_json));
        }
    }
}
