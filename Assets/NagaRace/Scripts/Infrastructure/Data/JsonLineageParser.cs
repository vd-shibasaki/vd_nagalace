using System;
using System.Collections.Generic;
using NagaRace.Application.Lineage;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Lineage;
using Newtonsoft.Json.Linq;

namespace NagaRace.Infrastructure.Data
{
    /// <summary>factors.json → <see cref="LineageCatalog"/>。</summary>
    public static class JsonLineageParser
    {
        public static LineageCatalog Parse(string json)
        {
            var root = JObject.Parse(json);
            var factors = new List<FactorDefinition>();
            foreach (var token in root["factors"] as JArray ?? throw new FormatException("factors がありません。"))
            {
                var f = (JObject)token;
                string acquired = f.Value<string>("acquiredFrom");
                factors.Add(new FactorDefinition(
                    f.Value<string>("id"),
                    f.Value<string>("name"),
                    CharacterParameter.Parse(f.Value<string>("parameter")),
                    string.IsNullOrEmpty(acquired) ? (CharacterParameter?)null : CharacterParameter.Parse(acquired)));
            }

            var mutations = new List<MutationDefinition>();
            foreach (var token in root["mutations"] as JArray ?? new JArray())
            {
                var m = (JObject)token;
                var modifiers = new Dictionary<CharacterParameter, int>();
                foreach (var p in (m["modifiers"] as JObject ?? new JObject()).Properties())
                {
                    modifiers[CharacterParameter.Parse(p.Name)] = p.Value.Value<int>();
                }

                mutations.Add(new MutationDefinition(m.Value<string>("id"), m.Value<string>("name"), modifiers));
            }

            return new LineageCatalog(factors, mutations);
        }
    }

    public sealed class JsonLineageCatalogRepository : ILineageCatalogRepository
    {
        private readonly string _json;
        private LineageCatalog _cache;

        public JsonLineageCatalogRepository(string json)
        {
            _json = json;
        }

        public LineageCatalog Load()
        {
            return _cache ?? (_cache = JsonLineageParser.Parse(_json));
        }
    }
}
