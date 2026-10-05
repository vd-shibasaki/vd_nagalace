using System;
using System.Collections.Generic;
using NagaRace.Domain.Common;
using NagaRace.Domain.Lineage;

namespace NagaRace.Domain.Characters
{
    /// <summary>個体の状態（08 §1 status）。</summary>
    public enum CharacterStatus
    {
        Active,
        Retired,
        Lost,
    }

    /// <summary>1レース分の戦績。</summary>
    public sealed class RaceHistoryEntry
    {
        public RaceHistoryEntry(int day, string courseId, int rank, long timeMilliseconds, bool retired, bool lost)
        {
            Day = day;
            CourseId = courseId;
            Rank = rank;
            TimeMilliseconds = timeMilliseconds;
            Retired = retired;
            Lost = lost;
        }

        public int Day { get; }
        public string CourseId { get; }
        public int Rank { get; }
        /// <summary>未ゴールは -1。</summary>
        public long TimeMilliseconds { get; }
        public bool Retired { get; }
        public bool Lost { get; }
    }

    /// <summary>生涯戦績（02 §12 思い出アルバムの元データ）。</summary>
    public sealed class CharacterRecord
    {
        private readonly List<RaceHistoryEntry> _history = new List<RaceHistoryEntry>();
        private readonly Dictionary<string, long> _bestTimes = new Dictionary<string, long>();

        public int Starts { get; private set; }
        public int Wins { get; private set; }
        public int Top3 { get; private set; }
        public int Retirements { get; private set; }
        /// <summary>葉脈炉の最長稼働（秒）。</summary>
        public double MaxFurnaceSeconds { get; private set; }
        public IReadOnlyList<RaceHistoryEntry> History => _history;
        public IReadOnlyDictionary<string, long> BestTimes => _bestTimes;

        public void Add(RaceHistoryEntry entry, double furnaceSeconds)
        {
            _history.Add(entry);
            Starts++;
            if (entry.Retired) Retirements++;
            else
            {
                if (entry.Rank == 1) Wins++;
                if (entry.Rank <= 3) Top3++;
            }

            if (entry.TimeMilliseconds > 0 && (!_bestTimes.TryGetValue(entry.CourseId, out long best) || entry.TimeMilliseconds < best))
            {
                _bestTimes[entry.CourseId] = entry.TimeMilliseconds;
            }

            MaxFurnaceSeconds = Math.Max(MaxFurnaceSeconds, furnaceSeconds);
        }

        /// <summary>最も成績が良かった（平均着順が良い）コース。</summary>
        public string FavoriteCourseId
        {
            get
            {
                string best = null;
                double bestAverage = double.MaxValue;
                var sums = new Dictionary<string, (int sum, int count)>();
                foreach (var h in _history)
                {
                    sums.TryGetValue(h.CourseId, out var v);
                    sums[h.CourseId] = (v.sum + h.Rank, v.count + 1);
                }

                foreach (var pair in sums)
                {
                    double average = (double)pair.Value.sum / pair.Value.count;
                    if (average < bestAverage)
                    {
                        bestAverage = average;
                        best = pair.Key;
                    }
                }

                return best;
            }
        }

        /// <summary>保存データからの復元用。</summary>
        public static CharacterRecord Restore(IEnumerable<RaceHistoryEntry> history, IEnumerable<double> furnaceSeconds)
        {
            var record = new CharacterRecord();
            using (var seconds = furnaceSeconds.GetEnumerator())
            {
                foreach (var entry in history)
                {
                    record.Add(entry, seconds.MoveNext() ? seconds.Current : 0);
                }
            }

            return record;
        }
    }

    /// <summary>
    /// プレイヤーが育てる1個体の可変状態（08 §1）。セーブ対象。
    /// ScriptableObject には保存しない（CLAUDE.md §4）。
    /// </summary>
    public sealed class CharacterState
    {
        public CharacterState(
            string id,
            string name,
            string speciesId,
            CharacterStats stats,
            FurnaceStats furnace,
            Affinities affinities,
            Personality personality,
            int maxDurability = 100,
            int bornDay = 1,
            string lineageId = null,
            int generation = 1)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? id;
            SpeciesId = speciesId;
            Stats = stats ?? CharacterStats.Uniform(50);
            Furnace = furnace ?? FurnaceStats.Uniform(50);
            Affinities = affinities ?? Affinities.NeutralAll;
            Personality = personality;
            MaxDurability = Math.Max(1, maxDurability);
            Durability = MaxDurability;
            BornDay = bornDay;
            LineageId = lineageId;
            Generation = generation;
            Condition = Condition.Normal;
            Status = CharacterStatus.Active;
        }

        public string Id { get; }
        public string Name { get; private set; }
        public string SpeciesId { get; }
        public string LineageId { get; set; }
        public int Generation { get; set; }
        public int BornDay { get; }

        public CharacterStats Stats { get; set; }
        public FurnaceStats Furnace { get; set; }
        public Affinities Affinities { get; set; }
        public Personality Personality { get; set; }
        public Condition Condition { get; set; }

        /// <summary>疲労 0〜100。</summary>
        public int Fatigue { get; private set; }
        public int Durability { get; private set; }
        public int MaxDurability { get; private set; }

        public CharacterStatus Status { get; private set; }
        public int? EndedDay { get; private set; }
        /// <summary>引退・ロストの理由（08 §1 endReason）。</summary>
        public string EndReason { get; private set; }

        public CharacterRecord Record { get; private set; } = new CharacterRecord();

        // ---- 成長（M6） ----

        public GrowthProfile Growth { get; set; } = new GrowthProfile(GrowthType.Normal, 10, 24, 28, 16);

        /// <summary>生まれてからの日数（0始まり）。</summary>
        public int AgeDays(int today) => Math.Max(0, today - BornDay);

        public GrowthPhase PhaseOn(int today) => Growth.PhaseAt(AgeDays(today));

        // ---- 育成（M5） ----

        private readonly Dictionary<string, double> _bodyAccumulation = new Dictionary<string, double>();
        private readonly List<string> _bodyTraits = new List<string>();
        private readonly List<string> _traits = new List<string>();

        /// <summary>体質の隠れ累積値（05 §4: moisture / dryness / rapidExperience 等）。</summary>
        public IReadOnlyDictionary<string, double> BodyAccumulation => _bodyAccumulation;
        /// <summary>獲得済みの体質 id。</summary>
        public IReadOnlyList<string> BodyTraits => _bodyTraits;
        /// <summary>特性 id（02 §5）。継承・突然変異で付く。</summary>
        public IReadOnlyList<string> Traits => _traits;

        // ---- 継承（M7） ----

        private readonly List<GeneFactor> _factors = new List<GeneFactor>();

        /// <summary>親木から受け継いだ因子（05 §8）。</summary>
        public IReadOnlyList<GeneFactor> Factors => _factors;

        /// <summary>突然変異（05 §9）。なければ null。</summary>
        public string MutationId { get; set; }

        public void SetFactors(IEnumerable<GeneFactor> factors)
        {
            _factors.Clear();
            if (factors != null) _factors.AddRange(factors);
        }

        /// <summary>直前に行ったならし（同じならしの連続回数の判定に使う）。</summary>
        public string LastTrainingId { get; private set; }
        public int TrainingStreak { get; private set; }

        public void AddBodyAccumulation(string key, double amount)
        {
            _bodyAccumulation.TryGetValue(key, out double current);
            _bodyAccumulation[key] = Math.Max(0, current + amount);
        }

        public bool HasBodyTrait(string id) => _bodyTraits.Contains(id);

        public void AddBodyTrait(string id)
        {
            if (!_bodyTraits.Contains(id)) _bodyTraits.Add(id);
        }

        public void AddTrait(string id)
        {
            if (!_traits.Contains(id)) _traits.Add(id);
        }

        /// <summary>ならしを記録し、同じならしの連続回数（今回を含む）を返す。</summary>
        public int RecordTraining(string trainingId)
        {
            TrainingStreak = trainingId == LastTrainingId ? TrainingStreak + 1 : 1;
            LastTrainingId = trainingId;
            return TrainingStreak;
        }

        /// <summary>保存データからの復元用。</summary>
        public void RestoreTraining(IDictionary<string, double> accumulation, IEnumerable<string> bodyTraits, IEnumerable<string> traits, string lastTrainingId, int streak)
        {
            _bodyAccumulation.Clear();
            if (accumulation != null)
            {
                foreach (var pair in accumulation) _bodyAccumulation[pair.Key] = pair.Value;
            }

            _bodyTraits.Clear();
            if (bodyTraits != null) _bodyTraits.AddRange(bodyTraits);
            _traits.Clear();
            if (traits != null) _traits.AddRange(traits);
            LastTrainingId = lastTrainingId;
            TrainingStreak = streak;
        }

        public bool IsActive => Status == CharacterStatus.Active;

        public void Rename(string name)
        {
            if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
        }

        public void SetFatigue(int value)
        {
            Fatigue = MathUtil.Clamp(value, 0, 100);
        }

        public void SetDurability(int value)
        {
            Durability = MathUtil.Clamp(value, 0, MaxDurability);
        }

        /// <summary>最大耐久を下げる（大怪我、02 §9）。現在値も上限に合わせる。</summary>
        public void ReduceMaxDurability(int amount)
        {
            MaxDurability = Math.Max(1, MaxDurability - Math.Max(0, amount));
            Durability = Math.Min(Durability, MaxDurability);
        }

        public void Retire(int day)
        {
            if (!IsActive) return;
            Status = CharacterStatus.Retired;
            EndedDay = day;
            EndReason = "Retired";
        }

        public void MarkLost(int day, string reason)
        {
            if (!IsActive) return;
            Status = CharacterStatus.Lost;
            EndedDay = day;
            EndReason = reason;
        }

        /// <summary>保存データからの復元用（Infrastructure の Save で使う）。</summary>
        public void RestoreRuntime(int fatigue, int durability, int maxDurability, CharacterStatus status, int? endedDay, string endReason, CharacterRecord record)
        {
            MaxDurability = Math.Max(1, maxDurability);
            Durability = MathUtil.Clamp(durability, 0, MaxDurability);
            Fatigue = MathUtil.Clamp(fatigue, 0, 100);
            Status = status;
            EndedDay = endedDay;
            EndReason = endReason;
            Record = record ?? new CharacterRecord();
        }
    }
}
