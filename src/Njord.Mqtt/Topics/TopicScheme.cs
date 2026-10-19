using Njord.Model;

namespace Njord.Mqtt.Topics;

public static class TopicScheme
{
    public static string AvailabilityTopic(string baseTopic)
        => $"{baseTopic}/status";

    public static string DomainTopic(string baseTopic, string location, DomainKind domain, string parameter)
        => $"{baseTopic}/{Slug(location)}/{domain.ToTopicSegment()}/{parameter}";

    public static string EnrichmentDeviceId(string location, string typeName)
        => $"njord_{Slug(location)}_{typeName}";

    public static string EnrichmentTopic(string baseTopic, string location, string typeName)
        => $"{baseTopic}/{Slug(location)}/{typeName}";

    public static string EnrichmentSubTopic(string baseTopic, string location, string typeName, string sub)
        => $"{baseTopic}/{Slug(location)}/{typeName}/{sub}";

    public static string Slug(string value)
        => TopicSlug.Slug(value);
}
