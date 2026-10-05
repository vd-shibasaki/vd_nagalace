namespace NagaRace.Domain.Race
{
    public enum Weather
    {
        Sunny,
        Cloudy,
        Rain,
        HeavyRain,
        Wind,
    }

    public enum WaterLevel
    {
        Low,
        Normal,
        High,
    }

    /// <summary>レースの天候・水量（04 §2.1）。</summary>
    public sealed class RaceConditions
    {
        public RaceConditions(Weather weather, WaterLevel waterLevel)
        {
            Weather = weather;
            WaterLevel = waterLevel;
        }

        public static RaceConditions Default => new RaceConditions(Weather.Sunny, WaterLevel.Normal);

        public Weather Weather { get; }
        public WaterLevel WaterLevel { get; }
    }
}
