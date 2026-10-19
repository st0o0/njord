namespace Njord.Core.Domain;

public interface IEnrichment<in TInput> where TInput : IEnrichmentInput
{
    IReadOnlyList<EnrichmentEvent> Evaluate(TInput input, TimeProvider time);
}
