namespace Njord.Messages.Pipeline;

// Messages for a poll worker carry the worker's entity id (the location name, configured spelling).
public interface IWithLocationId
{
    string LocationName { get; }
}
