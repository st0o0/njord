namespace Njord.Forecast.Solar;

[Flags]
public enum SatelliteParams
{
    None = 0,
    GlobalHorizontal = 1 << 0,
    DirectRadiation = 1 << 1,
    DiffuseRadiation = 1 << 2,
    DirectNormal = 1 << 3,
    GlobalTilted = 1 << 4,
}
