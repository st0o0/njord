using Njord.Core.Domain;
using Njord.Forecast.Flood;

namespace Njord.Advisories.Tests;

public sealed class FloodTimelineRequirementsSpec
{
    [Fact]
    public void The_timeline_handler_requires_discharge_and_the_warning_level()
    {
        Assert.Equal(
            new DomainRequirements(Flood: FloodParams.Discharge | FloodParams.DischargeP75),
            new FloodTimelineHandler().Feature.Requirements);
    }
}
