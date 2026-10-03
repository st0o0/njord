using Microsoft.AspNetCore.Builder;

namespace Njord.Grpc;

public static class GrpcEndpointExtensions
{
    public static WebApplication MapNjordGrpc(this WebApplication app)
    {
        app.MapGrpcService<WeatherGrpcService>();
        app.MapGrpcService<AdminGrpcService>();
        app.MapGrpcService<OpsGrpcService>();
        app.MapGrpcService<SensorGrpcService>();
        return app;
    }
}
