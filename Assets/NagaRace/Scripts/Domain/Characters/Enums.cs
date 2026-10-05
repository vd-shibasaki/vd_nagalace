namespace NagaRace.Domain.Characters
{
    /// <summary>
    /// 性格。1個体に1つ。レースAIの判断傾向を決める（02 §5.1）。
    /// TODO(M5): 負けず嫌い・喧嘩葉は育成・妨害と合わせて追加する。
    /// </summary>
    public enum Personality
    {
        Normal,
        Cautious,
        Gambler,
    }

    /// <summary>レース前にプレイヤーが選ぶ AI作戦（03 §6）。</summary>
    public enum AiStyle
    {
        Safe,
        Standard,
        Aggressive,
    }

    /// <summary>調子（02 §8）。</summary>
    public enum Condition
    {
        Excellent,
        Good,
        Normal,
        Poor,
        Terrible,
    }

    public static class CharacterEnumKeys
    {
        /// <summary>設定ファイルのキー（"cautious" 等）へ変換する。</summary>
        public static string ToConfigKey(this Personality personality)
        {
            switch (personality)
            {
                case Personality.Cautious: return "cautious";
                case Personality.Gambler: return "gambler";
                default: return "normal";
            }
        }
    }
}
