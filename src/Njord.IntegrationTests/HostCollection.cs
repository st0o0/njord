namespace Njord.IntegrationTests;

// The host binds the Akka remoting port 2552, so every test class that starts a host runs in this collection,
// one after another.
[CollectionDefinition(Name)]
public sealed class HostCollection
{
    public const string Name = "host";
}
