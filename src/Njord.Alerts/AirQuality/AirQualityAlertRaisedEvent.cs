using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.AirQuality;

public sealed record AirQualityAlertRaisedEvent(Location Location, AirQualityAlert Alert, DateTimeOffset Timestamp)
    : EnrichmentEvent(Location);
