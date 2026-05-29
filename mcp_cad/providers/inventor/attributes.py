"""Tag store and sketch inspector for Inventor entities.

- ``TagStore``: in-memory mapping of ``(sketch_index, entity_index) → tag``.
  Tags survive the session but not across restarts.
- ``inspect_sketch``: delegates to the C# bridge for geometry + persistent
  tag reading (from AttributeSets, when available).
- ``bridge_revolve``: delegates revolve to C# (AddForSolid works there).
"""

from __future__ import annotations

import logging
import sys
from pathlib import Path
from typing import Any

# proxy/ is at the project root, outside the mcp_cad package.
_PROXY_DIR = Path(__file__).resolve().parent.parent.parent.parent / "proxy"
if str(_PROXY_DIR) not in sys.path:
    sys.path.insert(0, str(_PROXY_DIR))

from bridge import InventorBridge

log = logging.getLogger(__name__)

_bridge: InventorBridge | None = None
_store: dict[tuple[int, int], str] = {}


class TagStore:
    """In-memory tag registry for sketch entities."""

    @staticmethod
    def set_tag(sketch_index: int, entity_index: int, tag: str) -> None:
        _store[(sketch_index, entity_index)] = tag

    @staticmethod
    def get_tag(sketch_index: int, entity_index: int) -> str | None:
        return _store.get((sketch_index, entity_index))

    @staticmethod
    def resolve(sketch_index: int, tag: str) -> int | None:
        for (si, ei), t in _store.items():
            if si == sketch_index and t == tag:
                return ei
        return None


def _get_bridge() -> InventorBridge:
    global _bridge
    if _bridge is None:
        _bridge = InventorBridge()
        result = _bridge.connect()
        if not result.get("ok"):
            _bridge.close()
            _bridge = None
            raise RuntimeError(f"Bridge failed: {result.get('error')}")
    return _bridge


def inspect_sketch(sketch_index: int) -> dict | None:
    """List all entities with geometry and tags via the C# bridge."""
    try:
        bridge = _get_bridge()
        result = bridge.inspect(sketch_index)
        if not result.get("ok"):
            return None
        data = result.get("data", {})
        for ent in data.get("entities", []):
            tag = ent.get("tag")
            if tag:
                TagStore.set_tag(sketch_index, ent["index"], tag)
        return data
    except Exception as exc:
        log.debug("Inspect failed: %s", exc)
        return None


def bridge_revolve(
    sketch_index: int,
    axis_sketchline_index: int,
    angle: float = 360.0,
    operation: str = "join",
) -> dict:
    """Delegate revolve to C# bridge."""
    bridge = _get_bridge()
    return bridge.revolve(sketch_index, axis_sketchline_index, angle, operation)
