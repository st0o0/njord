using Njord.Model;
namespace Njord.Forecast;

public sealed record HorizonValue(ParameterDef Parameter, ForecastValue Value);
