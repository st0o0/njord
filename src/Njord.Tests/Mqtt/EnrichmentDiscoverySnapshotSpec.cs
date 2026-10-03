using static VerifyXunit.Verifier;

namespace Njord.Tests.Mqtt;

public sealed class EnrichmentDiscoverySnapshotSpec
{
    private static Task VerifyDiscovery(string typeName)
    {
        var options = EnrichmentGoldenMasterFixtures.Options();
        var presenter = EnrichmentGoldenMasterFixtures.Presenter(typeName);
        var payload = presenter.BuildDiscoveryPayload(
            EnrichmentGoldenMasterFixtures.Context(options), EnrichmentGoldenMasterFixtures.Location);

        return Verify(EnrichmentGoldenMasterFixtures.RenderDiscovery(
            presenter.DeviceId(EnrichmentGoldenMasterFixtures.Location), payload));
    }

    [Fact]
    public Task Alerts_discovery_payload_matches_golden_master() => VerifyDiscovery("alerts");

    [Fact]
    public Task Derived_discovery_payload_matches_golden_master() => VerifyDiscovery("derived");

    [Fact]
    public Task Trends_discovery_payload_matches_golden_master() => VerifyDiscovery("trends");

    [Fact]
    public Task Indices_discovery_payload_matches_golden_master() => VerifyDiscovery("indices");

    [Fact]
    public Task History_discovery_payload_matches_golden_master() => VerifyDiscovery("history");
}
