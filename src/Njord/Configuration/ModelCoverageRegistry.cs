namespace Njord.Configuration;

public enum CoverageTier { Global, Continental, Regional }

public sealed record BoundingBox(double MinLat, double MaxLat, double MinLon, double MaxLon)
{
    public bool Contains(double lat, double lon) =>
        lat >= MinLat && lat <= MaxLat && lon >= MinLon && lon <= MaxLon;
}

public sealed record ModelCoverage(
    CoverageTier Tier,
    string Region,
    BoundingBox? Bounds,
    int? MaxForecastHours = null,
    string? DisplayName = null,
    string? Provider = null,
    double? ResolutionKm = null,
    string? Description = null);

// Verified 2026-07-17 via live API probes (tools/model-probes/).
public static class ModelCoverageRegistry
{
    private static readonly BoundingBox Europe = new(34, 72, -12, 45);

    private static readonly Dictionary<string, ModelCoverage> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Global models ──

        ["icon_global"] = new(CoverageTier.Global, "Global", null, 180,
            "ICON Global", "DWD", 13,
            "DWD global model, 13km resolution, 180h forecast horizon"),

        ["gfs_global"] = new(CoverageTier.Global, "Global", null, 384,
            "GFS Global", "NOAA/NCEP", 25,
            "NOAA global model, 0.25° resolution, 16-day forecast horizon"),

        ["gfs_seamless"] = new(CoverageTier.Global, "Global", null, 384,
            "GFS Seamless", "NOAA/NCEP", 25,
            "GFS blended with HRRR for short-range, seamless 16-day coverage"),

        ["ecmwf_ifs025"] = new(CoverageTier.Global, "Global", null, 362,
            "ECMWF IFS 0.25°", "ECMWF", 25,
            "ECMWF flagship global model, 0.25° resolution, 15-day horizon"),

        ["ifs"] = new(CoverageTier.Global, "Global", null, 362,
            "ECMWF IFS", "ECMWF", 25,
            "ECMWF Integrated Forecasting System"),

        ["ifs_seamless"] = new(CoverageTier.Global, "Global", null, 362,
            "IFS Seamless", "ECMWF", 25,
            "ECMWF IFS blended with AIFS for short-range"),

        ["aifs"] = new(CoverageTier.Global, "Global", null, 360,
            "ECMWF AIFS", "ECMWF", 25,
            "ECMWF AI-based forecast system, machine-learning driven"),

        ["ukmo_global_10km"] = new(CoverageTier.Global, "Global", null, 168,
            "UKMO Global 10km", "Met Office", 10,
            "UK Met Office global model, 10km resolution, 7-day horizon"),

        ["ukmo_global_deterministic_10km"] = new(CoverageTier.Global, "Global", null, 168,
            "UKMO Global Deterministic", "Met Office", 10,
            "UK Met Office deterministic global model"),

        ["ukmo_seamless"] = new(CoverageTier.Global, "Global", null, 168,
            "UKMO Seamless", "Met Office", 10,
            "Met Office blended global+regional model"),

        ["arpege_world"] = new(CoverageTier.Global, "Global", null, 108,
            "ARPEGE World", "Météo-France", 40,
            "Météo-France global model, ~40km resolution"),

        ["arpege_seamless"] = new(CoverageTier.Global, "Global", null, 108,
            "ARPEGE Seamless", "Météo-France", 40,
            "Météo-France blended global+regional"),

        ["gem_global"] = new(CoverageTier.Global, "Global", null, 240,
            "GEM Global", "ECCC", 25,
            "Environment Canada global model, 25km resolution, 10-day horizon"),

        ["gem_seamless"] = new(CoverageTier.Global, "Global", null, 240,
            "GEM Seamless", "ECCC", 25,
            "Environment Canada blended global+regional"),

        ["jma_gsm"] = new(CoverageTier.Global, "Global", null, 269,
            "JMA GSM", "JMA", 20,
            "Japan Meteorological Agency global spectral model"),

        ["jma_seamless"] = new(CoverageTier.Global, "Global", null, 269,
            "JMA Seamless", "JMA", 20,
            "JMA blended GSM+MSM"),

        ["kma_gdps"] = new(CoverageTier.Global, "Global", null, 288,
            "KMA GDPS", "KMA", 12,
            "Korea Meteorological Administration global model, 12km"),

        ["kma_seamless"] = new(CoverageTier.Global, "Global", null, 288,
            "KMA Seamless", "KMA", 12,
            "KMA blended global+local"),

        ["bom_access_global"] = new(CoverageTier.Global, "Global", null, 240,
            "BOM ACCESS Global", "BOM", 25,
            "Australian Bureau of Meteorology global model"),

        ["cma_grapes_global"] = new(CoverageTier.Global, "Global", null, 128,
            "CMA GRAPES Global", "CMA", 25,
            "China Meteorological Administration global model"),

        // ── Continental models ──

        ["icon_eu"] = new(CoverageTier.Continental, "Europe", Europe, 126,
            "ICON-EU", "DWD", 7,
            "DWD European nest, 7km resolution, 5-day horizon"),

        ["icon_seamless"] = new(CoverageTier.Continental, "Europe", Europe, 180,
            "ICON Seamless", "DWD", 7,
            "DWD blended ICON-D2 + ICON-EU + ICON Global"),

        ["arpege_europe"] = new(CoverageTier.Continental, "Europe", Europe, 108,
            "ARPEGE Europe", "Météo-France", 11,
            "Météo-France European domain, ~11km resolution"),

        ["knmi_harmonie_arome_europe"] = new(CoverageTier.Continental, "Central & Northern Europe", Europe, 70,
            "HARMONIE-AROME Europe (KNMI)", "KNMI", 5.5,
            "KNMI high-resolution European domain, 5.5km"),

        ["harmonie_arome_europe"] = new(CoverageTier.Continental, "Central & Northern Europe", Europe, 70,
            "HARMONIE-AROME Europe", "KNMI", 5.5,
            "KNMI high-resolution European domain, 5.5km"),

        ["dmi_harmonie_arome_europe"] = new(CoverageTier.Continental, "Central & Northern Europe", Europe, 68,
            "HARMONIE-AROME Europe (DMI)", "DMI", 5.5,
            "Danish Meteorological Institute European domain, 5.5km"),

        ["harmonie_arome_europe_dmi"] = new(CoverageTier.Continental, "Central & Northern Europe", Europe, 68,
            "HARMONIE-AROME Europe (DMI)", "DMI", 5.5,
            "DMI high-resolution European domain"),

        ["dmi_seamless"] = new(CoverageTier.Continental, "Central & Northern Europe", Europe, 68,
            "DMI Seamless", "DMI", 5.5,
            "DMI blended regional+European"),

        ["knmi_seamless"] = new(CoverageTier.Continental, "Europe & Netherlands", Europe, 70,
            "KNMI Seamless", "KNMI", 5.5,
            "KNMI blended Netherlands+European"),

        // ── Regional models ──

        ["icon_d2"] = new(CoverageTier.Regional, "DE, CH, AT", new(43, 57, 1, 18), 60,
            "ICON-D2", "DWD", 2.2,
            "DWD high-resolution convection-permitting model, 2.2km, Central Europe"),

        ["knmi_harmonie_arome_netherlands"] = new(CoverageTier.Regional, "NL, BE", new(49, 55, 2, 9), 70,
            "HARMONIE-AROME Netherlands", "KNMI", 2.5,
            "KNMI convection-permitting model for Benelux, 2.5km"),

        ["harmonie_arome_netherlands"] = new(CoverageTier.Regional, "NL, BE", new(49, 55, 2, 9), 70,
            "HARMONIE-AROME Netherlands", "KNMI", 2.5,
            "KNMI convection-permitting model for Benelux, 2.5km"),

        ["metno_nordic"] = new(CoverageTier.Regional, "NO, DK, SE, FI", new(53, 73, -1, 33), 70,
            "MetNo Nordic", "MET Norway", 2.5,
            "MET Norway Nordic model, 2.5km, Scandinavia"),

        ["metno_nordic_seamless"] = new(CoverageTier.Regional, "NO, DK, SE, FI", new(53, 73, -1, 33), 70,
            "MetNo Nordic Seamless", "MET Norway", 2.5,
            "MET Norway blended Nordic+ECMWF"),

        ["arome_france"] = new(CoverageTier.Regional, "France", new(40, 53, -6, 10), 60,
            "AROME France", "Météo-France", 1.3,
            "Météo-France high-resolution convection-permitting, 1.3km"),

        ["arome_france_hd"] = new(CoverageTier.Regional, "France", new(40, 53, -6, 10), 60,
            "AROME France HD", "Météo-France", 1.3,
            "Météo-France high-definition regional model"),

        ["arome_seamless"] = new(CoverageTier.Regional, "France", new(40, 53, -6, 10), 108,
            "AROME Seamless", "Météo-France", 1.3,
            "Météo-France blended AROME+ARPEGE"),

        ["meteoswiss_icon_ch1"] = new(CoverageTier.Regional, "CH & Central Europe", new(44, 50, 4, 12), 42,
            "MeteoSwiss ICON-CH1", "MeteoSwiss", 1.1,
            "MeteoSwiss ultra high-resolution, 1.1km, Switzerland"),

        ["meteoswiss_icon_ch2"] = new(CoverageTier.Regional, "CH & Central Europe", new(44, 50, 4, 12), 126,
            "MeteoSwiss ICON-CH2", "MeteoSwiss", 2.2,
            "MeteoSwiss regional model, 2.2km, Central Europe"),

        ["meteoswiss_seamless"] = new(CoverageTier.Regional, "CH & Central Europe", new(44, 50, 4, 12), 126,
            "MeteoSwiss Seamless", "MeteoSwiss", 2.2,
            "MeteoSwiss blended CH1+CH2+ICON"),

        ["geosphere_arome_austria"] = new(CoverageTier.Regional, "Austria", new(45, 50, 8, 19), 69,
            "GeoSphere AROME", "GeoSphere Austria", 2.5,
            "GeoSphere Austria convection-permitting model, 2.5km"),

        ["geosphere_seamless"] = new(CoverageTier.Regional, "Austria", new(45, 50, 8, 19), 69,
            "GeoSphere Seamless", "GeoSphere Austria", 2.5,
            "GeoSphere Austria blended regional+ECMWF"),

        ["ukmo_uk_2km"] = new(CoverageTier.Regional, "UK, Ireland", new(48, 62, -12, 4), 63,
            "UKMO UK 2km", "Met Office", 2,
            "Met Office UKV model, 2km, British Isles"),

        ["ukmo_uk_deterministic_2km"] = new(CoverageTier.Regional, "UK, Ireland", new(48, 62, -12, 4), 63,
            "UKMO UK Deterministic 2km", "Met Office", 2,
            "Met Office deterministic UKV model"),

        ["hrrr_us_conus"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 54,
            "HRRR CONUS", "NOAA/NCEP", 3,
            "NOAA High-Resolution Rapid Refresh, 3km, contiguous US"),

        ["ncep_hrrr_conus"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 54,
            "NCEP HRRR CONUS", "NOAA/NCEP", 3,
            "NOAA HRRR convection-permitting, 3km"),

        ["gfs_hrrr"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 384,
            "GFS-HRRR Blend", "NOAA/NCEP", 3,
            "NOAA blended HRRR+GFS for seamless US coverage"),

        ["nbm_us_conus"] = new(CoverageTier.Regional, "US", new(23, 51, -131, -59), 276,
            "NBM CONUS", "NOAA/NCEP", 2.5,
            "National Blend of Models, multi-model consensus for US"),

        ["ncep_nbm_conus"] = new(CoverageTier.Regional, "US", new(23, 51, -131, -59), 276,
            "NCEP NBM CONUS", "NOAA/NCEP", 2.5,
            "NCEP National Blend of Models"),

        ["nam_us_conus"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 66,
            "NAM CONUS", "NOAA/NCEP", 12,
            "North American Mesoscale model, 12km"),

        ["ncep_nam_conus"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 66,
            "NCEP NAM CONUS", "NOAA/NCEP", 12,
            "NCEP North American Mesoscale"),

        ["jma_msm"] = new(CoverageTier.Regional, "Japan, Korea", new(24, 47, 122, 151), 78,
            "JMA MSM", "JMA", 5,
            "JMA Mesoscale Model, 5km, Japan & surroundings"),

        ["kma_ldps"] = new(CoverageTier.Regional, "Korea", new(32, 44, 123, 133), 48,
            "KMA LDPS", "KMA", 1.5,
            "Korea Local Data Processing System, 1.5km"),

        ["gem_regional"] = new(CoverageTier.Regional, "North America", new(39, 86, -146, -49), 90,
            "GEM Regional", "ECCC", 10,
            "Environment Canada regional model, 10km, North America"),

        ["gem_hrdps_continental"] = new(CoverageTier.Regional, "Canada, Northern US", new(39, 65, -146, -49), 54,
            "GEM HRDPS Continental", "ECCC", 2.5,
            "ECCC high-resolution continental, 2.5km"),

        ["gem_hrdps_west"] = new(CoverageTier.Regional, "Western Canada", new(44, 65, -146, -110), 48,
            "GEM HRDPS West", "ECCC", 2.5,
            "ECCC high-resolution Western Canada, 2.5km"),
    };

    public static ModelCoverage? Get(string modelId) =>
        Registry.GetValueOrDefault(modelId);

    public static int EffectiveForecastDays(string modelId, int requestedDays)
    {
        var coverage = Get(modelId);
        if (coverage?.MaxForecastHours is not { } maxHours)
        {
            return requestedDays;
        }

        var maxDays = (int)Math.Ceiling(maxHours / 24.0);
        return Math.Min(requestedDays, maxDays);
    }

    public static bool IsPlausible(string modelId, double latitude, double longitude)
    {
        var coverage = Get(modelId);
        if (coverage is null)
        {
            return true;
        }

        if (coverage.Tier == CoverageTier.Global)
        {
            return true;
        }

        return coverage.Bounds?.Contains(latitude, longitude) ?? true;
    }
}
