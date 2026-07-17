namespace Njord.Configuration;

public enum CoverageTier { Global, Europe, Regional }

public sealed record BoundingBox(double MinLat, double MaxLat, double MinLon, double MaxLon)
{
    public bool Contains(double lat, double lon) =>
        lat >= MinLat && lat <= MaxLat && lon >= MinLon && lon <= MaxLon;
}

public sealed record ModelCoverage(CoverageTier Tier, string Region, BoundingBox? Bounds, int? MaxForecastHours = null);

// Verified 2026-07-17 via live API probes (tools/model-probes/).
public static class ModelCoverageRegistry
{
    private static readonly BoundingBox Europe = new(34, 72, -12, 45);

    private static readonly Dictionary<string, ModelCoverage> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        // Global models
        ["icon_global"] = new(CoverageTier.Global, "Global", null, 180),
        ["gfs_global"] = new(CoverageTier.Global, "Global", null, 384),
        ["gfs_seamless"] = new(CoverageTier.Global, "Global", null, 384),
        ["ecmwf_ifs025"] = new(CoverageTier.Global, "Global", null, 362),
        ["ifs"] = new(CoverageTier.Global, "Global", null, 362),
        ["ifs_seamless"] = new(CoverageTier.Global, "Global", null, 362),
        ["aifs"] = new(CoverageTier.Global, "Global", null, 360),
        ["ukmo_global_10km"] = new(CoverageTier.Global, "Global", null, 168),
        ["ukmo_global_deterministic_10km"] = new(CoverageTier.Global, "Global", null, 168),
        ["ukmo_seamless"] = new(CoverageTier.Global, "Global", null, 168),
        ["arpege_world"] = new(CoverageTier.Global, "Global", null, 108),
        ["arpege_seamless"] = new(CoverageTier.Global, "Global", null, 108),
        ["gem_global"] = new(CoverageTier.Global, "Global", null, 240),
        ["gem_seamless"] = new(CoverageTier.Global, "Global", null, 240),
        ["jma_gsm"] = new(CoverageTier.Global, "Global", null, 269),
        ["jma_seamless"] = new(CoverageTier.Global, "Global", null, 269),
        ["kma_gdps"] = new(CoverageTier.Global, "Global", null, 288),
        ["kma_seamless"] = new(CoverageTier.Global, "Global", null, 288),
        ["bom_access_global"] = new(CoverageTier.Global, "Global", null, 240),
        ["cma_grapes_global"] = new(CoverageTier.Global, "Global", null, 128),

        // Europe models
        ["icon_eu"] = new(CoverageTier.Europe, "Europe", Europe, 126),
        ["icon_seamless"] = new(CoverageTier.Europe, "Europe", Europe, 180),
        ["arpege_europe"] = new(CoverageTier.Europe, "Europe", Europe, 108),
        ["knmi_harmonie_arome_europe"] = new(CoverageTier.Europe, "Central & Northern Europe", Europe, 70),
        ["harmonie_arome_europe"] = new(CoverageTier.Europe, "Central & Northern Europe", Europe, 70),
        ["dmi_harmonie_arome_europe"] = new(CoverageTier.Europe, "Central & Northern Europe", Europe, 68),
        ["harmonie_arome_europe_dmi"] = new(CoverageTier.Europe, "Central & Northern Europe", Europe, 68),
        ["dmi_seamless"] = new(CoverageTier.Europe, "Central & Northern Europe", Europe, 68),
        ["knmi_seamless"] = new(CoverageTier.Europe, "Europe & Netherlands", Europe, 70),

        // Regional models — DE, CH, AT
        ["icon_d2"] = new(CoverageTier.Regional, "DE, CH, AT", new(43, 57, 1, 18), 60),

        // Regional — NL, BE
        ["knmi_harmonie_arome_netherlands"] = new(CoverageTier.Regional, "NL, BE", new(49, 55, 2, 9), 70),
        ["harmonie_arome_netherlands"] = new(CoverageTier.Regional, "NL, BE", new(49, 55, 2, 9), 70),

        // Regional — Scandinavia
        ["metno_nordic"] = new(CoverageTier.Regional, "NO, DK, SE, FI", new(53, 73, -1, 33), 70),
        ["metno_nordic_seamless"] = new(CoverageTier.Regional, "NO, DK, SE, FI", new(53, 73, -1, 33), 70),

        // Regional — France
        ["arome_france"] = new(CoverageTier.Regional, "France", new(40, 53, -6, 10), 60),
        ["arome_france_hd"] = new(CoverageTier.Regional, "France", new(40, 53, -6, 10), 60),
        ["arome_seamless"] = new(CoverageTier.Regional, "France", new(40, 53, -6, 10), 108),

        // Regional — Switzerland & Central Europe
        ["meteoswiss_icon_ch1"] = new(CoverageTier.Regional, "CH & Central Europe", new(44, 50, 4, 12), 42),
        ["meteoswiss_icon_ch2"] = new(CoverageTier.Regional, "CH & Central Europe", new(44, 50, 4, 12), 126),
        ["meteoswiss_seamless"] = new(CoverageTier.Regional, "CH & Central Europe", new(44, 50, 4, 12), 126),

        // Regional — Austria
        ["geosphere_arome_austria"] = new(CoverageTier.Regional, "Austria", new(45, 50, 8, 19), 69),
        ["geosphere_seamless"] = new(CoverageTier.Regional, "Austria", new(45, 50, 8, 19), 69),

        // Regional — UK, Ireland
        ["ukmo_uk_2km"] = new(CoverageTier.Regional, "UK, Ireland", new(48, 62, -12, 4), 63),
        ["ukmo_uk_deterministic_2km"] = new(CoverageTier.Regional, "UK, Ireland", new(48, 62, -12, 4), 63),

        // Regional — US, Canada
        ["hrrr_us_conus"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 54),
        ["ncep_hrrr_conus"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 54),
        ["gfs_hrrr"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 384),
        ["nbm_us_conus"] = new(CoverageTier.Regional, "US", new(23, 51, -131, -59), 276),
        ["ncep_nbm_conus"] = new(CoverageTier.Regional, "US", new(23, 51, -131, -59), 276),
        ["nam_us_conus"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 66),
        ["ncep_nam_conus"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 66),

        // Regional — Japan, Korea
        ["jma_msm"] = new(CoverageTier.Regional, "Japan, Korea", new(24, 47, 122, 151), 78),
        ["kma_ldps"] = new(CoverageTier.Regional, "Korea", new(32, 44, 123, 133), 48),

        // Regional — Canada
        ["gem_regional"] = new(CoverageTier.Regional, "North America", new(39, 86, -146, -49), 90),
        ["gem_hrdps_continental"] = new(CoverageTier.Regional, "Canada, Northern US", new(39, 65, -146, -49), 54),
        ["gem_hrdps_west"] = new(CoverageTier.Regional, "Western Canada", new(44, 65, -146, -110), 48),
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
