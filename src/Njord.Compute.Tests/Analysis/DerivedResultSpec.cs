using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Settings;
using Njord.Weather;

namespace Njord.Compute.Tests.Analysis;

public sealed class DerivedResultSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef DewPoint = ParameterRegistry.GetByApiName("dew_point_2m")!;
    private static readonly ParameterDef WeatherCode = ParameterRegistry.GetByApiName("weather_code")!;
    private static readonly ParameterDef PressureMsl = ParameterRegistry.GetByApiName("pressure_msl")!;
    private static readonly ParameterDef SurfacePressure = ParameterRegistry.GetByApiName("surface_pressure")!;
    private static readonly ParameterDef SunshineDuration = ParameterRegistry.GetByApiName("sunshine_duration")!;
    private static readonly ParameterDef IsDay = ParameterRegistry.GetByApiName("is_day")!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(
        ["Weather", "Solar"], [], []);

    private static Forecast MakeForecast(
        WeatherModel model, params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 72; h++)
        {
            var values = new Dictionary<ParameterDef, double?>();
            foreach (var (param, value) in hourlyValues)
            {
                values[param] = value;
            }

            points.Add(new ForecastPoint(T0.AddHours(h), values));
        }
        return new Forecast(model, "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private static ModelSnapshot SnapshotWith(params Forecast[] forecasts)
    {
        var snap = ModelSnapshot.Empty;
        foreach (var f in forecasts)
        {
            snap = snap.Update(f);
        }

        return snap;
    }

    [Fact]
    public void Compute_produces_horizon_and_scalar_results()
    {
        var snap = SnapshotWith(
            MakeForecast(new WeatherModel("m1"),
                (Temperature, 5.0), (WindSpeed, 8.0), (DewPoint, 3.0),
                (WeatherCode, 3.0), (PressureMsl, 1020.0), (SurfacePressure, 1015.0),
                (SunshineDuration, 3600.0), (IsDay, 1.0)),
            MakeForecast(new WeatherModel("m2"),
                (Temperature, 7.0), (WindSpeed, 10.0), (DewPoint, 4.0),
                (WeatherCode, 3.0), (PressureMsl, 1021.0), (SurfacePressure, 1016.0),
                (SunshineDuration, 3600.0), (IsDay, 1.0)));

        var consensus = new ConsensusBuilder(Parameters, Time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = new DerivedResultComputer(Parameters).Compute(consensus, new DerivedSettings([3, 6]));

        Assert.Equal(new LocationId("lucerne"), result.Location);
        Assert.Equal(2, result.ByHorizon.Count);
        Assert.Contains(Horizon.Hours(3), result.ByHorizon.Keys);
        Assert.Contains(Horizon.Hours(6), result.ByHorizon.Keys);

        var h3 = result.ByHorizon[Horizon.Hours(3)];
        Assert.NotNull(h3.Beaufort);
        Assert.Equal(WeatherCondition.Overcast, h3.Condition);
        Assert.Equal(DewPointComfort.Dry, h3.DewPointComfort);
    }

}
