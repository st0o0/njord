"""Shared helpers for njord entity construction."""

from __future__ import annotations

from homeassistant.config_entries import ConfigEntry
from homeassistant.helpers.device_registry import DeviceInfo

from .const import DOMAIN


def device_info(entry: ConfigEntry, location: str, sw_version: str | None = None) -> DeviceInfo:
    return DeviceInfo(
        identifiers={(DOMAIN, f"{entry.entry_id}_{location}")},
        name=location.title(),
        manufacturer="njord",
        model="Weather Station",
        sw_version=sw_version,
        entry_type=None,
    )


def server_device_info(entry: ConfigEntry, sw_version: str | None = None) -> DeviceInfo:
    return DeviceInfo(
        identifiers={(DOMAIN, f"{entry.entry_id}_server")},
        name="Server",
        manufacturer="njord",
        model="Weather Service",
        sw_version=sw_version,
        entry_type=None,
    )
