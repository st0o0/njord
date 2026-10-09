using Njord.Weather;

namespace Njord.History;

public interface IHistoryInput
{
    ModelSnapshot Snapshot { get; }
    string Location { get; }
}
