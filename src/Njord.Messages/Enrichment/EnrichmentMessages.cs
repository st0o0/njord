using Njord.Domain.Weather;

namespace Njord.Messages.Enrichment;

public sealed record RecordSnapshot(string Location, ModelSnapshot Snapshot) : IWithLocation;

public sealed record QueryHistory(string Location) : IWithLocation;
