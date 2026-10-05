using System;
using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Race
{
    /// <summary>悪葉っぱの妨害スキル（04 §10, 02 §13）。</summary>
    public enum ObstructionSkill
    {
        /// <summary>体当たり: 隣の相手を減速させる。自分も姿勢を崩す。</summary>
        Ram,
        /// <summary>泥はね: 後ろの相手の判断を数秒鈍らせる。</summary>
        Mud,
        /// <summary>渦: 自分の後方の水面に短時間の荒れ（DangerZone）を作る。</summary>
        Whirl,
        /// <summary>葉力妨害: 相手の葉力の溜まり方を落とす。</summary>
        LeafDrain,
    }

    /// <summary>渦で荒れた区間。</summary>
    public sealed class WhirlZone
    {
        public FlowSegment Segment { get; set; }
        public double From { get; set; }
        public double To { get; set; }
        public int ExpireTick { get; set; }
        public int OwnerIndex { get; set; }
    }

    /// <summary>
    /// 妨害スキル。「強い妨害キャラ」ではなく戦術型（02 §13）: 葉力・クールダウン・自分のバランスを代償にする。
    /// 妨害によるダメージ・姿勢崩れでは耐久は1未満にならず、Lost もしない（10 C-03）。
    /// </summary>
    public sealed partial class RaceSimulator
    {
        private readonly List<WhirlZone> _whirlZones = new List<WhirlZone>();

        public IReadOnlyList<WhirlZone> WhirlZones => _whirlZones;

        private bool IsObstructorSpecies(RaceEntrant entrant)
        {
            var ids = _balance.Obstruction?.SpeciesIds;
            return ids != null && Array.IndexOf(ids, entrant.SpeciesId) >= 0;
        }

        /// <summary>毎Tickの妨害の状態更新（効果時間・クールダウン・渦の消滅）。</summary>
        private void UpdateObstructionTimers(RacerState racer, int tick, double dt)
        {
            racer.MudRemaining = Math.Max(0, racer.MudRemaining - dt);
            racer.LeafDrainRemaining = Math.Max(0, racer.LeafDrainRemaining - dt);
            racer.GlobalSkillCooldown = Math.Max(0, racer.GlobalSkillCooldown - dt);
            for (int i = 0; i < racer.SkillCooldowns.Length; i++)
            {
                racer.SkillCooldowns[i] = Math.Max(0, racer.SkillCooldowns[i] - dt);
            }
        }

        private void ExpireWhirlZones(int tick)
        {
            if (_whirlZones.Count > 0) _whirlZones.RemoveAll(z => z.ExpireTick <= tick);
        }

        /// <summary>この位置が渦の中か。</summary>
        private WhirlZone WhirlAt(RacerState racer)
        {
            foreach (var zone in _whirlZones)
            {
                if (zone.OwnerIndex != racer.Index && zone.Segment == racer.Segment && racer.Offset >= zone.From && racer.Offset <= zone.To) return zone;
            }

            return null;
        }

        /// <summary>妨害 AI。使える状態なら優先度順に1つだけ使う。</summary>
        private void RunObstructionAi(RacerState racer, int tick)
        {
            var o = _balance.Obstruction;
            if (!racer.IsObstructor || !racer.AutoControl || racer.GlobalSkillCooldown > 0 || racer.Progress < o.MinProgress) return;

            if (Ready(racer, ObstructionSkill.Ram, o.Ram))
            {
                var target = FindNearest(racer, -o.Ram.RangeMeters, o.Ram.RangeMeters, _balance.Lane.RacerWidthMeters * 1.5);
                if (target != null)
                {
                    UseRam(racer, target, tick);
                    return;
                }
            }

            if (Ready(racer, ObstructionSkill.Whirl, o.Whirl) && CountBehind(racer, o.Whirl.ZoneBehindTo) >= 2)
            {
                Spend(racer, ObstructionSkill.Whirl, o.Whirl);
                _whirlZones.Add(new WhirlZone
                {
                    Segment = racer.Segment,
                    From = Math.Max(0, racer.Offset - o.Whirl.ZoneBehindTo),
                    To = Math.Max(0, racer.Offset - o.Whirl.ZoneBehindFrom),
                    ExpireTick = tick + (int)Math.Round(o.Whirl.DurationSeconds * TickRate),
                    OwnerIndex = racer.Index,
                });
                _events.Add(new RaceEvent(tick, RaceEventType.SkillUsed, racer.Index, racer.Segment.Id, -1, (int)ObstructionSkill.Whirl));
                return;
            }

            if (Ready(racer, ObstructionSkill.Mud, o.Mud))
            {
                var target = FindNearest(racer, -o.Mud.RangeMeters, -0.5, double.MaxValue);
                if (target != null && target.MudRemaining <= 0)
                {
                    Spend(racer, ObstructionSkill.Mud, o.Mud);
                    target.MudRemaining = o.Mud.DurationSeconds;
                    _events.Add(new RaceEvent(tick, RaceEventType.SkillUsed, racer.Index, racer.Segment.Id, target.Index, (int)ObstructionSkill.Mud));
                    return;
                }
            }

            if (Ready(racer, ObstructionSkill.LeafDrain, o.LeafDrain))
            {
                var target = FindNearest(racer, 0.5, o.LeafDrain.RangeMeters, double.MaxValue);
                if (target != null && target.LeafDrainRemaining <= 0 && target.LeafPower >= _furnace.MinimumLeafPower * 0.6)
                {
                    Spend(racer, ObstructionSkill.LeafDrain, o.LeafDrain);
                    target.LeafDrainRemaining = o.LeafDrain.DurationSeconds;
                    _events.Add(new RaceEvent(tick, RaceEventType.SkillUsed, racer.Index, racer.Segment.Id, target.Index, (int)ObstructionSkill.LeafDrain));
                }
            }
        }

        private void UseRam(RacerState racer, RacerState target, int tick)
        {
            var ram = _balance.Obstruction.Ram;
            Spend(racer, ObstructionSkill.Ram, ram);
            _events.Add(new RaceEvent(tick, RaceEventType.SkillUsed, racer.Index, racer.Segment.Id, target.Index, (int)ObstructionSkill.Ram));

            // 炉の稼働中は妨害に強い（04 §8「一部妨害耐性」）
            double resist = target.IsFurnaceActive ? _furnace.ContactSlowdownMultiplier : 1.0;
            _pendingSpeed[target.Index] = Math.Min(_pendingSpeed[target.Index], target.CurrentSpeed * (1.0 - ram.TargetSlowdown * resist));
            target.StunRemaining = Math.Max(target.StunRemaining, ram.TargetStunSeconds * resist);
            ApplyDamage(target, ram.Damage, DamageCause.Obstruction, tick, majorIncident: false);

            // 代償: 自分もバランスを崩す
            _pendingSpeed[racer.Index] = Math.Min(_pendingSpeed[racer.Index], racer.CurrentSpeed * (1.0 - ram.SelfSlowdown));
            racer.StunRemaining = Math.Max(racer.StunRemaining, ram.SelfStunSeconds);
            ApplyDamage(racer, ram.Damage, DamageCause.Contact, tick, majorIncident: false);
        }

        private bool Ready(RacerState racer, ObstructionSkill skill, SkillBalance balance)
        {
            return racer.SkillCooldowns[(int)skill] <= 0 && racer.LeafPower >= balance.LeafCost;
        }

        private void Spend(RacerState racer, ObstructionSkill skill, SkillBalance balance)
        {
            racer.LeafPower -= balance.LeafCost;
            racer.SkillCooldowns[(int)skill] = balance.CooldownSeconds;
            racer.GlobalSkillCooldown = _balance.Obstruction.GlobalCooldownSeconds;
        }

        /// <summary>同じ Segment で、前後 [minGap, maxGap] m・横方向 maxLateral m 以内の最も近い相手。</summary>
        private RacerState FindNearest(RacerState racer, double minGap, double maxGap, double maxLateral)
        {
            RacerState best = null;
            double bestDistance = double.MaxValue;
            double myLateral = LateralMeters(racer.Segment, racer.Lateral);
            foreach (var other in _racers)
            {
                if (other == racer || other.IsOut || other.Segment != racer.Segment) continue;
                double gap = other.Offset - racer.Offset;
                if (gap < minGap || gap > maxGap) continue;
                if (Math.Abs(LateralMeters(other.Segment, other.Lateral) - myLateral) > maxLateral) continue;
                double distance = Math.Abs(gap);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = other;
                }
            }

            return best;
        }

        private int CountBehind(RacerState racer, double range)
        {
            int count = 0;
            foreach (var other in _racers)
            {
                if (other == racer || other.IsOut || other.Segment != racer.Segment) continue;
                double gap = racer.Offset - other.Offset;
                if (gap > 0 && gap <= range) count++;
            }

            return count;
        }
    }
}
