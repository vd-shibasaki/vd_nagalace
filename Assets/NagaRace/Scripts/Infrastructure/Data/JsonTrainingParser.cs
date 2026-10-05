using System;
using System.Collections.Generic;
using NagaRace.Application.Training;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;
using NagaRace.Domain.Training;
using Newtonsoft.Json.Linq;

namespace NagaRace.Infrastructure.Data
{
    /// <summary>trainings.json → <see cref="TrainingCatalog"/>。</summary>
    public static class JsonTrainingParser
    {
        public static TrainingCatalog Parse(string json)
        {
            var root = JObject.Parse(json);
            var trainings = new List<TrainingDefinition>();
            foreach (var token in root["trainings"] as JArray ?? throw new FormatException("trainings がありません。"))
            {
                var t = (JObject)token;
                string id = t.Value<string>("id") ?? throw new FormatException("training.id がありません。");

                var growth = new Dictionary<CharacterParameter, double>();
                foreach (var p in (t["growth"] as JObject ?? new JObject()).Properties())
                {
                    growth[CharacterParameter.Parse(p.Name)] = p.Value.Value<double>();
                }

                var weather = new Dictionary<Weather, double>();
                foreach (var p in (t["weather"] as JObject ?? new JObject()).Properties())
                {
                    if (!Enum.TryParse(p.Name, out Weather w)) throw new FormatException($"{id}: 未知の天候 '{p.Name}'");
                    weather[w] = p.Value.Value<double>();
                }

                var body = new Dictionary<string, double>();
                foreach (var p in (t["body"] as JObject ?? new JObject()).Properties())
                {
                    body[p.Name] = p.Value.Value<double>();
                }

                Personality? shiftTo = null;
                double shiftChance = 0;
                if (t["personalityShift"] is JObject shift)
                {
                    if (!Enum.TryParse(shift.Value<string>("to"), out Personality to)) throw new FormatException($"{id}: 未知の性格");
                    shiftTo = to;
                    shiftChance = shift.Value<double>("chance");
                }

                trainings.Add(new TrainingDefinition(
                    id,
                    t.Value<string>("name") ?? id,
                    t.Value<int?>("fatigue") ?? 0,
                    t.Value<int?>("risk") ?? 0,
                    growth,
                    weather,
                    body,
                    t.Value<int?>("durabilityRecovery") ?? 0,
                    t.Value<double?>("conditionUp") ?? 0,
                    t.Value<bool?>("extreme") ?? false,
                    shiftTo,
                    shiftChance));
            }

            var bodyTraits = new List<BodyTraitDefinition>();
            foreach (var token in root["bodyTraits"] as JArray ?? new JArray())
            {
                var b = (JObject)token;
                var modifiers = new Dictionary<CharacterParameter, int>();
                foreach (var p in (b["modifiers"] as JObject ?? new JObject()).Properties())
                {
                    modifiers[CharacterParameter.Parse(p.Name)] = p.Value.Value<int>();
                }

                bodyTraits.Add(new BodyTraitDefinition(b.Value<string>("id"), b.Value<string>("name"), b.Value<string>("accumulation"), modifiers));
            }

            return new TrainingCatalog(trainings, bodyTraits);
        }
    }

    public sealed class JsonTrainingCatalogRepository : ITrainingCatalogRepository
    {
        private readonly string _json;
        private TrainingCatalog _cache;

        public JsonTrainingCatalogRepository(string json)
        {
            _json = json;
        }

        public TrainingCatalog Load()
        {
            return _cache ?? (_cache = JsonTrainingParser.Parse(_json));
        }
    }
}
