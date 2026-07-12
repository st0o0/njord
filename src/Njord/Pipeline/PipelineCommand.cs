using Njord.Domain;

namespace Njord.Pipeline;

public abstract record PipelineCommand
{
    private PipelineCommand() { }

    public sealed record RefreshLocation(string Location) : PipelineCommand;

    public sealed record RefreshModel(string Location, WeatherModel Model) : PipelineCommand;
}
