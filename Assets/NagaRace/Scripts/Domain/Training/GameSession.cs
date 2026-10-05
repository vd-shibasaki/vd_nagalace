using System;
using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Common;
using NagaRace.Domain.Lineage;
using NagaRace.Domain.Race;

namespace NagaRace.Domain.Training
{
    /// <summary>1日経過したときの個体ごとの変化。</summary>
    public sealed class DailyChange
    {
        public string CharacterId { get; set; }
        public int DurabilityRecovered { get; set; }
        public int FatigueRecovered { get; set; }
        /// <summary>紅葉・枯葉期の衰えで下がった能力（なければ null）。</summary>
        public StatType? Declined { get; set; }
        public GrowthPhase PhaseBefore { get; set; }
        public GrowthPhase PhaseAfter { get; set; }
    }

    /// <summary>
    /// 1つのセーブデータ全体の進行状態（08 §7 の calendar / characters など）。
    /// 乱数は世界 Seed・日付・用途・個体 Id から毎回派生させるため、セーブ・ロードしても結果が変わらない。
    /// </summary>
    public sealed class GameSession
    {
        public GameSession(ulong worldSeed, int day = 1, Roster roster = null)
        {
            WorldSeed = worldSeed;
            Day = Math.Max(1, day);
            Roster = roster ?? new Roster();
        }

        public ulong WorldSeed { get; }
        /// <summary>通算日（1始まり）。1アクション = 1日。</summary>
        public int Day { get; private set; }
        public Roster Roster { get; }

        private readonly List<ParentTree> _parentTrees = new List<ParentTree>();

        /// <summary>親木園（05 §6）。</summary>
        public IReadOnlyList<ParentTree> ParentTrees => _parentTrees;

        public ParentTree FindParentTree(string id)
        {
            foreach (var t in _parentTrees)
            {
                if (t.Id == id) return t;
            }

            return null;
        }

        public void AddParentTree(ParentTree tree)
        {
            if (FindParentTree(tree.Id) != null) throw new InvalidOperationException($"親木 {tree.Id} は登録済みです。");
            _parentTrees.Add(tree);
        }

        public GameDate Date(BalanceConfig balance) => new GameDate(Day, balance.Calendar);

        public RaceConditions TodayConditions(BalanceConfig balance) => WeatherForecast.For(WorldSeed, Date(balance), balance);

        /// <summary>その日・用途・個体に固有の乱数。</summary>
        public IRandomProvider RandomFor(string purpose, string characterId = null)
        {
            ulong key = WorldSeed;
            key ^= RandomStreams.SplitMix64((ulong)Day * 0xD1B54A32D192ED03UL);
            key ^= RandomStreams.SplitMix64(RandomStreams.StableHash(purpose));
            key ^= RandomStreams.SplitMix64(RandomStreams.StableHash(characterId) * 31UL);
            return new Pcg32Random(RandomStreams.SplitMix64(key), RandomStreams.SplitMix64(key ^ 0xA5A5A5A5UL));
        }

        /// <summary>
        /// 1日進める。全現役個体の自然回復と、紅葉・枯葉期の衰えを処理する。
        /// </summary>
        public List<DailyChange> AdvanceDay(BalanceConfig balance)
        {
            var changes = new List<DailyChange>();
            int today = Day;
            Day++;
            foreach (var c in Roster.Active)
            {
                var change = new DailyChange
                {
                    CharacterId = c.Id,
                    PhaseBefore = c.PhaseOn(today),
                    PhaseAfter = c.PhaseOn(Day),
                };

                int durability = c.Durability;
                c.SetDurability(durability + balance.Training.DailyDurabilityRecovery);
                change.DurabilityRecovered = c.Durability - durability;

                int fatigue = c.Fatigue;
                c.SetFatigue(fatigue - balance.Training.DailyFatigueRecovery);
                change.FatigueRecovered = fatigue - c.Fatigue;

                change.Declined = GrowthRules.DailyDecline(c, change.PhaseAfter, balance, RandomFor("decline", c.Id));
                changes.Add(change);
            }

            return changes;
        }

        /// <summary>保存データからの復元・テスト用。</summary>
        public void SetDay(int day)
        {
            Day = Math.Max(1, day);
        }
    }
}
