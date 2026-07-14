namespace Njord.Configuration;

public enum CoverageTier { Global, Europe, Regional }

public sealed record BoundingBox(double MinLat, double MaxLat, double MinLon, double MaxLon)
{
    public bool Contains(double lat, double lon) =>
        lat >= MinLat && lat <= MaxLat && lon >= MinLon && lon <= MaxLon;
}

public sealed record ModelCoverage(CoverageTier Tier, string Region, BoundingBox? Bounds, int? MaxForecastHours = null);

public static class ModelCoverageRegistry
{
    private static readonly BoundingBox Europe = new(34, 72, -12, 45);

    private static readonly Dictionary<string, ModelCoverage> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        // Global models
        ["icon_global"] = new(CoverageTier.Global, "Global", null, 180),
        ["gfs_global"] = new(CoverageTier.Global, "Global", null, 384),
        ["gfs_seamless"] = new(CoverageTier.Global, "Global", null, 384),
        ["ecmwf_ifs025"] = new(CoverageTier.Global, "Global", null, 240),
        ["ifs"] = new(CoverageTier.Global, "Global", null, 240),
        ["ifs_seamless"] = new(CoverageTier.Global, "Global", null, 240),
        ["aifs"] = new(CoverageTier.Global, "Global", null, 360),
        ["ukmo_global_10km"] = new(CoverageTier.Global, "Global", null, 168),
        ["ukmo_seamless"] = new(CoverageTier.Global, "Global", null, 168),
        ["arpege_world"] = new(CoverageTier.Global, "Global", null, 96),
        ["arpege_seamless"] = new(CoverageTier.Global, "Global", null, 96),
        ["gem_global"] = new(CoverageTier.Global, "Global", null, 240),
        ["gem_seamless"] = new(CoverageTier.Global, "Global", null, 240),
        ["jma_gsm"] = new(CoverageTier.Global, "Global", null, 264),
        ["jma_seamless"] = new(CoverageTier.Global, "Global", null, 264),
        ["kma_gdps"] = new(CoverageTier.Global, "Global", null, 288),
        ["kma_seamless"] = new(CoverageTier.Global, "Global", null, 288),
        ["bom_access_global"] = new(CoverageTier.Global, "Global", null, 240),
        ["cma_grapes_global"] = new(CoverageTier.Global, "Global", null, 240),

        // Europe models
        ["icon_eu"] = new(CoverageTier.Europe, "Europe", Europe, 120),
        ["icon_seamless"] = new(CoverageTier.Europe, "Europe", Europe, 180),
        ["arpege_europe"] = new(CoverageTier.Europe, "Europe", Europe, 96),
        ["knmi_harmonie_arome_europe"] = new(CoverageTier.Europe, "Central & Northern Europe", Europe, 60),
        ["harmonie_arome_europe"] = new(CoverageTier.Europe, "Central & Northern Europe", Europe, 60),
        ["dmi_harmonie_arome_europe"] = new(CoverageTier.Europe, "Central & Northern Europe", Europe, 60),
        ["harmonie_arome_europe_dmi"] = new(CoverageTier.Europe, "Central & Northern Europe", Europe, 60),
        ["dmi_seamless"] = new(CoverageTier.Europe, "Central & Northern Europe", Europe, 60),
        ["knmi_seamless"] = new(CoverageTier.Europe, "Europe & Netherlands", Europe, 60),

        // Regional models — DE, CH, AT
        ["icon_d2"] = new(CoverageTier.Regional, "DE, CH, AT", new(43, 57, 1, 18), 48),

        // Regional — NL, BE
        ["knmi_harmonie_arome_netherlands"] = new(CoverageTier.Regional, "NL, BE", new(49, 55, 2, 9), 48),
        ["harmonie_arome_netherlands"] = new(CoverageTier.Regional, "NL, BE", new(49, 55, 2, 9), 48),

        // Regional — Scandinavia
        ["metno_nordic"] = new(CoverageTier.Regional, "NO, DK, SE, FI", new(53, 73, -1, 33), 60),
        ["metno_nordic_seamless"] = new(CoverageTier.Regional, "NO, DK, SE, FI", new(53, 73, -1, 33), 60),

        // Regional — France
        ["arome_france"] = new(CoverageTier.Regional, "France", new(40, 53, -6, 10), 48),
        ["arome_france_hd"] = new(CoverageTier.Regional, "France", new(40, 53, -6, 10), 48),
        ["arome_seamless"] = new(CoverageTier.Regional, "France", new(40, 53, -6, 10), 96),

        // Regional — Switzerland & Central Europe
        ["meteoswiss_icon_ch1"] = new(CoverageTier.Regional, "CH & Central Europe", new(44, 50, 4, 12), 33),
        ["meteoswiss_icon_ch2"] = new(CoverageTier.Regional, "CH & Central Europe", new(44, 50, 4, 12), 45),
        ["meteoswiss_seamless"] = new(CoverageTier.Regional, "CH & Central Europe", new(44, 50, 4, 12), 45),

        // Regional — Austria
        ["geosphere_arome_austria"] = new(CoverageTier.Regional, "Austria", new(45, 50, 8, 19), 48),
        ["geosphere_seamless"] = new(CoverageTier.Regional, "Austria", new(45, 50, 8, 19), 48),

        // Regional — Italy
        ["arpae_2i"] = new(CoverageTier.Regional, "Italy", new(35, 49, 5, 20), 48),

        // Regional — UK, Ireland
        ["ukmo_uk_2km"] = new(CoverageTier.Regional, "UK, Ireland", new(48, 62, -12, 4), 54),

        // Regional — US, Canada
        ["hrrr_us_conus"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 48),
        ["gfs_hrrr"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 384),
        ["nbm_us_conus"] = new(CoverageTier.Regional, "US", new(23, 51, -131, -59), 192),
        ["nam_us_conus"] = new(CoverageTier.Regional, "US, Canada", new(23, 51, -131, -59), 84),

        // Regional — Japan, Korea
        ["jma_msm"] = new(CoverageTier.Regional, "Japan, Korea", new(24, 47, 122, 151), 78),
        ["kma_ldps"] = new(CoverageTier.Regional, "Korea", new(32, 44, 123, 133), 48),

        // Regional — Canada
        ["gem_regional"] = new(CoverageTier.Regional, "North America", new(39, 86, -146, -49), 84),
        ["gem_hrdps_continental"] = new(CoverageTier.Regional, "Canada, Northern US", new(39, 65, -146, -49), 48),
        ["gem_hrdps_west"] = new(CoverageTier.Regional, "Western Canada", new(44, 65, -146, -110), 48),
    };

    public static ModelCoverage? Get(string modelId) =>
        Registry.GetValueOrDefault(modelId);

    public static int EffectiveForecastDays(string modelId, int requestedDays)
    {
        var coverage = Get(modelId);
        if (coverage?.MaxForecastHours is not { } maxHours) return requestedDays;
        var maxDays = (int)Math.Ceiling(maxHours / 24.0);
        return Math.Min(requestedDays, maxDays);
    }

    public static bool IsPlausible(string modelId, double latitude, double longitude)
    {
        var coverage = Get(modelId);
        if (coverage is null) return true;
        if (coverage.Tier == CoverageTier.Global) return true;
        return coverage.Bounds?.Contains(latitude, longitude) ?? true;
    }
}
