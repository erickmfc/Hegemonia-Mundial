using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class CodexPlaceCityInCena19
{
    private const string ScenePath = "Assets/_Recovery/cena19).unity";
    private const string PrefabPath = "Assets/Prefabs/Estaleiro Marinho/Cidade Nova Aurora.prefab";
    private const string CompletedKey = "Codex.PlaceCityInCena19.Completed";
    private static bool running;

    static CodexPlaceCityInCena19()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (EditorPrefs.GetBool(CompletedKey, false) || running)
            return;
        RunAndPlay();
    }

    [MenuItem("Tools/Hegemonia/Colocar cidade no mapa 19 e entrar em Play Mode")]
    private static void RunFromMenu()
    {
        RunAndPlay();
    }

    private static void RunAndPlay()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        running = true;
        try
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Scene active = SceneManager.GetActiveScene();
                if (active.IsValid() && active.isDirty && active.path != ScenePath)
                    throw new InvalidOperationException("A cena ativa tem alterações pendentes; não vou trocá-la automaticamente.");
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            EditorSceneManager.SetActiveScene(scene);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new FileNotFoundException("Prefab não encontrado: " + PrefabPath);

            GameObject city = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate.gameObject) == PrefabPath)
                    {
                        city = PrefabUtility.GetNearestPrefabInstanceRoot(candidate.gameObject);
                        break;
                    }
                }
                if (city != null) break;
            }

            bool created = city == null;
            if (created)
            {
                city = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (city == null)
                    throw new InvalidOperationException("Unity não conseguiu instanciar o prefab da cidade.");
                city.name = "Cidade Nova Aurora";
                Undo.RegisterCreatedObjectUndo(city, "Adicionar cidade 3D à cena 19");
            }

            int meshCollidersAdded = 0;
            foreach (MeshFilter filter in city.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshCollider>() != null)
                    continue;
                MeshCollider collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                collider.isTrigger = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                meshCollidersAdded++;
            }

            Bounds localBounds = GetRendererBoundsInRootSpace(city.transform);
            BoxCollider box = city.GetComponent<BoxCollider>();
            if (box == null)
                box = Undo.AddComponent<BoxCollider>(city);
            // The imported GLB is Z-up and the scene instance rotates it into
            // Unity's Y-up world. Keep the BoxCollider in the same local frame:
            // X/Y are the city footprint and Z is its height axis.
            box.center = localBounds.center;
            box.size = new Vector3(
                Mathf.Max(localBounds.size.x, 0.1f),
                Mathf.Max(localBounds.size.y, 0.1f),
                Mathf.Max(localBounds.size.z, 0.1f));
            // Per-mesh colliders provide physical building/ground collision;
            // this root box only marks the city's total volume.
            box.isTrigger = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);

            CidadeComplexoUrbano cidade = city.GetComponent<CidadeComplexoUrbano>();
            if (cidade == null) cidade = Undo.AddComponent<CidadeComplexoUrbano>(city);
            cidade.tema = TemaCidade.Moderna;
            cidade.nomeCidade = "Nova Aurora";
            cidade.teamId = 1;
            cidade.capacidadeHabitacional = 2000000;
            cidade.populacaoResidente = 100000;
            PrefabUtility.RecordPrefabInstancePropertyModifications(cidade);

            IdentidadeUnidade identidade = city.GetComponent<IdentidadeUnidade>();
            if (identidade == null) identidade = Undo.AddComponent<IdentidadeUnidade>(city);
            identidade.teamID = 1;
            identidade.tipoUnidade = TipoUnidade.Estrutura;
            PrefabUtility.RecordPrefabInstancePropertyModifications(identidade);

            SistemaDeDanos dano = city.GetComponent<SistemaDeDanos>();
            if (dano == null) dano = Undo.AddComponent<SistemaDeDanos>(city);
            dano.vidaMaxima = 5000000f;
            dano.vidaAtual = 5000000f;
            dano.ehEstrutura = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(dano);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Unity não conseguiu salvar a cena " + ScenePath);

            EditorPrefs.SetBool(CompletedKey, true);
            Debug.Log("[Codex City Setup] Cena=" + ScenePath + "; objeto=" + city.name + "; criado=" + created + "; MeshCollider adicionados=" + meshCollidersAdded + "; BoxCollider tamanho=" + box.size + "; centro=" + box.center + ".");
            SchedulePlayModeCapture();
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception)
        {
            Debug.LogError("[Codex City Setup] " + exception);
        }
        finally
        {
            running = false;
        }
    }

    private static Bounds GetRendererBoundsInRootSpace(Transform root)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        bool initialized = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            Bounds b = filter.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 meshLocal = new Vector3(
                    (corner & 1) == 0 ? b.min.x : b.max.x,
                    (corner & 2) == 0 ? b.min.y : b.max.y,
                    (corner & 4) == 0 ? b.min.z : b.max.z);
                Vector3 local = root.InverseTransformPoint(filter.transform.TransformPoint(meshLocal));
                if (!initialized) { result = new Bounds(local, Vector3.zero); initialized = true; }
                else result.Encapsulate(local);
            }
        }
        if (!initialized)
            throw new InvalidOperationException("O prefab da cidade não contém MeshFilter com malha para calcular os limites do collider.");
        return result;
    }

    private static void SchedulePlayModeCapture()
    {
        int updates = 0;
        EditorApplication.update += Capture;
        void Capture()
        {
            if (!EditorApplication.isPlaying)
                return;
            if (++updates < 120)
                return;
            EditorApplication.update -= Capture;
            string path = Path.Combine(Path.GetTempPath(), "cena19_cidade_playmode.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[Codex City Setup] Captura de Play Mode solicitada: " + path);
        }
    }
}
