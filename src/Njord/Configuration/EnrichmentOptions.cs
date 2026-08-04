namespace Njord.Configuration;

public sealed class EnrichmentOptions
{
    public ConsensusOptions Consensus { get; set; } = new();
    public AlertThresholdOptions Alerts { get; set; } = new();
    public DerivedOptions Derived { get; set; } = new();
    public TrendOptions Trends { get; set; } = new();
    public IndexOptions Indices { get; set; } = new();
    public HistoryOptions History { get; set; } = new();
}

public sealed class ConsensusOptions
{
    public bool Enabled { get; set; } = true;
    public string Method { get; set; } = "Median";
    public double TrimPercent { get; set; } = 0.1;
}

public sealed class AlertThresholdOptions
{
    public bool Enabled { get; set; } = true;
    public double FrostThreshold { get; set; } = 0.0;
    public double[] HeatThresholds { get; set; } = [30, 35, 40];
    public double StormGustThreshold { get; set; } = 16.7;
    public double HeavyRainHourlyThreshold { get; set; } = 10.0;
    public double HeavyRainDailyThreshold { get; set; } = 25.0;
    public double PressureDropThreshold { get; set; } = 5.0;
    public double CapeThreshold { get; set; } = 1000.0;
    public double ThunderstormPrecipThreshold { get; set; } = 5.0;
    public double ThunderstormGustThreshold { get; set; } = 15.0;
}

public sealed class DerivedOptions
{
    public bool Enabled { get; set; } = true;
}

public sealed class TrendOptions
{
    public bool Enabled { get; set; } = false;
}

public sealed class HistoryOptions
{
    public bool Enabled { get; set; } = false;
    public int RetentionDays { get; set; } = 30;
    public int MinSampleSize { get; set; } = 48;
    public int SnapshotInterval { get; set; } = 100;
}

public sealed class IndexOptions
{
    public bool Enabled { get; set; } = false;
    public IndexPreferences Preferences { get; set; } = new();
    public IDictionary<string, IndexPreferences> ScoreOverrides { get; set; } = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase);
    public IList<LocationIndexOverride> LocationOverrides { get; set; } = [];
}

public sealed class IndexPreferences
{
    public double? IdealOutdoorTemp { get; set; }
    public double? RunningIdealTempLow { get; set; }
    public double? RunningIdealTempHigh { get; set; }
    public double? BbqMinTemp { get; set; }
    public double? BbqIdealWindLow { get; set; }
    public double? BbqIdealWindHigh { get; set; }
    public double? IndoorTemp { get; set; }
    public double? HeatSensitivity { get; set; }
    public double? HumiditySensitivity { get; set; }
    public double? WindSensitivity { get; set; }
    public double? RainSensitivity { get; set; }
}

public sealed class LocationIndexOverride
{
    public string Location { get; set; } = string.Empty;
    public IndexPreferences Preferences { get; set; } = new();
    public IDictionary<string, IndexPreferences> ScoreOverrides { get; set; } = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase);
}
