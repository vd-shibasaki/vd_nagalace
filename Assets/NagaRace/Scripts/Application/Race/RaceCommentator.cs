using System.Collections.Generic;
using NagaRace.Domain.Race;

namespace NagaRace.Application.Race
{
    /// <summary>実況の重要度。高いものほど前の実況を早く押しのける。</summary>
    public enum CommentaryPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
    }

    /// <summary>実況の1行。</summary>
    public readonly struct CommentaryLine
    {
        public CommentaryLine(int tick, string text, CommentaryPriority priority, bool aboutPlayer, string key = null)
        {
            Tick = tick;
            Text = text;
            Priority = priority;
            AboutPlayer = aboutPlayer;
            Key = key;
        }

        public int Tick { get; }
        public string Text { get; }
        public CommentaryPriority Priority { get; }
        /// <summary>自分の葉っぱについての実況か（表示で強調する）。</summary>
        public bool AboutPlayer { get; }
        /// <summary>同じ種類の実況を続けて言わないためのキー（null なら制限なし）。</summary>
        public string Key { get; }

        public override string ToString() => $"[{Tick}] {Priority} {Text}";
    }

    /// <summary>
    /// レースの実況（文字）。RaceSimulator の RaceEvent と順位の変化から、実況の文を作る。
    /// - シミュレーションの乱数は使わない（言い回しの選び分けは Tick から決める）ので、同じレースなら同じ実況になる
    /// - 実況が詰まりすぎないように、最低間隔をあけ、その間に起きた出来事は重要度の高いものだけを残す
    /// - 何も起きない時間が続いたら、順位の状況を伝える
    /// </summary>
    public sealed class RaceCommentator
    {
        /// <summary>次の実況まであける最低秒数。</summary>
        public const double MinGapSeconds = 2.2;
        /// <summary>重要度 Low（状況説明・まわりの出来事）はゆっくり流す。</summary>
        public const double LowPriorityGapSeconds = 3.6;
        /// <summary>同じ種類の実況（同じ葉っぱの同じ出来事）を繰り返さない秒数。</summary>
        public const double RepeatSeconds = 8.0;
        /// <summary>重要度 High はこの秒数で前の実況を押しのけられる。</summary>
        public const double HighPriorityGapSeconds = 0.8;
        /// <summary>待っている実況がこの秒数を超えたら古いので捨てる。</summary>
        public const double PendingLifetimeSeconds = 3.0;
        /// <summary>何も起きない時間がこの秒数続いたら状況を伝える。</summary>
        public const double IdleSeconds = 7.0;
        /// <summary>この差（m）以内なら接戦と言う。</summary>
        public const double CloseGapMeters = 6.0;
        /// <summary>重要な実況をためておく上限（あふれたら古いものから捨てる）。</summary>
        public const int MaxImportantQueue = 3;

        private readonly RaceSimulator _simulator;
        private readonly int _playerIndex;
        private readonly List<CommentaryLine> _lines = new List<CommentaryLine>();
        private readonly Dictionary<string, int> _lastKeyTick = new Dictionary<string, int>();
        private int _processedEvents;
        private int _lastLineTick = int.MinValue;
        private CommentaryLine? _pending;
        private readonly Queue<CommentaryLine> _important = new Queue<CommentaryLine>();
        private int _leaderIndex = -1;
        private int _leaderSinceTick;
        private int _announcedLeader = -1;
        private int _playerRank;
        private int _playerRankSinceTick;
        private int _announcedPlayerRank;
        private int _finishCount;
        private bool _started;
        private bool _halfwayCalled;
        private bool _finalStretchCalled;
        private bool _closeRaceCalled;

        public RaceCommentator(RaceSimulator simulator, int playerIndex)
        {
            _simulator = simulator;
            _playerIndex = playerIndex;
            _playerRank = _announcedPlayerRank = simulator.Racers[playerIndex].Rank;
        }

        /// <summary>これまでに出した実況（古い順）。</summary>
        public IReadOnlyList<CommentaryLine> Lines => _lines;

        private string PlayerName => Name(_playerIndex);

        /// <summary>
        /// シミュレーションを進めたあとに呼ぶ。新しく出た実況の数を返す（Lines の末尾に追加される）。
        /// </summary>
        public int Update()
        {
            int before = _lines.Count;
            int tick = _simulator.Tick;

            if (!_started && tick > 0)
            {
                _started = true;
                Emit(new CommentaryLine(tick, Pick("各葉っぱ、一斉にスタート！", "スタートしました！ 8枚の葉っぱが流れに乗る！"), CommentaryPriority.High, false));
            }

            var events = _simulator.Events;
            for (; _processedEvents < events.Count; _processedEvents++) Consider(FromEvent(events[_processedEvents]));

            if (!_simulator.IsFinished)
            {
                Consider(FromPositions(tick));
                if (_pending == null && _important.Count == 0 && tick - _lastLineTick >= Ticks(IdleSeconds)) Consider(Situation(tick));
            }

            FlushPending(tick);
            return _lines.Count - before;
        }

        private void Consider(CommentaryLine? candidate)
        {
            if (candidate == null) return;
            var line = candidate.Value;
            if (line.Key != null && _lastKeyTick.TryGetValue(line.Key, out int last) && line.Tick - last < Ticks(RepeatSeconds)) return;
            // 重要な出来事（ゴール・炉・事故など）は取りこぼさないよう順番に並べる
            if (line.Priority == CommentaryPriority.High)
            {
                _important.Enqueue(line);
                if (_important.Count > MaxImportantQueue) _important.Dequeue();
                return;
            }

            // それ以外は1つだけ待たせる（待っているものより重要なら入れ替える。同じ重要度なら新しい方）
            if (_pending == null || line.Priority >= _pending.Value.Priority) _pending = line;
        }

        private void FlushPending(int tick)
        {
            // レースが終わったら Tick が進まないので、残っている重要な実況を間隔を待たずに全部出す
            if (_simulator.IsFinished)
            {
                while (_important.Count > 0) Emit(_important.Dequeue());
                _pending = null;
                return;
            }

            bool Waited(double seconds) => _lastLineTick == int.MinValue || tick - _lastLineTick >= Ticks(seconds);
            if (_important.Count > 0)
            {
                if (Waited(HighPriorityGapSeconds)) Emit(_important.Dequeue());
                return;
            }

            if (_pending == null) return;
            var line = _pending.Value;
            if (tick - line.Tick > Ticks(PendingLifetimeSeconds))
            {
                _pending = null;
                return;
            }

            if (!Waited(line.Priority == CommentaryPriority.Low ? LowPriorityGapSeconds : MinGapSeconds)) return;
            _pending = null;
            Emit(line);
        }

        private void Emit(CommentaryLine line)
        {
            _lines.Add(new CommentaryLine(_simulator.Tick, line.Text, line.Priority, line.AboutPlayer, line.Key));
            _lastLineTick = _simulator.Tick;
            if (line.Key != null) _lastKeyTick[line.Key] = _simulator.Tick;
        }

        private CommentaryLine? FromEvent(RaceEvent e)
        {
            bool isPlayer = e.EntrantIndex == _playerIndex;
            string name = e.EntrantIndex >= 0 ? Name(e.EntrantIndex) : string.Empty;
            int t = e.Tick;
            switch (e.Type)
            {
                case RaceEventType.EnterSegment:
                {
                    if (e.Tick == 0) return null;
                    var segment = _simulator.Course.GetSegment(e.SegmentId);
                    bool leader = _simulator.Racers[e.EntrantIndex].Rank == 1;
                    if (segment.IsGoal && leader && !_finalStretchCalled)
                    {
                        _finalStretchCalled = true;
                        return Line(t, Pick($"最終直線！ 先頭は{name}！", $"さあ最終直線！ {name}が先頭で入ってきた！"), CommentaryPriority.High, isPlayer);
                    }

                    if (!IsShowPiece(segment) || !(isPlayer || leader)) return null;
                    string section = SectionName(segment);
                    if (isPlayer) return Line(t, Pick($"{name}、{section}に突入！ ここが勝負どころ！", $"{name}が{section}へ！ うまく抜けられるか！"), CommentaryPriority.Normal, true);
                    return Line(t, $"先頭の{name}、{section}に入った！", CommentaryPriority.Low, false);
                }
                case RaceEventType.BranchChoice:
                    if (!isPlayer) return null;
                    return Line(t, $"{name}は「{SectionName(_simulator.Course.GetSegment(e.SegmentId))}」のルートを選んだ！", CommentaryPriority.Normal, true);
                case RaceEventType.Contact:
                {
                    bool involved = isPlayer || e.OtherIndex == _playerIndex;
                    if (!involved && !IsNotable(e.EntrantIndex)) return null;
                    string other = e.OtherIndex >= 0 ? Name(e.OtherIndex) : "前の葉っぱ";
                    return Line(t, Pick($"{name}と{other}が接触！", $"おっと、{name}が{other}にぶつかった！"), involved ? CommentaryPriority.Normal : CommentaryPriority.Low, involved, Key("contact", e.EntrantIndex));
                }
                case RaceEventType.Stumble:
                    if (!isPlayer && !IsNotable(e.EntrantIndex)) return null;
                    return Line(t, Pick($"{name}、荒れた水面で姿勢を崩した！", $"{name}がバランスを崩す！"), isPlayer ? CommentaryPriority.Normal : CommentaryPriority.Low, isPlayer, Key("stumble", e.EntrantIndex));
                case RaceEventType.GoodFlow:
                    if (!isPlayer) return null;
                    return Line(t, Pick($"{name}、いい流れをつかんだ！", $"{name}、流れに乗ってぐんぐん進む！"), CommentaryPriority.Low, true, Key("flow", e.EntrantIndex));
                case RaceEventType.FurnaceStart:
                    if (isPlayer) return Line(t, Pick($"{name}、葉脈炉 始動！ 一気に加速！", $"出た！ {name}の葉脈炉！"), CommentaryPriority.High, true);
                    if (!IsNotable(e.EntrantIndex)) return null;
                    return Line(t, Pick($"{name}が葉脈炉を始動！", $"{name}も炉に火を入れた！"), CommentaryPriority.Normal, false, Key("furnace", e.EntrantIndex));
                case RaceEventType.FurnaceOverflow:
                    if (isPlayer) return Line(t, $"OVER FLOW！ {name}、まだ引っ張るのか！？", CommentaryPriority.High, true);
                    if (!IsNotable(e.EntrantIndex)) return null;
                    return Line(t, $"{name}の炉がOVER FLOW！ 危ないぞ！", CommentaryPriority.Normal, false, Key("overflow", e.EntrantIndex));
                case RaceEventType.FurnaceRelease:
                    if (!isPlayer) return null;
                    return Line(t, $"{name}、炉を解除。{e.Value:0.0}秒の加速だった！", CommentaryPriority.Normal, true);
                case RaceEventType.FurnaceFailureOccurred:
                    return Line(t, $"{name}の葉脈炉にトラブル発生！", isPlayer ? CommentaryPriority.High : CommentaryPriority.Normal, isPlayer);
                case RaceEventType.CourseAccident:
                    return Line(t, $"事故だ！ {name}が大きく流された！", CommentaryPriority.High, isPlayer);
                case RaceEventType.Retire:
                    return Line(t, $"{name}、ここでリタイア……", CommentaryPriority.High, isPlayer);
                case RaceEventType.Lost:
                    return Line(t, $"{name}……流れの向こうへ消えていった……", CommentaryPriority.High, isPlayer);
                case RaceEventType.SkillUsed:
                {
                    string skill = SkillName((ObstructionSkill)(int)e.Value);
                    bool involved = isPlayer || e.OtherIndex == _playerIndex;
                    if (!involved && !IsNotable(e.EntrantIndex)) return null;
                    string text = e.OtherIndex >= 0 ? $"{name}の{skill}！ {Name(e.OtherIndex)}が巻き込まれた！" : $"{name}の{skill}！ 後ろの葉っぱは気をつけろ！";
                    return Line(t, text, involved ? CommentaryPriority.Normal : CommentaryPriority.Low, involved);
                }
                case RaceEventType.Finish:
                {
                    _finishCount++;
                    if (_finishCount == 1)
                    {
                        string text = isPlayer ? $"{name}、1着でゴール！！ やりました！" : $"{name}、1着でゴール！";
                        return Line(t, text, CommentaryPriority.High, isPlayer);
                    }

                    if (isPlayer) return Line(t, $"{name}、{_finishCount}着でゴール！ おつかれさま！", CommentaryPriority.High, true);
                    return null;
                }
                case RaceEventType.Timeout:
                    return Line(t, "ここでタイムアップ！ レース終了！", CommentaryPriority.High, false);
                default:
                    return null;
            }
        }

        /// <summary>先頭の入れ替わり・自分の順位の変化・折り返し・接戦。</summary>
        private CommentaryLine? FromPositions(int tick)
        {
            var leader = _simulator.RacerAtRank(1);
            if (leader.Index != _leaderIndex)
            {
                _leaderIndex = leader.Index;
                _leaderSinceTick = tick;
            }

            // 1秒以上先頭を保ったら「入れ替わった」と言う（抜きつ抜かれつで連呼しない）
            CommentaryLine? result = null;
            if (_leaderIndex != _announcedLeader && tick - _leaderSinceTick >= Ticks(_leaderIndex == _playerIndex ? 1.0 : 2.0))
            {
                bool first = _announcedLeader < 0;
                _announcedLeader = _leaderIndex;
                bool isPlayer = _leaderIndex == _playerIndex;
                string name = Name(_leaderIndex);
                if (!first || isPlayer)
                {
                    result = isPlayer
                        ? Line(tick, Pick($"{name}が先頭に躍り出た！", $"{name}、ついに先頭へ！"), CommentaryPriority.High, true)
                        : Line(tick, Pick($"先頭が入れ替わった！ {name}が前へ！", $"{name}が先頭を奪った！"), CommentaryPriority.Normal, false, "leader");
                }
            }

            var player = _simulator.Racers[_playerIndex];
            if (!player.IsOut)
            {
                if (player.Rank != _playerRank)
                {
                    _playerRank = player.Rank;
                    _playerRankSinceTick = tick;
                }

                if (_playerRank != _announcedPlayerRank && _playerRank != 1 && tick - _playerRankSinceTick >= Ticks(1.0))
                {
                    int previous = _announcedPlayerRank;
                    _announcedPlayerRank = _playerRank;
                    if (_playerRank < previous && result == null)
                        result = Line(tick, $"{PlayerName}、{_playerRank}番手に浮上！", CommentaryPriority.Normal, true);
                    else if (_playerRank >= previous + 2 && result == null)
                        result = Line(tick, $"{PlayerName}、{_playerRank}番手に下がった……ここから巻き返せるか", CommentaryPriority.Low, true);
                }
                else if (_playerRank == 1)
                {
                    _announcedPlayerRank = 1;
                }
            }

            if (result != null) return result;

            double distance = _simulator.Course.Distance;
            if (!_halfwayCalled && leader.Progress >= 0.5)
            {
                _halfwayCalled = true;
                return Line(tick, $"レースは折り返し！ 先頭は{Name(leader.Index)}、{PositionOfPlayer()}", CommentaryPriority.Low, false);
            }

            if (!_closeRaceCalled && leader.Progress >= 0.8 && !leader.IsOut)
            {
                var second = _simulator.RacerAtRank(2);
                if (!second.IsOut && (leader.Progress - second.Progress) * distance <= CloseGapMeters)
                {
                    _closeRaceCalled = true;
                    bool involved = leader.Index == _playerIndex || second.Index == _playerIndex;
                    return Line(tick, $"先頭争いは大接戦！ {Name(leader.Index)}と{Name(second.Index)}、ほぼ並んでいる！", involved ? CommentaryPriority.High : CommentaryPriority.Normal, involved);
                }
            }

            return null;
        }

        /// <summary>何も起きないときの状況説明。</summary>
        private CommentaryLine? Situation(int tick)
        {
            var player = _simulator.Racers[_playerIndex];
            if (player.IsOut) return null;
            var leader = _simulator.RacerAtRank(1);
            double remaining = (1.0 - leader.Progress) * _simulator.Course.Distance;
            if (player.Rank == 1)
            {
                double lead = (player.Progress - _simulator.RacerAtRank(2).Progress) * _simulator.Course.Distance;
                return Line(tick, $"{PlayerName}が先頭をキープ！ 2番手との差は{lead:0}m、残り{remaining:0}m！", CommentaryPriority.Low, true);
            }

            return Line(tick, Pick($"先頭は{Name(leader.Index)}、{PositionOfPlayer()} 残り{remaining:0}m！", $"残り{remaining:0}m。{PositionOfPlayer()}"), CommentaryPriority.Low, true);
        }

        private string PositionOfPlayer()
        {
            var player = _simulator.Racers[_playerIndex];
            if (player.IsOut) return string.Empty;
            if (player.Rank == 1) return $"{PlayerName}が先頭！";
            double behind = (_simulator.RacerAtRank(player.Rank - 1).Progress - player.Progress) * _simulator.Course.Distance;
            return $"{PlayerName}は{player.Rank}番手、前とは{behind:0}m。";
        }

        private static CommentaryLine Line(int tick, string text, CommentaryPriority priority, bool aboutPlayer, string key = null) => new CommentaryLine(tick, text, priority, aboutPlayer, key);

        private string Name(int index) => _simulator.Racers[index].Entrant.Name;

        private int Ticks(double seconds) => (int)System.Math.Round(seconds * _simulator.TickRate);

        /// <summary>言い回しを出した行数で選ぶ（乱数を使わないので再現性に影響しない）。</summary>
        private string Pick(string a, string b) => (_lines.Count & 1) == 0 ? a : b;

        private static string Key(string kind, int index) => kind + ":" + index;

        /// <summary>まわりの出来事を実況する相手か（先頭争い、または自分のすぐ前後）。</summary>
        private bool IsNotable(int index)
        {
            var racer = _simulator.Racers[index];
            if (racer.IsOut) return false;
            int playerRank = _simulator.Racers[_playerIndex].Rank;
            return racer.Rank <= 2 || System.Math.Abs(racer.Rank - playerRank) <= 1;
        }

        private static bool IsShowPiece(FlowSegment segment)
        {
            return segment.HasTag(SegmentTags.Rapid) || segment.HasTag(SegmentTags.Waterfall)
                || segment.HasTag(SegmentTags.Whirlpool) || segment.HasTag(SegmentTags.Narrow);
        }

        /// <summary>Segment の表示名（タグから決める）。</summary>
        public static string SectionName(FlowSegment segment)
        {
            if (segment.HasTag(SegmentTags.Waterfall)) return "滝";
            if (segment.HasTag(SegmentTags.Whirlpool)) return "渦";
            if (segment.HasTag(SegmentTags.Shortcut)) return "近道";
            if (segment.HasTag(SegmentTags.Narrow)) return "狭い岩間";
            if (segment.HasTag(SegmentTags.Rapid)) return "激流";
            if (segment.HasTag(SegmentTags.Mud)) return "泥の流れ";
            if (segment.IsGoal) return "最終直線";
            return "穏やかな流れ";
        }

        public static string SkillName(ObstructionSkill skill)
        {
            switch (skill)
            {
                case ObstructionSkill.Ram: return "体当たり";
                case ObstructionSkill.Mud: return "泥はね";
                case ObstructionSkill.Whirl: return "渦おこし";
                default: return "葉力妨害";
            }
        }
    }
}
