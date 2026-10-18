using Microsoft.Extensions.Time.Testing;
using Njord.Alerts.Configuration;
using Njord.Consensus;
using Njord.Consensus.Settings;
using Njord.Core.Configuration;
using Njord.Messages.Alerts;
using Njord.Tests.Shared;
using Njord.Model;
using Njord.Forecast;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Alerts.Tests.Rules;

internal static class AlertRuleFixture
{
    public static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    public static readonly AlertSettings Defaults = AppSettingsDefaults.Options<AlertOptions>().ToSettings();

    public static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    public static readonly ParameterDef ApparentTemp = ParameterRegistry.GetByApiName("apparent_temperature")!;
    public static readonly ParameterDef WindGusts = ParameterRegistry.GetByApiName("wind_gusts_10m")!;
    public static readonly ParameterDef Precipitation = ParameterRegistry.GetByApiName("precipitation")!;
    public static readonly ParameterDef UvIndexParam = ParameterRegistry.GetByApiName("uv_index")!;
    public static readonly ParameterDef DewpointParam = ParameterRegistry.GetByApiName("dew_point_2m")!;
    public static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    public static readonly ParameterDef HumidityParam = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
    public static readonly ParameterDef Snowfall = ParameterRegistry.GetByApiName("snowfall")!;
    public static readonly ParameterDef PressureMsl = ParameterRegistry.GetByApiName("pressure_msl")!;
    public static readonly ParameterDef Cape = ParameterRegistry.GetByApiName("cape")!;
    public static readonly ParameterDef RainParam = ParameterRegistry.GetByApiName("rain")!;
    public static readonly ParameterDef SoilTemp0cm = ParameterRegistry.GetByApiName("soil_temperature_0cm")!;
    public static readonly ParameterDef VisibilityParam = ParameterRegistry.GetByApiName("visibility")!;
    public static readonly ParameterDef IsDayParam = ParameterRegistry.GetByApiName("is_day")!;
    public static readonly ParameterDef DailyPrecipSum = ParameterRegistry.GetByApiName("precipitation_sum")!;
    public static readonly ParameterDef DailyUvMax = ParameterRegistry.GetByApiName("uv_index_max")!;
    public static readonly ParameterDef DailySnowfallSum = ParameterRegistry.GetByApiName("snowfall_sum")!;

    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ResolvedParameterSet Parameters = ParameterSelection.Resolve(["Weather", "Solar", "Soil"], [], []);

    public static LocationConsensus ToConsensus(ModelForecasts snap) =>
        new ConsensusBuilder(Parameters, Time).Create(snap, "lucerne", ConsensusSettings.Default);

    public static LocationConsensus ConsensusOf(params WeatherForecast[] forecasts) => ToConsensus(ForecastsWith(forecasts));

    public static WeatherForecast MakeForecast(WeatherModel model, params (ParameterDef Param, double Value)[] hourlyValues) =>
        MakeForecast(model, _ => hourlyValues);

    public static WeatherForecast MakeForecast(WeatherModel model, Func<int, (ParameterDef Param, double Value)[]> hourlyValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 24; h++)
        {
            var values = new Dictionary<ParameterDef, ForecastValue>();
            foreach (var (param, value) in hourlyValues(h))
            {
                values[param] = value;
            }

            points.Add(new ForecastPoint(T0.AddHours(h), values));
        }

        return new WeatherForecast(model, "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    public static LocationConsensus Identical(int models, Func<int, (ParameterDef Param, double Value)[]> hourlyValues) =>
        ConsensusOf([.. Enumerable.Range(0, models).Select(i => MakeForecast(new WeatherModel($"m{i}"), hourlyValues))]);

    public static WeatherForecast MakeForecastOver(
        WeatherModel model, int hours, Func<int, (ParameterDef Param, double Value)[]> hourlyValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < hours; h++)
        {
            var values = new Dictionary<ParameterDef, ForecastValue>();
            foreach (var (param, value) in hourlyValues(h))
            {
                values[param] = value;
            }

            points.Add(new ForecastPoint(T0.AddHours(h), values));
        }

        return new WeatherForecast(model, "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    public static WeatherForecast MakeForecastWithDailySeries(
        WeatherModel model,
        (ParameterDef Param, double[] Values)[] dailySeries)
    {
        var today = DateOnly.FromDateTime(T0.UtcDateTime);
        var days = dailySeries.Max(series => series.Values.Length);
        var points = Enumerable.Range(0, days).Select(day =>
            new DailyForecastPoint(
                today.AddDays(day),
                dailySeries.ToDictionary(series => series.Param, series => (ForecastValue)series.Values[day]))).ToList();

        return MakeForecast(model) with { Daily = new DailyForecastSeries(points) };
    }

    public static WeatherForecast MakeForecastWithDaily(
        WeatherModel model,
        (ParameterDef Param, double Value)[] hourlyValues,
        (ParameterDef Param, double Value)[] dailyValues)
    {
        var today = DateOnly.FromDateTime(T0.UtcDateTime);
        var hourly = MakeForecast(model, hourlyValues);

        var numeric = new Dictionary<ParameterDef, ForecastValue>();
        foreach (var (param, value) in dailyValues)
        {
            numeric[param] = value;
        }

        var dailyPoint = new DailyForecastPoint(today, numeric);
        return hourly with { Daily = new DailyForecastSeries([dailyPoint]) };
    }

    public static ModelForecasts ForecastsWith(params WeatherForecast[] forecasts)
    {
        var snap = ModelForecasts.Empty;
        foreach (var f in forecasts)
        {
            snap = snap.Update(f);
        }

        return snap;
    }
}
