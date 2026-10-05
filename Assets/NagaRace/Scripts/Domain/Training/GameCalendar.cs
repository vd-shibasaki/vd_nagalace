using System;
using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Common;
using NagaRace.Domain.Race;

namespace NagaRace.Domain.Training
{
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter,
    }

    /// <summary>
    /// ゲーム内の暦（02 §7）。1アクション = 1日。1週4日・1季3週・1年4季（設定で変更可）。
    /// </summary>
    public readonly struct GameDate
    {
        public GameDate(int day, CalendarBalance calendar)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            Day = day;
            int daysPerSeason = calendar.DaysPerWeek * calendar.WeeksPerSeason;
            int daysPerYear = daysPerSeason * calendar.SeasonsPerYear;
            int index = day - 1;
            Year = index / daysPerYear + 1;
            int inYear = index % daysPerYear;
            Season = (Season)(inYear / daysPerSeason % 4);
            int inSeason = inYear % daysPerSeason;
            Week = inSeason / calendar.DaysPerWeek + 1;
            DayOfWeek = inSeason % calendar.DaysPerWeek + 1;
        }

        /// <summary>通算日（1始まり）。</summary>
        public int Day { get; }
        public int Year { get; }
        public Season Season { get; }
        public int Week { get; }
        public int DayOfWeek { get; }

        public string Label => $"{Year}年目 {SeasonLabel(Season)} {Week}週{DayOfWeek}日";

        public static string SeasonLabel(Season season)
        {
            switch (season)
            {
                case Season.Summer: return "夏";
                case Season.Autumn: return "秋";
                case Season.Winter: return "冬";
                default: return "春";
            }
        }
    }

    /// <summary>
    /// その日の天気と水量。世界 Seed と日付だけで決まるため、セーブ・ロードしても同じ日は同じ天気になる。
    /// </summary>
    public static class WeatherForecast
    {
        private static readonly Weather[] Weathers = { Weather.Sunny, Weather.Cloudy, Weather.Rain, Weather.HeavyRain, Weather.Wind };

        public static RaceConditions For(ulong worldSeed, GameDate date, BalanceConfig balance)
        {
            var random = RandomStreams.Create(worldSeed ^ RandomStreams.SplitMix64((ulong)date.Day * 0x9E3779B97F4A7C15UL), RandomStreams.SharedIndex, RandomStreamId.Event);
            var w = balance.Weather;
            var weights = w.SeasonWeights != null && w.SeasonWeights.TryGetValue(date.Season.ToString(), out var table) ? table : null;

            var weather = Weather.Sunny;
            if (weights != null)
            {
                double total = 0;
                foreach (var k in Weathers) total += Lookup(weights, k.ToString());
                double roll = random.NextDouble() * total;
                foreach (var k in Weathers)
                {
                    roll -= Lookup(weights, k.ToString());
                    if (roll < 0)
                    {
                        weather = k;
                        break;
                    }
                }
            }

            double waterRoll = random.NextDouble();
            var level = WaterLevel.Normal;
            if (waterRoll < Lookup(w.HighWaterChance, weather.ToString())) level = WaterLevel.High;
            else if (waterRoll < Lookup(w.LowWaterChance, weather.ToString())) level = WaterLevel.Low;
            return new RaceConditions(weather, level);
        }

        public static string WeatherLabel(Weather weather)
        {
            switch (weather)
            {
                case Weather.Cloudy: return "くもり";
                case Weather.Rain: return "雨";
                case Weather.HeavyRain: return "大雨";
                case Weather.Wind: return "強風";
                default: return "晴れ";
            }
        }

        public static string WaterLabel(WaterLevel level)
        {
            switch (level)
            {
                case WaterLevel.Low: return "少なめ";
                case WaterLevel.High: return "増水";
                default: return "普通";
            }
        }

        private static double Lookup(Dictionary<string, double> table, string key)
        {
            return table != null && table.TryGetValue(key, out double v) ? v : 0;
        }
    }
}
