"""Map WMO weather codes to Home Assistant condition strings."""

from __future__ import annotations

# (day_condition, night_condition)
WMO_TO_HA: dict[int, tuple[str, str]] = {
    0: ("sunny", "clear-night"),
    1: ("partlycloudy", "partlycloudy"),
    2: ("partlycloudy", "partlycloudy"),
    3: ("cloudy", "cloudy"),
    45: ("fog", "fog"),
    48: ("fog", "fog"),
    51: ("rainy", "rainy"),
    53: ("rainy", "rainy"),
    55: ("rainy", "rainy"),
    56: ("rainy", "rainy"),
    57: ("rainy", "rainy"),
    61: ("rainy", "rainy"),
    63: ("rainy", "rainy"),
    65: ("pouring", "pouring"),
    66: ("rainy", "rainy"),
    67: ("pouring", "pouring"),
    71: ("snowy", "snowy"),
    73: ("snowy", "snowy"),
    75: ("snowy", "snowy"),
    77: ("snowy", "snowy"),
    80: ("rainy", "rainy"),
    81: ("rainy", "rainy"),
    82: ("pouring", "pouring"),
    85: ("snowy", "snowy"),
    86: ("snowy", "snowy"),
    95: ("lightning-rainy", "lightning-rainy"),
    96: ("hail", "hail"),
    99: ("hail", "hail"),
}


_KNOWN_CODES = sorted(WMO_TO_HA.keys())


def _nearest_wmo_code(code: int) -> int:
    """Find the nearest known WMO code."""
    return min(_KNOWN_CODES, key=lambda c: abs(c - code))


def map_condition(weather_code: int, is_day: bool) -> str:
    """Map a WMO weather code + is_day flag to an HA condition string."""
    pair = WMO_TO_HA.get(weather_code)
    if pair is None:
        pair = WMO_TO_HA[_nearest_wmo_code(weather_code)]
    return pair[0] if is_day else pair[1]
