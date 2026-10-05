using System;
using System.Collections.Generic;

namespace NagaRace.Domain.Race
{
    /// <summary>Segment のタグ（04 §2）。</summary>
    [Flags]
    public enum SegmentTags
    {
        None = 0,
        Calm = 1 << 0,
        Rapid = 1 << 1,
        Whirlpool = 1 << 2,
        Narrow = 1 << 3,
        Waterfall = 1 << 4,
        Mud = 1 << 5,
        Shortcut = 1 << 6,
        CollisionHeavy = 1 << 7,
    }

    /// <summary>
    /// 川の1区間（04 §2）。距離は m、速度は m/s。
    /// progressStart / progressEnd は RaceCourse の構築時に自動計算される。
    /// </summary>
    public sealed class FlowSegment
    {
        public FlowSegment(
            string id,
            double length,
            double baseFlowSpeed,
            double waterDepth,
            double roughness,
            int danger,
            double width,
            SegmentTags tags,
            IReadOnlyList<string> nextSegmentIds)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Segment id が空です。");
            if (length <= 0) throw new ArgumentException($"Segment {id}: length は正の値にしてください。");
            if (baseFlowSpeed <= 0) throw new ArgumentException($"Segment {id}: baseFlowSpeed は正の値にしてください。");
            if (width <= 0) throw new ArgumentException($"Segment {id}: width は正の値にしてください。");

            Id = id;
            Length = length;
            BaseFlowSpeed = baseFlowSpeed;
            WaterDepth = waterDepth;
            Roughness = roughness;
            Danger = danger;
            Width = width;
            Tags = tags;
            NextSegmentIds = nextSegmentIds ?? Array.Empty<string>();
        }

        public string Id { get; }
        public double Length { get; }
        public double BaseFlowSpeed { get; }
        /// <summary>0〜1。</summary>
        public double WaterDepth { get; }
        /// <summary>0〜1。</summary>
        public double Roughness { get; }
        /// <summary>1〜4（安全/注意/危険/極危険）。</summary>
        public int Danger { get; }
        public double Width { get; }
        public SegmentTags Tags { get; }
        public IReadOnlyList<string> NextSegmentIds { get; }

        /// <summary>構築時に解決される次Segment。</summary>
        public IReadOnlyList<FlowSegment> Next { get; internal set; } = Array.Empty<FlowSegment>();

        public double ProgressStart { get; internal set; }
        public double ProgressEnd { get; internal set; }

        /// <summary>この Segment の開始点（分岐・合流点）のノード番号。</summary>
        public int StartNode { get; internal set; }
        public int EndNode { get; internal set; }

        public bool IsBranchPoint => Next.Count > 1;
        public bool IsGoal => Next.Count == 0;

        public bool HasTag(SegmentTags tag)
        {
            return (Tags & tag) != 0;
        }

        /// <summary>Segment 内のオフセット（m）からコース全体の進捗 0〜1 を返す。</summary>
        public double ProgressAt(double offset)
        {
            double t = offset / Length;
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return ProgressStart + (ProgressEnd - ProgressStart) * t;
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
