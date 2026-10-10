using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;
using Njord.Messages.Egress;
using Njord.Weather;

namespace Njord.Core.Pipeline;

public interface IAnalysisStage
{
    Flow<ModelForecast, IReadOnlyList<PipelineEvent>, NotUsed> CreateFlow(IUntypedActorContext context);
}
