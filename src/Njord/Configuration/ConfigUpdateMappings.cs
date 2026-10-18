using Njord.Alerts.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Messages.Config;
using Njord.Pipeline.Configuration;
using Njord.Trends.Configuration;

namespace Njord.Configuration;

public static class ConfigUpdateMappings
{
    public static PollingChanged ToMessage(this PollingOptions polling) =>
        new(polling.PollInterval, polling.DiscoveryInterval);

    public static List<AnalysisUpdate> ToAnalysisUpdates(this UpdateAnalysisConfig cmd)
    {
        var reset = cmd.Reset ?? [];
        var updates = new List<AnalysisUpdate>();
        Add(EnrichmentSection.Consensus, cmd.Consensus is not null,
            s => SectionUpdate.For<ConsensusOptions>(opt => { if (cmd.Consensus is { } c) c.ApplyTo(opt); }, s));
        Add(EnrichmentSection.Alerts, cmd.Alerts is not null,
            s => SectionUpdate.For<AlertOptions>(opt => { if (cmd.Alerts is { } a) a.ApplyTo(opt); }, s));
        Add(EnrichmentSection.Derived, cmd.DerivedEnabled is not null,
            s => SectionUpdate.For<DerivedOptions>(opt => { if (cmd.DerivedEnabled is { } d) opt.Enabled = d; }, s));
        Add(EnrichmentSection.Trends, cmd.TrendsEnabled is not null,
            s => SectionUpdate.For<TrendOptions>(opt => { if (cmd.TrendsEnabled is { } t) opt.Enabled = t; }, s));
        Add(EnrichmentSection.Indices, cmd.Indices is not null,
            s => SectionUpdate.For<IndexOptions>(opt => { if (cmd.Indices is { } i) i.ApplyTo(opt); }, s));
        return updates;

        void Add(EnrichmentSection section, bool hasChange, Func<bool, SectionUpdate> create)
        {
            var isReset = reset.Contains(section);
            if (hasChange || isReset)
            {
                updates.Add(new AnalysisUpdate(section, $"Enrichment.{section}.", create(isReset)));
            }
        }
    }

    public static void ApplyTo(this ConsensusConfigUpdate update, ConsensusOptions opt)
    {
        if (update.Enabled is { } enabled) opt.Enabled = enabled;
        if (update.Method is { } method) opt.Method = method;
        if (update.TrimPercent is { } trimPercent) opt.TrimPercent = trimPercent;
    }

    public static void ApplyTo(this AlertConfigUpdate update, AlertOptions opt)
    {
        if (update.Enabled is { } enabled) opt.Enabled = enabled;
        if (update.FrostThresholds is { } frost) opt.FrostThresholds = [.. frost];
        if (update.HeatThresholds is { } heat) opt.HeatThresholds = [.. heat];
        if (update.StormGustThresholds is { } storm) opt.StormGustThresholds = [.. storm];
        if (update.HeavyRainHourlyThreshold is { } rainHourly) opt.HeavyRainHourlyThreshold = rainHourly;
        if (update.HeavyRainDailyThreshold is { } rainDaily) opt.HeavyRainDailyThreshold = rainDaily;
        if (update.PressureDropThreshold is { } pressureDrop) opt.PressureDropThreshold = pressureDrop;
        if (update.CapeThreshold is { } cape) opt.CapeThreshold = cape;
        if (update.ThunderstormPrecipThreshold is { } stormPrecip) opt.ThunderstormPrecipThreshold = stormPrecip;
        if (update.ThunderstormGustThreshold is { } stormGust) opt.ThunderstormGustThreshold = stormGust;
        if (update.PressureDropSevereThreshold is { } pressureSevere) opt.PressureDropSevereThreshold = pressureSevere;
        if (update.FogPersistentHours is { } fog) opt.FogPersistentHours = fog;
        if (update.IceThreshold is { } ice) opt.IceThreshold = ice;
        if (update.WindChillThresholds is { } windChill) opt.WindChillThresholds = [.. windChill];
        if (update.VisibilityThresholds is { } visibility) opt.VisibilityThresholds = [.. visibility];
        if (update.TropicalNightThresholds is { } tropical) opt.TropicalNightThresholds = [.. tropical];
        if (update.HumidityThresholds is { } humidity) opt.HumidityThresholds = [.. humidity];
    }

    public static void ApplyTo(this IndexConfigUpdate update, IndexOptions opt)
    {
        if (update.Enabled is { } enabled) opt.Enabled = enabled;
        if (update.IndoorTemp is { } indoor) opt.Preferences.IndoorTemp = indoor;
        if (update.IdealOutdoorTemp is { } outdoor) opt.Preferences.IdealOutdoorTemp = outdoor;
        if (update.HeatSensitivity is { } heat) opt.Preferences.HeatSensitivity = heat;
        if (update.HumiditySensitivity is { } humidity) opt.Preferences.HumiditySensitivity = humidity;
        if (update.WindSensitivity is { } wind) opt.Preferences.WindSensitivity = wind;
        if (update.RainSensitivity is { } rain) opt.Preferences.RainSensitivity = rain;
        if (update.RunningIdealTempLow is { } runningLow) opt.Preferences.RunningIdealTempLow = runningLow;
        if (update.RunningIdealTempHigh is { } runningHigh) opt.Preferences.RunningIdealTempHigh = runningHigh;
        if (update.BbqMinTemp is { } bbqMin) opt.Preferences.BbqMinTemp = bbqMin;
        if (update.BbqIdealWindLow is { } bbqWindLow) opt.Preferences.BbqIdealWindLow = bbqWindLow;
        if (update.BbqIdealWindHigh is { } bbqWindHigh) opt.Preferences.BbqIdealWindHigh = bbqWindHigh;
    }

    public static void ApplyTo(this UpdateSettings cmd, NjordOptions opt)
    {
        if (cmd.ForecastDays is { } days)
        {
            opt.ForecastDays = days;
        }

        if (cmd.Horizons.Count > 0)
        {
            opt.Horizons = [.. cmd.Horizons];
        }

        if (cmd.DefaultModels.Count > 0)
        {
            opt.Models = [.. cmd.DefaultModels];
        }

        if (cmd.Parameters is { } parameters)
        {
            opt.Parameters = new ParameterOptions
            {
                Groups = [.. parameters.Groups],
                Extra = [.. parameters.Extra],
                Exclude = [.. parameters.Exclude],
            };
        }
    }
}
