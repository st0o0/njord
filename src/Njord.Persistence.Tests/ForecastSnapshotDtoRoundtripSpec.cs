using Njord.Persistence;
using Njord.Tests.Shared;

namespace Njord.Persistence.Tests;

public sealed class ForecastSnapshotDtoRoundtripSpec
{
    [Fact]
    public void ForecastSnapshotDto_roundtrip()
    {
        var dto = new ForecastSnapshotDto
        {
            Version = 1,
            Forecasts = new Dictionary<string, ModelForecastDto>
            {
                ["lucerne/icon_d2"] = new()
                {
                    ModelId = "icon_d2",
                    Location = "lucerne",
                    CycleUtcTicks = 638_600_000_000_000_000L,
                    Hourly =
                    [
                        new ForecastPointDto
                        {
                            ValidAtUtcTicks = 638_600_000_000_000_000L,
                            Values = new Dictionary<string, double?>
                            {
                                ["temperature_2m"] = 18.5,
                                ["precipitation"] = null,
                            },
                        },
                    ],
                    Daily =
                    [
                        new DailyForecastPointDto
                        {
                            Date = "2026-07-12",
                            NumericValues = new Dictionary<string, double?>
                            {
                                ["temperature_2m_max"] = 24.0,
                                ["temperature_2m_min"] = 14.0,
                            },
                            MetaValues = new Dictionary<string, string?>
                            {
                                ["sunrise"] = "05:30",
                                ["sunset"] = "21:15",
                            },
                        },
                    ],
                },
            },
        };

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Version, result.Version);
        var forecast = Assert.Single(result.Forecasts);
        Assert.Equal("lucerne/icon_d2", forecast.Key);
        Assert.Equal("icon_d2", forecast.Value.ModelId);
        Assert.Equal("lucerne", forecast.Value.Location);
        Assert.Equal(dto.Forecasts["lucerne/icon_d2"].CycleUtcTicks, forecast.Value.CycleUtcTicks);

        var hourly = Assert.Single(forecast.Value.Hourly);
        Assert.Equal(18.5, hourly.Values["temperature_2m"]);
        Assert.Null(hourly.Values["precipitation"]);

        var daily = Assert.Single(forecast.Value.Daily);
        Assert.Equal("2026-07-12", daily.Date);
        Assert.Equal(24.0, daily.NumericValues["temperature_2m_max"]);
        Assert.Equal("05:30", daily.MetaValues["sunrise"]);
    }
}
