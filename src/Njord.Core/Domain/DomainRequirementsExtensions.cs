namespace Njord.Core.Domain;

public static class DomainRequirementsExtensions
{
    public static IReadOnlyList<DomainKind> RequiredDomains(this DomainRequirements requirements) =>
        [.. Enum.GetValues<DomainKind>().Where(requirements.Requires).Order()];
}
