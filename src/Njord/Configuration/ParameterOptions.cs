namespace Njord.Configuration;

public sealed class ParameterOptions
{
    public IList<string> Groups { get; set; } = ["Weather"];
    public IList<string> Extra { get; set; } = [];
    public IList<string> Exclude { get; set; } = [];
}
