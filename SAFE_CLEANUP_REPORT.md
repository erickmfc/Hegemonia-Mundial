# Safe Cleanup Report

Date: 2026-07-07

## Removed From Main Unity Repo

The following directories were removed after reference checks confirmed they were outside the active build and had no direct reuse signal in the main game scenes/prefabs:

| Path | Approx. Size Removed |
| --- | ---: |
| `Assets/TextMesh Pro/Examples & Extras` | 5.60 MB |
| `Assets/UnityTechnologies/EffectExamples` | 298.30 MB |
| `Assets/SUIMONO - WATER SYSTEM 2/_DEMO` | 71.81 MB |
| `Assets/Simple InterceptMissile&TurretBehaviour/ExampleScene` | 0.96 MB |
| `Assets/CityVoxelPack/DemoScenes` | 4.37 MB |
| `Assets/Civil Transport Aircraft/Scenes` | 0.02 MB |
| `Assets/CartoonMilitaryModelPack/SampleScene` | 0.23 MB |
| `Assets/base militar/Military Base Pack/Demo` | 98.47 MB |
| `Assets/efeitos/WarFX/Demo` | 3.41 MB |
| `_backup_scripts_error` | 0.01 MB |

Approximate total removed from the main repo: `483.18 MB`

## Quarantined Outside The Repo

The following external copies and loose logs were moved instead of deleted, so the cleanup stays reversible:

- Quarantine directory: `C:\Users\Mathe\Desktop\Hegemonia-Mundial-main\_cleanup_quarantine_20260707-211336`
- Moved: `main-worktree` (`~6.74 GB`)
- Moved: root-level `Assets` folder outside `.git`
- Moved: `stabilization-unity.log`
- Moved: `unity-batch.log`
- Moved: `unity-codex-check.log`
- Moved: `unity-compile.log`
- Moved: `unity-market-test.log`
- Moved: `unity-stderr.log`
- Moved: `unity-stdout.log`

## Duplicate Code Findings Kept Intact

The cleanup removed duplicate/example scripts that lived inside third-party demo folders. The following duplicate gameplay-side symbols were detected but intentionally not changed in this pass:

- `ContatoNavalIA` in `Assets/scripts/IA_Dominadora.cs` and `Assets/scripts/IA_Suprema.cs`
- `GrupoNavalIA` in `Assets/scripts/IA_Dominadora.cs` and `Assets/scripts/IA_Suprema.cs`
- `RaycastHitDistanceComparer` in `Assets/scripts/Construtor.cs` and `Assets/scripts/GerenteSelecao.cs`
- `ControllerState` in `Assets/scripts/IA/NovaIA/IA_RuntimeCoordinator.cs` and `Assets/scripts/IA/Sovereign/AISovereignCore.cs`

These were left untouched because they appear to belong to active gameplay/runtime code rather than imported samples.

## Validation Notes

- `ProjectSettings/EditorBuildSettings.asset` still lists the same 4 build scenes:
  - `Assets/_Recovery/Menu/Menu cena.unity`
  - `Assets/Scenes/MenuPrincipal.unity`
  - `Assets/Scenes/SampleScene.unity`
  - `Assets/_Recovery/Tutorial/tutorial.unity`
- All targeted cleanup directories now return `Exists = False`.
- Unity batch startup via direct `-logFile` exited with `return code 1` and produced only minimal startup logs:
  - `cleanup-unity-compile.log`
  - `cleanup-unity-compile-2.log`
- Unity's global `Editor.log` provided the stronger verification signal after refresh/open:
  - `AssetDatabase: script compilation time: 8.217101s`
  - `*** Tundra build success`
  - `Loaded scene 'Assets/_Recovery/cena19).unity'`
  - `[MissingScriptLocator] Nenhum script faltando encontrado nas cenas abertas.`
- Residual non-blocking warnings still exist in the editor environment, including Cinemachine sample asmref warnings and WarFX shader fallback warnings, but no cleanup-specific compile failure was detected in the refreshed project load.

## Recommended Next Pass

- Open the project once in the editor and inspect the Console for scene or prefab references that only appear during full asset refresh.
- If the editor stays clean, the quarantine directory can later be deleted permanently.
- Handle the kept duplicate gameplay symbols in a separate refactor pass with dedicated testing.
