namespace NagaRace.Domain.Common
{
    /// <summary>
    /// ドメインで使う乱数の抽象。UnityEngine.Random / System.Random は使わない（07 §10）。
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>32bit の一様乱数。</summary>
        uint NextUInt();

        /// <summary>[0, 1) の一様乱数。</summary>
        double NextDouble();

        /// <summary>[minInclusive, maxExclusive) の整数。</summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>[min, max) の実数。</summary>
        double Range(double min, double max);
    }
}
