"""Tests for njord weather entities."""

from __future__ import annotations

from datetime import UTC, datetime
from unittest.mock import AsyncMock

import pytest
from freezegun import freeze_time
from homeassistant.components.weather import WeatherEntityFeature
from homeassistant.core import HomeAssistant

from custom_components.njord.condition_mapper import map_condition
from custom_components.njord.models import (
    ConsensusData,
    DailyForecastData,
    EnrichmentData,
    ForecastData,
    HorizonConsensusData,
    HourlyForecastData,
    ParameterConsensusData,
)
from tests.conftest import init_integration


def test_condition_mapping_day():
    assert map_condition(0, is_day=True) == "sunny"
    assert map_condition(1, is_day=True) == "partlycloudy"
    assert map_condition(61, is_day=True) == "rainy"


def test_condition_mapping_night():
    assert map_condition(0, is_day=False) == "clear-night"


async def test_weather_entity_state(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    assert state.state == "partlycloudy"
    assert state.attributes["temperature"] == 22.5
    assert state.attributes["humidity"] == 65.0
    assert state.attributes["pressure"] == 1013.0
    assert state.attributes["wind_speed"] == pytest.approx(12.6, abs=0.1)
    assert state.attributes["wind_bearing"] == 180.0


async def test_weather_entity_second_model(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_ecmwf_ifs_0_25deg")
    assert state is not None
    assert state.state == "partlycloudy"


@freeze_time("2024-07-03T12:00:00+00:00")
async def test_consensus_current_state_from_h0(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_consensus")
    assert state is not None
    assert state.attributes.get("temperature") == 20.0
    assert state.attributes.get("agreement") is not None
    assert state.attributes.get("available_models") is not None
    assert state.attributes.get("reliable_hours") is not None
    assert state.attributes["reliable_hours"] > 0


async def test_consensus_supported_features(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_consensus")
    assert state is not None
    features = WeatherEntityFeature(state.attributes["supported_features"])
    assert features & WeatherEntityFeature.FORECAST_HOURLY
    assert features & WeatherEntityFeature.FORECAST_DAILY


async def test_supported_features_with_hourly_and_daily(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    features = WeatherEntityFeature(state.attributes["supported_features"])
    assert features & WeatherEntityFeature.FORECAST_HOURLY
    assert features & WeatherEntityFeature.FORECAST_DAILY


async def test_supported_features_hourly_only(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(
        side_effect=lambda loc, model: ForecastData(
            location=loc,
            model=model,
            updated_at=datetime(2024, 7, 3, 12, 0, tzinfo=UTC),
            hourly=[
                HourlyForecastData(
                    valid_at=datetime(2026, 7, 15, 12, 0, tzinfo=UTC),
                    temperature=22.5,
                    weather_code=1,
                    is_day=True,
                ),
            ],
            daily=[],
        )
    )
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    features = WeatherEntityFeature(state.attributes["supported_features"])
    assert features & WeatherEntityFeature.FORECAST_HOURLY
    assert not (features & WeatherEntityFeature.FORECAST_DAILY)


async def test_supported_features_stub_has_no_features(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(side_effect=Exception("fetch error"))
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    features = WeatherEntityFeature(state.attributes["supported_features"])
    assert not (features & WeatherEntityFeature.FORECAST_HOURLY)
    assert not (features & WeatherEntityFeature.FORECAST_DAILY)


async def test_weather_entity_available_with_data(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    assert state.state != "unavailable"


async def test_weather_entity_available_with_stub(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(side_effect=Exception("fetch error"))
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    assert state.state == "unknown"


async def test_extra_state_attributes_with_extras(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(
        side_effect=lambda loc, model: ForecastData(
            location=loc,
            model=model,
            updated_at=datetime(2024, 7, 3, 12, 0, tzinfo=UTC),
            hourly=[
                HourlyForecastData(
                    valid_at=datetime(2026, 7, 15, 12, 0, tzinfo=UTC),
                    temperature=22.5,
                    weather_code=1,
                    is_day=True,
                    extra={"cape": 450.0, "uv_index": 7.2},
                ),
            ],
            daily=[
                DailyForecastData(
                    date="2026-07-15",
                    temperature_max=28.0,
                    temperature_min=15.0,
                    weather_code=2,
                ),
                DailyForecastData(date="2026-07-16", temperature_max=25.0, temperature_min=14.0, weather_code=1),
                DailyForecastData(date="2026-07-17", temperature_max=30.0, temperature_min=18.0, weather_code=3),
            ],
        )
    )
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    assert state.attributes["cape"] == 450.0
    assert state.attributes["uv_index"] == 7.2


async def test_extra_state_attributes_empty(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    assert "cape" not in state.attributes


@freeze_time("2026-07-15T11:00:00+00:00")
async def test_hourly_forecast_includes_extras(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(
        side_effect=lambda loc, model: ForecastData(
            location=loc,
            model=model,
            updated_at=datetime(2024, 7, 3, 12, 0, tzinfo=UTC),
            hourly=[
                HourlyForecastData(
                    valid_at=datetime(2026, 7, 15, 12, 0, tzinfo=UTC),
                    temperature=22.5,
                    weather_code=1,
                    is_day=True,
                    extra={"cape": 450.0},
                ),
                HourlyForecastData(
                    valid_at=datetime(2026, 7, 15, 13, 0, tzinfo=UTC),
                    temperature=23.0,
                    weather_code=1,
                    is_day=True,
                    extra={},
                ),
            ],
            daily=[
                DailyForecastData(date="2026-07-15", temperature_max=28.0, temperature_min=15.0, weather_code=2),
                DailyForecastData(date="2026-07-16", temperature_max=25.0, temperature_min=14.0, weather_code=1),
                DailyForecastData(date="2026-07-17", temperature_max=30.0, temperature_min=18.0, weather_code=3),
            ],
        )
    )
    await init_integration(hass, mock_config_entry)

    forecasts = await hass.services.async_call(
        "weather",
        "get_forecasts",
        {"entity_id": "weather.home_icon_d2", "type": "hourly"},
        blocking=True,
        return_response=True,
    )
    data = forecasts["weather.home_icon_d2"]["forecast"]
    assert data[0]["cape"] == 450.0
    assert "cape" not in data[1]


@freeze_time("2026-07-15T14:00:00+00:00")
async def test_daily_forecast_includes_extras(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(
        side_effect=lambda loc, model: ForecastData(
            location=loc,
            model=model,
            updated_at=datetime(2024, 7, 3, 12, 0, tzinfo=UTC),
            hourly=[
                HourlyForecastData(
                    valid_at=datetime(2026, 7, 15, 12, 0, tzinfo=UTC),
                    temperature=22.5,
                    weather_code=1,
                    is_day=True,
                ),
            ],
            daily=[
                DailyForecastData(
                    date="2026-07-15",
                    temperature_max=28.0,
                    temperature_min=15.0,
                    weather_code=2,
                    extra={"soil_moisture": 23.5},
                ),
                DailyForecastData(
                    date="2026-07-16",
                    temperature_max=25.0,
                    temperature_min=14.0,
                    weather_code=1,
                    extra={},
                ),
                DailyForecastData(
                    date="2026-07-17",
                    temperature_max=30.0,
                    temperature_min=18.0,
                    weather_code=3,
                ),
            ],
        )
    )
    await init_integration(hass, mock_config_entry)

    forecasts = await hass.services.async_call(
        "weather",
        "get_forecasts",
        {"entity_id": "weather.home_icon_d2", "type": "daily"},
        blocking=True,
        return_response=True,
    )
    data = forecasts["weather.home_icon_d2"]["forecast"]
    assert data[0]["soil_moisture"] == 23.5
    assert "soil_moisture" not in data[1]


# --- Current-hour selection tests ---


def _multi_hour_forecast(loc: str = "home", model: str = "icon_d2") -> ForecastData:
    return ForecastData(
        location=loc,
        model=model,
        updated_at=datetime(2026, 7, 15, 14, 0, tzinfo=UTC),
        hourly=[
            HourlyForecastData(
                valid_at=datetime(2026, 7, 15, 14, 0, tzinfo=UTC),
                temperature=20.0,
                weather_code=1,
                is_day=True,
                humidity=60.0,
                wind_speed=3.0,
                wind_bearing=90.0,
                pressure_msl=1010.0,
            ),
            HourlyForecastData(
                valid_at=datetime(2026, 7, 15, 15, 0, tzinfo=UTC),
                temperature=21.0,
                weather_code=2,
                is_day=True,
                humidity=58.0,
                wind_speed=3.5,
                wind_bearing=100.0,
                pressure_msl=1011.0,
            ),
            HourlyForecastData(
                valid_at=datetime(2026, 7, 15, 16, 0, tzinfo=UTC),
                temperature=22.0,
                weather_code=3,
                is_day=True,
                humidity=55.0,
                wind_speed=4.0,
                wind_bearing=110.0,
                pressure_msl=1012.0,
            ),
            HourlyForecastData(
                valid_at=datetime(2026, 7, 15, 17, 0, tzinfo=UTC),
                temperature=23.0,
                weather_code=1,
                is_day=True,
                humidity=52.0,
                wind_speed=4.5,
                wind_bearing=120.0,
                pressure_msl=1013.0,
            ),
            HourlyForecastData(
                valid_at=datetime(2026, 7, 15, 18, 0, tzinfo=UTC),
                temperature=22.5,
                weather_code=2,
                is_day=True,
                humidity=54.0,
                wind_speed=4.0,
                wind_bearing=115.0,
                pressure_msl=1012.5,
            ),
        ],
        daily=[
            DailyForecastData(date="2026-07-15", temperature_max=28.0, temperature_min=15.0, weather_code=2),
            DailyForecastData(date="2026-07-16", temperature_max=25.0, temperature_min=14.0, weather_code=1),
            DailyForecastData(date="2026-07-17", temperature_max=30.0, temperature_min=18.0, weather_code=3),
        ],
    )


@freeze_time("2026-07-15T16:30:00+00:00")
async def test_current_hourly_selects_matching_hour(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(side_effect=lambda loc, model: _multi_hour_forecast(loc, model))
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    assert state.attributes["temperature"] == 22.0
    assert state.attributes["humidity"] == 55.0
    assert state.attributes["pressure"] == 1012.0
    assert state.attributes["wind_bearing"] == 110.0


@freeze_time("2026-07-15T14:10:00+00:00")
async def test_current_hourly_selects_first_entry(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(side_effect=lambda loc, model: _multi_hour_forecast(loc, model))
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    assert state.attributes["temperature"] == 20.0


@freeze_time("2026-07-15T13:00:00+00:00")
async def test_current_hourly_all_future_returns_unknown(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(side_effect=lambda loc, model: _multi_hour_forecast(loc, model))
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    assert state.state == "unknown"


async def test_current_hourly_empty_returns_unknown(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(side_effect=Exception("fetch error"))
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_icon_d2")
    assert state is not None
    assert state.state == "unknown"


# --- Hourly forecast filter tests ---


@freeze_time("2026-07-15T16:30:00+00:00")
async def test_hourly_forecast_filters_past_entries(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(side_effect=lambda loc, model: _multi_hour_forecast(loc, model))
    await init_integration(hass, mock_config_entry)

    forecasts = await hass.services.async_call(
        "weather",
        "get_forecasts",
        {"entity_id": "weather.home_icon_d2", "type": "hourly"},
        blocking=True,
        return_response=True,
    )
    data = forecasts["weather.home_icon_d2"]["forecast"]
    assert len(data) == 2
    assert data[0]["temperature"] == 23.0
    assert data[1]["temperature"] == 22.5


@freeze_time("2026-07-15T19:00:00+00:00")
async def test_hourly_forecast_all_past_returns_none(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    mock_client.get_forecast = AsyncMock(side_effect=lambda loc, model: _multi_hour_forecast(loc, model))
    await init_integration(hass, mock_config_entry)

    forecasts = await hass.services.async_call(
        "weather",
        "get_forecasts",
        {"entity_id": "weather.home_icon_d2", "type": "hourly"},
        blocking=True,
        return_response=True,
    )
    data = forecasts["weather.home_icon_d2"]["forecast"]
    assert not data


# --- Consensus horizon advance tests ---


def _consensus_with_timestamp(updated_at: datetime) -> EnrichmentData:
    """Build enrichment with consensus data and a known updated_at."""
    temp_horizons = []
    wmo_horizons = []
    is_day_horizons = []
    for i in range(49):
        agreement = max(0.0, 0.9 - i * 0.015)
        temp_horizons.append(
            HorizonConsensusData(
                horizon=f"h{i}",
                median=20.0 + i * 0.5,
                spread=3.0 + i * 0.1,
                agreement=round(agreement, 2),
                available_models=max(2, 10 - i // 10),
            )
        )
        wmo_horizons.append(HorizonConsensusData(horizon=f"h{i}", median=1.0, available_models=5))
        is_day_horizons.append(HorizonConsensusData(horizon=f"h{i}", median=1.0, available_models=5))
    daily_temp_max = []
    daily_temp_min = []
    daily_wmo = []
    for d in range(3):
        daily_temp_max.append(HorizonConsensusData(horizon=f"d{d}", median=28.0 + d, available_models=5))
        daily_temp_min.append(HorizonConsensusData(horizon=f"d{d}", median=15.0 + d * 0.5, available_models=5))
        daily_wmo.append(HorizonConsensusData(horizon=f"d{d}", median=1.0, available_models=5))

    return EnrichmentData(
        location="home",
        consensus=ConsensusData(
            hourly_parameters=[
                ParameterConsensusData(parameter="temperature_2m", unit="°C", by_horizon=temp_horizons),
                ParameterConsensusData(parameter="weather_code", unit="wmo code", by_horizon=wmo_horizons),
                ParameterConsensusData(parameter="is_day", by_horizon=is_day_horizons),
            ],
            daily_parameters=[
                ParameterConsensusData(parameter="temperature_2m_max", unit="°C", by_horizon=daily_temp_max),
                ParameterConsensusData(parameter="temperature_2m_min", unit="°C", by_horizon=daily_temp_min),
                ParameterConsensusData(parameter="weather_code", unit="wmo code", by_horizon=daily_wmo),
            ],
        ),
        consensus_updated_at=updated_at,
    )


@freeze_time("2026-07-15T17:00:00+00:00")
async def test_consensus_reads_adjusted_horizon(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    enrichment = _consensus_with_timestamp(datetime(2026, 7, 15, 14, 0, tzinfo=UTC))
    mock_client.get_enrichments = AsyncMock(return_value=enrichment)
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_consensus")
    assert state is not None
    assert state.attributes["temperature"] == 20.0 + 3 * 0.5


@freeze_time("2026-07-15T14:10:00+00:00")
async def test_consensus_reads_h0_when_just_pushed(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    enrichment = _consensus_with_timestamp(datetime(2026, 7, 15, 14, 0, tzinfo=UTC))
    mock_client.get_enrichments = AsyncMock(return_value=enrichment)
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_consensus")
    assert state is not None
    assert state.attributes["temperature"] == 20.0


@freeze_time("2026-07-15T14:00:00+00:00")
async def test_consensus_falls_back_to_h0_without_timestamp(
    hass: HomeAssistant, mock_client, mock_config_entry
) -> None:
    enrichment = _consensus_with_timestamp(datetime(2026, 7, 15, 14, 0, tzinfo=UTC))
    from dataclasses import replace

    enrichment = replace(enrichment, consensus_updated_at=None)
    mock_client.get_enrichments = AsyncMock(return_value=enrichment)
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_consensus")
    assert state is not None
    assert state.attributes["temperature"] == 20.0


@freeze_time("2026-07-18T14:00:00+00:00")
async def test_consensus_elapsed_exceeds_horizons_returns_unknown(
    hass: HomeAssistant, mock_client, mock_config_entry
) -> None:
    enrichment = _consensus_with_timestamp(datetime(2026, 7, 15, 14, 0, tzinfo=UTC))
    mock_client.get_enrichments = AsyncMock(return_value=enrichment)
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_consensus")
    assert state is not None
    assert state.state == "unknown"


@freeze_time("2026-07-15T17:00:00+00:00")
async def test_consensus_extra_attrs_use_adjusted_horizon(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    enrichment = _consensus_with_timestamp(datetime(2026, 7, 15, 14, 0, tzinfo=UTC))
    mock_client.get_enrichments = AsyncMock(return_value=enrichment)
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_consensus")
    assert state is not None
    assert state.attributes["agreement"] == round(0.9 - 3 * 0.015, 2)
    assert state.attributes["available_models"] == 10
    assert state.attributes["current_horizon"] == "h3"
    assert state.attributes["consensus_age_hours"] == 3


@freeze_time("2026-07-15T14:10:00+00:00")
async def test_consensus_current_horizon_at_zero_offset(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    enrichment = _consensus_with_timestamp(datetime(2026, 7, 15, 14, 0, tzinfo=UTC))
    mock_client.get_enrichments = AsyncMock(return_value=enrichment)
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_consensus")
    assert state is not None
    assert state.attributes["current_horizon"] == "h0"
    assert state.attributes["consensus_age_hours"] == 0


@freeze_time("2026-07-15T14:00:00+00:00")
async def test_consensus_age_absent_without_timestamp(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    from dataclasses import replace

    enrichment = replace(
        _consensus_with_timestamp(datetime(2026, 7, 15, 14, 0, tzinfo=UTC)),
        consensus_updated_at=None,
    )
    mock_client.get_enrichments = AsyncMock(return_value=enrichment)
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("weather.home_consensus")
    assert state is not None
    assert state.attributes["current_horizon"] == "h0"
    assert "consensus_age_hours" not in state.attributes


@freeze_time("2026-07-15T14:00:00+00:00")
async def test_consensus_daily_forecast_from_server(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    enrichment = _consensus_with_timestamp(datetime(2026, 7, 15, 14, 0, tzinfo=UTC))
    mock_client.get_enrichments = AsyncMock(return_value=enrichment)
    await init_integration(hass, mock_config_entry)

    entity = hass.states.get("weather.home_consensus")
    assert entity is not None

    forecasts = await hass.services.async_call(
        "weather",
        "get_forecasts",
        {"entity_id": "weather.home_consensus", "type": "daily"},
        blocking=True,
        return_response=True,
    )
    daily = forecasts["weather.home_consensus"]["forecast"]
    assert len(daily) == 3

    assert daily[0]["temperature"] == 28.0
    assert daily[0]["templow"] == 15.0
    assert daily[0]["datetime"] == "2026-07-15T00:00:00+00:00"
    assert daily[0]["condition"] == "partlycloudy"

    assert daily[1]["temperature"] == 29.0
    assert daily[1]["templow"] == 15.5
    assert daily[1]["datetime"] == "2026-07-16T00:00:00+00:00"
