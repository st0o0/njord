using Grpc.Core;

namespace Njord.Grpc.Tests;

internal sealed class TestServerCallContext : ServerCallContext
{
    private readonly CancellationToken _cancellationToken;

    private TestServerCallContext(CancellationToken cancellationToken) => _cancellationToken = cancellationToken;

    public static TestServerCallContext Create(CancellationToken cancellationToken = default) => new(cancellationToken);

    protected override string MethodCore => "test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "test-peer";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => [];
    protected override CancellationToken CancellationTokenCore => _cancellationToken;
    protected override Metadata ResponseTrailersCore => [];
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => new("test", new Dictionary<string, List<AuthProperty>>());

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => throw new NotImplementedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => Task.CompletedTask;
}
