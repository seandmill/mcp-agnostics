"""
JSON file persistence for simulation runs.
Uses asyncio.Lock for single-process concurrency protection.
"""

import asyncio
import json
import os
import tempfile
import uuid
from pathlib import Path
from typing import Any

import aiofiles

STORAGE_FILE = Path(__file__).parent / "simulations.json"

_lock = asyncio.Lock()


def generate_run_id(seed: int | None = None) -> str:
    """Generate a deterministic UUID if seed is provided, otherwise random."""
    if seed is not None:
        import random
        rng = random.Random(seed)
        return str(uuid.UUID(int=rng.getrandbits(128), version=4))
    return str(uuid.uuid4())


async def _read_storage() -> dict[str, Any]:
    """Read the storage file, returning empty dict if not exists."""
    if not STORAGE_FILE.exists():
        return {}
    async with aiofiles.open(STORAGE_FILE, "r") as f:
        content = await f.read()
        return json.loads(content) if content.strip() else {}


async def _write_storage(data: dict[str, Any]) -> None:
    """Write storage atomically via temp file + rename."""
    dir_path = STORAGE_FILE.parent
    fd, tmp_path = tempfile.mkstemp(dir=dir_path, suffix=".tmp")
    try:
        os.close(fd)
        async with aiofiles.open(tmp_path, "w") as f:
            await f.write(json.dumps(data, indent=2))
        os.replace(tmp_path, STORAGE_FILE)
    except Exception:
        if os.path.exists(tmp_path):
            os.unlink(tmp_path)
        raise


async def save_simulation(run_id: str, data: dict[str, Any]) -> None:
    """Save a simulation run to storage."""
    async with _lock:
        storage = await _read_storage()
        storage[run_id] = data
        await _write_storage(storage)


async def get_simulation(run_id: str) -> dict[str, Any] | None:
    """Retrieve a simulation run by ID."""
    async with _lock:
        storage = await _read_storage()
        return storage.get(run_id)


async def list_simulations() -> list[str]:
    """List all simulation run IDs."""
    async with _lock:
        storage = await _read_storage()
        return list(storage.keys())
