using Microsoft.Extensions.DependencyInjection;

namespace Njord.Grpc;

public static class GrpcServiceCollectionExtensions
{
    public static IServiceCollection AddNjordGrpc(this IServiceCollection services)
    {
        services.AddGrpc();
        return services;
    }
}
