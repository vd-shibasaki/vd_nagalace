using System.Collections.Generic;
using System.Linq;
using NagaRace.Application.Race;
using NagaRace.Domain.Race;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    /// <summary>レース実況（文字）。再現性・レースへの影響なし・詰まりすぎないこと。</summary>
    public class CommentaryTests
    {
        private static (RaceSimulator sim, List<CommentaryLine> lines) Run(string courseId, ulong seed)
        {
            var sim = new RaceSimulator(TestData.Course(courseId), TestData.UniformEntrants(), RaceConditions.Default, TestData.Balance, seed);
            var commentator = new RaceCommentator(sim, 0);
            while (!sim.IsFinished)
            {
                sim.Step();
                commentator.Update();
            }

            return (sim, commentator.Lines.ToList());
        }

        [Test]
        public void 同じSeedなら同じ実況になる()
        {
            foreach (string course in TestData.CourseIds)
            {
                var a = Run(course, 11).lines.Select(l => l.ToString()).ToList();
                var b = Run(course, 11).lines.Select(l => l.ToString()).ToList();
                CollectionAssert.AreEqual(a, b, course);
            }
        }

        [Test]
        public void 実況してもレース結果は変わらない()
        {
            foreach (string course in TestData.CourseIds)
            {
                var plain = TestData.Run(course, TestData.UniformEntrants(), 21);
                var (sim, _) = Run(course, 21);
                var withCommentary = sim.BuildResult();
                Assert.AreEqual(plain.TotalTicks, withCommentary.TotalTicks, course);
                CollectionAssert.AreEqual(plain.Placings.Select(p => p.EntrantId), withCommentary.Placings.Select(p => p.EntrantId), course);
                CollectionAssert.AreEqual(plain.Events, withCommentary.Events, course);
            }
        }

        [Test]
        public void スタートとゴールを実況し_間隔をあけて流す()
        {
            foreach (string course in TestData.CourseIds)
            {
                for (ulong seed = 1; seed <= 5; seed++)
                {
                    var (sim, lines) = Run(course, seed);
                    if (seed == 1) TestContext.WriteLine($"--- {course} ---\n" + string.Join("\n", lines));
                    Assert.That(lines.First().Text, Does.Contain("スタート"), $"{course}/{seed}");
                    Assert.That(lines.Any(l => l.Text.Contains("1着でゴール")), $"{course}/{seed}: 1着の実況がない");

                    // 1レース（45〜90秒）で多すぎず少なすぎず
                    double seconds = sim.ElapsedSeconds;
                    Assert.GreaterOrEqual(lines.Count, seconds / 12.0, $"{course}/{seed}: 実況が少ない {lines.Count}行 / {seconds:0}秒");
                    Assert.LessOrEqual(lines.Count, seconds / RaceCommentator.HighPriorityGapSeconds + 2, $"{course}/{seed}: 実況が多すぎる");

                    // レース終了の瞬間以外は、最低間隔をあけている
                    int minGap = (int)System.Math.Round(RaceCommentator.HighPriorityGapSeconds * sim.TickRate);
                    for (int i = 1; i < lines.Count; i++)
                    {
                        if (lines[i].Tick == sim.Tick) continue;
                        Assert.GreaterOrEqual(lines[i].Tick - lines[i - 1].Tick, minGap, $"{course}/{seed}: {lines[i - 1]} → {lines[i]}");
                    }
                }
            }
        }

        [Test]
        public void 何も起きなくても長く黙らない()
        {
            var (sim, lines) = Run("test_straight", 3);
            int limit = (int)System.Math.Round((RaceCommentator.IdleSeconds + RaceCommentator.MinGapSeconds + 1.0) * sim.TickRate);
            int last = 0;
            var finishTick = sim.Racers.Min(r => r.IsFinished ? (int)System.Math.Round(r.FinishTimeSeconds * sim.TickRate) : int.MaxValue);
            foreach (var line in lines.Where(l => l.Tick <= finishTick))
            {
                Assert.LessOrEqual(line.Tick - last, limit, $"{line} の前に長い沈黙");
                last = line.Tick;
            }
        }
    }
}
