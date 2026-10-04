namespace Njord.Messages;

public interface IWithLocation
{
    string Location { get; }
}

public interface IWithModelKey : IWithLocation
{
    string ModelId { get; }
}

public interface IWithEnrichmentKey : IWithLocation
{
    string TypeName { get; }
}
