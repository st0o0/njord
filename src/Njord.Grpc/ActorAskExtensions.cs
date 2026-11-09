using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Status = Grpc.Core.Status;

namespace Njord.Grpc;

internal static class ActorAskExtensions
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public static async Task<TResponse> AskAsync<TKey, TResponse>(
        this ActorRegistry registry,
        object message,
        ServerCallContext context,
        TimeSpan? timeout = null)
    {
        if (!registry.TryGet<TKey>(out var actor))
        {
            throw new RpcException(new Status(StatusCode.Unavailable, $"{typeof(TKey).Name} is not available yet"));
        }

        var limit = timeout ?? DefaultTimeout;
        try
        {
            return await actor.Ask<TResponse>(message, limit, context.CancellationToken).ConfigureAwait(false);
        }
        catch (AskTimeoutException)
        {
            throw new RpcException(new Status(StatusCode.DeadlineExceeded,
                $"{typeof(TKey).Name} did not answer within {limit.TotalSeconds:0.#} s"));
        }
    }
}
