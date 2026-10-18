namespace Njord.Model;

public readonly record struct ForecastValue(double? Number, string? Text)
{
    public bool HasValue => Number.HasValue || Text is not null;

    public static implicit operator ForecastValue(double? value) => new(value, null);
    public static implicit operator ForecastValue(string? value) => new(null, value);
}
