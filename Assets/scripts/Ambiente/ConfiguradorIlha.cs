using System.Collections;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Adicione este script no GameObject raiz de qualquer novo Terrain ou ilha.
/// Ele garante automaticamente que:
/// 1. O MarcadorSuperficieMapa (Chao) esteja presente e correto
/// 2. O NavMesh esteja bakeado para a área (no Editor)
/// 3. As unidades terrestres possam caminhar aqui
/// </summary>
[RequireComponent(typeof(MarcadorSuperficieMapa))]
[AddComponentMenu("Hegemonia/Ambiente/Configurador de Ilha")]
public class ConfiguradorIlha : MonoBehaviour
{
    [Header("Diagnóstico em Runtime")]
    [Tooltip("Ativa logs de diagnóstico no Console durante o jogo")]
    public bool modoDiagnostico = false;

    [Header("Auto-Correção de Unidades")]
    [Tooltip("Tenta mover unidades presas fora do NavMesh de volta para dentro")]
    public bool autocorrigirUnidadesPresos = true;

    [Tooltip("A cada quantos segundos verifica unidades presas (0 = desativado)")]
    public float intervaloVerificacao = 5f;

    [Header("Raio de Busca NavMesh")]
    [Tooltip("Distância máxima para encontrar um ponto válido no NavMesh ao teleportar uma unidade presa")]
    public float raioRecuperacaoNavMesh = 50f;

    private MarcadorSuperficieMapa _marcador;

    private void Reset()
    {
        // Garante que o Marcador seja configurado como Chao ao adicionar o componente
        MarcadorSuperficieMapa m = GetComponent<MarcadorSuperficieMapa>();
        if (m != null)
        {
            // Usa serialização via SerializedObject no Editor para setar o campo privado
#if UNITY_EDITOR
            SerializedObject so = new SerializedObject(m);
            SerializedProperty tipoProp = so.FindProperty("tipoSuperficie");
            if (tipoProp != null)
            {
                tipoProp.enumValueIndex = 1; // 1 = Chao
                so.ApplyModifiedProperties();
            }
#endif
        }
    }

    private void Awake()
    {
        _marcador = GetComponent<MarcadorSuperficieMapa>();

        // Garante que o marcador seja Chao (defesa em runtime)
        if (_marcador == null)
        {
            _marcador = gameObject.AddComponent<MarcadorSuperficieMapa>();
        }

        _marcador.DefinirTipo(TipoSuperficieMapa.Chao);

        LogDiagnostico($"[ConfiguradorIlha] '{gameObject.name}' inicializado. Tipo: {_marcador.TipoSuperficie}");
    }

    private void Start()
    {
        // Verifica se o NavMesh cobre esta ilha
        VerificarNavMeshCobertura();

        // Inicia verificação periódica de unidades presas (se configurado)
        if (autocorrigirUnidadesPresos && intervaloVerificacao > 0f)
        {
            InvokeRepeating(nameof(RecuperarUnidadesPresosFora), intervaloVerificacao, intervaloVerificacao);
        }
    }

    private void VerificarNavMeshCobertura()
    {
        // Pega o centro do bounds do terrain/marcador
        if (!_marcador.HasBounds)
        {
            LogDiagnostico("[ConfiguradorIlha] AVISO: MarcadorSuperficieMapa sem bounds válidos. " +
                           "O Terrain precisa ter um Collider ou Renderer.");
            return;
        }

        Vector3 centroIlha = _marcador.Bounds.center;
        NavMeshHit hit;

        // Testa se o centro da ilha está coberto pelo NavMesh
        bool coberto = NavMesh.SamplePosition(centroIlha, out hit, 20f, NavMesh.AllAreas);

        if (coberto)
        {
            LogDiagnostico($"[ConfiguradorIlha] '{gameObject.name}' | NavMesh OK ✓ | " +
                           $"Ponto NavMesh encontrado em {hit.position}");
        }
        else
        {
            Debug.LogWarning($"[ConfiguradorIlha] '{gameObject.name}' | NavMesh NÃO BAKEADO nesta área! ⚠️\n" +
                             $"→ Centro da ilha: {centroIlha}\n" +
                             $"→ SOLUÇÃO: No Editor, vá em Window > AI > Navigation e clique em 'Bake'.\n" +
                             $"→ Certifique-se que o Terrain está marcado como 'Navigation Static'.", this);

#if UNITY_EDITOR
            // No Editor, oferece bake automático
            if (!Application.isPlaying)
            {
                // Em versões novas do Unity, o Bake via script no Editor mudou levemente
                #if UNITY_EDITOR
                #pragma warning disable 0618
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
                #pragma warning restore 0618
                #endif
                LogDiagnostico("[ConfiguradorIlha] NavMesh rebakeado automaticamente no Editor.");
            }
#endif
        }
    }

    /// <summary>
    /// Verifica todas as unidades terrestres na área desta ilha e tenta recuperar
    /// aquelas que estão fora do NavMesh.
    /// </summary>
    public void RecuperarUnidadesPresosFora()
    {
        if (!_marcador.HasBounds) return;

        Bounds area = _marcador.Bounds;

        // Busca todos os NavMeshAgents na cena
        NavMeshAgent[] agentes = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        int recuperados = 0;

        for (int i = 0; i < agentes.Length; i++)
        {
            NavMeshAgent agente = agentes[i];
            if (agente == null || !agente.enabled || !agente.gameObject.activeInHierarchy) continue;

            // Verifica se esta unidade está dentro dos bounds desta ilha
            if (!area.Contains(agente.transform.position)) continue;

            // Se a unidade está na área mas fora do NavMesh: tenta recuperar
            if (!agente.isOnNavMesh)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(agente.transform.position, out hit, raioRecuperacaoNavMesh, NavMesh.AllAreas))
                {
                    agente.Warp(hit.position);
                    recuperados++;
                    LogDiagnostico($"[ConfiguradorIlha] Unidade '{agente.name}' recuperada → {hit.position}");
                }
                else
                {
                    LogDiagnostico($"[ConfiguradorIlha] AVISO: Não foi possível recuperar '{agente.name}'. " +
                                   $"NavMesh não cobre a posição {agente.transform.position}. Rebake necessário.");
                }
            }
        }

        if (recuperados > 0)
        {
            LogDiagnostico($"[ConfiguradorIlha] {recuperados} unidade(s) recuperadas na ilha '{gameObject.name}'.");
        }
    }

    private void LogDiagnostico(string mensagem)
    {
        if (modoDiagnostico)
        {
            Debug.Log(mensagem, this);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Diagnóstico: Verificar NavMesh desta Ilha")]
    private void DiagnosticoEditor()
    {
        VerificarNavMeshCobertura();
    }

    [ContextMenu("Ação: Rebakear NavMesh da Cena Inteira")]
    private void RebakearNavMesh()
    {
        #pragma warning disable 0618
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        #pragma warning restore 0618
        Debug.Log("[ConfiguradorIlha] NavMesh rebakeado com sucesso! ✓");
    }

    [ContextMenu("Ação: Recuperar Unidades Presas Agora")]
    private void RecuperarUnidadesEditor()
    {
        RecuperarUnidadesPresosFora();
    }

    private void OnDrawGizmosSelected()
    {
        // Mostra visualmente a cobertura que esta ilha tem no NavMesh
        if (_marcador == null) _marcador = GetComponent<MarcadorSuperficieMapa>();
        if (_marcador == null || !_marcador.HasBounds) return;

        Bounds b = _marcador.Bounds;

        // Testa 9 pontos em grid para ver quais estão no NavMesh
        int divisoes = 3;
        float passoX = b.size.x / divisoes;
        float passoZ = b.size.z / divisoes;

        for (int x = 0; x <= divisoes; x++)
        {
            for (int z = 0; z <= divisoes; z++)
            {
                Vector3 ponto = new Vector3(
                    b.min.x + passoX * x,
                    b.center.y + 5f,
                    b.min.z + passoZ * z
                );

                NavMeshHit hit;
                bool noNavMesh = NavMesh.SamplePosition(ponto, out hit, 10f, NavMesh.AllAreas);

                Gizmos.color = noNavMesh ? new Color(0f, 1f, 0f, 0.8f) : new Color(1f, 0f, 0f, 0.8f);
                Gizmos.DrawSphere(noNavMesh ? hit.position + Vector3.up * 0.5f : ponto, 1.5f);
            }
        }
    }
#endif
}
