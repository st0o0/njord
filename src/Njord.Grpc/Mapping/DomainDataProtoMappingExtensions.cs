using Google.Protobuf.WellKnownTypes;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Seasonal;
using Njord.Forecast.Solar;
using Njord.Forecast.Weather;
using Njord.Grpc.V2;
using Njord.Messages.Pipeline;

namespace Njord.Grpc.Mapping;

internal static class DomainDataProtoMappingExtensions
{
    public static DomainDataUpdate? ToDomainDataUpdate(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        PipelineEvent.WeatherDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { Weather = e.Forecast.ToWeatherData(), Model = e.ModelId ?? string.Empty }),
        PipelineEvent.AirQualityDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { AirQuality = e.Forecast.ToAirQualityData() }),
        PipelineEvent.FloodDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { Flood = e.Forecast.ToFloodData() }),
        PipelineEvent.MarineDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { Marine = e.Forecast.ToMarineData() }),
        PipelineEvent.SeasonalDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { Seasonal = e.Forecast.ToSeasonalData() }),
        PipelineEvent.SatelliteDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { Satellite = e.Forecast.ToSatelliteData() }),
        _ => null,
    };

    public static string? ToModelId(this PipelineEvent pipelineEvent) =>
        pipelineEvent is PipelineEvent.WeatherDataReceived e ? e.ModelId : null;

    public static DomainKind? ToDomainKind(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        PipelineEvent.WeatherDataReceived => DomainKind.Weather,
        PipelineEvent.AirQualityDataReceived => DomainKind.AirQuality,
        PipelineEvent.FloodDataReceived => DomainKind.Flood,
        PipelineEvent.MarineDataReceived => DomainKind.Marine,
        PipelineEvent.SeasonalDataReceived => DomainKind.Seasonal,
        PipelineEvent.SatelliteDataReceived => DomainKind.Satellite,
        _ => null,
    };

    public static Location? ToDomainLocation(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        PipelineEvent.WeatherDataReceived e => e.Location,
        PipelineEvent.AirQualityDataReceived e => e.Location,
        PipelineEvent.FloodDataReceived e => e.Location,
        PipelineEvent.MarineDataReceived e => e.Location,
        PipelineEvent.SeasonalDataReceived e => e.Location,
        PipelineEvent.SatelliteDataReceived e => e.Location,
        _ => null,
    };

    private static DomainDataUpdate ToUpdate(this Location location, IDomainForecast forecast, DomainDataUpdate update)
    {
        update.Location = location.Name;
        update.Domain = forecast.Domain.ToProto();
        update.ModelRun = forecast.ModelRun.ToTimestamp();
        return update;
    }

    private static WeatherData ToWeatherData(this DomainForecast<WeatherPoint> forecast)
    {
        var data = new WeatherData();
        data.Points.AddRange(forecast.Points.Select(p => p.ToProto()));
        return data;
    }

    private static AirQualityData ToAirQualityData(this DomainForecast<AirQualityPoint> forecast)
    {
        var data = new AirQualityData();
        data.Points.AddRange(forecast.Points.Select(p => p.ToProto()));
        return data;
    }

    private static FloodData ToFloodData(this DomainForecast<FloodPoint> forecast)
    {
        var data = new FloodData();
        data.Points.AddRange(forecast.Points.Select(p => p.ToProto()));
        return data;
    }

    private static SatelliteData ToSatelliteData(this DomainForecast<SatelliteRadiationPoint> forecast)
    {
        var data = new SatelliteData();
        data.Points.AddRange(forecast.Points.Select(p => p.ToProto()));
        return data;
    }

    private static SeasonalData ToSeasonalData(this DomainForecast<SeasonalPoint> forecast)
    {
        var data = new SeasonalData();
        data.Points.AddRange(forecast.Points.Select(p => p.ToProto()));
        return data;
    }

    private static MarineData ToMarineData(this DomainForecast<MarinePoint> forecast)
    {
        var data = new MarineData();
        data.Points.AddRange(forecast.Points.Select(p => p.ToProto()));
        return data;
    }

    public static WeatherSample ToProto(this WeatherPoint p)
    {
        var sample = new WeatherSample
        {
            ValidAt = p.ValidFrom.ToTimestamp(),
        };
        if (p.Temperature is { } temperature) { sample.Temperature = temperature; }
        if (p.ApparentTemperature is { } apparent) { sample.ApparentTemperature = apparent; }
        if (p.WindSpeed is { } windSpeed) { sample.WindSpeed = windSpeed; }
        if (p.Precipitation is { } precipitation) { sample.Precipitation = precipitation; }
        if (p.PrecipitationProbability is { } probability) { sample.PrecipitationProbability = probability; }
        if (p.CloudCover is { } cloudCover) { sample.CloudCover = cloudCover; }
        if (p.Pressure is { } pressure) { sample.Pressure = pressure; }
        if (p.WeatherCode is { } weatherCode) { sample.WeatherCode = (int)weatherCode; }
        if (p.Humidity is { } humidity) { sample.Humidity = humidity; }
        if (p.DewPoint is { } dewPoint) { sample.DewPoint = dewPoint; }
        if (p.UvIndex is { } uvIndex) { sample.UvIndex = uvIndex; }
        if (p.EnsembleStdDev is { } stdDev) { sample.EnsembleStdDev = stdDev; }
        if (p.WindGusts is { } gusts) { sample.WindGusts = gusts; }
        if (p.Visibility is { } visibility) { sample.Visibility = visibility; }
        if (p.Cape is { } cape) { sample.Cape = cape; }
        if (p.Rain is { } rain) { sample.Rain = rain; }
        if (p.Snowfall is { } snowfall) { sample.Snowfall = snowfall; }
        if (p.SoilTemperature is { } soil) { sample.SoilTemperature = soil; }
        if (p.FreezingLevelHeight is { } freezing) { sample.FreezingLevelHeight = freezing; }
        if (p.IsDay is { } isDay) { sample.IsDay = isDay > 0.5; }
        return sample;
    }

    public static AirQualitySample ToProto(this AirQualityPoint p)
    {
        var sample = new AirQualitySample
        {
            ValidAt = p.ValidFrom.ToTimestamp(),
        };
        if (p.Pm25 is { } pm25) { sample.Pm25 = pm25; }
        if (p.Pm10 is { } pm10) { sample.Pm10 = pm10; }
        if (p.Ozone is { } ozone) { sample.Ozone = ozone; }
        if (p.BirchPollen is { } birch) { sample.BirchPollen = birch; }
        if (p.GrassPollen is { } grass) { sample.GrassPollen = grass; }
        if (p.Alder is { } alder) { sample.Alder = alder; }
        if (p.Mugwort is { } mugwort) { sample.Mugwort = mugwort; }
        if (p.CarbonMonoxide is { } co) { sample.CarbonMonoxide = co; }
        if (p.SulphurDioxide is { } so2) { sample.SulphurDioxide = so2; }
        if (p.Dust is { } dust) { sample.Dust = dust; }
        if (p.Ammonia is { } ammonia) { sample.Ammonia = ammonia; }
        if (p.OlivePollen is { } olive) { sample.OlivePollen = olive; }
        if (p.UsAqi is { } usAqi) { sample.UsAqi = usAqi; }
        if (p.UvIndex is { } uvIndex) { sample.UvIndex = uvIndex; }
        if (p.UvIndexClearSky is { } uvClearSky) { sample.UvIndexClearSky = uvClearSky; }
        return sample;
    }

    public static FloodSample ToProto(this FloodPoint p)
    {
        var sample = new FloodSample
        {
            ValidAt = p.ValidFrom.ToTimestamp(),
        };
        if (p.Discharge is { } discharge) { sample.Discharge = discharge; }
        if (p.DischargeMean is { } mean) { sample.DischargeMean = mean; }
        if (p.DischargeMedian is { } median) { sample.DischargeMedian = median; }
        if (p.DischargeMax is { } max) { sample.DischargeMax = max; }
        if (p.DischargeMin is { } min) { sample.DischargeMin = min; }
        if (p.DischargeP25 is { } p25) { sample.DischargeP25 = p25; }
        if (p.DischargeP75 is { } p75) { sample.DischargeP75 = p75; }
        return sample;
    }

    public static SatelliteSample ToProto(this SatelliteRadiationPoint p)
    {
        var sample = new SatelliteSample { ValidAt = p.ValidFrom.ToTimestamp() };
        if (p.GHI is { } ghi) { sample.Ghi = ghi; }
        if (p.DirectRadiation is { } direct) { sample.DirectRadiation = direct; }
        if (p.DiffuseRadiation is { } diffuse) { sample.DiffuseRadiation = diffuse; }
        if (p.DNI is { } dni) { sample.Dni = dni; }
        if (p.GTI is { } gti) { sample.Gti = gti; }
        return sample;
    }

    public static SeasonalSample ToProto(this SeasonalPoint p)
    {
        var sample = new SeasonalSample { ValidAt = p.ValidFrom.ToTimestamp() };
        if (p.TemperatureAnomaly is { } temperature) { sample.TemperatureAnomaly = temperature; }
        if (p.PrecipitationAnomaly is { } anomaly) { sample.PrecipitationAnomaly = anomaly; }
        if (p.PrecipitationMean is { } mean) { sample.PrecipitationMean = mean; }
        return sample;
    }

    public static MarineSample ToProto(this MarinePoint p)
    {
        var sample = new MarineSample
        {
            ValidAt = p.ValidFrom.ToTimestamp(),
        };
        if (p.WaveHeight is { } height) { sample.WaveHeight = height; }
        if (p.WavePeriod is { } period) { sample.WavePeriod = period; }
        if (p.WaveDirection is { } direction) { sample.WaveDirection = direction; }
        if (p.WaterTemperature is { } water) { sample.WaterTemperature = water; }
        if (p.WindWaveHeight is { } windWaveHeight) { sample.WindWaveHeight = windWaveHeight; }
        if (p.WindWavePeriod is { } windWavePeriod) { sample.WindWavePeriod = windWavePeriod; }
        if (p.WindWaveDirection is { } windWaveDirection) { sample.WindWaveDirection = windWaveDirection; }
        if (p.WindWavePeakPeriod is { } windWavePeakPeriod) { sample.WindWavePeakPeriod = windWavePeakPeriod; }
        if (p.SwellWaveHeight is { } swellWaveHeight) { sample.SwellWaveHeight = swellWaveHeight; }
        if (p.SwellWavePeriod is { } swellWavePeriod) { sample.SwellWavePeriod = swellWavePeriod; }
        if (p.SwellWaveDirection is { } swellWaveDirection) { sample.SwellWaveDirection = swellWaveDirection; }
        if (p.SwellWavePeakPeriod is { } swellWavePeakPeriod) { sample.SwellWavePeakPeriod = swellWavePeakPeriod; }
        if (p.SeaLevelHeight is { } seaLevelHeight) { sample.SeaLevelHeightMsl = seaLevelHeight; }
        return sample;
    }
}
