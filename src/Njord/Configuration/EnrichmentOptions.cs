namespace Njord.Configuration;

public sealed class EnrichmentOptions
{
    public ConsensusOptions Consensus { get; set; } = new();
    public AlertThresholdOptions Alerts { get; set; } = new();
    public DerivedOptions Derived { get; set; } = new();
    public TrendOptions Trends { get; set; } = new();
    public IndexOptions Indices { get; set; } = new();
    public EnergyOptions Energy { get; set; } = new();
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

public sealed class EnergyOptions
{
    public bool Enabled { get; set; } = false;
    public double FlowTemp { get; set; } = 35.0;
    public double CarnotEfficiency { get; set; } = 0.45;
    public double HeatingBaseTemp { get; set; } = 18.0;
    public int CopOptimalHours { get; set; } = 3;
    public double IndoorTemp { get; set; } = 22.0;
}

public sealed class IndexOptions
{
    public bool Enabled { get; set; } = false;
    public double HeatingBaseTemp { get; set; } = 18.0;
    public double CoolingBaseTemp { get; set; } = 24.0;
    public double IndoorTemp { get; set; } = 22.0;
}
