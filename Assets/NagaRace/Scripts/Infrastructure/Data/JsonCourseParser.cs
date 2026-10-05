using System;
using System.Collections.Generic;
using NagaRace.Application.Race;
using NagaRace.Domain.Race;
using Newtonsoft.Json.Linq;

namespace NagaRace.Infrastructure.Data
{
    /// <summary>コース JSON → <see cref="RaceCourse"/>（検証・進捗計算込み）。</summary>
    public static class JsonCourseParser
    {
        private static readonly Dictionary<string, SegmentTags> TagNames = new Dictionary<string, SegmentTags>(StringComparer.OrdinalIgnoreCase)
        {
            { "calm", SegmentTags.Calm },
            { "rapid", SegmentTags.Rapid },
            { "whirlpool", SegmentTags.Whirlpool },
            { "narrow", SegmentTags.Narrow },
            { "waterfall", SegmentTags.Waterfall },
            { "mud", SegmentTags.Mud },
            { "shortcut", SegmentTags.Shortcut },
            { "collision_heavy", SegmentTags.CollisionHeavy },
        };

        public static RaceCourse Parse(string json)
        {
            var root = JObject.Parse(json);
            string id = Required<string>(root, "id", "course");
            string name = root.Value<string>("name") ?? id;
            var segmentsToken = root["segments"] as JArray ?? throw new FormatException($"コース {id}: segments がありません。");

            var segments = new List<FlowSegment>();
            foreach (var token in segmentsToken)
            {
                var s = (JObject)token;
                string segmentId = Required<string>(s, "id", id);
                var next = new List<string>();
                foreach (var n in s["next"] as JArray ?? new JArray())
                {
                    next.Add(n.Value<string>());
                }

                segments.Add(new FlowSegment(
                    segmentId,
                    Required<double>(s, "length", segmentId),
                    Required<double>(s, "baseFlowSpeed", segmentId),
                    s.Value<double?>("waterDepth") ?? 0.4,
                    s.Value<double?>("roughness") ?? 0.0,
                    s.Value<int?>("danger") ?? 1,
                    s.Value<double?>("width") ?? 12.0,
                    ParseTags(s["tags"] as JArray, segmentId),
                    next));
            }

            double[] target = null;
            if (root["targetDurationSeconds"] is JArray targetArray && targetArray.Count == 2)
            {
                target = new[] { targetArray[0].Value<double>(), targetArray[1].Value<double>() };
            }

            return RaceCourse.Build(id, name, segments, target);
        }

        private static SegmentTags ParseTags(JArray tags, string segmentId)
        {
            var result = SegmentTags.None;
            if (tags == null) return result;
            foreach (var t in tags)
            {
                string tagName = t.Value<string>();
                if (!TagNames.TryGetValue(tagName, out var tag))
                {
                    throw new FormatException($"Segment {segmentId}: 未知のタグ '{tagName}'");
                }

                result |= tag;
            }

            return result;
        }

        private static T Required<T>(JObject obj, string key, string context)
        {
            var token = obj[key];
            if (token == null || token.Type == JTokenType.Null)
            {
                throw new FormatException($"{context}: '{key}' がありません。");
            }

            return token.Value<T>();
        }
    }

    /// <summary>JSON 文字列群から読み込むコースリポジトリ。</summary>
    public sealed class JsonCourseRepository : ICourseRepository
    {
        private readonly Dictionary<string, RaceCourse> _courses = new Dictionary<string, RaceCourse>();

        public JsonCourseRepository(IEnumerable<string> jsons)
        {
            foreach (var json in jsons)
            {
                var course = JsonCourseParser.Parse(json);
                _courses[course.Id] = course;
            }
        }

        public IEnumerable<string> Ids => _courses.Keys;

        public RaceCourse Get(string courseId)
        {
            if (!_courses.TryGetValue(courseId, out var course))
            {
                throw new KeyNotFoundException($"コース '{courseId}' がありません。");
            }

            return course;
        }
    }
}
