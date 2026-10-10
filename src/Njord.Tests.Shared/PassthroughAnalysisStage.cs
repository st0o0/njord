using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;
using Njord.Core.Pipeline;
using Njord.Messages.Egress;
using Njord.Weather;

namespace Njord.Tests.Shared;

public sealed class PassthroughAnalysisStage : IAnalysisStage
{
    public Flow<ModelForecast, IReadOnlyList<PipelineEvent>, NotUsed> CreateFlow(IUntypedActorContext context) =>
        Flow.Create<ModelForecast>()
            .Select(f => (IReadOnlyList<PipelineEvent>)
                new[] { new PipelineEvent.ModelForecastUpdate(f.Location, f.Model, f) });
}
