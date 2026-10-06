namespace Njord.Persistence.Tests;

public sealed class ForecastHistoryDtoRoundtripSpec
{
    [Fact]
    public void ForecastHistorySnapshotDto_roundtrip()
    {
        var dto = new ForecastHistorySnapshotDto
        {
            Version = 1,
            RetentionDays = 7,
            Records =
            [
                new ForecastRecordDto
                {
                    Version = 1,
                    TimestampUtcTicks = 638_600_000_000_000_000L,
                    Location = "lucerne",
                    ModelValues = new Dictionary<string, Dictionary<string, double?>>
                    {
                        ["icon_d2"] = new()
                        {
                            ["temperature_2m"] = 18.5,
                            ["precipitation"] = 0.0,
                        },
                    },
                    ConsensusValues = new Dictionary<string, double?>
                    {
                        ["temperature_2m"] = 18.3,
                    },
                },
            ],
        };

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Version, result.Version);
        Assert.Equal(7, result.RetentionDays);
        var record = Assert.Single(result.Records);
        Assert.Equal(1, record.Version);
        Assert.Equal(dto.Records[0].TimestampUtcTicks, record.TimestampUtcTicks);
        Assert.Equal("lucerne", record.Location);
        Assert.Equal(18.5, record.ModelValues["icon_d2"]["temperature_2m"]);
        Assert.Equal(18.3, record.ConsensusValues["temperature_2m"]);
    }

    [Fact]
    public void ForecastRecordDto_roundtrip()
    {
        var dto = new ForecastRecordDto
        {
            Version = 1,
            TimestampUtcTicks = 638_600_000_000_000_000L,
            Location = "zurich",
            ModelValues = new Dictionary<string, Dictionary<string, double?>>
            {
                ["ecmwf_ifs025"] = new()
                {
                    ["wind_speed_10m"] = 3.2,
                    ["relative_humidity_2m"] = null,
                },
            },
            ConsensusValues = new Dictionary<string, double?>
            {
                ["wind_speed_10m"] = 3.0,
            },
        };

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Version, result.Version);
        Assert.Equal(dto.TimestampUtcTicks, result.TimestampUtcTicks);
        Assert.Equal("zurich", result.Location);
        Assert.Equal(3.2, result.ModelValues["ecmwf_ifs025"]["wind_speed_10m"]);
        Assert.Null(result.ModelValues["ecmwf_ifs025"]["relative_humidity_2m"]);
        Assert.Equal(3.0, result.ConsensusValues["wind_speed_10m"]);
    }
}
