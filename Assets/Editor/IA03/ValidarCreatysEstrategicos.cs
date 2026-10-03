#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hegemonia.AI.IA03;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hegemonia.AI.IA03.EditorTools
{
    /// <summary>Validador de autoría; só emite avisos e nunca reposiciona Creatys.</summary>
    public static class ValidarCreatysEstrategicos
    {
        [MenuItem("Hegemonia/IA03/Validar Creatys Estratégicos")]
        public static void Validar()
        {
            int encontrados = 0;
            int avisos = 0;

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    CreatyEstrategico[] creatys = roots[rootIndex].GetComponentsInChildren<CreatyEstrategico>(true);
                    for (int i = 0; i < creatys.Length; i++)
                    {
                        encontrados++;
                        avisos += ValidarCreaty(creatys[i], scene.name, ids, true);
                    }
                }
            }

            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < prefabs.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabs[i]);
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    CreatyEstrategico[] creatys = root.GetComponentsInChildren<CreatyEstrategico>(true);
                    for (int j = 0; j < creatys.Length; j++)
                    {
                        encontrados++;
                        avisos += ValidarCreaty(creatys[j], path, ids, false);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("IA03: não foi possível validar o prefab " + path + ": " + ex.Message);
                    avisos++;
                }
                finally
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            Debug.Log("IA03 Creatys: " + encontrados + " componente(s), " + avisos + " aviso(s). A validação não alterou cenas nem prefabs.");
        }

        private static int ValidarCreaty(CreatyEstrategico creaty, string origem, HashSet<string> ids, bool verificarSuperficie)
        {
            if (creaty == null)
            {
                return 0;
            }

            int avisos = 0;
            string id = creaty.Id != null ? creaty.Id.Trim() : string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                Avisar("Creaty sem ID", origem, creaty);
                avisos++;
            }
            else if (!ids.Add(id))
            {
                Avisar("ID duplicado: " + id, origem, creaty);
                avisos++;
            }

            if (creaty.PaisProprietarioTeamId <= 0)
            {
                Avisar("país proprietário não configurado", origem, creaty);
                avisos++;
            }

            if (!Enum.IsDefined(typeof(IA03NivelConflito), creaty.NivelDeConflito) || creaty.NivelDeConflito == IA03NivelConflito.Paz)
            {
                Avisar("Creaty sem nível de conflito 1–4", origem, creaty);
                avisos++;
            }

            if (creaty.Tipo == IA03TipoCreaty.DesembarqueAnfibio && creaty.PaisAlvoTeamId <= 0)
            {
                Avisar("ponto de desembarque sem país-alvo", origem, creaty);
                avisos++;
            }

            if (verificarSuperficie && TentarObterSuperficie(creaty.transform.position, out bool estaNaAgua))
            {
                if (creaty.Dominio == IA03DominioEstrategico.Naval && !estaNaAgua)
                {
                    Avisar("Creaty naval está em terra", origem, creaty);
                    avisos++;
                }
                else if (creaty.Dominio == IA03DominioEstrategico.Terrestre && estaNaAgua)
                {
                    Avisar("Creaty terrestre está no mar", origem, creaty);
                    avisos++;
                }
            }

            return avisos;
        }

        private static bool TentarObterSuperficie(Vector3 posicao, out bool estaNaAgua)
        {
            estaNaAgua = false;
            int waterLayer = LayerMask.NameToLayer("Water");
            int groundLayer = LayerMask.NameToLayer("Chao");
            int mask = 0;
            if (waterLayer >= 0) mask |= 1 << waterLayer;
            if (groundLayer >= 0) mask |= 1 << groundLayer;
            if (mask == 0)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                posicao + Vector3.up * 1000f,
                Vector3.down,
                2000f,
                mask,
                QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            int maisProximo = 0;
            for (int i = 1; i < hits.Length; i++)
            {
                if (hits[i].distance < hits[maisProximo].distance)
                {
                    maisProximo = i;
                }
            }

            estaNaAgua = hits[maisProximo].collider.gameObject.layer == waterLayer;
            return true;
        }

        private static void Avisar(string mensagem, string origem, CreatyEstrategico creaty)
        {
            string caminho = GlobalObjectId.GetGlobalObjectIdSlow(creaty).ToString();
            Debug.LogWarning("IA03 Creaty [" + origem + "] " + creaty.name + " (" + caminho + "): " + mensagem, creaty);
        }
    }
}
#endif
