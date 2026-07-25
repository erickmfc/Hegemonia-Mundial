#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hegemonia.AI.IA01;

internal static class RestaurarReferenciasIA01
{
    [MenuItem("Hegemonia/IA01/Restaurar referencias dos slots")]
    private static void Restaurar()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (!cena.IsValid())
        {
            Debug.LogWarning("Nao ha uma cena valida aberta para restaurar a IA01.");
            return;
        }

        int navais = 0;
        int aeroportos = 0;

        IA01NavalBuildSlot[] slotsNavais = Object.FindObjectsByType<IA01NavalBuildSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < slotsNavais.Length; i++)
        {
            IA01NavalBuildSlot slot = slotsNavais[i];
            IA01BuildSlot buildSlot = slot.GetComponent<IA01BuildSlot>();
            if (buildSlot == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(slot);
            DefinirReferencia(serialized, "buildSlot", buildSlot);
            Transform spawnPoint = buildSlot.UnitSpawnPoint != null
                ? buildSlot.UnitSpawnPoint
                : EncontrarFilho(slot.transform, "Spawn_Unidades");
            Transform exitDirection = buildSlot.ExitDirection != null
                ? buildSlot.ExitDirection
                : EncontrarFilho(slot.transform, "Direcao_Saida");
            DefinirReferencia(serialized, "navalSpawnPoint", spawnPoint);
            DefinirReferencia(serialized, "exitDirection", exitDirection);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(slot);
            navais++;
        }

        IA01AirportBuildSlot[] slotsAeroportos = Object.FindObjectsByType<IA01AirportBuildSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < slotsAeroportos.Length; i++)
        {
            IA01AirportBuildSlot slot = slotsAeroportos[i];
            IA01BuildSlot buildSlot = slot.GetComponent<IA01BuildSlot>();
            if (buildSlot == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(slot);
            DefinirReferencia(serialized, "buildSlot", buildSlot);
            DefinirReferencia(serialized, "aircraftSpawn", buildSlot.UnitSpawnPoint);
            DefinirReferencia(serialized, "approachDirection", buildSlot.ExitDirection);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(slot);
            aeroportos++;
        }

        RestaurarRaizIA01();
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AssetDatabase.SaveAssets();
        Debug.Log("[IA01] Referencias restauradas: navais=" + navais + " aeroportos=" + aeroportos + " cena=" + cena.name);
    }

    private static void DefinirReferencia(SerializedObject serialized, string nome, Object referencia)
    {
        SerializedProperty propriedade = serialized.FindProperty(nome);
        if (propriedade != null)
        {
            propriedade.objectReferenceValue = referencia;
        }
    }

    private static Transform EncontrarFilho(Transform raiz, string nome)
    {
        Transform encontrado = raiz.Find(nome);
        if (encontrado != null)
        {
            return encontrado;
        }

        for (int i = 0; i < raiz.childCount; i++)
        {
            encontrado = EncontrarFilho(raiz.GetChild(i), nome);
            if (encontrado != null)
            {
                return encontrado;
            }
        }

        return null;
    }

    private static void RestaurarRaizIA01()
    {
        Transform raiz = null;
        Transform[] todas = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todas.Length; i++)
        {
            if (todas[i].name == "ia01")
            {
                raiz = todas[i];
                break;
            }
        }

        if (raiz == null)
        {
            return;
        }

        raiz.SetParent(null, false);
        raiz.localRotation = new Quaternion(0f, 1f, 0f, 0f);
        raiz.localPosition = new Vector3(884f, 0.000015258789f, -3127f);
        EditorUtility.SetDirty(raiz);
    }
}
#endif
