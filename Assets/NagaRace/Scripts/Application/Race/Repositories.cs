using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;

namespace NagaRace.Application.Race
{
    /// <summary>バランス設定の取得元。実装は Infrastructure（07 §4）。</summary>
    public interface IBalanceRepository
    {
        BalanceConfig Load();
    }

    /// <summary>コース定義の取得元。</summary>
    public interface ICourseRepository
    {
        RaceCourse Get(string courseId);
    }

    /// <summary>系統定義の取得元。</summary>
    public interface ISpeciesRepository
    {
        IReadOnlyList<SpeciesDefinition> GetAll();
    }
}
