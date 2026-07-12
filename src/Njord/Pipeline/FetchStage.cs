using Akka;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Streams.Supervision;
using Njord.Domain;
using Njord.Ingest;

namespace Njord.Pipeline;

public static class FetchStage
{
    private const int DefaultParallelism = 8;

    private static readonly Decider ResumeOnTransient = cause => Directive.Resume;

    public static Flow<WeightedTarget, FetchOutcome, NotUsed> Create(
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        int parallelism = DefaultParallelism)
    {
        return Flow.Create<WeightedTarget>()
            .SelectAsyncUnordered(parallelism, async target =>
            {
                var cycle = CycleId.From(timeProvider);
                return await client.FetchAsync(target.Location, target.Model, cycle, CancellationToken.None);
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(ResumeOnTransient));
    }
}
