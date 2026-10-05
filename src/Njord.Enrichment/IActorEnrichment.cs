using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;
using Njord.Domain.Weather;
using Njord.Messages.Egress;

namespace Njord.Enrichment;

public interface IActorEnrichment : IEnrichmentFeature
{
    Flow<ModelSnapshot, EgressEvent, NotUsed> CreateFlow(IUntypedActorContext context);
}
