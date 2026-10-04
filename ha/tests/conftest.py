"""Shared test fixtures for ha-njord."""

from __future__ import annotations

from datetime import UTC, datetime
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from custom_components.njord.const import DOMAIN
from custom_components.njord.models import (
    AlertData,
    BudgetStatusData,
    CatalogData,
    ConsensusData,
    DailyForecastData,
    DayScoreData,
    DerivedData,
    EnrichmentData,
    ForecastData,
    FrostData,
    HistoryData,
    HorizonConsensusData,
    HorizonDerivedData,
    HourlyForecastData,
    IndexData,
    ModelInfoData,
    ModelMetricsData,
    NjordConfigData,
    NjordLocation,
    ParameterConsensusData,
    ParameterTrendData,
    ServerStatusData,
    TargetData,
    TrendData,
    VpdData,
)

_UPDATED_AT = datetime(2024, 7, 3, 12, 0, tzinfo=UTC)


def _default_catalog() -> CatalogData:
    return CatalogData(
        locations=[
            NjordLocation(
                name="home",
                latitude=47.05,
                longitude=8.31,
                models=["icon_d2", "ecmwf_ifs025"],
            ),
        ],
        model_info={
            "icon_d2": ModelInfoData(
                id="icon_d2",
                display_name="ICON-D2",
                provider="DWD",
                region="DE, CH, AT",
                coverage_tier="regional",
                resolution_km=2.2,
                max_forecast_hours=60,
                description="DWD high-resolution convection-permitting model",
            ),
            "ecmwf_ifs025": ModelInfoData(
                id="ecmwf_ifs025",
                display_name="ECMWF IFS 0.25°",
                provider="ECMWF",
                region="Global",
                coverage_tier="global",
                resolution_km=25.0,
                max_forecast_hours=362,
                description="ECMWF flagship global model",
            ),
        },
    )


def _default_config() -> NjordConfigData:
    return NjordConfigData(
        locations=[
            NjordLocation(
                name="home",
                latitude=47.05,
                longitude=8.31,
                models=["icon_d2", "ecmwf_ifs025"],
            ),
        ],
        default_models=["icon_d2", "ecmwf_ifs025"],
        horizons=[3, 6, 12, 24],
        forecast_days=7,
        poll_interval_seconds=3600,
    )


def _default_forecast(location: str = "home", model: str = "icon_d2") -> ForecastData:
    return ForecastData(
        location=location,
        model=model,
        updated_at=_UPDATED_AT,
        hourly=[
            HourlyForecastData(
                valid_at=datetime(2026, 7, 15, 12, 0, tzinfo=UTC),
                temperature=22.5,
                apparent_temperature=21.0,
                humidity=65.0,
                wind_speed=3.5,
                wind_bearing=180.0,
                pressure_msl=1013.0,
                weather_code=1,
                is_day=True,
                precipitation=0.0,
                cloud_cover=30.0,
                rain=0.0,
                wind_gusts=8.0,
            ),
        ],
        daily=[
            DailyForecastData(
                date="2026-07-15",
                temperature_max=28.0,
                temperature_min=15.0,
                precipitation_sum=2.5,
                wind_speed_max=12.0,
                wind_gusts_max=18.0,
                sunrise="2026-07-15T03:48:00+00:00",
                sunset="2026-07-15T19:17:00+00:00",
                weather_code=61,
            ),
            DailyForecastData(
                date="2026-07-16",
                temperature_max=25.0,
                temperature_min=14.0,
                precipitation_sum=0.0,
                wind_speed_max=10.0,
                weather_code=1,
            ),
            DailyForecastData(
                date="2026-07-17",
                temperature_max=30.0,
                temperature_min=18.0,
                precipitation_sum=1.0,
                wind_speed_max=15.0,
                weather_code=3,
            ),
        ],
    )


def _default_consensus() -> ConsensusData:
    temp_horizons = []
    wmo_horizons = []
    is_day_horizons = []
    precip_horizons = []
    wind_horizons = []
    for i in range(49):
        agreement = max(0.0, 0.9 - i * 0.015)
        temp_horizons.append(
            HorizonConsensusData(
                horizon=f"h{i}",
                median=20.0 + (i % 12) * 0.5,
                spread=3.0 + i * 0.1,
                agreement=round(agreement, 2),
                available_models=max(2, 10 - i // 10),
            )
        )
        wmo_horizons.append(
            HorizonConsensusData(horizon=f"h{i}", median=1.0 if i < 24 else 3.0, available_models=max(2, 10 - i // 10))
        )
        is_day_horizons.append(
            HorizonConsensusData(
                horizon=f"h{i}", median=1.0 if 6 <= (i % 24) <= 20 else 0.0, available_models=max(2, 10 - i // 10)
            )
        )
        precip_horizons.append(
            HorizonConsensusData(horizon=f"h{i}", median=0.0 if i < 30 else 0.5, available_models=max(2, 10 - i // 10))
        )
        wind_horizons.append(
            HorizonConsensusData(horizon=f"h{i}", median=5.0 + (i % 8), available_models=max(2, 10 - i // 10))
        )
    daily_temp_max = []
    daily_temp_min = []
    daily_precip = []
    daily_wind = []
    daily_wmo = []
    for d in range(7):
        daily_temp_max.append(HorizonConsensusData(horizon=f"d{d}", median=25.0 + d, available_models=5))
        daily_temp_min.append(HorizonConsensusData(horizon=f"d{d}", median=14.0 + d * 0.5, available_models=5))
        daily_precip.append(HorizonConsensusData(horizon=f"d{d}", median=0.0 if d < 3 else 2.5, available_models=5))
        daily_wind.append(HorizonConsensusData(horizon=f"d{d}", median=8.0 + d, available_models=5))
        daily_wmo.append(HorizonConsensusData(horizon=f"d{d}", median=1.0 if d < 3 else 61.0, available_models=5))

    return ConsensusData(
        hourly_parameters=[
            ParameterConsensusData(parameter="temperature_2m", unit="°C", by_horizon=temp_horizons),
            ParameterConsensusData(parameter="weather_code", unit="wmo code", by_horizon=wmo_horizons),
            ParameterConsensusData(parameter="is_day", by_horizon=is_day_horizons),
            ParameterConsensusData(parameter="precipitation", unit="mm", by_horizon=precip_horizons),
            ParameterConsensusData(parameter="wind_speed_10m", unit="m/s", by_horizon=wind_horizons),
        ],
        daily_parameters=[
            ParameterConsensusData(parameter="temperature_2m_max", unit="°C", by_horizon=daily_temp_max),
            ParameterConsensusData(parameter="temperature_2m_min", unit="°C", by_horizon=daily_temp_min),
            ParameterConsensusData(parameter="precipitation_sum", unit="mm", by_horizon=daily_precip),
            ParameterConsensusData(parameter="wind_speed_10m_max", unit="m/s", by_horizon=daily_wind),
            ParameterConsensusData(parameter="weather_code", unit="wmo code", by_horizon=daily_wmo),
        ],
    )


def _default_enrichment(location: str = "home") -> EnrichmentData:
    return EnrichmentData(
        location=location,
        consensus_updated_at=_UPDATED_AT,
        alerts=[
            AlertData(
                type="uv",
                severity="orange",
                confidence=1.0,
                trigger_value=8.5,
                threshold=6.0,
                peak_value=9.2,
                hours_until=2,
                duration_hours=4,
            ),
            AlertData(type="frost", severity="none", confidence=0.0),
            AlertData(type="heat", severity="yellow", confidence=0.33, trigger_value=38.2, threshold=35.0),
            AlertData(type="storm", severity="none", confidence=0.0),
            AlertData(type="heavy_rain", severity="none", confidence=0.0),
            AlertData(type="fog", severity="none", confidence=0.0),
            AlertData(type="snow", severity="none", confidence=0.0),
            AlertData(type="pressure_drop", severity="none", confidence=0.0),
            AlertData(type="thunderstorm", severity="none", confidence=0.0),
            AlertData(type="ice", severity="none", confidence=0.0),
            AlertData(type="wind_chill", severity="none", confidence=0.0),
            AlertData(type="visibility", severity="none", confidence=0.0),
            AlertData(type="tropical_night", severity="none", confidence=0.0),
            AlertData(type="humidity", severity="none", confidence=0.0),
        ],
        indices=IndexData(
            laundry=47,
            outdoor=56,
            running=48,
            cycling=50,
            bbq=51,
            irrigation=22,
            solar=38,
            night_ventilation=22,
            frost=FrostData(hours_until=4, confidence=0.85),
            vpd=VpdData(kpa=0.59, category="optimal"),
            forecast=[DayScoreData(day_offset=1, laundry=40, outdoor=50, bbq=45)],
        ),
        trends=TrendData(
            parameter_trends=[
                ParameterTrendData(parameter="temperature_2m", direction="stable", delta=0.3),
            ],
            weather_change_description="Light rain expected in 2 hours",
            stability_label="stable",
            stability_ratio=0.83,
            precip_starts_in_hours=2,
            reliable_hours=3,
        ),
        derived=DerivedData(
            by_horizon=[
                HorizonDerivedData(horizon="h0", beaufort=3, wind_chill=18.5, dewpoint_comfort="comfortable"),
                HorizonDerivedData(horizon="h3", beaufort=2, wind_chill=16.2, dewpoint_comfort="sticky"),
            ],
            diurnal_amplitude=7.3,
            sunshine_pct=66.4,
            inversion=False,
        ),
        derived_updated_at=datetime.now(UTC),
        history=HistoryData(
            models=[ModelMetricsData(model="icon_d2", weight=0.5)],
            weighted_temperature=24.48,
        ),
        consensus=_default_consensus(),
    )


@pytest.fixture(autouse=True)
def auto_enable_custom_integrations(enable_custom_integrations):
    """Enable custom integrations for all tests."""
    yield


@pytest.fixture
def mock_client():
    """Patch NjordClient across all import sites with canned responses."""
    mock = AsyncMock()
    mock.connect = AsyncMock()
    mock.close = AsyncMock()
    mock.get_catalog = AsyncMock(return_value=_default_catalog())
    mock.get_config = AsyncMock(return_value=_default_config())
    mock.get_forecast = AsyncMock(side_effect=lambda loc, model: _default_forecast(loc, model))
    mock.get_enrichments = AsyncMock(side_effect=lambda loc: _default_enrichment(loc))
    mock.get_status = AsyncMock(
        return_value=ServerStatusData(
            version="1.2.3",
            uptime_seconds=3600,
            budget=BudgetStatusData(
                monthly_limit=20000, monthly_used=5000, daily_limit=700, daily_used=100, usage_percent=25.0
            ),
            active_enrichments=["consensus", "alerts", "derived", "trends", "indices", "history"],
        )
    )
    mock.trigger_poll = AsyncMock(return_value=6)
    mock.get_targets = AsyncMock(
        return_value=[
            TargetData(
                location="home",
                model="icon_d2",
                phase="polling",
                next_poll=datetime(2026, 7, 15, 12, 30, tzinfo=UTC),
                miss_count=0,
            ),
            TargetData(
                location="home",
                model="ecmwf_ifs025",
                phase="polling",
                next_poll=datetime(2026, 7, 15, 13, 0, tzinfo=UTC),
                miss_count=1,
            ),
        ]
    )

    async def _empty_async_gen(**kwargs):
        return
        yield

    mock.stream_forecasts = MagicMock(side_effect=lambda **kwargs: _empty_async_gen())
    mock.stream_enrichments = MagicMock(side_effect=lambda **kwargs: _empty_async_gen())
    mock.stream_config = MagicMock(side_effect=lambda **kwargs: _empty_async_gen())

    with (
        patch("custom_components.njord.NjordClient", return_value=mock),
        patch("custom_components.njord.config_flow.NjordClient", return_value=mock),
    ):
        yield mock


@pytest.fixture
def mock_config_entry(hass):
    """Create and add a mock config entry for njord."""
    from pytest_homeassistant_custom_component.common import MockConfigEntry

    entry = MockConfigEntry(
        domain=DOMAIN,
        data={"host": "localhost", "port": 8081},
        title="njord (localhost)",
        unique_id="localhost:8081",
    )
    entry.add_to_hass(hass)
    return entry


async def init_integration(hass, entry):
    """Set up the njord integration with a config entry."""
    await hass.config_entries.async_setup(entry.entry_id)
    await hass.async_block_till_done()
    return entry
