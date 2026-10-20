namespace Njord.OpenMeteo.Mapping;

internal static class SeriesAccess
{
    public static double? At(double?[]? series, int index) =>
        series is not null && index < series.Length ? series[index] : null;

    public static bool AnyAt(int index, params double?[]?[] series) =>
        series.Any(s => At(s, index) is not null);
}
