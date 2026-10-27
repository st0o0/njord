namespace Njord.Core.Solar;

// Satellite-observed irradiance in W/m2 for the hour starting at ValidFrom. GTI is tilted to the configured panel.
public sealed record SatelliteRadiationPoint(
    DateTimeOffset ValidFrom,
    double? GHI = null,
    double? DirectRadiation = null,
    double? DiffuseRadiation = null,
    double? DNI = null,
    double? GTI = null) : IHasValidTime
{
    private static readonly SatelliteParams[] Flags =
        [.. Enum.GetValues<SatelliteParams>().Where(p => p != SatelliteParams.None)];

    public bool HasFields(SatelliteParams required) =>
        Flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(SatelliteParams param) => param switch
    {
        SatelliteParams.GlobalHorizontal => GHI is not null,
        SatelliteParams.DirectRadiation => DirectRadiation is not null,
        SatelliteParams.DiffuseRadiation => DiffuseRadiation is not null,
        SatelliteParams.DirectNormal => DNI is not null,
        SatelliteParams.GlobalTilted => GTI is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
