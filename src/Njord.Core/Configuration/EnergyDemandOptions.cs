namespace Njord.Core.Configuration;

public sealed class EnergyDemandOptions
{
    public const string SectionName = "Njord:EnergyDemand";

    public double HeatingSetpoint { get; set; } = 20.0;
    public double CoolingSetpoint { get; set; } = 26.0;
}
