using NagaRace.Domain.Common;

namespace NagaRace.Domain.Race
{
    /// <summary>
    /// レース中の1出走者の状態。RaceSimulator だけが更新し、View は読み取りのみ行う。
    /// </summary>
    public sealed class RacerState
    {
        internal RacerState(int index, RaceEntrant entrant, ulong raceSeed)
        {
            Index = index;
            Entrant = entrant;
            RouteRandom = RandomStreams.Create(raceSeed, index, RandomStreamId.Route);
            MovementRandom = RandomStreams.Create(raceSeed, index, RandomStreamId.Movement);
            ContactRandom = RandomStreams.Create(raceSeed, index, RandomStreamId.Contact);
        }

        /// <summary>出走リスト上の番号。枠番を兼ねる。</summary>
        public int Index { get; }
        public RaceEntrant Entrant { get; }

        public FlowSegment Segment { get; internal set; }
        /// <summary>Segment 内の距離（m）。</summary>
        public double Offset { get; internal set; }
        /// <summary>分岐でこの次に進む予定の Segment。ゴールSegmentでは null。</summary>
        public FlowSegment PlannedNext { get; internal set; }

        /// <summary>横位置 -1〜+1（0 = 川の中央）。</summary>
        public double Lateral { get; internal set; }
        public double TargetLateral { get; internal set; }
        /// <summary>枠番で決まる自分のレーン。詰まりを避けた後はここへ戻る。</summary>
        public double HomeLateral { get; internal set; }

        public double CurrentSpeed { get; internal set; }
        public double TargetSpeed { get; internal set; }

        /// <summary>コース全体の進捗 0〜1。</summary>
        public double Progress { get; internal set; }
        /// <summary>現在の順位（1始まり）。</summary>
        public int Rank { get; internal set; }

        public bool IsFinished { get; internal set; }
        public double FinishTimeSeconds { get; internal set; }

        public double StunRemaining { get; internal set; }
        public bool IsBlocked { get; internal set; }

        internal double BlockedSeconds { get; set; }
        internal double SinceLaneChangeSeconds { get; set; }
        internal double SegmentJitter { get; set; } = 1.0;
        /// <summary>出走中に変化しない速度倍率（流速・調子・疲労・距離適性）。</summary>
        internal double StaticSpeedFactor { get; set; } = 1.0;
        internal bool NeedsRoutePlan { get; set; }

        internal IRandomProvider RouteRandom { get; }
        internal IRandomProvider MovementRandom { get; }
        internal IRandomProvider ContactRandom { get; }
    }
}
