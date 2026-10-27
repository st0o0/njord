namespace Njord.Core;

public interface IHasValidTime
{
    DateTimeOffset ValidFrom { get; }
}
