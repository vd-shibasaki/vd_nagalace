using System.Collections.Generic;

namespace NagaRace.Application.Analytics
{
    /// <summary>
    /// プレイログの記録先（11 §8 Analytics）。目的は面白さの改善であり、過度な課金誘導のためだけには使わない。
    /// MVP では端末内のファイルにだけ記録し、外部へは送信しない。
    /// </summary>
    public interface IAnalyticsSink
    {
        void Log(string eventName, IReadOnlyDictionary<string, object> data = null);
    }

    /// <summary>何も記録しない（テスト・記録オフ用）。</summary>
    public sealed class NullAnalyticsSink : IAnalyticsSink
    {
        public void Log(string eventName, IReadOnlyDictionary<string, object> data = null)
        {
        }
    }
}
