using Njord.Messages.Forecast;
using Njord.Model;

namespace Njord.OpenMeteo;

// Uniform view of the per-endpoint fetch results: a forecast on success, a reason and detail on failure.
public interface IFetchOutcome
{
    IDomainForecast? Forecast { get; }

    PollFailureReason Reason { get; }

    string? Detail { get; }
}
