"""Tests for njord config flow."""

from __future__ import annotations

from homeassistant.config_entries import SOURCE_USER
from homeassistant.core import HomeAssistant
from homeassistant.data_entry_flow import FlowResultType

from custom_components.njord.const import DOMAIN


async def test_config_flow_success(hass: HomeAssistant, mock_client) -> None:
    result = await hass.config_entries.flow.async_init(DOMAIN, context={"source": SOURCE_USER})
    assert result["type"] is FlowResultType.FORM
    assert result["step_id"] == "user"

    result = await hass.config_entries.flow.async_configure(
        result["flow_id"],
        user_input={"host": "localhost", "port": 8081},
    )
    assert result["type"] is FlowResultType.CREATE_ENTRY
    assert result["title"] == "njord (localhost)"
    assert result["data"] == {"host": "localhost", "port": 8081}


async def test_config_flow_cannot_connect(hass: HomeAssistant, mock_client) -> None:
    mock_client.get_catalog.side_effect = Exception("Connection refused")

    result = await hass.config_entries.flow.async_init(DOMAIN, context={"source": SOURCE_USER})
    result = await hass.config_entries.flow.async_configure(
        result["flow_id"],
        user_input={"host": "badhost", "port": 9999},
    )
    assert result["type"] is FlowResultType.FORM
    assert result["errors"] == {"base": "cannot_connect"}


async def test_config_flow_duplicate_abort(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    result = await hass.config_entries.flow.async_init(DOMAIN, context={"source": SOURCE_USER})
    result = await hass.config_entries.flow.async_configure(
        result["flow_id"],
        user_input={"host": "localhost", "port": 8081},
    )
    assert result["type"] is FlowResultType.ABORT
    assert result["reason"] == "already_configured"
