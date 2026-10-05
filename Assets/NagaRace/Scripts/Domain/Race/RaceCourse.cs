using System;
using System.Collections.Generic;
using System.Linq;

namespace NagaRace.Domain.Race
{
    /// <summary>
    /// Flow Graph で表したコース（04 §2）。<see cref="Build"/> で検証と進捗計算を行う。
    ///
    /// グラフの考え方:
    /// - Segment を「辺」、分岐・合流点を「ノード」とみなす
    /// - 同じ次Segmentを持つ Segment 同士は、次Segmentの集合が完全に一致しなければならない（合流点を一意にするため）
    /// - 全経路が必ず通るノード（関節点）の進捗は「スタートからの最短距離 / 最短全長」
    /// - 分岐区間の内部は、前後の関節点の進捗を経路内の距離比で線形補間する
    /// </summary>
    public sealed class RaceCourse
    {
        private static readonly SegmentTags ShowPieceTags =
            SegmentTags.Rapid | SegmentTags.Waterfall | SegmentTags.Whirlpool | SegmentTags.Narrow;

        private readonly Dictionary<string, FlowSegment> _segmentById;
        private readonly bool[] _isArticulationNode;

        private RaceCourse(
            string id,
            string name,
            IReadOnlyList<FlowSegment> segments,
            Dictionary<string, FlowSegment> segmentById,
            FlowSegment start,
            FlowSegment goal,
            double distance,
            bool[] isArticulationNode,
            double[] targetDurationSeconds)
        {
            Id = id;
            Name = name;
            Segments = segments;
            _segmentById = segmentById;
            StartSegment = start;
            GoalSegment = goal;
            Distance = distance;
            _isArticulationNode = isArticulationNode;
            TargetDurationSeconds = targetDurationSeconds;
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<FlowSegment> Segments { get; }
        public FlowSegment StartSegment { get; }
        public FlowSegment GoalSegment { get; }

        /// <summary>最短経路の全長（m）。距離適性の判定に使う。</summary>
        public double Distance { get; }

        /// <summary>大会側で指定がなければ null（バランス設定の既定値を使う）。</summary>
        public double[] TargetDurationSeconds { get; }

        public int NodeCount => _isArticulationNode.Length;

        public FlowSegment GetSegment(string id)
        {
            return _segmentById[id];
        }

        /// <summary>全経路が必ず通るノードか（分岐区間の外側）。</summary>
        public bool IsArticulationNode(int node)
        {
            return _isArticulationNode[node];
        }

        public static RaceCourse Build(string id, string name, IReadOnlyList<FlowSegment> segments, double[] targetDurationSeconds = null)
        {
            var errors = new List<string>();
            if (segments == null || segments.Count == 0)
            {
                throw new CourseValidationException(id, new[] { "Segment がありません。" });
            }

            // 1. ID の一意性と値の範囲
            var byId = new Dictionary<string, FlowSegment>();
            foreach (var segment in segments)
            {
                if (byId.ContainsKey(segment.Id))
                {
                    errors.Add($"Segment id '{segment.Id}' が重複しています。");
                    continue;
                }

                byId.Add(segment.Id, segment);
                if (segment.Danger < 1 || segment.Danger > 4) errors.Add($"Segment {segment.Id}: danger は 1〜4 で指定してください。");
                if (segment.WaterDepth < 0 || segment.WaterDepth > 1) errors.Add($"Segment {segment.Id}: waterDepth は 0〜1 で指定してください。");
                if (segment.Roughness < 0 || segment.Roughness > 1) errors.Add($"Segment {segment.Id}: roughness は 0〜1 で指定してください。");
            }

            ThrowIfAny(id, errors);

            // 2. 参照解決
            var predecessors = new Dictionary<string, List<FlowSegment>>();
            foreach (var segment in segments)
            {
                predecessors[segment.Id] = new List<FlowSegment>();
            }

            foreach (var segment in segments)
            {
                var next = new List<FlowSegment>();
                foreach (var nextId in segment.NextSegmentIds)
                {
                    if (nextId == segment.Id)
                    {
                        errors.Add($"Segment {segment.Id} が自分自身に接続しています。");
                    }
                    else if (!byId.TryGetValue(nextId, out var target))
                    {
                        errors.Add($"Segment {segment.Id} の次 '{nextId}' が存在しません。");
                    }
                    else if (!next.Contains(target))
                    {
                        next.Add(target);
                        predecessors[nextId].Add(segment);
                    }
                }

                segment.Next = next;
            }

            ThrowIfAny(id, errors);

            // 3. スタート・ゴールはそれぞれ1つ
            var starts = segments.Where(s => predecessors[s.Id].Count == 0).ToList();
            var goals = segments.Where(s => s.Next.Count == 0).ToList();
            if (starts.Count != 1) errors.Add($"スタートSegment（前がないSegment）は1つにしてください（現在 {starts.Count}）。");
            if (goals.Count != 1) errors.Add($"ゴールSegment（次がないSegment）は1つにしてください（現在 {goals.Count}）。");

            // 4. 合流点の一意性
            foreach (var segment in segments)
            {
                var preds = predecessors[segment.Id];
                for (int i = 1; i < preds.Count; i++)
                {
                    if (!SameNextSet(preds[0], preds[i]))
                    {
                        errors.Add($"Segment {preds[0].Id} と {preds[i].Id} は合流先 {segment.Id} を共有していますが、次Segmentの集合が一致しません。");
                    }
                }
            }

            ThrowIfAny(id, errors);
            var startSegment = starts[0];
            var goalSegment = goals[0];

            // 5. ノード番号の割当（0 = スタート地点）
            var nodeByKey = new Dictionary<string, int>();
            foreach (var segment in segments)
            {
                string key = segment.Next.Count == 0
                    ? "#goal"
                    : string.Join("|", segment.Next.Select(n => n.Id).OrderBy(x => x, StringComparer.Ordinal));
                if (!nodeByKey.TryGetValue(key, out int node))
                {
                    node = nodeByKey.Count + 1;
                    nodeByKey.Add(key, node);
                }

                segment.EndNode = node;
            }

            foreach (var segment in segments)
            {
                segment.StartNode = segment == startSegment ? 0 : predecessors[segment.Id][0].EndNode;
            }

            int nodeCount = nodeByKey.Count + 1;
            int goalNode = goalSegment.EndNode;

            // 6. トポロジカル順（循環の検出を兼ねる）
            var outgoing = new List<FlowSegment>[nodeCount];
            var incomingCount = new int[nodeCount];
            for (int n = 0; n < nodeCount; n++)
            {
                outgoing[n] = new List<FlowSegment>();
            }

            foreach (var segment in segments)
            {
                outgoing[segment.StartNode].Add(segment);
                incomingCount[segment.EndNode]++;
            }

            var order = TopologicalOrder(nodeCount, outgoing, incomingCount);
            if (order == null)
            {
                throw new CourseValidationException(id, new[] { "コースが循環しています（ループは禁止）。" });
            }

            // 7. 見せ場の有無（02 §14）
            bool hasShowPiece = segments.Any(s => s.IsBranchPoint || (s.Tags & ShowPieceTags) != 0);
            if (!hasShowPiece)
            {
                throw new CourseValidationException(id, new[] { "見せ場（分岐、または rapid / waterfall / whirlpool / narrow）がありません。" });
            }

            // 8. 最短距離と経路数
            var fromStart = Fill(nodeCount, double.PositiveInfinity);
            var toGoal = Fill(nodeCount, double.PositiveInfinity);
            var pathsIn = new double[nodeCount];
            var pathsOut = new double[nodeCount];
            fromStart[0] = 0;
            pathsIn[0] = 1;
            foreach (int n in order)
            {
                foreach (var s in outgoing[n])
                {
                    fromStart[s.EndNode] = Math.Min(fromStart[s.EndNode], fromStart[n] + s.Length);
                    pathsIn[s.EndNode] += pathsIn[n];
                }
            }

            toGoal[goalNode] = 0;
            pathsOut[goalNode] = 1;
            for (int i = order.Count - 1; i >= 0; i--)
            {
                int n = order[i];
                foreach (var s in outgoing[n])
                {
                    toGoal[n] = Math.Min(toGoal[n], s.Length + toGoal[s.EndNode]);
                    pathsOut[n] += pathsOut[s.EndNode];
                }
            }

            double totalPaths = pathsOut[0];
            double distance = fromStart[goalNode];

            // 9. 関節点の進捗 → 分岐内部の補間
            var isArticulation = new bool[nodeCount];
            var progress = new double[nodeCount];
            var topoIndex = new int[nodeCount];
            for (int i = 0; i < order.Count; i++)
            {
                topoIndex[order[i]] = i;
            }

            foreach (int n in order)
            {
                isArticulation[n] = Math.Abs(pathsIn[n] * pathsOut[n] - totalPaths) < 0.5;
                if (isArticulation[n])
                {
                    progress[n] = fromStart[n] / distance;
                }
            }

            foreach (int n in order)
            {
                if (isArticulation[n]) continue;

                // 関節点は全経路上に順番に並ぶため、トポロジカル順で直前・直後の関節点が分岐区間の両端になる
                int prev = -1;
                int next = -1;
                foreach (int a in order)
                {
                    if (!isArticulation[a]) continue;
                    if (topoIndex[a] < topoIndex[n]) prev = a;
                    else if (next < 0) next = a;
                }

                double fromPrev = fromStart[n] - fromStart[prev];
                double toNext = toGoal[n] - toGoal[next];
                double ratio = fromPrev / (fromPrev + toNext);
                progress[n] = progress[prev] + (progress[next] - progress[prev]) * ratio;
            }

            progress[0] = 0;
            progress[goalNode] = 1;
            foreach (var segment in segments)
            {
                segment.ProgressStart = progress[segment.StartNode];
                segment.ProgressEnd = progress[segment.EndNode];
            }

            return new RaceCourse(id, name, segments, byId, startSegment, goalSegment, distance, isArticulation, targetDurationSeconds);
        }

        private static bool SameNextSet(FlowSegment a, FlowSegment b)
        {
            if (a.Next.Count != b.Next.Count) return false;
            foreach (var n in a.Next)
            {
                bool found = false;
                foreach (var m in b.Next)
                {
                    if (ReferenceEquals(n, m))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found) return false;
            }

            return true;
        }

        private static List<int> TopologicalOrder(int nodeCount, List<FlowSegment>[] outgoing, int[] incomingCount)
        {
            var inDegree = (int[])incomingCount.Clone();
            var queue = new Queue<int>();
            for (int n = 0; n < nodeCount; n++)
            {
                if (inDegree[n] == 0) queue.Enqueue(n);
            }

            var order = new List<int>(nodeCount);
            while (queue.Count > 0)
            {
                int n = queue.Dequeue();
                order.Add(n);
                foreach (var s in outgoing[n])
                {
                    if (--inDegree[s.EndNode] == 0) queue.Enqueue(s.EndNode);
                }
            }

            return order.Count == nodeCount ? order : null;
        }

        private static double[] Fill(int count, double value)
        {
            var array = new double[count];
            for (int i = 0; i < count; i++) array[i] = value;
            return array;
        }

        private static void ThrowIfAny(string id, List<string> errors)
        {
            if (errors.Count > 0) throw new CourseValidationException(id, errors);
        }
    }
}
