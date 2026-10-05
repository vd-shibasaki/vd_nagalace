using System;
using System.Collections.Generic;
using NagaRace.Domain.Race;
using UnityEngine;

namespace NagaRace.Presentation.Race
{
    /// <summary>
    /// Flow Graph（論理コース）を 3D の川へ配置する見た目専用のジオメトリ（04 §2「Flow Graph + Spline」の Spline 側）。
    ///
    /// - 中心線はコース進捗 0〜1 の関数（ゆるい S 字に蛇行しながら少しずつ下る）
    /// - 分岐区間の各経路は、分岐点と合流点で中心線に戻る「膨らみ」で左右へずらす
    /// - 単位はメートル（1 unit = 1 m）
    ///
    /// レースの勝敗には一切影響しない。正式コースのアート差替え時は、このクラスを手置きの Spline に置き換える。
    /// </summary>
    public sealed class RaceCourseGeometry
    {
        [Serializable]
        public sealed class Settings
        {
            [SerializeField] private float _meanderAmplitude = 26f;
            [SerializeField] private float _meanderWavelength = 650f;
            [SerializeField] private float _dropPerMeter;
            [SerializeField] private float _branchSpacing = 22f;
            [SerializeField] private float _widthBlendMeters = 12f;

            public float MeanderAmplitude => _meanderAmplitude;
            public float MeanderWavelength => _meanderWavelength;
            public float DropPerMeter => _dropPerMeter;
            public float BranchSpacing => _branchSpacing;
            public float WidthBlendMeters => _widthBlendMeters;
        }

        private readonly Settings _settings;
        private readonly Dictionary<FlowSegment, float> _branchOffset = new Dictionary<FlowSegment, float>();
        private readonly Dictionary<FlowSegment, Vector2> _section = new Dictionary<FlowSegment, Vector2>();
        private readonly Dictionary<FlowSegment, FlowSegment> _predecessor = new Dictionary<FlowSegment, FlowSegment>();

        public RaceCourseGeometry(RaceCourse course, Settings settings)
        {
            Course = course ?? throw new ArgumentNullException(nameof(course));
            _settings = settings ?? new Settings();
            Length = (float)course.Distance;

            foreach (var segment in course.Segments)
            {
                foreach (var s in course.Segments)
                {
                    if (s.EndNode == segment.StartNode && segment != course.StartSegment)
                    {
                        _predecessor[segment] = s;
                        break;
                    }
                }
            }

            foreach (var segment in course.Segments)
            {
                _section[segment] = FindSection(segment);
                _branchOffset[segment] = FindBranchOffset(segment);
            }
        }

        public RaceCourse Course { get; }

        /// <summary>中心線の Z 方向の長さ（m）。</summary>
        public float Length { get; }

        public Vector3 Center(double progress)
        {
            float z = (float)progress * Length;
            float x = _settings.MeanderAmplitude * Mathf.Sin(2f * Mathf.PI * z / _settings.MeanderWavelength);
            float y = -z * _settings.DropPerMeter;
            return new Vector3(x, y, z);
        }

        public Vector3 Tangent(double progress)
        {
            const double delta = 0.001;
            var a = Center(Math.Max(0, progress - delta));
            var b = Center(Math.Min(1, progress + delta));
            var t = b - a;
            return t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.forward;
        }

        /// <summary>流れに対して右向きの水平ベクトル。</summary>
        public Vector3 Right(double progress)
        {
            var t = Tangent(progress);
            return new Vector3(t.z, 0f, -t.x).normalized;
        }

        /// <summary>分岐区間で経路を左右へずらす量（m）。分岐点・合流点では 0。</summary>
        public float BranchOffset(FlowSegment segment, double progress)
        {
            float offset = _branchOffset[segment];
            if (offset == 0f) return 0f;

            var section = _section[segment];
            float u = Mathf.InverseLerp(section.x, section.y, (float)progress);
            float bump = Mathf.SmoothStep(0f, 1f, u / 0.3f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.7f) / 0.3f));
            return offset * bump;
        }

        /// <summary>Segment 内の位置での川幅（m）。Segment の入口では直前の川幅から補間する。</summary>
        public float WidthAt(FlowSegment segment, double offsetInSegment)
        {
            float width = (float)segment.Width;
            if (_predecessor.TryGetValue(segment, out var previous) && offsetInSegment < _settings.WidthBlendMeters)
            {
                float t = (float)(offsetInSegment / _settings.WidthBlendMeters);
                width = Mathf.Lerp((float)previous.Width, width, Mathf.SmoothStep(0f, 1f, t));
            }

            return width;
        }

        /// <summary>Segment 内の位置と横位置（m）からワールド座標を求める。</summary>
        public Vector3 Point(FlowSegment segment, double offsetInSegment, float lateralMeters)
        {
            double progress = segment.ProgressAt(offsetInSegment);
            return Center(progress) + Right(progress) * (BranchOffset(segment, progress) + lateralMeters);
        }

        /// <summary>
        /// 出走者の正規化横位置（-1〜+1）をメートルへ換算した位置。
        /// ゴール後は見た目だけゴール線の先へ流れていく（全員がゴール線上に積み重ならないように）。
        /// </summary>
        public Vector3 RacerPosition(RacerState racer, float racerWidth, double secondsSinceFinish = 0)
        {
            float halfFree = Mathf.Max(0f, (WidthAt(racer.Segment, racer.Offset) - racerWidth) / 2f);
            var position = Point(racer.Segment, racer.Offset, (float)racer.Lateral * halfFree);
            if (racer.IsFinished && secondsSinceFinish > 0)
            {
                // 減速しながら最大 30m 先まで流れる
                float drift = 30f * (1f - Mathf.Exp(-(float)secondsSinceFinish * 0.5f));
                position += Tangent(1.0) * drift;
            }

            return position;
        }

        /// <summary>分岐を含む区間（前後の関節点の進捗）。分岐外の Segment は自分自身の範囲。</summary>
        private Vector2 FindSection(FlowSegment segment)
        {
            var first = segment;
            while (!Course.IsArticulationNode(first.StartNode) && _predecessor.TryGetValue(first, out var previous))
            {
                first = previous;
            }

            var last = segment;
            while (!Course.IsArticulationNode(last.EndNode) && last.Next.Count > 0)
            {
                last = last.Next[0];
            }

            return new Vector2((float)first.ProgressStart, (float)last.ProgressEnd);
        }

        private float FindBranchOffset(FlowSegment segment)
        {
            // 分岐点から出る兄弟 Segment の並び順で左右に振り分ける。分岐区間内の後続は先頭のずらしを引き継ぐ
            var head = segment;
            while (!Course.IsArticulationNode(head.StartNode) && _predecessor.TryGetValue(head, out var previous))
            {
                head = previous;
            }

            var siblings = new List<FlowSegment>();
            foreach (var s in Course.Segments)
            {
                if (s.StartNode == head.StartNode) siblings.Add(s);
            }

            if (siblings.Count <= 1) return 0f;

            // 太い（主流）方を中央寄りにするため、川幅の広い順に並べる
            siblings.Sort((a, b) => b.Width.CompareTo(a.Width));
            int index = siblings.IndexOf(head);
            float centered = index - (siblings.Count - 1) / 2f;
            return centered * _settings.BranchSpacing;
        }
    }
}
