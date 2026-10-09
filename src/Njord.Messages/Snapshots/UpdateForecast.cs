using Njord.Weather;

namespace Njord.Messages.Snapshots;

public sealed record UpdateForecast(string Location, WeatherModel Model, ModelForecast Forecast) : IWithModelKey
{
    string IWithModelKey.ModelId => Model.Id;
}
