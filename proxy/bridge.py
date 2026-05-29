"""Python client for InventorBridge.exe — stdin/stdout JSON.

The bridge handles geometry reading (inspect) and AttributeSet reading
that Python COM cannot do.  Tag writing is managed in-memory by Python.
"""

from __future__ import annotations

import json
import logging
import subprocess
import sys
from pathlib import Path

log = logging.getLogger(__name__)

_BRIDGE_EXE = "InventorBridge.exe"


class InventorBridge:
    """Manages a long-lived InventorBridge.exe subprocess."""

    def __init__(self) -> None:
        self._proc: subprocess.Popen | None = None

    def connect(self) -> dict:
        """Spawn the bridge and connect to Inventor."""
        if self._proc is not None:
            return self._send("connect")

        exe_path = self._find_exe()
        self._proc = subprocess.Popen(
            [str(exe_path)],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
        )
        return self._send("connect")

    def close(self) -> None:
        """Terminate the bridge subprocess."""
        if self._proc is not None:
            try:
                self._proc.stdin.close()
                self._proc.terminate()
                self._proc.wait(timeout=3)
            except Exception:
                self._proc.kill()
            self._proc = None

    def revolve(
        self, sketch: int, axis: int, angle: float = 360.0,
        operation: str = "join",
    ) -> dict:
        """Create a revolve feature via the bridge (AddForSolid works in C#)."""
        return self._send("revolve", {
            "sketch": sketch,
            "axis": axis,
            "angle": str(angle),
            "operation": operation,
        })
        """List all entities in a sketch with geometry and tags.

        Returns raw bridge response dict.
        """
        return self._send("inspect", {"sketch": sketch})

    def _send(self, action: str, params: dict | None = None) -> dict:
        if self._proc is None:
            return {"ok": False, "error": "Bridge not started"}
        request = {"action": action}
        if params:
            request.update(params)
        try:
            line = json.dumps(request, ensure_ascii=False)
            self._proc.stdin.write(line + "\n")
            self._proc.stdin.flush()
            response_line = self._proc.stdout.readline()
            if not response_line:
                return {"ok": False, "error": "Bridge closed"}
            return json.loads(response_line)
        except (BrokenPipeError, OSError, json.JSONDecodeError) as exc:
            return {"ok": False, "error": str(exc)}

    @staticmethod
    def _find_exe() -> Path:
        candidates = [
            Path(__file__).resolve().parent / _BRIDGE_EXE,
            Path(sys.prefix) / "bin" / _BRIDGE_EXE,
        ]
        for p in candidates:
            if p.exists():
                return p
        raise FileNotFoundError(
            f"{_BRIDGE_EXE} not found next to bridge.py"
        )
