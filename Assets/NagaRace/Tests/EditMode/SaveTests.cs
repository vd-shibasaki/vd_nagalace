using System;
using System.IO;
using System.Linq;
using NagaRace.Application.Save;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Lineage;
using NagaRace.Domain.Race;
using NagaRace.Domain.Training;
using NagaRace.Infrastructure.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    /// <summary>10_ACCEPTANCE_TESTS.md の G（Save）。</summary>
    public class SaveTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "nagarace-save-tests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        /// <summary>育成・レース・引退・接ぎ木の状態を一通り含むセッション。</summary>
        private static (GameSession session, GameSettings settings) CreateRichSession()
        {
            var b = TestData.Balance;
            var session = new GameSession(0xFEEDFACECAFEBEEFUL, 1);
            var akane = new CharacterState("akane", "アカネ", "maple", CharacterStats.Uniform(55), FurnaceStats.Uniform(50), null, Personality.Gambler, lineageId: "akane-line");
            akane.Growth = GrowthProfile.Roll(GrowthType.Early, b, session.RandomFor("growth", "akane"));
            akane.SetFactors(new[] { new GeneFactor("swift", 2, false) });
            session.Roster.Add(akane);
            session.Roster.Add(new CharacterState("gin", "ギンナン", "ginkgo", CharacterStats.Uniform(60), FurnaceStats.Uniform(55), null, Personality.Cautious));

            // 育成（体質の累積・連続回数・疲労を作る）
            for (int i = 0; i < 6; i++)
            {
                TrainingResolver.Apply(akane, TestData.Trainings.Get("sparring"), Weather.Cloudy, akane.PhaseOn(session.Day), TestData.Trainings, b, session.RandomFor("training", "akane"));
                session.AdvanceDay(b);
            }

            // レース（戦績・耐久・疲労）
            var entrants = TestData.UniformEntrants();
            entrants[0] = RaceEntrant.FromCharacter(akane, true);
            var result = TestData.Run(TestData.StandardCourse, entrants, 3);
            RaceAftermath.Apply(akane, result.PlacingOf(0), TestData.StandardCourse, session.Day, b);

            // 引退 → 親木
            session.Roster.Retire("gin", session.Day);
            session.AddParentTree(ParentTree.FromCharacter(session.Roster.Find("gin"), TestData.Lineage, b, session.Day));

            var settings = new GameSettings { AutoFurnace = true, PlayerAiStyle = AiStyle.Aggressive, PlaybackSpeed = 2f, SelectedCharacterId = "akane" };
            return (session, settings);
        }

        private static string Snapshot(GameSession session, GameSettings settings)
        {
            return new SaveSerializer().Serialize(SaveMapper.ToDto(session, settings));
        }

        [Test]
        public void G01_保存して読み込むと主要状態が一致する()
        {
            var (session, settings) = CreateRichSession();
            var repository = new JsonSaveRepository(_directory);
            repository.Save(session, settings);

            var loaded = new JsonSaveRepository(_directory).Load();
            Assert.IsTrue(loaded.Success, loaded.Error);
            Assert.AreEqual(SaveSource.Primary, loaded.Source);

            // 保存形式に落とした内容が完全に一致する
            Assert.AreEqual(Snapshot(session, settings), Snapshot(loaded.Session, loaded.Settings));

            var akane = loaded.Session.Roster.Find("akane");
            Assert.AreEqual(session.Day, loaded.Session.Day);
            Assert.AreEqual(1, akane.Record.Starts);
            Assert.AreEqual(GrowthType.Early, akane.Growth.Type);
            Assert.AreEqual("swift", akane.Factors.Single().FactorId);
            Assert.IsTrue(akane.BodyAccumulation.ContainsKey("collisionExperience"));
            Assert.AreEqual(1, loaded.Session.ParentTrees.Count);
            Assert.AreEqual(CharacterStatus.Retired, loaded.Session.Roster.Album.Single().Status);
            Assert.AreEqual(AiStyle.Aggressive, loaded.Settings.PlayerAiStyle);
        }

        [Test]
        public void G01_読み込んだ後も天気と乱数が同じになる()
        {
            var (session, settings) = CreateRichSession();
            var repository = new JsonSaveRepository(_directory);
            repository.Save(session, settings);
            var loaded = repository.Load().Session;

            var b = TestData.Balance;
            Assert.AreEqual(session.TodayConditions(b).Weather, loaded.TodayConditions(b).Weather);
            Assert.AreEqual(session.RandomFor("training", "akane").NextUInt(), loaded.RandomFor("training", "akane").NextUInt());
        }

        [Test]
        public void G02_古いschemaVersionはMigrationしてから読み込む()
        {
            var (session, settings) = CreateRichSession();
            new JsonSaveRepository(_directory).Save(session, settings);

            // 現行 v1 のセーブを、仮の v2 形式へ移行して読む（v2 では名前に記号を付ける、という架空の変更）
            var migrated = new JsonSaveRepository(_directory, serializer: new SaveSerializer(new ISaveMigration[] { new RenameMigration() }, targetVersion: 2)).Load();
            Assert.IsTrue(migrated.Success, migrated.Error);
            Assert.AreEqual("アカネ★", migrated.Session.Roster.Find("akane").Name);

            // 移行処理がなければ、クラッシュせずエラーとして返す
            var missing = new JsonSaveRepository(_directory, serializer: new SaveSerializer(targetVersion: 3)).Load();
            Assert.IsFalse(missing.Success);
            StringAssert.Contains("移行処理がありません", missing.Error);
        }

        [Test]
        public void G03_壊れたJSONでもクラッシュせずバックアップを試す()
        {
            var (session, settings) = CreateRichSession();
            var repository = new JsonSaveRepository(_directory);
            repository.Save(session, settings);
            session.SetDay(session.Day + 5);
            repository.Save(session, settings); // 1回目がバックアップになる

            File.WriteAllText(repository.PrimaryPath, "{ this is not json");
            var fromBackup = repository.Load();
            Assert.IsTrue(fromBackup.Success);
            Assert.AreEqual(SaveSource.Backup, fromBackup.Source);
            Assert.AreEqual(session.Day - 5, fromBackup.Session.Day);
            Assert.IsNotNull(fromBackup.Error);

            File.WriteAllText(repository.BackupPath, string.Empty);
            var none = repository.Load();
            Assert.IsFalse(none.Success);
            Assert.AreEqual(SaveSource.None, none.Source);
        }

        [Test]
        public void G03_改ざんはチェックサムで検出する()
        {
            var (session, settings) = CreateRichSession();
            var repository = new JsonSaveRepository(_directory);
            repository.Save(session, settings);

            var json = JObject.Parse(File.ReadAllText(repository.PrimaryPath));
            json["payload"]["World"]["Day"] = 999;
            File.WriteAllText(repository.PrimaryPath, json.ToString());

            var result = repository.Load();
            Assert.IsFalse(result.Success, "改ざんされたセーブを読めてしまう");
            StringAssert.Contains("チェックサム", result.Error);
        }

        private sealed class RenameMigration : ISaveMigration
        {
            public int FromVersion => 1;

            public JObject Migrate(JObject payload)
            {
                foreach (var c in (JArray)payload["Characters"])
                {
                    c["Name"] = c.Value<string>("Name") + "★";
                }

                return payload;
            }
        }
    }
}
