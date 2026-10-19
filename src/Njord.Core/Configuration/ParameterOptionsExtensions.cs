
namespace Njord.Core.Configuration;

public static class ParameterOptionsExtensions
{
    public static ResolvedParameterSet Resolve(this ParameterOptions parameters) =>
        ParameterSelection.Resolve(parameters.Groups, parameters.Extra, parameters.Exclude);
}
