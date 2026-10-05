using System.Collections.Generic;
using NagaRace.Domain.Race;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    public class CourseGraphTests
    {
        private static FlowSegment Seg(string id, double length, SegmentTags tags = SegmentTags.Calm, params string[] next)
        {
            return new FlowSegment(id, length, 25, 0.4, 0.1, 1, 12, tags, next);
        }

        [Test]
        public void テストコースはすべて検証を通る()
        {
            foreach (var id in TestData.CourseIds)
            {
                var course = TestData.Course(id);
                Assert.AreEqual(0.0, course.StartSegment.ProgressStart, 1e-9, id);
                Assert.AreEqual(1.0, course.GoalSegment.ProgressEnd, 1e-9, id);
            }
        }

        [Test]
        public void 直線コースの進捗は距離比になる()
        {
            var course = RaceCourse.Build("c", "c", new List<FlowSegment>
            {
                Seg("a", 300, SegmentTags.Rapid, "b"),
                Seg("b", 700),
            });

            Assert.AreEqual(1000, course.Distance, 1e-9);
            Assert.AreEqual(0.3, course.GetSegment("a").ProgressEnd, 1e-9);
            Assert.AreEqual(0.3, course.GetSegment("b").ProgressStart, 1e-9);
            Assert.AreEqual(0.65, course.GetSegment("b").ProgressAt(350), 1e-9);
        }

        [Test]
        public void 分岐の各経路は同じ進捗区間を覆う()
        {
            // a → (short 200 | long1 150 → long2 150) → c
            var course = RaceCourse.Build("c", "c", new List<FlowSegment>
            {
                Seg("a", 400, SegmentTags.Calm, "short", "long1"),
                Seg("short", 200, SegmentTags.Calm, "c"),
                Seg("long1", 150, SegmentTags.Calm, "long2"),
                Seg("long2", 150, SegmentTags.Calm, "c"),
                Seg("c", 400),
            });

            // 最短全長 = 400 + 200 + 400
            Assert.AreEqual(1000, course.Distance, 1e-9);
            var shortSeg = course.GetSegment("short");
            Assert.AreEqual(0.4, shortSeg.ProgressStart, 1e-9);
            Assert.AreEqual(0.6, shortSeg.ProgressEnd, 1e-9);
            Assert.AreEqual(0.4, course.GetSegment("long1").ProgressStart, 1e-9);
            // 長い経路の中間点は、経路内の距離比（150/300）で補間される
            Assert.AreEqual(0.5, course.GetSegment("long1").ProgressEnd, 1e-9);
            Assert.AreEqual(0.6, course.GetSegment("long2").ProgressEnd, 1e-9);
        }

        [Test]
        public void ループは検出される()
        {
            Assert.Throws<CourseValidationException>(() => RaceCourse.Build("c", "c", new List<FlowSegment>
            {
                Seg("start", 100, SegmentTags.Rapid, "a"),
                Seg("a", 100, SegmentTags.Calm, "b"),
                Seg("b", 100, SegmentTags.Calm, "a", "goal"),
                Seg("goal", 100),
            }));
        }

        [Test]
        public void ゴールが2つあると不正()
        {
            Assert.Throws<CourseValidationException>(() => RaceCourse.Build("c", "c", new List<FlowSegment>
            {
                Seg("a", 100, SegmentTags.Calm, "b", "c"),
                Seg("b", 100),
                Seg("c", 100),
            }));
        }

        [Test]
        public void 存在しない次Segmentは不正()
        {
            Assert.Throws<CourseValidationException>(() => RaceCourse.Build("c", "c", new List<FlowSegment>
            {
                Seg("a", 100, SegmentTags.Rapid, "missing"),
            }));
        }

        [Test]
        public void 見せ場がないコースは不正()
        {
            Assert.Throws<CourseValidationException>(() => RaceCourse.Build("c", "c", new List<FlowSegment>
            {
                Seg("a", 100, SegmentTags.Calm, "b"),
                Seg("b", 100),
            }));
        }

        [Test]
        public void 合流先を共有するのに次の集合が違うと不正()
        {
            Assert.Throws<CourseValidationException>(() => RaceCourse.Build("c", "c", new List<FlowSegment>
            {
                Seg("a", 100, SegmentTags.Calm, "b", "c"),
                Seg("b", 100, SegmentTags.Calm, "d", "c"),
                Seg("c", 100, SegmentTags.Calm, "d"),
                Seg("d", 100),
            }));
        }
    }
}
