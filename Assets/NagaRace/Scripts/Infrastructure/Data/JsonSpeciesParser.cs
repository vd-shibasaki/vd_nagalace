using System;
using System.Collections.Generic;
using NagaRace.Application.Race;
using NagaRace.Domain.Characters;
using Newtonsoft.Json.Linq;

namespace NagaRace.Infrastructure.Data
{
    /// <summary>species.json → <see cref="SpeciesDefinition"/> の一覧。</summary>
    public static class JsonSpeciesParser
    {
        public static List<SpeciesDefinition> Parse(string json)
        {
            var root = JObject.Parse(json);
            var list = new List<SpeciesDefinition>();
            foreach (var token in root["species"] as JArray ?? throw new FormatException("species がありません。"))
            {
                var s = (JObject)token;
                string id = s.Value<string>("id") ?? throw new FormatException("species.id がありません。");
                list.Add(new SpeciesDefinition(
                    id,
                    s.Value<string>("displayName") ?? id,
                    ParseRanges<StatType>(s["stats"] as JObject, id),
                    ParseRanges<AffinityType>(s["affinities"] as JObject, id),
                    ParseWeights(s["personalityWeights"] as JObject, id)));
            }

            return list;
        }

        private static Dictionary<TEnum, IntRange> ParseRanges<TEnum>(JObject obj, string context) where TEnum : struct
        {
            var result = new Dictionary<TEnum, IntRange>();
            if (obj == null) return result;
            foreach (var property in obj.Properties())
            {
                if (!Enum.TryParse(property.Name, true, out TEnum key))
                {
                    throw new FormatException($"{context}: 未知の項目 '{property.Name}'");
                }

                var array = (JArray)property.Value;
                result[key] = new IntRange(array[0].Value<int>(), array[1].Value<int>());
            }

            return result;
        }

        private static Dictionary<Personality, int> ParseWeights(JObject obj, string context)
        {
            var result = new Dictionary<Personality, int>();
            if (obj == null) return result;
            foreach (var property in obj.Properties())
            {
                if (!Enum.TryParse(property.Name, true, out Personality key))
                {
                    throw new FormatException($"{context}: 未知の性格 '{property.Name}'");
                }

                result[key] = property.Value.Value<int>();
            }

            return result;
        }
    }

    public sealed class JsonSpeciesRepository : ISpeciesRepository
    {
        private readonly List<SpeciesDefinition> _species;

        public JsonSpeciesRepository(string json)
        {
            _species = JsonSpeciesParser.Parse(json);
        }

        public IReadOnlyList<SpeciesDefinition> GetAll()
        {
            return _species;
        }
    }
}
