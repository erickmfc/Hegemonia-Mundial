using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.Performance
{
    /// <summary>
    /// Gerenciador leve de Culling Visual por distância (HLOD Lógico).
    /// Desativa SOMENTE os Renderers visuais de cidades e navios quando distantes ou fora de visão,
    /// mantendo o GameObject raiz e 100% dos scripts de economia, movimentação, combate e radar ativos.
    /// Atualiza em lotes espaçados (0.4s) para custo zero na Main Thread.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CullingVisualHierarquia : MonoBehaviour
    {
        [Header("Configuração de Culling")]
        [Tooltip("Distância da câmera a partir da qual os renderers detalhados são ocultados.")]
        [SerializeField] private float distanciaCulling = 1000f;

        [Tooltip("Intervalo entre checagens de visibilidade.")]
        [SerializeField] private float intervaloChecagem = 0.4f;

        [Header("Alvos de Culling")]
        [SerializeField] private List<Renderer> renderersDecorativos = new List<Renderer>();

        private Transform transformCamera;
        private float proximaChecagem;
        private bool visivel = true;
        private float distanciaCullingSqr;

        private void Start()
        {
            if (renderersDecorativos.Count == 0)
            {
                // Localiza container visual consolidado se existir, ou renderers locais
                Transform container = transform.Find("Container_VisualConsolidado");
                if (container != null)
                {
                    renderersDecorativos.AddRange(container.GetComponentsInChildren<Renderer>(true));
                }
                else
                {
                    // Pega renderers filhos que não contenham scripts
                    Renderer[] todos = GetComponentsInChildren<Renderer>(true);
                    for (int i = 0; i < todos.Length; i++)
                    {
                        Renderer r = todos[i];
                        if (r != null && r.gameObject != gameObject)
                        {
                            renderersDecorativos.Add(r);
                        }
                    }
                }
            }

            float raioBounds = 0f;
            Bounds? boundsCombinado = null;
            for (int i = 0; i < renderersDecorativos.Count; i++)
            {
                Renderer r = renderersDecorativos[i];
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                if (boundsCombinado == null) boundsCombinado = r.bounds;
                else
                {
                    Bounds b = boundsCombinado.Value;
                    b.Encapsulate(r.bounds);
                    boundsCombinado = b;
                }
            }
            if (boundsCombinado.HasValue)
                raioBounds = boundsCombinado.Value.extents.magnitude;

            // Cidades e grandes estruturas no mapa global não devem desaparecer quando a câmera RTS sobe/afasta.
            float distanciaBase = GetComponent<CidadeComplexoUrbano>() != null
                ? Mathf.Max(distanciaCulling, 12000f)
                : distanciaCulling;
            float distanciaEfetiva = distanciaBase + raioBounds;
            distanciaCullingSqr = distanciaEfetiva * distanciaEfetiva;

            // Espaçar início para não acumular no mesmo frame
            proximaChecagem = Time.time + Random.Range(0f, intervaloChecagem);
        }

        public void RegistrarRenderersExtras(IEnumerable<Renderer> extras)
        {
            if (extras == null) return;
            foreach (var r in extras)
            {
                if (r != null && !renderersDecorativos.Contains(r))
                {
                    renderersDecorativos.Add(r);
                }
            }
        }

        private void Update()
        {
            if (Time.time < proximaChecagem) return;
            proximaChecagem = Time.time + intervaloChecagem;

            if (transformCamera == null)
            {
                if (Camera.main != null)
                {
                    transformCamera = Camera.main.transform;
                }
                else
                {
                    return;
                }
            }

            float distSqr = (transform.position - transformCamera.position).sqrMagnitude;
            bool deveEstarVisivel = distSqr <= distanciaCullingSqr;

            if (visivel != deveEstarVisivel)
            {
                visivel = deveEstarVisivel;
                AplicarVisibilidade(visivel);
            }
        }

        private void AplicarVisibilidade(bool estado)
        {
            for (int i = 0; i < renderersDecorativos.Count; i++)
            {
                Renderer r = renderersDecorativos[i];
                if (r != null && r.enabled != estado)
                {
                    r.enabled = estado;
                }
            }
        }

        private void OnDisable()
        {
            // Restaura visibilidade ao desativar o componente
            AplicarVisibilidade(true);
        }
    }
}
