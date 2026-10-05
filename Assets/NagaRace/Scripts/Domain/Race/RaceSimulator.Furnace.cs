using System;
using System.Collections.Generic;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Race
{
    /// <summary>
    /// 葉力・葉力チャンス・葉脈炉（04 §7〜8）とプレイヤー入力の処理。
    /// </summary>
    public sealed partial class RaceSimulator
    {
        private readonly FurnaceRules _furnace;
        private readonly double[] _chancePoints;
        private readonly List<(int EntrantIndex, int Order, RaceInputType Type)> _pendingInputs = new List<(int, int, RaceInputType)>();
        private readonly List<RaceInput> _inputLog = new List<RaceInput>();

        public FurnaceRules FurnaceRules => _furnace;

        /// <summary>葉力チャンスが開く進捗（全員共通）。</summary>
        public IReadOnlyList<double> ChanceProgressPoints => _chancePoints;

        /// <summary>反映済みのプレイヤー入力。</summary>
        public IReadOnlyList<RaceInput> Inputs => _inputLog;

        /// <summary>
        /// 入力を受け付ける。次の <see cref="Step"/> の最初に反映され、その Tick 番号で記録される。
        /// </summary>
        public void SubmitInput(int entrantIndex, RaceInputType type)
        {
            if (entrantIndex < 0 || entrantIndex >= _racers.Count) throw new ArgumentOutOfRangeException(nameof(entrantIndex));
            _pendingInputs.Add((entrantIndex, _pendingInputs.Count, type));
        }

        /// <summary>葉脈炉を始動できる状態か（UI の表示用）。</summary>
        public bool CanActivateFurnace(int entrantIndex)
        {
            var racer = _racers[entrantIndex];
            return !racer.IsOut && racer.FurnacePhase == FurnacePhase.Ready && racer.LeafPower >= _furnace.MinimumLeafPower;
        }

        private double[] CreateChancePoints(ulong seed)
        {
            var leaf = _balance.LeafPower;
            var random = RandomStreams.Create(seed, RandomStreams.SharedIndex, RandomStreamId.Event);
            int count = random.Range(leaf.ChanceCountRange[0], leaf.ChanceCountRange[1] + 1);
            var points = new double[count];
            double from = leaf.ChanceProgressRange[0];
            double width = (leaf.ChanceProgressRange[1] - from) / count;
            for (int i = 0; i < count; i++)
            {
                // 区間を等分し、各区間の中で位置をずらす（間隔が詰まりすぎない）
                points[i] = from + width * (i + random.Range(0.15, 0.85));
            }

            return points;
        }

        private void ApplyPendingInputs(int tick)
        {
            if (_pendingInputs.Count == 0) return;

            // 同じ Tick 内の複数入力は出走者番号順 → 受付順で処理する
            _pendingInputs.Sort((a, b) => a.EntrantIndex != b.EntrantIndex ? a.EntrantIndex.CompareTo(b.EntrantIndex) : a.Order.CompareTo(b.Order));
            foreach (var (index, _, type) in _pendingInputs)
            {
                var racer = _racers[index];
                if (racer.IsOut) continue;
                _inputLog.Add(new RaceInput(tick, index, type));
                switch (type)
                {
                    case RaceInputType.FurnaceActivate:
                        racer.AutoControl = false;
                        TryActivateFurnace(racer, tick, reportRejection: true);
                        break;
                    case RaceInputType.FurnaceRelease:
                        racer.AutoControl = false;
                        ReleaseFurnace(racer, tick);
                        break;
                    case RaceInputType.Charge:
                        TryCharge(racer, tick);
                        break;
                }
            }

            _pendingInputs.Clear();
        }

        private void UpdateLeafPowerAndFurnace(RacerState racer, int tick, double dt)
        {
            var leaf = _balance.LeafPower;
            double now = tick * dt;

            // 葉力の基礎獲得 + 後方ボーナス（何もしなくても溜まる）
            double gain = leaf.PassiveGainPerSecond;
            if (leaf.BehindBonusPerSecond != null && leaf.BehindBonusPerSecond.TryGetValue(racer.Rank.ToString(), out double bonus))
            {
                gain += bonus;
            }

            if (racer.LeafDrainRemaining > 0) gain *= _balance.Obstruction.LeafDrain.GainMultiplier;
            racer.LeafPower = Math.Min(leaf.Max, racer.LeafPower + gain * dt);
            UpdateChance(racer, tick, now);

            if (racer.AutoControl) RunFurnaceAi(racer, tick);
            UpdateFurnace(racer, tick, dt);
        }

        private void RecordFurnaceUse(RacerState racer, double dt)
        {
            racer.FurnaceSecondsTotal += dt;
            if (racer.FurnacePhase == FurnacePhase.ActiveRisk) racer.OverflowSecondsTotal += dt;
            racer.MaxFurnaceSeconds = Math.Max(racer.MaxFurnaceSeconds, racer.FurnaceActiveSeconds);
        }

        // ---------------- 葉力チャンス ----------------

        private void UpdateChance(RacerState racer, int tick, double now)
        {
            var leaf = _balance.LeafPower;
            if (racer.IsChanceOpen)
            {
                racer.ChanceRemainingSeconds = racer.ChanceOpenedAt + leaf.ChanceWindowSeconds - now;
                if (racer.ChanceRemainingSeconds <= 0)
                {
                    racer.IsChanceOpen = false;
                    racer.ChanceRemainingSeconds = 0;
                    if (!racer.ChanceUsed) _events.Add(new RaceEvent(tick, RaceEventType.LeafChanceMiss, racer.Index));
                }

                return;
            }

            if (racer.NextChanceIndex >= _chancePoints.Length || racer.Progress < _chancePoints[racer.NextChanceIndex]) return;

            racer.NextChanceIndex++;
            racer.IsChanceOpen = true;
            racer.ChanceUsed = false;
            racer.ChanceOpenedAt = now;
            racer.ChanceRemainingSeconds = leaf.ChanceWindowSeconds;
            _events.Add(new RaceEvent(tick, RaceEventType.LeafChanceOpen, racer.Index));

            if (racer.AutoControl)
            {
                // AI は判断が高いほど成功しやすい。乱数は成功・品質で必ず2回引く
                double successRoll = racer.EventRandom.NextDouble();
                double qualityRoll = racer.EventRandom.NextDouble();
                double successRate = MathUtil.LerpByStat(leaf.NpcChanceSuccessByJudgement, racer.Entrant.Stats.Judgement);
                if (successRoll < successRate)
                {
                    GrantChance(racer, tick, MathUtil.Lerp(leaf.ChanceMinQuality, 1.0, qualityRoll));
                }
            }
        }

        /// <summary>受付時間の中央で入力するほど多く獲得する。1回の受付で1度だけ（連打は無効）。</summary>
        private void TryCharge(RacerState racer, int tick)
        {
            if (!racer.IsChanceOpen || racer.ChanceUsed) return;

            var leaf = _balance.LeafPower;
            double now = tick * DeltaTime;
            double half = leaf.ChanceWindowSeconds / 2.0;
            double fromCenter = Math.Abs(now - (racer.ChanceOpenedAt + half)) / half;
            double quality = MathUtil.Lerp(1.0, leaf.ChanceMinQuality, MathUtil.Clamp01(fromCenter));
            GrantChance(racer, tick, quality);
        }

        private void GrantChance(RacerState racer, int tick, double quality)
        {
            var leaf = _balance.LeafPower;
            double amount = leaf.ChanceGain * quality;
            racer.LeafPower = Math.Min(leaf.Max, racer.LeafPower + amount);
            racer.ChanceUsed = true;
            _events.Add(new RaceEvent(tick, RaceEventType.LeafChanceHit, racer.Index, null, -1, amount));
        }

        // ---------------- 葉脈炉 ----------------

        private bool TryActivateFurnace(RacerState racer, int tick, bool reportRejection)
        {
            if (racer.FurnacePhase != FurnacePhase.Ready || racer.LeafPower < _furnace.MinimumLeafPower)
            {
                if (reportRejection) _events.Add(new RaceEvent(tick, RaceEventType.FurnaceRejected, racer.Index, null, -1, racer.LeafPower));
                return false;
            }

            racer.LeafPower = Math.Max(0, racer.LeafPower - _furnace.IgnitionCost(racer.Entrant));
            racer.FurnacePhase = FurnacePhase.Ignition;
            racer.FurnacePhaseSeconds = 0;
            racer.FurnaceActiveSeconds = 0;
            _events.Add(new RaceEvent(tick, RaceEventType.FurnaceIgnite, racer.Index));
            return true;
        }

        private void ReleaseFurnace(RacerState racer, int tick)
        {
            if (racer.FurnacePhase != FurnacePhase.Ignition && !racer.IsFurnaceActive) return;

            _events.Add(new RaceEvent(tick, RaceEventType.FurnaceRelease, racer.Index, null, -1, racer.FurnaceActiveSeconds));
            racer.FurnacePhase = FurnacePhase.Cooldown;
            racer.FurnaceCooldownRemaining = _furnace.Cooldown(racer.Entrant);
            racer.FurnacePhaseSeconds = 0;
        }

        private void UpdateFurnace(RacerState racer, int tick, double dt)
        {
            racer.FurnacePhaseSeconds += dt;
            switch (racer.FurnacePhase)
            {
                case FurnacePhase.Ignition:
                    if (racer.FurnacePhaseSeconds >= _furnace.IgnitionLag(racer.Entrant))
                    {
                        racer.FurnacePhase = FurnacePhase.ActiveSafe;
                        racer.FurnaceActiveSeconds = 0;
                        _events.Add(new RaceEvent(tick, RaceEventType.FurnaceStart, racer.Index));
                    }

                    break;

                case FurnacePhase.ActiveSafe:
                case FurnacePhase.ActiveRisk:
                    racer.FurnaceActiveSeconds += dt;
                    RecordFurnaceUse(racer, dt);
                    racer.LeafPower -= _furnace.DrainPerSecond * dt;
                    if (racer.LeafPower <= 0)
                    {
                        // 葉力切れは故障ではなく自動解除
                        racer.LeafPower = 0;
                        ReleaseFurnace(racer, tick);
                        break;
                    }

                    if (racer.FurnacePhase == FurnacePhase.ActiveSafe && racer.FurnaceActiveSeconds > racer.SafeDurationSeconds)
                    {
                        racer.FurnacePhase = FurnacePhase.ActiveRisk;
                        _events.Add(new RaceEvent(tick, RaceEventType.FurnaceOverflow, racer.Index));
                    }

                    if (racer.FurnacePhase == FurnacePhase.ActiveRisk)
                    {
                        // 故障抽選は RiskZone 中だけ（Safe 中に解除すれば乱数も消費しない: 10 B-02）
                        double overrun = racer.FurnaceActiveSeconds - racer.SafeDurationSeconds;
                        double chance = _furnace.HazardPerSecond(racer.Entrant, overrun) * dt;
                        if (racer.FurnaceRandom.NextDouble() < chance)
                        {
                            ApplyFailure(racer, _furnace.RollSeverity(racer.FurnaceRandom, overrun), tick);
                        }
                    }

                    break;

                case FurnacePhase.Cooldown:
                    racer.FurnaceCooldownRemaining -= dt;
                    if (racer.FurnaceCooldownRemaining <= 0)
                    {
                        racer.FurnaceCooldownRemaining = 0;
                        racer.FurnacePhase = FurnacePhase.Ready;
                    }

                    break;
            }
        }

        private void ApplyFailure(RacerState racer, FurnaceFailure failure, int tick)
        {
            racer.LastFailure = failure;
            if (failure > racer.WorstFailure) racer.WorstFailure = failure;
            if (failure >= FurnaceFailure.FurnaceDamage) racer.FurnaceDamaged = true;
            _events.Add(new RaceEvent(tick, RaceEventType.FurnaceFailureOccurred, racer.Index, racer.Segment.Id, -1, (int)failure));

            double slowdown;
            double stun;
            switch (failure)
            {
                case FurnaceFailure.MinorDamage:
                    slowdown = _furnace.MinorSlowdown;
                    stun = _furnace.MinorStunSeconds;
                    racer.FurnacePhase = FurnacePhase.Cooldown;
                    racer.FurnaceCooldownRemaining = _furnace.Cooldown(racer.Entrant);
                    break;
                case FurnaceFailure.FurnaceDamage:
                    slowdown = _furnace.MinorSlowdown;
                    stun = _furnace.MinorStunSeconds;
                    racer.FurnacePhase = FurnacePhase.Broken;
                    break;
                default:
                    slowdown = _furnace.BlowSlowdown;
                    stun = _furnace.BlowStunSeconds;
                    racer.FurnacePhase = FurnacePhase.Broken;
                    break;
            }

            _pendingSpeed[racer.Index] = Math.Min(_pendingSpeed[racer.Index], racer.CurrentSpeed * (1.0 - slowdown));
            racer.StunRemaining = Math.Max(racer.StunRemaining, stun);

            // Blow は耐久0で Lost 判定（耐久0での大事故）、Critical は耐久に関係なく Lost 判定（葉脈炉重大破損）
            bool major = failure >= FurnaceFailure.Blow;
            ApplyDamage(racer, FailureDamage(failure), DamageCause.Furnace, tick, major);
            if (failure == FurnaceFailure.Critical && !racer.IsOut)
            {
                TryLost(racer, DamageCause.Furnace, tick);
            }
        }

        private double FurnaceSpeedMultiplier(RacerState racer)
        {
            return racer.IsFurnaceActive
                ? _furnace.SpeedMultiplier(racer.Entrant, racer.FurnaceActiveSeconds, racer.SafeDurationSeconds)
                : 1.0;
        }

        /// <summary>
        /// 炉AI（NPC と「おまかせ炉」）。勝負所（終盤）で始動し、AI作戦に応じた長さで解除する（04 §13）。
        /// </summary>
        private void RunFurnaceAi(RacerState racer, int tick)
        {
            if (racer.FurnacePhase == FurnacePhase.Ready && racer.LeafPower >= _furnace.MinimumLeafPower)
            {
                bool finalStretch = racer.Progress >= _furnace.NpcFinalStretchProgress;
                bool gaugeFull = racer.LeafPower >= _furnace.NpcFullGaugeLeafPower;
                if (finalStretch || gaugeFull) TryActivateFurnace(racer, tick, reportRejection: false);
            }
            else if (racer.IsFurnaceActive)
            {
                double ratio = _balance.AiStyle.TryGetValue(racer.Entrant.AiStyle.ToString(), out var style) ? style.FurnaceReleaseRatio : 0.95;
                if (racer.FurnaceActiveSeconds >= racer.SafeDurationSeconds * ratio) ReleaseFurnace(racer, tick);
            }
        }
    }
}
