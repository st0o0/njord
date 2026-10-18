using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Weather;

namespace Njord.Alerts.Rules;

public sealed class IceRule : IAlertRule
{
    public AlertType Type => AlertType.Ice;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        var rain = consensus.Hourly.Find(ParameterRegistry.Rain);
        var temperature = consensus.Hourly.Find(ParameterRegistry.Temperature2m);
        if (rain is null || temperature is null)
        {
            return Alert.None(Type);
        }

        var soil = consensus.Hourly.Find(ParameterRegistry.SoilTemperature0cm);
        var iceThreshold = settings.IceThreshold;
        var rainHours = 0;
        var minTemp = double.MaxValue;
        var iceAgreements = new List<double>();
        var soilFrozen = false;

        foreach (var horizon in AlertWindow.HourlyHorizons)
        {
            if (rain.At(horizon) is not { } rainHc || temperature.At(horizon) is not { } tempHc)
            {
                continue;
            }

            if (rainHc.Median is > 0 && tempHc.Median is { } tempVal && tempVal <= iceThreshold)
            {
                rainHours++;
                if (tempVal < minTemp)
                {
                    minTemp = tempVal;
                }

                if (tempHc.Agreement is { } agreement)
                {
                    iceAgreements.Add(agreement);
                }

                if (soil.MedianAt(horizon) is <= 0)
                {
                    soilFrozen = true;
                }
            }
        }

        if (rainHours == 0)
        {
            return Alert.None(Type);
        }

        var confidence = iceAgreements.Count > 0 ? Math.Round(iceAgreements.Average(), 3) : 0.0;
        var severity = (minTemp, soilFrozen) switch
        {
            ( <= 0, true) => AlertSeverity.Red,
            ( <= 0, false) => AlertSeverity.Orange,
            _ => AlertSeverity.Yellow,
        };

        var details = new IceDetails(ExpectedLow: Math.Round(minTemp, 1), RainHours: rainHours, SoilFrozen: soilFrozen);

        return new Alert(Type, severity, confidence, details,
            TriggerValue: Math.Round(minTemp, 1),
            Threshold: iceThreshold);
    }
}
