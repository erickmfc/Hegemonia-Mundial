using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Hegemonia.Performance
{
    /// <summary>
    /// Utilitário seguro para consolidação de malhas e geração procedural com time-slicing
    /// com orçamento estrito de frame (3ms a 4ms), prevenindo vazamento de malhas órfãs na memória RAM.
    /// </summary>
    public sealed class GeradorMalhasComOrcamento : MonoBehaviour
    {
        private const double MaxOrcamentoMsPorFrame = 3.5;

        private static GeradorMalhasComOrcamento _instancia;
        public static GeradorMalhasComOrcamento Instancia
        {
            get
            {
                if (_instancia == null)
                {
                    GameObject go = new GameObject("[GeradorMalhasComOrcamento]");
                    _instancia = go.AddComponent<GeradorMalhasComOrcamento>();
                    DontDestroyOnLoad(go);
                }
                return _instancia;
            }
        }

        private void Awake()
        {
            if (_instancia != null && _instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            _instancia = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Consolida uma lista de CombineInstance em lotes espaçados sem travar a Main Thread,
        /// liberando explicitamente qualquer Mesh antiga que seja substituída.
        /// </summary>
        public void ConsolidarMalhaTimeSliced(
            MeshFilter filtroAlvo,
            CombineInstance[] partes,
            Material material,
            MeshCollider colisorOpcional = null,
            Action aoFinalizar = null)
        {
            if (filtroAlvo == null || partes == null || partes.Length == 0)
            {
                aoFinalizar?.Invoke();
                return;
            }

            StartCoroutine(RotinaConsolidacao(filtroAlvo, partes, material, colisorOpcional, aoFinalizar));
        }

        private IEnumerator RotinaConsolidacao(
            MeshFilter filtroAlvo,
            CombineInstance[] partes,
            Material material,
            MeshCollider colisorOpcional,
            Action aoFinalizar)
        {
            Stopwatch cronometro = new Stopwatch();
            cronometro.Start();

            // Destrói malha antiga com segurança para não deixar órfãos na memória
            if (filtroAlvo.sharedMesh != null)
            {
                Mesh velha = filtroAlvo.sharedMesh;
                filtroAlvo.sharedMesh = null;
                Destroy(velha);
            }

            Mesh malhaFinal = new Mesh
            {
                name = "MalhaConsolidada_TimeSliced",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };

            // Processamento fatiado
            int total = partes.Length;
            int indice = 0;
            const int loteTamanho = 32;

            List<CombineInstance> bufferLote = new List<CombineInstance>(loteTamanho);
            List<CombineInstance> subMalhas = new List<CombineInstance>();

            while (indice < total)
            {
                cronometro.Restart();

                while (indice < total && bufferLote.Count < loteTamanho)
                {
                    bufferLote.Add(partes[indice]);
                    indice++;
                }

                if (bufferLote.Count > 0)
                {
                    Mesh subMesh = new Mesh();
                    subMesh.CombineMeshes(bufferLote.ToArray(), true, true);
                    subMalhas.Add(new CombineInstance { mesh = subMesh, transform = Matrix4x4.identity });
                    bufferLote.Clear();
                }

                if (cronometro.Elapsed.TotalMilliseconds >= MaxOrcamentoMsPorFrame)
                {
                    yield return null;
                }
            }

            // Junção final
            malhaFinal.CombineMeshes(subMalhas.ToArray(), true, false);
            malhaFinal.RecalculateBounds();
            malhaFinal.RecalculateNormals();

            // Limpa as submalhas temporárias intermediárias da memória
            for (int i = 0; i < subMalhas.Count; i++)
            {
                if (subMalhas[i].mesh != null)
                {
                    Destroy(subMalhas[i].mesh);
                }
            }
            subMalhas.Clear();

            filtroAlvo.sharedMesh = malhaFinal;

            MeshRenderer mr = filtroAlvo.GetComponent<MeshRenderer>();
            if (mr != null && material != null)
            {
                mr.sharedMaterial = material;
            }

            // Se houver MeshCollider, aplica em um frame separado com cooking leve
            if (colisorOpcional != null)
            {
                yield return null;
                colisorOpcional.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices;
                colisorOpcional.sharedMesh = malhaFinal;
            }

            aoFinalizar?.Invoke();
        }
    }
}
