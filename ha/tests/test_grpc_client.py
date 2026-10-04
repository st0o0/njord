"""Tests for NjordClient against a mock gRPC server."""

from __future__ import annotations

import asyncio
import importlib.util
from datetime import datetime
from unittest.mock import MagicMock

import grpc
import pytest
from google.protobuf.timestamp_pb2 import Timestamp

from custom_components.njord.grpc_client import (
    SENSOR_KIND_MAP,
    NjordClient,
    _parse_extra,
    _to_alert,
)
from custom_components.njord.models import (
    CatalogData,
    EnrichmentData,
    ForecastData,
    NjordConfigData,
    SensorPushResult,
    ServerStatusData,
)
from custom_components.njord.proto.njord.v2 import (
    admin_pb2,
    admin_pb2_grpc,
    common_pb2,
    ops_pb2,
    ops_pb2_grpc,
    sensor_pb2,
    sensor_pb2_grpc,
    weather_pb2,
    weather_pb2_grpc,
)


def _make_ts(epoch: int) -> Timestamp:
    ts = Timestamp()
    ts.FromSeconds(epoch)
    return ts


# --- Mock Servicers ---


class MockWeatherServicer(weather_pb2_grpc.WeatherServiceServicer):
    def __init__(self) -> None:
        self.stream_call_count = 0
        self.fail_stream_on_call: int | None = None

    async def GetCatalog(self, request, context):
        return weather_pb2.GetCatalogResponse(
            locations=[
                common_pb2.LocationInfo(
                    name="lucerne",
                    latitude=47.05,
                    longitude=8.31,
                    models=["icon_d2"],
                ),
                common_pb2.LocationInfo(
                    name="zurich",
                    latitude=47.37,
                    longitude=8.54,
                    models=["icon_d2", "ecmwf_ifs025"],
                ),
            ],
            models=[
                common_pb2.ModelInfo(
                    id="icon_d2",
                    display_name="ICON-D2",
                    provider="DWD",
                    region="DE, CH, AT",
                    coverage_tier=3,
                    resolution_km=2.2,
                    max_forecast_hours=60,
                ),
                common_pb2.ModelInfo(
                    id="ecmwf_ifs025",
                    display_name="ECMWF IFS 0.25°",
                    provider="ECMWF",
                    coverage_tier=1,
                ),
            ],
        )

    async def GetForecast(self, request, context):
        return weather_pb2.GetForecastResponse(
            location=request.location,
            model=request.model,
            updated_at=_make_ts(1720000000),
            hourly=[
                common_pb2.HourlyForecast(
                    valid_at=_make_ts(1720000000),
                    temperature=22.5,
                    weather_code=3,
                    is_day=True,
                ),
            ],
            daily=[
                common_pb2.DailyForecast(
                    date="2026-07-15",
                    temperature_max=28.0,
                    temperature_min=15.0,
                    weather_code=2,
                ),
            ],
        )

    async def GetEnrichments(self, request, context):
        return weather_pb2.GetEnrichmentsResponse(
            location=request.location,
            alerts=common_pb2.AlertUpdate(
                alerts=[
                    common_pb2.Alert(type=5, severity=2, confidence=1.0),
                    common_pb2.Alert(type=1, severity=0, confidence=0.0),
                ]
            ),
            indices=common_pb2.IndexUpdate(
                days=[
                    common_pb2.DayScoreSet(
                        day_offset=0,
                        laundry=47,
                        outdoor=56,
                        bbq=51,
                    ),
                    common_pb2.DayScoreSet(
                        day_offset=1,
                        laundry=40,
                        outdoor=50,
                        bbq=45,
                    ),
                ],
                vpd=common_pb2.VpdInfo(kpa=0.59, category="optimal"),
            ),
            trends=common_pb2.TrendUpdate(
                parameter_trends=[
                    common_pb2.ParameterTrend(parameter="temperature_2m", direction="stable", delta=0.3),
                ],
                stability_label="stable",
                stability_ratio=0.83,
                precip_starts_in_hours=2,
                reliable_hours=3,
            ),
            derived=common_pb2.DerivedUpdate(
                by_horizon=[
                    common_pb2.HorizonDerived(
                        horizon="h3", beaufort=2, dewpoint_comfort="sticky", wmo_description="Rain: slight"
                    ),
                ],
                scalars=common_pb2.ScalarDerived(diurnal_amplitude=7.3, sunshine_pct=66.4, inversion=False),
            ),
            history=common_pb2.HistoryUpdate(
                models=[common_pb2.ModelMetrics(model="icon_global", weight=0.1667, drift=0.0)],
                weighted_temperature=24.48,
            ),
            consensus=common_pb2.ConsensusUpdate(
                hourly_parameters=[
                    common_pb2.ParameterConsensus(
                        parameter="temperature_2m",
                        unit="°C",
                        by_horizon=[
                            common_pb2.HorizonConsensus(
                                horizon="h3", median=20.4, spread=5.2, agreement=0.67, available_models=6
                            ),
                        ],
                    ),
                ],
                daily_parameters=[
                    common_pb2.ParameterConsensus(
                        parameter="temperature_2m_max",
                        unit="°C",
                        by_horizon=[
                            common_pb2.HorizonConsensus(horizon="d0", median=28.5, available_models=5),
                        ],
                    ),
                ],
            ),
        )

    async def StreamForecasts(self, request, context):
        self.stream_call_count += 1
        if self.fail_stream_on_call == self.stream_call_count:
            await context.abort(grpc.StatusCode.UNAVAILABLE, "simulated disconnect")
            return
        for i in range(3):
            yield weather_pb2.ForecastUpdate(
                location=request.location or "lucerne",
                model="icon_d2",
                updated_at=_make_ts(1720000000 + i),
                hourly=[],
                daily=[],
            )

    async def StreamEnrichments(self, request, context):
        for i in range(2):
            yield weather_pb2.EnrichmentEvent(
                location=request.location or "lucerne",
                type_name="alerts",
                updated_at=_make_ts(1720000000 + i),
                alerts=common_pb2.AlertUpdate(alerts=[common_pb2.Alert(type=2, severity=1, confidence=0.5)]),
            )


class MockAdminServicer(admin_pb2_grpc.AdminServiceServicer):
    def __init__(self) -> None:
        self.stream_call_count = 0

    async def GetConfig(self, request, context):
        return admin_pb2.NjordConfig(
            locations=[
                common_pb2.LocationInfo(name="lucerne", latitude=47.05, longitude=8.31, models=["icon_d2"]),
            ],
            default_models=["icon_d2", "ecmwf_ifs025"],
            horizons=[1, 3, 6],
            forecast_days=7,
            poll_interval_seconds=300,
        )

    async def StreamConfig(self, request, context):
        self.stream_call_count += 1
        for i in range(2):
            yield admin_pb2.NjordConfig(
                locations=[
                    common_pb2.LocationInfo(name="lucerne", latitude=47.05, longitude=8.31, models=["icon_d2"]),
                ],
                default_models=["icon_d2"],
                horizons=[1, 3],
                forecast_days=7 + i,
                poll_interval_seconds=300,
            )


class MockOpsServicer(ops_pb2_grpc.OpsServiceServicer):
    async def GetStatus(self, request, context):
        return ops_pb2.StatusResponse(
            version="1.2.3",
            uptime_seconds=3600,
            process_start=_make_ts(1719996400),
            budget=ops_pb2.BudgetStatus(
                monthly_limit=20000,
                monthly_used=5000,
                daily_limit=700,
                daily_used=100,
                usage_percent=25.0,
            ),
            models=[
                ops_pb2.ModelStatus(
                    location="lucerne",
                    model="icon_d2",
                    phase="polling",
                    next_poll=_make_ts(1720003600),
                    miss_count=0,
                ),
            ],
            active_enrichments=["consensus", "alerts"],
        )

    async def TriggerPoll(self, request, context):
        return ops_pb2.TriggerPollResponse(
            triggered_count=6,
            targets=["lucerne/icon_d2", "lucerne/ecmwf_ifs025"],
        )


class MockSensorServicer(sensor_pb2_grpc.SensorServiceServicer):
    def __init__(self) -> None:
        self.received: list[sensor_pb2.SensorReading] = []

    async def Push(self, request, context):
        self.received.append(request)
        if request.kind == sensor_pb2.SENSOR_KIND_UNSPECIFIED:
            return sensor_pb2.PushResponse(accepted=False, rejection_reason="kind must not be UNSPECIFIED")
        return sensor_pb2.PushResponse(accepted=True)

    async def StreamPush(self, request_iterator, context):
        async for reading in request_iterator:
            self.received.append(reading)
        return sensor_pb2.PushResponse(accepted=True)


# --- Fixtures ---


@pytest.fixture()
async def mock_server():
    """Start a mock gRPC server and return (port, weather_servicer, admin_servicer, ops_servicer, sensor_servicer)."""
    if importlib.util.find_spec("pytest_homeassistant_custom_component"):
        pytest.skip("gRPC poller thread conflicts with HA plugin thread checker")
    weather_servicer = MockWeatherServicer()
    admin_servicer = MockAdminServicer()
    ops_servicer = MockOpsServicer()
    sensor_servicer = MockSensorServicer()
    server = grpc.aio.server()
    weather_pb2_grpc.add_WeatherServiceServicer_to_server(weather_servicer, server)
    admin_pb2_grpc.add_AdminServiceServicer_to_server(admin_servicer, server)
    ops_pb2_grpc.add_OpsServiceServicer_to_server(ops_servicer, server)
    sensor_pb2_grpc.add_SensorServiceServicer_to_server(sensor_servicer, server)
    port = server.add_insecure_port("[::]:0")
    await server.start()
    yield port, weather_servicer, admin_servicer, ops_servicer, sensor_servicer
    await server.stop(grace=None)


@pytest.fixture()
async def client(mock_server):
    """Create a connected NjordClient pointing at the mock server."""
    port, _, _, _, _ = mock_server
    c = NjordClient(host="localhost", port=port)
    await c.connect()
    yield c
    await c.close()


# --- Unary RPC Tests ---


@pytest.mark.asyncio
async def test_get_catalog(client):
    catalog = await client.get_catalog()
    assert isinstance(catalog, CatalogData)
    assert len(catalog.locations) == 2
    assert catalog.locations[0].name == "lucerne"
    assert catalog.locations[0].latitude == pytest.approx(47.05)
    assert catalog.locations[0].models == ["icon_d2"]
    assert catalog.locations[1].name == "zurich"
    assert "icon_d2" in catalog.model_info
    assert catalog.model_info["icon_d2"].display_name == "ICON-D2"
    assert catalog.model_info["icon_d2"].coverage_tier == "regional"
    assert catalog.model_info["ecmwf_ifs025"].coverage_tier == "global"


@pytest.mark.asyncio
async def test_get_forecast(client):
    forecast = await client.get_forecast("lucerne", "icon_d2")
    assert isinstance(forecast, ForecastData)
    assert forecast.location == "lucerne"
    assert forecast.model == "icon_d2"
    assert len(forecast.hourly) == 1
    assert forecast.hourly[0].temperature == 22.5
    assert forecast.hourly[0].weather_code == 3
    assert forecast.hourly[0].is_day is True
    assert isinstance(forecast.hourly[0].valid_at, datetime)
    assert isinstance(forecast.updated_at, datetime)
    assert len(forecast.daily) == 1
    assert forecast.daily[0].temperature_max == 28.0


@pytest.mark.asyncio
async def test_get_config(client):
    config = await client.get_config()
    assert isinstance(config, NjordConfigData)
    assert len(config.locations) == 1
    assert config.locations[0].name == "lucerne"
    assert config.locations[0].latitude == pytest.approx(47.05)
    assert config.default_models == ["icon_d2", "ecmwf_ifs025"]
    assert config.horizons == [1, 3, 6]
    assert config.forecast_days == 7


@pytest.mark.asyncio
async def test_get_status(client):
    status = await client.get_status()
    assert isinstance(status, ServerStatusData)
    assert status.version == "1.2.3"
    assert status.uptime_seconds == 3600
    assert status.budget is not None
    assert status.budget.monthly_limit == 20000
    assert status.budget.usage_percent == pytest.approx(25.0)
    assert status.process_start is not None
    assert isinstance(status.process_start, datetime)
    assert len(status.model_statuses) == 1
    assert status.model_statuses[0].location == "lucerne"
    assert status.model_statuses[0].model == "icon_d2"
    assert status.active_enrichments == ["consensus", "alerts"]


@pytest.mark.asyncio
async def test_trigger_poll(client):
    count = await client.trigger_poll()
    assert count == 6


@pytest.mark.asyncio
async def test_trigger_poll_with_params(client):
    count = await client.trigger_poll(location="lucerne", model="icon_d2")
    assert count == 6


# --- Context Manager Test ---


@pytest.mark.asyncio
async def test_context_manager(mock_server):
    port, _, _, _ = mock_server
    async with NjordClient(host="localhost", port=port) as client:
        catalog = await client.get_catalog()
        assert len(catalog.locations) == 2


@pytest.mark.asyncio
async def test_not_connected_raises():
    client = NjordClient(host="localhost", port=9999)
    with pytest.raises(RuntimeError, match="not connected"):
        await client.get_catalog()


# --- Streaming Tests ---


@pytest.mark.asyncio
async def test_stream_forecasts(client):
    updates: list[ForecastData] = []
    async for update in client.stream_forecasts():
        updates.append(update)
        if len(updates) >= 3:
            break
    assert len(updates) == 3
    assert all(isinstance(u, ForecastData) for u in updates)
    assert updates[0].location == "lucerne"
    assert updates[0].model == "icon_d2"
    assert isinstance(updates[0].updated_at, datetime)


@pytest.mark.asyncio
async def test_stream_forecasts_with_location(client):
    updates: list[ForecastData] = []
    async for update in client.stream_forecasts(location="zurich"):
        updates.append(update)
        if len(updates) >= 3:
            break
    assert len(updates) == 3
    assert all(u.location == "zurich" for u in updates)


@pytest.mark.asyncio
async def test_stream_config(client):
    configs: list[NjordConfigData] = []
    async for config in client.stream_config():
        configs.append(config)
        if len(configs) >= 2:
            break
    assert len(configs) == 2
    assert all(isinstance(c, NjordConfigData) for c in configs)
    assert configs[0].forecast_days == 7
    assert configs[1].forecast_days == 8


# --- Reconnect Tests ---


@pytest.mark.asyncio
async def test_stream_reconnects_on_normal_end(mock_server, monkeypatch):
    """Stream that ends normally (EOF) should reconnect, not exit."""
    if importlib.util.find_spec("pytest_homeassistant_custom_component"):
        pytest.skip("gRPC poller thread conflicts with HA plugin thread checker")
    port, weather_servicer, _, _ = mock_server

    import custom_components.njord.grpc_client as client_module

    monkeypatch.setattr(client_module, "_BACKOFF_INITIAL", 0.05)
    monkeypatch.setattr(client_module, "_BACKOFF_MAX", 0.1)

    on_disconnect = MagicMock()
    on_reconnect = MagicMock()

    client = NjordClient(host="localhost", port=port)
    await client.connect()

    updates: list[ForecastData] = []
    async for update in client.stream_forecasts(
        on_disconnect=on_disconnect,
        on_reconnect=on_reconnect,
    ):
        updates.append(update)
        if len(updates) >= 6:
            break

    await client.close()

    assert len(updates) == 6
    assert weather_servicer.stream_call_count >= 2
    assert on_disconnect.call_count == 0
    assert on_reconnect.call_count == 1


@pytest.mark.asyncio
async def test_stream_reconnects_on_failure(mock_server, monkeypatch):
    if importlib.util.find_spec("pytest_homeassistant_custom_component"):
        pytest.skip("gRPC poller thread conflicts with HA plugin thread checker")
    port, weather_servicer, _, _ = mock_server
    weather_servicer.fail_stream_on_call = 1

    import custom_components.njord.grpc_client as client_module

    monkeypatch.setattr(client_module, "_BACKOFF_INITIAL", 0.05)
    monkeypatch.setattr(client_module, "_BACKOFF_MAX", 0.1)

    on_disconnect = MagicMock()
    on_reconnect = MagicMock()

    client = NjordClient(host="localhost", port=port)
    await client.connect()

    updates: list[ForecastData] = []
    async for update in client.stream_forecasts(
        on_disconnect=on_disconnect,
        on_reconnect=on_reconnect,
    ):
        updates.append(update)
        if len(updates) >= 3:
            break

    await client.close()
    await asyncio.sleep(1.0)

    assert len(updates) == 3
    assert on_disconnect.call_count == 1
    assert on_reconnect.call_count == 2


@pytest.mark.asyncio
async def test_repeated_errors_do_not_spam_disconnect(mock_server, monkeypatch):
    """Repeated gRPC errors should only fire on_disconnect once."""
    if importlib.util.find_spec("pytest_homeassistant_custom_component"):
        pytest.skip("gRPC poller thread conflicts with HA plugin thread checker")
    port, weather_servicer, _, _ = mock_server
    weather_servicer.fail_stream_on_call = 1

    import custom_components.njord.grpc_client as client_module

    monkeypatch.setattr(client_module, "_BACKOFF_INITIAL", 0.05)
    monkeypatch.setattr(client_module, "_BACKOFF_MAX", 0.1)

    on_disconnect = MagicMock()
    on_reconnect = MagicMock()

    client = NjordClient(host="localhost", port=port)
    await client.connect()

    updates: list[ForecastData] = []
    async for update in client.stream_forecasts(
        on_disconnect=on_disconnect,
        on_reconnect=on_reconnect,
    ):
        updates.append(update)
        if len(updates) >= 3:
            break

    await client.close()

    assert on_disconnect.call_count == 1


# --- Enrichment Tests ---


@pytest.mark.asyncio
async def test_get_enrichments(client):
    enrichment = await client.get_enrichments("lucerne")
    assert isinstance(enrichment, EnrichmentData)
    assert enrichment.location == "lucerne"

    assert len(enrichment.alerts) == 2
    assert enrichment.alerts[0].type == "uv"
    assert enrichment.alerts[0].severity == "orange"
    assert enrichment.alerts[1].type == "frost"

    assert enrichment.indices is not None
    assert enrichment.indices.laundry == 47
    assert enrichment.indices.bbq == 51
    assert enrichment.indices.vpd is not None
    assert enrichment.indices.vpd.kpa == pytest.approx(0.59)
    assert enrichment.indices.vpd.category == "optimal"
    assert len(enrichment.indices.forecast) == 1
    assert enrichment.indices.forecast[0].day_offset == 1
    assert enrichment.indices.forecast[0].laundry == 40

    assert enrichment.trends is not None
    assert enrichment.trends.stability_label == "stable"
    assert enrichment.trends.reliable_hours == 3

    assert enrichment.derived is not None
    assert enrichment.derived.by_horizon[0].beaufort == 2
    assert enrichment.derived.sunshine_pct == pytest.approx(66.4)
    assert enrichment.derived.inversion is False

    assert enrichment.history is not None
    assert enrichment.history.weighted_temperature == pytest.approx(24.48)

    assert enrichment.consensus is not None
    assert enrichment.consensus.hourly_parameters[0].by_horizon[0].median == pytest.approx(20.4)
    assert enrichment.consensus.daily_parameters[0].parameter == "temperature_2m_max"
    assert enrichment.consensus.daily_parameters[0].by_horizon[0].median == pytest.approx(28.5)


@pytest.mark.asyncio
async def test_stream_enrichments(client):
    updates: list[EnrichmentData] = []
    async for update in client.stream_enrichments():
        updates.append(update)
        if len(updates) >= 2:
            break
    assert len(updates) == 2
    assert all(isinstance(u, EnrichmentData) for u in updates)
    assert updates[0].location == "lucerne"
    assert len(updates[0].alerts) == 1
    assert updates[0].alerts[0].type == "heat"
    assert updates[0].alerts[0].severity == "yellow"


# --- _parse_extra tests ---


class TestParseExtra:
    def test_numeric_values(self):
        extras = [
            common_pb2.ParameterValue(name="cape", numeric=450.0),
            common_pb2.ParameterValue(name="uv_index", numeric=7.2),
        ]
        result = _parse_extra(extras)
        assert result == {"cape": 450.0, "uv_index": 7.2}

    def test_text_values(self):
        extras = [
            common_pb2.ParameterValue(name="pollen_level", text="high"),
        ]
        result = _parse_extra(extras)
        assert result == {"pollen_level": "high"}

    def test_flag_values(self):
        extras = [
            common_pb2.ParameterValue(name="frost_risk", flag=True),
            common_pb2.ParameterValue(name="sunny", flag=False),
        ]
        result = _parse_extra(extras)
        assert result == {"frost_risk": True, "sunny": False}

    def test_mixed_types(self):
        extras = [
            common_pb2.ParameterValue(name="cape", numeric=450.0),
            common_pb2.ParameterValue(name="pollen", text="low"),
            common_pb2.ParameterValue(name="frost", flag=True),
        ]
        result = _parse_extra(extras)
        assert result == {"cape": 450.0, "pollen": "low", "frost": True}

    def test_empty_list(self):
        result = _parse_extra([])
        assert result == {}

    def test_unset_value_skipped(self):
        pv = common_pb2.ParameterValue(name="empty")
        result = _parse_extra([pv])
        assert result == {}


class TestToAlert:
    def test_all_fields_set(self):
        pb = common_pb2.Alert(
            type=5,
            severity=2,
            confidence=0.95,
            trigger_value=8.5,
            threshold=6.0,
            peak_value=9.2,
            hours_until=2,
            duration_hours=4,
        )
        alert = _to_alert(pb)
        assert alert.type == "uv"
        assert alert.severity == "orange"
        assert alert.peak_value == 9.2

    def test_only_required_fields(self):
        pb = common_pb2.Alert(
            type=2,
            severity=1,
            confidence=0.8,
            trigger_value=38.2,
            threshold=35.0,
        )
        alert = _to_alert(pb)
        assert alert.type == "heat"
        assert alert.peak_value is None

    def test_inactive_alert(self):
        pb = common_pb2.Alert(type=1, severity=0, confidence=0.0)
        alert = _to_alert(pb)
        assert alert.severity == "none"


# --- Sensor push tests ---


@pytest.mark.asyncio
async def test_push_sensor(client, mock_server):
    result = await client.push_sensor("indoor_temperature", "lucerne", 23.5, source="wohnzimmer")
    assert isinstance(result, SensorPushResult)
    assert result.accepted is True

    _, _, _, _, sensor_servicer = mock_server
    assert len(sensor_servicer.received) == 1
    reading = sensor_servicer.received[0]
    assert reading.kind == sensor_pb2.SENSOR_KIND_INDOOR_TEMPERATURE
    assert reading.location == "lucerne"
    assert reading.source == "wohnzimmer"
    assert reading.value == pytest.approx(23.5)


@pytest.mark.asyncio
async def test_push_sensor_humidity(client):
    result = await client.push_sensor("indoor_humidity", "lucerne", 65.0)
    assert result.accepted is True


@pytest.mark.asyncio
async def test_push_sensor_unknown_kind(client):
    with pytest.raises(ValueError, match="Unknown sensor kind"):
        await client.push_sensor("unknown_kind", "lucerne", 1.0)


def test_sensor_kind_map():
    assert "indoor_temperature" in SENSOR_KIND_MAP
    assert "indoor_humidity" in SENSOR_KIND_MAP
    assert len(SENSOR_KIND_MAP) == 2
