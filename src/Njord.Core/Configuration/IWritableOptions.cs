namespace Njord.Core.Configuration;

public interface IWritableOptions<out T> where T : class, new()
{
    T Value { get; }

    T Update(Action<T> applyChanges);
}
