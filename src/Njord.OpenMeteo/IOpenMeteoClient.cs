using Njord.Messages.Forecast;
using Njord.Model;

namespace Njord.OpenMeteo;

public sealed record OpenMeteoRequest(string Uri, string Location, DomainKind Domain);

public abstract record OpenMeteoFetchResult
{
    public sealed record Success(OpenMeteoResponse Response) : OpenMeteoFetchResult;

    public sealed record Failure(PollFailureReason Reason, string? Detail) : OpenMeteoFetchResult;
}

public interface IOpenMeteoClient
{
    Task<OpenMeteoFetchResult> FetchAsync(OpenMeteoRequest request, CancellationToken cancellationToken);
}
