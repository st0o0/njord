using Microsoft.Extensions.DependencyInjection;

namespace Njord.Grpc;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGrpcConsumer(this IServiceCollection services)
    {
        services.AddGrpc();
        return services;
    }
}
