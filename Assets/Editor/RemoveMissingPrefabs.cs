using UnityEngine;
using UnityEditor;

public class RemoveMissingPrefabs : EditorWindow
{
    [MenuItem("Tools/Limpar Prefabs Quebrados")]
    public static void LimparPrefabs()
    {
        GameObject[] todosObjetos = Object.FindObjectsOfType<GameObject>(true);
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
            Debug.Log($"[Limpar Prefabs] Limpeza concluída! {removidos} objetos perdidos foram deletados com segurança.");
        }
        else
        {
            Debug.Log($"[Limpar Prefabs] O mapa já está limpo! Nenhum erro amarelo ou vermelho relacionado a prefabs na cena.");
        }
    }
}
