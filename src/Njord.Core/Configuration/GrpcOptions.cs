namespace Njord.Core.Configuration;

public sealed class GrpcOptions
{
    public const string SectionName = "Njord:Grpc";

    public int Port { get; set; } = 8081;
}
