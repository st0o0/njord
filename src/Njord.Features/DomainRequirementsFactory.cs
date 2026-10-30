using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Messages.Results;

namespace Njord.Features;

public sealed class DomainRequirementsFactory(IEnumerable<IFeatureRule> rules) : IOptionsFactory<DomainRequirements>
{
    public DomainRequirements Create(string name) =>
        rules
            .Where(r => r.Enabled)
            .Aggregate(new DomainRequirements(), (all, f) => all | f.Descriptor.Requirements)
            .WithBaseParams();
}
