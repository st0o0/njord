"""Tests for horizon offset utilities."""

from __future__ import annotations

from datetime import UTC, datetime, timedelta

from custom_components.njord.horizon import current_horizon_offset, get_horizon_entry
from custom_components.njord.models import HorizonDerivedData


def test_offset_after_2_5_hours() -> None:
    ts = datetime.now(UTC) - timedelta(hours=2, minutes=30)
    assert current_horizon_offset(ts) == 2


def test_offset_at_zero() -> None:
    ts = datetime.now(UTC) - timedelta(minutes=30)
    assert current_horizon_offset(ts) == 0


def test_offset_never_negative() -> None:
    ts = datetime.now(UTC) + timedelta(hours=1)
    assert current_horizon_offset(ts) == 0


def test_offset_none_returns_zero() -> None:
    assert current_horizon_offset(None) == 0


def test_get_horizon_entry_found() -> None:
    horizons = [
        HorizonDerivedData(horizon="h0", beaufort=3),
        HorizonDerivedData(horizon="h1", beaufort=4),
        HorizonDerivedData(horizon="h2", beaufort=5),
    ]
    result = get_horizon_entry(horizons, 1)
    assert result is not None
    assert result.beaufort == 4


def test_get_horizon_entry_not_found() -> None:
    horizons = [
        HorizonDerivedData(horizon="h0", beaufort=3),
    ]
    result = get_horizon_entry(horizons, 5)
    assert result is None


def test_get_horizon_entry_empty_list() -> None:
    result = get_horizon_entry([], 0)
    assert result is None
