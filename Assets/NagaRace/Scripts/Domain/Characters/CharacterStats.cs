using System;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Characters
{
    /// <summary>6主能力（02 §4）。</summary>
    public enum StatType
    {
        FlowSpeed,
        Acceleration,
        Buoyancy,
        Stability,
        /// <summary>頑丈（旧称「耐久」）。HP相当の durability とは別物。</summary>
        Toughness,
        Judgement,
    }

    /// <summary>6主能力の値。0〜100 の整数で保持する不変オブジェクト。</summary>
    public sealed class CharacterStats
    {
        public const int MinValue = 0;
        public const int MaxValue = 100;
        public static readonly int Count = Enum.GetValues(typeof(StatType)).Length;

        private readonly int[] _values;

        public CharacterStats(int flowSpeed, int acceleration, int buoyancy, int stability, int toughness, int judgement)
        {
            _values = new[]
            {
                Normalize(flowSpeed),
                Normalize(acceleration),
                Normalize(buoyancy),
                Normalize(stability),
                Normalize(toughness),
                Normalize(judgement),
            };
        }

        private CharacterStats(int[] values)
        {
            _values = values;
        }

        public int FlowSpeed => _values[(int)StatType.FlowSpeed];
        public int Acceleration => _values[(int)StatType.Acceleration];
        public int Buoyancy => _values[(int)StatType.Buoyancy];
        public int Stability => _values[(int)StatType.Stability];
        public int Toughness => _values[(int)StatType.Toughness];
        public int Judgement => _values[(int)StatType.Judgement];

        public int this[StatType type] => _values[(int)type];

        public static CharacterStats Uniform(int value)
        {
            return new CharacterStats(value, value, value, value, value, value);
        }

        public CharacterStats With(StatType type, int value)
        {
            var copy = (int[])_values.Clone();
            copy[(int)type] = Normalize(value);
            return new CharacterStats(copy);
        }

        public CharacterStats AddAll(int delta)
        {
            var copy = new int[_values.Length];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = Normalize(_values[i] + delta);
            }

            return new CharacterStats(copy);
        }

        public override string ToString()
        {
            return $"流速{FlowSpeed} 加速{Acceleration} 浮力{Buoyancy} 安定{Stability} 頑丈{Toughness} 判断{Judgement}";
        }

        private static int Normalize(int value)
        {
            return MathUtil.Clamp(value, MinValue, MaxValue);
        }
    }
}
