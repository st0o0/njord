namespace Njord.Forecast;

public interface IHasValidTime
{
    DateTimeOffset ValidFrom { get; }
}
