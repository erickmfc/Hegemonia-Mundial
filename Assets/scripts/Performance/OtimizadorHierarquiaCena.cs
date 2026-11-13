using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hegemonia.Performance
{
    /// <summary>
    /// Resultado das métricas de otimização executadas em um GameObject ou na cena.
    /// </summary>
    public struct MetricasOtimizacao
    {
        public int malhasConsolidadas;
        public int objetosDesativados;
        public int colisoresPodados;
        public int malhasNaoLegiveisPreservadas;
    }

    /// <summary>
    /// Utilitário seguro para otimização de hierarquia de GameObjects e consolidação de malhas
    /// em tempo de execução e no Editor para grupos urbanos (City) e modelos navais hiperdetalhados.
    /// Preserva rigorosamente todas as referências serializadas e públicas via reflexão.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OtimizadorHierarquiaCena : MonoBehaviour
    {
        [Header("Configurações")]
        [Tooltip("Executa a consolidação segura automaticamente no Start.")]
        [SerializeField] private bool otimizarNoStart = true;

        [Tooltip("Remove colisores secundários redundantes de subpeças puramente visuais, mantendo o colisor principal.")]
        [SerializeField] private bool podarColisoresSecundarios = true;

        [Tooltip("Combina malhas estáticas que compartilham o mesmo Material em um container consolidado.")]
        [SerializeField] private bool combinarMalhasDecorativas = true;

        [Tooltip("Desativa os GameObjects originais mesclados em vez de destruí-los, garantindo máxima reversibilidade.")]
        [SerializeField] private bool desativarEmVezDeDestruir = true;

        private bool jaOtimizado;

        // Métricas globais acumuladas da sessão
        private static int _totalGlobalObjetosDesativados;
        private static int _totalGlobalColisoresPodados;
        private static int _totalGlobalMalhasConsolidadas;
        private static int _totalGlobalMalhasNaoLegiveis;

        public static int TotalGlobalObjetosDesativados => _totalGlobalObjetosDesativados;
        public static int TotalGlobalColisoresPodados => _totalGlobalColisoresPodados;
        public static int TotalGlobalMalhasConsolidadas => _totalGlobalMalhasConsolidadas;
        public static int TotalGlobalMalhasNaoLegiveis => _totalGlobalMalhasNaoLegiveis;

        private void Start()
        {
            if (otimizarNoStart && !jaOtimizado)
            {
                ExecutarOtimizacao();
            }
        }

        /// <summary>
        /// Mapeia reflexivamente todos os nós protegidos em MonoBehaviours da hierarquia
        /// para garantir ZERO quebra de referências (disparadores, radares, hélices, waypoints, etc).
        /// </summary>
        public static HashSet<Transform> MapearNosProtegidos(GameObject raiz)
        {
            var protegidos = new HashSet<Transform>();
            if (raiz == null) return protegidos;

            protegidos.Add(raiz.transform);

            MonoBehaviour[] componentes = raiz.GetComponentsInChildren<MonoBehaviour>(true);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            for (int i = 0; i < componentes.Length; i++)
            {
                MonoBehaviour mb = componentes[i];
                if (mb == null) continue;

                protegidos.Add(mb.transform);

                Type tipo = mb.GetType();
                while (tipo != null && tipo != typeof(MonoBehaviour) && tipo != typeof(Behaviour) && tipo != typeof(Component) && tipo != typeof(UnityEngine.Object))
                {
                    FieldInfo[] campos = tipo.GetFields(flags);
                    for (int f = 0; f < campos.Length; f++)
                    {
                        FieldInfo campo = campos[f];
                        if (campo.IsPublic || Attribute.IsDefined(campo, typeof(SerializeField)))
                        {
                            object valor = null;
                            try { valor = campo.GetValue(mb); } catch { }

                            RegistrarReferencia(valor, protegidos);
                        }
                    }
                    tipo = tipo.BaseType;
                }
            }

            return protegidos;
        }

        private static void RegistrarReferencia(object obj, HashSet<Transform> protegidos)
        {
            if (obj == null) return;

            // Na Unity, um UnityEngine.Object destruído ou não atribuído não é null para C#, mas avalia como true em unityObj == null
            if (obj is UnityEngine.Object unityObj && unityObj == null)
            {
                return;
            }

            if (obj is Component comp)
            {
                if (comp != null && comp.transform != null)
                {
                    protegidos.Add(comp.transform);
                }
                return;
            }

            if (obj is GameObject go)
            {
                if (go != null && go.transform != null)
                {
                    protegidos.Add(go.transform);
                }
                return;
            }

            // Ignora tipos primitivos, estruturas ou strings antes de tentar IEnumerable
            if (obj is string || obj.GetType().IsPrimitive || obj.GetType().IsValueType)
            {
                return;
            }

            // Não iterar sobre Transform (que implementa IEnumerable para filhos e dispara exceções em referências falsas-nulas)
            if (obj is Transform)
            {
                return;
            }

            if (obj is System.Collections.IEnumerable enumerable)
            {
                try
                {
                    foreach (var item in enumerable)
                    {
                        RegistrarReferencia(item, protegidos);
                    }
                }
                catch { }
            }
        }

        [ContextMenu("Otimizar Hierarquia e Mesclar Malhas")]
        public MetricasOtimizacao ExecutarOtimizacao()
        {
            MetricasOtimizacao metricas = new MetricasOtimizacao();
            if (jaOtimizado) return metricas;
            jaOtimizado = true;

            HashSet<Transform> protegidos = MapearNosProtegidos(gameObject);

            if (podarColisoresSecundarios)
            {
                metricas.colisoresPodados = PodarColisoresRedundantes(gameObject, protegidos);
                _totalGlobalColisoresPodados += metricas.colisoresPodados;
            }

            if (combinarMalhasDecorativas)
            {
                ConsolidarMalhasDecorativas(gameObject, protegidos, desativarEmVezDeDestruir, ref metricas);
                _totalGlobalMalhasConsolidadas += metricas.malhasConsolidadas;
                _totalGlobalObjetosDesativados += metricas.objetosDesativados;
                _totalGlobalMalhasNaoLegiveis += metricas.malhasNaoLegiveisPreservadas;
            }

            // Garante componente de culling visual
            if (GetComponent<CullingVisualHierarquia>() == null)
            {
                gameObject.AddComponent<CullingVisualHierarquia>();
            }

            return metricas;
        }

        private static int PodarColisoresRedundantes(GameObject raiz, HashSet<Transform> protegidos)
        {
            // Garante que a raiz ou um nó principal possua colisor representativo
            Collider colisorPrincipal = raiz.GetComponent<Collider>();
            if (colisorPrincipal == null)
            {
                colisorPrincipal = raiz.GetComponentInChildren<Collider>(true);
            }

            if (colisorPrincipal == null)
            {
                return 0;
            }

            int podados = 0;
            Collider[] todosColisores = raiz.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < todosColisores.Length; i++)
            {
                Collider col = todosColisores[i];
                if (col == null || col == colisorPrincipal) continue;

                // Não remove colisores que estejam explicitamente referenciados em scripts
                if (protegidos.Contains(col.transform)) continue;

                // Se tiver script próprio no mesmo transform, preserva
                if (col.GetComponent<MonoBehaviour>() != null) continue;

                // Desativa o colisor secundário redundante para tirar a carga do PhysX na Main Thread
                if (col.enabled)
                {
                    col.enabled = false;
                    podados++;
                }
            }

            return podados;
        }

        private static void ConsolidarMalhasDecorativas(
            GameObject raiz,
            HashSet<Transform> protegidos,
            bool desativarApenas,
            ref MetricasOtimizacao metricas)
        {
            MeshFilter[] todosFiltros = raiz.GetComponentsInChildren<MeshFilter>(true);
            if (todosFiltros == null || todosFiltros.Length <= 1) return;

            // Agrupa por Material compartilhado
            var gruposPorMaterial = new Dictionary<Material, List<MeshFilter>>();
            var filtrosElegiveis = new List<MeshFilter>();
            var renderersNaoLegiveis = new List<Renderer>();

            for (int i = 0; i < todosFiltros.Length; i++)
            {
                MeshFilter mf = todosFiltros[i];
                if (mf == null || mf.sharedMesh == null) continue;

                Transform t = mf.transform;

                // Proteções estritas: não mexer em nós com referências diretas em scripts
                if (protegidos.Contains(t)) continue;

                // Não mexer em nós com animação, skinned mesh ou outros scripts
                if (t.GetComponent<MonoBehaviour>() != null || t.GetComponent<Animation>() != null || t.GetComponent<Animator>() != null)
                    continue;

                MeshRenderer mr = t.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;

                // Proteção isReadable: Se a malha não for legível via script de runtime na Unity,
                // não podemos usar CombineMeshes. Preservamos sua malha e incluímos o Renderer no CullingVisualHierarquia.
                if (!mf.sharedMesh.isReadable)
                {
                    metricas.malhasNaoLegiveisPreservadas++;
                    renderersNaoLegiveis.Add(mr);
                    continue;
                }

                Material mat = mr.sharedMaterial;
                if (mat == null) continue;

                if (!gruposPorMaterial.TryGetValue(mat, out var lista))
                {
                    lista = new List<MeshFilter>();
                    gruposPorMaterial.Add(mat, lista);
                }

                lista.Add(mf);
                filtrosElegiveis.Add(mf);
            }

            // Se existirem renderers não legíveis, vincula-os ao componente de CullingVisual
            if (renderersNaoLegiveis.Count > 0)
            {
                CullingVisualHierarquia culling = raiz.GetComponent<CullingVisualHierarquia>();
                if (culling == null) culling = raiz.AddComponent<CullingVisualHierarquia>();
                culling.RegistrarRenderersExtras(renderersNaoLegiveis);
            }

            // Para grupos estáticos (como cidades ou prédios fixos), aplica StaticBatchingUtility na raiz
            // para consolidar draw calls da GPU sem depender de isReadable na CPU.
            if (raiz.isStatic || raiz.name.StartsWith("City", StringComparison.OrdinalIgnoreCase) || raiz.name.StartsWith("Cidade", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    StaticBatchingUtility.Combine(raiz);
                }
                catch { }
            }

            // Limpa/desativa GameObjects filhos puramente vazios (sem componentes extras além de Transform e sem filhos ativos)
            LimparNosFolhaVazios(raiz, protegidos, ref metricas);

            if (filtrosElegiveis.Count <= 1) return;

            Transform containerVisual = raiz.transform.Find("Container_VisualConsolidado");
            if (containerVisual == null)
            {
                GameObject cvGo = new GameObject("Container_VisualConsolidado");
                containerVisual = cvGo.transform;
                containerVisual.SetParent(raiz.transform, false);
            }

            Matrix4x4 raizInv = raiz.transform.worldToLocalMatrix;

            foreach (var kvp in gruposPorMaterial)
            {
                Material mat = kvp.Key;
                List<MeshFilter> filtros = kvp.Value;
                if (filtros.Count == 0) continue;

                var combines = new List<CombineInstance>();
                for (int f = 0; f < filtros.Count; f++)
                {
                    MeshFilter mf = filtros[f];
                    if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;

                    CombineInstance ci = new CombineInstance
                    {
                        mesh = mf.sharedMesh,
                        transform = raizInv * mf.transform.localToWorldMatrix
                    };
                    combines.Add(ci);
                }

                if (combines.Count == 0) continue;

                // Criação obrigatória com UInt32 para suportar grandes cidades e navios
                Mesh malhaCombinada = new Mesh
                {
                    name = "MalhaCombinada_" + mat.name,
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
                };
                malhaCombinada.CombineMeshes(combines.ToArray(), true, true);
                malhaCombinada.RecalculateBounds();

                GameObject subObjetoVisual = new GameObject("Visual_" + mat.name);
                subObjetoVisual.transform.SetParent(containerVisual, false);

                MeshFilter novoMf = subObjetoVisual.AddComponent<MeshFilter>();
                novoMf.sharedMesh = malhaCombinada;

                MeshRenderer novoMr = subObjetoVisual.AddComponent<MeshRenderer>();
                novoMr.sharedMaterial = mat;
                novoMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                novoMr.receiveShadows = true;

                metricas.malhasConsolidadas++;
            }

            // Desativa ou remove os renderers / objetos decorativos já fundidos
            for (int i = 0; i < filtrosElegiveis.Count; i++)
            {
                MeshFilter mf = filtrosElegiveis[i];
                if (mf == null) continue;

                if (desativarApenas)
                {
                    if (mf.transform.childCount == 0)
                    {
                        if (mf.gameObject.activeSelf)
                        {
                            mf.gameObject.SetActive(false);
                            metricas.objetosDesativados++;
                        }
                    }
                    else
                    {
                        MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                        if (mr != null && mr.enabled)
                        {
                            mr.enabled = false;
                            metricas.objetosDesativados++;
                        }
                    }
                }
                else
                {
                    if (mf.transform.childCount == 0)
                    {
                        Destroy(mf.gameObject);
                        metricas.objetosDesativados++;
                    }
                    else
                    {
                        MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                        if (mr != null) Destroy(mr);
                        Destroy(mf);
                        metricas.objetosDesativados++;
                    }
                }
            }
        }

        private static void LimparNosFolhaVazios(GameObject raiz, HashSet<Transform> protegidos, ref MetricasOtimizacao metricas)
        {
            Transform[] todos = raiz.GetComponentsInChildren<Transform>(true);
            // Itera de trás para frente para processar folhas antes de nós pais
            for (int i = todos.Length - 1; i >= 0; i--)
            {
                Transform t = todos[i];
                if (t == null || t == raiz.transform) continue;
                if (protegidos.Contains(t)) continue;

                // Não mexer se tiver scripts
                if (t.GetComponent<MonoBehaviour>() != null) continue;

                // Não mexer se tiver filhos ativos
                if (t.childCount > 0)
                {
                    bool temFilhoAtivo = false;
                    for (int c = 0; c < t.childCount; c++)
                    {
                        if (t.GetChild(c).gameObject.activeSelf)
                        {
                            temFilhoAtivo = true;
                            break;
                        }
                    }
                    if (temFilhoAtivo) continue;
                }

                // Verifica se tem outros componentes úteis (Renderers, Colliders ativos, etc)
                Component[] comps = t.GetComponents<Component>();
                // Todo GameObject tem pelo menos Transform (1 componente)
                bool apenasTransformOuInativo = true;
                for (int c = 0; c < comps.Length; c++)
                {
                    Component comp = comps[c];
                    if (comp == null || comp is Transform) continue;

                    if (comp is Renderer r && r.enabled)
                    {
                        apenasTransformOuInativo = false;
                        break;
                    }
                    if (comp is Collider col && col.enabled)
                    {
                        apenasTransformOuInativo = false;
                        break;
                    }
                    if (comp is Light l && l.enabled)
                    {
                        apenasTransformOuInativo = false;
                        break;
                    }
                    if (comp is Camera || comp is AudioSource || comp is ParticleSystem)
                    {
                        apenasTransformOuInativo = false;
                        break;
                    }
                }

                if (apenasTransformOuInativo && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    metricas.objetosDesativados++;
                }
            }
        }

        /// <summary>
        /// Aplica com segurança a otimização em um GameObject alvo (Cidade ou Navio)
        /// retornando se a operação foi executada.
        /// </summary>
        public static bool OtimizarAlvo(GameObject alvo)
        {
            if (alvo == null) return false;
            OtimizadorHierarquiaCena otim = alvo.GetComponent<OtimizadorHierarquiaCena>();
            if (otim == null)
            {
                otim = alvo.AddComponent<OtimizadorHierarquiaCena>();
            }
            otim.ExecutarOtimizacao();
            return true;
        }
    }
}
