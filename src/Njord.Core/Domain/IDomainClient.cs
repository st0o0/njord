namespace Njord.Core.Domain;

public interface IDomainClient
{
    DomainKind Domain { get; }

    int CallWeight { get; }

    Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken);
}
