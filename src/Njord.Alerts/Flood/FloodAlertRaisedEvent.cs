using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Flood;

public sealed record FloodAlertRaisedEvent(Location Location, FloodAlert Alert, DateTimeOffset Timestamp)
    : EnrichmentEvent(Location);
