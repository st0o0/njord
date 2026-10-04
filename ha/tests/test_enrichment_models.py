"""Tests for njord enrichment data models."""

import pytest

from custom_components.njord.models import (
    AlertData,
    ConsensusData,
    DayScoreData,
    DerivedData,
    EnrichmentData,
    FrostData,
    HistoryData,
    HorizonConsensusData,
    HorizonDerivedData,
    IndexData,
    ModelMetricsData,
    ParameterConsensusData,
    ParameterTrendData,
    TrendData,
    VpdData,
)


def test_alert_data_defaults():
    a = AlertData(type="frost")
    assert a.type == "frost"
    assert a.severity == "none"
    assert a.confidence == 0.0


def test_alert_data_with_values():
    a = AlertData(type="heat", severity="orange", confidence=0.85)
    assert a.severity == "orange"
    assert a.confidence == 0.85


def test_alert_data_frozen():
    a = AlertData(type="uv")
    with pytest.raises(AttributeError):
        a.severity = "red"


def test_index_data_defaults():
    idx = IndexData()
    assert idx.laundry == 0
    assert idx.bbq == 0
    assert idx.night_ventilation == 0
    assert idx.frost is None
    assert idx.vpd is None
    assert idx.forecast == []


def test_index_data_with_values():
    idx = IndexData(
        laundry=47,
        outdoor=56,
        bbq=51,
        frost=FrostData(hours_until=4, confidence=0.85),
        vpd=VpdData(kpa=0.59, category="optimal"),
        forecast=[DayScoreData(day_offset=1, laundry=40, bbq=30)],
    )
    assert idx.laundry == 47
    assert idx.frost.hours_until == 4
    assert idx.frost.confidence == 0.85
    assert idx.vpd.category == "optimal"
    assert len(idx.forecast) == 1
    assert idx.forecast[0].day_offset == 1


def test_frost_data_defaults():
    f = FrostData()
    assert f.hours_until == 0
    assert f.confidence == 0.0


def test_vpd_data_defaults():
    v = VpdData()
    assert v.kpa == 0.0
    assert v.category == ""


def test_day_score_data_defaults():
    d = DayScoreData()
    assert d.day_offset == 0
    assert d.laundry == 0
    assert d.night_ventilation == 0
    assert d.hours_included == 0


def test_parameter_trend_data():
    t = ParameterTrendData(parameter="temperature_2m", direction="rising", delta=1.5)
    assert t.parameter == "temperature_2m"
    assert t.direction == "rising"


def test_trend_data_defaults():
    t = TrendData()
    assert t.parameter_trends == []
    assert t.stability_label is None
    assert t.precip_starts_in_hours is None


def test_trend_data_with_values():
    t = TrendData(
        stability_label="stable",
        stability_ratio=0.83,
        precip_starts_in_hours=2,
        precip_ends_in_hours=24,
        reliable_hours=3,
    )
    assert t.stability_label == "stable"
    assert t.precip_starts_in_hours == 2


def test_horizon_derived_data():
    h = HorizonDerivedData(horizon="h3", beaufort=2, dewpoint_comfort="sticky")
    assert h.horizon == "h3"
    assert h.beaufort == 2
    assert h.wmo_description is None


def test_derived_data_defaults():
    d = DerivedData()
    assert d.by_horizon == []
    assert d.sunshine_pct is None
    assert d.inversion is None


def test_derived_data_with_values():
    d = DerivedData(
        by_horizon=[HorizonDerivedData(horizon="h3", beaufort=2)],
        diurnal_amplitude=7.3,
        sunshine_pct=66.4,
        inversion=False,
    )
    assert d.sunshine_pct == 66.4
    assert d.inversion is False


def test_model_metrics_data():
    m = ModelMetricsData(model="icon_global", weight=0.1667, mae_7d=1.2)
    assert m.model == "icon_global"
    assert m.weight == 0.1667
    assert m.drift is None


def test_history_data_defaults():
    h = HistoryData()
    assert h.models == []
    assert h.weighted_temperature is None
    assert h.anomaly is None


def test_history_data_with_values():
    h = HistoryData(
        models=[ModelMetricsData(model="icon_eu", weight=0.5)],
        weighted_temperature=24.48,
        seasonal_best="icon_d2",
    )
    assert h.weighted_temperature == 24.48


def test_horizon_consensus_data():
    hc = HorizonConsensusData(horizon="h3", median=20.4, spread=5.2, agreement=0.67, available_models=6)
    assert hc.median == 20.4
    assert hc.available_models == 6


def test_parameter_consensus_data():
    pc = ParameterConsensusData(
        parameter="temperature_2m",
        unit="°C",
        by_horizon=[HorizonConsensusData(horizon="h3", median=20.4)],
    )
    assert pc.parameter == "temperature_2m"
    assert len(pc.by_horizon) == 1


def test_consensus_data_defaults():
    c = ConsensusData()
    assert c.hourly_parameters == []
    assert c.daily_parameters == []


def test_enrichment_data_defaults():
    e = EnrichmentData(location="home")
    assert e.location == "home"
    assert e.alerts == []
    assert e.indices is None
    assert e.trends is None
    assert e.derived is None
    assert e.history is None
    assert e.consensus is None


def test_enrichment_data_with_all():
    e = EnrichmentData(
        location="home",
        alerts=[AlertData(type="uv", severity="orange", confidence=1.0)],
        indices=IndexData(bbq=51),
        trends=TrendData(stability_label="stable"),
        derived=DerivedData(sunshine_pct=66.4),
        history=HistoryData(weighted_temperature=24.48),
        consensus=ConsensusData(),
    )
    assert len(e.alerts) == 1
    assert e.indices.bbq == 51


def test_enrichment_data_frozen():
    e = EnrichmentData(location="home")
    with pytest.raises(AttributeError):
        e.location = "other"
