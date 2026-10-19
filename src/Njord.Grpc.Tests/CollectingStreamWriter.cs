using System.Collections.Concurrent;
using Grpc.Core;

namespace Njord.Grpc.Tests;

public sealed class CollectingStreamWriter<T> : IServerStreamWriter<T>
{
    private readonly ConcurrentQueue<T> _messages = new();

    public WriteOptions? WriteOptions { get; set; }

    public IReadOnlyList<T> Messages => [.. _messages];

    public Task WriteAsync(T message)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task WriteAsync(T message, CancellationToken cancellationToken) => WriteAsync(message);
}
