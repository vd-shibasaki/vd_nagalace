using System;
using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Race
{
    /// <summary>
    /// 分岐での進路選択（04 §3）。
    ///
    /// routeScore = 相対速度（最速候補=1） + 近道ボーナス(性格) - 危険回避(性格×AI作戦×順位) - 混雑 + ノイズ(判断が高いほど小さい)
    /// </summary>
    public sealed class RouteSelector
    {
        private readonly BalanceConfig _balance;
        private readonly RaceCourse _course;
        private readonly SpeedCalculator _speed;

        public RouteSelector(BalanceConfig balance, RaceCourse course, SpeedCalculator speed)
        {
            _balance = balance;
            _course = course;
            _speed = speed;
        }

        /// <summary>
        /// racer が今いる Segment の次に進む Segment を選ぶ。
        /// planned は「この Tick の判断より前に確定していた」他出走者の予定（同 Tick 内の判断順に依存させないため）。
        /// </summary>
        public FlowSegment Choose(RacerState racer, IReadOnlyList<RacerState> racers, FlowSegment[] planned)
        {
            var options = racer.Segment.Next;
            if (options.Count == 0) return null;
            if (options.Count == 1) return options[0];

            var entrant = racer.Entrant;
            int count = options.Count;
            var times = new double[count];
            var dangers = new int[count];
            var shortcut = new bool[count];
            double minTime = double.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                EvaluateChain(entrant, racer.StaticSpeedFactor, options[i], out times[i], out dangers[i], out shortcut[i]);
                minTime = Math.Min(minTime, times[i]);
            }

            var personality = Lookup(_balance.Personality, entrant.Personality.ToConfigKey());
            var aiStyle = Lookup(_balance.AiStyle, entrant.AiStyle.ToString());
            var route = _balance.Route;
            double aversion = (personality?.DangerAversion ?? 1.0) * (aiStyle?.DangerAversionMul ?? 1.0);
            if (racer.Rank >= route.TrailingRankFrom)
            {
                aversion *= route.TrailingDangerAversionMul;
            }

            double noiseScale = MathUtil.LerpByStat(_balance.Stats.RouteNoiseByJudgement, entrant.Stats.Judgement);
            if (racer.MudRemaining > 0) noiseScale *= _balance.Obstruction.Mud.RouteNoiseMultiplier;

            FlowSegment best = null;
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                double score = minTime / times[i];
                score -= aversion * (dangers[i] - 1) * route.DangerPenaltyPerLevel;
                if (shortcut[i]) score += personality?.ShortcutBonus ?? 0.0;
                score -= CountCrowd(racer, racers, planned, options[i]) * route.CrowdPenaltyPerRacer;
                // ノイズは候補の並び順に必ず1回ずつ引く（再現性のため分岐で乱数消費数を変えない）
                score += MathUtil.ApproxGaussian(racer.RouteRandom) * noiseScale;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = options[i];
                }
            }

            return best;
        }

        /// <summary>候補Segmentから次の合流点（関節点）までの所要時間（姿勢崩れの期待損失込み）・最大危険度・近道の有無を見積もる。</summary>
        private void EvaluateChain(RaceEntrant entrant, double staticFactor, FlowSegment first, out double time, out int maxDanger, out bool hasShortcut)
        {
            time = 0;
            maxDanger = 1;
            hasShortcut = false;
            var segment = first;
            while (segment != null)
            {
                double segmentTime = segment.Length / _speed.ExpectedSpeed(entrant, staticFactor, segment);
                // 姿勢崩れの期待損失も所要時間に含める（浮力・安定の低い個体は荒れたルートを避けるのが正解になる）
                double expectedStumbles = segmentTime * _speed.StumbleHazardPerSecond(entrant, segment);
                time += segmentTime + expectedStumbles * _balance.Route.StumbleTimeLossSeconds;
                maxDanger = Math.Max(maxDanger, _speed.EffectiveDanger(segment));
                hasShortcut |= segment.HasTag(SegmentTags.Shortcut);
                if (_course.IsArticulationNode(segment.EndNode) || segment.Next.Count == 0)
                {
                    break;
                }

                // 入れ子の分岐は見積もりでは先頭の候補で代表させる（MVPのコースでは入れ子分岐を作らない）
                segment = segment.Next[0];
            }
        }

        private double CountCrowd(RacerState self, IReadOnlyList<RacerState> racers, FlowSegment[] planned, FlowSegment option)
        {
            int crowd = 0;
            double lookAhead = _balance.Route.CrowdLookAheadMeters;
            foreach (var other in racers)
            {
                if (other == self || other.IsOut) continue;
                if (other.Segment == option)
                {
                    crowd++;
                }
                else if (other.Segment == self.Segment
                    && planned[other.Index] == option
                    && other.Offset - self.Offset > 0
                    && other.Offset - self.Offset <= lookAhead)
                {
                    crowd++;
                }
            }

            return crowd;
        }

        private static T Lookup<T>(Dictionary<string, T> table, string key) where T : class
        {
            return table != null && table.TryGetValue(key, out var value) ? value : null;
        }
    }
}
