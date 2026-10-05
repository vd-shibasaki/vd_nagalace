using System;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Race
{
    /// <summary>ダメージの原因。妨害だけでは Lost させない（04 §9.1, 10 C-03）。</summary>
    public enum DamageCause
    {
        Contact,
        Stumble,
        Furnace,
        Accident,
        /// <summary>悪葉っぱの妨害スキル。TODO(M7以降): 妨害スキル実装時に使う。</summary>
        Obstruction,
    }

    /// <summary>
    /// 耐久ダメージ・リタイア・Lost 判定（04 §6, §9, 02 §11）。
    ///
    /// Lost の候補になるのは次の3つの重大事象だけ:
    /// - 葉脈炉の Critical（葉脈炉重大破損）
    /// - 炉の Blow で耐久が0になった（耐久0での大事故）
    /// - 危険区間の事故で耐久が0になった（激流事故）
    /// さらにコース危険度・出走時の耐久で候補を絞り、ハードガードで低リスクのレースでは絶対に起きない。
    /// </summary>
    public sealed partial class RaceSimulator
    {
        /// <summary>天候補正込みのコース危険度（全 Segment の最大）。</summary>
        public int CourseDanger { get; private set; }

        /// <summary>
        /// このレースでこの出走者が Lost の候補になりうるか（04 §9.1 の条件 2）。出走前の危険度表示と同じ判定。
        /// </summary>
        public static bool IsLostPossible(int courseDanger, int startDurability, NagaRace.Domain.Balance.BalanceConfig balance)
        {
            var lost = balance.Lost;
            bool hardGuard = courseDanger <= lost.HardGuardCourseDangerMax && startDurability >= lost.HardGuardDurabilityMin;
            if (hardGuard) return false;
            return courseDanger >= lost.CandidateCourseDangerMin || startDurability < balance.Durability.DamagedMin;
        }

        private void InitializeDurability()
        {
            int max = 1;
            foreach (var segment in Course.Segments)
            {
                max = Math.Max(max, _speed.EffectiveDanger(segment));
            }

            CourseDanger = max;
        }

        /// <summary>
        /// 耐久ダメージを与える。頑丈が高いほど軽減。majorIncident なら耐久0で Lost 判定へ進む。
        /// </summary>
        private void ApplyDamage(RacerState racer, double rawAmount, DamageCause cause, int tick, bool majorIncident)
        {
            if (racer.IsOut || rawAmount <= 0) return;

            double amount = rawAmount * MathUtil.LerpByStat(_balance.Stats.DamageMultiplierByToughness, racer.Entrant.Stats.Toughness);
            if (cause == DamageCause.Obstruction)
            {
                // 妨害だけでは耐久を1未満にしない
                amount = Math.Min(amount, Math.Max(0, racer.CurrentDurability - 1));
            }

            racer.DurabilityDamage += amount;
            _events.Add(new RaceEvent(tick, RaceEventType.DurabilityDamaged, racer.Index, racer.Segment.Id, (int)cause, amount));

            if (racer.CurrentDurability > 0) return;

            if (majorIncident && TryLost(racer, cause, tick)) return;
            Retire(racer, tick);
        }

        /// <summary>Lost 判定。候補なら確率で Lost。</summary>
        private bool TryLost(RacerState racer, DamageCause cause, int tick)
        {
            if (cause == DamageCause.Obstruction) return false;
            if (!IsLostPossible(CourseDanger, racer.Entrant.Durability, _balance)) return false;

            // 判定は Event ストリームで1回だけ引く
            if (racer.EventRandom.NextDouble() >= _balance.Lost.ChanceOnCandidate) return false;

            racer.IsLost = true;
            Retire(racer, tick);
            _events.Add(new RaceEvent(tick, RaceEventType.Lost, racer.Index, racer.Segment.Id, (int)cause));
            return true;
        }

        private void Retire(RacerState racer, int tick)
        {
            if (racer.IsRetired) return;
            racer.IsRetired = true;
            racer.RetiredAtTick = tick;
            racer.CurrentSpeed = 0;
            _pendingSpeed[racer.Index] = 0;
            if (racer.IsFurnaceActive || racer.FurnacePhase == FurnacePhase.Ignition) racer.FurnacePhase = FurnacePhase.Cooldown;
            racer.IsChanceOpen = false;
            _events.Add(new RaceEvent(tick, RaceEventType.Retire, racer.Index, racer.Segment.Id, -1, racer.Progress));
        }

        /// <summary>姿勢崩れが危険区間での重大事故に発展するか（04 §6「重大事故は危険Segment + 低耐久等の条件でのみ」）。</summary>
        private void CheckAccident(RacerState racer, int tick)
        {
            var d = _balance.Durability;
            int danger = _speed.EffectiveDanger(racer.Segment);
            if (d.AccidentChanceOnStumbleByDanger == null || !d.AccidentChanceOnStumbleByDanger.TryGetValue(danger.ToString(), out double chance)) return;

            chance *= racer.CurrentDurability < d.LightDamageMin ? d.LowDurabilityAccidentMultiplier : d.HealthyAccidentMultiplier;
            if (racer.EventRandom.NextDouble() >= chance) return;

            racer.HadAccident = true;
            _events.Add(new RaceEvent(tick, RaceEventType.CourseAccident, racer.Index, racer.Segment.Id, -1, danger));
            _pendingSpeed[racer.Index] = Math.Min(_pendingSpeed[racer.Index], racer.CurrentSpeed * (1.0 - _furnace.BlowSlowdown));
            racer.StunRemaining = Math.Max(racer.StunRemaining, _furnace.BlowStunSeconds);
            ApplyDamage(racer, d.AccidentDamage, DamageCause.Accident, tick, majorIncident: true);
        }

        private double FailureDamage(FurnaceFailure failure)
        {
            var table = _balance.Durability.FailureDamage;
            return table != null && table.TryGetValue(failure.ToString(), out double v) ? v : 0;
        }
    }
}
