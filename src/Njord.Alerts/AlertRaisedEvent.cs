using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts;

public sealed record AlertRaisedEvent(Location Location, Alert Alert, DateTimeOffset Timestamp) : EnrichmentEvent(Location);
