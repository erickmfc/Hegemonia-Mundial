"""Small, seamless texture set shared by the Blender and Unity shipyard assets.

All patterns wrap at the image edges, use a fixed seed, and are kept at 512 px
because they repeat in metre-scale UVs across a strategy-game environment.
"""

from __future__ import annotations

import struct
import zlib
from pathlib import Path

import numpy as np


TEXTURE_KIND = {
    "MAT_Concrete_Naval": "concrete",
    "MAT_Concrete_Light": "concrete_light",
    "MAT_Steel_Grey": "painted_metal",
    "MAT_GalvanizedMetal": "galvanized",
    "MAT_RoofMetal": "roof",
    "MAT_SafetyYellow": "paint",
    "MAT_Asphalt": "asphalt",
    "MAT_DarkSteel": "dark_steel",
    "MAT_Glass": "glass",
    "MAT_Water": "water",
    "MAT_Green": "coastal_ground",
    "MAT_Foliage_Olive": "coastal_ground",
    "MAT_White_Marking": "paint",
    "MAT_Red_Safety": "paint",
}

NORMAL_KINDS = {
    "concrete", "concrete_light", "painted_metal", "galvanized",
    "roof", "asphalt", "dark_steel", "water",
}

# Albedo baked from industrial texture assets already shipped in this Unity
# project. The separate photo bake script recreates them; Blender keeps these
# files intact when rebuilding the model and still refreshes their normal maps.
PHOTO_ALBEDO_NAMES = {
    "MAT_Concrete_Naval", "MAT_Concrete_Light", "MAT_RoofMetal", "MAT_Steel_Grey",
}


def _periodic_noise(rng, size: int, cells: int) -> np.ndarray:
    """Bilinear value noise with a toroidal lattice."""
    lattice = rng.random((cells, cells), dtype=np.float32)
    positions = np.arange(size, dtype=np.float32) * (cells / size)
    lower = positions.astype(np.int32)
    upper = (lower + 1) % cells
    blend = positions - lower
    blend = blend * blend * (3.0 - 2.0 * blend)
    x0, x1 = lower[None, :], upper[None, :]
    y0, y1 = lower[:, None], upper[:, None]
    tx, ty = blend[None, :], blend[:, None]
    return (
        lattice[y0, x0] * (1 - tx) * (1 - ty)
        + lattice[y0, x1] * tx * (1 - ty)
        + lattice[y1, x0] * (1 - tx) * ty
        + lattice[y1, x1] * tx * ty
    )


def _png_chunk(tag: bytes, data: bytes) -> bytes:
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)


def _write_rgb_png(path: Path, pixels: np.ndarray) -> None:
    height, width, channels = pixels.shape
    if channels != 3 or pixels.dtype != np.uint8:
        raise ValueError("Expected an RGB uint8 image.")
    rows = b"".join(b"\x00" + pixels[row].tobytes() for row in range(height))
    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    path.write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + _png_chunk(b"IHDR", header)
        + _png_chunk(b"IDAT", zlib.compress(rows, level=6))
        + _png_chunk(b"IEND", b"")
    )


def _material_pattern(kind: str, rng, size: int) -> tuple[np.ndarray, np.ndarray]:
    coarse = _periodic_noise(rng, size, 6)
    medium = _periodic_noise(rng, size, 24)
    fine = _periodic_noise(rng, size, 96)
    xx = np.arange(size, dtype=np.float32)[None, :]
    yy = np.arange(size, dtype=np.float32)[:, None]
    grain = (coarse - 0.5) * 0.08 + (medium - 0.5) * 0.07 + (fine - 0.5) * 0.035
    variation = grain.copy()
    height = medium * 0.08 + fine * 0.025

    if kind in {"concrete", "concrete_light"}:
        joints = ((xx < 14) | (yy < 14)).astype(np.float32)
        stains = np.maximum(coarse - 0.59, 0) * 0.14
        variation += (coarse - 0.5) * 0.15 + (medium - 0.5) * 0.11 - stains - joints * (0.17 if kind == "concrete" else 0.13)
        height -= joints * 0.06
    elif kind == "roof":
        phase = (xx * 8 / size) % 1.0
        rib = np.exp(-((phase - 0.28) / 0.08) ** 2)
        groove = np.exp(-((phase - 0.44) / 0.06) ** 2)
        variation += rib * 0.17 - groove * 0.12
        height += rib * 0.18 - groove * 0.04
    elif kind in {"painted_metal", "galvanized", "dark_steel"}:
        phase = (xx * 4 / size) % 1.0
        seam = (phase < 0.018).astype(np.float32)
        brushed = np.sin(2 * np.pi * xx * 24 / size) * 0.012
        variation += brushed - seam * 0.08
        if kind == "galvanized":
            variation += (fine - 0.5) * 0.08
        height += brushed * 0.8 - seam * 0.045
    elif kind == "asphalt":
        variation += (fine - 0.5) * 0.13 + (medium - 0.5) * 0.08
        height += fine * 0.06
    elif kind == "water":
        wave = np.sin(2 * np.pi * (yy * 5 / size + (coarse - 0.5) * 0.18))
        variation = (coarse - 0.5) * 0.10 + wave * 0.035 + (fine - 0.5) * 0.012
        height = wave * 0.025 + medium * 0.014
    elif kind == "coastal_ground":
        variation += (coarse - 0.5) * 0.14 + (medium - 0.5) * 0.11
    elif kind == "glass":
        variation = np.sin(2 * np.pi * xx * 3 / size) * 0.035 + (coarse - 0.5) * 0.025
    elif kind == "paint":
        variation = (medium - 0.5) * 0.035 + (fine - 0.5) * 0.02

    return variation, height


def _normal_map(height: np.ndarray, strength: float = 4.0) -> np.ndarray:
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * strength
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * strength
    normal = np.stack((-dx, -dy, np.ones_like(height)), axis=2)
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    return np.clip(np.rint((normal * 0.5 + 0.5) * 255), 0, 255).astype(np.uint8)


def generate_textures(directory: Path, palette: dict, size: int = 512) -> dict[str, tuple[Path, Path | None]]:
    directory.mkdir(parents=True, exist_ok=True)
    result = {}
    for index, (name, (rgba, _, _)) in enumerate(palette.items()):
        kind = TEXTURE_KIND[name]
        rng = np.random.default_rng(8700 + index)
        variation, height = _material_pattern(kind, rng, size)
        base = np.array(rgba[:3], dtype=np.float32)
        rgb = np.clip(np.rint(base[None, None, :] * (1.0 + variation[:, :, None]) * 255), 0, 255).astype(np.uint8)
        albedo = directory / f"{name}_Albedo.png"
        if name not in PHOTO_ALBEDO_NAMES or not albedo.exists():
            _write_rgb_png(albedo, rgb)
        normal = None
        if kind in NORMAL_KINDS:
            normal = directory / f"{name}_Normal.png"
            _write_rgb_png(normal, _normal_map(height))
        result[name] = (albedo, normal)
    return result
