using System;

namespace NagaRace.Domain.Characters
{
    public enum ParameterGroup
    {
        Stats,
        Affinity,
        Furnace,
    }

    /// <summary>
    /// データ上のキー（"stats.flowSpeed" / "affinity.rapid" / "furnace.output"）で能力を読み書きする（05 §2.1）。
    /// </summary>
    public readonly struct CharacterParameter : IEquatable<CharacterParameter>
    {
        private CharacterParameter(string key, ParameterGroup group, int index)
        {
            Key = key;
            Group = group;
            Index = index;
        }

        public string Key { get; }
        public ParameterGroup Group { get; }
        private int Index { get; }

        public bool Equals(CharacterParameter other) => Group == other.Group && Index == other.Index;

        public override bool Equals(object obj) => obj is CharacterParameter other && Equals(other);

        public override int GetHashCode() => ((int)Group * 64) + Index;

        public override string ToString() => Key;

        public static CharacterParameter Parse(string key)
        {
            int dot = key.IndexOf('.');
            if (dot <= 0) throw new FormatException($"パラメータキーが不正です: {key}");
            string group = key.Substring(0, dot);
            string name = key.Substring(dot + 1);
            switch (group)
            {
                case "stats" when Enum.TryParse(name, true, out StatType s):
                    return new CharacterParameter(key, ParameterGroup.Stats, (int)s);
                case "affinity" when Enum.TryParse(name, true, out AffinityType a):
                    return new CharacterParameter(key, ParameterGroup.Affinity, (int)a);
                case "furnace" when Enum.TryParse(name, true, out FurnaceStat f):
                    return new CharacterParameter(key, ParameterGroup.Furnace, (int)f);
                default:
                    throw new FormatException($"パラメータキーが不正です: {key}");
            }
        }

        public int Get(CharacterState c)
        {
            switch (Group)
            {
                case ParameterGroup.Stats: return c.Stats[(StatType)Index];
                case ParameterGroup.Affinity: return c.Affinities[(AffinityType)Index];
                default: return c.Furnace[(FurnaceStat)Index];
            }
        }

        /// <summary>値を設定する（0〜100 に丸める）。</summary>
        public void Set(CharacterState c, int value)
        {
            switch (Group)
            {
                case ParameterGroup.Stats:
                    c.Stats = c.Stats.With((StatType)Index, value);
                    break;
                case ParameterGroup.Affinity:
                    c.Affinities = c.Affinities.With((AffinityType)Index, value);
                    break;
                default:
                    c.Furnace = c.Furnace.With((FurnaceStat)Index, Math.Max(0, Math.Min(100, value)));
                    break;
            }
        }

        public void Add(CharacterState c, int delta)
        {
            Set(c, Get(c) + delta);
        }

        /// <summary>UI 表示名。</summary>
        public string Label
        {
            get
            {
                switch (Key)
                {
                    case "stats.flowSpeed": return "流速";
                    case "stats.acceleration": return "加速";
                    case "stats.buoyancy": return "浮力";
                    case "stats.stability": return "安定";
                    case "stats.toughness": return "頑丈";
                    case "stats.judgement": return "判断";
                    case "affinity.rapid": return "激流適性";
                    case "affinity.whirlpool": return "渦適性";
                    case "affinity.narrow": return "狭所適性";
                    case "affinity.deep": return "深水適性";
                    case "furnace.output": return "炉出力";
                    case "furnace.startup": return "炉起動";
                    case "furnace.criticalDuration": return "炉臨界";
                    case "furnace.cooling": return "炉冷却";
                    case "furnace.durability": return "炉耐久";
                    default: return Key;
                }
            }
        }
    }
}
