using NagaRace.Domain.Common;

namespace NagaRace.Domain.Characters
{
    /// <summary>適性の種類（02 §5.2）。</summary>
    public enum AffinityType
    {
        Rapid,
        Whirlpool,
        Narrow,
        Deep,
        Sprint,
        Middle,
        Long,
    }

    /// <summary>適性値。0〜100、50 が補正なし。</summary>
    public sealed class Affinities
    {
        public const int Neutral = 50;

        private readonly int[] _values;

        public Affinities(int rapid, int whirlpool, int narrow, int deep, int sprint, int middle, int @long)
        {
            _values = new[]
            {
                Clamp(rapid), Clamp(whirlpool), Clamp(narrow), Clamp(deep),
                Clamp(sprint), Clamp(middle), Clamp(@long),
            };
        }

        private Affinities(int[] values)
        {
            _values = values;
        }

        public static Affinities NeutralAll => new Affinities(Neutral, Neutral, Neutral, Neutral, Neutral, Neutral, Neutral);

        public int this[AffinityType type] => _values[(int)type];

        public Affinities With(AffinityType type, int value)
        {
            var copy = (int[])_values.Clone();
            copy[(int)type] = Clamp(value);
            return new Affinities(copy);
        }

        private static int Clamp(int value)
        {
            return MathUtil.Clamp(value, 0, 100);
        }
    }
}
