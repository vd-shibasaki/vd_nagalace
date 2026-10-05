using System.Collections.Generic;

namespace NagaRace.Infrastructure.Save
{
    // セーブデータの JSON 構造（08 §7）。Domain の型を直接シリアライズしないことで、
    // ドメインの変更とセーブ形式の変更を切り離す（形式を変えるときは schemaVersion を上げて Migration を書く）。

    public sealed class SaveDto
    {
        public WorldDto World { get; set; } = new WorldDto();
        public List<CharacterDto> Characters { get; set; } = new List<CharacterDto>();
        public List<ParentTreeDto> ParentTrees { get; set; } = new List<ParentTreeDto>();
        public SettingsDto Settings { get; set; } = new SettingsDto();
    }

    public sealed class WorldDto
    {
        /// <summary>ulong を安全に保存するため文字列にする。</summary>
        public string Seed { get; set; }
        public int Day { get; set; }
        public int TopGradeWon { get; set; }
    }

    public sealed class SettingsDto
    {
        public bool AutoFurnace { get; set; }
        public string PlayerAiStyle { get; set; }
        public float PlaybackSpeed { get; set; } = 1f;
        public string SelectedCharacterId { get; set; }
        public List<string> TutorialSeen { get; set; } = new List<string>();
    }

    public sealed class CharacterDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SpeciesId { get; set; }
        public string LineageId { get; set; }
        public int Generation { get; set; }
        public int BornDay { get; set; }
        /// <summary>StatType の順。</summary>
        public int[] Stats { get; set; }
        /// <summary>FurnaceStat の順。</summary>
        public int[] Furnace { get; set; }
        /// <summary>AffinityType の順。</summary>
        public int[] Affinities { get; set; }
        public string Personality { get; set; }
        public string Condition { get; set; }
        public int Fatigue { get; set; }
        public int Durability { get; set; }
        public int MaxDurability { get; set; }
        public string Status { get; set; }
        public int? EndedDay { get; set; }
        public string EndReason { get; set; }
        public GrowthDto Growth { get; set; }
        public Dictionary<string, double> BodyAccumulation { get; set; } = new Dictionary<string, double>();
        public List<string> BodyTraits { get; set; } = new List<string>();
        public List<string> Traits { get; set; } = new List<string>();
        public string LastTrainingId { get; set; }
        public int TrainingStreak { get; set; }
        public List<FactorDto> Factors { get; set; } = new List<FactorDto>();
        public string MutationId { get; set; }
        public RecordDto Record { get; set; } = new RecordDto();
    }

    public sealed class GrowthDto
    {
        public string Type { get; set; }
        public int[] PhaseDays { get; set; }
    }

    public sealed class FactorDto
    {
        public string FactorId { get; set; }
        public int Level { get; set; }
        public bool Acquired { get; set; }
    }

    public sealed class RecordDto
    {
        public double MaxFurnaceSeconds { get; set; }
        public List<RaceHistoryDto> History { get; set; } = new List<RaceHistoryDto>();
    }

    public sealed class RaceHistoryDto
    {
        public int Day { get; set; }
        public string CourseId { get; set; }
        public int Rank { get; set; }
        public long TimeMilliseconds { get; set; }
        public bool Retired { get; set; }
        public bool Lost { get; set; }
    }

    public sealed class ParentTreeDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SourceCharacterId { get; set; }
        public string SpeciesId { get; set; }
        public string LineageId { get; set; }
        public int Generation { get; set; }
        public int[] Stats { get; set; }
        public int[] Furnace { get; set; }
        public int[] Affinities { get; set; }
        public string GrowthType { get; set; }
        public string Personality { get; set; }
        public List<FactorDto> Factors { get; set; } = new List<FactorDto>();
        public int RegisteredDay { get; set; }
        public bool FromLost { get; set; }
    }
}
