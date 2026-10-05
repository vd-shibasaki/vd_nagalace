using System.Collections.Generic;
using NagaRace.Domain.Characters;

namespace NagaRace.Domain.Lineage
{
    /// <summary>因子の定義（05 §8）。</summary>
    public sealed class FactorDefinition
    {
        public FactorDefinition(string id, string name, CharacterParameter parameter, CharacterParameter? acquiredFrom)
        {
            Id = id;
            Name = name;
            Parameter = parameter;
            AcquiredFrom = acquiredFrom;
        }

        public string Id { get; }
        public string Name { get; }
        /// <summary>子に継承されたとき上がる能力。</summary>
        public CharacterParameter Parameter { get; }
        /// <summary>現役中にこの能力が高ければ後天的に得られる（引退時に登録）。</summary>
        public CharacterParameter? AcquiredFrom { get; }
    }

    /// <summary>突然変異の定義（05 §9）。「レア = 必ず強い」にしないため長所と短所を持つ。</summary>
    public sealed class MutationDefinition
    {
        public MutationDefinition(string id, string name, IReadOnlyDictionary<CharacterParameter, int> modifiers)
        {
            Id = id;
            Name = name;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyDictionary<CharacterParameter, int> Modifiers { get; }
    }

    /// <summary>個体・親木が持つ因子。</summary>
    public sealed class GeneFactor
    {
        public GeneFactor(string factorId, int level, bool acquired)
        {
            FactorId = factorId;
            Level = level < 1 ? 1 : (level > 3 ? 3 : level);
            Acquired = acquired;
        }

        public string FactorId { get; }
        /// <summary>1〜3。</summary>
        public int Level { get; }
        /// <summary>現役中に得た後天因子か（ロストでは登録されない）。</summary>
        public bool Acquired { get; }
    }

    public sealed class LineageCatalog
    {
        private readonly Dictionary<string, FactorDefinition> _factors = new Dictionary<string, FactorDefinition>();
        private readonly Dictionary<string, MutationDefinition> _mutations = new Dictionary<string, MutationDefinition>();

        public LineageCatalog(IReadOnlyList<FactorDefinition> factors, IReadOnlyList<MutationDefinition> mutations)
        {
            Factors = factors;
            Mutations = mutations;
            foreach (var f in factors) _factors[f.Id] = f;
            foreach (var m in mutations) _mutations[m.Id] = m;
        }

        public IReadOnlyList<FactorDefinition> Factors { get; }
        public IReadOnlyList<MutationDefinition> Mutations { get; }

        public FactorDefinition Factor(string id) => _factors.TryGetValue(id, out var f) ? f : null;
        public MutationDefinition Mutation(string id) => id != null && _mutations.TryGetValue(id, out var m) ? m : null;
    }
}
