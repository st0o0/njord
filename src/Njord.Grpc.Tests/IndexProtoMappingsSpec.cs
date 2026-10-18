using Njord.Consensus;
using Njord.Grpc.Mapping;
using Njord.Messages.Derived;
using Njord.Messages.Indices;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Model;
using DomainAlert = Njord.Messages.Alerts.Alert;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainAlertType = Njord.Messages.Alerts.AlertType;
using DomainHorizonConsensus = Njord.Consensus.HorizonConsensus;
using DomainHorizonDerived = Njord.Messages.Derived.HorizonDerived;
using DomainParameterConsensus = Njord.Consensus.ParameterConsensus;
using DomainParameterTrend = Njord.Messages.Trends.ParameterTrend;
using DomainScalarDerived = Njord.Messages.Derived.ScalarDerived;

namespace Njord.Grpc.Tests;

public sealed class IndexProtoMappingsSpec
{
    [Fact]
    public void Indices_should_map_daily_scores_frost_and_vpd()
    {
        var result = new IndexResult(
            Location: new LocationId("lucerne"),
            Days: [new DayScoreSet(0, Laundry: 80, Outdoor: 70, Running: 65, Cycling: 75,
                Bbq: 90, Irrigation: 30, Solar: 85, NightVentilation: 60, HoursIncluded: 14)],
            FrostProtection: new FrostProtectionInfo(HoursUntilFrost: 8, Confidence: 0.7),
            Vpd: new VpdInfo(Category: VpdCategory.Optimal, Vpd: 1.2));

        var update = IndexProtoMappings.ToProto(result);

        Assert.Single(update.Days);
        var d0 = update.Days[0];
        Assert.Equal(0, d0.DayOffset);
        Assert.Equal(80, d0.Laundry);
        Assert.Equal(70, d0.Outdoor);
        Assert.Equal(65, d0.Running);
        Assert.Equal(75, d0.Cycling);
        Assert.Equal(90, d0.Bbq);
        Assert.Equal(30, d0.Irrigation);
        Assert.Equal(85, d0.Solar);
        Assert.Equal(60, d0.NightVentilation);
        Assert.Equal(14, d0.HoursIncluded);
        Assert.NotNull(update.Frost);
        Assert.Equal(8, update.Frost.HoursUntilFrost);
        Assert.Equal(0.7, update.Frost.Confidence);
        Assert.NotNull(update.Vpd);
        Assert.Equal(1.2, update.Vpd.Kpa);
        Assert.Equal(Njord.Grpc.V2.VpdCategory.Optimal, update.Vpd.Category);
    }

    [Fact]
    public void Indices_should_omit_frost_when_null()
    {
        var result = new IndexResult(new LocationId("lucerne"),
            [new DayScoreSet(0, 50, 50, 50, 50, 50, 50, 50, 50, HoursIncluded: 14)],
            null, null);

        var update = IndexProtoMappings.ToProto(result);

        Assert.Null(update.Frost);
        Assert.Null(update.Vpd);
    }

    [Fact]
    public void Indices_should_map_envelopes_when_present()
    {
        var envelope = new ScoreEnvelope(65, 80, 0.9);
        var result = new IndexResult(new LocationId("lucerne"),
            [new DayScoreSet(0, 70, 70, 70, 70, 70, 70, 70, 70, HoursIncluded: 14,
                OutdoorEnvelope: envelope)],
            null, null);

        var update = IndexProtoMappings.ToProto(result);

        var d0 = update.Days[0];
        Assert.NotNull(d0.OutdoorEnvelope);
        Assert.Equal(65, d0.OutdoorEnvelope.Min);
        Assert.Equal(80, d0.OutdoorEnvelope.Max);
        Assert.Equal(0.9, d0.OutdoorEnvelope.Confidence);
        Assert.Null(d0.LaundryEnvelope);
    }
}
