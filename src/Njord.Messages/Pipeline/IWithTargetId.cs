namespace Njord.Messages.Pipeline;

// Messages for a poll worker carry the worker's entity id ("{location}|{model}", configured spelling).
public interface IWithTargetId
{
    string TargetId { get; }
}
