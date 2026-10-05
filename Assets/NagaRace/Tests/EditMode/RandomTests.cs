using System.Collections.Generic;
using NagaRace.Domain.Common;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    public class RandomTests
    {
        [Test]
        public void 同じSeedなら同じ系列になる()
        {
            var a = new Pcg32Random(12345);
            var b = new Pcg32Random(12345);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
            }
        }

        [Test]
        public void Seedが違えば系列が変わる()
        {
            var a = new Pcg32Random(1);
            var b = new Pcg32Random(2);
            int same = 0;
            for (int i = 0; i < 100; i++)
            {
                if (a.NextUInt() == b.NextUInt()) same++;
            }

            Assert.Less(same, 3);
        }

        [Test]
        public void NextDoubleは0以上1未満()
        {
            var random = new Pcg32Random(99);
            for (int i = 0; i < 10000; i++)
            {
                double v = random.NextDouble();
                Assert.GreaterOrEqual(v, 0.0);
                Assert.Less(v, 1.0);
            }
        }

        [Test]
        public void Rangeは指定範囲に収まり全値が出る()
        {
            var random = new Pcg32Random(7);
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
            {
                int v = random.Range(3, 9);
                Assert.That(v, Is.InRange(3, 8));
                seen.Add(v);
            }

            Assert.AreEqual(6, seen.Count);
        }

        [Test]
        public void サブストリームは出走者と用途で独立する()
        {
            var route0 = RandomStreams.Create(42, 0, RandomStreamId.Route);
            var route0Again = RandomStreams.Create(42, 0, RandomStreamId.Route);
            var route1 = RandomStreams.Create(42, 1, RandomStreamId.Route);
            var movement0 = RandomStreams.Create(42, 0, RandomStreamId.Movement);

            uint first = route0.NextUInt();
            Assert.AreEqual(first, route0Again.NextUInt());
            Assert.AreNotEqual(first, route1.NextUInt());
            Assert.AreNotEqual(first, movement0.NextUInt());
        }
    }
}
