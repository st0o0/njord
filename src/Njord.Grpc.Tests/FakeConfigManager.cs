using Akka.Actor;
using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Messages.Config;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

// Answers the configuration queries from a fixed configuration, through the production mapping.
internal sealed class FakeConfigManager : ReceiveActor
{
    public FakeConfigManager(ConfigView config, AnalysisConfigView? analysis = null)
    {
        var sections = analysis ?? TestAnalysisConfigView.Create();
        Receive<QueryCatalog>(_ => Sender.Tell(config.Scope.ToCatalog()));
        Receive<QueryConfig>(_ => Sender.Tell(new ConfigResult(config.ToEffectiveConfig(sections))));
    }
}
