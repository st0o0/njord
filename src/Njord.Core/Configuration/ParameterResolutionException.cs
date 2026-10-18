namespace Njord.Core.Configuration;

public sealed class ParameterResolutionException(IReadOnlyList<string> errors) : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
