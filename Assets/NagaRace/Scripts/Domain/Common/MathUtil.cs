using System;

namespace NagaRace.Domain.Common
{
    /// <summary>UnityEngine.Mathf を使わないための最小限の数学関数。</summary>
    public static class MathUtil
    {
        public static double Clamp(double value, double min, double max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        public static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        public static double Clamp01(double value)
        {
            return Clamp(value, 0.0, 1.0);
        }

        public static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * Clamp01(t);
        }

        /// <summary>
        /// 設定ファイルの [能力0の値, 能力100の値] 形式のレンジを、能力値で線形補間する。
        /// </summary>
        public static double LerpByStat(double[] range, int stat)
        {
            if (range == null || range.Length != 2)
            {
                throw new ArgumentException("レンジは [a, b] の2要素で指定してください。");
            }

            return Lerp(range[0], range[1], stat / 100.0);
        }

        public static double MoveTowards(double current, double target, double maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
            {
                return target;
            }

            return current + Math.Sign(target - current) * maxDelta;
        }

        /// <summary>平均0・おおむね標準偏差1のノイズ（一様乱数3つの和で近似。極端な値を出さない）。</summary>
        public static double ApproxGaussian(IRandomProvider random)
        {
            double sum = random.NextDouble() + random.NextDouble() + random.NextDouble();
            // 一様3つの和の分散は 3/12 = 0.25 → 標準偏差 0.5
            return (sum - 1.5) / 0.5;
        }
    }
}
