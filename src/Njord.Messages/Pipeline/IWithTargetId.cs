namespace Njord.Messages.Pipeline;

// Kept for backward compatibility during transition. New code should use IWithLocationId.
public interface IWithTargetId
{
    string TargetId { get; }
}
