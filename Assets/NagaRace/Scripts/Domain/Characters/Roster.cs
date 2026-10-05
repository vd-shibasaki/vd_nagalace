using System;
using System.Collections.Generic;

namespace NagaRace.Domain.Characters
{
    /// <summary>
    /// 思い出アルバムの1件（02 §12）。引退・ロスト後も「ゲームから消えた」感覚にしないための記録。
    /// </summary>
    public sealed class AlbumEntry
    {
        public AlbumEntry(CharacterState character)
        {
            Character = character ?? throw new ArgumentNullException(nameof(character));
        }

        /// <summary>引退・ロスト時点の個体（以後は変更しない）。</summary>
        public CharacterState Character { get; }
        public string Id => Character.Id;
        public string Name => Character.Name;
        public CharacterStatus Status => Character.Status;
        public int BornDay => Character.BornDay;
        public int? EndedDay => Character.EndedDay;
        public string EndReason => Character.EndReason;
        public CharacterRecord Record => Character.Record;

        /// <summary>最終レース。</summary>
        public RaceHistoryEntry LastRace => Record.History.Count > 0 ? Record.History[Record.History.Count - 1] : null;

        /// <summary>アルバムに添える一言（自動生成）。</summary>
        public string Comment
        {
            get
            {
                if (Status == CharacterStatus.Lost)
                {
                    return Record.Wins > 0
                        ? $"{Record.Wins}勝を挙げた{Name}は、流れの向こうへ旅立った。"
                        : $"{Name}は最後まで流れに挑み続けた。";
                }

                return Record.Wins > 0
                    ? $"{Record.Starts}戦{Record.Wins}勝。{Name}の流れは次の世代へ。"
                    : $"{Record.Starts}戦を走り抜いた{Name}。おつかれさま。";
            }
        }
    }

    /// <summary>手持ちの個体とアルバム。Lost 後は現役一覧から外れ、アルバムに残る（10 C-04）。</summary>
    public sealed class Roster
    {
        private readonly List<CharacterState> _active = new List<CharacterState>();
        private readonly List<AlbumEntry> _album = new List<AlbumEntry>();

        public IReadOnlyList<CharacterState> Active => _active;
        public IReadOnlyList<AlbumEntry> Album => _album;

        public void Add(CharacterState character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (Find(character.Id) != null) throw new InvalidOperationException($"個体 {character.Id} は既にいます。");
            if (character.IsActive) _active.Add(character);
            else _album.Add(new AlbumEntry(character));
        }

        public CharacterState Find(string id)
        {
            foreach (var c in _active)
            {
                if (c.Id == id) return c;
            }

            foreach (var entry in _album)
            {
                if (entry.Id == id) return entry.Character;
            }

            return null;
        }

        /// <summary>任意引退（02 §10）。</summary>
        public AlbumEntry Retire(string id, int day)
        {
            var character = RequireActive(id);
            character.Retire(day);
            return MoveToAlbum(character);
        }

        public AlbumEntry MarkLost(string id, int day, string reason)
        {
            var character = RequireActive(id);
            character.MarkLost(day, reason);
            return MoveToAlbum(character);
        }

        private AlbumEntry MoveToAlbum(CharacterState character)
        {
            _active.Remove(character);
            var entry = new AlbumEntry(character);
            _album.Add(entry);
            return entry;
        }

        private CharacterState RequireActive(string id)
        {
            foreach (var c in _active)
            {
                if (c.Id == id) return c;
            }

            throw new InvalidOperationException($"現役の個体 {id} が見つかりません。");
        }
    }
}
