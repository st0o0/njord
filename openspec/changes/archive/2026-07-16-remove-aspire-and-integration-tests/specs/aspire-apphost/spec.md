## REMOVED Requirements

### Requirement: Aspire AppHost orchestrates local development
**Reason**: Aspire AppHost and container-based orchestration removed to simplify the solution. The service runs standalone via `dotnet run` with `Mqtt:Enabled = false` for Docker-free development.
**Migration**: Use `dotnet run --project Njord/Njord.csproj` directly. Configure MQTT host via environment variables or appsettings when a broker is available.
