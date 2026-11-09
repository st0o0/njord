using Microsoft.AspNetCore.Builder;

namespace Njord.Grpc;

public static class GrpcEndpointExtensions
{
    public static WebApplication MapNjordGrpc(this WebApplication app)
    {
        app.MapGrpcService<NjordGrpcService>();
        return app;
    }
}
