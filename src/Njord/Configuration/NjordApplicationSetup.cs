using Njord.ServiceDefaults;
using Servus.Core.Application.Startup;

namespace Njord.Configuration;

public sealed class NjordApplicationSetup : ApplicationSetupContainer<WebApplication>
{
    protected override void SetupApplication(WebApplication app)
    {
        app.MapDefaultEndpoints();
    }
}
