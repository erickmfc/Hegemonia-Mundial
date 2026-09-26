# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `C:/Users/Mathe/Desktop/Hegemonia-Mundial-main/Hegemonia-Mundial-main`
- Last analyzed: 2026-09-26
- Last analyzed commit: `96b6e3135d4dce35a938c6c7d9ea515951811f0a`

## Confirmed Environment

- Unity version: 6000.2.15f1 (revision `0707b6d1e918`)
- Render pipeline: URP 17.2.0
- Input system: Both input handlers enabled (`activeInputHandler: 2`); existing RTS bindings wrap `UnityEngine.Input`.
- Target platforms: Not confirmed from the inspected settings.

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Rendering | URP | Confirmed | `Packages/manifest.json`, `ProjectSettings/GraphicsSettings.asset` |
| Input | Input System package and legacy input API are both available | Confirmed | `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset`, `Assets/scripts/RTS/RTSInputBindings.cs` |
| Navigation | AI Navigation 2.0.12 | Confirmed | `Packages/manifest.json` |
| Camera | Cinemachine 3.1.5 is installed | Confirmed | `Packages/manifest.json` |
| Tests | Unity Test Framework 1.6.0 | Confirmed | `Packages/manifest.json`, `Assets/Tests/*/*.asmdef` |
| Unity MCP | `com.coplaydev.unity-mcp` is in the manifest; no callable Unity MCP tools are exposed to this task | Confirmed | `Packages/manifest.json`, current tool inventory |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/scripts/` | Gameplay and systems; most first-party runtime code | Confirmed | Representative sources |
| `Assets/scripts/RTS/` | RTS session, input, visibility, objectives, resource services | Confirmed | Source files |
| `Assets/scripts/Governo/` | Country, economy, diplomacy, and government systems | Confirmed | Source files |
| `Assets/scripts/Mapa/` | Map-region runtime and expansion territories | Confirmed | Source files |
| `Assets/Scenes/`, `Assets/_Recovery/` | Scenes; canonical editable gameplay scenes are under `_Recovery` | Confirmed | `ConfiguracaoCenasJogo.cs`, `EditorBuildSettings.asset` |
| `Assets/Resources/` | Runtime-loaded ScriptableObject and other named resources | Confirmed | Existing resource assets and code |
| `Assets/Tests/` | EditMode and PlayMode test assemblies | Confirmed | Test assembly definitions |
| `Docs/` | Project architecture and feature documentation | Confirmed | Existing documents |

## Assembly Boundaries

| Assembly | Responsibility | Key references | Notes |
| --- | --- | --- | --- |
| `Assembly-CSharp` | Main project gameplay and runtime systems | Unity predefined assembly | First-party scripts are predominantly here; no first-party runtime `.asmdef` was found. |
| `Hegemonia.EditMode.Tests` | EditMode tests | Reflection-based access to runtime types | Editor-only test assembly, no explicit production assembly references. |
| `Hegemonia.PlayMode.Tests` | PlayMode tests | Test assemblies | Separate test assembly. |

## Scenes And Startup Flow

- Build scenes: `_Recovery/Cena menu P`, `_Recovery/cena19)`, `_Recovery/Md Historia`, `_Recovery/Tutorial`, `_Recovery/Ano1`, `_Recovery/demo1`, `_Recovery/teste` are enabled.
- Likely startup scene: `_Recovery/Cena menu P.unity`, first enabled build scene.
- Scene loading flow: `FluxoInicialJogo` routes standalone builds through the canonical menu and ensures campaign runtime systems after gameplay scene loads. `ConfiguracaoCenasJogo` maps canonical and legacy paths.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Gameplay | MonoBehaviour-centered systems in a large predefined runtime assembly | Confirmed | Representative gameplay sources and assembly inventory |
| Data | Serializable classes and ScriptableObjects for authored and saved state | Confirmed | Government/save models and resource assets |
| Services | Static singleton accessors with runtime bootstrap and scene-lifecycle setup | Confirmed | `RTSRuntimeBootstrap.cs`, `SistemaGovernoMundial.cs`, `GerenteDeTerritorio.cs` |
| Events | C# events for state changes and registry updates | Confirmed | `RTSVisibilityService.cs`, `RegistroEntidadesJogo.cs`, government systems |
| Save | `SistemaSaveGame` serializes `DadosDoJogo` with `JsonUtility` and explicit capture/restore hooks | Confirmed | `SistemaSaveGame.cs` |

## Coding Conventions

- Namespace style: Legacy/global namespace remains common; newer RTS and AI systems use feature namespaces.
- Serialized fields: Public fields are common in older systems; newer code favors private `[SerializeField]` fields and properties.
- Async: Not assessed for the territory task; inspected systems primarily use Unity lifecycle methods and synchronous operations.
- Comments/docs: Portuguese comments and XML summaries are common.

## Testing And Validation

- EditMode tests: Available under `Assets/Tests/EditMode`; includes `AISovereignTerritoryTests`.
- PlayMode tests: Available under `Assets/Tests/PlayMode`, including naval and aircraft behavior tests.
- CI/build validation: No CI workflow or single canonical test command was confirmed in this audit.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity Editor / console / scene inspection MCP | unavailable to this task | No callable Unity MCP tools are exposed. |
| Unity MCP package | available in project configuration, connection unverified | Package listed in manifest and lock file. |
| Unity CLI | unverified | Editor executable location not confirmed yet. |
| Unity Test Framework | available | Package and test assemblies present. |

## Important Constraints

- Follow `AGENTS.md` and `REGRAS_FIXAS_ARQUITETURA.md` before edits.
- Preserve existing serialized references and scene behavior; do not edit scenes, prefabs, terrain, radar detection, movement, or economy unless the requested integration proves it necessary.
- The current working tree contains user changes in recovery scenes and untracked recovery/test artifacts. Treat them as user-owned and preserve them.
- `GerenteDeTerritorio` currently uses marker-centered square influence and expansion zones; `GerenciadorDivisaoTerritorial` separately uses marker radii for city/economic calculations.
- `MapaGeralController` owns the existing M shortcut, orthographic map camera, unit-order interaction, zoom/pan, and fog-aware icon display.
- `RTSVisibilityService.TeamsAtWar` participates in both detection and targeting; detection must remain independent from territorial attack authorization.

## Unknowns And Confidence

- Physical world extents and final scene bounds are being handled in a separate map-building task; derive political map bounds from explicit authored configuration or the active world's terrain coverage.
- The exact association of land polygons to five country IDs must follow the drawn boundaries, not fill colors. Repeated colors may represent different countries or biomes.
- Native Unity Editor availability and the Unity MCP connection are unverified.

## Source Files Inspected

- `AGENTS.md`, `REGRAS_FIXAS_ARQUITETURA.md`
- `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/ProjectSettings.asset`
- `Packages/manifest.json`, `Packages/packages-lock.json`
- `Assets/scripts/GerenteDeTerritorio.cs`, `MarcadorTerritorio.cs`, `Mapa/GerenciadorExpansaoFronteira.cs`, `Governo/GerenciadorDivisaoTerritorial.cs`
- `Assets/scripts/Governo/SistemaGovernoMundial.cs`, `Governo/DadosPaisGoverno.cs`, `RTS/RTSVisibilityService.cs`, `RTS/RTSInputBindings.cs`, `MapaGeralController.cs`, `SistemaSaveGame.cs`
- `Assets/scripts/Construtor.cs`, `IdentidadeUnidade.cs`, `ComplexoGovernamental.cs`, `RegistroEntidadesJogo.cs`, `RTS/RTSRuntimeBootstrap.cs`, `ConfiguracaoCenasJogo.cs`, `Menus/FluxoInicialJogo.cs`
- `Assets/Tests/EditMode/AISovereignTerritoryTests.cs` and test assembly definitions

<!-- unity-onboarding:generated:end -->
