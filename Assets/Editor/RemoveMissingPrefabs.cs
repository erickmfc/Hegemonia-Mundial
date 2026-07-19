using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class RemoveMissingPrefabs
{
    [MenuItem("Tools/Limpar Prefabs Quebrados")]
    public static void LimparPrefabs()
    {
        GameObject[] todosObjetos = ObterGameObjectsDaCena();
        int removidos = 0;

        foreach (GameObject obj in todosObjetos)
        {
            // O Unity mais novo não usa IsMissingPrefabInstance diretamente. 
            // Usa GetPrefabAssetType e GetPrefabInstanceStatus
            if (PrefabUtility.IsAnyPrefabInstanceRoot(obj) && PrefabUtility.GetPrefabInstanceStatus(obj) == PrefabInstanceStatus.MissingAsset)
            {
                Debug.Log($"[Limpar Prefabs] Prefab fantasma exterminado: {obj.name}");
                Undo.DestroyObjectImmediate(obj);
                removidos++;
            }
        }

        if (removidos > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[Limpar Prefabs] Limpeza concluída! {removidos} objetos perdidos foram deletados com segurança.");
        }
        else
        {
            Debug.Log($"[Limpar Prefabs] O mapa já está limpo! Nenhum erro amarelo ou vermelho relacionado a prefabs na cena.");
        }
    }

    private static GameObject[] ObterGameObjectsDaCena()
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<GameObject>(true);
#endif
    }
}
