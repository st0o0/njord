using Njord.Messages.Config;

namespace Njord.Core.Configuration;

public sealed record AnalysisUpdate(EnrichmentSection Section, string Prefix, SectionUpdate Update);
