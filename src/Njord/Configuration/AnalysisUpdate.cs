using Njord.Messages.Config;

namespace Njord.Configuration;

public sealed record AnalysisUpdate(EnrichmentSection Section, string Prefix, SectionUpdate Update);
