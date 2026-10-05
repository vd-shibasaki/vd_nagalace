using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NagaRace.Application.Save;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Lineage;
using NagaRace.Domain.Training;

namespace NagaRace.Infrastructure.Save
{
    /// <summary>GameSession / GameSettings と SaveDto の相互変換。</summary>
    public static class SaveMapper
    {
        private static readonly StatType[] StatTypes = (StatType[])Enum.GetValues(typeof(StatType));
        private static readonly FurnaceStat[] FurnaceStatTypes = (FurnaceStat[])Enum.GetValues(typeof(FurnaceStat));
        private static readonly AffinityType[] AffinityTypes = (AffinityType[])Enum.GetValues(typeof(AffinityType));

        public static SaveDto ToDto(GameSession session, GameSettings settings)
        {
            var dto = new SaveDto
            {
                World = new WorldDto { Seed = session.WorldSeed.ToString(CultureInfo.InvariantCulture), Day = session.Day },
                Settings = new SettingsDto
                {
                    AutoFurnace = settings.AutoFurnace,
                    PlayerAiStyle = settings.PlayerAiStyle.ToString(),
                    PlaybackSpeed = settings.PlaybackSpeed,
                    SelectedCharacterId = settings.SelectedCharacterId,
                },
            };

            foreach (var c in session.Roster.Active) dto.Characters.Add(ToDto(c));
            foreach (var entry in session.Roster.Album) dto.Characters.Add(ToDto(entry.Character));
            foreach (var t in session.ParentTrees) dto.ParentTrees.Add(ToDto(t));
            return dto;
        }

        public static (GameSession session, GameSettings settings) FromDto(SaveDto dto)
        {
            if (dto?.World == null) throw new FormatException("world がありません。");
            ulong seed = ulong.Parse(dto.World.Seed, CultureInfo.InvariantCulture);
            var roster = new Roster();
            foreach (var c in dto.Characters ?? new List<CharacterDto>())
            {
                roster.Add(FromDto(c));
            }

            var session = new GameSession(seed, dto.World.Day, roster);
            foreach (var t in dto.ParentTrees ?? new List<ParentTreeDto>())
            {
                session.AddParentTree(FromDto(t));
            }

            var s = dto.Settings ?? new SettingsDto();
            var settings = new GameSettings
            {
                AutoFurnace = s.AutoFurnace,
                PlayerAiStyle = ParseEnum(s.PlayerAiStyle, AiStyle.Standard),
                PlaybackSpeed = s.PlaybackSpeed <= 0 ? 1f : s.PlaybackSpeed,
                SelectedCharacterId = s.SelectedCharacterId,
            };
            return (session, settings);
        }

        private static CharacterDto ToDto(CharacterState c)
        {
            return new CharacterDto
            {
                Id = c.Id,
                Name = c.Name,
                SpeciesId = c.SpeciesId,
                LineageId = c.LineageId,
                Generation = c.Generation,
                BornDay = c.BornDay,
                Stats = StatTypes.Select(t => c.Stats[t]).ToArray(),
                Furnace = FurnaceStatTypes.Select(t => c.Furnace[t]).ToArray(),
                Affinities = AffinityTypes.Select(t => c.Affinities[t]).ToArray(),
                Personality = c.Personality.ToString(),
                Condition = c.Condition.ToString(),
                Fatigue = c.Fatigue,
                Durability = c.Durability,
                MaxDurability = c.MaxDurability,
                Status = c.Status.ToString(),
                EndedDay = c.EndedDay,
                EndReason = c.EndReason,
                Growth = new GrowthDto { Type = c.Growth.Type.ToString(), PhaseDays = c.Growth.PhaseDays.ToArray() },
                BodyAccumulation = c.BodyAccumulation.ToDictionary(p => p.Key, p => p.Value),
                BodyTraits = c.BodyTraits.ToList(),
                Traits = c.Traits.ToList(),
                LastTrainingId = c.LastTrainingId,
                TrainingStreak = c.TrainingStreak,
                Factors = c.Factors.Select(ToDto).ToList(),
                MutationId = c.MutationId,
                Record = new RecordDto
                {
                    MaxFurnaceSeconds = c.Record.MaxFurnaceSeconds,
                    History = c.Record.History.Select(h => new RaceHistoryDto
                    {
                        Day = h.Day,
                        CourseId = h.CourseId,
                        Rank = h.Rank,
                        TimeMilliseconds = h.TimeMilliseconds,
                        Retired = h.Retired,
                        Lost = h.Lost,
                    }).ToList(),
                },
            };
        }

        private static CharacterState FromDto(CharacterDto d)
        {
            var c = new CharacterState(
                d.Id,
                d.Name,
                d.SpeciesId,
                ToStats(d.Stats),
                ToFurnace(d.Furnace),
                ToAffinities(d.Affinities),
                ParseEnum(d.Personality, Personality.Normal),
                d.MaxDurability,
                d.BornDay,
                d.LineageId,
                d.Generation)
            {
                Condition = ParseEnum(d.Condition, Condition.Normal),
                MutationId = d.MutationId,
            };

            if (d.Growth?.PhaseDays != null && d.Growth.PhaseDays.Length == 4)
            {
                var p = d.Growth.PhaseDays;
                c.Growth = new GrowthProfile(ParseEnum(d.Growth.Type, GrowthType.Normal), p[0], p[1], p[2], p[3]);
            }

            var history = (d.Record?.History ?? new List<RaceHistoryDto>())
                .Select(h => new RaceHistoryEntry(h.Day, h.CourseId, h.Rank, h.TimeMilliseconds, h.Retired, h.Lost))
                .ToList();
            // 最長の炉稼働秒は最初の1件に載せて復元する（戦績の他の集計は履歴から再計算される）
            var furnaceSeconds = history.Select((_, i) => i == 0 ? d.Record?.MaxFurnaceSeconds ?? 0 : 0);
            c.RestoreRuntime(d.Fatigue, d.Durability, d.MaxDurability, ParseEnum(d.Status, CharacterStatus.Active), d.EndedDay, d.EndReason,
                CharacterRecord.Restore(history, furnaceSeconds));
            c.RestoreTraining(d.BodyAccumulation, d.BodyTraits, d.Traits, d.LastTrainingId, d.TrainingStreak);
            c.SetFactors((d.Factors ?? new List<FactorDto>()).Select(FromDto));
            return c;
        }

        private static ParentTreeDto ToDto(ParentTree t)
        {
            return new ParentTreeDto
            {
                Id = t.Id,
                Name = t.Name,
                SourceCharacterId = t.SourceCharacterId,
                SpeciesId = t.SpeciesId,
                LineageId = t.LineageId,
                Generation = t.Generation,
                Stats = StatTypes.Select(s => t.Stats[s]).ToArray(),
                Furnace = FurnaceStatTypes.Select(s => t.Furnace[s]).ToArray(),
                Affinities = AffinityTypes.Select(s => t.Affinities[s]).ToArray(),
                GrowthType = t.GrowthType.ToString(),
                Personality = t.Personality.ToString(),
                Factors = t.Factors.Select(ToDto).ToList(),
                RegisteredDay = t.RegisteredDay,
                FromLost = t.FromLost,
            };
        }

        private static ParentTree FromDto(ParentTreeDto d)
        {
            return new ParentTree(
                d.Id,
                d.Name,
                d.SourceCharacterId,
                d.SpeciesId,
                d.LineageId,
                d.Generation,
                ToStats(d.Stats),
                ToFurnace(d.Furnace),
                ToAffinities(d.Affinities),
                ParseEnum(d.GrowthType, GrowthType.Normal),
                ParseEnum(d.Personality, Personality.Normal),
                (d.Factors ?? new List<FactorDto>()).Select(FromDto).ToList(),
                d.RegisteredDay,
                d.FromLost);
        }

        private static FactorDto ToDto(GeneFactor f) => new FactorDto { FactorId = f.FactorId, Level = f.Level, Acquired = f.Acquired };

        private static GeneFactor FromDto(FactorDto f) => new GeneFactor(f.FactorId, f.Level, f.Acquired);

        private static CharacterStats ToStats(int[] v)
        {
            if (v == null || v.Length != StatTypes.Length) throw new FormatException("stats の要素数が不正です。");
            return new CharacterStats(v[0], v[1], v[2], v[3], v[4], v[5]);
        }

        private static FurnaceStats ToFurnace(int[] v)
        {
            if (v == null || v.Length != FurnaceStatTypes.Length) throw new FormatException("furnace の要素数が不正です。");
            return new FurnaceStats(v[0], v[1], v[2], v[3], v[4]);
        }

        private static Affinities ToAffinities(int[] v)
        {
            if (v == null || v.Length != AffinityTypes.Length) throw new FormatException("affinities の要素数が不正です。");
            return new Affinities(v[0], v[1], v[2], v[3], v[4], v[5], v[6]);
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse(value, out T parsed) ? parsed : fallback;
        }
    }
}
