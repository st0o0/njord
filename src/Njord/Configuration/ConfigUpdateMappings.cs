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
            s => SectionUpdate.For<ConsensusOptions>(opt => { if (cmd.Consensus is { } c)
                {
                    c.ApplyTo(opt);
                }
            }, s));
        Add(EnrichmentSection.Alerts, cmd.Alerts is not null,
            s => SectionUpdate.For<AlertOptions>(opt => { if (cmd.Alerts is { } a)
                {
                    a.ApplyTo(opt);
                }
            }, s));
        Add(EnrichmentSection.Derived, cmd.DerivedEnabled is not null,
            s => SectionUpdate.For<DerivedOptions>(opt => { if (cmd.DerivedEnabled is { } d)
                {
                    opt.Enabled = d;
                }
            }, s));
        Add(EnrichmentSection.Trends, cmd.TrendsEnabled is not null,
            s => SectionUpdate.For<TrendOptions>(opt => { if (cmd.TrendsEnabled is { } t)
                {
                    opt.Enabled = t;
                }
            }, s));
        Add(EnrichmentSection.Indices, cmd.Indices is not null,
            s => SectionUpdate.For<IndexOptions>(opt => { if (cmd.Indices is { } i)
                {
                    i.ApplyTo(opt);
                }
            }, s));
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
        if (update.Enabled is { } enabled)
        {
            opt.Enabled = enabled;
        }

        if (update.Method is { } method)
        {
            opt.Method = method;
        }

        if (update.TrimPercent is { } trimPercent)
        {
            opt.TrimPercent = trimPercent;
        }
    }

    public static void ApplyTo(this AlertConfigUpdate update, AlertOptions opt)
    {
        if (update.Enabled is { } enabled)
        {
            opt.Enabled = enabled;
        }

        if (update.Thresholds is { } t)
        {
            if (t.FrostThresholds is { } frost)
            {
                opt.FrostThresholds = [.. frost];
            }

            if (t.HeatThresholds is { } heat)
            {
                opt.HeatThresholds = [.. heat];
            }

            if (t.StormGustThresholds is { } storm)
            {
                opt.StormGustThresholds = [.. storm];
            }

            if (t.HeavyRainHourlyThreshold is { } rainHourly)
            {
                opt.HeavyRainHourlyThreshold = rainHourly;
            }

            if (t.HeavyRainDailyThreshold is { } rainDaily)
            {
                opt.HeavyRainDailyThreshold = rainDaily;
            }

            if (t.PressureDropThreshold is { } pressureDrop)
            {
                opt.PressureDropThreshold = pressureDrop;
            }

            if (t.PressureDropSevereThreshold is { } pressureSevere)
            {
                opt.PressureDropSevereThreshold = pressureSevere;
            }

            if (t.FogPersistentHours is { } fog)
            {
                opt.FogPersistentHours = fog;
            }

            if (t.CapeThreshold is { } cape)
            {
                opt.CapeThreshold = cape;
            }

            if (t.ThunderstormPrecipThreshold is { } stormPrecip)
            {
                opt.ThunderstormPrecipThreshold = stormPrecip;
            }

            if (t.ThunderstormGustThreshold is { } stormGust)
            {
                opt.ThunderstormGustThreshold = stormGust;
            }

            if (t.IceThreshold is { } ice)
            {
                opt.IceThreshold = ice;
            }

            if (t.WindChillThresholds is { } windChill)
            {
                opt.WindChillThresholds = [.. windChill];
            }

            if (t.VisibilityThresholds is { } visibility)
            {
                opt.VisibilityThresholds = [.. visibility];
            }

            if (t.TropicalNightThresholds is { } tropical)
            {
                opt.TropicalNightThresholds = [.. tropical];
            }

            if (t.HumidityThresholds is { } humidity)
            {
                opt.HumidityThresholds = [.. humidity];
            }
        }
    }

    public static void ApplyTo(this IndexConfigUpdate update, IndexOptions opt)
    {
        if (update.Enabled is { } enabled)
        {
            opt.Enabled = enabled;
        }

        if (update.Preferences is not { } prefs)
        {
            return;
        }

        if (prefs.IndoorTemp is { } indoor)
        {
            opt.Preferences.IndoorTemp = indoor;
        }

        if (prefs.IdealOutdoorTemp is { } outdoor)
        {
            opt.Preferences.IdealOutdoorTemp = outdoor;
        }

        if (prefs.HeatSensitivity is { } heat)
        {
            opt.Preferences.HeatSensitivity = heat;
        }

        if (prefs.HumiditySensitivity is { } humidity)
        {
            opt.Preferences.HumiditySensitivity = humidity;
        }

        if (prefs.WindSensitivity is { } wind)
        {
            opt.Preferences.WindSensitivity = wind;
        }

        if (prefs.RainSensitivity is { } rain)
        {
            opt.Preferences.RainSensitivity = rain;
        }

        if (prefs.RunningIdealTempLow is { } runningLow)
        {
            opt.Preferences.RunningIdealTempLow = runningLow;
        }

        if (prefs.RunningIdealTempHigh is { } runningHigh)
        {
            opt.Preferences.RunningIdealTempHigh = runningHigh;
        }

        if (prefs.BbqMinTemp is { } bbqMin)
        {
            opt.Preferences.BbqMinTemp = bbqMin;
        }

        if (prefs.BbqIdealWindLow is { } bbqWindLow)
        {
            opt.Preferences.BbqIdealWindLow = bbqWindLow;
        }

        if (prefs.BbqIdealWindHigh is { } bbqWindHigh)
        {
            opt.Preferences.BbqIdealWindHigh = bbqWindHigh;
        }
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
