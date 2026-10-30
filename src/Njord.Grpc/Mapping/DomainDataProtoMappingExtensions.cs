using Google.Protobuf.WellKnownTypes;
using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Seasonal;
using Njord.Grpc.V2;
using Njord.Messages.Stream;

namespace Njord.Grpc.Mapping;

internal static class DomainDataProtoMappingExtensions
{
    public static DomainDataUpdate? ToDomainDataUpdate(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        ForecastDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { Forecast = e.Forecast.ToWeatherData(), Model = e.ModelId ?? string.Empty }),
        AirQualityDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { AirQuality = e.Forecast.ToAirQualityData() }),
        FloodDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { Flood = e.Forecast.ToFloodData() }),
        MarineDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { Marine = e.Forecast.ToMarineData() }),
        SeasonalDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { Seasonal = e.Forecast.ToSeasonalData() }),
        EnsembleDataReceived e => e.Location.ToUpdate(e.Forecast, new DomainDataUpdate { Ensemble = e.Forecast.ToEnsembleData() }),
        _ => null,
    };

    public static string? ToModelId(this PipelineEvent pipelineEvent) =>
        pipelineEvent is ForecastDataReceived e ? e.ModelId : null;

    public static DomainKind? ToDomainKind(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        ForecastDataReceived => DomainKind.Forecast,
        AirQualityDataReceived => DomainKind.AirQuality,
        FloodDataReceived => DomainKind.Flood,
        MarineDataReceived => DomainKind.Marine,
        SeasonalDataReceived => DomainKind.Seasonal,
        EnsembleDataReceived => DomainKind.Ensemble,
        _ => null,
    };

    public static Location? ToDomainLocation(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        ForecastDataReceived e => e.Location,
        AirQualityDataReceived e => e.Location,
        FloodDataReceived e => e.Location,
        MarineDataReceived e => e.Location,
        SeasonalDataReceived e => e.Location,
        EnsembleDataReceived e => e.Location,
        _ => null,
    };

    private static DomainDataUpdate ToUpdate(this Location location, IDomainForecast forecast, DomainDataUpdate update)
    {
        update.Location = location.Name;
        update.Domain = forecast.Domain.ToProto();
        update.ModelRun = forecast.ModelRun.ToTimestamp();
        return update;
    }

    private static ForecastDomainData ToWeatherData(this DomainForecast<ForecastPoint> forecast)
    {
        var data = new ForecastDomainData();
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

    private static EnsembleData ToEnsembleData(this DomainForecast<EnsemblePoint> forecast)
    {
        var data = new EnsembleData();
        data.Points.AddRange(forecast.Points.Select(p => p.ToProto()));
        return data;
    }

    public static EnsembleSample ToProto(this EnsemblePoint p)
    {
        var sample = new EnsembleSample { ValidAt = p.ValidFrom.ToTimestamp() };
        if (p.TemperatureSpread is { } temp) { sample.TemperatureSpread = temp; }
        if (p.PrecipitationSpread is { } precip) { sample.PrecipitationSpread = precip; }
        if (p.WindSpeedSpread is { } wind) { sample.WindSpeedSpread = wind; }
        return sample;
    }

    public static ForecastSample ToProto(this ForecastPoint p)
    {
        var sample = new ForecastSample
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

        if (p.WindGusts is { } gusts) { sample.WindGusts = gusts; }
        if (p.Visibility is { } visibility) { sample.Visibility = visibility; }
        if (p.Cape is { } cape) { sample.Cape = cape; }
        if (p.Rain is { } rain) { sample.Rain = rain; }
        if (p.Snowfall is { } snowfall) { sample.Snowfall = snowfall; }
        if (p.SoilTemperature is { } soil) { sample.SoilTemperature = soil; }
        if (p.FreezingLevelHeight is { } freezing) { sample.FreezingLevelHeight = freezing; }
        if (p.IsDay is { } isDay) { sample.IsDay = isDay > 0.5; }
        if (p.UvIndexClearSky is { } uvClearSkyW) { sample.UvIndexClearSky = uvClearSkyW; }
        if (p.Showers is { } showers) { sample.Showers = showers; }
        if (p.SoilMoisture0To1cm is { } soilMoisture01) { sample.SoilMoisture0To1Cm = soilMoisture01; }
        if (p.SoilMoisture1To3cm is { } soilMoisture13) { sample.SoilMoisture1To3Cm = soilMoisture13; }
        if (p.Evapotranspiration is { } et0) { sample.Evapotranspiration = et0; }
        if (p.VapourPressureDeficit is { } vpd) { sample.VapourPressureDeficit = vpd; }
        if (p.BoundaryLayerHeight is { } blh) { sample.BoundaryLayerHeight = blh; }
        if (p.CloudCoverLow is { } ccLow) { sample.CloudCoverLow = ccLow; }
        if (p.CloudCoverMid is { } ccMid) { sample.CloudCoverMid = ccMid; }
        if (p.CloudCoverHigh is { } ccHigh) { sample.CloudCoverHigh = ccHigh; }
        if (p.ConvectiveInhibition is { } cin) { sample.ConvectiveInhibition = cin; }
        if (p.LiftedIndex is { } li) { sample.LiftedIndex = li; }
        if (p.SurfacePressure is { } sp) { sample.SurfacePressure = sp; }
        if (p.WindSpeed80m is { } ws80) { sample.WindSpeed80M = ws80; }
        if (p.WindDirection80m is { } wd80) { sample.WindDirection80M = wd80; }
        if (p.WindSpeed120m is { } ws120) { sample.WindSpeed120M = ws120; }
        if (p.WindDirection120m is { } wd120) { sample.WindDirection120M = wd120; }
        if (p.Temperature80m is { } t80) { sample.Temperature80M = t80; }
        if (p.Temperature120m is { } t120) { sample.Temperature120M = t120; }
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
        if (p.RagweedPollen is { } ragweed) { sample.RagweedPollen = ragweed; }
        if (p.EuropeanAqiPm25 is { } eaqiPm25) { sample.EuropeanAqiPm25 = eaqiPm25; }
        if (p.EuropeanAqiPm10 is { } eaqiPm10) { sample.EuropeanAqiPm10 = eaqiPm10; }
        if (p.EuropeanAqiNitrogenDioxide is { } eaqiNo2) { sample.EuropeanAqiNitrogenDioxide = eaqiNo2; }
        if (p.EuropeanAqiOzone is { } eaqiO3) { sample.EuropeanAqiOzone = eaqiO3; }
        if (p.EuropeanAqiSulphurDioxide is { } eaqiSo2) { sample.EuropeanAqiSulphurDioxide = eaqiSo2; }
        if (p.AerosolOpticalDepth is { } aod) { sample.AerosolOpticalDepth = aod; }
        if (p.CarbonDioxide is { } co2) { sample.CarbonDioxide = co2; }
        if (p.Methane is { } ch4) { sample.Methane = ch4; }
        if (p.UsAqiPm25 is { } usAqiPm25) { sample.UsAqiPm25 = usAqiPm25; }
        if (p.UsAqiPm10 is { } usAqiPm10) { sample.UsAqiPm10 = usAqiPm10; }
        if (p.UsAqiNitrogenDioxide is { } usAqiNo2) { sample.UsAqiNitrogenDioxide = usAqiNo2; }
        if (p.UsAqiOzone is { } usAqiO3) { sample.UsAqiOzone = usAqiO3; }
        if (p.UsAqiSulphurDioxide is { } usAqiSo2) { sample.UsAqiSulphurDioxide = usAqiSo2; }
        if (p.UsAqiCarbonMonoxide is { } usAqiCo) { sample.UsAqiCarbonMonoxide = usAqiCo; }
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
        if (p.OceanCurrentVelocity is { } currentVelocity) { sample.OceanCurrentVelocity = currentVelocity; }
        if (p.OceanCurrentDirection is { } currentDirection) { sample.OceanCurrentDirection = currentDirection; }
        if (p.SecondarySwellWaveHeight is { } ssHeight) { sample.SecondarySwellWaveHeight = ssHeight; }
        if (p.SecondarySwellWaveDirection is { } ssDir) { sample.SecondarySwellWaveDirection = ssDir; }
        if (p.SecondarySwellWavePeriod is { } ssPeriod) { sample.SecondarySwellWavePeriod = ssPeriod; }
        if (p.TertiarySwellWaveHeight is { } tsHeight) { sample.TertiarySwellWaveHeight = tsHeight; }
        if (p.TertiarySwellWaveDirection is { } tsDir) { sample.TertiarySwellWaveDirection = tsDir; }
        if (p.TertiarySwellWavePeriod is { } tsPeriod) { sample.TertiarySwellWavePeriod = tsPeriod; }
        if (p.InvertBarometerHeight is { } ibh) { sample.InvertBarometerHeight = ibh; }
        return sample;
    }
}
