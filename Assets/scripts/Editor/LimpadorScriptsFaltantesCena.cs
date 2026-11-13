using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hegemonia.EditorTools
{
    public static class LimpadorScriptsFaltantesCena
    {
        [MenuItem("Tools/Performance/Limpar Scripts Faltantes em GlobalMapRTS")]
        public static void ExecutarLimpezaGlobalMap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string scenePath = "Assets/Scenes/GlobalMapRTS.unity";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != scenePath)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (!scene.IsValid())
            {
                Debug.LogError("Não foi possível abrir a cena " + scenePath);
                return;
            }

            int totalRemovidos = 0;
            GameObject[] raizes = scene.GetRootGameObjects();
            for (int i = 0; i < raizes.Length; i++)
            {
                totalRemovidos += LimparRecursivo(raizes[i]);
            }

            if (totalRemovidos > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[LimpadorScriptsFaltantes] Sucesso: {totalRemovidos} referências a missing scripts removidas em {scenePath}.");
            }
            else
            {
                Debug.Log("[LimpadorScriptsFaltantes] Nenhum missing script encontrado em " + scenePath);
            }
        }

        private static int LimparRecursivo(GameObject go)
        {
            if (go == null) return 0;
            int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            for (int i = 0; i < go.transform.childCount; i++)
            {
                count += LimparRecursivo(go.transform.GetChild(i).gameObject);
            }
            return count;
        }
    }
}
