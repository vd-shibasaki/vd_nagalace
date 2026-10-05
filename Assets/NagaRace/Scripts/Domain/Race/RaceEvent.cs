using System;

namespace NagaRace.Domain.Race
{
    public enum RaceEventType
    {
        /// <summary>Segment に入った。SegmentId = 入ったSegment。</summary>
        EnterSegment,
        /// <summary>分岐でルートを選んだ。SegmentId = 選んだSegment、Value = 候補数。</summary>
        BranchChoice,
        /// <summary>前の出走者に接触した。OtherIndex = 相手、Value = 減速前後の速度差。</summary>
        Contact,
        /// <summary>荒れた水面で姿勢を崩した。Value = 減速前の速度。</summary>
        Stumble,
        /// <summary>良い流れに乗った（Segment のゆらぎが大きくプラス）。Value = 速度倍率。</summary>
        GoodFlow,
        /// <summary>詰まりを避けて横位置を変えた。Value = 新しい目標横位置。</summary>
        LaneChange,
        /// <summary>葉脈炉の始動入力を受け付けた（起動ラグ開始）。</summary>
        FurnaceIgnite,
        /// <summary>葉力不足・冷却中・故障中で始動できなかった。</summary>
        FurnaceRejected,
        /// <summary>葉脈炉が稼働した。</summary>
        FurnaceStart,
        /// <summary>SafeDuration を超えて RiskZone に入った（OVER FLOW）。</summary>
        FurnaceOverflow,
        /// <summary>解除した（葉力切れを含む）。Value = 稼働秒数。</summary>
        FurnaceRelease,
        /// <summary>故障した。Value = (int)FurnaceFailure。</summary>
        FurnaceFailureOccurred,
        /// <summary>葉力チャンスの受付開始。</summary>
        LeafChanceOpen,
        /// <summary>葉力チャンスで獲得した。Value = 獲得量。</summary>
        LeafChanceHit,
        /// <summary>葉力チャンスを逃した。</summary>
        LeafChanceMiss,
        /// <summary>耐久ダメージ。OtherIndex = (int)DamageCause、Value = ダメージ量。</summary>
        DurabilityDamaged,
        /// <summary>危険区間での重大事故。Value = 区間の危険度。</summary>
        CourseAccident,
        /// <summary>リタイア。Value = 進捗。</summary>
        Retire,
        /// <summary>ロスト（02 §11）。OtherIndex = (int)DamageCause。</summary>
        Lost,
        /// <summary>妨害スキルを使った。OtherIndex = 対象（渦は -1）、Value = (int)ObstructionSkill。</summary>
        SkillUsed,
        /// <summary>ゴールした。Value = ゴールタイム（秒）。</summary>
        Finish,
        /// <summary>制限時間でレースを打ち切った。</summary>
        Timeout,
    }

    /// <summary>
    /// レース中の出来事。時刻は Tick 番号で持つ（04 §1.1）。
    /// View はこれを演出（通知・SE・カメラ）へ変換する。
    /// </summary>
    public readonly struct RaceEvent : IEquatable<RaceEvent>
    {
        public RaceEvent(int tick, RaceEventType type, int entrantIndex, string segmentId = null, int otherIndex = -1, double value = 0)
        {
            Tick = tick;
            Type = type;
            EntrantIndex = entrantIndex;
            SegmentId = segmentId;
            OtherIndex = otherIndex;
            Value = value;
        }

        public int Tick { get; }
        public RaceEventType Type { get; }
        public int EntrantIndex { get; }
        public string SegmentId { get; }
        public int OtherIndex { get; }
        public double Value { get; }

        public bool Equals(RaceEvent other)
        {
            return Tick == other.Tick
                && Type == other.Type
                && EntrantIndex == other.EntrantIndex
                && SegmentId == other.SegmentId
                && OtherIndex == other.OtherIndex
                && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is RaceEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Tick;
                hash = hash * 31 + (int)Type;
                hash = hash * 31 + EntrantIndex;
                hash = hash * 31 + (SegmentId?.GetHashCode() ?? 0);
                hash = hash * 31 + OtherIndex;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"[{Tick}] {Type} #{EntrantIndex} {SegmentId} {OtherIndex} {Value:0.###}";
        }
    }
}
