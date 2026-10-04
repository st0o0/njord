"""Tests for WMO to HA condition mapping."""

from custom_components.njord.condition_mapper import map_condition


def test_clear_sky_day():
    assert map_condition(0, is_day=True) == "sunny"


def test_clear_sky_night():
    assert map_condition(0, is_day=False) == "clear-night"


def test_partly_cloudy():
    assert map_condition(1, is_day=True) == "partlycloudy"
    assert map_condition(2, is_day=False) == "partlycloudy"


def test_overcast():
    assert map_condition(3, is_day=True) == "cloudy"
    assert map_condition(3, is_day=False) == "cloudy"


def test_fog():
    assert map_condition(45, is_day=True) == "fog"
    assert map_condition(48, is_day=False) == "fog"


def test_drizzle():
    assert map_condition(51, is_day=True) == "rainy"


def test_slight_rain():
    assert map_condition(61, is_day=True) == "rainy"


def test_heavy_rain():
    assert map_condition(65, is_day=True) == "pouring"


def test_snow():
    assert map_condition(71, is_day=True) == "snowy"
    assert map_condition(75, is_day=False) == "snowy"


def test_thunderstorm():
    assert map_condition(95, is_day=True) == "lightning-rainy"
    assert map_condition(95, is_day=False) == "lightning-rainy"


def test_hail():
    assert map_condition(96, is_day=True) == "hail"


def test_unknown_code_maps_to_nearest():
    assert map_condition(999, is_day=True) == "hail"
    assert map_condition(4, is_day=True) == "cloudy"
    assert map_condition(50, is_day=True) == "rainy"
