using System;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Race;

namespace NagaRace.Domain.Characters
{
    /// <summary>レース後に個体へ反映した変化（03 §8 Result の表示に使う）。</summary>
    public sealed class AftermathReport
    {
        public int DurabilityBefore { get; set; }
        public int DurabilityAfter { get; set; }
        public int MaxDurabilityBefore { get; set; }
        public int MaxDurabilityAfter { get; set; }
        public int FatigueBefore { get; set; }
        public int FatigueAfter { get; set; }
        public int FurnaceDurabilityLoss { get; set; }
        /// <summary>大怪我（最大耐久が下がった）。</summary>
        public bool Injured { get; set; }
        public bool Lost { get; set; }
        public bool Retired { get; set; }
        public int Rank { get; set; }
    }

    /// <summary>
    /// レース結果を個体へ反映する（07 §6 ResolveRace）。耐久・最大耐久・疲労・炉耐久・戦績。
    /// </summary>
    public static class RaceAftermath
    {
        public static AftermathReport Apply(CharacterState character, RacePlacing placing, string courseId, int day, BalanceConfig balance)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (placing == null) throw new ArgumentNullException(nameof(placing));

            var d = balance.Durability;
            var a = balance.Aftermath;
            var report = new AftermathReport
            {
                DurabilityBefore = character.Durability,
                MaxDurabilityBefore = character.MaxDurability,
                FatigueBefore = character.Fatigue,
                Rank = placing.Rank,
                Lost = placing.Lost,
                Retired = placing.Retired,
            };

            int damage = (int)Math.Round(placing.DurabilityDamage);
            character.SetDurability(character.Durability - damage);

            // 大怪我は最大耐久そのものが下がる（「使い込んだエース」の歴史が数値に残る）
            if (placing.DurabilityDamage >= d.InjuryDamageThreshold)
            {
                character.ReduceMaxDurability((int)Math.Round(placing.DurabilityDamage * d.InjuryMaxDurabilityLossRatio));
                report.Injured = true;
            }

            double fatigue = a.RaceFatigue + placing.FurnaceSeconds * a.FatiguePerFurnaceSecond + placing.OverflowSeconds * a.FatiguePerOverflowSecond;
            character.SetFatigue(character.Fatigue + (int)Math.Round(fatigue));

            if (placing.FurnaceDamaged)
            {
                var f = character.Furnace;
                report.FurnaceDurabilityLoss = Math.Min(f.Durability, d.FurnaceDurabilityLossOnDamage);
                character.Furnace = new FurnaceStats(f.Output, f.Startup, f.CriticalDuration, f.Cooling, f.Durability - report.FurnaceDurabilityLoss);
            }

            character.Record.Add(
                new RaceHistoryEntry(day, courseId, placing.Rank, placing.FinishTimeMilliseconds, placing.Retired, placing.Lost),
                placing.MaxFurnaceSeconds);

            report.DurabilityAfter = character.Durability;
            report.MaxDurabilityAfter = character.MaxDurability;
            report.FatigueAfter = character.Fatigue;
            return report;
        }

        /// <summary>Lost の理由（08 §1 endReason）。</summary>
        public static string LostReason(RacePlacing placing)
        {
            if (placing.WorstFailure == FurnaceFailure.Critical) return "LostFurnace";
            if (placing.HadAccident) return "LostRapidAccident";
            return "LostDurability";
        }
    }
}
