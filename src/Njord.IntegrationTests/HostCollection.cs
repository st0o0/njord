namespace Njord.IntegrationTests;

// Each host starts an Akka actor system; tests in this collection run one after another to avoid interference.
[CollectionDefinition(Name)]
public sealed class HostCollection
{
    public const string Name = "host";
}
