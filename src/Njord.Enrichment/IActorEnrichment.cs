using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;
using Njord.Messages.Egress;
using Njord.Weather;

namespace Njord.Enrichment;

public interface IActorEnrichment : IEnrichmentFeature
{
    Flow<ModelSnapshot, EgressEvent, NotUsed> CreateFlow(IUntypedActorContext context);
}
