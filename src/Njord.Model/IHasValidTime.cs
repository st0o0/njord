namespace Njord.Model;

public interface IHasValidTime
{
    DateTimeOffset ValidFrom { get; }
}
