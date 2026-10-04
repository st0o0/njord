"""Tests for GetTargets integration (TargetData, client, sensors)."""

from __future__ import annotations

import pytest
from homeassistant.helpers import entity_registry as er

from custom_components.njord.const import DOMAIN
from custom_components.njord.models import TargetData
from tests.conftest import init_integration


@pytest.mark.asyncio
async def test_target_data_model():
    target = TargetData(location="home", model="icon_d2", phase="polling")
    assert target.location == "home"
    assert target.model == "icon_d2"
    assert target.phase == "polling"
    assert target.next_poll is None
    assert target.last_change is None
    assert target.miss_count == 0
    assert target.cycle_seconds is None


@pytest.mark.asyncio
async def test_get_targets_called(hass, mock_client, mock_config_entry):
    await init_integration(hass, mock_config_entry)
    mock_client.get_targets.assert_called()


@pytest.mark.asyncio
async def test_target_sensors_registered(hass, mock_client, mock_config_entry):
    await init_integration(hass, mock_config_entry)
    entity_reg = er.async_get(hass)

    icon_id = entity_reg.async_get_entity_id("sensor", DOMAIN, f"{mock_config_entry.entry_id}_home_icon_d2_target")
    assert icon_id is not None

    ecmwf_id = entity_reg.async_get_entity_id(
        "sensor", DOMAIN, f"{mock_config_entry.entry_id}_home_ecmwf_ifs025_target"
    )
    assert ecmwf_id is not None

    icon_entry = entity_reg.async_get(icon_id)
    assert icon_entry.disabled_by is not None
