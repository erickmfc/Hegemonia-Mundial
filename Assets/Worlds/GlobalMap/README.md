# Global Map RTS

Open `Assets/Scenes/GlobalMapRTS.unity` for the isolated 512 km world scene.

## Geometry and environment

- `References/fase.png` is the only source for island placement and coastline shape. The map keeps the source image's 16:9 aspect ratio inside a 512 km × 288 km terrain footprint, centered in a 512 km ocean area.
- `References/referencia_biomas_vista_superior.png` supplies broad relief and biome weights. The 3D and Sea Power screenshots remain appearance references; no image is generated or substituted.
- The scene has one existing `Assets/Mar_Feito/Models/Sea.prefab` instance and one `OceanAdvanced` component. Mean sea level stays at Y=0; ships continue to use `OceanAdvanced.GetWaterHeight` and its existing wave model.
- The beach floor rises to at least Y=4 m to stay above the current wave model's summed 2.6 m amplitude.
- The coarse terrain includes the underwater floor for visual and physical continuity. It starts inactive for one frame so the existing surface initializer skips its world-sized rectangular land marker; the streamer then enables it, and local high-detail tiles register as `Chao` as they stream in. Naval water checks continue to hit the single Sea collider first.

## Runtime controls and streaming

The main camera starts at strategic height. Mouse wheel / Space / Ctrl zoom; WASD pans; Shift increases movement speed. Control sensitivity scales with altitude only in this scene. At low altitude, 8 km terrain tiles stream around the camera; at strategic height, the coarse Terrain represents the world.

The on-screen diagnostics report FPS, profiler CPU/GPU frame counters when the active graphics backend exposes them, terrain tiles/trees, active scene GameObjects, tile-build time, Unity/Mono memory, and graphics-driver memory.

## Rebuild and validation

Use **Hegemonia → World → Build Global Map RTS Scene** or run `GlobalWorldSceneBuilder.BuildGlobalWorldScene` in batch mode. This rebuilds only the profile, coarse Terrain asset, and this scene. **Hegemonia → World → Validate Global Map RTS Scene** checks the official-mask samples, sea level, single-ocean invariant, Terrain layers, streamer, and camera wiring.
