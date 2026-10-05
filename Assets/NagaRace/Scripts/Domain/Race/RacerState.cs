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
            FurnaceRandom = RandomStreams.Create(raceSeed, index, RandomStreamId.Furnace);
            EventRandom = RandomStreams.Create(raceSeed, index, RandomStreamId.Event);
            AutoControl = !entrant.IsPlayer || entrant.AutoFurnace;
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

        /// <summary>耐久0・重大事故でリタイアした（Lost を含む）。</summary>
        public bool IsRetired { get; internal set; }
        /// <summary>Lost 判定でロストした（02 §11）。</summary>
        public bool IsLost { get; internal set; }
        internal int RetiredAtTick { get; set; }

        /// <summary>レースから外れた（ゴール済み or リタイア）。</summary>
        public bool IsOut => IsFinished || IsRetired;

        // ---- 耐久（04 §6, §9） ----

        /// <summary>このレースで受けた耐久ダメージ。</summary>
        public double DurabilityDamage { get; internal set; }
        /// <summary>現在の耐久（出走時の耐久 − ダメージ）。</summary>
        public double CurrentDurability => System.Math.Max(0.0, Entrant.Durability - DurabilityDamage);
        public bool HadAccident { get; internal set; }

        // ---- 炉の使用記録（レース後の疲労・アルバムに使う） ----

        public double FurnaceSecondsTotal { get; internal set; }
        public double OverflowSecondsTotal { get; internal set; }
        public double MaxFurnaceSeconds { get; internal set; }
        /// <summary>炉耐久が減る故障（FurnaceDamage 以上）があった。</summary>
        public bool FurnaceDamaged { get; internal set; }
        public FurnaceFailure WorstFailure { get; internal set; }
        public double FinishTimeSeconds { get; internal set; }

        public double StunRemaining { get; internal set; }
        public bool IsBlocked { get; internal set; }

        internal double BlockedSeconds { get; set; }
        internal double SinceLaneChangeSeconds { get; set; }
        internal double SegmentJitter { get; set; } = 1.0;
        /// <summary>出走中に変化しない速度倍率（流速・調子・疲労・距離適性）。</summary>
        internal double StaticSpeedFactor { get; set; } = 1.0;
        internal bool NeedsRoutePlan { get; set; }

        // ---- 葉力・葉脈炉（04 §7〜8） ----

        /// <summary>葉力 0〜100。</summary>
        public double LeafPower { get; internal set; }
        public FurnacePhase FurnacePhase { get; internal set; } = FurnacePhase.Ready;
        /// <summary>稼働時間（Ignition 後から）。</summary>
        public double FurnaceActiveSeconds { get; internal set; }
        /// <summary>この個体の SafeDuration（秒）。</summary>
        public double SafeDurationSeconds { get; internal set; }
        public double FurnaceCooldownRemaining { get; internal set; }
        /// <summary>最後に起きた故障。</summary>
        public FurnaceFailure LastFailure { get; internal set; }
        /// <summary>AI が炉・葉力チャンスを操作するか（NPC と「おまかせ炉」）。プレイヤーが炉を操作すると false になる。</summary>
        public bool AutoControl { get; internal set; }

        /// <summary>葉力チャンスの受付中か。</summary>
        public bool IsChanceOpen { get; internal set; }
        /// <summary>受付の残り秒数（UI 用）。</summary>
        public double ChanceRemainingSeconds { get; internal set; }

        public bool IsFurnaceActive => FurnacePhase == FurnacePhase.ActiveSafe || FurnacePhase == FurnacePhase.ActiveRisk;

        /// <summary>臨界（SafeDuration）までの残り秒数。RiskZone 中は負。</summary>
        public double SecondsToCritical => SafeDurationSeconds - FurnaceActiveSeconds;

        internal double FurnacePhaseSeconds { get; set; }
        internal int NextChanceIndex { get; set; }
        internal double ChanceOpenedAt { get; set; }
        internal bool ChanceUsed { get; set; }

        internal IRandomProvider FurnaceRandom { get; }
        internal IRandomProvider EventRandom { get; }
        internal IRandomProvider RouteRandom { get; }
        internal IRandomProvider MovementRandom { get; }
        internal IRandomProvider ContactRandom { get; }
    }
}
