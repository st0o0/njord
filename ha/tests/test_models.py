"""Tests for njord data models."""

from datetime import UTC, datetime

import pytest

from custom_components.njord.models import (
    BudgetStatusData,
    DailyForecastData,
    ForecastData,
    HourlyForecastData,
    NjordConfigData,
    NjordLocation,
    SensorPushResult,
    ServerStatusData,
)


def test_hourly_forecast_defaults_to_none():
    h = HourlyForecastData(valid_at=datetime(2026, 7, 15, tzinfo=UTC))
    assert h.temperature is None
    assert h.weather_code is None
    assert h.is_day is None


def test_hourly_forecast_with_values():
    h = HourlyForecastData(
        valid_at=datetime(2026, 7, 15, 12, 0, tzinfo=UTC),
        temperature=22.5,
        weather_code=3,
        is_day=True,
    )
    assert h.temperature == 22.5
    assert h.weather_code == 3
    assert h.is_day is True


def test_hourly_forecast_frozen():
    h = HourlyForecastData(valid_at=datetime(2026, 7, 15, tzinfo=UTC))
    with pytest.raises(AttributeError):
        h.temperature = 10.0


def test_daily_forecast_defaults():
    d = DailyForecastData(date="2026-07-15")
    assert d.temperature_max is None
    assert d.sunrise is None


def test_forecast_data():
    f = ForecastData(location="lucerne", model="icon_d2", updated_at=datetime(2024, 1, 1, tzinfo=UTC))
    assert f.location == "lucerne"
    assert f.hourly == []
    assert f.daily == []


def test_forecast_data_frozen():
    f = ForecastData(location="lucerne", model="icon_d2", updated_at=datetime(2024, 1, 1, tzinfo=UTC))
    with pytest.raises(AttributeError):
        f.location = "zurich"


def test_njord_location():
    loc = NjordLocation(name="lucerne", latitude=47.05, longitude=8.31, models=["icon_d2"])
    assert loc.name == "lucerne"
    assert loc.models == ["icon_d2"]


def test_njord_config_data_defaults():
    cfg = NjordConfigData()
    assert cfg.locations == []
    assert cfg.forecast_days == 0


def test_server_status_data():
    s = ServerStatusData(version="1.0.0", uptime_seconds=3600)
    assert s.version == "1.0.0"
    assert s.budget is None


def test_budget_status_data():
    b = BudgetStatusData(monthly_limit=20000, monthly_used=5000, usage_percent=25.0)
    assert b.monthly_limit == 20000


def test_sensor_push_result_accepted():
    r = SensorPushResult(accepted=True)
    assert r.accepted is True
    assert r.rejection_reason == ""


def test_sensor_push_result_rejected():
    r = SensorPushResult(accepted=False, rejection_reason="invalid kind")
    assert r.accepted is False
    assert r.rejection_reason == "invalid kind"
