#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hegemonia.AI.IA01;
using Hegemonia.AI.IA02;

namespace Hegemonia.AI.IA02.EditorTools
{
    /// <summary>
    /// Configuração idempotente da segunda IA na cena jogável. A ferramenta só
    /// cria objetos com tipos IA02 e nunca altera os objetos IA01 de origem.
    /// </summary>
    public static class IA02CampaignSetup
    {
        private const string CampaignScenePath = "Assets/Scenes/cena19).unity";
        private const string ProfilePath = "Assets/IA02/Profiles/IA02NationProfile.asset";
        private const string PlanPath = "Assets/IA02/BuildPlans/IA02BuildPlan.asset";
        private const string SourceProfilePath = "Assets/Prefabs/Imobiliario/Prefeitura/IA01NationProfile.asset";
        private const string SourcePlanPath = "Assets/Prefabs/IA01Controller_IA01Build.asset";
        private const string RootName = "IA02 Runtime - Uniao Carmesim";
        private const int Ia02NationId = 3;
        private const int Ia02TeamId = 3;
        private const float MinimumTerritorySeparation = 5000f;
        private const float TerritorySearchStep = 500f;
        private const float TerritoryFootprintPadding = 120f;

        [MenuItem("Hegemonia/IA02/Configurar campanha cena19)", priority = 1)]
        public static void ConfigureCampaign()
        {
            ConfigureScene(CampaignScenePath, RootName);
        }

        [MenuItem("Hegemonia/IA02/Configurar tutorial demo1", priority = 1)]
        public static void ConfigureTutorial()
        {
            ConfigureScene(ConfiguracaoCenasJogo.CaminhoCenaTutorialCanonica, RootName + " (Demo)");
        }

        private static void ConfigureScene(string scenePath, string rootName)
        {
            Scene scene = OpenScene(scenePath);
            IA02Manager existing = UnityEngine.Object.FindFirstObjectByType<IA02Manager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                RepairExistingInfrastructure(existing, scene);
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            IA01Controller sourceController = UnityEngine.Object.FindFirstObjectByType<IA01Controller>(FindObjectsInactive.Include);
            IA01CityLayout sourceLayout = FindSourceLayout(sourceController);
            if (sourceController == null || sourceLayout == null)
            {
                Debug.LogError("[IA02] Não foi possível localizar IA01Controller e IA01CityLayout na cena ativa.");
                return;
            }

            IA02NationProfile profile = EnsureProfile();
            IA02BuildPlan plan = EnsureBuildPlan();
            Vector3 rootPosition = FindFreeTerritoryPosition(sourceLayout);

            GameObject root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, "Criar infraestrutura IA02");
            root.transform.position = rootPosition;
            root.transform.rotation = sourceLayout.transform.rotation;
            root.transform.localScale = Vector3.one;

            IA02Manager manager = root.AddComponent<IA02Manager>();
            ConfigureManager(manager);

            GameObject controllerObject = new GameObject("IA02Controller - Uniao Carmesim");
            Undo.RegisterCreatedObjectUndo(controllerObject, "Criar controlador IA02");
            controllerObject.transform.SetParent(root.transform, false);
            IA02Controller controller = controllerObject.AddComponent<IA02Controller>();

            GameObject layoutObject = new GameObject("IA02CityLayout - Uniao Carmesim");
            Undo.RegisterCreatedObjectUndo(layoutObject, "Criar layout IA02");
            layoutObject.transform.SetParent(root.transform, false);
            IA02BuildSlotRegistry registry = layoutObject.AddComponent<IA02BuildSlotRegistry>();
            IA02CityLayout layout = layoutObject.AddComponent<IA02CityLayout>();

            Dictionary<string, IA02BuildSlot> copiedSlots = CopySlots(sourceLayout, layout.transform);
            ConfigureLayout(layout, registry, copiedSlots);
            ConfigureController(controller, sourceController, profile, plan, layout, rootPosition);

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(layout);
            EditorUtility.SetDirty(registry);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;

            int slotCount = copiedSlots.Count;
            float separation = Vector3.Distance(sourceLayout.transform.position, rootPosition);
            Debug.Log(string.Format("[IA02] Configurada na cena {0}: time {1} ({2}), slots {3}, separação {4:0}m, perfil expansionista.",
                scenePath, Ia02TeamId, "Uniao Carmesim", slotCount, separation));
        }

        private static void RepairExistingInfrastructure(IA02Manager manager, Scene scene)
        {
            IA01Controller sourceController = UnityEngine.Object.FindFirstObjectByType<IA01Controller>(FindObjectsInactive.Include);
            IA01CityLayout sourceLayout = FindSourceLayout(sourceController);
            IA02CityLayout targetLayout = manager.GetComponentInChildren<IA02CityLayout>(true);
            IA02Controller targetController = manager.GetComponentInChildren<IA02Controller>(true);
            if (sourceController == null || sourceLayout == null || targetLayout == null || targetController == null)
            {
                Debug.LogError("[IA02] A infraestrutura existente não pôde ser reparada: faltam controlador ou layout IA01/IA02.");
                return;
            }

            IA02BuildSlotRegistry registry = targetLayout.GetComponent<IA02BuildSlotRegistry>();
            if (registry == null)
            {
                registry = targetLayout.gameObject.AddComponent<IA02BuildSlotRegistry>();
            }

            IA02NationProfile profile = EnsureProfile();
            IA02BuildPlan plan = EnsureBuildPlan();
            Dictionary<string, IA02BuildSlot> slots = CopySlots(sourceLayout, targetLayout.transform);
            ConfigureLayout(targetLayout, registry, slots);
            ConfigureController(targetController, sourceController, profile, plan, targetLayout, manager.transform.position);
            ConfigureManager(manager);

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(targetController);
            EditorUtility.SetDirty(targetLayout);
            EditorUtility.SetDirty(registry);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);

            int slotCount = targetLayout.GetComponentsInChildren<IA02BuildSlot>(true).Length;
            Debug.Log(string.Format("[IA02] Infraestrutura existente reparada: {0}, slots={1}, layout pronto para uso.",
                manager.name, slotCount));
        }

        [MenuItem("Hegemonia/IA02/Validar separação de territórios", priority = 2)]
        public static void ValidateCampaign()
        {
            OpenCampaignScene();
            IA01CityLayout ia01 = UnityEngine.Object.FindFirstObjectByType<IA01CityLayout>(FindObjectsInactive.Include);
            IA02CityLayout ia02 = UnityEngine.Object.FindFirstObjectByType<IA02CityLayout>(FindObjectsInactive.Include);
            IA02Controller controller = UnityEngine.Object.FindFirstObjectByType<IA02Controller>(FindObjectsInactive.Include);
            IA02Manager manager = UnityEngine.Object.FindFirstObjectByType<IA02Manager>(FindObjectsInactive.Include);
            if (ia01 == null || ia02 == null || controller == null || manager == null)
            {
                Debug.LogError("[IA02] Validação falhou: infraestrutura IA01/IA02 incompleta na cena.");
                return;
            }

            float distance = Vector3.Distance(ia01.transform.position, ia02.transform.position);
            bool capitalOnLand = IsLayoutOnValidLand(ia02, out string terrainReason);
            bool idsAreSeparate = controller.TeamId == Ia02TeamId && controller.NationId == Ia02NationId;
            bool layoutReady = ia02.EnsureRuntimeReady() && ia02.RegisteredSlotCount > 0;
            bool valid = distance >= MinimumTerritorySeparation && idsAreSeparate && layoutReady && capitalOnLand;
            string message = string.Format("[IA02] Validação: distância={0:0}m, slots={1}, team={2}, nation={3}, layoutReady={4}, terreno={5}.",
                distance, ia02.RegisteredSlotCount, controller.TeamId, controller.NationId, layoutReady, terrainReason);
            if (valid) Debug.Log(message + " OK - sem sobreposição lógica e com fundação terrestre.");
            else Debug.LogError(message + " FALHOU.");
        }

        [MenuItem("Hegemonia/IA02/Relocar território para terra livre", priority = 3)]
        public static void RelocateExistingTerritoryToSafeLand()
        {
            Scene scene = OpenCampaignScene();
            IA01CityLayout sourceLayout = UnityEngine.Object.FindFirstObjectByType<IA01CityLayout>(FindObjectsInactive.Include);
            IA02CityLayout ia02Layout = UnityEngine.Object.FindFirstObjectByType<IA02CityLayout>(FindObjectsInactive.Include);
            if (sourceLayout == null || ia02Layout == null)
            {
                Debug.LogError("[IA02] Relocação cancelada: layouts IA01 e IA02 precisam existir na cena.");
                return;
            }

            Transform runtimeRoot = ia02Layout.transform.parent;
            if (runtimeRoot == null)
            {
                Debug.LogError("[IA02] Relocação cancelada: IA02CityLayout não possui o runtime como pai.");
                return;
            }

            RefreshSurfaceRegistry();
            Vector3 oldPosition = runtimeRoot.position;
            if (!TryFindSafeTerritoryPosition(sourceLayout.transform.position, ia02Layout, out Vector3 newPosition, out string reason))
            {
                Debug.LogError("[IA02] Não foi possível localizar terra livre para o território: " + reason);
                return;
            }

            Undo.RecordObject(runtimeRoot, "Relocar território IA02 para terra livre");
            runtimeRoot.position = newPosition;
            EditorUtility.SetDirty(runtimeRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = runtimeRoot.gameObject;

            float distanceIa01 = Vector3.Distance(sourceLayout.transform.position, newPosition);
            Debug.Log(string.Format("[IA02] Território relocado de {0} para {1}. Distância IA01={2:0}m. Fundação em terra e faixa costeira validadas.",
                oldPosition, newPosition, distanceIa01));
        }

        private static Scene OpenCampaignScene()
        {
            return OpenScene(CampaignScenePath);
        }

        private static Scene OpenScene(string scenePath)
        {
            Scene active = SceneManager.GetActiveScene();
            if (!string.Equals(active.path, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                active = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            return active;
        }

        private static IA01CityLayout FindSourceLayout(IA01Controller sourceController)
        {
            IA01CityLayout controllerLayout = sourceController != null ? sourceController.CityLayout : null;
            if (controllerLayout != null && controllerLayout.GetComponentsInChildren<IA01BuildSlot>(true).Length > 0)
            {
                return controllerLayout;
            }

            IA01CityLayout[] layouts = UnityEngine.Object.FindObjectsByType<IA01CityLayout>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            IA01CityLayout fallback = controllerLayout;
            for (int i = 0; i < layouts.Length; i++)
            {
                IA01CityLayout candidate = layouts[i];
                if (candidate == null) continue;
                if (fallback == null) fallback = candidate;
                if (candidate.GetComponentsInChildren<IA01BuildSlot>(true).Length > 0) return candidate;
            }

            return fallback;
        }

        private static IA02NationProfile EnsureProfile()
        {
            EnsureFolder("Assets/IA02");
            EnsureFolder("Assets/IA02/Profiles");
            IA02NationProfile profile = AssetDatabase.LoadAssetAtPath<IA02NationProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<IA02NationProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            SerializedObject serialized = new SerializedObject(profile);
            SetString(serialized, "profileKey", "ia02.uniao.carmesim.expansionista");
            SetInt(serialized, "nationIdHint", Ia02NationId);
            SetInt(serialized, "teamIdHint", Ia02TeamId);
            SetString(serialized, "nationName", "Uniao Carmesim");
            SetString(serialized, "presidentName", "Comando Carmesim");
            SetString(serialized, "countryProfile", "ProdutorPetroleo");
            SetString(serialized, "difficultyProfile", "aggressive_expansion");
            SetInt(serialized, "personality", (int)IA02NationPersonality.Expansionist);
            SetInt(serialized, "doctrine", (int)IA02NationDoctrine.Naval);
            SetInt(serialized, "defaultExecutionMode", (int)IA02ExecutionMode.Full);
            SetInt(serialized, "defaultNationMode", (int)IA02NationMode.War);
            SetInt(serialized, "defaultStage", (int)IA02NationStage.Initialization);
            SetInt(serialized, "defaultPosture", (int)IA02NationPosture.War);

            SetFloat(serialized, "aggressionWeight", 0.95f);
            SetFloat(serialized, "cautionWeight", 0.18f);
            SetFloat(serialized, "commercialWeight", 0.32f);
            SetFloat(serialized, "diplomacyWeight", 0.12f);
            SetFloat(serialized, "militaryWeight", 0.90f);
            SetFloat(serialized, "selfSufficiencyWeight", 0.62f);
            SetFloat(serialized, "expansionWeight", 0.96f);
            SetFloat(serialized, "opportunismWeight", 0.84f);
            SetFloat(serialized, "landWeight", 0.60f);
            SetFloat(serialized, "airWeight", 0.72f);
            SetFloat(serialized, "navalWeight", 0.86f);
            SetFloat(serialized, "defenseWeight", 0.42f);
            SetFloat(serialized, "economyWeight", 0.46f);
            SetFloat(serialized, "industryWeight", 0.86f);
            SetFloat(serialized, "agricultureWeight", 0.30f);
            SetFloat(serialized, "technologyWeight", 0.64f);
            SetFloat(serialized, "riskTolerance", 82f);
            SetFloat(serialized, "militaryPriority", 0.62f);
            SetFloat(serialized, "economyPriority", 0.23f);
            SetFloat(serialized, "diplomacyPriority", 0.15f);
            SetInt(serialized, "initialTreasury", 45000);
            SetFloat(serialized, "baseCadenceSeconds", 0.45f);
            SetFloat(serialized, "minSliceMilliseconds", 0.08f);
            SetFloat(serialized, "maxSliceMilliseconds", 0.90f);
            SetInt(serialized, "maxOperationsPerSlice", 10);
            SetInt(serialized, "maxEventsPerSlice", 16);
            SetBool(serialized, "allowObserverWriteBacks", false);
            SetBool(serialized, "preferDeterministicBoot", true);
            SetBool(serialized, "allowSaveIntegration", true);
            SetBool(serialized, "allowAutoBootstrap", true);
            SetInt(serialized, "constructionGovernor.emergencyReserve", 2500);
            SetInt(serialized, "constructionGovernor.minimumConstructionReserve", 1000);
            SetFloat(serialized, "constructionGovernor.maximumConstructionBudgetPercent", 0.38f);
            SetFloat(serialized, "constructionGovernor.maximumMaintenancePercent", 0.20f);
            SetInt(serialized, "constructionGovernor.maxCandidatesPerSlice", 12);
            SetInt(serialized, "constructionGovernor.maxPhysicsChecksPerSlice", 48);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static IA02BuildPlan EnsureBuildPlan()
        {
            EnsureFolder("Assets/IA02");
            EnsureFolder("Assets/IA02/BuildPlans");
            IA02BuildPlan plan = AssetDatabase.LoadAssetAtPath<IA02BuildPlan>(PlanPath);
            if (plan == null)
            {
                plan = ScriptableObject.CreateInstance<IA02BuildPlan>();
                AssetDatabase.CreateAsset(plan, PlanPath);
            }

            IA01BuildPlan source = AssetDatabase.LoadAssetAtPath<IA01BuildPlan>(SourcePlanPath);
            SerializedObject target = new SerializedObject(plan);
            SetString(target, "planId", "ia02.plan.expansionista");
            SetInt(target, "layoutVersion", 1);
            if (source != null)
            {
                SerializedObject sourceSerialized = new SerializedObject(source);
                SerializedProperty sourceSteps = sourceSerialized.FindProperty("steps");
                SerializedProperty targetSteps = target.FindProperty("steps");
                if (sourceSteps != null && targetSteps != null)
                {
                    targetSteps.arraySize = sourceSteps.arraySize;
                    for (int i = 0; i < sourceSteps.arraySize; i++)
                    {
                        CopyPlanStep(sourceSteps.GetArrayElementAtIndex(i), targetSteps.GetArrayElementAtIndex(i));
                    }
                }
            }

            target.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(plan);
            return plan;
        }

        private static void CopyPlanStep(SerializedProperty source, SerializedProperty target)
        {
            CopyString(source, target, "stepId", true);
            CopyObjectReference(source, target, "constructionData");
            CopyInt(source, target, "requiredRole");
            CopyInt(source, target, "placementMode");
            CopyString(source, target, "primarySlotId", true);
            CopyString(source, target, "slotGroupId", false);
            CopyString(source, target, "autonomousZoneId", true);
            CopyBool(source, target, "required");
            CopyInt(source, target, "minimumStage");
            CopyInt(source, target, "maximumCount");
            CopyFloat(source, target, "cooldownAfterCompletion");
            CopyInt(source, target, "failurePolicy");
            SerializedProperty sourceCondition = source.FindPropertyRelative("condition");
            SerializedProperty targetCondition = target.FindPropertyRelative("condition");
            if (sourceCondition != null && targetCondition != null)
            {
                CopyInt(sourceCondition, targetCondition, "type");
                CopyFloat(sourceCondition, targetCondition, "target");
                CopyInt(sourceCondition, targetCondition, "role");
            }
        }

        private static Dictionary<string, IA02BuildSlot> CopySlots(IA01CityLayout sourceLayout, Transform targetLayout)
        {
            Dictionary<string, IA02BuildSlot> result = new Dictionary<string, IA02BuildSlot>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, IA02BuildSlot> existingById = new Dictionary<string, IA02BuildSlot>(StringComparer.OrdinalIgnoreCase);
            IA02BuildSlot[] existingSlots = targetLayout.GetComponentsInChildren<IA02BuildSlot>(true);
            for (int i = 0; i < existingSlots.Length; i++)
            {
                IA02BuildSlot existing = existingSlots[i];
                if (existing == null || string.IsNullOrWhiteSpace(existing.SlotId)) continue;
                if (!existingById.ContainsKey(existing.SlotId)) existingById.Add(existing.SlotId, existing);
            }

            IA01BuildSlot[] sourceSlots = sourceLayout.GetComponentsInChildren<IA01BuildSlot>(true);
            if (sourceSlots.Length == 0)
            {
                // Algumas cenas antigas mantêm os slots como objetos de cena
                // válidos, mas com a lista de filhos do layout serializada de
                // forma incompleta. A busca global continua limitada à cena
                // aberta e evita deixar uma IA02 aparentemente configurada,
                // porém sem nenhum slot utilizável.
                IA01BuildSlot[] sceneSlots = UnityEngine.Object.FindObjectsByType<IA01BuildSlot>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                // Se a relação de pai também estiver inconsistente, ainda
                // assim todos os IA01BuildSlot encontrados pertencem ao
                // layout da IA01 nesta cena. Não descartá-los evita que uma
                // configuração antiga congele a IA02 com zero slots.
                sourceSlots = sceneSlots;
            }

            for (int i = 0; i < sourceSlots.Length; i++)
            {
                IA01BuildSlot source = sourceSlots[i];
                if (source == null) continue;
                string sourceId = source.SlotId;
                string targetId = ReplaceIa01(sourceId);
                if (string.IsNullOrWhiteSpace(targetId) || result.ContainsKey(targetId)) continue;

                if (existingById.TryGetValue(targetId, out IA02BuildSlot existing))
                {
                    result.Add(targetId, existing);
                    continue;
                }

                GameObject slotObject = new GameObject(BuildSafeName(targetId));
                Undo.RegisterCreatedObjectUndo(slotObject, "Duplicar slot IA02");
                slotObject.transform.SetParent(targetLayout, false);
                slotObject.transform.localPosition = sourceLayout.transform.InverseTransformPoint(source.transform.position);
                slotObject.transform.localRotation = Quaternion.Inverse(sourceLayout.transform.rotation) * source.transform.rotation;
                slotObject.transform.localScale = source.transform.localScale;
                IA02BuildSlot target = slotObject.AddComponent<IA02BuildSlot>();
                CopySlotData(source, target, targetId);

                Transform buildingPoint = CreateReferencePoint(slotObject.transform, "BuildingPoint", source.BuildingPoint, source.transform);
                Transform spawnPoint = source.UnitSpawnPoint != null
                    ? CreateReferencePoint(slotObject.transform, "UnitSpawnPoint", source.UnitSpawnPoint, source.transform)
                    : null;
                Transform exitDirection = source.ExitDirection != null
                    ? CreateReferencePoint(slotObject.transform, "ExitDirection", source.ExitDirection, source.transform)
                    : null;
                SerializedObject serialized = new SerializedObject(target);
                serialized.FindProperty("buildingPoint").objectReferenceValue = buildingPoint;
                serialized.FindProperty("unitSpawnPoint").objectReferenceValue = spawnPoint;
                serialized.FindProperty("exitDirection").objectReferenceValue = exitDirection;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
                result.Add(targetId, target);
            }

            return result;
        }

        private static void CopySlotData(IA01BuildSlot source, IA02BuildSlot target, string targetId)
        {
            SerializedObject sourceSerialized = new SerializedObject(source);
            SerializedObject targetSerialized = new SerializedObject(target);
            CopyString(sourceSerialized, targetSerialized, "slotId", targetId);
            CopyString(sourceSerialized, targetSerialized, "slotGroupId", null);
            CopyInt(sourceSerialized, targetSerialized, "allowedRole");
            CopyInt(sourceSerialized, targetSerialized, "allowedDomain");
            CopyBool(sourceSerialized, targetSerialized, "required");
            CopyBool(sourceSerialized, targetSerialized, "exactPosition");
            CopyBool(sourceSerialized, targetSerialized, "allowAlternativeSlot");
            CopyVector2(sourceSerialized, targetSerialized, "reservedFootprint");
            CopyFloat(sourceSerialized, targetSerialized, "safetyMargin");
            SetInt(targetSerialized, "ownerTeamId", Ia02TeamId);
            SetInt(targetSerialized, "ownerNationId", Ia02NationId);
            SetInt(targetSerialized, "state", (int)IA02BuildSlotState.Available);
            SetString(targetSerialized, "reservedCommandId", string.Empty);
            SetString(targetSerialized, "constructedItemId", string.Empty);
            SetFloat(targetSerialized, "reservedAt", 0f);
            SetString(targetSerialized, "blockReason", string.Empty);
            SetInt(targetSerialized, "layoutVersion", 1);
            targetSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform CreateReferencePoint(Transform targetSlot, string name, Transform sourcePoint, Transform sourceSlot)
        {
            GameObject pointObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(pointObject, "Criar referência de slot IA02");
            Transform point = pointObject.transform;
            point.SetParent(targetSlot, false);
            if (sourcePoint == null)
            {
                point.localPosition = Vector3.zero;
                point.localRotation = Quaternion.identity;
            }
            else
            {
                point.localPosition = sourceSlot.InverseTransformPoint(sourcePoint.position);
                point.localRotation = Quaternion.Inverse(sourceSlot.rotation) * sourcePoint.rotation;
                point.localScale = Vector3.one;
            }

            return point;
        }

        private static void ConfigureLayout(IA02CityLayout layout, IA02BuildSlotRegistry registry, Dictionary<string, IA02BuildSlot> slots)
        {
            SerializedObject serialized = new SerializedObject(layout);
            SetString(serialized, "layoutId", "ia02.layout.uniao.carmesim");
            SetInt(serialized, "layoutVersion", 1);
            serialized.FindProperty("slotRegistry").objectReferenceValue = registry;
            if (slots.TryGetValue("ia02.local.prefeitura_01", out IA02BuildSlot capital))
            {
                serialized.FindProperty("capitalSlot").objectReferenceValue = capital;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            layout.EnsureRuntimeReady();
            layout.ConfigureOwner(Ia02TeamId, Ia02NationId);
        }

        private static void ConfigureController(IA02Controller controller, IA01Controller source, IA02NationProfile profile,
            IA02BuildPlan plan, IA02CityLayout layout, Vector3 rootPosition)
        {
            SerializedObject serialized = new SerializedObject(controller);
            SetInt(serialized, "nationId", Ia02NationId);
            SetInt(serialized, "teamId", Ia02TeamId);
            SetInt(serialized, "matchSeed", 2026);
            SetString(serialized, "nationNameOverride", "Uniao Carmesim");
            SetString(serialized, "presidentNameOverride", "Comando Carmesim");
            SetString(serialized, "countryProfileOverride", "ProdutorPetroleo");
            SetString(serialized, "difficultyProfileOverride", "aggressive_expansion");
            serialized.FindProperty("profileAsset").objectReferenceValue = profile;
            SetBool(serialized, "createRuntimeProfileWhenMissing", true);
            SetInt(serialized, "executionModeOverride", (int)IA02ExecutionMode.Full);
            SetInt(serialized, "nationModeOverride", (int)IA02NationMode.War);
            SetInt(serialized, "stageOverride", (int)IA02NationStage.Initialization);
            SetInt(serialized, "postureOverride", (int)IA02NationPosture.War);
            serialized.FindProperty("buildPlan").objectReferenceValue = plan;
            serialized.FindProperty("cityLayout").objectReferenceValue = layout;
            serialized.FindProperty("capitalBlueprint").objectReferenceValue = source.CapitalBlueprint;
            serialized.FindProperty("fighterPrefab").objectReferenceValue = source.FighterPrefab;
            CopyObjectReference(serialized, source, "prefeituraAnchor");
            CopyObjectList(serialized, source, "fichasDeConstrucao");
            CopyObjectList(serialized, source, "fichasMilitaresPermitidas");
            SetBool(serialized, "useScriptedOpening", true);
            SetBool(serialized, "usePreparedSlots", true);
            SetBool(serialized, "allowAutonomousExpansion", true);
            SetBool(serialized, "enablePlanningAdvisor", true);
            SetFloat(serialized, "maxConstructionDistanceFromController", 4200f);
            SetBool(serialized, "progressiveMilitaryCatalog", true);
            SetBool(serialized, "allowMilitaryTierAdvancement", true);
            SetBool(serialized, "autoRegisterWithManager", true);
            SetBool(serialized, "autoApplyGovernmentSnapshot", true);
            SetFloat(serialized, "fallbackCadenceSeconds", 0.45f);
            SetFloat(serialized, "nonCapitalConstructionIntervalSeconds", 3.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            controller.transform.localPosition = Vector3.zero;
            controller.transform.localRotation = Quaternion.identity;
        }

        private static void ConfigureManager(IA02Manager manager)
        {
            SerializedObject serialized = new SerializedObject(manager);
            SetBool(serialized, "autoBindSceneControllers", true);
            SetBool(serialized, "autoResolveIdentityCollisions", true);
            SetBool(serialized, "autoSpawnMissingControllersFromSave", true);
            SetBool(serialized, "autoSpawnFromGovernment", false);
            SetFloat(serialized, "frameBudgetMilliseconds", 1.5f);
            SetFloat(serialized, "serviceRefreshInterval", 1f);
            SetFloat(serialized, "summaryRefreshInterval", 0.25f);
            SetInt(serialized, "matchSeed", 2026);
            SetBool(serialized, "usarOrquestradorGlobal", true);
            SetFloat(serialized, "frequenciaEstrategicaGlobal", 0.5f);
            SetBool(serialized, "logSummary", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector3 FindFreeTerritoryPosition(IA01CityLayout sourceLayout)
        {
            RefreshSurfaceRegistry();
            if (TryFindSafeTerritoryPosition(sourceLayout.transform.position, null, out Vector3 position, out string reason))
            {
                return position;
            }

            throw new InvalidOperationException("[IA02] Não foi possível encontrar área livre para o território da União Carmesim: " + reason);
        }

        private static bool TryFindSafeTerritoryPosition(Vector3 ia01Position, IA02CityLayout existingLayout, out Vector3 bestPosition, out string reason)
        {
            bestPosition = Vector3.zero;
            reason = string.Empty;
            RefreshSurfaceRegistry();

            Bounds landBounds;
            if (!RegistroSuperficieMapa.TryGetBounds(TipoSuperficieMapa.Chao, out landBounds))
            {
                Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                bool foundTerrain = false;
                for (int i = 0; i < terrains.Length; i++)
                {
                    Terrain terrain = terrains[i];
                    if (terrain == null || terrain.terrainData == null) continue;
                    Bounds terrainBounds = terrain.terrainData.bounds;
                    terrainBounds.center += terrain.transform.position;
                    if (!foundTerrain)
                    {
                        landBounds = terrainBounds;
                        foundTerrain = true;
                    }
                    else
                    {
                        landBounds.Encapsulate(terrainBounds);
                    }
                }

                if (!foundTerrain)
                {
                    reason = "nenhum marcador de Chao ou Terrain foi encontrado";
                    return false;
                }
            }

            Vector3 playerPosition = ia01Position;
            GameObject player = null;
            try
            {
                player = GameObject.FindGameObjectWithTag("Player");
            }
            catch (UnityException)
            {
                // A tag pode não existir em uma cena mínima de teste.
            }

            if (player != null) playerPosition = player.transform.position;

            float bestScore = float.MinValue;
            float footprint = GetTerritoryFootprint(existingLayout);
            int tested = 0;
            int maxSamples = 25000;
            for (float x = landBounds.min.x; x <= landBounds.max.x && tested < maxSamples; x += TerritorySearchStep)
            {
                for (float z = landBounds.min.z; z <= landBounds.max.z && tested < maxSamples; z += TerritorySearchStep)
                {
                    tested++;
                    Vector3 candidate = new Vector3(x, landBounds.center.y, z);
                    if (!IsSafeTerritoryCandidate(candidate, existingLayout, footprint, ia01Position, playerPosition)) continue;

                    float score = Mathf.Min(Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(ia01Position.x, ia01Position.z)),
                        Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(playerPosition.x, playerPosition.z)));
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPosition = candidate;
                    }
                }
            }

            if (bestScore == float.MinValue)
            {
                reason = string.Format("nenhum ponto terrestre passou a separação mínima de {0:0}m após {1} amostras", MinimumTerritorySeparation, tested);
                return false;
            }

            if (RegistroSuperficieMapa.TryGetAltura(bestPosition, TipoSuperficieMapa.Chao, out float height, TerritoryFootprintPadding))
            {
                bestPosition.y = height;
            }

            return true;
        }

        private static bool IsSafeTerritoryCandidate(Vector3 candidate, IA02CityLayout layout, float footprint, Vector3 ia01Position, Vector3 playerPosition)
        {
            float ia01Distance = Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(ia01Position.x, ia01Position.z));
            float playerDistance = Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(playerPosition.x, playerPosition.z));
            if (ia01Distance < MinimumTerritorySeparation || playerDistance < MinimumTerritorySeparation) return false;
            if (!IsNonWater(candidate)) return false;

            if (layout != null)
            {
                IA02BuildSlot[] slots = layout.GetComponentsInChildren<IA02BuildSlot>(true);
                for (int i = 0; i < slots.Length; i++)
                {
                    IA02BuildSlot slot = slots[i];
                    if (slot == null || slot.AllowedDomain == IA02BuildDomain.Coastal || slot.AllowedDomain == IA02BuildDomain.Water) continue;
                    Vector3 slotPosition = candidate + (slot.transform.position - layout.transform.position);
                    if (!IsNonWater(slotPosition)) return false;
                }
            }
            else if (!IsNonWater(candidate + Vector3.right * footprint)
                || !IsNonWater(candidate - Vector3.right * footprint)
                || !IsNonWater(candidate + Vector3.forward * footprint)
                || !IsNonWater(candidate - Vector3.forward * footprint))
            {
                return false;
            }

            return HasCoastOrWaterNearby(candidate, footprint + 180f);
        }

        private static bool IsNonWater(Vector3 position)
        {
            if (!RegistroSuperficieMapa.TryClassify(position, out ClassificacaoSuperficieMapa classification, out _, 1.5f, TerritoryFootprintPadding))
            {
                return false;
            }

            return classification == ClassificacaoSuperficieMapa.Chao || classification == ClassificacaoSuperficieMapa.Costa;
        }

        private static bool HasCoastOrWaterNearby(Vector3 center, float radius)
        {
            Vector3[] samples =
            {
                center + Vector3.right * radius,
                center - Vector3.right * radius,
                center + Vector3.forward * radius,
                center - Vector3.forward * radius,
                center + new Vector3(1f, 0f, 1f).normalized * radius,
                center + new Vector3(-1f, 0f, 1f).normalized * radius,
                center + new Vector3(1f, 0f, -1f).normalized * radius,
                center + new Vector3(-1f, 0f, -1f).normalized * radius
            };

            for (int i = 0; i < samples.Length; i++)
            {
                if (!RegistroSuperficieMapa.TryClassify(samples[i], out ClassificacaoSuperficieMapa classification, out _, 1.5f, TerritoryFootprintPadding)) continue;
                if (classification == ClassificacaoSuperficieMapa.Agua || classification == ClassificacaoSuperficieMapa.Costa) return true;
            }

            return false;
        }

        private static float GetTerritoryFootprint(IA02CityLayout layout)
        {
            if (layout == null) return 500f;
            float maxDistance = 250f;
            IA02BuildSlot[] slots = layout.GetComponentsInChildren<IA02BuildSlot>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                IA02BuildSlot slot = slots[i];
                if (slot == null) continue;
                Vector3 local = layout.transform.InverseTransformPoint(slot.transform.position);
                maxDistance = Mathf.Max(maxDistance, new Vector2(local.x, local.z).magnitude + slot.ReservedFootprint.magnitude + TerritoryFootprintPadding);
            }

            return maxDistance;
        }

        private static bool IsLayoutOnValidLand(IA02CityLayout layout, out string reason)
        {
            reason = "desconhecido";
            if (layout == null)
            {
                reason = "layout ausente";
                return false;
            }

            IA02BuildSlot[] slots = layout.GetComponentsInChildren<IA02BuildSlot>(true);
            int checkedLandSlots = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                IA02BuildSlot slot = slots[i];
                if (slot == null || slot.AllowedDomain == IA02BuildDomain.Coastal || slot.AllowedDomain == IA02BuildDomain.Water) continue;
                checkedLandSlots++;
                if (!IsNonWater(slot.BuildingPoint.position))
                {
                    reason = "slot terrestre em água: " + slot.SlotId;
                    return false;
                }
            }

            reason = checkedLandSlots > 0 ? "slots terrestres em terra" : "sem slots terrestres para validar";
            return checkedLandSlots > 0;
        }

        private static void RefreshSurfaceRegistry()
        {
            MarcadorSuperficieMapa[] markers = UnityEngine.Object.FindObjectsByType<MarcadorSuperficieMapa>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null) RegistroSuperficieMapa.Registrar(markers[i]);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            string name = path.Substring(path.LastIndexOf('/') + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string ReplaceIa01(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            // Alguns slots antigos da copia da IA01 nao tinham prefixo de
            // namespace. Eles precisam de IDs estaveis proprios para que o
            // registro IA02 nunca possa colidir com o da IA01.
            switch (value.Trim())
            {
                case "prefeitura_01": return "ia02.local.prefeitura_01";
                case "energia_01": return "ia02.local.energia_01";
                case "fazenda_01": return "ia02.local.fazenda_01";
                case "casa_01": return "ia02.local.casa_01";
                case "armazem_01": return "ia02.local.armazem_01";
                default: return value.Replace("IA01", "IA02").Replace("ia01", "ia02");
            }
        }

        private static string BuildSafeName(string id)
        {
            string value = id.Replace('/', '_').Replace(':', '_');
            return string.IsNullOrWhiteSpace(value) ? "IA02BuildSlot" : value;
        }

        private static void CopyObjectReference(SerializedObject target, UnityEngine.Object sourceObject, string path)
        {
            SerializedObject source = new SerializedObject(sourceObject);
            CopyObjectReference(target, source, path);
        }

        private static void CopyObjectReference(SerializedObject target, SerializedObject source, string path)
        {
            SerializedProperty targetProperty = target.FindProperty(path);
            SerializedProperty sourceProperty = source.FindProperty(path);
            if (targetProperty != null && sourceProperty != null) targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
        }

        private static void CopyObjectList(SerializedObject target, UnityEngine.Object sourceObject, string path)
        {
            SerializedObject source = new SerializedObject(sourceObject);
            SerializedProperty sourceProperty = source.FindProperty(path);
            SerializedProperty targetProperty = target.FindProperty(path);
            if (sourceProperty == null || targetProperty == null || !sourceProperty.isArray || !targetProperty.isArray) return;
            targetProperty.arraySize = sourceProperty.arraySize;
            for (int i = 0; i < sourceProperty.arraySize; i++)
            {
                targetProperty.GetArrayElementAtIndex(i).objectReferenceValue = sourceProperty.GetArrayElementAtIndex(i).objectReferenceValue;
            }
        }

        private static void CopyString(SerializedProperty source, SerializedProperty target, string name, bool replaceIa01)
        {
            SerializedProperty sourceProperty = source.FindPropertyRelative(name);
            SerializedProperty targetProperty = target.FindPropertyRelative(name);
            if (sourceProperty == null || targetProperty == null) return;
            string value = sourceProperty.stringValue;
            targetProperty.stringValue = replaceIa01 ? ReplaceIa01(value) : value;
        }

        private static void CopyString(SerializedObject source, SerializedObject target, string name, string forcedValue)
        {
            SerializedProperty sourceProperty = source.FindProperty(name);
            SerializedProperty targetProperty = target.FindProperty(name);
            if (targetProperty == null) return;
            targetProperty.stringValue = forcedValue ?? (sourceProperty != null ? ReplaceIa01(sourceProperty.stringValue) : string.Empty);
        }

        private static void CopyObjectReference(SerializedProperty source, SerializedProperty target, string name)
        {
            SerializedProperty sourceProperty = source.FindPropertyRelative(name);
            SerializedProperty targetProperty = target.FindPropertyRelative(name);
            if (sourceProperty != null && targetProperty != null) targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
        }

        private static void CopyInt(SerializedProperty source, SerializedProperty target, string name)
        {
            SerializedProperty sourceProperty = source.FindPropertyRelative(name);
            SerializedProperty targetProperty = target.FindPropertyRelative(name);
            if (sourceProperty != null && targetProperty != null) targetProperty.intValue = sourceProperty.intValue;
        }

        private static void CopyBool(SerializedProperty source, SerializedProperty target, string name)
        {
            SerializedProperty sourceProperty = source.FindPropertyRelative(name);
            SerializedProperty targetProperty = target.FindPropertyRelative(name);
            if (sourceProperty != null && targetProperty != null) targetProperty.boolValue = sourceProperty.boolValue;
        }

        private static void CopyFloat(SerializedProperty source, SerializedProperty target, string name)
        {
            SerializedProperty sourceProperty = source.FindPropertyRelative(name);
            SerializedProperty targetProperty = target.FindPropertyRelative(name);
            if (sourceProperty != null && targetProperty != null) targetProperty.floatValue = sourceProperty.floatValue;
        }

        private static void CopyVector2(SerializedObject source, SerializedObject target, string name)
        {
            SerializedProperty sourceProperty = source.FindProperty(name);
            SerializedProperty targetProperty = target.FindProperty(name);
            if (sourceProperty != null && targetProperty != null) targetProperty.vector2Value = sourceProperty.vector2Value;
        }

        private static void CopyInt(SerializedObject source, SerializedObject target, string name)
        {
            SerializedProperty sourceProperty = source.FindProperty(name);
            SerializedProperty targetProperty = target.FindProperty(name);
            if (sourceProperty != null && targetProperty != null) targetProperty.intValue = sourceProperty.intValue;
        }

        private static void CopyBool(SerializedObject source, SerializedObject target, string name)
        {
            SerializedProperty sourceProperty = source.FindProperty(name);
            SerializedProperty targetProperty = target.FindProperty(name);
            if (sourceProperty != null && targetProperty != null) targetProperty.boolValue = sourceProperty.boolValue;
        }

        private static void CopyFloat(SerializedObject source, SerializedObject target, string name)
        {
            SerializedProperty sourceProperty = source.FindProperty(name);
            SerializedProperty targetProperty = target.FindProperty(name);
            if (sourceProperty != null && targetProperty != null) targetProperty.floatValue = sourceProperty.floatValue;
        }

        private static void SetString(SerializedObject serialized, string path, string value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null) property.stringValue = value ?? string.Empty;
        }

        private static void SetInt(SerializedObject serialized, string path, int value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null) property.intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string path, float value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null) property.floatValue = value;
        }

        private static void SetBool(SerializedObject serialized, string path, bool value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null) property.boolValue = value;
        }
    }
}
#endif
