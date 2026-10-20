namespace Njord.OpenMeteo;

public interface ISeasonalClient
{
    Task<SeasonalResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, CancellationToken cancellationToken);
}
