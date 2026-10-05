using System;
using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Race
{
    /// <summary>
    /// 8体オートレースの固定Tickシミュレーション（04）。
    ///
    /// 1 Tick の処理順:
    ///   0. 入力反映（プレイヤー入力を Tick 番号付きで記録）
    ///   A. 判断（葉力・葉脈炉の更新、Tick開始時点の位置・速度だけを見て目標速度・接触・横移動先を決める）
    ///   B. 移動（全員を同時に動かす。Segment の乗り換え・ゴール判定）
    ///   C. 進路計画（新しく分岐Segmentに入った出走者のルートを決める）
    ///   D. 順位更新
    /// A と C は他の出走者の「この Tick で更新された値」を見ないため、出走者の処理順で結果が変わらない。
    /// </summary>
    public sealed partial class RaceSimulator
    {
        private readonly BalanceConfig _balance;
        private readonly SpeedCalculator _speed;
        private readonly RouteSelector _routes;
        private readonly List<RacerState> _racers;
        private readonly List<RaceEvent> _events = new List<RaceEvent>();
        private readonly double[] _pendingSpeed;
        private readonly FlowSegment[] _plannedSnapshot;
        private readonly FlowSegment[] _newPlans;
        private readonly int[] _rankOrder;
        private readonly double _timeoutSeconds;

        public RaceSimulator(RaceCourse course, IReadOnlyList<RaceEntrant> entrants, RaceConditions conditions, BalanceConfig balance, ulong seed)
        {
            Course = course ?? throw new ArgumentNullException(nameof(course));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            Conditions = conditions ?? RaceConditions.Default;
            if (entrants == null || entrants.Count == 0) throw new ArgumentException("出走者がいません。", nameof(entrants));
            if (balance.Race.TickRate <= 0) throw new ArgumentException("race.tickRate は正の値にしてください。");

            Seed = seed;
            TickRate = balance.Race.TickRate;
            DeltaTime = 1.0 / TickRate;
            _timeoutSeconds = balance.Race.TimeoutSeconds;
            _speed = new SpeedCalculator(balance, Conditions, course);
            _routes = new RouteSelector(balance, course, _speed);
            _furnace = new FurnaceRules(balance);
            _chancePoints = CreateChancePoints(seed);

            int count = entrants.Count;
            _racers = new List<RacerState>(count);
            _pendingSpeed = new double[count];
            _plannedSnapshot = new FlowSegment[count];
            _newPlans = new FlowSegment[count];
            _rankOrder = new int[count];

            for (int i = 0; i < count; i++)
            {
                var racer = new RacerState(i, entrants[i], seed)
                {
                    // 枠順に等間隔で横に並べる
                    Lateral = -1.0 + (2.0 * i + 1.0) / count,
                    Rank = i + 1,
                };
                racer.TargetLateral = racer.Lateral;
                racer.HomeLateral = racer.Lateral;
                racer.StaticSpeedFactor = _speed.StaticFactor(racer.Entrant);
                racer.SafeDurationSeconds = _furnace.SafeDuration(racer.Entrant);
                EnterSegment(racer, course.StartSegment, 0);
                racer.CurrentSpeed = _speed.TargetSpeed(racer) * balance.Race.StartSpeedRatio;
                racer.Progress = 0;
                _racers.Add(racer);
                _rankOrder[i] = i;
            }

            PlanRoutes(0);
        }

        public RaceCourse Course { get; }
        public RaceConditions Conditions { get; }
        public ulong Seed { get; }
        public int TickRate { get; }
        public double DeltaTime { get; }
        public int Tick { get; private set; }
        public double ElapsedSeconds => Tick * DeltaTime;
        public bool IsFinished { get; private set; }
        public bool TimedOut { get; private set; }
        public IReadOnlyList<RacerState> Racers => _racers;
        public IReadOnlyList<RaceEvent> Events => _events;

        /// <summary>順位順の出走者（1位から）。</summary>
        public RacerState RacerAtRank(int rank)
        {
            return _racers[_rankOrder[rank - 1]];
        }

        public RaceResult RunToEnd()
        {
            while (!IsFinished)
            {
                Step();
            }

            return BuildResult();
        }

        public void Step()
        {
            if (IsFinished) return;

            int tick = Tick + 1;
            double dt = DeltaTime;

            // 0. 入力反映
            ApplyPendingInputs(tick);

            // A. 判断
            for (int i = 0; i < _racers.Count; i++)
            {
                var racer = _racers[i];
                _pendingSpeed[i] = racer.CurrentSpeed;
                if (racer.IsFinished) continue;
                Decide(racer, tick, dt);
            }

            // B. 移動
            for (int i = 0; i < _racers.Count; i++)
            {
                var racer = _racers[i];
                if (racer.IsFinished) continue;
                Move(racer, _pendingSpeed[i], tick, dt);
            }

            // C. 進路計画
            PlanRoutes(tick);

            // D. 順位
            UpdateRanks();

            Tick = tick;
            if (AllFinished())
            {
                IsFinished = true;
            }
            else if (ElapsedSeconds >= _timeoutSeconds)
            {
                IsFinished = true;
                TimedOut = true;
                _events.Add(new RaceEvent(tick, RaceEventType.Timeout, -1));
            }
        }

        public RaceResult BuildResult()
        {
            var placings = new List<RacePlacing>(_racers.Count);
            for (int rank = 1; rank <= _racers.Count; rank++)
            {
                var racer = RacerAtRank(rank);
                placings.Add(new RacePlacing(racer.Index, racer.Entrant.Id, rank, racer.IsFinished, racer.FinishTimeSeconds));
            }

            return new RaceResult(Seed, Tick, TickRate, TimedOut, placings, _events.ToArray(), _inputLog.ToArray());
        }

        private void Decide(RacerState racer, int tick, double dt)
        {
            var stats = _balance.Stats;
            var lane = _balance.Lane;
            UpdateLeafPowerAndFurnace(racer, tick, dt);
            racer.TargetSpeed = _speed.TargetSpeed(racer) * FurnaceSpeedMultiplier(racer);

            // 姿勢崩れ。乱数は毎Tick必ず1回引く（状況で消費数を変えず再現性を保つ）
            double stumbleRoll = racer.MovementRandom.NextDouble();
            if (racer.StunRemaining <= 0 && stumbleRoll < _speed.StumbleHazardPerSecond(racer.Entrant, racer.Segment) * dt)
            {
                _pendingSpeed[racer.Index] = Math.Min(_pendingSpeed[racer.Index], racer.CurrentSpeed * (1.0 - stats.StumbleSlowdown));
                racer.StunRemaining = MathUtil.LerpByStat(stats.StunSecondsByStability, racer.Entrant.Stats.Stability);
                _events.Add(new RaceEvent(tick, RaceEventType.Stumble, racer.Index, racer.Segment.Id, -1, racer.CurrentSpeed));
            }

            int blockerIndex = FindBlocker(racer);
            if (blockerIndex < 0)
            {
                racer.IsBlocked = false;
                racer.BlockedSeconds = 0;
                if (racer.SinceLaneChangeSeconds >= lane.ReturnToHomeLaneDelaySeconds
                    && Math.Abs(racer.TargetLateral - racer.HomeLateral) > 0.01
                    && Math.Abs(racer.Lateral - racer.TargetLateral) < 0.05)
                {
                    TryReturnToHomeLane(racer, tick);
                }

                return;
            }

            var blocker = _racers[blockerIndex];
            racer.IsBlocked = true;
            racer.BlockedSeconds += dt;

            double speedDiff = racer.CurrentSpeed - blocker.CurrentSpeed;
            if (racer.StunRemaining <= 0 && speedDiff > lane.ContactSpeedDiffThreshold)
            {
                // 接触: 安定が高いほど減速・スタンが小さい（04 §6）
                double slowdown = MathUtil.LerpByStat(stats.ContactSlowdownByStability, racer.Entrant.Stats.Stability);
                if (racer.IsFurnaceActive) slowdown *= _furnace.ContactSlowdownMultiplier;
                double variance = 0.75 + 0.5 * racer.ContactRandom.NextDouble();
                _pendingSpeed[racer.Index] = Math.Min(_pendingSpeed[racer.Index], racer.CurrentSpeed * (1.0 - slowdown * variance));
                racer.StunRemaining = MathUtil.LerpByStat(stats.StunSecondsByStability, racer.Entrant.Stats.Stability);
                _events.Add(new RaceEvent(tick, RaceEventType.Contact, racer.Index, racer.Segment.Id, blocker.Index, speedDiff));
            }

            racer.TargetSpeed = Math.Min(racer.TargetSpeed, blocker.CurrentSpeed);

            double reaction = MathUtil.LerpByStat(stats.BlockReactionSecondsByJudgement, racer.Entrant.Stats.Judgement);
            if (racer.BlockedSeconds >= reaction && Math.Abs(racer.Lateral - racer.TargetLateral) < 0.05)
            {
                TryDodge(racer, tick);
            }
        }

        private void Move(RacerState racer, double startSpeed, int tick, double dt)
        {
            var stats = _balance.Stats;
            var lane = _balance.Lane;
            var entrant = racer.Entrant;

            double rate = racer.TargetSpeed > startSpeed ? _speed.AccelRate(entrant) : stats.DecelRate;
            if (racer.IsFurnaceActive && racer.TargetSpeed > startSpeed) rate *= _furnace.AccelMultiplier;
            racer.CurrentSpeed = MathUtil.MoveTowards(startSpeed, racer.TargetSpeed, rate * dt);

            double agility = (entrant.Stats.Acceleration + entrant.Stats.Stability) / 2.0;
            double lateralSpeed = lane.LateralSpeedPerSecond * MathUtil.Lerp(lane.LateralSpeedMulByAgility[0], lane.LateralSpeedMulByAgility[1], agility / 100.0);
            racer.Lateral = MathUtil.MoveTowards(racer.Lateral, racer.TargetLateral, lateralSpeed * dt);

            double move = racer.CurrentSpeed * dt;
            racer.Offset += move;
            while (racer.Offset >= racer.Segment.Length)
            {
                if (racer.Segment.IsGoal)
                {
                    // ゴール線を跨いだ Tick 内で線形補間（04 §1.1）
                    double overshoot = racer.Offset - racer.Segment.Length;
                    double fraction = move > 0 ? (move - overshoot) / move : 1.0;
                    racer.FinishTimeSeconds = (tick - 1 + fraction) * dt;
                    racer.IsFinished = true;
                    racer.Offset = racer.Segment.Length;
                    racer.Progress = 1.0;
                    racer.PlannedNext = null;
                    _events.Add(new RaceEvent(tick, RaceEventType.Finish, racer.Index, racer.Segment.Id, -1, racer.FinishTimeSeconds));
                    return;
                }

                racer.Offset -= racer.Segment.Length;
                EnterSegment(racer, racer.PlannedNext ?? racer.Segment.Next[0], tick);
            }

            racer.Progress = racer.Segment.ProgressAt(racer.Offset);
            racer.StunRemaining = Math.Max(0, racer.StunRemaining - dt);
            racer.SinceLaneChangeSeconds += dt;
        }

        private void EnterSegment(RacerState racer, FlowSegment segment, int tick)
        {
            racer.Segment = segment;
            // 判断が高いほど良い流れを見つけやすい（一様乱数を指数で偏らせる）
            double jitter = _balance.Race.SegmentSpeedJitter;
            double skew = MathUtil.LerpByStat(_balance.Stats.GoodFlowSkewByJudgement, racer.Entrant.Stats.Judgement);
            double roll = Math.Pow(racer.MovementRandom.NextDouble(), skew);
            racer.SegmentJitter = 1.0 + (roll * 2.0 - 1.0) * jitter;
            racer.PlannedNext = segment.Next.Count == 1 ? segment.Next[0] : null;
            racer.NeedsRoutePlan = segment.Next.Count > 1;
            _events.Add(new RaceEvent(tick, RaceEventType.EnterSegment, racer.Index, segment.Id));
            if (racer.SegmentJitter - 1.0 >= _balance.Race.GoodFlowEventThreshold)
            {
                _events.Add(new RaceEvent(tick, RaceEventType.GoodFlow, racer.Index, segment.Id, -1, racer.SegmentJitter));
            }
        }

        private void PlanRoutes(int tick)
        {
            bool any = false;
            for (int i = 0; i < _racers.Count; i++)
            {
                _plannedSnapshot[i] = _racers[i].PlannedNext;
                _newPlans[i] = null;
                any |= _racers[i].NeedsRoutePlan;
            }

            if (!any) return;

            for (int i = 0; i < _racers.Count; i++)
            {
                var racer = _racers[i];
                if (!racer.NeedsRoutePlan || racer.IsFinished) continue;
                _newPlans[i] = _routes.Choose(racer, _racers, _plannedSnapshot);
            }

            for (int i = 0; i < _racers.Count; i++)
            {
                var racer = _racers[i];
                if (_newPlans[i] == null) continue;
                racer.PlannedNext = _newPlans[i];
                racer.NeedsRoutePlan = false;
                _events.Add(new RaceEvent(tick, RaceEventType.BranchChoice, racer.Index, _newPlans[i].Id, -1, racer.Segment.Next.Count));
            }
        }

        /// <summary>同じ Segment（または予定している次Segmentの入口）で、すぐ前を同じ横位置で流れている相手。</summary>
        private int FindBlocker(RacerState racer)
        {
            var lane = _balance.Lane;
            int blocker = -1;
            double nearestGap = double.PositiveInfinity;
            double myLateralMeters = LateralMeters(racer.Segment, racer.Lateral);

            for (int i = 0; i < _racers.Count; i++)
            {
                var other = _racers[i];
                if (other == racer || other.IsFinished) continue;

                double gap;
                double otherLateralMeters;
                if (other.Segment == racer.Segment)
                {
                    gap = other.Offset - racer.Offset;
                    otherLateralMeters = LateralMeters(other.Segment, other.Lateral);
                }
                else if (other.Segment == racer.PlannedNext)
                {
                    gap = racer.Segment.Length - racer.Offset + other.Offset;
                    otherLateralMeters = LateralMeters(other.Segment, other.Lateral);
                }
                else
                {
                    continue;
                }

                if (gap <= 0 || gap > lane.BlockDistanceMeters) continue;
                if (Math.Abs(myLateralMeters - otherLateralMeters) >= lane.RacerWidthMeters) continue;

                // 同距離は index で決める（処理順に依存させない）
                if (gap < nearestGap || (gap == nearestGap && i < blocker))
                {
                    nearestGap = gap;
                    blocker = i;
                }
            }

            return blocker;
        }

        private void TryDodge(RacerState racer, int tick)
        {
            double step = LaneStep(racer.Segment);
            if (step <= 0) return;

            // 中央寄りを先に試す（端は逃げ場がないため）。中央にいる場合は枠番の偶奇で左右を決める
            double towardCenter = racer.Lateral > 0.001 ? -1.0 : (racer.Lateral < -0.001 ? 1.0 : (racer.Index % 2 == 0 ? 1.0 : -1.0));
            for (int k = 1; k <= 3; k++)
            {
                if (TrySetLateral(racer, racer.Lateral + towardCenter * k * step, tick)) return;
                if (TrySetLateral(racer, racer.Lateral - towardCenter * k * step, tick)) return;
            }
        }

        private void TryReturnToHomeLane(RacerState racer, int tick)
        {
            double step = LaneStep(racer.Segment);
            if (step <= 0) return;

            double diff = racer.HomeLateral - racer.TargetLateral;
            double target = Math.Abs(diff) <= step
                ? racer.HomeLateral
                : racer.TargetLateral + Math.Sign(diff) * step;
            TrySetLateral(racer, target, tick);
        }

        private bool TrySetLateral(RacerState racer, double candidate, int tick)
        {
            if (candidate < -1.0 || candidate > 1.0) return false;
            if (!IsLaneClear(racer, candidate)) return false;

            racer.TargetLateral = candidate;
            racer.SinceLaneChangeSeconds = 0;
            racer.BlockedSeconds = 0;
            _events.Add(new RaceEvent(tick, RaceEventType.LaneChange, racer.Index, racer.Segment.Id, -1, candidate));
            return true;
        }

        private bool IsLaneClear(RacerState racer, double lateral)
        {
            var lane = _balance.Lane;
            double candidateMeters = LateralMeters(racer.Segment, lateral);
            foreach (var other in _racers)
            {
                if (other == racer || other.IsFinished || other.Segment != racer.Segment) continue;
                double gap = other.Offset - racer.Offset;
                if (gap <= -lane.RacerWidthMeters || gap >= lane.ClearLookAheadMeters) continue;
                if (Math.Abs(LateralMeters(other.Segment, other.Lateral) - candidateMeters) < lane.RacerWidthMeters) return false;
            }

            return true;
        }

        /// <summary>出走者1体分の横幅を、正規化横位置の刻みに換算する。狭すぎて動けない場合は 0。</summary>
        private double LaneStep(FlowSegment segment)
        {
            double halfFree = (segment.Width - _balance.Lane.RacerWidthMeters) / 2.0;
            if (halfFree <= 0.01) return 0;
            return _balance.Lane.RacerWidthMeters * 1.1 / halfFree;
        }

        private double LateralMeters(FlowSegment segment, double lateral)
        {
            double halfFree = Math.Max(0, (segment.Width - _balance.Lane.RacerWidthMeters) / 2.0);
            return lateral * halfFree;
        }

        private void UpdateRanks()
        {
            // 挿入ソート（8体なのでアロケーションなしで十分速い）
            for (int i = 1; i < _rankOrder.Length; i++)
            {
                int current = _rankOrder[i];
                int j = i - 1;
                while (j >= 0 && Compare(_racers[_rankOrder[j]], _racers[current]) > 0)
                {
                    _rankOrder[j + 1] = _rankOrder[j];
                    j--;
                }

                _rankOrder[j + 1] = current;
            }

            for (int rank = 0; rank < _rankOrder.Length; rank++)
            {
                _racers[_rankOrder[rank]].Rank = rank + 1;
            }
        }

        /// <summary>負なら a が上位。</summary>
        private static int Compare(RacerState a, RacerState b)
        {
            if (a.IsFinished != b.IsFinished) return a.IsFinished ? -1 : 1;
            if (a.IsFinished)
            {
                // ミリ秒まで同じなら枠番順（04 §11）
                long am = (long)Math.Round(a.FinishTimeSeconds * 1000.0);
                long bm = (long)Math.Round(b.FinishTimeSeconds * 1000.0);
                if (am != bm) return am < bm ? -1 : 1;
            }
            else if (a.Progress != b.Progress)
            {
                return a.Progress > b.Progress ? -1 : 1;
            }

            return a.Index.CompareTo(b.Index);
        }

        private bool AllFinished()
        {
            foreach (var racer in _racers)
            {
                if (!racer.IsFinished) return false;
            }

            return true;
        }
    }
}
