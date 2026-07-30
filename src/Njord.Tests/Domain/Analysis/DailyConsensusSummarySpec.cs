using Njord.Domain.Analysis;
using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Analysis;

public sealed class DailyConsensusSummarySpec
{
    private static readonly ParameterDef Temperature = new(
        "temperature_2m", "°C", "temperature", "temperature",
        ParameterGroup.Weather, ParameterGranularity.Hourly);

    private static readonly ParameterDef Precipitation = new(
        "precipitation", "mm", "precipitation", "precipitation",
        ParameterGroup.Weather, ParameterGranularity.Hourly);

    private static readonly ParameterDef WindSpeed = new(
        "wind_speed_10m", "m/s", "wind_speed", "wind_speed_10m",
        ParameterGroup.Weather, ParameterGranularity.Hourly);

    private static readonly ParameterDef WeatherCode = new(
        "weather_code", "wmo code", null, "weather_code",
        ParameterGroup.Weather, ParameterGranularity.Hourly);

    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly WeatherModel Ecmwf = new("ecmwf_ifs025");

    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Cest = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    [Fact(Timeout = 5000)]
    public void Temperature_max_and_min_from_hourly_medians()
    {
        var consensus = BuildConsensus(Temperature, new Dictionary<string, double>
        {
            ["h0"] = 20.0,
            ["h1"] = 25.0,
            ["h2"] = 30.0,
            ["h3"] = 28.0,
            ["h4"] = 22.0,
        });

        var summaries = DailyConsensusSummary.Aggregate(consensus, Now, TimeZoneInfo.Utc);

        var day = Assert.Single(summaries);
        Assert.Equal(30.0, day.TemperatureMax);
        Assert.Equal(20.0, day.TemperatureMin);
    }

    [Fact(Timeout = 5000)]
    public void Precipitation_sum_across_hours()
    {
        var consensus = BuildConsensus(
            (Temperature, new Dictionary<string, double> { ["h0"] = 20, ["h1"] = 21, ["h2"] = 22, ["h3"] = 23, ["h4"] = 24 }),
            (Precipitation, new Dictionary<string, double> { ["h0"] = 0.0, ["h1"] = 0.5, ["h2"] = 1.2, ["h3"] = 0.3, ["h4"] = 0.0 }));

        var summaries = DailyConsensusSummary.Aggregate(consensus, Now, TimeZoneInfo.Utc);

        var day = Assert.Single(summaries);
        Assert.Equal(2.0, day.PrecipitationSum);
    }

    [Fact(Timeout = 5000)]
    public void Wind_speed_max_from_hourly_medians()
    {
        var consensus = BuildConsensus(
            (Temperature, new Dictionary<string, double> { ["h0"] = 20, ["h1"] = 21 }),
            (WindSpeed, new Dictionary<string, double> { ["h0"] = 3.0, ["h1"] = 8.0 }));

        var summaries = DailyConsensusSummary.Aggregate(consensus, Now, TimeZoneInfo.Utc);

        var day = Assert.Single(summaries);
        Assert.Equal(8.0, day.WindSpeedMax);
    }

    [Fact(Timeout = 5000)]
    public void Weather_code_at_horizon_closest_to_local_noon()
    {
        var nowMorning = new DateTimeOffset(2026, 7, 29, 6, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(
            (Temperature, new Dictionary<string, double> { ["h0"] = 20, ["h1"] = 21, ["h2"] = 22, ["h3"] = 23, ["h4"] = 24, ["h5"] = 25, ["h6"] = 26 }),
            (WeatherCode, new Dictionary<string, double> { ["h0"] = 3, ["h1"] = 3, ["h2"] = 3, ["h3"] = 3, ["h4"] = 3, ["h5"] = 80, ["h6"] = 61 }));

        var summaries = DailyConsensusSummary.Aggregate(consensus, nowMorning, TimeZoneInfo.Utc);

        var day = Assert.Single(summaries);
        Assert.Equal(61, day.WeatherCode);
    }

    [Fact(Timeout = 5000)]
    public void Spread_is_average_of_temperature_spreads()
    {
        var consensus = BuildConsensusWithSpread(
            new Dictionary<string, (double Median, double Spread)>
            {
                ["h0"] = (20, 2.0),
                ["h1"] = (22, 3.0),
                ["h2"] = (24, 4.0),
                ["h3"] = (23, 3.0),
                ["h4"] = (21, 2.0),
            });

        var summaries = DailyConsensusSummary.Aggregate(consensus, Now, TimeZoneInfo.Utc);

        var day = Assert.Single(summaries);
        Assert.Equal(2.8, day.Spread);
    }

    [Fact(Timeout = 5000)]
    public void Agreement_is_average_of_temperature_agreements()
    {
        var consensus = BuildConsensusWithAgreement(
            new Dictionary<string, (double Median, double Agreement)>
            {
                ["h0"] = (20, 0.8),
                ["h1"] = (22, 0.9),
                ["h2"] = (24, 0.7),
                ["h3"] = (23, 0.85),
                ["h4"] = (21, 0.9),
            });

        var summaries = DailyConsensusSummary.Aggregate(consensus, Now, TimeZoneInfo.Utc);

        var day = Assert.Single(summaries);
        Assert.Equal(0.83, day.Agreement);
    }

    [Fact(Timeout = 5000)]
    public void Available_models_is_minimum_across_hours()
    {
        var horizons = new Dictionary<string, HorizonConsensus>
        {
            ["h0"] = MakeHorizon(20, modelCount: 8),
            ["h1"] = MakeHorizon(22, modelCount: 7),
            ["h2"] = MakeHorizon(24, modelCount: 6),
            ["h3"] = MakeHorizon(23, modelCount: 7),
            ["h4"] = MakeHorizon(21, modelCount: 8),
        };

        var consensus = new ConsensusResult(
            [new ParameterConsensus(Temperature, horizons)]);

        var summaries = DailyConsensusSummary.Aggregate(consensus, Now, TimeZoneInfo.Utc);

        var day = Assert.Single(summaries);
        Assert.Equal(6, day.AvailableModels);
    }

    [Fact(Timeout = 5000)]
    public void Missing_parameter_yields_null()
    {
        var consensus = BuildConsensus(Temperature, new Dictionary<string, double>
        {
            ["h0"] = 20,
            ["h1"] = 25,
        });

        var summaries = DailyConsensusSummary.Aggregate(consensus, Now, TimeZoneInfo.Utc);

        var day = Assert.Single(summaries);
        Assert.Null(day.PrecipitationSum);
        Assert.Null(day.WindSpeedMax);
        Assert.Null(day.WeatherCode);
    }

    [Fact(Timeout = 5000)]
    public void Horizons_grouped_by_timezone_calendar_day()
    {
        var nowEvening = new DateTimeOffset(2026, 7, 29, 20, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(Temperature, new Dictionary<string, double>
        {
            ["h0"] = 25,
            ["h1"] = 24,
            ["h2"] = 23,
            ["h3"] = 22,
            ["h4"] = 21,
            ["h5"] = 20,
            ["h6"] = 19,
            ["h7"] = 22,
            ["h8"] = 25,
        });

        var summaries = DailyConsensusSummary.Aggregate(consensus, nowEvening, Cest);

        Assert.Equal(2, summaries.Count);
        var today = summaries.First(s => s.Date == new DateOnly(2026, 7, 29));
        var tomorrow = summaries.First(s => s.Date == new DateOnly(2026, 7, 30));
        Assert.Equal(25, today.TemperatureMax);
        Assert.Equal(24, today.TemperatureMin);
        Assert.Equal(25, tomorrow.TemperatureMax);
    }

    [Fact(Timeout = 5000)]
    public void Empty_consensus_returns_empty_summaries()
    {
        var consensus = new ConsensusResult([]);

        var summaries = DailyConsensusSummary.Aggregate(consensus, Now, TimeZoneInfo.Utc);

        Assert.Empty(summaries);
    }

    [Fact(Timeout = 5000)]
    public void No_timezone_defaults_to_utc_bucketing()
    {
        var consensus = BuildConsensus(Temperature, new Dictionary<string, double>
        {
            ["h0"] = 20,
            ["h1"] = 25,
        });

        var summaries = DailyConsensusSummary.Aggregate(consensus, Now, TimeZoneInfo.Utc);

        var day = Assert.Single(summaries);
        Assert.Equal(new DateOnly(2026, 7, 29), day.Date);
    }

    private static ConsensusResult BuildConsensus(ParameterDef param, Dictionary<string, double> medians)
    {
        var horizons = medians.ToDictionary(kv => kv.Key, kv => MakeHorizon(kv.Value));
        return new ConsensusResult([new ParameterConsensus(param, horizons)]);
    }

    private static ConsensusResult BuildConsensus(params (ParameterDef Param, Dictionary<string, double> Medians)[] entries)
    {
        var parameters = entries.Select(e =>
            new ParameterConsensus(e.Param, e.Medians.ToDictionary(kv => kv.Key, kv => MakeHorizon(kv.Value)))).ToList();
        return new ConsensusResult(parameters);
    }

    private static ConsensusResult BuildConsensusWithSpread(Dictionary<string, (double Median, double Spread)> values)
    {
        var horizons = values.ToDictionary(
            kv => kv.Key,
            kv => new HorizonConsensus(kv.Value.Median, null, kv.Value.Spread, null, null, null, null,
                [IconD2, Ecmwf]));
        return new ConsensusResult([new ParameterConsensus(Temperature, horizons)]);
    }

    private static ConsensusResult BuildConsensusWithAgreement(Dictionary<string, (double Median, double Agreement)> values)
    {
        var horizons = values.ToDictionary(
            kv => kv.Key,
            kv => new HorizonConsensus(kv.Value.Median, null, null, null, kv.Value.Agreement, null, null,
                [IconD2, Ecmwf]));
        return new ConsensusResult([new ParameterConsensus(Temperature, horizons)]);
    }

    private static HorizonConsensus MakeHorizon(double median, int modelCount = 2)
    {
        var models = Enumerable.Range(0, modelCount).Select(i => new WeatherModel($"model_{i}")).ToList();
        return new HorizonConsensus(median, null, null, null, null, null, null, models);
    }
}
