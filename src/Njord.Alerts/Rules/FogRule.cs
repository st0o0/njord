using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Rules;

public sealed class FogRule : IAlertRule
{
    public AlertType Type => AlertType.Fog;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        var temperature = consensus.Hourly.Find(ParameterRegistry.Temperature2m);
        var dewPoint = consensus.Hourly.Find(ParameterRegistry.DewPoint2m);
        var wind = consensus.Hourly.Find(ParameterRegistry.WindSpeed10m);
        var humidity = consensus.Hourly.Find(ParameterRegistry.RelativeHumidity2m);
        if (temperature is null || dewPoint is null || wind is null || humidity is null)
        {
            return Alert.None(Type);
        }

        var fogHours = 0;
        var fogAgreements = new List<double>();
        foreach (var horizon in AlertWindow.HourlyHorizons)
        {
            if (temperature.At(horizon) is not { } t || dewPoint.At(horizon) is not { } d
                || wind.At(horizon) is not { } w || humidity.At(horizon) is not { } h)
            {
                continue;
            }

            if (t.Median is { } tv && d.Median is { } dv && w.Median is { } wv && h.Median is { } hv
                && tv - dv < 2.0 && wv < 3.0 && hv > 90.0)
            {
                fogHours++;
                var agreements = new[] { t.Agreement, d.Agreement, w.Agreement, h.Agreement }.OfType<double>().ToList();
                if (agreements.Count > 0)
                {
                    fogAgreements.Add(agreements.Average());
                }
            }
        }

        var confidence = fogAgreements.Count > 0 ? Math.Round(fogAgreements.Average(), 3) : 0.0;
        var severity = fogHours switch
        {
            >= 1 when fogHours >= settings.FogPersistentHours => AlertSeverity.Orange,
            >= 1 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var details = new FogDetails(FogHours: fogHours, ModelsAgreeing: temperature.ModelsAt(Horizon.Hours(0)));

        return new Alert(Type, severity, confidence, details,
            TriggerValue: fogHours,
            Threshold: 1,
            DurationHours: fogHours > 0 ? fogHours : null);
    }
}
