using System;
using System.Collections.Generic;
using NagaRace.Application.Race;
using NagaRace.Domain.Common;
using NagaRace.Domain.Lineage;
using NagaRace.Domain.Training;

namespace NagaRace.Application.Lineage
{
    /// <summary>因子・突然変異定義の取得元。</summary>
    public interface ILineageCatalogRepository
    {
        LineageCatalog Load();
    }

    /// <summary>
    /// 親木の登録と接ぎ木（07 §6 RetireCharacter / ExecuteGrafting）。
    /// </summary>
    public sealed class GraftingUseCase
    {
        private readonly IBalanceRepository _balance;
        private readonly ISpeciesRepository _species;
        private readonly ILineageCatalogRepository _catalog;

        public GraftingUseCase(IBalanceRepository balance, ISpeciesRepository species, ILineageCatalogRepository catalog)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _species = species ?? throw new ArgumentNullException(nameof(species));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>引退・ロストした個体を親木として登録する。</summary>
        public ParentTree RegisterParentTree(GameSession session, string characterId)
        {
            var character = session.Roster.Find(characterId) ?? throw new InvalidOperationException($"個体 {characterId} がいません。");
            var tree = ParentTree.FromCharacter(character, _catalog.Load(), _balance.Load(), session.Day);
            session.AddParentTree(tree);
            return tree;
        }

        public GraftingPreview Preview(GameSession session, string treeAId, string treeBId)
        {
            var (a, b) = Require(session, treeAId, treeBId);
            return GraftingResolver.Preview(a, b, _species.GetAll(), _balance.Load());
        }

        /// <summary>若葉の候補を作る（同じ日・同じ組み合わせなら同じ候補）。</summary>
        public List<GraftCandidate> CreateCandidates(GameSession session, string treeAId, string treeBId)
        {
            var (a, b) = Require(session, treeAId, treeBId);
            ulong seed = session.WorldSeed
                ^ RandomStreams.SplitMix64((ulong)session.Day)
                ^ RandomStreams.SplitMix64(RandomStreams.StableHash(a.Id + "|" + b.Id));
            return GraftingResolver.CreateCandidates(a, b, _species.GetAll(), _catalog.Load(), _balance.Load(), seed, session.Day, $"leaf-{session.Day}-{session.Roster.Active.Count + session.Roster.Album.Count}");
        }

        /// <summary>候補の1体を迎え入れる。接ぎ木は日を消費しない（02 §7 のアクションに含まれないため）。</summary>
        public void Adopt(GameSession session, GraftCandidate candidate, string name)
        {
            candidate.Character.Rename(name);
            session.Roster.Add(candidate.Character);
        }

        private static (ParentTree a, ParentTree b) Require(GameSession session, string treeAId, string treeBId)
        {
            if (treeAId == treeBId) throw new InvalidOperationException("同じ親木どうしでは接ぎ木できません。");
            var a = session.FindParentTree(treeAId) ?? throw new InvalidOperationException($"親木 {treeAId} がありません。");
            var b = session.FindParentTree(treeBId) ?? throw new InvalidOperationException($"親木 {treeBId} がありません。");
            return (a, b);
        }
    }
}
