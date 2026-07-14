using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Domain.Weather;
using Njord.Egress;

namespace Njord.Enrichment;

public interface IActorEnrichment : IEnrichmentFeature
{
    void Materialize(
        Source<ModelSnapshot, NotUsed> source,
        Sink<EgressEvent, NotUsed> sink,
        IMaterializer mat,
        IUntypedActorContext context);
}
