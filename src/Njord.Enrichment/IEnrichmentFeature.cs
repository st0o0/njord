namespace Njord.Enrichment;

public interface IEnrichmentFeature
{
    string TypeName { get; }
    bool Enabled { get; }
}
