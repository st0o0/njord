"""Tests for enrichment merge logic."""

from __future__ import annotations

from datetime import UTC, datetime

from custom_components.njord.coordinator import merge_enrichment
from custom_components.njord.models import (
    AlertData,
    ConsensusData,
    DerivedData,
    EnrichmentData,
    HistoryData,
    IndexData,
    TrendData,
)


def _base_enrichment() -> EnrichmentData:
    return EnrichmentData(
        location="home",
        alerts=[AlertData(type="frost", severity="yellow", confidence=0.8)],
        indices=IndexData(laundry=80, outdoor=60),
        trends=TrendData(stability_label="stable"),
        derived=DerivedData(sunshine_pct=70.0),
        history=HistoryData(seasonal_best="icon_d2"),
        consensus=ConsensusData(hourly_parameters=[]),
    )


def test_alert_merge_preserves_indices() -> None:
    base = _base_enrichment()
    event = EnrichmentData(
        location="home",
        alerts=[AlertData(type="storm", severity="red", confidence=1.0)],
    )
    result = merge_enrichment(base, event)

    assert result.alerts == event.alerts
    assert result.indices == base.indices
    assert result.trends == base.trends


def test_index_merge_preserves_alerts() -> None:
    base = _base_enrichment()
    new_indices = IndexData(laundry=90, outdoor=50)
    event = EnrichmentData(location="home", indices=new_indices)
    result = merge_enrichment(base, event)

    assert result.indices == new_indices
    assert result.alerts == base.alerts
    assert result.trends == base.trends


def test_all_enrichment_types_merge_independently() -> None:
    base = _base_enrichment()

    fields_and_values = [
        ("alerts", [AlertData(type="heat", severity="orange", confidence=0.9)]),
        ("indices", IndexData(laundry=99)),
        ("trends", TrendData(stability_label="changing")),
        ("derived", DerivedData(sunshine_pct=20.0)),
        ("history", HistoryData(seasonal_best="gfs")),
        ("consensus", ConsensusData(hourly_parameters=[])),
    ]

    for field_name, value in fields_and_values:
        event = EnrichmentData(location="home", **{field_name: value})
        result = merge_enrichment(base, event)
        assert getattr(result, field_name) == value

        for other_name, _ in fields_and_values:
            if other_name != field_name:
                assert getattr(result, other_name) == getattr(base, other_name), (
                    f"Merging {field_name} should not affect {other_name}"
                )


def test_merge_with_no_existing_base() -> None:
    event = EnrichmentData(
        location="zurich",
        alerts=[AlertData(type="uv", severity="yellow", confidence=0.5)],
    )
    result = merge_enrichment(None, event)

    assert result.location == "zurich"
    assert result.alerts == event.alerts
    assert result.indices is None
    assert result.trends is None
    assert result.derived is None
    assert result.history is None
    assert result.consensus is None


def test_merge_empty_event_returns_existing() -> None:
    base = _base_enrichment()
    event = EnrichmentData(location="home")
    result = merge_enrichment(base, event)

    assert result is base


def test_merge_preserves_immutability() -> None:
    base = _base_enrichment()
    event = EnrichmentData(
        location="home",
        alerts=[AlertData(type="fog", severity="yellow", confidence=0.6)],
    )
    result = merge_enrichment(base, event)

    assert result is not base
    assert base.alerts[0].type == "frost"
    assert result.alerts[0].type == "fog"


def test_consensus_merge_updates_timestamp() -> None:
    ts_old = datetime(2026, 7, 15, 14, 0, tzinfo=UTC)
    ts_new = datetime(2026, 7, 15, 17, 0, tzinfo=UTC)
    base = EnrichmentData(
        location="home",
        consensus=ConsensusData(hourly_parameters=[]),
        consensus_updated_at=ts_old,
    )
    event = EnrichmentData(
        location="home",
        consensus=ConsensusData(hourly_parameters=[]),
        consensus_updated_at=ts_new,
    )
    result = merge_enrichment(base, event)
    assert result.consensus_updated_at == ts_new


def test_non_consensus_merge_preserves_timestamp() -> None:
    ts = datetime(2026, 7, 15, 14, 0, tzinfo=UTC)
    base = EnrichmentData(
        location="home",
        consensus=ConsensusData(hourly_parameters=[]),
        consensus_updated_at=ts,
    )
    event = EnrichmentData(
        location="home",
        alerts=[AlertData(type="storm", severity="red", confidence=1.0)],
    )
    result = merge_enrichment(base, event)
    assert result.consensus_updated_at == ts
    assert result.alerts == event.alerts


def test_derived_merge_updates_derived_timestamp() -> None:
    ts_old = datetime(2026, 7, 15, 14, 0, tzinfo=UTC)
    ts_new = datetime(2026, 7, 15, 17, 0, tzinfo=UTC)
    base = EnrichmentData(
        location="home",
        derived=DerivedData(sunshine_pct=70.0),
        derived_updated_at=ts_old,
    )
    event = EnrichmentData(
        location="home",
        derived=DerivedData(sunshine_pct=50.0),
        derived_updated_at=ts_new,
    )
    result = merge_enrichment(base, event)
    assert result.derived_updated_at == ts_new
    assert result.derived.sunshine_pct == 50.0


def test_non_derived_merge_preserves_derived_timestamp() -> None:
    ts = datetime(2026, 7, 15, 14, 0, tzinfo=UTC)
    base = EnrichmentData(
        location="home",
        derived=DerivedData(sunshine_pct=70.0),
        derived_updated_at=ts,
    )
    event = EnrichmentData(
        location="home",
        alerts=[AlertData(type="storm", severity="red", confidence=1.0)],
    )
    result = merge_enrichment(base, event)
    assert result.derived_updated_at == ts
