using Njord.Weather;

namespace Njord.Core.Configuration;

public static class ParameterOptionsExtensions
{
    public static ResolvedParameterSet Resolve(this ParameterOptions parameters) =>
        ParameterRegistry.Resolve(parameters.Groups, parameters.Extra, parameters.Exclude);
}
