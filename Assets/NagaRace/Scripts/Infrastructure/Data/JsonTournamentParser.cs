using System;
using System.Collections.Generic;
using NagaRace.Application.Game;
using NagaRace.Domain.Tournament;
using Newtonsoft.Json.Linq;

namespace NagaRace.Infrastructure.Data
{
    /// <summary>tournaments.json → <see cref="TournamentCatalog"/>。</summary>
    public static class JsonTournamentParser
    {
        public static TournamentCatalog Parse(string json)
        {
            var root = JObject.Parse(json);
            var list = new List<TournamentDefinition>();
            foreach (var token in root["tournaments"] as JArray ?? throw new FormatException("tournaments がありません。"))
            {
                var t = (JObject)token;
                list.Add(new TournamentDefinition(
                    t.Value<string>("id"),
                    t.Value<string>("name"),
                    t.Value<int>("grade"),
                    t.Value<string>("courseId"),
                    t.Value<int?>("npcStatOffset") ?? 0));
            }

            var schedule = root["schedule"] as JObject ?? throw new FormatException("schedule がありません。");
            var weekly = new List<IReadOnlyList<string>>();
            foreach (var week in schedule["weekly"] as JArray ?? new JArray())
            {
                var ids = new List<string>();
                foreach (var id in (JArray)week) ids.Add(id.Value<string>());
                weekly.Add(ids);
            }

            var catalog = new TournamentCatalog(list, schedule.Value<int>("raceDayOfWeek"), weekly);
            foreach (var week in weekly)
            {
                foreach (var id in week) catalog.Get(id); // 未定義の大会を読込時に検出する
            }

            return catalog;
        }
    }

    public sealed class JsonTournamentCatalogRepository : ITournamentCatalogRepository
    {
        private readonly string _json;
        private TournamentCatalog _cache;

        public JsonTournamentCatalogRepository(string json)
        {
            _json = json;
        }

        public TournamentCatalog Load() => _cache ?? (_cache = JsonTournamentParser.Parse(_json));
    }
}
