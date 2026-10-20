using System.Globalization;

namespace Njord.Messages.Pipeline;

public static class FeatureData
{
    public static IReadOnlyDictionary<string, string> Of(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(entry => entry.Key, entry => entry.Value);

    public static string Time(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Flag(bool value) => value ? "true" : "false";
}
