using System.Collections.Generic;
using NagaRace.Domain.Race;
using UnityEngine;

namespace NagaRace.Presentation.Debug
{
    /// <summary>
    /// デバッグ表示用に、コースグラフを平面へ並べる。
    /// 進捗を Z 軸に、横位置と分岐を X 軸に割り当てる（正式な Spline 川は M2）。
    /// </summary>
    public sealed class CourseLayout
    {
        private readonly Dictionary<FlowSegment, float> _branchOffset = new Dictionary<FlowSegment, float>();

        public CourseLayout(RaceCourse course, float lengthScale, float branchSpacing)
        {
            Course = course;
            LengthScale = lengthScale;

            // 分岐点から出る Segment には左右のずらしを与え、分岐区間内ではそれを引き継ぐ
            foreach (var segment in OrderByProgress(course))
            {
                var siblings = new List<FlowSegment>();
                foreach (var s in course.Segments)
                {
                    if (s.StartNode == segment.StartNode) siblings.Add(s);
                }

                float offset = 0f;
                if (siblings.Count > 1)
                {
                    int index = siblings.IndexOf(segment);
                    offset = (index - (siblings.Count - 1) / 2f) * branchSpacing;
                }
                else if (!course.IsArticulationNode(segment.StartNode))
                {
                    foreach (var s in course.Segments)
                    {
                        if (s.EndNode == segment.StartNode && _branchOffset.TryGetValue(s, out float inherited))
                        {
                            offset = inherited;
                            break;
                        }
                    }
                }

                _branchOffset[segment] = offset;
            }
        }

        public RaceCourse Course { get; }
        public float LengthScale { get; }
        public float TotalLength => (float)Course.Distance * LengthScale;

        public float BranchOffset(FlowSegment segment)
        {
            return _branchOffset.TryGetValue(segment, out float offset) ? offset : 0f;
        }

        public Vector3 PositionOf(RacerState racer, float lateralHalfWidth)
        {
            float x = BranchOffset(racer.Segment) + (float)racer.Lateral * lateralHalfWidth;
            float z = (float)racer.Progress * TotalLength;
            return new Vector3(x, 0.5f, z);
        }

        private static List<FlowSegment> OrderByProgress(RaceCourse course)
        {
            var list = new List<FlowSegment>(course.Segments);
            list.Sort((a, b) => a.ProgressStart.CompareTo(b.ProgressStart));
            return list;
        }
    }
}
