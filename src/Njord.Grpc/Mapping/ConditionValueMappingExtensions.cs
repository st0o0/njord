using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;

namespace Njord.Grpc.Mapping;

internal static class ConditionValueMappingExtensions
{
    public static Proto.ConditionValue ToConditionValue(this ConditionFeatureResult r)
    {
        var active = r is not DefaultConditionResult;
        var proto = new Proto.ConditionValue { Active = active };
        if (active)
        {
            PopulateExtra(proto, r);
        }

        return proto;
    }

    private static void PopulateExtra(Proto.ConditionValue proto, ConditionFeatureResult r)
    {
        switch (r)
        {
            case CommuteSafetyResult c:
                proto.Extra["state"] = (double)c.Level;
                break;
            case VentilationWindowResult v:
                proto.Extra["best_start"] = v.BestStart.ToUnixTimeSeconds();
                proto.Extra["best_end"] = v.BestEnd.ToUnixTimeSeconds();
                break;
            case NightVentilationResult n:
                proto.Extra["low_temperature"] = n.LowTemperature;
                proto.Extra["tomorrow_peak"] = n.TomorrowPeak;
                break;
            case ExerciseAdvisoryResult e:
                proto.Extra["state"] = (double)e.Level;
                proto.Extra["best_window"] = e.BestWindow.ToUnixTimeSeconds();
                break;
            case GardeningResult g:
                proto.Extra["state"] = (double)g.Advice;
                proto.Extra["planting_safe"] = g.PlantingSafe ? 1 : 0;
                proto.Extra["watering_needed"] = g.WateringNeeded ? 1 : 0;
                proto.Extra["frost_risk"] = g.FrostRisk ? 1 : 0;
                break;
            case FloodTimelineResult f:
                proto.Extra["state"] = (double)f.Status;
                proto.Extra["peak_discharge"] = f.PeakDischarge;
                proto.Extra["peak_time"] = f.PeakTime.ToUnixTimeSeconds();
                proto.Extra["days_above_warning"] = f.DaysAboveWarning;
                break;
            case HeatPumpCopConditionResult h:
                proto.Extra["value"] = h.Cop;
                proto.Extra["state"] = (double)h.Recommendation;
                proto.Extra["best_cop"] = h.BestCop;
                proto.Extra["best_hour"] = h.BestHour;
                break;
            case SolarBatteryStrategyConditionResult s:
                proto.Extra["state"] = (double)s.Strategy;
                proto.Extra["today_kwh"] = s.TodayKwh;
                proto.Extra["tomorrow_kwh_estimate"] = s.TomorrowKwhEstimate;
                break;
            case IrrigationNeedConditionResult i:
                proto.Extra["state"] = (double)i.Advice;
                proto.Extra["soil_moisture_percent"] = i.SoilMoisturePercent;
                proto.Extra["rain_expected_mm"] = i.RainExpectedMm;
                proto.Extra["et_mm"] = i.EtMm;
                break;
            case FrostDepthConditionResult fd:
                proto.Extra["value"] = fd.DepthCm;
                proto.Extra["state"] = (double)fd.Risk;
                proto.Extra["snow_insulation_cm"] = fd.SnowInsulationCm;
                proto.Extra["soil_temperature_c"] = fd.SoilTemperatureC;
                break;
            case InversionDetectionConditionResult inv:
                proto.Extra["state"] = (double)inv.Strength;
                proto.Extra["temperature_difference_c"] = inv.TemperatureDifferenceC;
                proto.Extra["boundary_layer_height_m"] = inv.BoundaryLayerHeightM;
                if (inv.InversionType is { } invType)
                {
                    proto.Extra["inversion_type"] = (double)invType;
                }

                break;
            case AtmosphericStabilityConditionResult atm:
                proto.Extra["state"] = (double)atm.StabilityClass;
                proto.Extra["cape_j_per_kg"] = atm.CapeJPerKg;
                proto.Extra["cin_j_per_kg"] = atm.CinJPerKg;
                proto.Extra["lifted_index"] = atm.LiftedIndex;
                break;
            case CloudCeilingConditionResult cc:
                proto.Extra["value"] = cc.EstimatedCeilingM;
                proto.Extra["state"] = (double)cc.CeilingCategory;
                proto.Extra["cloud_cover_low_percent"] = cc.CloudCoverLowPercent;
                proto.Extra["cloud_cover_mid_percent"] = cc.CloudCoverMidPercent;
                proto.Extra["cloud_cover_high_percent"] = cc.CloudCoverHighPercent;
                break;
            case SolarYieldResult sy:
                proto.Extra["value"] = sy.KwhPerKwp;
                proto.Extra["peak_hour"] = sy.PeakHour;
                break;
            case ApproachingFrontResult af:
                proto.Extra["value"] = af.EstimatedInHours;
                proto.Extra["pressure_drop"] = af.PressureDrop;
                proto.Extra["temperature_drop"] = af.TemperatureDrop;
                break;
            case SnowDepthWatchResult sw:
                proto.Extra["value"] = sw.DepthCm;
                proto.Extra["state"] = (double)sw.Level;
                break;
            case SoilMoistureResult sm:
                proto.Extra["value"] = sm.Moisture;
                proto.Extra["state"] = (double)sm.Level;
                break;
            case EvapotranspirationResult et:
                proto.Extra["value"] = et.DailyMm;
                proto.Extra["state"] = (double)et.Level;
                break;
            case RagweedPollenResult rp:
                proto.Extra["value"] = rp.PeakValue;
                break;
            case GranularAqiResult ga:
                if (ga.Pm25Aqi is { } pm25)
                {
                    proto.Extra["pm25_aqi"] = pm25;
                }

                if (ga.Pm10Aqi is { } pm10)
                {
                    proto.Extra["pm10_aqi"] = pm10;
                }

                if (ga.No2Aqi is { } no2)
                {
                    proto.Extra["no2_aqi"] = no2;
                }

                if (ga.OzoneAqi is { } ozone)
                {
                    proto.Extra["ozone_aqi"] = ozone;
                }

                if (ga.So2Aqi is { } so2)
                {
                    proto.Extra["so2_aqi"] = so2;
                }

                break;
            case OceanCurrentsResult oc:
                proto.Extra["value"] = oc.PeakVelocityKmh;
                proto.Extra["state"] = (double)oc.Strength;
                if (oc.Direction is { } dir)
                {
                    proto.Extra["direction"] = dir;
                }

                break;
            case ElevatedWindResult ew:
                proto.Extra["value"] = ew.Speed80M;
                proto.Extra["direction_80m"] = ew.Direction80M;
                if (ew.Speed120M is { } s120)
                {
                    proto.Extra["speed_120m"] = s120;
                }

                if (ew.Direction120M is { } d120)
                {
                    proto.Extra["direction_120m"] = d120;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(r), r.GetType().Name, "Unknown condition result type");
        }
    }
}
