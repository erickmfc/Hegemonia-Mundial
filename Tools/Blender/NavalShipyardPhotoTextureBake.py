"""Bake compact shipyard albedos from textures already present in the game.

Run with the project's regular Python before regenerating the Blender scene.
Only four 512 px outputs are authored here; the procedural generator makes the
remaining materials and the light normal maps. Pillow and NumPy are required.
"""

from pathlib import Path

import numpy as np
from PIL import Image

from NavalShipyardTextureGenerator import _write_rgb_png


PROJECT_ROOT = Path(__file__).resolve().parents[2]
OUTPUT = PROJECT_ROOT / "Assets/Game/Environment/NavalShipyard/Textures"
SOURCES = {
    "MAT_Concrete_Naval": (
        "Assets/HQ Hangar Free/Textures/Concrete_floor/Concrete_floor_smooth_BaseMap.png",
        (0.62, 0.63, 0.60), 0.90, None,
    ),
    "MAT_Concrete_Light": (
        "Assets/HQ Hangar Free/Textures/Concrete_floor/Concrete_floor_smooth_BaseMap.png",
        (0.73, 0.74, 0.70), 0.73, None,
    ),
    "MAT_RoofMetal": (
        "Assets/HQ Hangar Free/Textures/Sheet_metal/Sheet_metal_BaseMap.png",
        (0.71, 0.73, 0.73), 0.85, (0, 0, 1024, 512),
    ),
    "MAT_Steel_Grey": (
        "Assets/Industrial Props/Textures/Seamless Metal.jpg",
        (0.27, 0.31, 0.33), 0.45, None,
    ),
}


def seamless(array: np.ndarray) -> np.ndarray:
    """Remove edge colour differences without hiding the source's fine detail."""
    width = array.shape[1]
    height = array.shape[0]
    ramp_x = np.linspace(0.0, 1.0, width, dtype=np.float32)[None, :, None]
    array = array - (array[:, -1:, :] - array[:, :1, :]) * ramp_x
    ramp_y = np.linspace(0.0, 1.0, height, dtype=np.float32)[:, None, None]
    return array - (array[-1:, :, :] - array[:1, :, :]) * ramp_y


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, (source, base, contrast, crop) in SOURCES.items():
        path = PROJECT_ROOT / source
        with Image.open(path) as image:
            image = image.convert("RGB")
            if crop is not None:
                image = image.crop(crop)
            image = image.resize((512, 512), Image.Resampling.LANCZOS)
            sample = np.asarray(image, dtype=np.float32) / 255.0
        sample = seamless(sample)
        if name == "MAT_RoofMetal":
            # The source panel has broad lighting bands along its length.
            # Remove them so repeated roof tiles show only the corrugation.
            row_bias = sample.mean(axis=1, keepdims=True) - sample.mean(axis=(0, 1), keepdims=True)
            sample -= row_bias * 0.93
        detail = (sample - sample.mean(axis=(0, 1), keepdims=True)) * contrast
        pixels = np.array(base, dtype=np.float32)[None, None, :] + detail
        if name.startswith("MAT_Concrete"):
            # One subtle expansion joint per UV tile, legible in the RTS view.
            edge = np.zeros((512, 512, 1), dtype=np.float32)
            edge[:12, :, :] = 1.0
            edge[:, :12, :] = 1.0
            pixels *= 1.0 - edge * (0.20 if name == "MAT_Concrete_Naval" else 0.15)
        pixels = np.clip(np.rint(pixels * 255.0), 0, 255).astype(np.uint8)
        output = OUTPUT / f"{name}_Albedo.png"
        _write_rgb_png(output, pixels)
        print(f"BAKED_TEXTURE={output}")


if __name__ == "__main__":
    main()
