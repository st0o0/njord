using Akka.Hosting;
using Akka.Persistence.Sqlite;
using Microsoft.Extensions.Options;
using Njord.Egress;
using Njord.Enrichment;
using Njord.Pipeline;
using Servus.Akka;
using Servus.Akka.Startup;

namespace Njord.Configuration;

public sealed class NjordActorSystemSetup : ActorSystemSetupContainer
{
    protected override string GetActorSystemName() => "njord";

    protected override void BuildSystem(AkkaConfigurationBuilder builder, IServiceProvider serviceProvider)
    {
        var njordOptions = serviceProvider.GetRequiredService<IOptions<NjordOptions>>().Value;
        var persistencePath = njordOptions.PersistencePath;

        builder
            .AddHocon(SqlitePersistence.DefaultConfiguration(), HoconAddMode.Prepend)
            .AddHocon($$"""
                akka.persistence {
                    journal {
                        plugin = "akka.persistence.journal.sqlite"
                        sqlite {
                            connection-string = "Data Source={{persistencePath}}"
                            auto-initialize = true
                        }
                    }
                    snapshot-store {
                        plugin = "akka.persistence.snapshot-store.sqlite"
                        sqlite {
                            connection-string = "Data Source={{persistencePath}}"
                            auto-initialize = true
                        }
                    }
                }
                """, HoconAddMode.Prepend)
            .WithResolvableActors(r =>
            {
                r.Register<MqttEgressActor>("mqtt-egress");
                r.Register<PipelineActor>("pipeline");
                r.Register<SchedulerActor>("scheduler");
                r.Register<EnrichmentActor>("enrichment");
            });
    }
}
