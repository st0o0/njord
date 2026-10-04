"""Shared horizon offset utilities for time-based enrichment lookups."""

from __future__ import annotations

from datetime import UTC, datetime


def current_horizon_offset(updated_at: datetime | None) -> int:
    if updated_at is None:
        return 0
    elapsed = (datetime.now(UTC) - updated_at).total_seconds()
    return max(0, int(elapsed // 3600))


def get_horizon_entry[T](horizons: list[T], offset: int) -> T | None:
    target = f"h{offset}"
    for entry in horizons:
        if entry.horizon == target:  # type: ignore[attr-defined]
            return entry
    return None
